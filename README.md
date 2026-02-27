# MediaTranscodeEngine

.NET 9 solution with source code under `src/` and tests under `tests/`.

Current status: `WIP` (C# engine migration in progress).

## Repository layout

- `src/MediaTranscodeEngine.Core` - main library project
- `tests/MediaTranscodeEngine.Core.Tests` - xUnit test project
- `docs/ToMkvGPU.Baseline.md` - required behavior parity baseline
- `MediaTranscodeEngine.sln` - solution file

## Requirements

- `.NET SDK` `9.0.309`
- `ffmpeg` `2026-01-07-git-af6a1dd0b2-full_build-www.gyan.dev`
- `ffprobe` `2026-01-07-git-af6a1dd0b2-full_build-www.gyan.dev`

## Quick start

```bash
dotnet restore
dotnet build
```

## Run tests

```bash
dotnet test
```
