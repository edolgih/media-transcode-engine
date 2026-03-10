using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

namespace MediaTranscodeEngine.Cli.Scenarios;

/*
Этот реестр хранит зарегистрированные CLI-сценарии и дает общий доступ
к их именам, help-данным и lookup по имени или legacy-токену.
*/
/// <summary>
/// Stores registered CLI scenarios and exposes lookup helpers used by parsing, help rendering, and processing.
/// </summary>
internal sealed class CliScenarioRegistry
{
    private readonly IReadOnlyDictionary<string, ICliScenarioHandler> _handlersByName;
    private readonly IReadOnlyDictionary<string, string> _legacyScenarioNamesByToken;

    /// <summary>
    /// Initializes a registry from the supplied scenario handlers.
    /// </summary>
    /// <param name="handlers">Registered scenario handlers.</param>
    public CliScenarioRegistry(IEnumerable<ICliScenarioHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var handlerList = handlers.ToArray();
        if (handlerList.Length == 0)
        {
            throw new ArgumentException("At least one CLI scenario handler must be registered.", nameof(handlers));
        }

        _handlersByName = handlerList.ToDictionary(
            static handler => handler.Name,
            StringComparer.OrdinalIgnoreCase);
        _legacyScenarioNamesByToken = BuildLegacyScenarioNames(handlerList);
    }

    /// <summary>
    /// Gets the default registry used by tests and simple entry points.
    /// </summary>
    public static CliScenarioRegistry Default { get; } = new(
        [
            new ToH264GpuCliScenarioHandler(new ToH264GpuInfoFormatter()),
            new ToMkvGpuCliScenarioHandler(new ToMkvGpuInfoFormatter())
        ]);

    /// <summary>
    /// Tries to resolve a registered scenario by name.
    /// </summary>
    /// <param name="scenarioName">Scenario name.</param>
    /// <param name="handler">Resolved scenario handler.</param>
    /// <returns><see langword="true"/> when the scenario exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetScenario(string scenarioName, out ICliScenarioHandler handler)
    {
        return _handlersByName.TryGetValue(scenarioName, out handler!);
    }

    /// <summary>
    /// Tries to resolve a legacy command token to its scenario name.
    /// </summary>
    /// <param name="token">Legacy token.</param>
    /// <param name="scenarioName">Resolved scenario name.</param>
    /// <returns><see langword="true"/> when the token is recognized; otherwise <see langword="false"/>.</returns>
    public bool TryGetLegacyScenarioName(string token, out string scenarioName)
    {
        return _legacyScenarioNamesByToken.TryGetValue(token, out scenarioName!);
    }

    /// <summary>
    /// Returns help rows for shared and scenario-specific CLI options.
    /// </summary>
    /// <returns>Help rows shown in CLI usage output.</returns>
    public IReadOnlyList<CliHelpOption> GetHelpOptions()
    {
        return CliCommonOptions.CreateHelpOptions(GetSupportedScenarioDisplay())
            .Concat(_handlersByName.Values
                .OrderBy(static handler => handler.Name, StringComparer.OrdinalIgnoreCase)
                .SelectMany(static handler => handler.HelpOptions))
            .ToArray();
    }

    /// <summary>
    /// Returns scenario-specific help examples for all registered scenarios.
    /// </summary>
    /// <param name="exeName">Executable name used in rendered examples.</param>
    /// <returns>Help examples.</returns>
    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        return _handlersByName.Values
            .OrderBy(static handler => handler.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(handler => handler.GetHelpExamples(exeName))
            .ToArray();
    }

    /// <summary>
    /// Returns the supported scenario names as a display string.
    /// </summary>
    /// <returns>Comma-separated scenario list.</returns>
    public string GetSupportedScenarioDisplay()
    {
        return string.Join(", ",
            _handlersByName.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, string> BuildLegacyScenarioNames(
        IReadOnlyList<ICliScenarioHandler> handlers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in handlers)
        {
            foreach (var token in handler.LegacyCommandTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                result[token.Trim()] = handler.Name;
            }
        }

        return result;
    }
}
