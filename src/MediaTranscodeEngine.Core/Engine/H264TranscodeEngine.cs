using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Engine;

public sealed class H264TranscodeEngine
{
    private readonly IProbeReader _probeReader;
    private readonly H264RemuxEligibilityPolicy _remuxEligibilityPolicy;
    private readonly H264TimestampPolicy _timestampPolicy;
    private readonly H264AudioPolicy _audioPolicy;
    private readonly H264RateControlPolicy _rateControlPolicy;
    private readonly ContainerPolicySelector _containerPolicySelector;
    private readonly H264CommandBuilder _commandBuilder;

    public H264TranscodeEngine(
        IProbeReader probeReader,
        H264RemuxEligibilityPolicy remuxEligibilityPolicy,
        H264TimestampPolicy timestampPolicy,
        H264AudioPolicy audioPolicy,
        H264RateControlPolicy rateControlPolicy,
        ContainerPolicySelector containerPolicySelector,
        H264CommandBuilder commandBuilder)
    {
        _probeReader = probeReader;
        _remuxEligibilityPolicy = remuxEligibilityPolicy;
        _timestampPolicy = timestampPolicy;
        _audioPolicy = audioPolicy;
        _rateControlPolicy = rateControlPolicy;
        _containerPolicySelector = containerPolicySelector;
        _commandBuilder = commandBuilder;
    }

    public string Process(TranscodeRequest request)
    {
        return ProcessCore(request, probeOverride: null, useProbeOverride: false);
    }

    public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe)
    {
        return ProcessCore(request, probe, useProbeOverride: true);
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

        var inputPath = request.InputPath;
        var probe = useProbeOverride
            ? probeOverride
            : _probeReader.Read(inputPath);

        if (probe is null || probe.Streams.Count == 0)
        {
            return $"REM ffprobe failed: {inputPath}";
        }

        var video = probe.Streams.FirstOrDefault(static stream =>
            stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase));
        if (video is null)
        {
            return $"REM Нет видеопотока: {inputPath}";
        }

        var audio = probe.Streams.FirstOrDefault(static stream =>
            stream.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase));

        var useDownscale = request.Downscale.HasValue &&
                           video.Height.HasValue &&
                           video.Height.Value > request.Downscale.Value;

        var fixTimestamps = _timestampPolicy.ShouldFixTimestamps(new H264TimestampInput(
            InputPath: inputPath,
            FormatName: probe.Format?.FormatName,
            ForceFixTimestamps: request.FixTimestamps));

        var remuxEligibilityInput = new H264RemuxEligibilityInput(
            InputExtension: Path.GetExtension(inputPath),
            FormatName: probe.Format?.FormatName,
            VideoCodec: video.CodecName,
            AudioCodec: audio?.CodecName,
            RFrameRate: video.RFrameRate,
            AvgFrameRate: video.AvgFrameRate,
            Denoise: request.Denoise,
            FixTimestamps: fixTimestamps,
            UseDownscale: useDownscale);

        var canRemux = _remuxEligibilityPolicy.CanRemux(remuxEligibilityInput);
        var containerPolicy = _containerPolicySelector.Select(request.TargetContainer);
        var outputPaths = containerPolicy.ResolveOutputPaths(
            inputPath: inputPath,
            keepSource: request.KeepSource,
            useDownscale: useDownscale,
            downscaleTarget: request.Downscale,
            willEncode: !canRemux);
        if (canRemux)
        {
            return _commandBuilder.BuildRemux(new H264RemuxCommandInput(
                InputPath: inputPath,
                OutputPath: outputPaths.OutputPath,
                TempOutputPath: outputPaths.TempOutputPath,
                ContainerPolicy: containerPolicy,
                ReplaceInput: !request.KeepSource));
        }

        var rateControl = _rateControlPolicy.Resolve(new H264RateControlInput(
            Video: video,
            UseDownscale: useDownscale,
            KeepFps: request.KeepFps,
            CqOverride: request.Cq));
        var copyAudio = _audioPolicy.CanCopyAudio(new H264AudioInput(
            AudioCodec: audio?.CodecName,
            FixTimestamps: fixTimestamps));

        return _commandBuilder.BuildEncode(new H264EncodeCommandInput(
            InputPath: inputPath,
            OutputPath: outputPaths.OutputPath,
            TempOutputPath: outputPaths.TempOutputPath,
            NvencPreset: request.VideoPreset,
            Cq: rateControl.Cq,
            FpsToken: rateControl.FpsToken,
            Gop: rateControl.Gop,
            ContainerPolicy: containerPolicy,
            ApplyDownscale: useDownscale,
            DownscaleTarget: request.Downscale ?? 0,
            DownscaleAlgo: request.DownscaleAlgo,
            UseAq: request.UseAq,
            AqStrength: request.AqStrength,
            Denoise: request.Denoise,
            FixTimestamps: fixTimestamps,
            CopyAudio: copyAudio,
            ReplaceInput: !request.KeepSource));
    }
}
