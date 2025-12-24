using Microsoft.Testing.Platform.Logging;
using Xunit;
using Xunit.Abstractions;

namespace YTest.MTP.XUnit2;

internal sealed class MTPDiagnosticMessageSink : LongLivedMarshalByRefObject, IMessageSink
{
    private readonly ILogger<MTPDiagnosticMessageSink> _logger;
    private readonly string _header;
    private readonly bool _showDiagnostics;

    public MTPDiagnosticMessageSink(
        ILoggerFactory loggerFactory,
        string? assemblyDisplayName = null,
        bool showDiagnostics = false)
    {
        _logger = loggerFactory.CreateLogger<MTPDiagnosticMessageSink>();
        _header = assemblyDisplayName is null ? string.Empty : assemblyDisplayName + ": ";
        _showDiagnostics = showDiagnostics;
    }

    public bool OnMessage(IMessageSinkMessage message)
    {
        if (!_showDiagnostics)
        {
            return true;
        }

        if (message is IDiagnosticMessage diagMessage)
        {
            _logger.LogWarning($"{_header}{diagMessage.Message}");
        }
        else if (message is IErrorMessage errorMessage)
        {
            var exception = new XUnitFailureException(errorMessage);
            _logger.LogError(exception);
        }

        return true;
    }
}
