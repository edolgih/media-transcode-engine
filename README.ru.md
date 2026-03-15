# Transcode

`Transcode` - это `.NET 9` runtime и CLI для инспекции медиафайлов и генерации сценарных команд транскодирования.

Текущая минимальная цепочка:

`argv -> CliArgumentParser -> CliParseResult(normalized ScenarioInput) -> CliTranscodeRequest(per input) -> VideoInspector -> SourceVideo -> TranscodeScenario -> ScenarioExecution`

Сейчас в репозитории реализованы два прикладных сценария, и CLI печатает per-file результат:

- `tomkvgpu` - mkv-ориентированные решения по GPU transcode/remux;
- `toh264gpu` - mp4-ориентированные решения по H.264 GPU transcode/remux;
- в обычном режиме: legacy-compatible строки команд и `REM ...` диагностику;
- в `--info` режиме: короткие маркеры решений без `ffmpeg`-команды.

В CLI сценарий нужно указывать явно через `--scenario <name>`. Сейчас публично поддерживаются `tomkvgpu` и `toh264gpu`.

## Структура Репозитория

- `src/Transcode.Core` - общая core-модель, инспекция входного файла, video settings и базовые контракты сценариев
- `src/Transcode.Cli.Core` - общий CLI parsing, orchestration request-ов и контракты registry/handler-ов сценариев
- `src/Transcode.Cli` - консольный host и dependency wiring
- `src/Transcode.Scenarios.ToH264Gpu` - runtime-логика и CLI adapter сценария `toh264gpu`
- `src/Transcode.Scenarios.ToMkvGpu` - runtime-логика и CLI adapter сценария `tomkvgpu`
- `tests/Transcode.Runtime.Tests` - unit-тесты общего core/runtime-поведения
- `tests/Transcode.Cli.Tests` - контрактные тесты CLI
- `Transcode.sln` - solution

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` с JSON output
- `ffmpeg` с нужными фильтрами и кодировщиками

CLI получает пути к `ffprobe` и `ffmpeg` из стандартных источников конфигурации host-а, включая `appsettings.json` и переменные окружения.

## Сборка И Тесты

```bash
dotnet restore
dotnet build Transcode.sln
dotnet test Transcode.sln
```

## Документация

- [README.md](README.md) - English overview
- [docs/cli.md](docs/cli.md) - CLI usage and option reference
- [docs/cli.ru.md](docs/cli.ru.md) - использование CLI
- [docs/architecture.md](docs/architecture.md) - architecture and timing/sync notes
- [docs/architecture.ru.md](docs/architecture.ru.md) - архитектура и заметки по таймлайну/синхронизации
- [docs/reference](docs/reference) - reference-данные из legacy
