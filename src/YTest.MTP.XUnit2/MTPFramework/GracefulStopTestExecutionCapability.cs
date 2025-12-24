using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace YTest.MTP.XUnit2;

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal sealed class GracefulStopTestExecutionCapability : IGracefulStopTestExecutionCapability
#pragma warning restore TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
{
    public bool IsGracefulStopRequested { get; private set; }

    public Task StopTestExecutionAsync(CancellationToken cancellationToken)
    {
        IsGracefulStopRequested = true;
        return Task.CompletedTask;
    }
}
