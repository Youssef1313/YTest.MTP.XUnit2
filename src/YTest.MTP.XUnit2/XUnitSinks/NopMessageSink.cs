using Xunit;
using Xunit.Abstractions;

namespace YTest.MTP.XUnit2;

internal sealed class NopMessageSink : LongLivedMarshalByRefObject, IMessageSink
{
    private NopMessageSink()
    {    
    }

    public static IMessageSink Instance { get; } = new NopMessageSink();

    public bool OnMessage(IMessageSinkMessage message) => true;
}
