using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Gpu.Core;
using Transcode.Scenarios.ToMkvGpu.Core;

namespace Transcode.Runtime.Tests.Tools;

/*
Эти тесты проверяют scenario-local ffmpeg rendering после отказа от shared plan/spec pipeline.
Они держатся за текущий decision-centric flow: scenario сначала принимает решение, затем local ffmpeg helper рендерит команды.
*/
/// <summary>
/// Verifies scenario-local ffmpeg command rendering through the current decision-centric flow.
/// </summary>
public sealed class FfmpegToolTests
{
    [Fact]
    public void ToMkvGpuTool_WhenDecisionIsNoOp_ReturnsEmptyExecution()
    {
        var tool = CreateToMkvTool();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mkv");
        var decision = new ToMkvGpuScenario(new ToMkvGpuRequest()).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ToMkvGpuTool_WhenEncodeIsRequired_BuildsNvencCommandAndDeleteStep()
    {
        var tool = CreateToMkvTool();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", audioCodecs: ["ac3"], filePath: @"C:\video\input.mp4");
        var decision = new ToMkvGpuScenario(new ToMkvGpuRequest()).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Commands[0].Should().Contain("-c:v h264_nvenc");
        actual.Commands[0].Should().Contain("-preset p6");
        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mp4\"");
    }

    [Fact]
    public void ToMkvGpuTool_WhenOverlayBackgroundIsEnabled_UsesFilterComplex()
    {
        var tool = CreateToMkvTool();
        var request = new ToMkvGpuRequest(overlayBackground: true);
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4", width: 720, height: 1280);
        var decision = new ToMkvGpuScenario(request).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-filter_complex");
        actual.Commands[0].Should().Contain("overlay");
    }

    [Fact]
    public void ToMkvGpuTool_WhenSynchronizeAudioOnCopyVideo_UsesStrongSyncRemux()
    {
        var tool = CreateToMkvTool();
        var request = new ToMkvGpuRequest(synchronizeAudio: true);
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mkv");
        var decision = new ToMkvGpuScenario(request).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-c:v copy -copytb 1");
        actual.Commands[0].Should().Contain("-af \"aresample=async=1:first_pts=0\"");
    }

    [Fact]
    public void ToH264GpuTool_WhenCopyMp4NeedsMuxRewrite_BuildsNonEmptyExecution()
    {
        var tool = CreateToH264Tool();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");
        var decision = new ToH264GpuScenario(new ToH264GpuRequest()).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.IsEmpty.Should().BeFalse();
        actual.Commands[0].Should().Contain("-movflags +faststart");
        actual.Commands[0].Should().Contain("-map 0:a:0? -c:a copy");
    }

    [Fact]
    public void ToH264GpuTool_WhenEncodeIsRequired_UsesNvencPresetAndCqMode()
    {
        var tool = CreateToH264Tool();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", audioCodecs: ["ac3"], filePath: @"C:\video\input.mkv");
        var decision = new ToH264GpuScenario(new ToH264GpuRequest()).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-c:v h264_nvenc");
        actual.Commands[0].Should().Contain("-preset p6");
        actual.Commands[0].Should().Contain("-rc vbr_hq -cq");
    }

    [Fact]
    public void ToH264GpuTool_WhenDownscaleIsRequested_UsesScaleCudaAndHardwareDecode()
    {
        var tool = CreateToH264Tool();
        var request = new ToH264GpuRequest(downscale: new DownscaleRequest(576));
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4", height: 1080);
        var decision = new ToH264GpuScenario(request).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Commands[0].Should().Contain("scale_cuda=-2:576:interp_algo=bicubic:format=nv12");
    }

    [Fact]
    public void ToH264GpuTool_WhenDenoiseIsRequestedWithoutDownscale_UsesVideoFilterAndPixelFormat()
    {
        var tool = CreateToH264Tool();
        var request = new ToH264GpuRequest(denoise: true);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", audioCodecs: ["aac"], filePath: @"C:\video\input.mkv");
        var decision = new ToH264GpuScenario(request).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-vf \"hqdn3d=1.2:1.2:6:6\"");
        actual.Commands[0].Should().Contain("-pix_fmt yuv420p");
    }

    [Fact]
    public void ToH264GpuTool_WhenSyncAudioIsRequested_EncodesAudioWithResample()
    {
        var tool = CreateToH264Tool();
        var request = new ToH264GpuRequest(synchronizeAudio: true);
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mkv");
        var decision = new ToH264GpuScenario(request).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().Contain("-af \"aresample=async=1:first_pts=0\"");
    }

    [Fact]
    public void ToH264GpuTool_WhenOutputReplacesSource_AddsDeleteAndRenameSteps()
    {
        var tool = CreateToH264Tool();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");
        var decision = new ToH264GpuScenario(new ToH264GpuRequest()).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands.Should().HaveCount(3);
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mp4\"");
        actual.Commands[2].Should().Be("ren \"C:\\video\\input_temp.mp4\" \"input.mp4\"");
    }

    [Fact]
    public void ToH264GpuTool_WhenContainerIsUnsupported_CanHandleReturnsFalse()
    {
        var tool = CreateToH264Tool();
        var decision = CreateToH264Decision(targetContainer: "avi");

        var actual = tool.CanHandle(decision);

        actual.Should().BeFalse();
    }

    private static ToH264GpuFfmpegTool CreateToH264Tool()
    {
        return new ToH264GpuFfmpegTool("ffmpeg", NullLogger<ToH264GpuFfmpegTool>.Instance);
    }

    private static ToMkvGpuFfmpegTool CreateToMkvTool()
    {
        return new ToMkvGpuFfmpegTool("ffmpeg", NullLogger<ToMkvGpuFfmpegTool>.Instance);
    }

    private static ToH264GpuDecision CreateToH264Decision(
        string targetContainer = "mp4",
        bool copyVideo = true,
        bool copyAudio = true,
        bool keepSource = false,
        string outputPath = @"C:\video\input.mp4")
    {
        VideoIntent videoIntent = copyVideo
            ? new CopyVideoIntent()
            : new EncodeVideoIntent(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: H264OutputProfile.H264High,
                EncoderPreset: "p6");
        AudioIntent audioIntent = copyAudio
            ? new CopyAudioIntent()
            : new EncodeAudioIntent();

        return new ToH264GpuDecision(
            targetContainer: targetContainer,
            videoIntent: videoIntent,
            audioIntent: audioIntent,
            keepSource: keepSource,
            outputPath: outputPath,
            mux: new ToH264GpuDecision.MuxExecution(optimizeForFastStart: true, mapPrimaryAudioOnly: true));
    }

    private static SourceVideo CreateVideo(
        string filePath = @"C:\video\input.mp4",
        string container = "mp4",
        string videoCodec = "h264",
        IReadOnlyList<string>? audioCodecs = null,
        int width = 1920,
        int height = 1080,
        double framesPerSecond = 29.97,
        TimeSpan? duration = null,
        long? bitrate = 5_000_000,
        string? formatName = null,
        double? rawFramesPerSecond = null,
        double? averageFramesPerSecond = null,
        long? primaryAudioBitrate = 128_000,
        int? primaryAudioSampleRate = null,
        int? primaryAudioChannels = null)
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs ?? ["aac"],
            width: width,
            height: height,
            framesPerSecond: framesPerSecond,
            duration: duration ?? TimeSpan.FromMinutes(10),
            bitrate: bitrate,
            formatName: formatName,
            rawFramesPerSecond: rawFramesPerSecond,
            averageFramesPerSecond: averageFramesPerSecond,
            primaryAudioBitrate: primaryAudioBitrate,
            primaryAudioSampleRate: primaryAudioSampleRate,
            primaryAudioChannels: primaryAudioChannels);
    }
}
