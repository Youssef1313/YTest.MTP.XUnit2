using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace YTest.MTP.XUnit2;

internal sealed class XUnit2MTPTestTrxCapability : ITrxReportCapability
{
    public bool IsSupported => true;

    public bool IsEnabled { get; private set; }

    public void Enable() => IsEnabled = true;
}
