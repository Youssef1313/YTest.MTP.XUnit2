using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace YTest.MTP.XUnit2;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into MTP via the generated entry-point.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds xUnit 2 support.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder on which registering xunit 2.</param>
    /// <param name="arguments">The test application cli arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] arguments)
    {
        // TODO: Add support for VSTest's --filter.
        var trxReportCapability = new XUnit2MTPTestTrxCapability();
        testApplicationBuilder.RegisterTestFramework(
            capabilitiesFactory: _ => new TestFrameworkCapabilities(trxReportCapability),
            frameworkFactory: (_, _) => new XUnit2MTPTestFramework(trxReportCapability));
    }
}
