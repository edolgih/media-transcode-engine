# MediaTranscodeEngine

`MediaTranscodeEngine` - это `.NET 9` runtime и CLI для инспекции медиафайлов и генерации сценарных команд транскодирования.

Текущая минимальная цепочка:

`path -> VideoInspector -> SourceVideo -> TranscodeScenario -> TranscodePlan -> ITranscodeTool -> ToolExecution`

Сейчас в репозитории реализованы два прикладных сценария, и CLI печатает per-file результат:

- `tomkvgpu` - mkv-ориентированные решения по GPU transcode/remux;
- `toh264gpu` - mp4-ориентированные решения по H.264 GPU transcode/remux;
- в обычном режиме: legacy-compatible строки команд и `REM ...` диагностику;
- в `--info` режиме: короткие маркеры решений без `ffmpeg`-команды.

В CLI сценарий нужно указывать явно через `--scenario <name>`. Сейчас публично поддерживаются `tomkvgpu` и `toh264gpu`.

## Структура Репозитория

- `src/MediaTranscodeEngine.Runtime` - runtime-модель, инспекция входного файла, сценарии и tool adapters
- `src/MediaTranscodeEngine.Cli` - консольное приложение поверх `Runtime`
- `tests/MediaTranscodeEngine.Runtime.Tests` - unit-тесты runtime-поведения
- `tests/MediaTranscodeEngine.Cli.Tests` - контрактные тесты CLI
- `MediaTranscodeEngine.sln` - solution

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` с JSON output
- `ffmpeg` с нужными фильтрами и кодировщиками

CLI получает пути к `ffprobe` и `ffmpeg` из стандартных источников конфигурации host-а, включая `appsettings.json` и переменные окружения.

## Сборка И Тесты

```bash
dotnet restore
dotnet build MediaTranscodeEngine.sln
dotnet test MediaTranscodeEngine.sln
```

## Документация

- [README.md](README.md) - English overview
- [docs/cli.md](docs/cli.md) - использование CLI
- [docs/architecture.md](docs/architecture.md) - архитектура и заметки по таймлайну/синхронизации
- [docs/reference](docs/reference) - reference-данные из legacy
