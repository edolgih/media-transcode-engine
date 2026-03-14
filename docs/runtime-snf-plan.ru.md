# План Нормализации Runtime До Semantic Normal Form

Связанные документы:

- [architecture.ru.md](architecture.ru.md)
- [code-design.md](code-design.md)

## Цель

Привести runtime-модель к состоянию, где:

- `null` используется только для `unknown` или `not applicable`;
- один семантический факт хранится в одном месте;
- defaults разрешаются на границе слоя, а не протягиваются через несколько уровней;
- взаимоисключающие режимы по возможности выражаются типами, а не комбинациями `bool` и nullable-полей;
- пустая коллекция выражается пустой коллекцией, а не `null`.

Это должно улучшить:

- простоту сопровождения;
- читаемость;
- локальность изменений;
- надежность инвариантов;
- предсказуемость дальнейших расширений по выбранным осям.

## Область Работы

Текущий фокус: `Runtime`.

CLI в этой задаче затрагивается только постольку, поскольку он передает данные в runtime и не должен навязывать runtime ложную необязательность.

## Критерии Semantic Normal Form

### Разрешенные случаи nullable/optional

Разрешено оставлять nullable или optional только там, где это выражает реальную семантику:

- сырые probe-данные могут быть неизвестны;
- scenario-specific `TranscodeExecutionSpec` может отсутствовать;
- override-поле может не быть задано, если это настоящий override, а не скрытый default.

### Запрещенные случаи ложной необязательности

Нужно убирать состояния, где:

- `null` эквивалентен пустому списку;
- `null` эквивалентен "пустому request object";
- два поля кодируют один и тот же факт;
- default всегда существует, но выражен через nullable-параметр;
- корректность объекта зависит от комбинации нескольких слабо связанных флагов.

## Инвентаризация И Задачи

### RT-SNF-01. Убрать Пустой `VideoSettingsRequest` `(выполнено)`

Проблема:

- `VideoSettingsRequest` может существовать как пустой объект;
- далее он многократно схлопывается через `HasValue`;
- это лишнее промежуточное состояние.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsRequest.cs`
- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuRequest.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuRequest.cs`

Целевая форма:

- если `VideoSettingsRequest` существует, он содержит хотя бы один override;
- отсутствие video settings выражается только `null`.

Подход:

1. Запретить "пустой" runtime-экземпляр в обычном конструкторе или ввести фабрику `CreateOrNull(...)`.
2. Убрать нормализацию вида `videoSettings?.HasValue == true ? videoSettings : null`.
3. Привести call sites к одному способу создания объекта.

Критерий готовности:

- в production runtime нет состояния "существует пустой `VideoSettingsRequest`";
- `HasValue` больше не нужен для normal flow либо используется только в переходной совместимости.

### RT-SNF-02. Убрать Дублирование Resolution State В `TranscodePlan` `(выполнено)`

Проблема:

- `TargetHeight` и `Downscale.TargetHeight` кодируют один и тот же факт;
- план вынужден проверять согласованность между двумя представлениями одного состояния.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`

Целевая форма:

- изменение разрешения представлено одним значением в одном месте;
- derived accessors допустимы, дублируемое хранимое состояние нет.

Подход:

1. Сделать `Downscale` единственным источником истины для resize-пути или ввести один `ResolutionChange`-тип.
2. Перевести `TargetHeight` в derived property либо убрать из модели.
3. Удалить проверки на согласованность дублирующих полей.

Критерий готовности:

- в runtime больше нет двух независимых хранимых носителей одного факта об output height.

### RT-SNF-03. Разложить Video Part `TranscodePlan` На Явные Режимы `(выполнено)`

Проблема:

- `CopyVideo`, `TargetVideoCodec`, `PreferredBackend`, `VideoCompatibilityProfile`, `EncoderPreset`, `TargetFramesPerSecond`, `UseFrameInterpolation` задают не одно поле выбора, а матрицу состояний;
- значительная часть инвариантов выражена защитными `if`.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuFfmpegTool.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuFfmpegTool.cs`

Целевая форма:

- видео-режим выражен явно как `Copy` или `Encode(...)`;
- encode-режим содержит обязательные параметры и не допускает semantically incomplete state.

Подход:

1. Выделить отдельную video-модель режима.
2. Сделать encode-specific поля обязательными внутри encode-ветки.
3. Упростить tool-адаптеры до разбора одного явного режима вместо логики по набору флагов.

Критерий готовности:

- инварианты copy-vs-encode больше не поддерживаются комбинацией nullable-полей и `bool`.

### RT-SNF-04. Разложить Audio Part На Явные Режимы `(выполнено)`

Проблема:

- `CopyAudio`, `FixTimestamps`, `SynchronizeAudio` выражают несколько разных аудио-путей через флаги;
- repair-path вычисляется косвенно.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuFfmpegTool.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuFfmpegTool.cs`

Целевая форма:

- аудио-режим выражен явно как `Copy`, `Encode` или `RepairEncode`;
- timestamp/sync semantics не дублируются в нескольких флагах без явного режима.

Подход:

1. Выделить отдельную audio-модель режима.
2. Перенести repair semantics в explicit mode.
3. Свести helper-branching к разбору mode, а не комбинаций флагов.

Критерий готовности:

- audio path выбирается одним явным значением режима.

### Контрольная Точка После Шага 4 `(выполнено)`

После выполнения задач `RT-SNF-01`, `RT-SNF-06`, `RT-SNF-02` и шага проверки:

1. Выполнить повторный анализ проекта на предмет соответствия принципам из `docs/architecture*.md` и `docs/code-design.md`.
2. Подготовить вывод-резюме:
   - что уже приведено к semantic normal form;
   - какие ложные optional-состояния еще остались;
   - не появилась ли лишняя shared-сложность;
   - достаточно ли модель стала проще для следующего этапа.
3. Только после этого переходить к следующим задачам.

### RT-SNF-05. Нормализовать `ToH264GpuExecutionSpec`

Проблема:

- `ToH264GpuExecutionSpec` сейчас представляет собой sparse option bag;
- tool продолжает достраивать defaults из `null`, значит spec не полностью нормализован.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuExecutionSpec.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuFfmpegTool.cs`

Целевая форма:

- execution spec содержит уже разрешенные значения для выбранного execution path;
- encode/mux/audio подчасти разделены, где это оправдано.

Подход:

1. Выделить подмодели по исполнению: mux, video encode, audio encode.
2. Перенести разрешение defaults в scenario-слой.
3. Упростить ffmpeg-tool до рендера уже нормализованного payload.

Критерий готовности:

- tool больше не использует `??` для заполнения основных execution defaults выбранного пути.

### RT-SNF-06. Убрать `null-as-empty` И Ложную Optional-ность В Infrastructure `(выполнено)`

Проблема:

- часть runtime API принимает `null`, хотя смысл совпадает с пустым списком, default request или обычным nullable-значением без default parameter;
- это размывает инварианты без добавления доменной семантики.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Tools/ToolExecution.cs`
- `src/MediaTranscodeEngine.Runtime/Videos/SourceVideo.cs`
- `src/MediaTranscodeEngine.Runtime/VideoSettings/Profiles/VideoSettingsProfile.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Tools/ITranscodeTool.cs`

Целевая форма:

- `null` в инфраструктурных типах остается только когда это реально отдельная семантика;
- пустые коллекции и default objects передаются явно.

Подход:

1. `ToolExecution.commands` сделать non-null.
2. `SourceVideo.audioCodecs` сделать non-null.
3. Убрать `= null` из `executionSpec` в `ITranscodeTool`, сохранив nullable-тип как значение.
4. Сценарные ctor с nullable-request заменить на required ctor или явный overload без параметров.
5. Где возможно, пустые profile lists передавать явно, а не через nullable ctor args.

Критерий готовности:

- infrastructure-level `null` больше не заменяет пустую коллекцию, default object или API convenience.

### RT-SNF-07. Ввести Явный `EffectiveVideoSettingsSelection`

Проблема:

- nullable `contentProfile`, `qualityProfile`, `autoSampleMode` дотягиваются глубоко до profile-layer;
- defaults разрешаются несколько раз и в разных местах.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsResolver.cs`
- `src/MediaTranscodeEngine.Runtime/VideoSettings/Profiles/VideoSettingsProfile.cs`

Целевая форма:

- profile-layer работает уже с fully resolved selection;
- nullable overrides живут только в request-layer.

Подход:

1. Отделить override-request от resolved selection.
2. Разрешать defaults в resolver.
3. Сделать внутренние profile API non-null по профилям и autosample mode.

Критерий готовности:

- profile-layer больше не принимает nullable content/quality/mode, если значения уже должны быть разрешены.

### RT-SNF-08. Зафиксировать Границы Допустимой Optional-ности В Документации

Проблема:

- без явного правила новые типы могут снова начать копить ложную optional-ность;
- критерии надо закрепить как архитектурное ограничение.

Текущие точки:

- `docs/architecture.md`
- `docs/architecture.ru.md`

Целевая форма:

- в документации явно зафиксировано, где nullable/optional допустим, а где нет.

Подход:

1. Добавить короткий раздел про semantic normal form runtime.
2. Зафиксировать правило про `unknown`, `not applicable`, overrides и запрет `null-as-empty`.

Критерий готовности:

- архитектурные документы содержат правило, по которому можно ревьюить новые runtime-типы.

### RT-SNF-09. Убрать Legacy Flattening Accessors Из `TranscodePlan`

Проблема:

- после введения `VideoPlan` и `AudioPlan` в `TranscodePlan` сохраняется переходный flattening layer;
- новые потребители могут продолжать зависеть от плоской формы (`TargetVideoCodec`, `PreferredBackend`, `VideoCompatibilityProfile`, `TargetHeight`, `TargetFramesPerSecond`, `UseFrameInterpolation`, `VideoSettings`, `Downscale`, `EncoderPreset`) вместо `plan.Video` и `plan.Audio`;
- shared model временно предоставляет два параллельных API поверх одного и того же смысла.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Plans/TranscodePlan.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/*`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/*`
- `tests/MediaTranscodeEngine.Runtime.Tests/*`

Целевая форма:

- потребители работают с `plan.Video` и `plan.Audio`, а не с плоскими derived-accessors encode-данных;
- flattening accessors, дублирующие shape `VideoPlan` и `AudioPlan`, удалены;
- в `TranscodePlan` остаются только осознанные semantic queries, если они реально упрощают чтение (`CopyVideo`, `CopyAudio`, `RequiresVideoEncode`, `RequiresAudioEncode`, `FixTimestamps`, `SynchronizeAudio`, `ChangesResolution`, `ChangesFrameRate`).

Подход:

1. Перевести formatter/tool/helper/test call-sites на `plan.Video` и `plan.Audio`.
2. Удалить accessors, которые лишь проксируют encode-specific shape из `VideoPlan` и `AudioPlan`.
3. Оставить только те derived queries, которые дают полезную доменную абстракцию, а не flattening ради совместимости.

Критерий готовности:

- `TranscodePlan` больше не предоставляет legacy flattening API для video/audio encode-shape;
- новые изменения по video/audio-осям не требуют возвращаться к плоской форме модели.

### RT-SNF-10. Вынести Effective Video Settings И Autosample Resolution Из `ToMkvGpuTool`

Проблема:

- `ToMkvGpuFfmpegTool` сейчас не только рендерит execution, но и сам принимает domain-решения по effective video settings;
- autosample/measurer semantics скрыты внутри tool-слоя и не выражены как явный runtime-контракт;
- `toh264gpu` и `tomkvgpu` расходятся по месту, где происходит profile-driven normalization.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuFfmpegTool.cs`
- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsResolver.cs`
- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsAutoSampler.cs`

Целевая форма:

- `ToMkvGpuScenario` или выделенный scenario-local builder подготавливает уже resolved execution payload;
- tool получает готовые effective video settings и не вызывает `VideoSettingsResolver` сам;
- правило autosample выражено явно:
  - `fast` может работать без measurer и оставаться estimate-only;
  - `accurate` и sample-backed фаза `hybrid` опираются на measurer-backed provider, если сценарий поддерживает sample measurement.

Подход:

1. Ввести scenario-specific execution payload для `tomkvgpu`.
2. Перенести `ResolveVideoSettings(...)` и связанную source-bitrate/autosample resolution из tool в scenario-local runtime component.
3. Передавать measurer-backed provider явно в момент построения execution payload, а не держать его скрытым знанием tool-а.
4. Оставить `ToMkvGpuFfmpegTool` только рендерингом уже нормализованного payload.

Критерий готовности:

- `ToMkvGpuFfmpegTool` больше не вызывает `VideoSettingsResolver` напрямую;
- autosample/measurer behavior задается явным runtime-контрактом и одинаково читается из scenario execution path.

### RT-SNF-11. Упростить Scenario-Local Структуру `tomkvgpu` `(выполнено)`

Проблема:

- локальное поведение `tomkvgpu` сейчас размазано между `ToMkvGpuScenario`, `ToMkvGpuExecutionSpecBuilder` и helper-типами;
- это увеличивает навигационную стоимость и число промежуточных слоев без появления новой общей оси модели;
- по стилю `tomkvgpu` расходится с более цельным `toh264gpu`, где большая часть локального поведения читается из одного scenario object.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuExecutionSpecBuilder.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuVideoGeometry.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuScenario.cs`

Целевая форма:

- если поведение естественно принадлежит состоянию сценария, оно живет в одном более насыщенном scenario-local объекте;
- промежуточные builder/helper-типы остаются только там, где они реально убирают инварианты или несут отдельную семантику;
- `tomkvgpu` и `toh264gpu` остаются разными по поведению, но не расходятся без необходимости по стилю локальной организации.

Подход:

1. Пересмотреть scenario-local split `tomkvgpu` на предмет типов, которые не добавляют новой семантики.
2. По возможности вернуть execution-spec preparation и связанные helper-решения в сам `ToMkvGpuScenario`.
3. Оставить отдельными только payload/value-типы, которые реально выражают отдельное состояние или контракт.

Критерий готовности:

- execution path `tomkvgpu` в основном читается из одного scenario-local объекта;
- число промежуточных scenario-local типов уменьшено до тех, которые реально окупаются семантикой или инвариантами.

### RT-SNF-12. Довести Ownership Defaults До Границы Слоя `(выполнено)`

Проблема:

- часть execution defaults все еще достраивается в ffmpeg-tool слое через `??`, хотя default уже существует и должен иметь одного владельца;
- `nvenc preset` остается размазан между scenario и tool;
- у `toh264gpu` default downscale algorithm тоже все еще скрыт внутри renderer-а.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuFfmpegTool.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuScenario.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToMkvGpu/ToMkvGpuFfmpegTool.cs`

Целевая форма:

- каждый execution default разрешается один раз на границе слоя, который владеет этим выбором;
- tool получает уже нормализованный payload и не достраивает ключевые defaults выбранного path;
- `nvenc preset` и default algorithm имеют одного явного владельца.

Подход:

1. Явно зафиксировать owner для defaults `nvenc preset` и default downscale algorithm.
2. Нормализовать эти значения в scenario/execution-spec path или в runtime value type, который ими владеет.
3. Убрать renderer-side fallback-ветки для обычного execution path.

Критерий готовности:

- `ToH264GpuFfmpegTool` и `ToMkvGpuFfmpegTool` больше не используют `??` для основных execution defaults выбранного path;
- review нового path можно делать по правилу "default разрешается один раз у владельца".

### RT-SNF-14. Довести `ToH264GpuRequest` До Полностью Нормализованного Входного Объекта `(выполнено)`

Проблема:

- `ToH264GpuRequest` все еще несет часть raw-ish nullable состояния, хотя для `nvenc preset` у сценария уже есть статический default;
- `ToH264GpuScenario` вынужден дожимать `Request.NvencPreset` через `??`, хотя это не source-dependent решение;
- в результате граница "после request объект уже нормализован" проводится неявно и не одинаково между `toh264gpu` и `tomkvgpu`.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuRequest.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuScenario.cs`
- `tests/MediaTranscodeEngine.Runtime.Tests/Scenarios/ToH264GpuRequestTests.cs`
- `tests/MediaTranscodeEngine.Runtime.Tests/Scenarios/ToH264GpuScenarioTests.cs`

Целевая форма:

- `ToH264GpuRequest` представляет собой нормализованный scenario input, а не carrier сырых nullable-полей;
- static scenario defaults разрешаются в `Request`, если они не зависят от inspected video или branch selection;
- `ToH264GpuScenario` использует `Request` напрямую без inline fallback-логики для `nvenc preset`.

Подход:

1. Нормализовать `NvencPreset` в `ToH264GpuRequest` до non-null значения.
2. Убрать `??` из `ToH264GpuScenario` и сделать `Request` единственным owner-ом этого static default.
3. Обновить тесты request/scenario/cli на контракт "после построения request preset уже разрешен".

Критерий готовности:

- `ToH264GpuRequest.NvencPreset` больше не выражает static default через nullable;
- `ToH264GpuScenario` не достраивает `nvenc preset`;
- review по `toh264gpu` можно делать по правилу "scenario получает уже нормализованный request".

### RT-SNF-15. Уточнить Ownership Effective Downscale Семантики `(выполнено)`

Проблема:

- `DownscaleRequest` сейчас хорошо выражает explicit intent, но не выражает, где живет effective algorithm normal execution path;
- default algorithm для `toh264gpu` уже убран из renderer-а, но пока нормализуется ad hoc в scenario-коде;
- из-за этого ответственность между "explicit override" и "effective execution value" пока остается не полностью очевидной.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/VideoSettings/DownscaleRequest.cs`
- `src/MediaTranscodeEngine.Runtime/Scenarios/ToH264Gpu/ToH264GpuScenario.cs`
- `tests/MediaTranscodeEngine.Runtime.Tests/Scenarios/ToH264GpuScenarioTests.cs`
- `tests/MediaTranscodeEngine.Runtime.Tests/Tools/FfmpegToolTests.cs`

Целевая форма:

- ownership effective downscale algorithm читается явно и не размазан между несколькими late-stage fallback-ветками;
- normal execution path работает с non-null algorithm там, где уже выбран resize path;
- решение остается простым: без новых типов, если их не требует модель, но и без скрытой полунормализации.

Подход:

1. Явно выбрать owner effective algorithm для normal execution path.
2. Зафиксировать его в коде как часть нормализованного request/value object или другой минимальной модели, которая реально окупается.
3. Привести тесты к одному контракту для explicit override и default algorithm.

Критерий готовности:

- у effective downscale algorithm есть один читаемый owner;
- normal flow не зависит от разрозненных локальных `??` или implicit знаний renderer-а;
- downscale path читается как уже нормализованный runtime contract.

### RT-SNF-13. Сузить Shared Video-Settings Каталог До Реально Используемой Семантики `(выполнено)`

Проблема:

- shared profile catalog все еще хранит поля, которые не участвуют в текущем runtime-поведении;
- это расширяет общую модель без дополнительной доменной выразительности;
- лишняя ширина shared-типа повышает риск дальнейшего ложного усложнения.

Текущие точки:

- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsProfiles.cs`
- `src/MediaTranscodeEngine.Runtime/VideoSettings/VideoSettingsAutoSampler.cs`
- `src/MediaTranscodeEngine.Runtime/VideoSettings/Profiles/VideoSettings*.cs`

Целевая форма:

- shared video-settings каталог содержит только поля, которые реально влияют на runtime behavior;
- если поле остается в shared profile model, у него есть читаемый behavior contract и тестовое покрытие;
- profile catalog не несет "про запас" конфигурационную ширину без исполняемого смысла.

Подход:

1. Провести инвентаризацию полей `VideoSettingsAutoSampling`, включая `EnabledByDefault`, `HybridAccurateIterations`, `AudioBitrateEstimateMbps`.
2. Для каждого поля выбрать одно из двух: удалить как неиспользуемое или подключить к реальному поведению с тестами.
3. Свести profile definitions и resolver/sampler behavior к одному реально используемому набору данных.

Критерий готовности:

- в shared video-settings catalog не остается полей без исполняемого runtime-смысла;
- общая profile-driven ось остается минимальной и читаемой.

## Порядок Выполнения

Первая безопасная партия:

1. `RT-SNF-01` `(выполнено)`
2. `RT-SNF-06` `(выполнено)`
3. `RT-SNF-02` `(выполнено)`
4. Проверка сборки и тестов для затронутых областей `(выполнено)`
5. Повторный анализ проекта и вывод-резюме соответствия `(выполнено)`

Следующая партия:

6. `RT-SNF-03` `(выполнено)`
7. `RT-SNF-04` `(выполнено)`
8. `RT-SNF-07` `(выполнено)`

Затем:

9. `RT-SNF-05` `(выполнено)`
10. `RT-SNF-09` `(выполнено)`
11. `RT-SNF-10` `(выполнено)`
12. `RT-SNF-11` `(выполнено)`
13. `RT-SNF-12` `(выполнено)`
14. `RT-SNF-14` `(выполнено)`
15. `RT-SNF-15` `(выполнено)`
16. `RT-SNF-13` `(выполнено)`
17. `RT-SNF-08`

## Что Считать Успехом На Промежуточной Контрольной Точке

После шага 5 должно быть видно, что:

- уменьшилось число runtime-состояний, которые отличаются технически, но не семантически;
- `TranscodePlan` и связанные request-типы стали проще читать;
- часть инвариантов переехала из defensive checks в форму данных;
- следующая волна refactoring может идти без наращивания shared-сложности.
