# MediaTranscodeEngine

`.NET 9` runtime и CLI для генерации команд транскодирования медиа.

Сейчас репозиторий собран вокруг новой минимальной цепочки:

`path -> VideoInspector -> SourceVideo -> TranscodeScenario -> TranscodePlan -> ITranscodeTool -> ToolExecution`

CLI использует эту цепочку для сценария `tomkvgpu` и печатает per-file результат:
- в обычном режиме: legacy-compatible строки команд и `REM ...` диагностику
- в `--info` режиме: короткие маркеры решений без `ffmpeg`-команды

## Структура репозитория

- `src/MediaTranscodeEngine.Runtime` - runtime-модель, инспекция входного файла, сценарии и tool adapters
- `src/MediaTranscodeEngine.Cli` - консольное приложение поверх `Runtime`
- `tests/MediaTranscodeEngine.Runtime.Tests` - unit-тесты runtime-поведения
- `tests/MediaTranscodeEngine.Cli.Tests` - контрактные тесты CLI
- `MediaTranscodeEngine.sln` - solution

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` с JSON output
- `ffmpeg` с нужными фильтрами/кодировщиками (`h264_nvenc`, `scale_cuda` и т.д.)

По умолчанию CLI берёт пути к бинарникам из `src/MediaTranscodeEngine.Cli/appsettings.json`:

```json
{
  "RuntimeValues": {
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg"
  }
}
```

## Сборка и тесты

```bash
dotnet restore
dotnet build MediaTranscodeEngine.sln
dotnet test MediaTranscodeEngine.sln
```

## Архитектура

Базовые runtime-типы:

- `src/MediaTranscodeEngine.Runtime/Videos/SourceVideo.cs` - факты о входном видео
- `src/MediaTranscodeEngine.Runtime/Videos/VideoInspector.cs` - читает файл через probe и создаёт `SourceVideo`
- `src/MediaTranscodeEngine.Runtime/Scenarios/TranscodeScenario.cs` - принимает доменные решения
- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs` - tool-agnostic намерение преобразования
- `src/MediaTranscodeEngine.Runtime/Tools/ITranscodeTool.cs` - конкретный инструмент исполнения
- `src/MediaTranscodeEngine.Runtime/Tools/ToolExecution.cs` - готовый recipe для выполнения

CLI подключает runtime через DI в `src/MediaTranscodeEngine.Cli/Program.cs` и использует:

- `src/MediaTranscodeEngine.Runtime/Inspection/FfprobeVideoProbe.cs` для чтения метаданных
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs` для принятия решений
- `src/MediaTranscodeEngine.Runtime/Tools/Ffmpeg/FfmpegTool.cs` для построения `ffmpeg`-команды

## Использование CLI

Показать справку:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --help
```

Сгенерировать команду по умолчанию:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv"
```

Проверить, что будет сделано, без генерации `ffmpeg`-команды:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --info
```

Сгенерировать команды для `downscale 576`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --downscale 576
```

Явно задать профиль `576`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --downscale 576 --content-profile film --quality-profile default
```

Форсировать `OverlayBg` и sync-safe audio path:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --overlay-bg --sync-audio
```

Обработать список путей из stdin:

```bash
Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | dotnet run --project src/MediaTranscodeEngine.Cli -- --info
```

## Поддерживаемые опции CLI

- `--help`, `-h`
- `--input <path>`; repeatable
- `--scenario tomkvgpu`
- `--info`
- `--keep-source`
- `--overlay-bg`
- `--downscale <int>`
- `--sync-audio`
- `--content-profile <anime|mult|film>`
- `--quality-profile <high|default|low>`
- `--no-autosample`
- `--autosample-mode <accurate|fast|hybrid>`
- `--downscale-algo <bilinear|bicubic|lanczos>`
- `--cq <int>`
- `--maxrate <number>`
- `--bufsize <number>`
- `--nvenc-preset <preset>`

## Текущее покрытие поведения

В новой реализации уже перенесены и зафиксированы тестами:

- `ffprobe` boundary и нормализация `SourceVideo`
- `copy vs encode`
- `keep-source`
- `wmv/asf -> encode + timestamp normalization`
- `OverlayBg`
- `downscale 576` static profile behavior
- `legacy CLI contract`:
  - `chcp 65001`
  - `REM ...` diagnostics
  - single-line commands через `&&`

Дальнейшая работа продолжается вокруг полного переноса поведения `ToMkvGPU` из legacy PowerShell tests в текущий `Runtime/CLI`.
