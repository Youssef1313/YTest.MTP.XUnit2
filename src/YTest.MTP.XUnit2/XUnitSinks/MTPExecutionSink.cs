using System;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace YTest.MTP.XUnit2;

internal sealed partial class MTPExecutionSink : TestMessageSink
{
    private readonly IDataProducer _dataProducer;
    private readonly ExecuteRequestContext _executeRequestContext;
    private readonly bool _isTrxEnabled;
    private readonly GracefulStopTestExecutionCapability _gracefulStopTestExecutionCapability;

    public MTPExecutionSink(IDataProducer dataProducer, ExecuteRequestContext executeRequestContext, bool isTrxEnabled, GracefulStopTestExecutionCapability gracefulStopTestExecutionCapability)
    {
        _dataProducer = dataProducer;
        _executeRequestContext = executeRequestContext;
        _isTrxEnabled = isTrxEnabled;
        _gracefulStopTestExecutionCapability = gracefulStopTestExecutionCapability;

        Execution.TestCaseStartingEvent += OnTestCaseStarting;
        Execution.TestFailedEvent += OnTestFailed;
        Execution.TestPassedEvent += OnTestPassed;
        Execution.TestSkippedEvent += OnTestSkipped;
        Execution.TestCaseCleanupFailureEvent += OnTestCaseCleanupFailure;
        Execution.TestClassCleanupFailureEvent += OnTestClassCleanupFailure;
        Execution.TestAssemblyCleanupFailureEvent += OnTestAssemblyCleanupFailure;
        Execution.TestCleanupFailureEvent += OnTestCleanupFailure;
        Execution.TestCollectionCleanupFailureEvent += OnTestCollectionCleanupFailure;
        Execution.TestMethodCleanupFailureEvent += OnTestMethodCleanupFailure;

        // TODO: Figure out how to report FileArtifactProperty, if attachments is a thing in xunit 2 at all - https://github.com/Youssef1313/YTest.MTP.XUnit2/issues/4
    }

    private void OnTestCaseStarting(MessageHandlerArgs<ITestCaseStarting> args)
    {
        var testNode = CreateTestNode(args.Message);
        testNode.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
        PublishTestUpdate(testNode);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestFailed(MessageHandlerArgs<ITestFailed> args)
    {
        OnFailure(null, args.Message, args.Message);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestPassed(MessageHandlerArgs<ITestPassed> args)
    {
        var testNode = CreateTestNode(args.Message);
        testNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);

        PublishTestUpdate(testNode);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestSkipped(MessageHandlerArgs<ITestSkipped> args)
    {
        var testNode = CreateTestNode(args.Message);
        testNode.Properties.Add(new SkippedTestNodeStateProperty(args.Message.Reason));
        PublishTestUpdate(testNode);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestClassCleanupFailure(MessageHandlerArgs<ITestClassCleanupFailure> args)
    {
        OnCleanupFailure($"Test Class Cleanup Failure ({args.Message.TestClass.Class.Name})", args.Message);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestAssemblyCleanupFailure(MessageHandlerArgs<ITestAssemblyCleanupFailure> args)
    {
        OnCleanupFailure($"Test Assembly Cleanup Failure ({args.Message.TestAssembly.Assembly.Name})", args.Message);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestCaseCleanupFailure(MessageHandlerArgs<ITestCaseCleanupFailure> args)
    {
        OnCleanupFailure($"Test Case Cleanup Failure ({args.Message.TestCase.DisplayName})", args.Message);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestMethodCleanupFailure(MessageHandlerArgs<ITestMethodCleanupFailure> args)
    {
        OnCleanupFailure($"Test Method Cleanup Failure ({args.Message.TestMethod.TestClass.Class.Name}.{args.Message.TestMethod.Method.Name})", args.Message);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestCollectionCleanupFailure(MessageHandlerArgs<ITestCollectionCleanupFailure> args)
    {
        OnCleanupFailure($"Test Collection Cleanup Failure ({args.Message.TestCollection.DisplayName})", args.Message);
        StopIfCancellationIsRequested(args);
    }

    private void OnTestCleanupFailure(MessageHandlerArgs<ITestCleanupFailure> args)
    {
        OnCleanupFailure($"Test Cleanup Failure ({args.Message.Test.DisplayName})", args.Message);
        StopIfCancellationIsRequested(args);
    }

    private void OnCleanupFailure<T>(string failureName, T message) where T : IFailureInformation, IExecutionMessage
    {
        foreach (var testCase in message.TestCases)
        {
            OnFailure(failureName, testCase, message);
        }
    }

    private void OnFailure(string? failureName, ITestCase testCase, IFailureInformation failureInformation)
    {
        var testNode = CreateTestNode(testCase);
        OnFailure(failureName, testNode, failureInformation);
    }

    private void OnFailure(string? failureName, ITestCaseMessage testCase, IFailureInformation failureInformation)
    {
        var testNode = CreateTestNode(testCase);
        OnFailure(failureName, testNode, failureInformation);
    }

    private void OnFailure(string? failureName, TestNode testNode, IFailureInformation failureInformation)
    {
        var failureException = failureName is null ? new XUnitFailureException(failureInformation) : new XUnitFailureException(failureInformation, failureName);
        testNode.Properties.Add(new FailedTestNodeStateProperty(failureException));
        if (_isTrxEnabled)
        {
            testNode.Properties.Add(new TrxExceptionProperty(failureException.Message, failureException.StackTrace));
        }

        PublishTestUpdate(testNode);
    }

    private void PublishTestUpdate(TestNode testNode)
    {
        var testNodeUpdateMessage = new TestNodeUpdateMessage(_executeRequestContext.Request.Session.SessionUid, testNode);
        _executeRequestContext.MessageBus.PublishAsync(_dataProducer, testNodeUpdateMessage).GetAwaiter().GetResult();
    }

    private TestNode CreateTestNode(ITestCase testCase)
    {
        var testNode = new TestNode()
        {
            Uid = testCase.UniqueID,
            DisplayName = testCase.DisplayName,
            Properties = new PropertyBag(),
        };

        if (_isTrxEnabled)
        {
            testNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(testCase.TestMethod.TestClass.Class.Name));
        }

        return testNode;
    }

    private TestNode CreateTestNode(ITestCaseMessage testMessage)
    {
        var testNode = CreateTestNode(testMessage.TestCase);

        if (testMessage is ITestResultMessage testResultMessage)
        {
            var endTime = DateTime.UtcNow;
            var duration = TimeSpan.FromSeconds((double)testResultMessage.ExecutionTime);
            testNode.Properties.Add(new TimingProperty(new TimingInfo(endTime.Subtract(duration), endTime, duration)));

            if (!string.IsNullOrEmpty(testResultMessage.Output))
            {
                if (_isTrxEnabled)
                {
                    testNode.Properties.Add(new TrxMessagesProperty([new StandardOutputTrxMessage(testResultMessage.Output)]));
                }

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                testNode.Properties.Add(new StandardOutputProperty(testResultMessage.Output));
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
        }

        return testNode;
    }

    private void StopIfCancellationIsRequested(MessageHandlerArgs args)
    {
        if (_executeRequestContext.CancellationToken.IsCancellationRequested || _gracefulStopTestExecutionCapability.IsGracefulStopRequested)
        {
            args.Stop();
        }
    }
}
