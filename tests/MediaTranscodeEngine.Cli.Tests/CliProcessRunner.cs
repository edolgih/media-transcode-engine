using System.Diagnostics;

namespace MediaTranscodeEngine.Cli.Tests;

internal readonly record struct CliProcessResult(
    int ExitCode,
    string StdOut,
    string StdErr);

internal static class CliProcessRunner
{
    public static async Task<CliProcessResult> RunAsync(
        IReadOnlyList<string> args,
        string? stdIn = null,
        int timeoutMs = 30_000)
    {
        var cliDllPath = Path.Combine(AppContext.BaseDirectory, "MediaTranscodeEngine.Cli.dll");
        if (!File.Exists(cliDllPath))
        {
            throw new FileNotFoundException("CLI assembly was not found in test output.", cliDllPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        startInfo.ArgumentList.Add(cliDllPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        if (!string.IsNullOrEmpty(stdIn))
        {
            await process.StandardInput.WriteAsync(stdIn);
        }

        process.StandardInput.Close();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(timeoutMs);
        await process.WaitForExitAsync(cts.Token);

        return new CliProcessResult(
            ExitCode: process.ExitCode,
            StdOut: await stdOutTask,
            StdErr: await stdErrTask);
    }
}
