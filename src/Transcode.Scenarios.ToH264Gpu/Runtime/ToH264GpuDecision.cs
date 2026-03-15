using Transcode.Runtime.MediaIntent;

namespace Transcode.Scenarios.ToH264Gpu.Runtime;

/*
Это локальная rich model сценария toh264gpu.
Она держит уже принятые scenario-local решения: container, video/audio path,
output layout и concrete ffmpeg execution details.
*/
/// <summary>
/// Carries the resolved toh264gpu decision together with concrete ffmpeg execution details.
/// </summary>
internal sealed class ToH264GpuDecision
{
    /// <summary>
    /// Initializes a fully resolved toh264gpu decision.
    /// </summary>
    public ToH264GpuDecision(
        string targetContainer,
        VideoIntent videoIntent,
        AudioIntent audioIntent,
        bool keepSource,
        string outputPath,
        MuxExecution mux,
        VideoExecution? videoExecution = null,
        AudioExecution? audioExecution = null)
    {
        TargetContainer = NormalizeRequiredToken(targetContainer, nameof(targetContainer));
        Video = NormalizeVideoPlan(videoIntent);
        Audio = NormalizeAudioPlan(audioIntent);
        KeepSource = keepSource;
        OutputPath = NormalizeOutputPath(outputPath, nameof(outputPath));
        Mux = mux ?? throw new ArgumentNullException(nameof(mux));
        VideoExecutionDetails = videoExecution;
        AudioExecutionDetails = audioExecution;
    }

    /// <summary>
    /// Gets the normalized target container identifier.
    /// </summary>
    public string TargetContainer { get; }

    /// <summary>
    /// Gets the resolved video path.
    /// </summary>
    public VideoIntent Video { get; }

    /// <summary>
    /// Gets the resolved audio path.
    /// </summary>
    public AudioIntent Audio { get; }

    /// <summary>
    /// Gets a value indicating whether the source file should be kept.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Gets the final output path chosen by the scenario.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// Gets mux-related execution details.
    /// </summary>
    public MuxExecution Mux { get; }

    /// <summary>
    /// Gets normalized video execution details when video encoding is required.
    /// </summary>
    public VideoExecution? VideoExecutionDetails { get; }

    /// <summary>
    /// Gets normalized audio execution details when audio encoding is required.
    /// </summary>
    public AudioExecution? AudioExecutionDetails { get; }

    /// <summary>
    /// Gets a value indicating whether the video stream should be copied.
    /// </summary>
    public bool CopyVideo => Video is CopyVideoIntent;

    /// <summary>
    /// Gets a value indicating whether the audio path copies compatible source streams.
    /// </summary>
    public bool CopyAudio => Audio is CopyAudioIntent;

    /// <summary>
    /// Gets a value indicating whether the decision uses the sync-safe audio path.
    /// </summary>
    public bool SynchronizeAudio => Audio is SynchronizeAudioIntent;

    /// <summary>
    /// Gets a value indicating whether timestamp normalization is required.
    /// </summary>
    public bool FixTimestamps => Audio is RepairAudioIntent;

    /// <summary>
    /// Gets a value indicating whether the decision requires video encoding.
    /// </summary>
    public bool RequiresVideoEncode => !CopyVideo;

    /// <summary>
    /// Gets a value indicating whether the decision requires audio encoding.
    /// </summary>
    public bool RequiresAudioEncode => !CopyAudio;

    /// <summary>
    /// Gets a value indicating whether the output container should be optimized for progressive playback.
    /// </summary>
    public bool OptimizeForFastStart => Mux.OptimizeForFastStart;

    /// <summary>
    /// Gets a value indicating whether only the primary audio stream should be mapped.
    /// </summary>
    public bool MapPrimaryAudioOnly => Mux.MapPrimaryAudioOnly;

    /// <summary>
    /// Gets the hardware-decode preference when video encoding is required.
    /// </summary>
    public bool? UseHardwareDecode => VideoExecutionDetails?.UseHardwareDecode;

    /// <summary>
    /// Gets a value indicating whether adaptive quantization is enabled.
    /// </summary>
    public bool? EnableAdaptiveQuantization => VideoExecutionDetails is null ? null : VideoExecutionDetails.AdaptiveQuantization is not null;

    /// <summary>
    /// Gets the AQ strength override.
    /// </summary>
    public int? AqStrength => VideoExecutionDetails?.AdaptiveQuantization?.Strength;

    /// <summary>
    /// Gets the lookahead window override.
    /// </summary>
    public int? RcLookahead => VideoExecutionDetails?.AdaptiveQuantization?.RcLookahead;

    /// <summary>
    /// Gets the target video bitrate in kilobits per second when VBR mode is requested.
    /// </summary>
    public int? VideoBitrateKbps => (VideoExecutionDetails?.RateControl as VariableBitrateVideoRateControlExecution)?.BitrateKbps;

    /// <summary>
    /// Gets the target video maxrate in kilobits per second.
    /// </summary>
    public int? VideoMaxrateKbps => VideoExecutionDetails?.RateControl switch
    {
        VariableBitrateVideoRateControlExecution rateControl => rateControl.MaxrateKbps,
        ConstantQualityVideoRateControlExecution rateControl => rateControl.MaxrateKbps,
        _ => null
    };

    /// <summary>
    /// Gets the target video buffer size in kilobits per second.
    /// </summary>
    public int? VideoBufferSizeKbps => VideoExecutionDetails?.RateControl switch
    {
        VariableBitrateVideoRateControlExecution rateControl => rateControl.BufferSizeKbps,
        ConstantQualityVideoRateControlExecution rateControl => rateControl.BufferSizeKbps,
        _ => null
    };

    /// <summary>
    /// Gets the explicit CQ value when CQ-driven mode is requested.
    /// </summary>
    public int? VideoCq => (VideoExecutionDetails?.RateControl as ConstantQualityVideoRateControlExecution)?.Cq;

    /// <summary>
    /// Gets the plain ffmpeg video filter expression when one is required.
    /// </summary>
    public string? VideoFilter => VideoExecutionDetails?.Filter;

    /// <summary>
    /// Gets the explicit pixel format token when one is required.
    /// </summary>
    public string? PixelFormat => VideoExecutionDetails?.PixelFormat;

    /// <summary>
    /// Gets the target audio bitrate in kilobits per second when audio must be encoded.
    /// </summary>
    public int? AudioBitrateKbps => AudioExecutionDetails?.BitrateKbps;

    /// <summary>
    /// Gets the explicit audio sample rate when one is required.
    /// </summary>
    public int? AudioSampleRate => AudioExecutionDetails?.SampleRate;

    /// <summary>
    /// Gets the explicit audio channel count when one is required.
    /// </summary>
    public int? AudioChannels => AudioExecutionDetails?.Channels;

    /// <summary>
    /// Gets the plain ffmpeg audio filter expression when one is required.
    /// </summary>
    public string? AudioFilter => AudioExecutionDetails?.Filter;

    /// <summary>
    /// Represents normalized mux details.
    /// </summary>
    public sealed class MuxExecution
    {
        /// <summary>
        /// Initializes mux-related execution details.
        /// </summary>
        public MuxExecution(
            bool optimizeForFastStart = false,
            bool mapPrimaryAudioOnly = false)
        {
            OptimizeForFastStart = optimizeForFastStart;
            MapPrimaryAudioOnly = mapPrimaryAudioOnly;
        }

        /// <summary>
        /// Gets a value indicating whether the output container should be optimized for progressive playback.
        /// </summary>
        public bool OptimizeForFastStart { get; }

        /// <summary>
        /// Gets a value indicating whether only the primary audio stream should be mapped.
        /// </summary>
        public bool MapPrimaryAudioOnly { get; }
    }

    /// <summary>
    /// Represents normalized video execution details.
    /// </summary>
    public sealed class VideoExecution
    {
        /// <summary>
        /// Initializes video execution details.
        /// </summary>
        public VideoExecution(
            bool useHardwareDecode,
            VideoRateControlExecution rateControl,
            AdaptiveQuantizationExecution? adaptiveQuantization = null,
            string? filter = null,
            string? pixelFormat = null)
        {
            RateControl = rateControl ?? throw new ArgumentNullException(nameof(rateControl));
            UseHardwareDecode = useHardwareDecode;
            AdaptiveQuantization = adaptiveQuantization;
            Filter = NormalizeOptionalText(filter);
            PixelFormat = NormalizeOptionalText(pixelFormat);
        }

        /// <summary>
        /// Gets a value indicating whether hardware decode should be enabled.
        /// </summary>
        public bool UseHardwareDecode { get; }

        /// <summary>
        /// Gets normalized rate-control details.
        /// </summary>
        public VideoRateControlExecution RateControl { get; }

        /// <summary>
        /// Gets normalized adaptive-quantization details when AQ is enabled.
        /// </summary>
        public AdaptiveQuantizationExecution? AdaptiveQuantization { get; }

        /// <summary>
        /// Gets the plain ffmpeg video filter expression when one is required.
        /// </summary>
        public string? Filter { get; }

        /// <summary>
        /// Gets the explicit pixel format token when one is required.
        /// </summary>
        public string? PixelFormat { get; }
    }

    /// <summary>
    /// Represents normalized video rate-control details.
    /// </summary>
    public abstract class VideoRateControlExecution
    {
    }

    /// <summary>
    /// Represents normalized VBR details.
    /// </summary>
    public sealed class VariableBitrateVideoRateControlExecution : VideoRateControlExecution
    {
        /// <summary>
        /// Initializes VBR details.
        /// </summary>
        public VariableBitrateVideoRateControlExecution(
            int bitrateKbps,
            int maxrateKbps,
            int bufferSizeKbps)
        {
            BitrateKbps = NormalizePositiveInt(bitrateKbps, nameof(bitrateKbps));
            MaxrateKbps = NormalizePositiveInt(maxrateKbps, nameof(maxrateKbps));
            BufferSizeKbps = NormalizePositiveInt(bufferSizeKbps, nameof(bufferSizeKbps));
        }

        /// <summary>
        /// Gets the target bitrate in kilobits per second.
        /// </summary>
        public int BitrateKbps { get; }

        /// <summary>
        /// Gets the target maxrate in kilobits per second.
        /// </summary>
        public int MaxrateKbps { get; }

        /// <summary>
        /// Gets the target buffer size in kilobits per second.
        /// </summary>
        public int BufferSizeKbps { get; }
    }

    /// <summary>
    /// Represents normalized CQ details.
    /// </summary>
    public sealed class ConstantQualityVideoRateControlExecution : VideoRateControlExecution
    {
        /// <summary>
        /// Initializes CQ details.
        /// </summary>
        public ConstantQualityVideoRateControlExecution(
            int cq,
            int? maxrateKbps = null,
            int? bufferSizeKbps = null)
        {
            if (maxrateKbps.HasValue != bufferSizeKbps.HasValue)
            {
                throw new ArgumentException("CQ maxrate and buffer size must either both be specified or both be omitted.");
            }

            Cq = NormalizePositiveInt(cq, nameof(cq));
            MaxrateKbps = NormalizeOptionalPositiveInt(maxrateKbps, nameof(maxrateKbps));
            BufferSizeKbps = NormalizeOptionalPositiveInt(bufferSizeKbps, nameof(bufferSizeKbps));
        }

        /// <summary>
        /// Gets the CQ value.
        /// </summary>
        public int Cq { get; }

        /// <summary>
        /// Gets the target maxrate in kilobits per second when bounded CQ mode is used.
        /// </summary>
        public int? MaxrateKbps { get; }

        /// <summary>
        /// Gets the target buffer size in kilobits per second when bounded CQ mode is used.
        /// </summary>
        public int? BufferSizeKbps { get; }
    }

    /// <summary>
    /// Represents normalized adaptive-quantization details.
    /// </summary>
    public sealed class AdaptiveQuantizationExecution
    {
        /// <summary>
        /// Initializes adaptive-quantization details.
        /// </summary>
        public AdaptiveQuantizationExecution(
            int rcLookahead,
            int? strength = null)
        {
            RcLookahead = NormalizePositiveInt(rcLookahead, nameof(rcLookahead));
            Strength = NormalizeOptionalPositiveInt(strength, nameof(strength));
        }

        /// <summary>
        /// Gets the lookahead window size.
        /// </summary>
        public int RcLookahead { get; }

        /// <summary>
        /// Gets the explicit AQ strength when one is required.
        /// </summary>
        public int? Strength { get; }
    }

    /// <summary>
    /// Represents normalized audio execution details.
    /// </summary>
    public sealed class AudioExecution
    {
        /// <summary>
        /// Initializes audio execution details.
        /// </summary>
        public AudioExecution(
            int bitrateKbps,
            int? sampleRate = null,
            int? channels = null,
            string? filter = null)
        {
            BitrateKbps = NormalizePositiveInt(bitrateKbps, nameof(bitrateKbps));
            SampleRate = NormalizeOptionalPositiveInt(sampleRate, nameof(sampleRate));
            Channels = NormalizeOptionalPositiveInt(channels, nameof(channels));
            Filter = NormalizeOptionalText(filter);
        }

        /// <summary>
        /// Gets the target bitrate in kilobits per second.
        /// </summary>
        public int BitrateKbps { get; }

        /// <summary>
        /// Gets the explicit sample rate when one is required.
        /// </summary>
        public int? SampleRate { get; }

        /// <summary>
        /// Gets the explicit channel count when one is required.
        /// </summary>
        public int? Channels { get; }

        /// <summary>
        /// Gets the plain ffmpeg audio filter expression when one is required.
        /// </summary>
        public string? Filter { get; }
    }

    private static int NormalizePositiveInt(int value, string paramName)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
    }

    private static int? NormalizeOptionalPositiveInt(int? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return NormalizePositiveInt(value.Value, paramName);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeOutputPath(string? outputPath, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath, paramName);
        return Path.GetFullPath(outputPath.Trim());
    }

    private static VideoIntent NormalizeVideoPlan(VideoIntent video)
    {
        ArgumentNullException.ThrowIfNull(video);
        return video switch
        {
            CopyVideoIntent => video,
            EncodeVideoIntent => video,
            _ => throw new ArgumentException($"Unsupported video plan type '{video.GetType().Name}'.", nameof(video))
        };
    }

    private static AudioIntent NormalizeAudioPlan(AudioIntent audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return audio switch
        {
            CopyAudioIntent => audio,
            SynchronizeAudioIntent => audio,
            RepairAudioIntent => audio,
            EncodeAudioIntent => audio,
            _ => throw new ArgumentException($"Unsupported audio plan type '{audio.GetType().Name}'.", nameof(audio))
        };
    }
}
