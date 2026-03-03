# MediaTranscodeEngine

`.NET 9` engine for media transcode command generation.

## Architecture

- C# (`Core` + `Infrastructure`) contains business logic and profile policy.
- PowerShell functions are thin wrappers over this engine.
- Wrapper contract:
  - normal path: outputs text command (`ffmpeg ...`) or `REM ...` for soft cases;
  - config/contract errors (for example invalid content/quality profile): raise error, not `REM`.
  - compatibility override: `ToMkvGPU -ForceVideoEncode` forces video re-encode even for copyable H.264.

## Repository layout

- `src/MediaTranscodeEngine.Core` - engine, policy, command builder, infrastructure adapters
- `src/MediaTranscodeEngine.Cli` - console host for engine commands (no in-process DLL loading in PowerShell)
- `src/MediaTranscodeEngine.Core/Infrastructure/Profiles/ToMkvGPU.576.Profiles.yaml` - default 576 profile config
- `tests/MediaTranscodeEngine.Core.Tests` - xUnit/NSubstitute/FluentAssertions tests
- `docs/ToMkvGPU.Baseline.md` - baseline parity scenarios
- `MediaTranscodeEngine.sln` - solution

## Runtime requirements

- `.NET SDK`: `9.0.x`
- `ffprobe`: `6.x+` with JSON probe output support:
  - `-print_format json`
  - `-show_format`
  - `-show_streams`
- `ffmpeg`: `6.x+` build with required capabilities:
  - NVENC encoder: `h264_nvenc`
  - CUDA hwaccel path: `-hwaccel cuda`, `-hwaccel_output_format cuda`
  - filters used by engine: `scale_cuda`, `overlay_cuda`, `overlay`, `aresample`
  - NVENC rc options used by command builder: `-rc vbr_hq`, `-spatial_aq`, `-temporal_aq`, `-rc-lookahead`

Notes:
- Specific build numbers are not pinned; compatibility is defined by available features.
- `ffmpeg -encoders` / `ffmpeg -filters` can be used to verify capabilities on target host.

## Build

```bash
dotnet restore
dotnet build
dotnet test
```

## CLI usage

Generate ToMkvGPU commands from explicit files:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- tomkvgpu --input "F:\2. Мульты\Зверополис.mkv" --force-video-encode
```

Generate commands from piped paths (one path per line):

```bash
some_path_producer | dotnet run --project src/MediaTranscodeEngine.Cli -- tomkvgpu --info
```
https://google.com
