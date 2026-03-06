using FluentAssertions;
using MediaTranscodeEngine.Cli.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Tests;

[Collection("Console")]
public sealed class ProgramTests
{
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
}
