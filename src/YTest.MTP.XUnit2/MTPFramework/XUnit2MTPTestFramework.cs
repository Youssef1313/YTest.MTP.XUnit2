using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Requests;
using Xunit;
using Xunit.Abstractions;
using YTest.MTP.XUnit2.Filter;

namespace YTest.MTP.XUnit2;

internal sealed class XUnit2MTPTestFramework : Microsoft.Testing.Platform.Extensions.TestFramework.ITestFramework, IDataProducer
{
    private readonly XUnit2MTPTestTrxCapability _trxReportCapability;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<XUnit2MTPTestFramework> _logger;

    public XUnit2MTPTestFramework(
        XUnit2MTPTestTrxCapability trxReportCapability,
        ICommandLineOptions commandLineOptions,
        ILoggerFactory loggerFactory)
    {
        _trxReportCapability = trxReportCapability;
        _commandLineOptions = commandLineOptions;
        _loggerFactory = loggerFactory;

        _logger = loggerFactory.CreateLogger<XUnit2MTPTestFramework>();
    }

    public string Uid => nameof(XUnit2MTPTestFramework);

    public string Version => "1.0.0";

    public string DisplayName => "XUnit 2 Microsoft.Testing.Platform adapter";

    public string Description => DisplayName;

    public Type[] DataTypesProduced { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
    ];

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        var assembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("XUnit2 MTP adapter cannot work when GetEntryAssembly returns null.");

        var assemblyPath = assembly.Location;

#if !NETFRAMEWORK
        if (OperatingSystem.IsWindows())
#endif
        {
            // Change .exe to .dll
            assemblyPath = Path.ChangeExtension(assemblyPath, "dll");
        }

        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("XUnit2 MTP adapter cannot find the test assembly.", assemblyPath);
        }

        TestCaseFilterExpression? filter = null;
        if (_commandLineOptions.TryGetOptionArgumentList(XUnit2MTPCommandLineProvider.FilterOptionName, out string[]? filterValue) &&
            filterValue.Length == 1)
        {
            filter = new TestCaseFilterExpression(new FilterExpressionWrapper(filterValue[0]));
        }

        await (context.Request switch 
        {
            DiscoverTestExecutionRequest discoverRequest => DiscoverTestsAsync(discoverRequest, context, assemblyPath, filter),
            RunTestExecutionRequest runRequest => RunTestsAsync(runRequest, context, assemblyPath, filter),
            _ => throw new NotSupportedException($"Request type '{context.Request.GetType().FullName}' is not supported by XUnit2 MTP adapter."),
        });

        context.Complete();
    }

    private async Task DiscoverTestsAsync(
        DiscoverTestExecutionRequest discoverRequest,
        ExecuteRequestContext context,
        string assemblyPath,
        TestCaseFilterExpression? filter)
    {
        var configuration = GetConfiguration(assemblyPath);
        
        using var frontController = GetFrontController(assemblyPath, configuration);

        var testCases = await DiscoverAsync(frontController, configuration, context.CancellationToken);

        foreach (ITestCase test in testCases)
        {
            if (!MatchesFilter(discoverRequest.Filter, filter, test))
            {
                continue;
            }

            var testNode = new TestNode()
            {
                Uid = test.UniqueID,
                DisplayName = test.DisplayName,
                Properties = new PropertyBag(),
            };

            testNode.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);

            string typeFQN = test.TestMethod.Method.Type.Name;
            string typeFQNWithoutNamespace = typeFQN;
            string @namespace = string.Empty;
            int lastIndexOfDot = typeFQN.LastIndexOf('.');
            if (lastIndexOfDot >= 0)
            {
                @namespace = typeFQN.Substring(0, lastIndexOfDot);
                typeFQNWithoutNamespace = typeFQN.Substring(lastIndexOfDot + 1);
            }

            // TODO: Parameter types.
            testNode.Properties.Add(new TestMethodIdentifierProperty(
                assemblyFullName: test.TestMethod.TestClass.TestCollection.TestAssembly.Assembly.Name,
                @namespace: @namespace,
                typeName: typeFQNWithoutNamespace,
                methodName: test.TestMethod.Method.Name,
                methodArity: test.TestMethod.Method.GetGenericArguments().Count(),
                parameterTypeFullNames: [],
                returnTypeFullName: test.TestMethod.Method.ReturnType.Name));

            if (!string.IsNullOrEmpty(test.SourceInformation.FileName))
            {
                var linePosition = new LinePosition(test.SourceInformation.LineNumber ?? -1, -1);
                testNode.Properties.Add(new TestFileLocationProperty(test.SourceInformation.FileName, new LinePositionSpan(linePosition, linePosition)));
            }

            foreach (var trait in test.Traits)
            {
                foreach (var traitValue in trait.Value)
                {
                    testNode.Properties.Add(new TestMetadataProperty(trait.Key, traitValue));
                }

                // Mimics https://github.com/xunit/xunit/blob/4ade48a7e65aa916a20b11d38da0ec127454bf80/src/common/MicrosoftTestingPlatform/TestNodeExtensions.cs#L65-L66
                if (_trxReportCapability.IsEnabled && test.Traits.Comparer.Equals(trait.Key, "category"))
                {
                    testNode.Properties.Add(new TrxCategoriesProperty(trait.Value.ToArray()));
                }
            }

            var testNodeUpdateMessage = new TestNodeUpdateMessage(context.Request.Session.SessionUid, testNode);
            await context.MessageBus.PublishAsync(this, testNodeUpdateMessage);
        }
    }

    private async Task RunTestsAsync(
        RunTestExecutionRequest runRequest,
        ExecuteRequestContext context,
        string assemblyPath,
        TestCaseFilterExpression? filter)
    {
        var configuration = GetConfiguration(assemblyPath);
        using var frontController = GetFrontController(assemblyPath, configuration);
        var testCases = await DiscoverAsync(frontController, configuration, context.CancellationToken);

        var assemblyDisplayName = Path.GetFileNameWithoutExtension(assemblyPath);
        var executionSinkOptions = new ExecutionSinkOptions
        {
            DiagnosticMessageSink = new MTPDiagnosticMessageSink(_loggerFactory, assemblyDisplayName, configuration.DiagnosticMessagesOrDefault),
            FailSkips = configuration.FailSkipsOrDefault,
            LongRunningTestTime = TimeSpan.FromSeconds(configuration.LongRunningTestSecondsOrDefault),
        };

        var executionSink = new ExecutionSink(new MTPExecutionSink(this, context, _trxReportCapability.IsEnabled), executionSinkOptions);
        frontController.RunTests(testCases.Where(tc => MatchesFilter(runRequest.Filter, filter, tc)), executionSink, TestFrameworkOptions.ForExecution(configuration));

        await Task.Factory.StartNew(executionSink.Finished.WaitOne, TaskCreationOptions.LongRunning);

        // TODO: SessionFileArtifact
    }

    private static async Task<List<ITestCase>> DiscoverAsync(XunitFrontController frontController, TestAssemblyConfiguration configuration, CancellationToken cancellationToken)
    {
        using var sink = new TestDiscoverySink(() => cancellationToken.IsCancellationRequested);
        frontController.Find(includeSourceInformation: true, sink, TestFrameworkOptions.ForDiscovery(configuration));
        await Task.Factory.StartNew(sink.Finished.WaitOne, TaskCreationOptions.LongRunning);
        return sink.TestCases;
    }

    private static bool MatchesFilter(ITestExecutionFilter mtpFilter, TestCaseFilterExpression? vstestFilter, ITestCase test)
    {
        if (vstestFilter is not null)
        {
            var vsTestMatches = vstestFilter.MatchTestCase(propertyName =>
            {
                if (string.Equals(propertyName, "FullyQualifiedName", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{test.TestMethod.TestClass.Class.Name}.{test.TestMethod.Method.Name}";
                }
                else if (string.Equals(propertyName, "DisplayName", StringComparison.OrdinalIgnoreCase))
                {
                    return test.DisplayName;
                }

                _ = test.Traits.TryGetValue(propertyName, out var values);
                return values?.ToArray();
            });

            if (!vsTestMatches)
            {
                return false;
            }
        }

        return mtpFilter switch
        {
#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            null or NopFilter => true,
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            TestNodeUidListFilter testNodeUidListFilter => testNodeUidListFilter.TestNodeUids.Contains(new TestNodeUid(test.UniqueID)),
            _ => throw new NotSupportedException($"Filter type '{mtpFilter.GetType().FullName}' is not supported by XUnit2 MTP adapter."),
        };
    }

    private TestAssemblyConfiguration GetConfiguration(string assemblyPath)
    {
        var warnings = new List<string>();
        var configuration = ConfigReader.Load(assemblyPath, configFileName: null, warnings);

        // This similar to:
        // 1. https://github.com/xunit/xunit/blob/4ade48a7e65aa916a20b11d38da0ec127454bf80/src/common/MicrosoftTestingPlatform/TestPlatformTestFramework.cs#L156-L158
        // 2. https://github.com/xunit/visualstudio.xunit/blob/d693866207d8c1b3269d1b7f4f62211b82ba7835/src/xunit.runner.visualstudio/VsTestRunner.cs#L210-L212
        // 3. https://github.com/xunit/visualstudio.xunit/blob/d693866207d8c1b3269d1b7f4f62211b82ba7835/src/xunit.runner.visualstudio/VsTestRunner.cs#L497-L499
        configuration.PreEnumerateTheories ??= true;

        foreach (var warning in warnings)
        {
            _logger.LogWarning(warning);
        }

        return configuration;
    }

    private static XunitFrontController GetFrontController(string assemblyPath, TestAssemblyConfiguration configuration)
        => new XunitFrontController(
            configuration.AppDomainOrDefault,
            assemblyPath,
            configFileName: null,
            configuration.ShadowCopyOrDefault,
            shadowCopyFolder: null,
            sourceInformationProvider: null,
            diagnosticMessageSink: null);
}
