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

    public MTPExecutionSink(IDataProducer dataProducer, ExecuteRequestContext executeRequestContext, bool isTrxEnabled)
    {
        _dataProducer = dataProducer;
        _executeRequestContext = executeRequestContext;
        _isTrxEnabled = isTrxEnabled;

        Execution.TestCaseStartingEvent += OnTestCaseStarting;
        Execution.TestFailedEvent += OnTestFailed;
        Execution.TestPassedEvent += OnTestPassed;
        Execution.TestSkippedEvent += OnTestSkipped;
        Execution.TestCaseCleanupFailureEvent += OnTestCaseCleanupFailure;

        // TODO: Figure out how to report FileArtifactProperty, if attachments is a thing in xunit 2 at all.
    }

    private void OnTestCaseStarting(MessageHandlerArgs<ITestCaseStarting> args)
    {
        var testNode = CreateTestNode(args.Message);
        testNode.Properties.Add(InProgressTestNodeStateProperty.CachedInstance);
        PublishTestUpdate(testNode);
    }

    private void OnTestFailed(MessageHandlerArgs<ITestFailed> args)
    {
        var testNode = CreateTestNode(args.Message);
        var failureException = new XUnitFailureException(args.Message);
        testNode.Properties.Add(new FailedTestNodeStateProperty(failureException));
        if (_isTrxEnabled)
        {
            testNode.Properties.Add(new TrxExceptionProperty(failureException.Message, failureException.StackTrace));
        }

        PublishTestUpdate(testNode);
    }

    private void OnTestPassed(MessageHandlerArgs<ITestPassed> args)
    {
        var testNode = CreateTestNode(args.Message);
        testNode.Properties.Add(PassedTestNodeStateProperty.CachedInstance);

        PublishTestUpdate(testNode);
    }

    private void OnTestSkipped(MessageHandlerArgs<ITestSkipped> args)
    {
        var testNode = CreateTestNode(args.Message);
        testNode.Properties.Add(new SkippedTestNodeStateProperty(args.Message.Reason));
        PublishTestUpdate(testNode);
    }

    private void OnTestCaseCleanupFailure(MessageHandlerArgs<ITestCaseCleanupFailure> args)
    {
    }

    private void PublishTestUpdate(TestNode testNode)
    {
        var testNodeUpdateMessage = new TestNodeUpdateMessage(_executeRequestContext.Request.Session.SessionUid, testNode);
        _executeRequestContext.MessageBus.PublishAsync(_dataProducer, testNodeUpdateMessage).GetAwaiter().GetResult();
    }

    private TestNode CreateTestNode(ITestCaseMessage testMessage)
    {
        var testNode = new TestNode()
        {
            Uid = testMessage.TestCase.UniqueID,
            DisplayName = testMessage.TestCase.DisplayName,
            Properties = new PropertyBag(),
        };

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

            if (_isTrxEnabled)
            {
                testNode.Properties.Add(new TrxFullyQualifiedTypeNameProperty(testMessage.TestCase.TestMethod.TestClass.Class.Name));
            }
        }

        return testNode;
    }
}
