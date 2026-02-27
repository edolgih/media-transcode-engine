# ToH264GPU -> Common Core Analysis

## Scope

This note compares current PowerShell `ToH264GPU` behavior with the existing C# core used by `ToMkvGPU`, and defines a pragmatic migration path to a thin-wrapper model.

Analyzed artifacts:
- `C:\Users\Evgeny\Documents\PowerShell\Modules\MyTools\Public\ToH264GPU.ps1`
- `C:\Users\Evgeny\Documents\PowerShell\Modules\MyTools\Tests\ToH264GPU.Tests.ps1`
- current C# core in this repository

Snapshot:
- `ToH264GPU` PowerShell tests: `50` `It` cases in `12` contexts
- `ToH264GPU` implementation style: one large function (no internal helper decomposition)

## What is already compatible with current C# core

These parts already exist conceptually and can be reused:
- Probe seam model (`ProcessWithProbeJson`/probe override style).
- High-level orchestration: `probe -> decision -> command text`.
- Basic video copy/encode branching.
- NVENC command generation building blocks (`h264_nvenc`, AQ/lookahead, CFR/GOP).
- Downscale path with `scale_cuda` and configurable interpolation algorithm.
- Soft command output conventions (`ffmpeg ...`, `REM ...`, empty output for no-op).

## Gaps for ToH264GPU migration

These behaviors are ToH264-specific and are not covered by current `ToMkvGPU` core as-is:
- Default target container logic (`.mp4` default, optional `.mkv`) and `+faststart` behavior.
- Remux-only fast path for mp4-family inputs (`-c copy`) with VFR suspicion guard (`r_frame_rate != avg_frame_rate`).
- Timestamp-fix policy:
  - explicit `-FixTimestamps`,
  - auto-enable for `wmv/asf` (by extension and by `format_name`).
- Rate-control policy based on source size+duration:
  - normal mode adaptive target bitrate (`vbr`) fallback to CQ,
  - downscale mode caps and fallback logic,
  - `KeepFps` downscale FPS cap behavior.
- Audio policy:
  - codec-specific copy/transcode rules (`aac/mp3` copy, others transcode),
  - AMR-NB special handling (`ac 1`, resample),
  - bitrate clamp corridor (`48..320k`) from source bitrate.
- `-Denoise` behavior (filter usage).
- Downscale `720` mode (current C# core has `720 not implemented` in `ToMkv` flow).

## Proposed common-core extraction (minimal)

Keep current architecture lean and add only what is needed:

1. New H264 flow in core:
- `H264TranscodeEngine` (or mode in existing engine).
- `H264CommandBuilder` (container, mux flags, audio policy, remux fast path support).

2. Policy slices (small, testable):
- `H264RateControlPolicy` (adaptive bitrate/CQ/downscale caps/fallbacks).
- `H264AudioPolicy` (copy/transcode/AMR/corridor).
- `H264TimestampPolicy` (manual + auto fix rules).
- `H264RemuxEligibilityPolicy` (mp4-family + codec + VFR checks).

3. Keep shared infrastructure:
- `FfprobeReader`, `ProcessRunner`, shared probe models, shared wrapper seam patterns.

No new architectural layers are required for this step.

## Migration slices (commit-friendly)

1. Baseline core contracts for ToH264:
- add request/result models for H264 mode;
- add first xUnit tests for remux eligibility and error/soft-output conventions.

2. Add H264 command builder + policies:
- port behavior from current PowerShell logic (small TDD slices);
- keep command text parity as primary acceptance criterion.

3. Wrapper bridge for `ToH264GPU`:
- PowerShell wrapper calls .NET engine by default;
- retain seam for mocked probe JSON in Pester.

4. Pester reduction:
- replace functional Pester checks with wrapper-contract checks only:
  - pipeline input handling,
  - parameter mapping to .NET call,
  - output forwarding (`ffmpeg`, `REM`, errors).

5. Final parity check:
- C# tests own behavior;
- PowerShell tests validate wrapper only.

## Version policy (for docs/README)

For ToH264 path, version requirements should stay capability-based:
- `ffprobe`: JSON output options available.
- `ffmpeg`: `h264_nvenc`, needed filters (`scale_cuda`, `overlay`/`overlay_cuda`, `aresample`, optional `hqdn3d`), and RC flags used by the builder.
- Use major/minor compatibility guidance, avoid pinning exact build IDs.
