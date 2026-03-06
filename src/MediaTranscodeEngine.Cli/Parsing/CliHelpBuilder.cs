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
            "Options:"
        };

        AddOptionRows(lines, GetOptionsForHelp());

        lines.Add(string.Empty);
        lines.Add("Configuration (appsettings / environment):");
        lines.Add($"  {nameof(RuntimeValues)}:FfprobePath current: {runtimeValues.FfprobePath}");
        lines.Add($"  {nameof(RuntimeValues)}:FfmpegPath  current: {runtimeValues.FfmpegPath}");

        lines.Add(string.Empty);
        lines.Add("Examples:");
        lines.Add($"  {exeName} --input \"C:\\video\\movie.mkv\"");
        lines.Add($"  {exeName} --input \"C:\\video\\movie.mkv\" --info");
        lines.Add($"  {exeName} --input \"C:\\video\\movie.mkv\" --keep-source --downscale 576");
        lines.Add($"  {exeName} --input \"C:\\video\\movie.mkv\" --overlay-bg --sync-audio");
        lines.Add($"  {exeName} --input \"C:\\video\\movie.mkv\" --downscale 576 --content-profile film --quality-profile default");
        lines.Add($"  Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | {exeName} --info");

        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<CliOptionDefinition> GetOptionsForHelp()
    {
        return CliContracts.OptionsByName.Values
            .Where(static option => option.Name != "-h")
            .OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddOptionRows(List<string> lines, IReadOnlyList<CliOptionDefinition> options)
    {
        foreach (var option in options)
        {
            var usage = option.Usage ?? option.Name;
            lines.Add($"  {usage,-32} {option.HelpText}");
        }
    }
}
