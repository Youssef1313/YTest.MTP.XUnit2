using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace YTest.MTP.XUnit2;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal sealed class BannerMessageOwnerCapability : IBannerMessageOwnerCapability
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    public Task<string?> GetBannerMessageAsync() =>
        Task.FromResult<string?>(string.Format(
            CultureInfo.CurrentCulture,
            "YTest.MTP.XUnit2 Runner version {0} ({1}-bit {2}) {3}",
            XUnit2MTPExtension.Instance.Version,
            IntPtr.Size * 8,
            RuntimeInformation.FrameworkDescription,
            Environment.NewLine
        ));
}
