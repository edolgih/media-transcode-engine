using Transcode.Cli.Core.Parsing;
using Transcode.Core.Scenarios;

namespace Transcode.Cli.Core.Scenarios;

/*
Это CLI-контракт одного прикладного сценария:
его имя, help, scenario-local parsing, создание runtime-сценария и обработка ошибок.
*/
/// <summary>
/// Defines the CLI-facing contract for one registered application scenario.
/// </summary>
public interface ICliScenarioHandler
{
    /// <summary>
    /// Gets the stable scenario name used by the CLI.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets legacy command tokens that should redirect users to <c>--scenario</c>.
    /// </summary>
    IReadOnlyList<string> LegacyCommandTokens { get; }

    /// <summary>
    /// Gets help rows for scenario-specific CLI options.
    /// </summary>
    IReadOnlyList<CliHelpOption> HelpOptions { get; }

    /// <summary>
    /// Returns scenario-specific command examples for CLI help text.
    /// </summary>
    /// <param name="exeName">Executable name used in rendered examples.</param>
    /// <returns>Scenario-specific examples.</returns>
    IReadOnlyList<string> GetHelpExamples(string exeName);

    /// <summary>
    /// Parses raw scenario-specific CLI arguments into a normalized scenario input object.
    /// </summary>
    /// <param name="args">Raw scenario-specific arguments.</param>
    /// <param name="scenarioInput">Normalized scenario-specific input object on success.</param>
    /// <param name="errorText">Error message on failure.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    bool TryParse(IReadOnlyList<string> args, out object scenarioInput, out string? errorText);

    /// <summary>
    /// Creates the runtime scenario instance for the supplied CLI request.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <returns>Scenario instance used to build the transcode plan.</returns>
    TranscodeScenario CreateScenario(CliTranscodeRequest request);

    /// <summary>
    /// Maps an exception to scenario-specific CLI failure output.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <param name="exception">Exception raised during processing.</param>
    /// <returns>Scenario-specific failure description.</returns>
    CliScenarioFailure DescribeFailure(CliTranscodeRequest request, Exception exception);
}
