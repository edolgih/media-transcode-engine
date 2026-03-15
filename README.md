# Transcode

Transcode is a `.NET 9` runtime and CLI for inspecting media files and generating scenario-driven transcoding commands.

Current runtime pipeline:

`argv -> CliArgumentParser -> CliParseResult(normalized ScenarioInput) -> CliTranscodeRequest(per input) -> VideoInspector -> SourceVideo -> TranscodeScenario -> ScenarioExecution`

The repository currently implements two application scenarios and produces per-file results:

- `tomkvgpu` - mkv-oriented GPU transcode/remux decisions
- `toh264gpu` - mp4-oriented H.264 GPU transcode/remux decisions
- normal mode: legacy-compatible command lines and `REM ...` diagnostics
- `--info` mode: short decision markers without an `ffmpeg` command

The CLI requires an explicit `--scenario <name>` argument. The current public scenarios are `tomkvgpu` and `toh264gpu`.

## Repository Layout

- `src/Transcode.Runtime` - shared runtime model, input inspection, video settings, and base scenario contracts
- `src/Transcode.Cli.Core` - shared CLI parsing, request orchestration, and scenario registry contracts
- `src/Transcode.Cli` - console host and dependency wiring
- `src/Transcode.Scenarios.ToH264Gpu` - `toh264gpu` scenario runtime logic and CLI adapter
- `src/Transcode.Scenarios.ToMkvGpu` - `tomkvgpu` scenario runtime logic and CLI adapter
- `tests/Transcode.Runtime.Tests` - runtime unit tests
- `tests/Transcode.Cli.Tests` - CLI contract tests
- `Transcode.sln` - solution

## Requirements

- `.NET SDK` `9.0.x`
- `ffprobe` with JSON output
- `ffmpeg` with the required filters and encoders

The CLI resolves `ffprobe` and `ffmpeg` paths from standard host configuration sources such as `appsettings.json` and environment variables.

## Build And Test

```bash
dotnet restore
dotnet build Transcode.sln
dotnet test Transcode.sln
```

## Documentation

- [README.ru.md](README.ru.md) - Russian overview
- [docs/cli.md](docs/cli.md) - CLI usage and option reference
- [docs/cli.ru.md](docs/cli.ru.md) - Russian CLI usage and option reference
- [docs/architecture.md](docs/architecture.md) - architecture and timing/sync notes
- [docs/architecture.ru.md](docs/architecture.ru.md) - Russian architecture and timing/sync notes
- [docs/reference](docs/reference) - legacy reference data
