using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Xunit;
using Xunit.Abstractions;
using YTest.MTP.XUnit2.Filter;

namespace YTest.MTP.XUnit2;

internal sealed class XUnit2MTPTestFramework : Microsoft.Testing.Platform.Extensions.TestFramework.ITestFramework, IDataProducer
{
    private readonly XUnit2MTPTestTrxCapability _trxReportCapability;
    private readonly ICommandLineOptions _commandLineOptions;

    public XUnit2MTPTestFramework(XUnit2MTPTestTrxCapability trxReportCapability, ICommandLineOptions commandLineOptions)
    {
        _trxReportCapability = trxReportCapability;
        _commandLineOptions = commandLineOptions;
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

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult() { IsSuccess = true });

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult() { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        var assembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("XUnit2 MTP adapter cannot work when GetEntryAssembly returns null.");

        var assemblyPath = assembly.Location;
#if NETFRAMEWORK
        // Change .exe to .dll
        assemblyPath = Path.ChangeExtension(assemblyPath, "dll");
#else
        if (OperatingSystem.IsWindows())
        {
            // Change .exe to .dll
            assemblyPath = Path.ChangeExtension(assemblyPath, "dll");
        }
        else
        {
            assemblyPath += ".dll";
        }
#endif

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

    private async Task DiscoverTestsAsync(
        DiscoverTestExecutionRequest discoverRequest,
        ExecuteRequestContext context,
        string assemblyPath,
        TestCaseFilterExpression? filter)
    {
        // TODO: Expose warnings.
        var configuration = ConfigReader.Load(assemblyPath, configFileName: null, warnings: null);
        configuration.PreEnumerateTheories ??= true;

        using var frontController = new XunitFrontController(
            configuration.AppDomainOrDefault,
            assemblyPath,
            configFileName: null,
            configuration.ShadowCopyOrDefault,
            shadowCopyFolder: null,
            sourceInformationProvider: null,
            diagnosticMessageSink: null);

        var sink = new TestDiscoverySink();
        frontController.Find(includeSourceInformation: true, sink, TestFrameworkOptions.ForDiscovery(configuration));

        // TODO: This might be blocking a threadpool thread. This is not good. It's better to implement our own sink that is fully async.
        sink.Finished.WaitOne();

        foreach (ITestCase test in sink.TestCases)
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
        // TODO: Expose warnings.
        var configuration = ConfigReader.Load(assemblyPath, configFileName: null, warnings: null);
        configuration.PreEnumerateTheories ??= true;

        using var frontController = new XunitFrontController(
            configuration.AppDomainOrDefault,
            assemblyPath,
            configFileName: null,
            configuration.ShadowCopyOrDefault,
            shadowCopyFolder: null,
            sourceInformationProvider: null,
            diagnosticMessageSink: null);

        var sink = new TestDiscoverySink();
        frontController.Find(includeSourceInformation: true, sink, TestFrameworkOptions.ForDiscovery(configuration));

        // TODO: This might be blocking a threadpool thread. This is not good. It's better to implement our own sink that is fully async.
        sink.Finished.WaitOne();

        var executionSinkOptions = new ExecutionSinkOptions
        {
            DiagnosticMessageSink = NopMessageSink.Instance,
            FailSkips = configuration.FailSkipsOrDefault,
            LongRunningTestTime = TimeSpan.FromSeconds(configuration.LongRunningTestSecondsOrDefault),
        };

        var executionSink = new ExecutionSink(new MTPExecutionSink(this, context, _trxReportCapability.IsEnabled), executionSinkOptions);

        frontController.RunTests(sink.TestCases.Where(tc => MatchesFilter(runRequest.Filter, filter, tc)), executionSink, TestFrameworkOptions.ForExecution(configuration));

        executionSink.Finished.WaitOne();

        // TODO: SessionFileArtifact
    }

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(true);
}
