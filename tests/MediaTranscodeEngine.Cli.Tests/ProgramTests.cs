using FluentAssertions;
using MediaTranscodeEngine.Cli.Processing;
using MediaTranscodeEngine.Cli.Tests.Logging;
using MediaTranscodeEngine.Runtime.Inspection;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Tests;

[Collection("Console")]
public sealed class ProgramTests
{
    [Fact]
    public void ConfigureUtf8ConsoleWriters_WhenCalled_SetsUtf8ForStdOutAndStdErr()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            Program.ConfigureUtf8ConsoleWriters();

            Console.Out.Encoding.WebName.Should().Be("utf-8");
            Console.Error.Encoding.WebName.Should().Be("utf-8");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void RunCli_WhenNonInfoMode_WritesChcpOnceBeforeProcessorOutput()
    {
        var services = new ServiceCollection()
            .AddSingleton<ITranscodeProcessor>(new StubProcessor("ffmpeg -i \"C:\\video\\a.mp4\" \"C:\\video\\a.mkv\""))
            .BuildServiceProvider();
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var logger = loggerFactory.CreateLogger("test");
        var runtimeValues = new RuntimeValues
        {
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg"
        };

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.RunCli(["--input", @"C:\video\a.mp4"], logger, services, runtimeValues, readRedirectedStdIn: false);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("chcp 65001");
            output.ToString().Should().Contain("ffmpeg -i \"C:\\video\\a.mp4\" \"C:\\video\\a.mkv\"");
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void RunCli_WhenInfoMode_DoesNotWriteChcp()
    {
        var services = new ServiceCollection()
            .AddSingleton<ITranscodeProcessor>(new StubProcessor("a.mp4: [ffprobe failed]"))
            .BuildServiceProvider();
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var logger = loggerFactory.CreateLogger("test");
        var runtimeValues = new RuntimeValues
        {
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg"
        };

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.RunCli(["--input", @"C:\video\a.mp4", "--info"], logger, services, runtimeValues, readRedirectedStdIn: false);

            exitCode.Should().Be(0);
            output.ToString().Should().NotContain("chcp 65001");
            output.ToString().Should().Contain("a.mp4: [ffprobe failed]");
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void RunCli_WhenNonInfoProcessorReturnsEmptyLine_WritesOnlyChcp()
    {
        var services = new ServiceCollection()
            .AddSingleton<ITranscodeProcessor>(new StubProcessor(string.Empty))
            .BuildServiceProvider();
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var logger = loggerFactory.CreateLogger("test");
        var runtimeValues = new RuntimeValues
        {
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg"
        };

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.RunCli(["--input", @"C:\video\a.mkv"], logger, services, runtimeValues, readRedirectedStdIn: false);

            exitCode.Should().Be(0);
            output.ToString().Trim().Should().Be("chcp 65001");
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void RunCli_WhenParseSucceeds_LogsRequestAndPerInputLifecycle()
    {
        var services = new ServiceCollection()
            .AddSingleton<ITranscodeProcessor>(new StubProcessor("ffmpeg -i input output"))
            .BuildServiceProvider();
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var logger = loggerFactory.CreateLogger("test");
        var runtimeValues = new RuntimeValues
        {
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg"
        };

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.RunCli(
                ["--input", @"C:\video\a.mp4", "--downscale", "576", "--autosample-mode", "fast"],
                logger,
                services,
                runtimeValues,
                readRedirectedStdIn: false);

            exitCode.Should().Be(0);
            provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                      entry.Message.Contains("CLI request received.", StringComparison.Ordinal));
            provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                      entry.Message.Contains("CLI request parsed.", StringComparison.Ordinal) &&
                                                      Equals(entry.Properties["InputCount"], 1) &&
                                                      Equals(entry.Properties["DownscaleTarget"], 576) &&
                                                      Equals(entry.Properties["AutoSampleMode"], "fast"));
            provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                      entry.Message.Contains("CLI input processing started.", StringComparison.Ordinal) &&
                                                      Equals(entry.Properties["InputPath"], @"C:\video\a.mp4"));
            provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                      entry.Message.Contains("CLI input processing completed.", StringComparison.Ordinal) &&
                                                      Equals(entry.Properties["HasOutput"], true));
            provider.Entries.Should().Contain(entry => entry.Level == LogLevel.Information &&
                                                      entry.Message.Contains("CLI request completed.", StringComparison.Ordinal));
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void RunCli_WhenHelpRequested_WritesHelpTextAndReturnsZero()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var logger = loggerFactory.CreateLogger("test");
        var runtimeValues = new RuntimeValues
        {
            FfprobePath = "ffprobe-custom",
            FfmpegPath = "ffmpeg-custom"
        };

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.RunCli(["--help"], logger, services, runtimeValues, readRedirectedStdIn: false);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("MediaTranscodeEngine CLI");
            output.ToString().Should().Contain("Usage:");
            output.ToString().Should().Contain("RuntimeValues:FfprobePath current: ffprobe-custom");
            output.ToString().Should().Contain("RuntimeValues:FfmpegPath  current: ffmpeg-custom");
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void RunCli_WhenNoInputsAreProvided_WritesErrorAndReturnsOne()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var logger = loggerFactory.CreateLogger("test");
        var runtimeValues = new RuntimeValues
        {
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg"
        };

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.RunCli(["--scenario", "tomkvgpu"], logger, services, runtimeValues, readRedirectedStdIn: false);

            exitCode.Should().Be(1);
            output.ToString().Should().BeEmpty();
            error.ToString().Trim().Should().Be("No input files provided. Use --input or pipe file paths to stdin.");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void RunCli_WhenProcessorReturnsLegacyIoRemLine_WritesOneLineAndKeepsStdErrEmpty()
    {
        using var provider = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var processor = new PrimaryTranscodeProcessor(
            new VideoInspector(new ThrowingVideoProbe(new IOException("Disk read failed."))),
            new StubTool(),
            new ToMkvGpuInfoFormatter(),
            loggerFactory.CreateLogger<PrimaryTranscodeProcessor>());
        var services = new ServiceCollection()
            .AddSingleton<ITranscodeProcessor>(processor)
            .BuildServiceProvider();
        var logger = loggerFactory.CreateLogger("test");
        var runtimeValues = new RuntimeValues
        {
            FfprobePath = "ffprobe",
            FfmpegPath = "ffmpeg"
        };

        using var output = new StringWriter();
        using var error = new StringWriter();
        var originalOut = Console.Out;
        var originalError = Console.Error;

        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var exitCode = Program.RunCli(["--input", @"C:\video\a.mp4"], logger, services, runtimeValues, readRedirectedStdIn: false);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("chcp 65001");
            output.ToString().Should().Contain("REM I/O error: a.mp4");
            error.ToString().Should().BeEmpty();
            var errorEntry = provider.Entries.Single(entry => entry.Level == LogLevel.Error &&
                                                              entry.Message.Contains("Processing returned failure marker.", StringComparison.Ordinal));
            errorEntry.Exception.Should().BeOfType<IOException>();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed class StubProcessor : ITranscodeProcessor
    {
        private readonly string _line;

        public StubProcessor(string line)
        {
            _line = line;
        }

        public string Process(CliTranscodeRequest request)
        {
            return _line;
        }
    }

    private sealed class ThrowingVideoProbe : IVideoProbe
    {
        private readonly Exception _exception;

        public ThrowingVideoProbe(Exception exception)
        {
            _exception = exception;
        }

        public VideoProbeSnapshot Probe(string filePath)
        {
            throw _exception;
        }
    }

    private sealed class StubTool : ITranscodeTool
    {
        public string Name => "stub";

        public bool CanHandle(Runtime.Plans.TranscodePlan plan)
        {
            return true;
        }

        public ToolExecution BuildExecution(SourceVideo video, Runtime.Plans.TranscodePlan plan)
        {
            return ToolExecution.Single("stub", "stub");
        }
    }
}
