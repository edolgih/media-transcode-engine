# MediaTranscodeEngine

MediaTranscodeEngine is a `.NET 9` runtime and CLI for inspecting media files and generating scenario-driven transcoding commands.

Current runtime pipeline:

`path -> VideoInspector -> SourceVideo -> TranscodeScenario -> TranscodePlan -> ITranscodeTool -> ToolExecution`

The repository currently implements two application scenarios and produces per-file results:

- `tomkvgpu` - mkv-oriented GPU transcode/remux decisions
- `toh264gpu` - mp4-oriented H.264 GPU transcode/remux decisions
- normal mode: legacy-compatible command lines and `REM ...` diagnostics
- `--info` mode: short decision markers without an `ffmpeg` command

The CLI requires an explicit `--scenario <name>` argument. The current public scenarios are `tomkvgpu` and `toh264gpu`.

## Repository Layout

- `src/MediaTranscodeEngine.Runtime` - runtime model, input inspection, scenarios, and tool adapters
- `src/MediaTranscodeEngine.Cli` - console application built on top of `Runtime`
- `tests/MediaTranscodeEngine.Runtime.Tests` - runtime unit tests
- `tests/MediaTranscodeEngine.Cli.Tests` - CLI contract tests
- `MediaTranscodeEngine.sln` - solution

## Requirements

- `.NET SDK` `9.0.x`
- `ffprobe` with JSON output
- `ffmpeg` with the required filters and encoders

The CLI resolves `ffprobe` and `ffmpeg` paths from standard host configuration sources such as `appsettings.json` and environment variables.

## Build And Test

```bash
dotnet restore
dotnet build MediaTranscodeEngine.sln
dotnet test MediaTranscodeEngine.sln
```

## Documentation

- [README.ru.md](README.ru.md) - Russian overview
- [docs/cli.md](docs/cli.md) - CLI usage and option reference
- [docs/architecture.md](docs/architecture.md) - architecture and timing/sync notes
- [docs/reference](docs/reference) - legacy reference data
