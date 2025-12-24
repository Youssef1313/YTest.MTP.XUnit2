using System;
using Xunit;
using Xunit.Abstractions;

namespace YTest.MTP.XUnit2;

internal sealed class XUnitFailureException : Exception
{
    public XUnitFailureException(IFailureInformation failureInformation)
        : base(ExceptionUtility.CombineMessages(failureInformation))
    {
        StackTrace = ExceptionUtility.CombineStackTraces(failureInformation);
    }

    public override string StackTrace { get; }
}
