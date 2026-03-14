# Заметки По Архитектуре И Поведению

English version: [architecture.md](architecture.md)

## Runtime Flow

Текущий runtime pipeline:

`path -> VideoInspector -> SourceVideo -> TranscodeScenario -> TranscodePlan (+ optional TranscodeExecutionSpec) -> ITranscodeTool -> ToolExecution`

Ключевые runtime-типы:

- `src/MediaTranscodeEngine.Runtime/Videos/SourceVideo.cs` - нормализованные факты о входном файле.
- `src/MediaTranscodeEngine.Runtime/Videos/VideoInspector.cs` - строит `SourceVideo` из probe-output.
- `src/MediaTranscodeEngine.Runtime/Scenarios/TranscodeScenario.cs` - принимает доменные решения.
- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs` - tool-agnostic намерение трансформации.
- `src/MediaTranscodeEngine.Runtime/Scenarios/TranscodeExecutionSpec.cs` - необязательная scenario-specific execution-нагрузка для конкретного tool-а.
- `src/MediaTranscodeEngine.Runtime/Tools/ITranscodeTool.cs` - конкретный backend выполнения.
- `src/MediaTranscodeEngine.Runtime/Tools/ToolExecution.cs` - итоговый рецепт выполнения.

CLI wiring:

- `src/MediaTranscodeEngine.Cli/Program.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/CliScenarioRegistry.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/ICliScenarioHandler.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/ToMkvGpu/ToMkvGpuCliRequestParser.cs`
- `src/MediaTranscodeEngine.Cli/Scenarios/ToH264Gpu/ToH264GpuCliRequestParser.cs`
- `src/MediaTranscodeEngine.Runtime/Inspection/FfprobeVideoProbe.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuFfmpegTool.cs`

Высокоуровневый CLI flow:

- общий CLI-слой парсит shared-аргументы, такие как обязательный scenario name, input paths и `--info`;
- выбранный сценарий владеет raw scenario-specific argv parsing в CLI-слое, валидирует эти transport values и маппит их в свой runtime request type;
- затем обработка загружает source facts, просит сценарий построить `TranscodePlan` и необязательный `TranscodeExecutionSpec`, после чего выбирает первый tool, способный выполнить эту комбинацию;
- на практике добавление нового application scenario должно в основном означать добавление нового CLI scenario handler плюс runtime request/scenario types, которыми он пользуется; если ffmpeg-rendering policy существенно отличается, лучше добавить отдельный adapter/tool, чем раздувать shared one;
- ordinary encode и downscale теперь разделяют одну profile-driven video-settings ось: output-height buckets, content/quality profiles, bucket bounds и autosample/bitrate-hint adjustment приходят из shared video-settings profile catalog, а не из scenario-local hardcoded fallback-ов;
- runtime request/value types больше не знают raw spellings вида `--option`; CLI-слой выступает transport adapter-ом, а runtime остаётся domain source of truth.

## Runtime-CLI Boundary

Текущие правила границы:

- `Runtime` владеет domain request objects, supported-value catalogs, normalization, validation и scenario invariants.
- `CLI` владеет raw option names, argv token reading, required-value checks, parse diagnostics и help rendering.
- `CLI` может содержать option-to-domain binding logic, потому что это transport-adapter knowledge.
- `CLI` не должен держать свои собственные domain supported-value lists, если те же значения уже существуют в `Runtime`.
- `Runtime` не должен содержать `--option` literals, argv parsing или CLI help formatting concerns.

Практически это означает:

- scenario-local CLI parsers переводят argv в runtime request objects;
- runtime request/value types валидируют canonical domain values;
- CLI help должен форматировать supported values из runtime-owned catalogs, а не дублировать эти списки.

## Правила Runtime-Модели

- `null` используется только для реальной семантики, такой как `unknown`, `not applicable` или настоящий неуказанный override.
- Один семантический факт должен храниться в одном месте.
- Defaults должны разрешаться на границе слоя, который ими владеет.
- Взаимоисключающие режимы по возможности должны выражаться типами, а не комбинациями флагов и nullable-полей.

## Timing, FPS And Sync Notes

- Если цель только адаптировать видео, сохранив source fps и source timeline, `-fps_mode:v cfr` и `-r` не должны добавляться по умолчанию.
- Если fps должен меняться, target fps должен быть явным, и для output path уместен CFR.
- `-fflags +genpts+igndts` не является общим desync fix. Он пересобирает timestamps и может неудачно интерпретировать пограничный source.
- `-avoid_negative_ts make_zero` только сдвигает timestamps в более чистый zero-based range. Сам по себе sync он не чинит.
- `-af "aresample=async=1:first_pts=0"` активно меняет audio timing. Его нужно считать repair logic, а не encode-step по умолчанию.
- `asetpts=N/SR/TB` только перенумеровывает audio PTS из sample order. Он не растягивает и не чинит audio относительно несвязанного video timeline.
- `-shortest` только подрезает явные хвосты. Он не исправляет desync в середине.
- Если тот же desync повторяется в том же месте даже после упрощения команды, первый подозреваемый - source или playback device.

Текущее намерение:

- ordinary downscale/encode path остаётся минимальным;
- fps-cap path добавляет только те framing controls, которые действительно нужны;
- `--sync-audio` остаётся явным repair mode.

## Reference Data

Стабильные legacy reference data остаются в:

- `docs/reference/legacy-fe62f0c/ToMkvGPU.576.Profiles.yaml`
