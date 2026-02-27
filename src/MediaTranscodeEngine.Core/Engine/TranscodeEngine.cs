using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Engine;

public sealed class TranscodeEngine
{
    private static readonly HashSet<string> CopyVideoCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264",
        "mpeg4"
    };

    private readonly IProbeReader _probeReader;
    private readonly IProfileRepository _profileRepository;
    private readonly TranscodePolicy _policy;
    private readonly FfmpegCommandBuilder _commandBuilder;
    private readonly IAutoSampleReductionProvider? _autoSampleReductionProvider;

    public TranscodeEngine(
        IProbeReader probeReader,
        IProfileRepository profileRepository,
        TranscodePolicy policy,
        FfmpegCommandBuilder commandBuilder,
        IAutoSampleReductionProvider? autoSampleReductionProvider = null)
    {
        _probeReader = probeReader;
        _profileRepository = profileRepository;
        _policy = policy;
        _commandBuilder = commandBuilder;
        _autoSampleReductionProvider = autoSampleReductionProvider;
    }

    public string Process(TranscodeRequest request)
    {
        return ProcessCore(request, probeOverride: null, useProbeOverride: false);
    }

    public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe)
    {
        return ProcessCore(request, probeOverride: probe, useProbeOverride: true);
    }

    public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson)
    {
        var parsedProbe = ProbeJsonParser.Parse(probeJson);
        return ProcessCore(request, parsedProbe, useProbeOverride: true);
    }

    private string ProcessCore(
        TranscodeRequest request,
        ProbeResult? probeOverride,
        bool useProbeOverride)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileName = request.InputPath;
        var displayName = Path.GetFileName(fileName);
        var directory = Path.GetDirectoryName(fileName);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var isMkv = ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        var outFinal = Path.Combine(directory, $"{baseName}.mkv");
        var outPath = isMkv ? Path.Combine(directory, $"{baseName}_temp.mkv") : outFinal;
        var postOp = isMkv
            ? $"&& del \"{fileName}\" && ren \"{outPath}\" \"{baseName}.mkv\""
            : $"&& del \"{fileName}\"";

        if (request.Downscale.HasValue && request.Downscale.Value == 720)
        {
            return request.Info
                ? $"{displayName}: [downscale 720 not implemented]"
                : $"REM Downscale 720 not implemented: {fileName}";
        }

        var probe = useProbeOverride
            ? probeOverride
            : _probeReader.Read(fileName);
        if (probe is null || probe.Streams.Count == 0)
        {
            return request.Info
                ? $"{displayName}: [ffprobe failed]"
                : $"REM ffprobe failed: {fileName}";
        }

        var video = probe.Streams.FirstOrDefault(static s => s.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase));
        if (video is null)
        {
            return request.Info
                ? $"{displayName}: [no video stream]"
                : $"REM Нет видеопотока: {fileName}";
        }

        var audioStreams = probe.Streams.Where(static s => s.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase)).ToArray();

        var applyDownscale = false;
        var downscaleTarget = request.Downscale ?? 0;
        if (request.Downscale.HasValue &&
            video.Height.HasValue &&
            video.Height.Value > 0 &&
            video.Height.Value > downscaleTarget)
        {
            applyDownscale = true;
        }

        TranscodePolicyResult? profileSettings = null;
        var downscaleAlgo = request.DownscaleAlgoOverride ?? "bicubic";

        if (applyDownscale && downscaleTarget == 576)
        {
            var config = _profileRepository.Get576Config();
            profileSettings = _policy.Resolve576Settings(
                config,
                new TranscodePolicyInput(
                    ContentProfile: request.ContentProfile,
                    QualityProfile: request.QualityProfile,
                    Cq: request.CqOverride,
                    Maxrate: request.MaxrateOverride,
                    Bufsize: request.BufsizeOverride,
                    DownscaleAlgo: request.DownscaleAlgoOverride));

            var bucket = _policy.ResolveSourceBucket(config, video.Height);
            if (bucket is null)
            {
                var heightToken = video.Height?.ToString() ?? "unknown";
                var hint = $"576 source bucket missing (height={heightToken}); add SourceBuckets match in ToMkvGPU.576.Profiles.psd1";
                return request.Info
                    ? $"{displayName}: [{hint}]"
                    : $"REM {hint}";
            }

            var bucketValidationError = _policy.GetSourceBucketMatrixValidationError(
                config,
                bucket,
                request.ContentProfile,
                request.QualityProfile);
            if (!string.IsNullOrWhiteSpace(bucketValidationError))
            {
                if (bucketValidationError.StartsWith("missing corridor", StringComparison.OrdinalIgnoreCase))
                {
                    var heightToken = video.Height?.ToString() ?? "unknown";
                    var matrixHint = $"576 source bucket matrix incomplete (height={heightToken}); add ContentQualityRanges or QualityRanges for {request.ContentProfile}/{request.QualityProfile}";
                    return request.Info
                        ? $"{displayName}: [{matrixHint}]"
                        : $"REM {matrixHint}";
                }

                var hint = $"576 source bucket invalid: {bucketValidationError}; fix SourceBuckets in ToMkvGPU.576.Profiles.psd1";
                return request.Info
                    ? $"{displayName}: [{hint}]"
                    : $"REM {hint}";
            }

            var qualityRange = _policy.ResolveQualityRange(
                config,
                request.ContentProfile,
                request.QualityProfile,
                video.Height);
            if (qualityRange is null)
            {
                var heightToken = video.Height?.ToString() ?? "unknown";
                var hint = $"576 source bucket matrix incomplete (height={heightToken}); add ContentQualityRanges or QualityRanges for {request.ContentProfile}/{request.QualityProfile}";
                return request.Info
                    ? $"{displayName}: [{hint}]"
                    : $"REM {hint}";
            }

            var shouldAutoSample =
                _autoSampleReductionProvider is not null &&
                !request.NoAutoSample &&
                !request.CqOverride.HasValue &&
                !request.MaxrateOverride.HasValue &&
                !request.BufsizeOverride.HasValue &&
                probe.Format?.DurationSeconds is > 0;

            if (shouldAutoSample)
            {
                var mode = string.IsNullOrWhiteSpace(request.AutoSampleMode)
                    ? config.AutoSampling?.ModeDefault ?? "accurate"
                    : request.AutoSampleMode;

                profileSettings = _policy.ResolveAutoSampleSettings(
                    config,
                    request.ContentProfile,
                    request.QualityProfile,
                    profileSettings,
                    video.Height,
                    mode,
                    accurateReductionProvider: (cq, maxrate, bufsize) =>
                        _autoSampleReductionProvider!.EstimateAccurate(
                            new AutoSampleReductionInput(fileName, cq, maxrate, bufsize)),
                    fastReductionProvider: (cq, maxrate, bufsize) =>
                        _autoSampleReductionProvider!.EstimateFast(
                            new AutoSampleReductionInput(fileName, cq, maxrate, bufsize)));
            }

            downscaleAlgo = profileSettings.DownscaleAlgo;
        }

        var codecLower = video.CodecName.ToLowerInvariant();
        var needVideoEncode = !CopyVideoCodecs.Contains(codecLower) || request.OverlayBg || applyDownscale || request.ForceVideoEncode;
        var needAudio = audioStreams.Any(static s => !s.CodecName.Equals("aac", StringComparison.OrdinalIgnoreCase));
        var forceSyncAudio = request.SyncAudio;
        var needAudioEncode = audioStreams.Length > 0 && (needAudio || needVideoEncode || forceSyncAudio);
        var needContainer = !isMkv;
        var onlyRemuxMkv = isMkv && !needVideoEncode && !needAudioEncode;

        if (request.Info)
        {
            var parts = new List<string>();
            if (needContainer)
            {
                parts.Add($"container {ext}→mkv");
            }

            if (!CopyVideoCodecs.Contains(codecLower))
            {
                parts.Add($"vcodec {video.CodecName}");
            }

            if (request.ForceVideoEncode)
            {
                parts.Add("force video encode");
            }

            if (needAudio)
            {
                parts.Add("audio non-AAC");
            }

            if (forceSyncAudio && audioStreams.Length > 0)
            {
                parts.Add("sync audio");
            }

            return parts.Count == 0 ? string.Empty : $"{displayName}: [{string.Join("] [", parts)}]";
        }

        if (onlyRemuxMkv)
        {
            return string.Empty;
        }

        var defaultCq = profileSettings is not null ? profileSettings.Cq : applyDownscale ? 19 : 21;
        var commandInput = new FfmpegCommandInput(
            InputPath: fileName,
            OutputPath: outPath,
            PostOperation: postOp,
            NeedVideoEncode: needVideoEncode,
            NeedAudioEncode: needAudioEncode,
            NeedContainer: needContainer,
            ForceSyncAudio: forceSyncAudio,
            ApplyDownscale: applyDownscale,
            DownscaleTarget: downscaleTarget,
            OverlayBg: request.OverlayBg,
            SourceWidth: video.Width,
            SourceHeight: video.Height,
            Cq: request.CqOverride ?? defaultCq,
            Maxrate: profileSettings?.Maxrate ?? 3.0,
            Bufsize: profileSettings?.Bufsize ?? 6.0,
            DownscaleAlgo: downscaleAlgo,
            NvencPreset: request.NvencPreset);

        return _commandBuilder.Build(commandInput);
    }
}
