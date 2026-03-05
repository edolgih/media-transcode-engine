# MediaTranscodeEngine

`.NET 9` движок и CLI для генерации команд транскодирования медиа.

## Структура репозитория

- `src/MediaTranscodeEngine.Core` - core-логика, политики, построители команд, адаптеры
- `src/MediaTranscodeEngine.Cli` - консольное приложение
- `src/MediaTranscodeEngine.Core/Profiles/ToMkvGPU.576.Profiles.yaml` - профильные данные по умолчанию
- `tests/MediaTranscodeEngine.Core.Tests` - тесты Core (xUnit/NSubstitute/FluentAssertions)
- `tests/MediaTranscodeEngine.Cli.Tests` - контрактные/интеграционные тесты CLI
- `MediaTranscodeEngine.sln` - solution

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` `6.x+` с поддержкой JSON-вывода
- `ffmpeg` `6.x+` с нужными фильтрами/кодировщиками (`h264_nvenc`, `scale_cuda` и т.д.)

## Сборка и тесты

```bash
dotnet restore
dotnet build
dotnet test
```

## Использование CLI

Показать справку:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --help
```

Сгенерировать команду по сценарию:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --scenario tomkvgpu
```

Сценарий с явным переопределением параметров:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --input "D:\Src\movie.mkv" --scenario tomkvgpu --cq 21 --downscale 576
```

Режим `info` для путей из pipe:

```bash
some_path_producer | dotnet run --project src/MediaTranscodeEngine.Cli -- --info --scenario tomkvgpu
```
