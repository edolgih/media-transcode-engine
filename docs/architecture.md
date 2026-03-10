# Architecture And Behavior Notes

## Runtime Flow

Current runtime pipeline:

`path -> VideoInspector -> SourceVideo -> TranscodeScenario -> TranscodePlan -> ITranscodeTool -> ToolExecution`

Core runtime types:

- `src/MediaTranscodeEngine.Runtime/Videos/SourceVideo.cs` - normalized facts about the input file.
- `src/MediaTranscodeEngine.Runtime/Videos/VideoInspector.cs` - builds `SourceVideo` from probe output.
- `src/MediaTranscodeEngine.Runtime/Scenarios/TranscodeScenario.cs` - makes domain decisions.
- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs` - tool-agnostic transform intent.
- `src/MediaTranscodeEngine.Runtime/Tools/ITranscodeTool.cs` - concrete execution backend.
- `src/MediaTranscodeEngine.Runtime/Tools/ToolExecution.cs` - final execution recipe.

CLI wiring:

- `src/MediaTranscodeEngine.Cli/Program.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/CliScenarioRegistry.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/ICliScenarioHandler.cs`
- `src/MediaTranscodeEngine.Runtime/Inspection/FfprobeVideoProbe.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuFfmpegTool.cs`

CLI flow at a high level:

- the common CLI layer parses shared arguments such as the required scenario name, input paths, and `--info`;
- the selected scenario validates its own scenario-specific arguments;
- processing then loads source facts, asks the scenario to build a `TranscodePlan`, and picks the first tool that can execute that plan;
- in practice, adding a new application scenario should mainly mean adding one new CLI scenario handler plus the runtime request/scenario types it uses; if the ffmpeg rendering policy differs materially, it may also justify a dedicated tool adapter instead of growing a shared one.

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

## Current Behavior Covered By Tests

The new implementation already fixes and verifies:

- `ffprobe` boundary handling and `SourceVideo` normalization
- `copy vs encode`
- `keep-source`
- `wmv/asf -> encode + timestamp normalization`
- `OverlayBg`
- `downscale 576` profile behavior
- `downscale 480` profile behavior
- `max-fps` cap behavior
- legacy CLI contract:
  - `chcp 65001`
  - `REM ...` diagnostics
  - single-line commands joined with `&&`

## Reference Data

Stable legacy reference data remains under:

- `docs/reference/legacy-fe62f0c/ToMkvGPU.576.Profiles.yaml`
