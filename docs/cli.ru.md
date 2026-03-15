# Использование CLI

English version: [cli.md](cli.md)

## Показать Справку

```bash
dotnet run --project src/Transcode.Cli -- --help
```

## Генерация Команд

Генерация команд для `tomkvgpu`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv"
```

Вывод только информации:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --info
```

`downscale 720`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 720
```

Явный профиль `576`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 576 --content-profile film --quality-profile default
```

`downscale 424`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 424
```

Overlay с явным repair mode:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --overlay-bg --sync-audio
```

Ограничение frame-rate:

```bash
dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --max-fps 30
```

Генерация команд для `toh264gpu`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.m4v"
```

`toh264gpu` downscale до `576`:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --downscale 576
```

`toh264gpu` с явным выбором quality-oriented profile:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --content-profile film --quality-profile default --autosample-mode fast
```

`toh264gpu` с явным audio-sync repair path:

```bash
dotnet run --project src/Transcode.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --sync-audio --keep-source
```

Чтение путей из stdin:

```powershell
Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | dotnet run --project src/Transcode.Cli -- --scenario tomkvgpu --info
```

## Поддерживаемые Опции

- `--help`, `-h`
- `--input <path>`; можно указывать несколько раз
- `--scenario <name>`; обязательно, сейчас поддерживаются `tomkvgpu` и `toh264gpu`
- `--info`

Опции `tomkvgpu`:

Параметры quality-oriented video settings:

- `--keep-source`
- `--overlay-bg`
- `--downscale <720|576|480|424>`
- `--max-fps <50|40|30|24>`
- `--sync-audio`
- `--content-profile <anime|mult|film>`
- `--quality-profile <high|default|low>`
- `--autosample-mode <accurate|fast|hybrid>`
- `--downscale-algo <bilinear|bicubic|lanczos>`
- `--cq <int>`
- `--maxrate <number>`
- `--bufsize <number>`
- `--nvenc-preset <preset>`

Опции `toh264gpu`:

Параметры quality-oriented video settings:

- `--keep-source`
- `--downscale <720|576|480|424>`
- `--keep-fps`
- `--content-profile <anime|mult|film>`
- `--quality-profile <high|default|low>`
- `--autosample-mode <accurate|fast|hybrid>`
- `--downscale-algo <bilinear|bicubic|lanczos>`
- `--cq <1..51>`
- `--maxrate <number>`
- `--bufsize <number>`
- `--nvenc-preset <p1..p7>`
- `--denoise`
- `--sync-audio`
  Использует явный audio-sync repair path.
- `--mkv`

## Требования

- `.NET SDK` `9.0.x`
- `ffprobe` с JSON output
- `ffmpeg` с нужными фильтрами и кодировщиками, например `h264_nvenc` и `scale_cuda`

CLI получает пути к бинарникам из стандартных источников host configuration, например `appsettings.json` и переменных окружения. Минимальный `appsettings.json` выглядит так:

```json
{
  "RuntimeValues": {
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg"
  }
}
```
