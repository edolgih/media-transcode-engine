using FluentAssertions;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Policy;
using NSubstitute;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class TranscodeEngineTests
{
    [Fact]
    public void Process_WhenDownscale720Requested_ReturnsNotImplementedRem()
    {
        var (sut, _, _) = CreateSut();
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4", Downscale: 720);

        var actual = sut.Process(request);

        actual.Should().StartWith("REM Downscale 720 not implemented:");
    }

    [Fact]
    public void Process_WhenProbeMissing_ReturnsFfprobeFailedRem()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns((ProbeResult?)null);
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    [Fact]
    public void ProcessWithProbeResult_WhenProbeProvided_BuildsCommandAndSkipsProbeReader()
    {
        var (sut, probeReader, _) = CreateSut();
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");
        var probe = CreateProbe(codec: "h264", audioCodec: "aac", height: 1080);

        var actual = sut.ProcessWithProbeResult(request, probe);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().StartWith("ffmpeg -hide_banner");
        actual.Should().Contain("-map 0:v:0 -c:v copy");
    }

    [Fact]
    public void ProcessWithProbeResult_WhenProbeIsNull_ReturnsFfprobeFailedRemAndSkipsProbeReader()
    {
        var (sut, probeReader, _) = CreateSut();
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.ProcessWithProbeResult(request, null);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    [Fact]
    public void ProcessWithProbeJson_WhenProbeJsonValid_BuildsCommandAndSkipsProbeReader()
    {
        var (sut, probeReader, _) = CreateSut();
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");
        var probeJson = CreateProbeJson();

        var actual = sut.ProcessWithProbeJson(request, probeJson);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().StartWith("ffmpeg -hide_banner");
        actual.Should().Contain("-map 0:v:0 -c:v copy");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{invalid")]
    public void ProcessWithProbeJson_WhenProbeJsonInvalid_ReturnsFfprobeFailedRemAndSkipsProbeReader(string? probeJson)
    {
        var (sut, probeReader, _) = CreateSut();
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.ProcessWithProbeJson(request, probeJson);

        probeReader.DidNotReceive().Read(Arg.Any<string>());
        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenNoVideoStream_ReturnsNoVideoRem()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(new ProbeResult(
            Format: null,
            Streams: new[] { new ProbeStream("audio", "aac") }));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Be("REM Нет видеопотока: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenInfoAndMkvWithoutChanges_ReturnsEmptyString()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mkv", Info: true);

        var actual = sut.Process(request);

        actual.Should().BeEmpty();
    }

    [Fact]
    public void Process_WhenMkvWithoutChanges_ReturnsEmptyString()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mkv", Info: false);

        var actual = sut.Process(request);

        actual.Should().BeEmpty();
    }

    [Fact]
    public void Process_WhenInfoAndProbeMissing_ReturnsDisplayNameWithFfprobeFailedMarker()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns((ProbeResult?)null);
        var request = new TranscodeRequest(InputPath: "C:\\video\\folder\\movie.mp4", Info: true);

        var actual = sut.Process(request);

        actual.Should().Be("movie.mp4: [ffprobe failed]");
    }

    [Fact]
    public void Process_WhenProbeStreamsAreEmpty_ReturnsFfprobeFailedRem()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(new ProbeResult(
            Format: new ProbeFormat(DurationSeconds: 600, BitrateBps: 6_000_000),
            Streams: []));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Be("REM ffprobe failed: C:\\video\\a.mp4");
    }

    [Fact]
    public void Process_WhenInfoAndInputNeedsContainerVideoAndAudioChanges_ReturnsExpectedMarkers()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "hevc", audioCodec: "ac3", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\folder\\movie.mp4", Info: true);

        var actual = sut.Process(request);

        actual.Should().StartWith("movie.mp4: ");
        actual.Should().Contain("container .mp4→mkv");
        actual.Should().Contain("vcodec hevc");
        actual.Should().Contain("audio non-AAC");
        actual.Should().NotContain("fps");
    }

    [Fact]
    public void Process_WhenInfoAndSyncAudioRequestedForCopyableMkv_ReturnsSyncAudioMarker()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mkv",
            Info: true,
            SyncAudio: true);

        var actual = sut.Process(request);

        actual.Should().Be("movie.mkv: [sync audio]");
    }

    [Fact]
    public void Process_WhenInfoAndDownscale576AndSourceBucketMissing_ReturnsHintMarker()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 900));
        profileRepository.Get576Config().Returns(CreateConfigWithoutBuckets());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Info: true,
            Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().StartWith("movie.mp4: [576 source bucket missing");
        actual.Should().Contain("add SourceBuckets match");
    }

    [Fact]
    public void Process_WhenInfoAndDownscale576AndSourceBucketMatrixIncomplete_ReturnsHintMarker()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Info: true,
            Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().StartWith("movie.mp4: [576 source bucket matrix incomplete");
        actual.Should().Contain("ContentQualityRanges or QualityRanges");
    }

    [Fact]
    public void Process_WhenInputIsNotMkv_UsesFinalMkvOutputAndDeletePostOperation()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\movie.mp4");

        var actual = sut.Process(request);

        actual.Should().Contain("\"C:\\video\\movie.mkv\"");
        actual.Should().Contain("&& del \"C:\\video\\movie.mp4\"");
        actual.Should().NotContain("_temp.mkv");
    }

    [Fact]
    public void Process_WhenInputIsMkvAndNeedsProcessing_UsesTempMkvAndRenamePostOperation()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "ac3", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\movie.mkv");

        var actual = sut.Process(request);

        actual.Should().Contain("\"C:\\video\\movie_temp.mkv\"");
        actual.Should().Contain("&& del \"C:\\video\\movie.mkv\" && ren \"C:\\video\\movie_temp.mkv\" \"movie.mkv\"");
    }

    [Fact]
    public void Process_WhenInputPathContainsSpaces_QuotesInputOutputAndPostOperationPaths()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "ac3", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video folder\\movie name.mkv");

        var actual = sut.Process(request);

        actual.Should().Contain("-i \"C:\\video folder\\movie name.mkv\"");
        actual.Should().Contain("\"C:\\video folder\\movie name_temp.mkv\"");
        actual.Should().Contain("&& del \"C:\\video folder\\movie name.mkv\" && ren \"C:\\video folder\\movie name_temp.mkv\" \"movie name.mkv\"");
    }

    [Fact]
    public void Process_WhenForceVideoEncodeAndStreamsAreCopyable_EncodesVideo()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            ForceVideoEncode: true);

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v h264_nvenc");
        actual.Should().NotContain("-map 0:v:0 -c:v copy");
    }

    [Fact]
    public void Process_WhenForceVideoEncodeAndSourceFpsAbove30_UsesLevel42()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(
            codec: "h264",
            audioCodec: "aac",
            height: 1080,
            rFrameRate: "60000/1001",
            avgFrameRate: "60000/1001"));
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            ForceVideoEncode: true);

        var actual = sut.Process(request);

        actual.Should().Contain("-level:v 4.2");
    }

    [Fact]
    public void Process_WhenSourceFpsIsHighAndVideoIsCopyable_DoesNotForceVideoEncode()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(
            codec: "h264",
            audioCodec: "aac",
            height: 1080,
            rFrameRate: "60000/1001",
            avgFrameRate: "60000/1001"));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Contain("-map 0:v:0 -c:v copy");
        actual.Should().NotContain("-c:v h264_nvenc");
    }

    [Fact]
    public void Process_WhenVideoEncodeAndCqProvided_UsesProvidedValue()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "hevc", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            Cq: 20);

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v h264_nvenc");
        actual.Should().Contain("-cq 20");
    }

    [Fact]
    public void Process_WhenVideoEncodeAndNvencPresetOverrideProvided_UsesOverrideValue()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "hevc", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            NvencPreset: "p4");

        var actual = sut.Process(request);

        actual.Should().Contain("-preset p4");
    }

    [Fact]
    public void Process_WhenAudioIsNonAacAndVideoIsCopyable_EncodesAudioAndKeepsVideoCopy()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "ac3", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mkv");

        var actual = sut.Process(request);

        actual.Should().Contain("-map 0:v:0 -c:v copy");
        actual.Should().Contain("-map 0:a? -c:a aac -ar 48000 -ac 2 -b:a 192k");
        actual.Should().Contain("aresample=async=1:first_pts=0");
    }

    [Fact]
    public void Process_WhenSyncAudioAndStreamsAreCopyable_EncodesAudioWithSoftSanitize()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            SyncAudio: true);

        var actual = sut.Process(request);

        actual.Should().Contain("-map 0:v:0 -c:v copy");
        actual.Should().Contain("-c:a aac");
        actual.Should().Contain("-fflags +genpts -avoid_negative_ts make_zero");
        actual.Should().NotContain("+igndts");
    }

    [Fact]
    public void Process_WhenDownscale576OverlayEnabledAndNoAutoSample_UsesOverlayCudaWithProfileAlgo()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigForProfileSelection());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            Downscale: 576,
            OverlayBg: true,
            NoAutoSample: true,
            ContentProfile: "film",
            QualityProfile: "default");

        var actual = sut.Process(request);

        actual.Should().Contain("-filter_complex");
        actual.Should().Contain("scale_cuda");
        actual.Should().Contain("interp_algo=bicubic");
    }

    [Fact]
    public void Process_WhenDownscale576AndAutoSampleModeFast_UsesFastResolvedSettingsInCommand()
    {
        var (sut, probeReader, profileRepository, autoSampleProvider) = CreateSutWithAutoSampleProvider();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        autoSampleProvider.EstimateFast(Arg.Any<AutoSampleReductionInput>())
            .Returns(30.0, 45.0);
        autoSampleProvider.EstimateAccurate(Arg.Any<AutoSampleReductionInput>())
            .Returns(45.0);
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            Downscale: 576,
            ContentProfile: "anime",
            QualityProfile: "default",
            AutoSampleMode: "fast");

        var actual = sut.Process(request);

        autoSampleProvider.Received(2).EstimateFast(Arg.Any<AutoSampleReductionInput>());
        autoSampleProvider.DidNotReceive().EstimateAccurate(Arg.Any<AutoSampleReductionInput>());
        actual.Should().Contain("-cq 24");
        actual.Should().Contain("-maxrate 2M");
        actual.Should().Contain("-bufsize 4M");
    }

    [Fact]
    public void Process_WhenVideoEncodeAndAudioIsAac_EncodesAudioWithHardSanitize()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "hevc", audioCodec: "aac", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mkv");

        var actual = sut.Process(request);

        actual.Should().Contain("-c:v h264_nvenc");
        actual.Should().Contain("-c:a aac");
        actual.Should().Contain("-fflags +genpts+igndts -avoid_negative_ts make_zero");
    }

    [Fact]
    public void Process_WhenNoAudioStreams_UsesAudioCopyMapOptional()
    {
        var (sut, probeReader, _) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbeWithoutAudio(codec: "h264", height: 1080));
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4");

        var actual = sut.Process(request);

        actual.Should().Contain("-map 0:v:0 -c:v copy");
        actual.Should().Contain("-map 0:a? -c:a copy");
    }

    [Fact]
    public void Process_WhenDownscale576AndSourceFpsAbove30_UsesLevel41()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(
            codec: "h264",
            audioCodec: "aac",
            height: 1080,
            rFrameRate: "60000/1001",
            avgFrameRate: "60000/1001"));
        profileRepository.Get576Config().Returns(CreateConfigForProfileSelection());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mkv",
            Downscale: 576,
            ContentProfile: "film",
            QualityProfile: "default");

        var actual = sut.Process(request);

        actual.Should().Contain("-level:v 4.1");
        actual.Should().NotContain("-level:v 4.2");
    }

    [Fact]
    public void Process_WhenDownscale576AndSourceBucketMissing_ReturnsHintRem()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 900));
        profileRepository.Get576Config().Returns(CreateConfigWithoutBuckets());
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4", Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().StartWith("REM 576 source bucket missing");
        actual.Should().Contain("add SourceBuckets match");
    }

    [Fact]
    public void Process_WhenDownscale576AndSourceBucketMatrixInvalid_ReturnsInvalidHintRem()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithInvalidBucketMatrix());
        var request = new TranscodeRequest(InputPath: "C:\\video\\a.mp4", Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().StartWith("REM 576 source bucket invalid:");
        actual.Should().Contain("missing ContentQualityRanges/QualityRanges");
    }

    [Fact]
    public void Process_WhenDownscale576WithProfileSettings_BuildsDownscaleCommand()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576,
            ContentProfile: "anime",
            QualityProfile: "default");

        var actual = sut.Process(request);

        actual.Should().StartWith("ffmpeg -hide_banner");
        actual.Should().Contain("scale_cuda=-2:576:interp_algo=bilinear:format=nv12");
        actual.Should().Contain("-cq 23");
        actual.Should().Contain("-maxrate 2.4M");
        actual.Should().Contain("-bufsize 4.8M");
    }

    [Fact]
    public void Process_WhenDownscale576AndContentAndQualityAreDefault_UsesFilmDefaultProfileValues()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigForProfileSelection());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().Contain("-cq 21");
        actual.Should().Contain("-maxrate 2.2M");
        actual.Should().Contain("-bufsize 4.4M");
    }

    [Fact]
    public void Process_WhenDownscale576AndOnlyContentSpecified_UsesDefaultQualityForSpecifiedContent()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigForProfileSelection());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576,
            ContentProfile: "anime");

        var actual = sut.Process(request);

        actual.Should().Contain("-cq 23");
        actual.Should().Contain("-maxrate 2.4M");
        actual.Should().Contain("-bufsize 4.8M");
    }

    [Fact]
    public void Process_WhenDownscale576AndOnlyQualitySpecified_UsesFilmProfileForSpecifiedQuality()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigForProfileSelection());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576,
            QualityProfile: "high");

        var actual = sut.Process(request);

        actual.Should().Contain("-cq 20");
        actual.Should().Contain("-maxrate 2.8M");
        actual.Should().Contain("-bufsize 5.6M");
    }

    [Fact]
    public void Process_WhenDownscale576AndSourceBucketMatrixIncomplete_ReturnsHintRem()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576);

        var actual = sut.Process(request);

        actual.Should().StartWith("REM 576 source bucket matrix incomplete");
        actual.Should().Contain("ContentQualityRanges or QualityRanges");
    }

    [Fact]
    public void Process_WhenDownscale576AndHeightIsNotAboveTarget_DoesNotApplyDownscale()
    {
        var (sut, probeReader, profileRepository) = CreateSut();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 576));
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576);

        var actual = sut.Process(request);

        profileRepository.DidNotReceive().Get576Config();
        actual.Should().Contain("-map 0:v:0 -c:v copy");
        actual.Should().NotContain("scale_cuda");
    }

    [Fact]
    public void Process_WhenDownscale576AndAutoSampleEnabled_UsesAutoSampleResult()
    {
        var (sut, probeReader, profileRepository, autoSampleProvider) = CreateSutWithAutoSampleProvider();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        autoSampleProvider.EstimateAccurate(Arg.Any<AutoSampleReductionInput>())
            .Returns(30.0, 45.0);
        autoSampleProvider.EstimateFast(Arg.Any<AutoSampleReductionInput>())
            .Returns(45.0);
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576,
            ContentProfile: "anime",
            QualityProfile: "default",
            AutoSampleMode: "accurate");

        var actual = sut.Process(request);

        autoSampleProvider.Received(2).EstimateAccurate(Arg.Any<AutoSampleReductionInput>());
        autoSampleProvider.DidNotReceive().EstimateFast(Arg.Any<AutoSampleReductionInput>());
        actual.Should().Contain("-cq 24");
        actual.Should().Contain("-maxrate 2M");
        actual.Should().Contain("-bufsize 4M");
    }

    [Theory]
    [InlineData(20, null, null, "-cq 20")]
    [InlineData(null, 2.6, null, "-maxrate 2.6M")]
    [InlineData(null, null, 7.2, "-bufsize 7.2M")]
    public void Process_WhenDownscale576AndManualOverrideProvided_SkipsAutoSample(
        int? cq,
        double? maxrate,
        double? bufsize,
        string expectedCommandPart)
    {
        var (sut, probeReader, profileRepository, autoSampleProvider) = CreateSutWithAutoSampleProvider();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576,
            ContentProfile: "anime",
            QualityProfile: "default",
            Cq: cq,
            Maxrate: maxrate,
            Bufsize: bufsize);

        var actual = sut.Process(request);

        autoSampleProvider.DidNotReceive().EstimateAccurate(Arg.Any<AutoSampleReductionInput>());
        autoSampleProvider.DidNotReceive().EstimateFast(Arg.Any<AutoSampleReductionInput>());
        actual.Should().Contain(expectedCommandPart);
    }

    [Fact]
    public void Process_WhenDownscale576AndProbeDurationMissing_SkipsAutoSampleAndUsesProfileSettings()
    {
        var (sut, probeReader, profileRepository, autoSampleProvider) = CreateSutWithAutoSampleProvider();
        probeReader.Read(Arg.Any<string>()).Returns(CreateProbe(codec: "h264", audioCodec: "aac", height: 1080, durationSeconds: null));
        profileRepository.Get576Config().Returns(CreateConfigWithBuckets());
        var request = new TranscodeRequest(
            InputPath: "C:\\video\\a.mp4",
            Downscale: 576,
            ContentProfile: "anime",
            QualityProfile: "default");

        var actual = sut.Process(request);

        autoSampleProvider.DidNotReceive().EstimateAccurate(Arg.Any<AutoSampleReductionInput>());
        autoSampleProvider.DidNotReceive().EstimateFast(Arg.Any<AutoSampleReductionInput>());
        actual.Should().Contain("-cq 23");
        actual.Should().Contain("-maxrate 2.4M");
        actual.Should().Contain("-bufsize 4.8M");
    }

    private static (TranscodeEngine Sut, IProbeReader ProbeReader, IProfileRepository ProfileRepository) CreateSut()
    {
        var probeReader = Substitute.For<IProbeReader>();
        var profileRepository = Substitute.For<IProfileRepository>();
        var sut = new TranscodeEngine(
            probeReader,
            profileRepository,
            new TranscodePolicy(),
            new FfmpegCommandBuilder());

        return (sut, probeReader, profileRepository);
    }

    private static (TranscodeEngine Sut, IProbeReader ProbeReader, IProfileRepository ProfileRepository, IAutoSampleReductionProvider AutoSampleProvider) CreateSutWithAutoSampleProvider()
    {
        var probeReader = Substitute.For<IProbeReader>();
        var profileRepository = Substitute.For<IProfileRepository>();
        var autoSampleProvider = Substitute.For<IAutoSampleReductionProvider>();
        var sut = new TranscodeEngine(
            probeReader,
            profileRepository,
            new TranscodePolicy(),
            new FfmpegCommandBuilder(),
            autoSampleProvider);

        return (sut, probeReader, profileRepository, autoSampleProvider);
    }

    private static ProbeResult CreateProbe(
        string codec,
        string audioCodec,
        int height,
        double? durationSeconds = 600,
        string? rFrameRate = null,
        string? avgFrameRate = null)
    {
        return new ProbeResult(
            Format: new ProbeFormat(DurationSeconds: durationSeconds, BitrateBps: 6_000_000),
            Streams: new[]
            {
                new ProbeStream("video", codec, Width: 1920, Height: height, RFrameRate: rFrameRate, AvgFrameRate: avgFrameRate),
                new ProbeStream("audio", audioCodec)
            });
    }

    private static ProbeResult CreateProbeWithoutAudio(
        string codec,
        int height,
        double? durationSeconds = 600,
        string? rFrameRate = null,
        string? avgFrameRate = null)
    {
        return new ProbeResult(
            Format: new ProbeFormat(DurationSeconds: durationSeconds, BitrateBps: 6_000_000),
            Streams: new[]
            {
                new ProbeStream("video", codec, Width: 1920, Height: height, RFrameRate: rFrameRate, AvgFrameRate: avgFrameRate)
            });
    }

    private static string CreateProbeJson()
    {
        return """
               {
                 "format": {
                   "duration": "600.0",
                   "bit_rate": "6000000"
                 },
                 "streams": [
                   {
                     "codec_type": "video",
                     "codec_name": "h264",
                     "width": 1920,
                     "height": 1080
                   },
                   {
                     "codec_type": "audio",
                     "codec_name": "aac"
                   }
                 ]
               }
               """;
    }

    private static TranscodePolicyConfig CreateConfigWithoutBuckets()
    {
        var animeDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var animeLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        var filmDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 21, Maxrate: 2.2, Bufsize: 4.4)
        };

        var filmLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        return new TranscodePolicyConfig(
            ContentProfiles: new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["anime"] = new ContentProfileSettings("bilinear", animeDefaults, animeLimits),
                ["film"] = new ContentProfileSettings("bilinear", filmDefaults, filmLimits)
            },
            RateModel: new RateModelSettings(0.4, 2.0),
            SourceBuckets: new List<SourceBucketSettings>());
    }

    private static TranscodePolicyConfig CreateConfigWithBuckets()
    {
        var config = CreateConfigWithoutBuckets();
        var bucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 45, MaxInclusive: 60)
            }
        };

        var buckets = new List<SourceBucketSettings>
        {
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: bucketRanges)
        };

        return config with { SourceBuckets = buckets };
    }

    private static TranscodePolicyConfig CreateConfigWithInvalidBucketMatrix()
    {
        var config = CreateConfigWithoutBuckets();

        var buckets = new List<SourceBucketSettings>
        {
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300))
        };

        return config with { SourceBuckets = buckets };
    }

    private static TranscodePolicyConfig CreateConfigForProfileSelection()
    {
        var animeDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8),
            ["high"] = new ProfileDefaults(Cq: 22, Maxrate: 3.0, Bufsize: 6.0)
        };

        var animeLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.4),
            ["high"] = new ProfileLimits(CqMin: 19, CqMax: 24, MaxrateMin: 2.4, MaxrateMax: 3.8)
        };

        var filmDefaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 21, Maxrate: 2.2, Bufsize: 4.4),
            ["high"] = new ProfileDefaults(Cq: 20, Maxrate: 2.8, Bufsize: 5.6)
        };

        var filmLimits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 19, CqMax: 25, MaxrateMin: 1.8, MaxrateMax: 3.0),
            ["high"] = new ProfileLimits(CqMin: 18, CqMax: 24, MaxrateMin: 2.4, MaxrateMax: 3.4)
        };

        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings("bilinear", animeDefaults, animeLimits),
            ["film"] = new ContentProfileSettings("bicubic", filmDefaults, filmLimits)
        };

        var bucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 45, MaxInclusive: 60),
                ["high"] = new ReductionRange(MinInclusive: 40, MaxInclusive: 55)
            },
            ["film"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 45, MaxInclusive: 60),
                ["high"] = new ReductionRange(MinInclusive: 40, MaxInclusive: 55)
            }
        };

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(0.4, 2.0),
            SourceBuckets: new List<SourceBucketSettings>
            {
                new(
                    Name: "fhd_1080",
                    Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                    ContentQualityRanges: bucketRanges)
            });
    }
}
