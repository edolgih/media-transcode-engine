using FluentAssertions;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Tools.Ffmpeg;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests.Tools;

public sealed class FfmpegToolTests
{
    [Fact]
    public void BuildExecution_WhenPlanDoesNothing_ReturnsEmptyRecipe()
    {
        var sut = CreateSut();
        var video = CreateVideo();
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: video.FilePath);

        var actual = sut.BuildExecution(video, plan);

        actual.ToolName.Should().Be("ffmpeg");
        actual.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void BuildExecution_WhenContainerChangesWithCopyStreams_BuildsCopyCommandAndDeleteStep()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: @"C:\video\input.mkv");

        var actual = sut.BuildExecution(video, plan);

        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-c:a copy");
        actual.Commands[0].Should().Contain("-fflags +genpts -avoid_negative_ts make_zero");
        actual.Commands[0].Should().Contain("-sn");
        actual.Commands[0].Should().Contain("-max_muxing_queue_size 4096");
        actual.Commands[0].Should().Contain("\"C:\\video\\input.mkv\"");
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mp4\"");
    }

    [Fact]
    public void BuildExecution_WhenPathsContainSpaces_QuotesInputOutputAndDeleteSteps()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", filePath: @"C:\video\my file (1).mp4");
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: @"C:\video\my file (1).mkv");

        var actual = sut.BuildExecution(video, plan);

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

        var actual = sut.BuildExecution(video, plan);

        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("-c:v h264_nvenc");
        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().Contain("-fflags +genpts+igndts -avoid_negative_ts make_zero");
        actual.Commands[0].Should().Contain("-fps_mode:v cfr");
        actual.Commands[0].Should().Contain("-cq 21");
        actual.Commands[0].Should().Contain("-maxrate 4M -bufsize 8M");
    }

    [Fact]
    public void BuildExecution_WhenAudioTracksAreMissing_UsesOptionalAudioCopyPath()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", audioCodecs: [], filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(copyVideo: true, copyAudio: true, outputPath: @"C:\video\input.mkv");

        var actual = sut.BuildExecution(video, plan);

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
            outputPath: @"C:\video\input.mkv");

        var actual = sut.BuildExecution(video, plan);

        actual.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Commands[0].Should().Contain("scale_cuda=-2:576:interp_algo=bilinear:format=nv12");
        actual.Commands[0].Should().Contain("-cq 19");
        actual.Commands[0].Should().Contain("-maxrate 3M -bufsize 6M");
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

        var actual = sut.BuildExecution(video, plan);

        actual.Commands[0].Should().Contain("-filter_complex");
        actual.Commands[0].Should().Contain("-map \"[v]\"");
        actual.Commands[0].Should().NotContain("-map 0:v:0 -vf");
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

        var actual = sut.BuildExecution(video, plan);

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

        var actual = sut.BuildExecution(video, plan);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-fflags +genpts -avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("+igndts");
    }

    [Fact]
    public void BuildExecution_WhenOnlyAudioEncodingIsNeededForMkvWithoutSyncAudio_UsesOnlyAvoidNegativeTs()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["ac3"], filePath: @"C:\video\input.mkv");
        var plan = CreatePlan(copyVideo: true, copyAudio: false, outputPath: @"C:\video\input_out.mkv");

        var actual = sut.BuildExecution(video, plan);

        actual.Commands[0].Should().Contain("-c:v copy");
        actual.Commands[0].Should().Contain("-avoid_negative_ts make_zero");
        actual.Commands[0].Should().NotContain("-fflags +genpts");
        actual.Commands[0].Should().NotContain("+igndts");
    }

    [Fact]
    public void BuildExecution_WhenCommandIsBuilt_AlwaysDisablesSubtitles()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");
        var plan = CreatePlan(copyVideo: false, copyAudio: false, targetVideoCodec: "h264", preferredBackend: "gpu", outputPath: @"C:\video\input.mkv");

        var actual = sut.BuildExecution(video, plan);

        actual.Commands[0].Should().Contain("-sn");
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

        var actual = sut.CanHandle(plan);

        actual.Should().BeFalse();
    }

    private static FfmpegTool CreateSut(string ffmpegPath = "ffmpeg")
    {
        return new FfmpegTool(ffmpegPath);
    }

    private static SourceVideo CreateVideo(
        string container = "mkv",
        string videoCodec = "h264",
        IReadOnlyList<string>? audioCodecs = null,
        int width = 1920,
        int height = 1080,
        double framesPerSecond = 29.97,
        string filePath = @"C:\video\input.mkv")
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs ?? ["aac"],
            width: width,
            height: height,
            framesPerSecond: framesPerSecond,
            duration: TimeSpan.FromMinutes(10));
    }

    private static TranscodePlan CreatePlan(
        bool copyVideo,
        bool copyAudio,
        string? targetVideoCodec = null,
        string? preferredBackend = null,
        int? targetHeight = null,
        double? targetFramesPerSecond = null,
        bool useFrameInterpolation = false,
        bool fixTimestamps = false,
        bool keepSource = false,
        string outputPath = @"C:\video\input.mkv",
        bool applyOverlayBackground = false,
        bool synchronizeAudio = false)
    {
        return new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: targetVideoCodec,
            preferredBackend: preferredBackend,
            targetHeight: targetHeight,
            targetFramesPerSecond: targetFramesPerSecond,
            useFrameInterpolation: useFrameInterpolation,
            copyVideo: copyVideo,
            copyAudio: copyAudio,
            fixTimestamps: fixTimestamps,
            keepSource: keepSource,
            outputPath: outputPath,
            applyOverlayBackground: applyOverlayBackground,
            synchronizeAudio: synchronizeAudio);
    }
}
