namespace MediaTranscodeEngine.Core.Policy;

public sealed class TranscodePolicy
{
    public TranscodePolicyResult Resolve576Settings(
        TranscodePolicyConfig config,
        TranscodePolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(input);

        if (!config.ContentProfiles.TryGetValue(input.ContentProfile, out var contentProfile))
        {
            throw new ArgumentException($"Unsupported ContentProfile: {input.ContentProfile}", nameof(input));
        }

        if (!contentProfile.Defaults.TryGetValue(input.QualityProfile, out var defaults))
        {
            throw new ArgumentException(
                $"Unsupported QualityProfile for '{input.ContentProfile}': {input.QualityProfile}",
                nameof(input));
        }

        if (!contentProfile.Limits.TryGetValue(input.QualityProfile, out var limits))
        {
            throw new ArgumentException(
                $"Missing limits for '{input.ContentProfile}/{input.QualityProfile}'",
                nameof(input));
        }

        var resolvedCq = input.Cq ?? defaults.Cq;
        var resolvedMaxrate = ResolveMaxrate(defaults, limits, config.RateModel, resolvedCq, input.Cq.HasValue, input.Maxrate);
        var resolvedBufsize = ResolveBufsize(defaults, config.RateModel, input, resolvedMaxrate);
        var resolvedAlgo = input.DownscaleAlgo ?? contentProfile.AlgoDefault;

        return new TranscodePolicyResult(
            Cq: resolvedCq,
            Maxrate: resolvedMaxrate,
            Bufsize: resolvedBufsize,
            DownscaleAlgo: resolvedAlgo);
    }

    private static double ResolveMaxrate(
        ProfileDefaults defaults,
        ProfileLimits limits,
        RateModelSettings rateModel,
        int resolvedCq,
        bool hasCq,
        double? maxrateOverride)
    {
        if (maxrateOverride.HasValue)
        {
            return maxrateOverride.Value;
        }

        if (!hasCq)
        {
            return defaults.Maxrate;
        }

        var delta = defaults.Cq - resolvedCq;
        var modeled = defaults.Maxrate + delta * rateModel.CqStepToMaxrateStep;
        return Clamp(modeled, limits.MaxrateMin, limits.MaxrateMax);
    }

    private static double ResolveBufsize(
        ProfileDefaults defaults,
        RateModelSettings rateModel,
        TranscodePolicyInput input,
        double resolvedMaxrate)
    {
        if (input.Bufsize.HasValue)
        {
            return input.Bufsize.Value;
        }

        if (input.Maxrate.HasValue || input.Cq.HasValue)
        {
            return resolvedMaxrate * rateModel.BufsizeMultiplier;
        }

        return defaults.Bufsize;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    public SourceBucketSettings? ResolveSourceBucket(
        TranscodePolicyConfig config,
        double? sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(config);

        var buckets = config.SourceBuckets;
        if (buckets is null || buckets.Count == 0)
        {
            return null;
        }

        SourceBucketSettings? defaultBucket = null;
        foreach (var bucket in buckets)
        {
            if (bucket.IsDefault && defaultBucket is null)
            {
                defaultBucket = bucket;
            }

            if (bucket.Match is null)
            {
                continue;
            }

            if (IsSourceHeightMatched(bucket.Match, sourceHeight))
            {
                return bucket;
            }
        }

        return defaultBucket;
    }

    public ReductionRange? ResolveQualityRange(
        TranscodePolicyConfig config,
        string contentProfile,
        string qualityProfile,
        double? sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualityProfile);

        var bucket = ResolveSourceBucket(config, sourceHeight);

        if (bucket?.ContentQualityRanges is not null &&
            bucket.ContentQualityRanges.TryGetValue(contentProfile, out var bucketContentRanges) &&
            bucketContentRanges.TryGetValue(qualityProfile, out var bucketContentRange))
        {
            return bucketContentRange;
        }

        if (bucket?.QualityRanges is not null &&
            bucket.QualityRanges.TryGetValue(qualityProfile, out var bucketQualityRange))
        {
            return bucketQualityRange;
        }

        if (config.ContentQualityRanges is not null &&
            config.ContentQualityRanges.TryGetValue(contentProfile, out var contentRanges) &&
            contentRanges.TryGetValue(qualityProfile, out var contentRange))
        {
            return contentRange;
        }

        if (config.QualityRanges is not null &&
            config.QualityRanges.TryGetValue(qualityProfile, out var qualityRange))
        {
            return qualityRange;
        }

        return null;
    }

    private static bool IsSourceHeightMatched(SourceBucketMatch match, double? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            return false;
        }

        var height = sourceHeight.Value;

        if (match.MinHeightInclusive.HasValue && height < match.MinHeightInclusive.Value)
        {
            return false;
        }

        if (match.MinHeightExclusive.HasValue && height <= match.MinHeightExclusive.Value)
        {
            return false;
        }

        if (match.MaxHeightInclusive.HasValue && height > match.MaxHeightInclusive.Value)
        {
            return false;
        }

        if (match.MaxHeightExclusive.HasValue && height >= match.MaxHeightExclusive.Value)
        {
            return false;
        }

        return true;
    }

    public TranscodePolicyResult ResolveAutoSampleSettings(
        TranscodePolicyConfig config,
        string contentProfile,
        string qualityProfile,
        TranscodePolicyResult baseSettings,
        double? sourceHeight,
        string autoSampleMode,
        Func<int, double, double, double?> accurateReductionProvider,
        Func<int, double, double, double?> fastReductionProvider)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualityProfile);
        ArgumentNullException.ThrowIfNull(baseSettings);
        ArgumentException.ThrowIfNullOrWhiteSpace(autoSampleMode);
        ArgumentNullException.ThrowIfNull(accurateReductionProvider);
        ArgumentNullException.ThrowIfNull(fastReductionProvider);

        var qualityRange = ResolveQualityRange(config, contentProfile, qualityProfile, sourceHeight);
        if (qualityRange is null)
        {
            return baseSettings;
        }

        if (!config.ContentProfiles.TryGetValue(contentProfile, out var content) ||
            !content.Limits.TryGetValue(qualityProfile, out var limits))
        {
            return baseSettings;
        }

        var autoSampling = config.AutoSampling ?? new AutoSamplingSettings();
        var maxIterations = Math.Max(autoSampling.MaxIterations, 1);
        var hybridIterations = Math.Max(autoSampling.HybridAccurateIterations, 1);
        var bounds = BuildBounds(qualityRange);

        var mode = autoSampleMode.ToLowerInvariant();
        if (mode == "fast")
        {
            return RunLoop(
                maxIterations,
                config.RateModel,
                limits,
                baseSettings,
                bounds,
                fastReductionProvider).Result;
        }

        if (mode == "hybrid")
        {
            var fast = RunLoop(
                maxIterations,
                config.RateModel,
                limits,
                baseSettings,
                bounds,
                fastReductionProvider,
                captureInBounds: true);

            if (fast.InBounds)
            {
                return fast.Result;
            }

            var accurateStart = fast.Result;
            var accurate = RunLoop(
                Math.Min(maxIterations, hybridIterations),
                config.RateModel,
                limits,
                accurateStart,
                bounds,
                accurateReductionProvider);

            return accurate.Result;
        }

        var accurateOnly = RunLoop(
            maxIterations,
            config.RateModel,
            limits,
            baseSettings,
            bounds,
            accurateReductionProvider);

        return accurateOnly.Result;
    }

    private static LoopResult RunLoop(
        int maxIterations,
        RateModelSettings rateModel,
        ProfileLimits limits,
        TranscodePolicyResult start,
        ReductionBounds bounds,
        Func<int, double, double, double?> reductionProvider,
        bool captureInBounds = false)
    {
        var cq = start.Cq;
        var maxrate = start.Maxrate;
        var inBounds = false;

        for (var i = 0; i < maxIterations; i++)
        {
            var bufsize = maxrate * rateModel.BufsizeMultiplier;
            var reduction = reductionProvider(cq, maxrate, bufsize);
            if (!reduction.HasValue)
            {
                break;
            }

            var reductionValue = reduction.Value;
            if (IsReductionInBounds(reductionValue, bounds))
            {
                inBounds = true;
                break;
            }

            var prevCq = cq;
            var prevMaxrate = maxrate;

            if (IsReductionBelowBounds(reductionValue, bounds))
            {
                if (cq < limits.CqMax)
                {
                    cq++;
                }

                maxrate = Math.Max(maxrate - rateModel.CqStepToMaxrateStep, limits.MaxrateMin);
            }
            else
            {
                if (cq > limits.CqMin)
                {
                    cq--;
                }

                maxrate = Math.Min(maxrate + rateModel.CqStepToMaxrateStep, limits.MaxrateMax);
            }

            if (prevCq == cq && Math.Abs(prevMaxrate - maxrate) < 0.000001)
            {
                break;
            }
        }

        return new LoopResult(
            Result: start with
            {
                Cq = cq,
                Maxrate = maxrate,
                Bufsize = maxrate * rateModel.BufsizeMultiplier
            },
            InBounds: captureInBounds && inBounds);
    }

    private static ReductionBounds BuildBounds(ReductionRange range)
    {
        var lower = range.MinInclusive ?? range.MinExclusive;
        var lowerInclusive = range.MinInclusive.HasValue;

        var upper = range.MaxInclusive ?? range.MaxExclusive;
        var upperInclusive = range.MaxInclusive.HasValue;

        return new ReductionBounds(
            Lower: lower,
            LowerInclusive: lowerInclusive,
            Upper: upper,
            UpperInclusive: upperInclusive);
    }

    private static bool IsReductionInBounds(double value, ReductionBounds bounds)
    {
        if (bounds.Lower.HasValue)
        {
            if (bounds.LowerInclusive)
            {
                if (value < bounds.Lower.Value)
                {
                    return false;
                }
            }
            else if (value <= bounds.Lower.Value)
            {
                return false;
            }
        }

        if (bounds.Upper.HasValue)
        {
            if (bounds.UpperInclusive)
            {
                if (value > bounds.Upper.Value)
                {
                    return false;
                }
            }
            else if (value >= bounds.Upper.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReductionBelowBounds(double value, ReductionBounds bounds)
    {
        if (!bounds.Lower.HasValue)
        {
            return false;
        }

        if (bounds.LowerInclusive)
        {
            return value < bounds.Lower.Value;
        }

        return value <= bounds.Lower.Value;
    }

    private sealed record ReductionBounds(
        double? Lower,
        bool LowerInclusive,
        double? Upper,
        bool UpperInclusive);

    private sealed record LoopResult(
        TranscodePolicyResult Result,
        bool InBounds);
}
