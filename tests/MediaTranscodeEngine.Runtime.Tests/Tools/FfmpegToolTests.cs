using FluentAssertions;
using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.Tests.Logging;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Videos;
using MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

namespace MediaTranscodeEngine.Runtime.Tests.Tools;

/*
Это тесты ffmpeg-based tool-строителей.
Они проверяют, что из плана и сценарного execution spec собираются корректные команды.
*/
/// <summary>
/// Verifies ffmpeg execution building from plans and scenario-specific execution specs.
/// </summary>
public sealed class FfmpegToolTests
{
    [Fact]
    public void BuildExecution_WhenPlanDoesNothing_ReturnsEmptyRecipe()
    {
        var sut = CreateSut();
        var video = CreateVideo();
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: video.FilePath);

        var actual = BuildExecution(sut, video, plan);

        actual.ToolName.Should().Be("ffmpeg");
        actual.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WhenMp4TargetAndExecutionSpecAreProvided_ReturnsTrue()
    {
        var sut = CreateToH264GpuSut();
        var plan = CreatePlan(
            copyVideo: true,
            copyAudio: true,
            targetContainer: "mp4",
            outputPath: @"C:\video\input.mp4");
        var spec = CreateToH264GpuExecutionSpec(optimizeForFastStart: true);

        var actual = sut.CanHandle(plan, spec);

        actual.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WhenVideoEncodeSpecIsMissingVideoPayload_ReturnsFalse()
    {
        var sut = CreateToH264GpuSut();
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: true,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetContainer: "mp4",
            outputPath: @"C:\video\input.mp4");
        var spec = new ToH264GpuExecutionSpec(
            new ToH264GpuExecutionSpec.MuxExecution(optimizeForFastStart: true));

        var actual = sut.CanHandle(plan, spec);

        actual.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_WhenAudioEncodeSpecIsMissingAudioPayload_ReturnsFalse()
    {
        var sut = CreateToH264GpuSut();
        var plan = CreatePlan(
            copyVideo: true,
            copyAudio: false,
            synchronizeAudio: true,
            targetContainer: "mp4",
            outputPath: @"C:\video\input.mp4");
        var spec = new ToH264GpuExecutionSpec(
            new ToH264GpuExecutionSpec.MuxExecution(optimizeForFastStart: true));

        var actual = sut.CanHandle(plan, spec);

        actual.Should().BeFalse();
    }

    [Fact]
    public void BuildExecution_WhenContainerChangesWithCopyStreams_BuildsCopyCommandAndDeleteStep()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-c:a copy");
        actual.Commands[0].Should().Contain("-avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("-fflags +genpts");
        actual.Commands[0].Should().Contain("-sn");
        actual.Commands[0].Should().Contain("-max_muxing_queue_size 4096");
        actual.Commands[0].Should().Contain("\"C:\\video\\input.mkv\"");
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mp4\"");
    }

    [Fact]
    public void BuildExecution_WhenMp4OutputIsRequested_AddsFaststart()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mp4", filePath: @"C:\video\input.m4v");
        var plan = CreatePlan(
            copyVideo: true,
            copyAudio: true,
            targetContainer: "mp4",
            outputPath: @"C:\video\input.mp4");
        var spec = CreateToH264GpuExecutionSpec(
            optimizeForFastStart: true,
            mapPrimaryAudioOnly: true);

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-map 0:a:0? -c:a copy");
        actual.Commands[0].Should().Contain("-movflags +faststart");
        actual.Commands[0].Should().Contain("\"C:\\video\\input.mp4\"");
    }

    [Fact]
    public void BuildExecution_WhenMp4RemuxNeedsFaststart_DoesNotReturnEmptyRecipe()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mp4", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: true,
            copyAudio: true,
            targetContainer: "mp4",
            outputPath: @"C:\video\input.mp4");
        var spec = CreateToH264GpuExecutionSpec(optimizeForFastStart: true);

        var actual = sut.BuildExecution(video, plan, spec);

        actual.IsEmpty.Should().BeFalse();
        actual.Commands.Should().HaveCount(3);
        actual.Commands[0].Should().Contain("\"C:\\video\\input_temp.mp4\"");
        actual.Commands[0].Should().Contain("-movflags +faststart");
        actual.Commands[2].Should().Be("ren \"C:\\video\\input_temp.mp4\" \"input.mp4\"");
    }

    [Fact]
    public void BuildExecution_WhenPathsContainSpaces_QuotesInputOutputAndDeleteSteps()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", filePath: @"C:\video\my file (1).mp4");
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: @"C:\video\my file (1).mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-i \"C:\\video\\my file (1).mp4\"");
        actual.Commands[0].Should().Contain("\"C:\\video\\my file (1).mkv\"");
        actual.Commands[1].Should().Be("del \"C:\\video\\my file (1).mp4\"");
    }

    [Fact]
    public void BuildExecution_WhenVideoEncodingIsRequired_BuildsNvencCommandWithAudioEncode()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("-c:v h264_nvenc");
        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().Contain("-avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("+igndts");
        actual.Commands[0].Should().NotContain("-fps_mode:v cfr");
        actual.Commands[0].Should().NotContain(" -r ");
        actual.Commands[0].Should().NotContain("-af \"aresample=async=1:first_pts=0\"");
        actual.Commands[0].Should().Contain("-cq 21");
        actual.Commands[0].Should().Contain("-maxrate 5.2M -bufsize 10.4M");
    }

    [Fact]
    public void BuildExecution_WhenPrimaryAudioMappingIsRequested_MapsFirstAudioOnly()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", audioCodecs: ["aac", "ac3"], filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4");
        var spec = CreateToH264GpuExecutionSpec(mapPrimaryAudioOnly: true);

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-map 0:a:0? -c:a aac");
        actual.Commands[0].Should().NotContain("-map 0:a? -c:a aac");
    }

    [Fact]
    public void BuildExecution_WhenOrdinaryEncodeUsesProfileOnlyRequest_UsesOrdinaryEncodeProfileDefaults()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 21");
        actual.Commands[0].Should().Contain("-maxrate 3.4M -bufsize 6.8M");
    }

    [Fact]
    public void BuildExecution_WhenOrdinaryEncodeUsesFastAutoSample_UsesBitrateGuidedSettings()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            filePath: @"C:\video\input.mp4",
            bitrate: 10_000_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            videoSettings: new VideoSettingsRequest(autoSampleMode: "fast"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 21");
        actual.Commands[0].Should().Contain("-maxrate 5.2M -bufsize 10.4M");
    }

    [Fact]
    public void BuildExecution_WhenExplicitVbrSettingsAreProvided_UsesRequestedVideoBitrateTokens()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4");
        var spec = CreateToH264GpuExecutionSpec(
            videoBitrateKbps: 1400,
            videoMaxrateKbps: 2200,
            videoBufferSizeKbps: 4400);

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-rc vbr -b:v 1400k -maxrate 2200k -bufsize 4400k");
    }

    [Fact]
    public void BuildExecution_WhenExplicitCqSettingsAreProvided_UsesRequestedCqTokens()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4");
        var spec = CreateToH264GpuExecutionSpec(videoCq: 21);

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-rc vbr_hq -cq 21 -b:v 0");
        actual.Commands[0].Should().NotContain("-maxrate ");
    }

    [Fact]
    public void BuildExecution_WhenToH264GpuPresetIsNotSpecified_UsesP6Preset()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4");
        var spec = CreateToH264GpuExecutionSpec(videoCq: 21);

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-preset p6");
    }

    [Fact]
    public void BuildExecution_WhenAudioEncodingOptionsAreProvided_UsesRequestedAudioSettings()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", audioCodecs: ["ac3"], filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4");
        var spec = CreateToH264GpuExecutionSpec(
            audioBitrateKbps: 96,
            audioSampleRate: 48000,
            audioChannels: 1,
            audioFilter: "aresample=48000:async=1:first_pts=0");

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-c:a aac -ar 48000 -ac 1 -b:a 96k -af \"aresample=48000:async=1:first_pts=0\"");
    }

    [Fact]
    public void BuildExecution_WhenToH264GpuPlanSynchronizesAudioWithCopiedVideo_UsesSharedRepairPath()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: true,
            copyAudio: false,
            fixTimestamps: true,
            synchronizeAudio: true,
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4");
        var spec = CreateToH264GpuExecutionSpec(
            mapPrimaryAudioOnly: true,
            audioSampleRate: 48000,
            audioChannels: 2,
            audioFilter: "aresample=async=1:first_pts=0");

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-copytb 1");
        actual.Commands[0].Should().Contain("-map 0:a:0? -c:a aac");
        actual.Commands[0].Should().Contain("-ar 48000 -ac 2 -b:a 192k");
        actual.Commands[0].Should().Contain("-af \"aresample=async=1:first_pts=0\"");
        actual.Commands[0].Should().Contain("-fflags +genpts+igndts -avoid_negative_ts make_zero");
    }

    [Fact]
    public void BuildExecution_WhenVideoFilterIsProvided_UsesRequestedFilter()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4");
        var spec = CreateToH264GpuExecutionSpec(
            useHardwareDecode: false,
            videoFilter: "hqdn3d=1.2:1.2:6:6",
            pixelFormat: "yuv420p");

        var actual = sut.BuildExecution(video, plan, spec);

        actual.Commands[0].Should().Contain("-vf \"hqdn3d=1.2:1.2:6:6\"");
        actual.Commands[0].Should().Contain("-pix_fmt yuv420p");
        actual.Commands[0].Should().NotContain("-hwaccel cuda -hwaccel_output_format cuda");
    }

    [Fact]
    public void BuildExecution_WhenEncoderPresetIsOverridden_UsesRequestedPreset()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            encoderPreset: "p5",
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-preset p5");
    }

    [Fact]
    public void BuildExecution_WhenToMkvGpuPresetIsMissing_ThrowsInvariantViolation()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = new TranscodePlan(
            targetContainer: "mkv",
            video: new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: VideoCompatibilityProfile.H264High),
            audio: new EncodeAudioPlan(),
            keepSource: false,
            outputPath: @"C:\video\input.mkv");

        Action action = () => BuildExecution(sut, video, plan);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Encoder preset must be resolved before tool rendering.*");
    }

    [Fact]
    public void BuildExecution_WhenEncodeCqIsOverriddenWithoutDownscale_UsesRequestedCq()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            videoSettings: new VideoSettingsRequest(cq: 23),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 23");
        actual.Commands[0].Should().Contain("-maxrate 4.4M -bufsize 8.8M");
    }

    [Fact]
    public void BuildExecution_WhenH264CompatibilityProfileIsProvided_UsesRequestedProfile()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = new TranscodePlan(
            targetContainer: "mkv",
            video: new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: VideoCompatibilityProfile.H264Main,
                EncoderPreset: "p6"),
            audio: new EncodeAudioPlan(),
            keepSource: false,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-profile:v main -level:v 4.0");
    }

    [Fact]
    public void BuildExecution_WhenAudioTracksAreMissing_UsesOptionalAudioCopyPath()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", audioCodecs: [], filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-map 0:a? -c:a copy");
        actual.Commands[0].Should().NotContain("-c:a aac");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleIsRequested_AddsCudaScalePath()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: null,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Commands[0].Should().Contain("scale_cuda=-2:576:interp_algo=bicubic:format=nv12");
        actual.Commands[0].Should().Contain("-avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("+igndts");
        actual.Commands[0].Should().NotContain("-fps_mode:v cfr");
        actual.Commands[0].Should().NotContain(" -r ");
        actual.Commands[0].Should().NotContain("-af \"aresample=async=1:first_pts=0\"");
        actual.Commands[0].Should().Contain("-cq 26");
        actual.Commands[0].Should().Contain("-maxrate 3.4M -bufsize 6.9M");
    }

    [Fact]
    public void BuildExecution_WhenDownscale480UsesDefaultProfile_Uses480Defaults()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", height: 692, filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 480,
            videoSettings: null,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("scale_cuda=-2:480:interp_algo=bicubic:format=nv12");
        actual.Commands[0].Should().Contain("-cq 27");
        actual.Commands[0].Should().Contain("-maxrate 2.6M -bufsize 5.2M");
    }

    [Fact]
    public void BuildExecution_WhenDownscale720UsesDefaultProfile_Uses720Defaults()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", height: 1080, filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 720,
            videoSettings: null,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("scale_cuda=-2:720:interp_algo=bicubic:format=nv12");
        actual.Commands[0].Should().Contain("-cq 23");
        actual.Commands[0].Should().Contain("-maxrate 4.5M -bufsize 9M");
    }

    [Fact]
    public void BuildExecution_WhenDownscale424UsesDefaultProfile_Uses424Defaults()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", height: 576, filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 424,
            videoSettings: null,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("scale_cuda=-2:424:interp_algo=bicubic:format=nv12");
        actual.Commands[0].Should().Contain("-cq 28");
        actual.Commands[0].Should().Contain("-maxrate 2.1M -bufsize 4.2M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleUsesAnimeDefaultProfile_UsesAnimeDefaults()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 23");
        actual.Commands[0].Should().Contain("-maxrate 2.4M -bufsize 4.8M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleUsesFilmHighProfile_UsesSelectedQualityDefaults()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(qualityProfile: "high"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 24");
        actual.Commands[0].Should().Contain("-maxrate 3.7M -bufsize 7.4M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleTargets576AtSource60Fps_UsesH264Level32()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            width: 1920,
            height: 1080,
            framesPerSecond: 59.94,
            filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: null,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-profile:v high -level:v 3.2");
        actual.Commands[0].Should().NotContain(" -r ");
    }

    [Fact]
    public void BuildExecution_WhenEncoding1080pAtSource60Fps_UsesH264Level42()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            width: 1920,
            height: 1080,
            framesPerSecond: 59.94,
            filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-profile:v high -level:v 4.2");
        actual.Commands[0].Should().NotContain(" -r ");
    }

    [Fact]
    public void BuildExecution_WhenEncoding1080pAtSource30Fps_UsesH264Level40()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            width: 1920,
            height: 1080,
            framesPerSecond: 29.97,
            filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-profile:v high -level:v 4.0");
        actual.Commands[0].Should().NotContain(" -r ");
    }

    [Fact]
    public void BuildExecution_WhenToH264GpuDownscaleAlgorithmIsMissing_ThrowsInvariantViolation()
    {
        var sut = CreateToH264GpuSut();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv");
        var plan = new TranscodePlan(
            targetContainer: "mp4",
            video: new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: VideoCompatibilityProfile.H264High,
                Downscale: new DownscaleRequest(576),
                EncoderPreset: "p6"),
            audio: new EncodeAudioPlan(),
            keepSource: false,
            outputPath: @"C:\video\input.mp4");
        var spec = CreateToH264GpuExecutionSpec(videoCq: 21);

        Action action = () => sut.BuildExecution(video, plan, spec);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Downscale algorithm must be resolved before tool rendering.*");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleAlgorithmIsOverridden_UsesExplicitAlgorithm()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: null,
            downscale: new DownscaleRequest(576, "lanczos"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("interp_algo=lanczos");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleMaxrateIsOverriddenWithoutBufsize_ComputesBufsizeByMultiplier()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(maxrate: 2.5m),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-maxrate 2.5M -bufsize 5M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleCqIsOverridden_UsesRateModelAndClamp()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default", cq: 21),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 21");
        actual.Commands[0].Should().Contain("-maxrate 3M -bufsize 6M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleUsesBucketSpecificBounds_UsesSourceBucketSpecificClamp()
    {
        var sut = CreateSut();
        var hdVideo = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\hd.mkv", height: 720);
        var fhdVideo = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\fhd.mkv", height: 1080);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "mult", qualityProfile: "default", cq: 21),
            outputPath: @"C:\video\output.mkv");

        var hdActual = BuildExecution(sut, hdVideo, plan);
        var fhdActual = BuildExecution(sut, fhdVideo, plan);

        hdActual.Commands[0].Should().Contain("-cq 21");
        hdActual.Commands[0].Should().Contain("-maxrate 3.6M -bufsize 7.2M");
        fhdActual.Commands[0].Should().Contain("-cq 21");
        fhdActual.Commands[0].Should().Contain("-maxrate 2.8M -bufsize 5.6M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleManualCqIsProvided_SkipsAutoSample()
    {
        var providerCalls = 0;
        var logger = new ListLogger<ToMkvGpuFfmpegTool>();
        var sut = CreateSut(sampleReductionProvider: (_, _, _, _) =>
        {
            providerCalls++;
            return 45m;
        }, logger: logger);
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10),
            bitrate: 8_000_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default", cq: 21),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        providerCalls.Should().Be(0);
        actual.Commands[0].Should().Contain("-cq 21");
        actual.Commands[0].Should().Contain("-maxrate 3M -bufsize 6M");
        logger.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Information &&
                                                 entry.Message.Contains("Video settings autosample resolved.", StringComparison.Ordinal) &&
                                                 Equals(entry.Properties["Mode"], "none") &&
                                                 Equals(entry.Properties["Path"], "skip") &&
                                                 Equals(entry.Properties["Reason"], "manual_override"));
    }

    [Fact]
    public void BuildExecution_WhenDownscaleFastAutoSampleIsRequested_UsesFastResolvedSettings()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10),
            bitrate: 8_000_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(autoSampleMode: "fast"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 24");
        actual.Commands[0].Should().Contain("-maxrate 4.2M -bufsize 8.4M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleFastAutoSampleIsRequestedAndProbeBitrateIsMissing_UsesFileSizeDurationEstimate()
    {
        var logger = new ListLogger<ToMkvGpuFfmpegTool>();
        var sut = CreateSut(logger: logger);
        var filePath = CreateTempFileWithLength(10_000_000);

        try
        {
            var video = CreateVideo(
                container: "mp4",
                videoCodec: "h264",
                filePath: filePath,
                height: 1080,
                duration: TimeSpan.FromSeconds(10),
                bitrate: null);
            var plan = CreatePlan(
                copyVideo: false,
                copyAudio: false,
                targetVideoCodec: "h264",
                preferredBackend: "gpu",
                targetHeight: 576,
                videoSettings: new VideoSettingsRequest(autoSampleMode: "fast"),
                outputPath: Path.ChangeExtension(filePath, ".mkv"));

            var actual = BuildExecution(sut, video, plan);

            actual.Commands[0].Should().Contain("-cq 24");
            actual.Commands[0].Should().Contain("-maxrate 4.2M -bufsize 8.4M");
            logger.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Information &&
                                                     entry.Message.Contains("Video settings autosample resolved.", StringComparison.Ordinal) &&
                                                     Equals(entry.Properties["Mode"], "fast") &&
                                                     Equals(entry.Properties["SourceBitrateOrigin"], "file_size_estimate") &&
                                                     Equals(entry.Properties["SourceBitrateMbps"], "8"));
        }
        finally
        {
            TryDelete(filePath);
        }
    }

    [Fact]
    public void BuildExecution_WhenDownscaleFastAutoSampleIsRequested_UsesFastAutosamplePath()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10),
            bitrate: 4_500_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(autoSampleMode: "fast"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 28");
        actual.Commands[0].Should().Contain("-maxrate 2.6M -bufsize 5.2M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleFastAutoSampleIsRequestedButDurationIsMissing_UsesBaseProfileSettings()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.Zero,
            bitrate: 8_000_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(autoSampleMode: "fast"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 26");
        actual.Commands[0].Should().Contain("-maxrate 3.4M -bufsize 6.9M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleAccurateAutoSampleIsRequested_UsesMeasuredSettings()
    {
        IReadOnlyList<VideoSettingsSampleWindow>? actualWindows = null;
        var logger = new ListLogger<ToMkvGpuFfmpegTool>();
        var sut = CreateSut(sampleReductionProvider: (_, _, settings, windows) =>
        {
            actualWindows = windows;
            settings.Cq.Should().Be(23);
            settings.Maxrate.Should().Be(2.4m);
            return 45m;
        }, logger: logger);
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10));
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default", autoSampleMode: "accurate"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-cq 23");
        actual.Commands[0].Should().Contain("-maxrate 2.4M -bufsize 4.8M");
        actualWindows.Should().Equal(
            new VideoSettingsSampleWindow(StartSeconds: 105, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 285, DurationSeconds: 30),
            new VideoSettingsSampleWindow(StartSeconds: 465, DurationSeconds: 30));
        logger.Entries.Should().Contain(entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Information &&
                                                 entry.Message.Contains("Video settings autosample resolved.", StringComparison.Ordinal) &&
                                                 Equals(entry.Properties["Mode"], "accurate") &&
                                                 Equals(entry.Properties["Path"], "sample") &&
                                                 Equals(entry.Properties["Reason"], "resolved") &&
                                                 Equals(entry.Properties["Windows"], "105+30,285+30,465+30") &&
                                                 Equals(entry.Properties["LastReductionPercent"], 45m));
    }

    [Fact]
    public void BuildExecution_WhenDownscaleAutoSampleModeIsOmitted_UsesHybridByDefault()
    {
        var providerCalls = 0;
        var sut = CreateSut(sampleReductionProvider: (_, _, _, _) =>
        {
            providerCalls++;
            return 45m;
        });
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10));
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        providerCalls.Should().Be(1);
        actual.Commands[0].Should().Contain("-cq 23");
        actual.Commands[0].Should().Contain("-maxrate 2.4M -bufsize 4.8M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleHybridAutoSampleFastSeedIsInCorridor_SkipsMeasuredRefinement()
    {
        var providerCalls = 0;
        var sut = CreateSut(
            videoSettingsProfiles: CreateVideoSettingsProfiles(maxIterations: 1, hybridAccurateIterations: 1),
            sampleReductionProvider: (_, _, _, _) =>
            {
                providerCalls++;
                return 45m;
            });
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10),
            bitrate: 5_184_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default", autoSampleMode: "hybrid"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        providerCalls.Should().Be(0);
        actual.Commands[0].Should().Contain("-cq 23");
        actual.Commands[0].Should().Contain("-maxrate 2.4M -bufsize 4.8M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleHybridAutoSampleFastSeedIsOutsideCorridor_UsesMeasuredRefinement()
    {
        VideoSettingsDefaults? actualStart = null;
        var sut = CreateSut(
            videoSettingsProfiles: CreateVideoSettingsProfiles(maxIterations: 1, hybridAccurateIterations: 1),
            sampleReductionProvider: (_, _, settings, _) =>
            {
                actualStart ??= settings;
                return 45m;
            });
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10),
            bitrate: 4_000_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default", autoSampleMode: "hybrid"),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actualStart.Should().NotBeNull();
        actualStart!.Cq.Should().Be(24);
        actualStart.Maxrate.Should().Be(2.0m);
        actual.Commands[0].Should().Contain("-cq 24");
        actual.Commands[0].Should().Contain("-maxrate 2M -bufsize 4M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleManualMaxrateIsProvided_SkipsAutoSample()
    {
        var providerCalls = 0;
        var sut = CreateSut(sampleReductionProvider: (_, _, _, _) =>
        {
            providerCalls++;
            return 45m;
        });
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10),
            bitrate: 8_000_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default", maxrate: 2.7m),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        providerCalls.Should().Be(0);
        actual.Commands[0].Should().Contain("-maxrate 2.7M -bufsize 5.4M");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleManualBufsizeIsProvided_SkipsAutoSample()
    {
        var providerCalls = 0;
        var sut = CreateSut(sampleReductionProvider: (_, _, _, _) =>
        {
            providerCalls++;
            return 45m;
        });
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "h264",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            duration: TimeSpan.FromMinutes(10),
            bitrate: 8_000_000);
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default", bufsize: 7.0m),
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        providerCalls.Should().Be(0);
        actual.Commands[0].Should().Contain("-maxrate 2.4M -bufsize 7M");
    }

    [Fact]
    public void BuildExecution_WhenOverlayBackgroundIsRequested_UsesFilterComplexPath()
    {
        var sut = CreateSut();
        var video = CreateVideo(width: 720, height: 1280, container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            applyOverlayBackground: true,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-filter_complex");
        actual.Commands[0].Should().Contain("-map \"[v]\"");
        actual.Commands[0].Should().NotContain("-map 0:v:0 -vf");
    }

    [Fact]
    public void BuildExecution_WhenOverlayBackgroundUsesPortraitSource_RotatesCanvasToLandscape()
    {
        var sut = CreateSut();
        var video = CreateVideo(width: 720, height: 1280, container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            applyOverlayBackground: true,
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("scale=1280:-1,crop=1280:720");
    }

    [Fact]
    public void BuildExecution_WhenOverlayBackgroundHasNonPositiveDimensions_Uses1920x1080FallbackCanvas()
    {
        var sut = CreateSut();
        var video = CreateVideo(width: 0, height: 0, container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mkv",
            applyOverlayBackground: true);

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("scale=1920:-1,crop=1920:1080");
    }

    [Fact]
    public void BuildExecution_WhenOverlayBackgroundAndDownscale576AreRequested_UsesOverlayCudaAndProfileAlgorithm()
    {
        var sut = CreateSut();
        var video = CreateVideo(width: 1920, height: 1080, container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetHeight: 576,
            videoSettings: new VideoSettingsRequest(contentProfile: "anime", qualityProfile: "default"),
            outputPath: @"C:\video\input.mkv",
            applyOverlayBackground: true);

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-filter_complex");
        actual.Commands[0].Should().Contain("overlay_cuda");
        actual.Commands[0].Should().Contain("scale_cuda=1024:-2:interp_algo=bicubic:format=nv12");
        actual.Commands[0].Should().Contain("crop=1024:576");
    }

    [Fact]
    public void BuildExecution_WhenReplacingMkvInPlace_UsesTemporaryOutputAndMoveStep()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mkv", filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands.Should().HaveCount(3);
        actual.Commands[0].Should().Contain("\"C:\\video\\input_temp.mkv\"");
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mkv\"");
        actual.Commands[2].Should().Be("ren \"C:\\video\\input_temp.mkv\" \"input.mkv\"");
    }

    [Fact]
    public void BuildExecution_WhenOnlyAudioEncodingIsNeededForContainerChange_UsesSoftSanitizeWithoutIgndts()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", audioCodecs: ["mp3"], filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(copyVideo: true, copyAudio: false, outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().NotContain("-af \"aresample=async=1:first_pts=0\"");
        actual.Commands[0].Should().NotContain("-c:a copy");
        actual.Commands[0].Should().Contain("-avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("-fflags +genpts");
        actual.Commands[0].Should().NotContain("+igndts");
    }

    [Fact]
    public void BuildExecution_WhenOnlyAudioEncodingIsNeededForMkvWithoutSyncAudio_UsesOnlyAvoidNegativeTs()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["ac3"], filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(copyVideo: true, copyAudio: false, outputPath: @"C:\video\input_out.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("-fflags +genpts");
        actual.Commands[0].Should().NotContain("+igndts");
    }

    [Fact]
    public void BuildExecution_WhenOnlyAudioEncodingNeedsTimestampRepair_UsesAsyncResample()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["ac3"], filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: true,
            copyAudio: false,
            fixTimestamps: true,
            outputPath: @"C:\video\input_out.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().Contain("-af \"aresample=async=1:first_pts=0\"");
        actual.Commands[0].Should().Contain("-fflags +genpts+igndts -avoid_negative_ts make_zero");
    }

    [Fact]
    public void BuildExecution_WhenSyncAudioIsRequestedWithCopiedVideo_ForcesAacEncodeAndAsyncResample()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: true,
            copyAudio: false,
            fixTimestamps: true,
            outputPath: @"C:\video\input_out.mkv",
            synchronizeAudio: true);

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-copytb 1");
        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().Contain("-af \"aresample=async=1:first_pts=0\"");
        actual.Commands[0].Should().Contain("-fflags +genpts+igndts -avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("-fps_mode:v cfr");
        actual.Commands[0].Should().NotContain("-c:a copy");
    }

    [Fact]
    public void BuildExecution_WhenCommandIsBuilt_AlwaysDisablesSubtitles()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(copyVideo: false, copyAudio: false, targetVideoCodec: "h264", preferredBackend: "gpu", outputPath: @"C:\video\input.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-sn");
    }

    [Fact]
    public void BuildExecution_WhenTargetFrameRateIsRequested_RendersRequestedCfrRate()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["aac"], framesPerSecond: 59.94, filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetFramesPerSecond: 50,
            outputPath: @"C:\video\input_out.mkv");

        var actual = BuildExecution(sut, video, plan);

        actual.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Commands[0].Should().Contain("-avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("+igndts");
        actual.Commands[0].Should().Contain("-fps_mode:v cfr");
        actual.Commands[0].Should().NotContain("-pix_fmt yuv420p");
        actual.Commands[0].Should().Contain("-r 50");
        actual.Commands[0].Should().Contain("-g 100");
        actual.Commands[0].Should().NotContain("-af \"aresample=async=1:first_pts=0\"");
    }

    [Fact]
    public void CanHandle_WhenFrameInterpolationIsRequested_ReturnsFalse()
    {
        var sut = CreateSut();
        var plan = CreatePlan(
            copyVideo: false,
            copyAudio: false,
            targetVideoCodec: "h264",
            preferredBackend: "gpu",
            targetFramesPerSecond: 60,
            useFrameInterpolation: true);

        var actual = sut.CanHandle(plan, null);

        actual.Should().BeFalse();
    }

    private static ToMkvGpuToolHarness CreateSut(
        string ffmpegPath = "ffmpeg",
        VideoSettingsProfiles? videoSettingsProfiles = null,
        Func<string, int, VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? sampleReductionProvider = null,
        Microsoft.Extensions.Logging.ILogger<ToMkvGpuFfmpegTool>? logger = null)
    {
        var profiles = videoSettingsProfiles ?? VideoSettingsProfiles.Default;
        var tool = new ToMkvGpuFfmpegTool(
            ffmpegPath,
            logger ?? CreateLogger<ToMkvGpuFfmpegTool>());
        var scenario = new ToMkvGpuScenario(
            new ToMkvGpuRequest(),
            profiles,
            sampleReductionProvider);
        return new ToMkvGpuToolHarness(tool, scenario);
    }

    private static ToolExecution BuildExecution(ToMkvGpuToolHarness sut, SourceVideo video, TranscodePlan plan)
    {
        return sut.BuildExecution(video, plan);
    }

    private static ToH264GpuFfmpegTool CreateToH264GpuSut(
        string ffmpegPath = "ffmpeg",
        Microsoft.Extensions.Logging.ILogger<ToH264GpuFfmpegTool>? logger = null)
    {
        return new ToH264GpuFfmpegTool(
            ffmpegPath,
            logger ?? CreateLogger<ToH264GpuFfmpegTool>());
    }

    private static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
    {
        return new ListLogger<T>();
    }

    private static VideoSettingsProfiles CreateVideoSettingsProfiles(int maxIterations, int hybridAccurateIterations)
    {
        var profile = VideoSettings576Profile.Create();
        return VideoSettingsProfiles.Create(
            new VideoSettingsProfile(
                targetHeight: profile.TargetHeight,
                defaultContentProfile: profile.DefaultContentProfile,
                defaultQualityProfile: profile.DefaultQualityProfile,
                rateModel: profile.RateModel,
                autoSampling: profile.AutoSampling with
                {
                    MaxIterations = maxIterations,
                    HybridAccurateIterations = hybridAccurateIterations
                },
                sourceBuckets: profile.SourceBuckets,
                defaults: profile.Defaults,
                globalContentRanges: profile.GlobalContentRanges,
                globalQualityRanges: profile.GlobalQualityRanges));
    }

    private static SourceVideo CreateVideo(
        string container = "mkv",
        string videoCodec = "h264",
        IReadOnlyList<string>? audioCodecs = null,
        int width = 1920,
        int height = 1080,
        double framesPerSecond = 29.97,
        string filePath = @"C:\video\input.mkv",
        TimeSpan? duration = null,
        long? bitrate = null,
        string? formatName = null,
        double? rawFramesPerSecond = null,
        double? averageFramesPerSecond = null,
        long? primaryAudioBitrate = null,
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

    private static string CreateTempFileWithLength(long lengthBytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mte-ffmpegtool-test-{Guid.NewGuid():N}.mkv");
        using var stream = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        stream.SetLength(lengthBytes);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static TranscodePlan CreatePlan(
        bool copyVideo,
        bool copyAudio,
        string? targetVideoCodec = null,
        string? preferredBackend = null,
        VideoCompatibilityProfile? videoCompatibilityProfile = null,
        int? targetHeight = null,
        double? targetFramesPerSecond = null,
        bool useFrameInterpolation = false,
        VideoSettingsRequest? videoSettings = null,
        DownscaleRequest? downscale = null,
        bool fixTimestamps = false,
        bool keepSource = false,
        string? encoderPreset = null,
        string outputPath = @"C:\video\input.mkv",
        bool applyOverlayBackground = false,
        bool synchronizeAudio = false,
        string targetContainer = "mkv")
    {
        downscale ??= targetHeight.HasValue ? new DownscaleRequest(targetHeight.Value, "bicubic") : null;
        VideoPlan videoPlan = copyVideo
            ? new CopyVideoPlan()
            : new EncodeVideoPlan(
                TargetVideoCodec: targetVideoCodec ?? "h264",
                PreferredBackend: preferredBackend,
                CompatibilityProfile: videoCompatibilityProfile ?? ResolveDefaultCompatibilityProfile(copyVideo, targetVideoCodec),
                TargetFramesPerSecond: targetFramesPerSecond,
                UseFrameInterpolation: useFrameInterpolation,
                VideoSettings: videoSettings,
                Downscale: downscale,
                EncoderPreset: encoderPreset ?? "p6");
        AudioPlan audioPlan = copyAudio
            ? new CopyAudioPlan()
            : synchronizeAudio
                ? new SynchronizeAudioPlan()
                : fixTimestamps
                    ? new RepairAudioPlan()
                    : new EncodeAudioPlan();

        return new TranscodePlan(
            targetContainer: targetContainer,
            video: videoPlan,
            audio: audioPlan,
            keepSource: keepSource,
            outputPath: outputPath,
            applyOverlayBackground: applyOverlayBackground);
    }

    private static ToH264GpuExecutionSpec CreateToH264GpuExecutionSpec(
        bool optimizeForFastStart = false,
        bool mapPrimaryAudioOnly = false,
        bool? useHardwareDecode = null,
        bool? enableAdaptiveQuantization = null,
        int? aqStrength = null,
        int? rcLookahead = null,
        int? videoBitrateKbps = null,
        int? videoMaxrateKbps = null,
        int? videoBufferSizeKbps = null,
        int? videoCq = null,
        string? videoFilter = null,
        string? pixelFormat = null,
        int? audioBitrateKbps = null,
        int? audioSampleRate = null,
        int? audioChannels = null,
        string? audioFilter = null)
    {
        var mux = new ToH264GpuExecutionSpec.MuxExecution(
            optimizeForFastStart: optimizeForFastStart,
            mapPrimaryAudioOnly: mapPrimaryAudioOnly);
        var video = new ToH264GpuExecutionSpec.VideoExecution(
            useHardwareDecode: useHardwareDecode ?? false,
            rateControl: CreateVideoRateControl(
                videoBitrateKbps,
                videoMaxrateKbps,
                videoBufferSizeKbps,
                videoCq),
            adaptiveQuantization: enableAdaptiveQuantization == true
                ? new ToH264GpuExecutionSpec.AdaptiveQuantizationExecution(
                    rcLookahead: rcLookahead ?? 32,
                    strength: aqStrength)
                : null,
            filter: videoFilter,
            pixelFormat: pixelFormat);
        var audio = new ToH264GpuExecutionSpec.AudioExecution(
            bitrateKbps: audioBitrateKbps ?? 192,
            sampleRate: audioSampleRate,
            channels: audioChannels,
            filter: audioFilter);

        return new ToH264GpuExecutionSpec(
            mux: mux,
            video: video,
            audio: audio);
    }

    private static ToH264GpuExecutionSpec.VideoRateControlExecution CreateVideoRateControl(
        int? videoBitrateKbps,
        int? videoMaxrateKbps,
        int? videoBufferSizeKbps,
        int? videoCq)
    {
        if (videoBitrateKbps.HasValue)
        {
            var maxrateKbps = videoMaxrateKbps ?? videoBitrateKbps.Value;
            var bufferSizeKbps = videoBufferSizeKbps ?? (maxrateKbps * 2);
            return new ToH264GpuExecutionSpec.VariableBitrateVideoRateControlExecution(
                bitrateKbps: videoBitrateKbps.Value,
                maxrateKbps: maxrateKbps,
                bufferSizeKbps: bufferSizeKbps);
        }

        if (videoMaxrateKbps.HasValue || videoBufferSizeKbps.HasValue)
        {
            var maxrateKbps = videoMaxrateKbps ?? 3000;
            var bufferSizeKbps = videoBufferSizeKbps ?? (maxrateKbps * 2);
            return new ToH264GpuExecutionSpec.ConstantQualityVideoRateControlExecution(
                cq: videoCq ?? 19,
                maxrateKbps: maxrateKbps,
                bufferSizeKbps: bufferSizeKbps);
        }

        return new ToH264GpuExecutionSpec.ConstantQualityVideoRateControlExecution(cq: videoCq ?? 19);
    }

    private static VideoCompatibilityProfile? ResolveDefaultCompatibilityProfile(bool copyVideo, string? targetVideoCodec)
    {
        if (copyVideo)
        {
            return null;
        }

        return string.Equals(targetVideoCodec, "h264", StringComparison.OrdinalIgnoreCase)
            ? VideoCompatibilityProfile.H264High
            : null;
    }

    private sealed class ToMkvGpuToolHarness
    {
        private readonly ToMkvGpuScenario _scenario;
        private readonly ToMkvGpuFfmpegTool _tool;

        public ToMkvGpuToolHarness(ToMkvGpuFfmpegTool tool, ToMkvGpuScenario scenario)
        {
            _tool = tool;
            _scenario = scenario;
        }

        public ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan)
        {
            var executionSpec = _scenario.BuildExecutionSpec(video, plan);
            return _tool.BuildExecution(video, plan, executionSpec);
        }

        public bool CanHandle(TranscodePlan plan, TranscodeExecutionSpec? executionSpec)
        {
            return _tool.CanHandle(plan, executionSpec);
        }
    }
}
