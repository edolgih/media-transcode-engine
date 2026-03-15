using FluentAssertions;
using Transcode.Core.Failures;
using Transcode.Core.Inspection;

namespace Transcode.Runtime.Tests.Inspection;

/*
Это тесты ffprobe-based реализации IVideoProbe.
Они проверяют разбор JSON и классификацию структурированных ошибок probe-слоя.
*/
/// <summary>
/// Verifies ffprobe process result handling and snapshot mapping behavior.
/// </summary>
public sealed class FfprobeVideoProbeTests
{
    [Fact]
    public void Probe_WhenProcessReturnsValidJson_ReturnsMappedSnapshot()
    {
        var capturedPath = string.Empty;
        var sut = new FfprobeVideoProbe(filePath =>
        {
            capturedPath = filePath;
            return new FfprobeProcessResult(
                ExitCode: 0,
                StandardOutput: CreateValidJson(),
                StandardError: string.Empty);
        });

        var actual = sut.Probe(@".\input.mp4");

        capturedPath.Should().Be(Path.GetFullPath(@".\input.mp4"));
        actual.container.Should().Be("mp4");
        actual.duration.Should().Be(TimeSpan.FromSeconds(600.123));
        actual.formatBitrate.Should().Be(4567000);
        actual.streams.Should().HaveCount(2);
        actual.streams[0].streamType.Should().Be("video");
        actual.streams[0].codec.Should().Be("h264");
        actual.streams[0].width.Should().Be(1920);
        actual.streams[0].height.Should().Be(1080);
        actual.streams[0].framesPerSecond.Should().BeApproximately(30000d / 1001d, 0.0001);
        actual.streams[0].bitrate.Should().Be(3456000);
        actual.streams[1].streamType.Should().Be("audio");
        actual.streams[1].codec.Should().Be("aac");
        actual.streams[1].bitrate.Should().Be(192000);
    }

    [Fact]
    public void Probe_WhenProcessFails_ThrowsRuntimeFailureException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 1,
            StandardOutput: string.Empty,
            StandardError: "ffprobe exploded"));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        var exception = action.Should().Throw<RuntimeFailureException>().Which;

        exception.Code.Should().Be(RuntimeFailureCode.ProbeProcessFailed);
    }

    [Fact]
    public void Probe_WhenStdOutIsEmpty_ThrowsRuntimeFailureException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: "   ",
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        var exception = action.Should().Throw<RuntimeFailureException>().Which;

        exception.Code.Should().Be(RuntimeFailureCode.ProbeEmptyOutput);
    }

    [Fact]
    public void Probe_WhenJsonIsInvalid_ThrowsRuntimeFailureException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: "{invalid",
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        var exception = action.Should().Throw<RuntimeFailureException>().Which;

        exception.Code.Should().Be(RuntimeFailureCode.ProbeInvalidJson);
    }

    [Fact]
    public void Probe_WhenRequiredStreamFieldIsMissing_ThrowsRuntimeFailureException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: CreateJsonWithoutCodecName(),
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        var exception = action.Should().Throw<RuntimeFailureException>().Which;

        exception.Code.Should().Be(RuntimeFailureCode.ProbeMissingRequiredField);
    }

    [Fact]
    public void Probe_WhenStreamsFieldIsMissing_ThrowsRuntimeFailureException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: """
                            {
                              "format": {
                                "format_name": "mp4"
                              }
                            }
                            """,
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        var exception = action.Should().Throw<RuntimeFailureException>().Which;

        exception.Code.Should().Be(RuntimeFailureCode.ProbeMissingStreamsField);
    }

    [Fact]
    public void Probe_WhenStreamEntryIsInvalid_ThrowsRuntimeFailureException()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: """
                            {
                              "streams": [5]
                            }
                            """,
            StandardError: string.Empty));

        Action action = () => sut.Probe(@"C:\video\input.mp4");

        var exception = action.Should().Throw<RuntimeFailureException>().Which;

        exception.Code.Should().Be(RuntimeFailureCode.ProbeInvalidStreamEntry);
    }

    [Fact]
    public void Probe_WhenPathHasNoExtension_UsesFormatNameForContainer()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: """
                            {
                              "format": {
                                "format_name": "matroska,webm",
                                "duration": "90.5",
                                "bit_rate": "900000"
                              },
                              "streams": [
                                {
                                  "codec_type": "video",
                                  "codec_name": "h264",
                                  "width": 1280,
                                  "height": 720,
                                  "r_frame_rate": "25/1"
                                }
                              ]
                            }
                            """,
            StandardError: string.Empty));

        var actual = sut.Probe(@"C:\video\input");

        actual.container.Should().Be("matroska");
        actual.duration.Should().Be(TimeSpan.FromSeconds(90.5));
        actual.formatBitrate.Should().Be(900000);
    }

    [Fact]
    public void Probe_WhenFormatNameAndExtendedStreamFactsArePresent_PreservesThem()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: """
                            {
                              "format": {
                                "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
                                "duration": "90.5",
                                "bit_rate": "900000"
                              },
                              "streams": [
                                {
                                  "codec_type": "video",
                                  "codec_name": "h264",
                                  "width": 1280,
                                  "height": 720,
                                  "r_frame_rate": "60000/1001",
                                  "avg_frame_rate": "30000/1001",
                                  "bit_rate": "700000"
                                },
                                {
                                  "codec_type": "audio",
                                  "codec_name": "aac",
                                  "bit_rate": "128000",
                                  "sample_rate": "44100",
                                  "channels": 2
                                }
                              ]
                            }
                            """,
            StandardError: string.Empty));

        var actual = sut.Probe(@"C:\video\input.mp4");

        actual.formatName.Should().Be("mov,mp4,m4a,3gp,3g2,mj2");
        actual.streams[0].rawFramesPerSecond.Should().BeApproximately(60000d / 1001d, 0.0001);
        actual.streams[0].averageFramesPerSecond.Should().BeApproximately(30000d / 1001d, 0.0001);
        actual.streams[0].framesPerSecond.Should().BeApproximately(60000d / 1001d, 0.0001);
        actual.streams[1].sampleRate.Should().Be(44100);
        actual.streams[1].channels.Should().Be(2);
    }

    [Fact]
    public void Probe_WhenRawFrameRateIsMissing_UsesAverageFrameRate()
    {
        var sut = new FfprobeVideoProbe(_ => new FfprobeProcessResult(
            ExitCode: 0,
            StandardOutput: """
                            {
                              "format": {
                                "format_name": "mp4"
                              },
                              "streams": [
                                {
                                  "codec_type": "video",
                                  "codec_name": "h264",
                                  "width": 1280,
                                  "height": 720,
                                  "r_frame_rate": "0/0",
                                  "avg_frame_rate": "30000/1001"
                                }
                              ]
                            }
                            """,
            StandardError: string.Empty));

        var actual = sut.Probe(@"C:\video\input.mp4");

        actual.streams[0].rawFramesPerSecond.Should().BeNull();
        actual.streams[0].averageFramesPerSecond.Should().BeApproximately(30000d / 1001d, 0.0001);
        actual.streams[0].framesPerSecond.Should().BeApproximately(30000d / 1001d, 0.0001);
    }

    private static string CreateValidJson()
    {
        return """
               {
                 "format": {
                   "format_name": "mov,mp4,m4a,3gp,3g2,mj2",
                   "duration": "600.123",
                   "bit_rate": "4567000"
                 },
                 "streams": [
                   {
                     "codec_type": "video",
                     "codec_name": "h264",
                     "width": 1920,
                     "height": 1080,
                     "r_frame_rate": "30000/1001",
                     "bit_rate": "3456000"
                   },
                   {
                     "codec_type": "audio",
                     "codec_name": "aac",
                     "bit_rate": "192000"
                   }
                 ]
               }
               """;
    }

    private static string CreateJsonWithoutCodecName()
    {
        return """
               {
                 "format": {
                   "format_name": "mp4",
                   "duration": "120.5"
                 },
                 "streams": [
                   {
                     "codec_type": "video",
                     "width": 1920,
                     "height": 1080,
                     "r_frame_rate": "25/1"
                   }
                 ]
               }
               """;
    }
}
