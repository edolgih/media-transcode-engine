namespace MediaTranscodeEngine.Cli.Parsing;

internal static class CliHelpBuilder
{
    public static string BuildHelpText(RuntimeValues runtimeValues)
    {
        var exeName = Path.GetFileName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(exeName))
        {
            exeName = "MediaTranscodeEngine.Cli.exe";
        }

        var lines = new List<string>
        {
            "MediaTranscodeEngine CLI",
            string.Empty,
            $"Usage: {exeName} [options]",
            string.Empty,
            "Common options:"
        };

        AddOptionRows(lines, GetCommonOptions());

        lines.Add(string.Empty);
        lines.Add($"Scenario: {CliContracts.ToMkvGpuScenario}");
        AddOptionRows(lines, GetScenarioOptions(CliContracts.ToMkvGpuScenario));

        lines.Add(string.Empty);
        lines.Add($"Scenario: {CliContracts.ToH264GpuScenario}");
        AddOptionRows(lines, GetScenarioOptions(CliContracts.ToH264GpuScenario));

        lines.Add(string.Empty);
        lines.Add("Configuration (appsettings / environment):");
        lines.Add($"  {nameof(RuntimeValues)}:ProfilesYamlPath                 current: {runtimeValues.ProfilesYamlPath}");
        lines.Add($"  {nameof(RuntimeValues)}:FfprobePath                     current: {runtimeValues.FfprobePath}");
        lines.Add($"  {nameof(RuntimeValues)}:FfmpegPath                      current: {runtimeValues.FfmpegPath}");
        lines.Add($"  {nameof(RuntimeValues)}:ProcessTimeoutMs                current: {runtimeValues.ProcessTimeoutMs}");
        lines.Add($"  {nameof(RuntimeValues)}:SampleEncodeInactivityTimeoutMs current: {runtimeValues.SampleEncodeInactivityTimeoutMs}");
        lines.Add($"  {nameof(RuntimeValues)}:SampleDurationSeconds           current: {runtimeValues.SampleDurationSeconds}");
        lines.Add($"  {nameof(RuntimeValues)}:SampleEncodeMaxRetries          current: {runtimeValues.SampleEncodeMaxRetries}");
        lines.Add($"  {nameof(RuntimeValues)}:AutoSampleNvencPreset           current: {runtimeValues.AutoSampleNvencPreset}");

        lines.Add(string.Empty);
        lines.Add("Examples:");
        lines.Add($"  {exeName} --input \"C:\\video\\movie.mkv\"");
        lines.Add($"  {exeName} --input \"C:\\video\\movie.mkv\" --info");
        lines.Add($"  {exeName} --scenario toh264gpu --input \"C:\\video\\movie.mp4\" --output-mkv");
        lines.Add($"  Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | {exeName} --info");

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<CliOptionDefinition> GetCommonOptions()
    {
        return CliContracts.OptionsByName.Values
            .Where(static option =>
                option.Name != "-h" &&
                option.AppliesToScenarios.Contains(CliContracts.ToMkvGpuScenario) &&
                option.AppliesToScenarios.Contains(CliContracts.ToH264GpuScenario))
            .OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<CliOptionDefinition> GetScenarioOptions(string scenarioName)
    {
        return CliContracts.OptionsByName.Values
            .Where(option =>
                option.Name != "-h" &&
                option.AppliesToScenarios.Contains(scenarioName) &&
                !(option.AppliesToScenarios.Contains(CliContracts.ToMkvGpuScenario) &&
                  option.AppliesToScenarios.Contains(CliContracts.ToH264GpuScenario)))
            .OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddOptionRows(List<string> lines, IReadOnlyList<CliOptionDefinition> options)
    {
        foreach (var option in options)
        {
            var usage = option.Usage ?? option.Name;
            lines.Add($"  {usage,-44} {option.HelpText}");
        }
    }
}
