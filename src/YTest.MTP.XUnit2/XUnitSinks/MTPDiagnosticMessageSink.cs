using System;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Xunit;
using Xunit.Abstractions;

namespace YTest.MTP.XUnit2;

internal sealed class MTPDiagnosticMessageSink : LongLivedMarshalByRefObject, IMessageSink
{
    private readonly Action<IOutputDeviceData> _displayOutput;
    private readonly ILogger<MTPDiagnosticMessageSink> _logger;
    private readonly string _header;
    private readonly bool _showDiagnostics;

    public MTPDiagnosticMessageSink(
        Action<IOutputDeviceData> displayOutput,
        ILoggerFactory loggerFactory,
        string? assemblyDisplayName = null,
        bool showDiagnostics = false)
    {
        _displayOutput = displayOutput;
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
            string warning = $"{_header}{diagMessage.Message}";
            _logger.LogWarning(warning);
            _displayOutput(new WarningMessageOutputDeviceData(warning));
        }
        else if (message is IErrorMessage errorMessage)
        {
            var exception = new XUnitFailureException(errorMessage);
            _logger.LogError(exception);
            _displayOutput(new ErrorMessageOutputDeviceData(exception.ToString()));
        }

        return true;
    }
}
