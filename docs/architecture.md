# Architecture And Behavior Notes

Russian version: [architecture.ru.md](architecture.ru.md)

This document describes the current implemented architecture and runtime flow.
If a separate refactoring document captures a simpler target shape, that is not a contradiction: `architecture*.md` remains the current-state description until the code changes.

## Runtime Flow

Current runtime pipeline:

`argv -> CliArgumentParser -> CliParseResult(normalized ScenarioInput) -> CliTranscodeRequest(per input) -> VideoInspector -> SourceVideo -> TranscodeScenario -> ScenarioExecution`

Core runtime types:

- `src/Transcode.Cli.Core/Parsing/CliContracts.cs` - shared CLI parse result that carries normalized `ScenarioInput`.
- `src/Transcode.Cli.Core/CliTranscodeRequest.cs` - per-input CLI request built from the common parse result.
- `src/Transcode.Runtime/Videos/SourceVideo.cs` - normalized facts about the input file.
- `src/Transcode.Runtime/Videos/VideoInspector.cs` - builds `SourceVideo` from probe output.
- `src/Transcode.Runtime/Scenarios/TranscodeScenario.cs` - scenario contract that exposes `FormatInfo(...)` and `BuildExecution(...)`.
- `src/Transcode.Runtime/Scenarios/ScenarioExecution.cs` - final scenario-level command recipe.
- `src/Transcode.Runtime/Plans/VideoPlan.cs` and `src/Transcode.Runtime/Plans/AudioPlan.cs` - shared stream-level plan primitives reused by multiple scenarios.
- `src/Transcode.Runtime/VideoSettings/*` - shared video-settings catalog and resolver used by multiple scenarios.
- `src/Transcode.Runtime/Tools/Ffmpeg/FfmpegExecutionLayout.cs` - shared helper for output/temp path layout and post-operations.
- `src/Transcode.Scenarios.ToH264Gpu/Runtime/ToH264GpuDecision.cs` and `src/Transcode.Scenarios.ToMkvGpu/Runtime/ToMkvGpuDecision.cs` - scenario-local rich decision models; they are internal to scenario projects, not shared runtime contracts.

CLI wiring:

- `src/Transcode.Cli/Program.cs`
- `src/Transcode.Cli.Core/Parsing/CliArgumentParser.cs`
- `src/Transcode.Cli.Core/Parsing/CliContracts.cs`
- `src/Transcode.Cli.Core/Scenarios/CliScenarioRegistry.cs`
- `src/Transcode.Cli.Core/Scenarios/ICliScenarioHandler.cs`
- `src/Transcode.Scenarios.ToMkvGpu/Cli/ToMkvGpuCliScenarioHandler.cs`
- `src/Transcode.Scenarios.ToMkvGpu/Cli/ToMkvGpuCliRequestParser.cs`
- `src/Transcode.Scenarios.ToH264Gpu/Cli/ToH264GpuCliScenarioHandler.cs`
- `src/Transcode.Scenarios.ToH264Gpu/Cli/ToH264GpuCliRequestParser.cs`
- `src/Transcode.Runtime/Inspection/FfprobeVideoProbe.cs`
- `src/Transcode.Scenarios.ToH264Gpu/Runtime/ToH264GpuScenario.cs`
- `src/Transcode.Scenarios.ToH264Gpu/Runtime/ToH264GpuFfmpegTool.cs`
- `src/Transcode.Scenarios.ToMkvGpu/Runtime/ToMkvGpuScenario.cs`
- `src/Transcode.Scenarios.ToMkvGpu/Runtime/ToMkvGpuFfmpegTool.cs`

CLI flow at a high level:

- the common CLI layer parses shared arguments such as the required scenario name, input paths, and `--info`;
- the selected scenario handler parses scenario-specific argv exactly once in the CLI layer and returns a normalized runtime request as `ScenarioInput`;
- the common CLI layer stores that normalized scenario input in `CliParseResult`, then builds one `CliTranscodeRequest` per input file without reparsing scenario argv;
- processing loads source facts, asks the handler to create the scenario from the already parsed input, and then calls `scenario.FormatInfo(...)` or `scenario.BuildExecution(...)`;
- concrete ffmpeg command rendering now stays inside scenario projects; the shared runtime no longer resolves tools and no longer routes execution through a shared `plan/spec/tool` pipeline;
- in practice, adding a new application scenario should mainly mean adding one new CLI scenario handler plus the runtime request/scenario-local rendering types it uses.
- ordinary encode and downscale now share the same profile-driven video-settings axis: output-height buckets, content/quality profiles, bucket bounds, and autosample/bitrate-hint adjustment all come from the shared video-settings profile catalog rather than scenario-local hardcoded fallbacks.
- runtime request/value types no longer know raw `--option` spellings; the CLI layer is the transport adapter and runtime stays the domain source of truth.

## Runtime-CLI Boundary

Current boundary rules:

- `Runtime` owns domain request objects, supported-value catalogs, normalization, validation, and scenario invariants.
- `CLI` owns raw option names, argv token reading, required-value checks, parse diagnostics, and help rendering.
- `CLI` may contain option-to-domain binding logic, because that is transport-adapter knowledge.
- scenario-specific argv must be parsed once at the CLI boundary into normalized scenario input.
- `CliParseResult` and per-input `CliTranscodeRequest` carry that normalized scenario input downstream; they are not raw-only carriers anymore.
- info path, normal execution path, and failure path must work from the already available parsed scenario input or avoid reparsing entirely.
- `CLI` must not carry its own domain supported-value lists when the same values already exist in `Runtime`.
- `Runtime` must not contain `--option` literals, argv parsing, or CLI help formatting concerns.

In practice:

- scenario-local CLI parsers translate argv into runtime request objects exactly once;
- `CliArgumentParser` stores the parsed scenario object in `CliParseResult`;
- per-input CLI processing carries that same object through `CliTranscodeRequest`;
- runtime request/value types validate canonical domain values;
- CLI help should format supported values from runtime-owned catalogs instead of duplicating those lists.

## Runtime Modeling Rules

- `null` is used only for real semantics such as `unknown`, `not applicable`, or a true override that was not provided.
- `null` must not stand in for an empty collection, a default object, or API convenience at runtime.
- Nullable override fields are acceptable only while the model still represents unresolved request intent; after resolution, downstream runtime contracts should be non-null.
- One semantic fact should be stored in one place.
- Defaults should be resolved at the boundary of the layer that owns them.
- Mutually exclusive modes should be expressed by types where practical, rather than by combinations of flags and nullable fields.

## Timing, FPS And Sync Notes

- If the goal is only to adapt video while preserving source fps and source timeline, `-fps_mode:v cfr` and `-r` should not be added by default.
- If fps must change, target fps must be explicit and CFR is appropriate for the output path.
- `-fflags +genpts+igndts` is not a general desync fix. It rebuilds timestamps and can reinterpret a marginal source in an unhelpful way.
- `-avoid_negative_ts make_zero` only shifts timestamps to a cleaner zero-based range. It does not repair sync on its own.
- `-af "aresample=async=1:first_pts=0"` actively changes audio timing. It should be treated as repair logic, not as a default encode step.
- `asetpts=N/SR/TB` only renumbers audio PTS from sample order. It does not stretch or repair audio against an unrelated video timeline.
- `-shortest` only trims obvious tails. It does not fix mid-stream desync.
- If the same desync reappears in the same region after a simplified command, the source or the playback device is the first suspect.

Current intent:

- ordinary downscale/encode path stays minimal;
- fps-cap path adds only the framing controls it really needs;
- `--sync-audio` remains an explicit repair mode.

## Reference Data

Stable legacy reference data remains under:

- `docs/reference/legacy-fe62f0c/ToMkvGPU.576.Profiles.yaml`
