using System.Diagnostics;
using MediaTranscodeEngine.Core.Abstractions;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class ProcessRunner : IProcessRunner
{
    public ProcessRunResult Run(string fileName, string arguments, int timeoutMs = 30_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort kill.
            }

            return new ProcessRunResult(
                ExitCode: -1,
                StdOut: string.Empty,
                StdErr: $"Process timeout after {timeoutMs}ms: {fileName} {arguments}");
        }

        return new ProcessRunResult(
            ExitCode: process.ExitCode,
            StdOut: stdOut,
            StdErr: stdErr);
    }
}
