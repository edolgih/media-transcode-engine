using FluentAssertions;
using System.Text.Json;
using MediaTranscodeEngine.Runtime.Failures;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests.Scenarios;

/*
Это тесты formatter-а info-режима tomkvgpu.
Они проверяют текстовую сводку плана и маркировку известных failure cases.
*/
/// <summary>
/// Verifies summary formatting and failure markers produced by the ToMkvGpu info formatter.
/// </summary>
public sealed class ToMkvGpuInfoFormatterTests
{
    [Fact]
    public void Format_WhenPlanDoesNothing_ReturnsEmptyString()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mkv", container: "mkv", videoCodec: "h264", audioCodecs: ["aac"]);
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: video.FilePath);

        var actual = sut.Format(video, plan);

        actual.Should().BeEmpty();
    }

    [Fact]
    public void Format_WhenContainerAndVideoAndAudioNeedChanges_ReturnsExpectedMarkers()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mp4", container: "mp4", videoCodec: "av1", audioCodecs: ["ac3"]);
        var plan = CreatePlan(copyVideo: false, copyAudio: false, targetVideoCodec: "h264", preferredBackend: "gpu", outputPath: @"C:\video\input.mkv");

        var actual = sut.Format(video, plan);

        actual.Should().Be("input.mp4: [container .mp4→mkv] [vcodec av1] [audio non-AAC]");
    }

    [Fact]
    public void Format_WhenSyncAudioIsRequested_ReturnsSyncAudioMarker()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mkv", container: "mkv", videoCodec: "h264", audioCodecs: ["aac"]);
        var plan = CreatePlan(copyVideo: true, copyAudio: false, outputPath: video.FilePath, synchronizeAudio: true);

        var actual = sut.Format(video, plan);

        actual.Should().Be("input.mkv: [sync audio]");
    }

    [Fact]
    public void Format_WhenFrameRateCapIsApplied_ReturnsFpsMarker()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mkv", container: "mkv", videoCodec: "h264", audioCodecs: ["aac"]);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            outputPath: @"C:\video\input_out.mkv",
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetFramesPerSecond: 50);

        var actual = sut.Format(video, plan);

        actual.Should().Be("input.mkv: [vcodec h264] [fps 50]");
    }

    [Fact]
    public void Format_WhenPathContainsDirectories_UsesOnlyFileName()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\nested\folder\input.mp4", container: "mp4", videoCodec: "av1", audioCodecs: ["aac"]);
        var plan = CreatePlan(copyVideo: false, copyAudio: false, targetVideoCodec: "h264", preferredBackend: "gpu", outputPath: @"C:\nested\folder\input.mkv");

        var actual = sut.Format(video, plan);

        actual.Should().StartWith("input.mp4:");
        actual.Should().NotContain(@"C:\nested\folder");
    }

    [Fact]
    public void FormatProbeFailure_WhenCalled_ReturnsFileNameMarker()
    {
        var sut = CreateSut();

        var actual = sut.FormatProbeFailure(@"C:\nested\folder\input.mp4");

        actual.Should().Be("input.mp4: [ffprobe failed]");
    }

    [Fact]
    public void FormatFailure_WhenProbeErrorIsGeneric_ReturnsFfprobeFailedMarker()
    {
        var sut = CreateSut();

        var actual = sut.FormatFailure(
            @"C:\nested\folder\input.mp4",
            RuntimeFailures.ProbeInvalidJson(new JsonException()));

        actual.Should().Be("input.mp4: [ffprobe failed]");
    }

    [Fact]
    public void FormatFailure_WhenNoVideoStreamErrorOccurs_ReturnsNoVideoStreamMarker()
    {
        var sut = CreateSut();

        var actual = sut.FormatFailure(
            @"C:\nested\folder\input.mp4",
            RuntimeFailures.NoVideoStream());

        actual.Should().Be("input.mp4: [no video stream]");
    }

    [Fact]
    public void FormatFailure_WhenUnknownDimensionsErrorOccurs_ReturnsUnknownDimensionsMarker()
    {
        var sut = CreateSut();

        var actual = sut.FormatFailure(
            @"C:\nested\folder\input.mp4",
            RuntimeFailures.InvalidVideoHeight());

        actual.Should().Be("input.mp4: [unknown dimensions]");
    }

    [Fact]
    public void FormatFailure_WhenDownscaleSourceBucketIsMissing_ReturnsBucketHint()
    {
        var sut = CreateSut();

        var actual = sut.FormatFailure(
            @"C:\nested\folder\input.mp4",
            RuntimeFailures.DownscaleSourceBucketIssue("576 source bucket missing: height 900; add SourceBuckets"));

        actual.Should().Be("input.mp4: [576 source bucket missing: height 900; add SourceBuckets]");
    }

    [Fact]
    public void FormatFailure_WhenDownscaleSourceBucketIsInvalid_ReturnsBucketHint()
    {
        var sut = CreateSut();

        var actual = sut.FormatFailure(
            @"C:\nested\folder\input.mp4",
            RuntimeFailures.DownscaleSourceBucketIssue("576 source bucket invalid: missing corridor 'mult/low'"));

        actual.Should().Be("input.mp4: [576 source bucket invalid: missing corridor 'mult/low']");
    }

    private static ToMkvGpuInfoFormatter CreateSut()
    {
        return new ToMkvGpuInfoFormatter();
    }

    private static SourceVideo CreateVideo(
        string filePath,
        string container,
        string videoCodec,
        IReadOnlyList<string> audioCodecs)
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs,
            width: 1920,
            height: 1080,
            framesPerSecond: 29.97,
            duration: TimeSpan.FromMinutes(10));
    }

    private static TranscodePlan CreatePlan(
        bool copyVideo,
        bool copyAudio,
        string outputPath,
        string? targetVideoCodec = null,
        string? preferredBackend = null,
        bool synchronizeAudio = false,
        double? targetFramesPerSecond = null)
    {
        return new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: targetVideoCodec,
            preferredBackend: preferredBackend,
            videoCompatibilityProfile: copyVideo || !string.Equals(targetVideoCodec, "h264", StringComparison.OrdinalIgnoreCase) ? null : VideoCompatibilityProfile.H264High,
            targetHeight: null,
            targetFramesPerSecond: targetFramesPerSecond,
            useFrameInterpolation: false,
            videoSettings: null,
            copyVideo: copyVideo,
            copyAudio: copyAudio,
            fixTimestamps: false,
            keepSource: false,
            outputPath: outputPath,
            synchronizeAudio: synchronizeAudio);
    }
}
