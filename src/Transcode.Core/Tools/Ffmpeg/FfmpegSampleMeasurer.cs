using System.Diagnostics;
using System.Globalization;
using Transcode.Core.VideoSettings;

namespace Transcode.Core.Tools.Ffmpeg;

/*
Этот helper измеряет bitrate reduction на sample-участках через ffmpeg.
Он нужен для autosample-режимов video-settings профилей.
*/
/// <summary>
/// Measures video-settings sample reduction by running temporary ffmpeg commands on source fragments.
/// </summary>
public sealed class FfmpegSampleMeasurer
{
    private readonly string _ffmpegPath;

    /// <summary>
    /// Initializes a sample measurer backed by the supplied <c>ffmpeg</c> executable path.
    /// </summary>
    /// <param name="ffmpegPath">Path to the <c>ffmpeg</c> executable used for sample measurement.</param>
    public FfmpegSampleMeasurer(string ffmpegPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        _ffmpegPath = ffmpegPath.Trim();
    }

    internal decimal? MeasureAverageReduction(
        string inputPath,
        int targetHeight,
        VideoSettingsDefaults settings,
        IReadOnlyList<VideoSettingsSampleWindow> windows)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || windows.Count == 0)
        {
            return null;
        }

        var reductions = new List<decimal>();
        foreach (var window in windows)
        {
            var sourceSample = CreateSourceSample(inputPath, window);
            if (sourceSample is null)
            {
                continue;
            }

            try
            {
                var encodedSize = EncodeSample(sourceSample.Path, targetHeight, settings);
                if (!encodedSize.HasValue || encodedSize.Value <= 0 || sourceSample.SizeBytes <= 0)
                {
                    continue;
                }

                var reduction = (1m - (encodedSize.Value / sourceSample.SizeBytes)) * 100m;
                reduction = Clamp(reduction, -100m, 100m);
                reductions.Add(Math.Round(reduction, 2, MidpointRounding.AwayFromZero));
            }
            finally
            {
                TryDelete(sourceSample.Path);
            }
        }

        if (reductions.Count == 0)
        {
            return null;
        }

        return Math.Round(reductions.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private SourceSample? CreateSourceSample(string inputPath, VideoSettingsSampleWindow window)
    {
        if (!File.Exists(inputPath) || window.DurationSeconds < 1)
        {
            return null;
        }

        var samplePath = Path.Combine(Path.GetTempPath(), $"transcode-srcsample-{Guid.NewGuid():N}.mkv");
        var arguments = new[]
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-ss", window.StartSeconds.ToString(CultureInfo.InvariantCulture),
            "-t", window.DurationSeconds.ToString(CultureInfo.InvariantCulture),
            "-i", inputPath,
            "-map", "0:v:0",
            "-map", "0:a?",
            "-c", "copy",
            "-sn",
            samplePath
        };

        if (!TryExecuteProcess(arguments) || !File.Exists(samplePath))
        {
            TryDelete(samplePath);
            return null;
        }

        var size = new FileInfo(samplePath).Length;
        if (size <= 0)
        {
            TryDelete(samplePath);
            return null;
        }

        return new SourceSample(samplePath, size);
    }

    private long? EncodeSample(string samplePath, int targetHeight, VideoSettingsDefaults settings)
    {
        if (!File.Exists(samplePath))
        {
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"transcode-outsample-{Guid.NewGuid():N}.mkv");
        var arguments = new[]
        {
            "-hide_banner",
            "-loglevel", "error",
            "-y",
            "-hwaccel", "cuda",
            "-hwaccel_output_format", "cuda",
            "-i", samplePath,
            "-map", "0:v:0",
            "-fps_mode:v", "cfr",
            "-vf", $"scale_cuda=-2:{targetHeight}:interp_algo={settings.Algorithm}:format=nv12",
            "-c:v", "h264_nvenc",
            "-preset", NvencPresetOptions.DefaultPreset,
            "-rc", "vbr_hq",
            "-cq", settings.Cq.ToString(CultureInfo.InvariantCulture),
            "-b:v", "0",
            "-maxrate", FormatRate(settings.Maxrate),
            "-bufsize", FormatRate(settings.Bufsize),
            "-spatial_aq", "1",
            "-temporal_aq", "1",
            "-rc-lookahead", "32",
            "-profile:v", "high",
            "-level:v", "4.1",
            "-g", "48",
            "-map", "0:a?",
            "-c:a", "aac",
            "-ar", "48000",
            "-ac", "2",
            "-b:a", "192k",
            "-af", "aresample=async=1:first_pts=0",
            "-sn",
            "-max_muxing_queue_size", "4096",
            outputPath
        };

        try
        {
            if (!TryExecuteProcess(arguments) || !File.Exists(outputPath))
            {
                return null;
            }

            var size = new FileInfo(outputPath).Length;
            return size > 0 ? size : null;
        }
        finally
        {
            TryDelete(outputPath);
        }
    }

    private bool TryExecuteProcess(IReadOnlyList<string> arguments)
    {
        using var process = new Process();
        process.StartInfo = CreateStartInfo(arguments);
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static string FormatRate(decimal value)
    {
        return $"{value.ToString("0.###", CultureInfo.InvariantCulture)}M";
    }

    /*
    Это локальная запись одного измеренного sample-файла.
    Она нужна measurer-у для расчета bitrate по временному артефакту без выноса детали наружу.
    */
    /// <summary>
    /// Represents one temporary sample artifact measured to estimate bitrate.
    /// </summary>
    private sealed record SourceSample(string Path, decimal SizeBytes);
}
