# CLI Usage

## Show Help

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --help
```

## Generate Commands

Command generation for `tomkvgpu`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv"
```

Info-only output:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --info
```

`downscale 576`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 576
```

Explicit `576` profile:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 576 --content-profile film --quality-profile default
```

`downscale 424`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --downscale 424
```

Overlay with explicit repair mode:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --overlay-bg --sync-audio
```

Frame-rate cap:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --input "D:\\Src\\movie.mkv" --max-fps 30
```

Command generation for `toh264gpu`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.m4v"
```

`toh264gpu` downscale to `576`:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --downscale 576
```

`toh264gpu` encode with AQ:

```bash
dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario toh264gpu --input "D:\\Src\\movie.mkv" --use-aq --aq-strength 4
```

Read paths from stdin:

```powershell
Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | dotnet run --project src/MediaTranscodeEngine.Cli -- --scenario tomkvgpu --info
```

## Supported Options

- `--help`, `-h`
- `--input <path>`; repeatable
- `--scenario <name>`; required, currently `tomkvgpu` or `toh264gpu`
- `--info`

`tomkvgpu` options:

- `--keep-source`
- `--overlay-bg`
- `--downscale <576|480|424>`
- `--max-fps <50|40|30|24>`
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

`toh264gpu` options:

- `--downscale <720|576>`
- `--keep-fps`
- `--downscale-algo <bilinear|bicubic|lanczos>`
- `--cq <1..51>`
- `--nvenc-preset <p1..p7>`
- `--use-aq`
- `--aq-strength <0..15>`
- `--denoise`
- `--fix-timestamps`
- `--mkv`

## Requirements

- `.NET SDK` `9.0.x`
- `ffprobe` with JSON output
- `ffmpeg` with required filters and encoders such as `h264_nvenc` and `scale_cuda`

The CLI resolves binary paths from standard host configuration sources such as `appsettings.json` and environment variables. A minimal `appsettings.json` looks like this:

```json
{
  "RuntimeValues": {
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg"
  }
}
```
