using System.Diagnostics;
using System.Text;
using MediaTranscodeEngine.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner>? logger = null)
    {
        _logger = logger ?? NullLogger<ProcessRunner>.Instance;
    }

    public ProcessRunResult Run(string fileName, string arguments, int timeoutMs = 30_000)
    {
        return RunWithInactivityTimeout(fileName, arguments, timeoutMs, inactivityTimeoutMs: 0);
    }

    public ProcessRunResult RunWithInactivityTimeout(
        string fileName,
        string arguments,
        int timeoutMs = 30_000,
        int inactivityTimeoutMs = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be greater than zero.");
        }

        if (inactivityTimeoutMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inactivityTimeoutMs), "Inactivity timeout cannot be negative.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        _logger.LogDebug(
            "Starting process: {FileName} {Arguments} (timeoutMs={TimeoutMs}, inactivityTimeoutMs={InactivityTimeoutMs})",
            fileName,
            arguments,
            timeoutMs,
            inactivityTimeoutMs);

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var stdOutBuilder = new StringBuilder();
        var stdErrBuilder = new StringBuilder();
        var stdOutClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdErrClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastActivityUtcTicks = DateTime.UtcNow.Ticks;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                stdOutClosed.TrySetResult();
                return;
            }

            Interlocked.Exchange(ref lastActivityUtcTicks, DateTime.UtcNow.Ticks);
            if (stdOutBuilder.Length > 0)
            {
                stdOutBuilder.AppendLine();
            }

            stdOutBuilder.Append(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                stdErrClosed.TrySetResult();
                return;
            }

            Interlocked.Exchange(ref lastActivityUtcTicks, DateTime.UtcNow.Ticks);
            if (stdErrBuilder.Length > 0)
            {
                stdErrBuilder.AppendLine();
            }

            stdErrBuilder.Append(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var startedAt = DateTime.UtcNow;

        var waitForExit = process.WaitForExitAsync();
        var hardDeadlineUtc = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (!waitForExit.IsCompleted)
        {
            if (DateTime.UtcNow >= hardDeadlineUtc)
            {
                TryKillProcessTree(process);
                WaitForPipeDrain(stdOutClosed.Task, stdErrClosed.Task);
                _logger.LogWarning(
                    "Process timeout reached after {TimeoutMs}ms: {FileName} {Arguments}",
                    timeoutMs,
                    fileName,
                    arguments);

                return new ProcessRunResult(
                    ExitCode: -1,
                    StdOut: stdOutBuilder.ToString(),
                    StdErr: $"Process timeout after {timeoutMs}ms: {fileName} {arguments}");
            }

            if (inactivityTimeoutMs > 0)
            {
                var lastActivity = new DateTime(Interlocked.Read(ref lastActivityUtcTicks), DateTimeKind.Utc);
                var inactivity = DateTime.UtcNow - lastActivity;
                if (inactivity.TotalMilliseconds >= inactivityTimeoutMs)
                {
                    TryKillProcessTree(process);
                    WaitForPipeDrain(stdOutClosed.Task, stdErrClosed.Task);
                    _logger.LogWarning(
                        "Process inactivity timeout reached after {InactivityTimeoutMs}ms: {FileName} {Arguments}",
                        inactivityTimeoutMs,
                        fileName,
                        arguments);

                    return new ProcessRunResult(
                        ExitCode: -1,
                        StdOut: stdOutBuilder.ToString(),
                        StdErr: $"Process inactivity timeout after {inactivityTimeoutMs}ms: {fileName} {arguments}");
                }
            }

            Task.Delay(100).GetAwaiter().GetResult();
        }

        waitForExit.GetAwaiter().GetResult();
        WaitForPipeDrain(stdOutClosed.Task, stdErrClosed.Task);
        _logger.LogDebug(
            "Process finished: {FileName} exitCode={ExitCode} elapsedMs={ElapsedMs}",
            fileName,
            process.ExitCode,
            (int)(DateTime.UtcNow - startedAt).TotalMilliseconds);

        return new ProcessRunResult(
            ExitCode: process.ExitCode,
            StdOut: stdOutBuilder.ToString(),
            StdErr: stdErrBuilder.ToString());
    }

    private static void WaitForPipeDrain(Task stdOutClosed, Task stdErrClosed)
    {
        var drainTask = Task.WhenAll(stdOutClosed, stdErrClosed);
        var completed = Task.WhenAny(drainTask, Task.Delay(1_000)).GetAwaiter().GetResult();
        if (ReferenceEquals(completed, drainTask))
        {
            drainTask.GetAwaiter().GetResult();
        }
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort kill.
        }
    }
}
