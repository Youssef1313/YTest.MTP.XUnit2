using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace YTest.MTP.XUnit2;

internal class XUnit2MTPCommandLineProvider : ICommandLineOptionsProvider
{
    public const string FilterOptionName = "filter";

    private readonly IReadOnlyCollection<CommandLineOption> _commandLineOptions =
    [
        new CommandLineOption(FilterOptionName, "Provides VSTest filter support", ArgumentArity.ExactlyOne, isHidden: false),
    ];

    public string Uid => nameof(XUnit2MTPCommandLineProvider);

    public string Version => "1.0.0";

    public string DisplayName => "XUnit2 MTP Command Line Provider";

    public string Description => DisplayName;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => _commandLineOptions;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(true);

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;
}
