# Architecture And Behavior Notes

Russian version: [architecture.ru.md](architecture.ru.md)

## Runtime Flow

Current runtime pipeline:

`path -> VideoInspector -> SourceVideo -> TranscodeScenario -> TranscodePlan (+ optional TranscodeExecutionSpec) -> ITranscodeTool -> ToolExecution`

Core runtime types:

- `src/MediaTranscodeEngine.Runtime/Videos/SourceVideo.cs` - normalized facts about the input file.
- `src/MediaTranscodeEngine.Runtime/Videos/VideoInspector.cs` - builds `SourceVideo` from probe output.
- `src/MediaTranscodeEngine.Runtime/Scenarios/TranscodeScenario.cs` - makes domain decisions.
- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs` - tool-agnostic transform intent.
- `src/MediaTranscodeEngine.Runtime/Scenarios/TranscodeExecutionSpec.cs` - optional scenario-specific execution payload for a concrete tool.
- `src/MediaTranscodeEngine.Runtime/Tools/ITranscodeTool.cs` - concrete execution backend.
- `src/MediaTranscodeEngine.Runtime/Tools/ToolExecution.cs` - final execution recipe.

CLI wiring:

- `src/MediaTranscodeEngine.Cli/Program.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/CliScenarioRegistry.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/ICliScenarioHandler.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/ToMkvGpu/ToMkvGpuCliRequestParser.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/ToH264Gpu/ToH264GpuCliRequestParser.cs`
- `src/MediaTranscodeEngine.Runtime/Inspection/FfprobeVideoProbe.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuFfmpegTool.cs`

CLI flow at a high level:

- the common CLI layer parses shared arguments such as the required scenario name, input paths, and `--info`;
- the selected scenario owns its raw scenario-specific argv parsing in the CLI layer, validates those transport values, and maps them to its runtime request type;
- processing then loads source facts, asks the scenario to build a `TranscodePlan` and an optional `TranscodeExecutionSpec`, and picks the first tool that can execute that combination;
- in practice, adding a new application scenario should mainly mean adding one new CLI scenario handler plus the runtime request/scenario types it uses; if the ffmpeg rendering policy differs materially, it may also justify a dedicated tool adapter instead of growing a shared one.
- ordinary encode and downscale now share the same profile-driven video-settings axis: output-height buckets, content/quality profiles, bucket bounds, and autosample/bitrate-hint adjustment all come from the shared video-settings profile catalog rather than scenario-local hardcoded fallbacks.
- runtime request/value types no longer know raw `--option` spellings; the CLI layer is the transport adapter and runtime stays the domain source of truth.

## Runtime-CLI Boundary

Current boundary rules:

- `Runtime` owns domain request objects, supported-value catalogs, normalization, validation, and scenario invariants.
- `CLI` owns raw option names, argv token reading, required-value checks, parse diagnostics, and help rendering.
- `CLI` may contain option-to-domain binding logic, because that is transport-adapter knowledge.
- `CLI` must not carry its own domain supported-value lists when the same values already exist in `Runtime`.
- `Runtime` must not contain `--option` literals, argv parsing, or CLI help formatting concerns.

In practice:

- scenario-local CLI parsers translate argv into runtime request objects;
- runtime request/value types validate canonical domain values;
- CLI help should format supported values from runtime-owned catalogs instead of duplicating those lists.

## Runtime Modeling Rules

- `null` is used only for real semantics such as `unknown`, `not applicable`, or a true override that was not provided.
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
