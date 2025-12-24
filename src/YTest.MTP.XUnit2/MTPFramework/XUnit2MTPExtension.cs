using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions;

namespace YTest.MTP.XUnit2;

internal sealed class XUnit2MTPExtension : IExtension
{
    private XUnit2MTPExtension()
    {
    }

    public static IExtension Instance { get; } = new XUnit2MTPExtension();

    public string Uid => nameof(XUnit2MTPTestFramework);

    public string Version => "1.0.0";

    public string DisplayName => "XUnit 2 Microsoft.Testing.Platform adapter";

    public string Description => DisplayName;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
