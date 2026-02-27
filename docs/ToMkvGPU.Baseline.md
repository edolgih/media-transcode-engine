# ToMkvGPU Baseline (обязательный паритет v1)

Этот список фиксирует минимальный набор сценариев, которые должны оставаться green:
- в текущем Pester-наборе PowerShell;
- в переносимых xUnit-тестах C# (логика/поведение).

## 1) Downscale 576/720 и профильные настройки
- `ToMkvGPU_Downscale720_WhenRequested_OutputsNotImplementedError`
- `ToMkvGPU_Downscale576_WhenNoAutoSampleUsesProfileDefaults_UsesConfiguredDefaults`
- `ToMkvGPU_Downscale576_WhenContentAndQualityNotSpecified_UsesFilmAndDefaultProfileValues`
- `ToMkvGPU_Downscale576_WhenOnlyContentSpecified_UsesDefaultQualityForSpecifiedContent`
- `ToMkvGPU_Downscale576_WhenOnlyQualitySpecified_UsesFilmContentForSpecifiedQuality`
- `ToMkvGPU_Downscale576_WhenNoAutoSampleAndCqOverride_UsesRateModelAndClamp`
- `ToMkvGPU_Downscale576_WhenNoAutoSampleAndMaxrateOverride_ComputesBufsizeByMultiplier`
- `ToMkvGPU_Downscale576_WhenNoAutoSampleAndExplicitBufsize_UsesExplicitBufsize`
- `ToMkvGPU_Downscale576_WhenDownscaleAlgoSpecified_UsesExplicitAlgo`
- `ToMkvGPU_Downscale576_WhenHeightIsNotAboveTarget_DoesNotApplyDownscale`

## 2) Source bucket / quality corridor
- `ToMkvGPU_Downscale576_WhenSourceBucketIsMissing_OutputsHintAndSkipsCommand`
- `ToMkvGPU_Downscale576_WhenSourceBucketMatrixIncomplete_OutputsHintAndSkipsCommand`
- `Resolve-ToMkvGpuSourceBucket_WhenHeightMatchesConfiguredBucket_ReturnsMatchedBucket`
- `Resolve-ToMkvGpuSourceBucket_WhenHeightDoesNotMatchConfiguredBuckets_ReturnsDefaultBucket`
- `Resolve-ToMkvGpuQualityRange_WhenBucketHasContentRange_ReturnsBucketRange`
- `Resolve-ToMkvGpuQualityRange_WhenBucketDoesNotMatch_FallsBackToGlobalRange`

## 3) Autosampling
- `ToMkvGPU_Downscale576_WhenAutoSampleEnabled_UsesAutoSampleResult`
- `ToMkvGPU_Downscale576_WhenManualCqProvided_SkipsAutoSample`
- `ToMkvGPU_Downscale576_WhenManualMaxrateProvided_SkipsAutoSample`
- `ToMkvGPU_Downscale576_WhenManualBufsizeProvided_SkipsAutoSample`
- `ToMkvGPU_Downscale576_WhenProbeDurationMissing_SkipsAutoSampleAndUsesProfileSettings`
- `Resolve-ToMkvGpu576AutoSampleSettings_WhenSamplingModeFast_UsesProbeEstimateStrategy`
- `Resolve-ToMkvGpu576AutoSampleSettings_WhenSamplingModeAccurate_UsesAccurateStrategy`
- `Resolve-ToMkvGpu576AutoSampleSettings_WhenSamplingModeHybrid_UsesFastThenAccurateStrategies`
- `Resolve-ToMkvGpu576AutoSampleSettings_WhenFastEstimateWithinCorridor_SkipsAccurateRefinement`
- `Resolve-ToMkvGpu576AutoSampleSettings_WhenFastEstimateOutsideCorridor_RunsAccurateRefinement`

## 4) Command invariants (текстовый output в wrapper)
- `всегда добавляет -sn`
- `всегда добавляет -max_muxing_queue_size 4096`
- `всегда выход .mkv`
- overlay routing (`-filter_complex`, `-map "[v]"`) и не-overlay routing (`-map 0:v:0`)
- audio routing (`copy` vs `aac`) и sanitize flags в зависимости от сценария

## 5) Wrapper compatibility
- PowerShell обертка должна сохранить `ValueFromPipeline`.
- Output обертки должен оставаться текстовым (`ffmpeg ...` / `REM ...`).
- Текущий набор Pester-тестов `ToMkvGPU` должен проходить полностью.
