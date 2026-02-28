# ToMkvGPU: План Покрытия Поведения (Pester -> C#)

## Контекст
- В legacy-версии `ToMkvGPU` было `104` Pester-теста (коммит `26ca8be` в PowerShell-репозитории).
- Ранее этот набор уже прогонялся против .NET Core engine через адаптерный слой и служит baseline-референсом поведения.
- Сейчас в `MediaTranscodeEngine.Core.Tests` тестов меньше, и цель не в копировании 1:1, а в полном покрытии поведения без дублирования.

## Базовые Артефакты
- Полный список legacy-тестов: [legacy-tomkvgpu-pester-tests.txt](/C:/Users/Evgeny/Documents/Visual%20Studio%202022/Projects/GitHub/MediaTranscodeEngine/docs/legacy-tomkvgpu-pester-tests.txt)
- Сводка по контекстам: [legacy-tomkvgpu-pester-context-counts.txt](/C:/Users/Evgeny/Documents/Visual%20Studio%202022/Projects/GitHub/MediaTranscodeEngine/docs/legacy-tomkvgpu-pester-context-counts.txt)
- Рабочая матрица переноса (по строке на каждый legacy-тест): [legacy-tomkvgpu-coverage-matrix.csv](/C:/Users/Evgeny/Documents/Visual%20Studio%202022/Projects/GitHub/MediaTranscodeEngine/docs/legacy-tomkvgpu-coverage-matrix.csv)

## Цель
- Тесты должны документировать поведение и защищать от регрессий.
- Бизнес-логика должна быть канонически покрыта в C# (`xUnit`).
- Pester должен остаться тонким контрактом обертки (pipeline/help/вызов engine), без повторения всей бизнес-логики.

## Правило Недублирования
1. Если сценарий проверяет выбор параметров/ветвление/команду ffmpeg, он покрывается в C#.
2. В Pester оставляется только проверка обертки и сквозной контракт вызова.
3. Один поведенческий сценарий имеет один канонический тестовый слой.
4. Допускается минимальный smoke-тест на соседнем слое, но без повторения детальной проверки.

## Инвентарь Legacy Групп
1. `вывод (не -Info)` — 5
2. `Info mode` — 9
3. `I/O и post-операции` — 3
4. `video: выбор copy vs nvenc` — 6
5. `Downscale` — 15
6. `Downscale autosample internals` — 3
7. `Downscale autosample tdd coverage` — 43
8. `video: размеры / ориентация (в filter_complex)` — 2
9. `audio: выбор copy vs AAC encode` — 4
10. `sanitize flags` — 5
11. `общие инварианты команды` — 3
12. `subtitles handling` — 1
13. `OverlayBg (optional filter_complex)` — 5

## Матрица Целевого Покрытия По Блокам
| Блок | Legacy (It) | Канонический слой | Текущий статус | Что закрыть |
|---|---:|---|---|---|
| Info/Non-Info поведение | 14 | Core + тонкий Wrapper | Частично | Довести все маркеры и REM-hints до явных xUnit-кейсов |
| I/O/post-op и quoting | 3 | Core | Частично | Проверить все варианты temp/rename/del и кавычек |
| Выбор copy vs encode (video/audio) | 10 | Core | Частично | Закрыть недостающие ветви без дублирования |
| Sanitize/fflags/async | 5 | Core | Частично | Завершить покрытие условий добавления флагов |
| Downscale 576/720, buckets, profile selection | 15 | Core | Частично | Закрыть все граничные условия bucket/matrix |
| Autosample internals + режимы | 46 | Core | Gap | Реализовать production-provider и покрыть runtime-путь |
| OverlayBg/filter_complex и геометрия | 7 | Core | Частично | Довести edge-cases размеров/ориентации |
| Инварианты команды/subtitles | 4 | Core | Частично | Добить точечные проверки |
| Wrapper-контракт (pipeline/help/маршрутизация параметров) | N/A | Pester | Частично | Оставить минимальный набор контрактных тестов |

## План Выполнения (TDD)
1. `M0` Инвентаризация и трекинг.
2. `M1` Проставить текущий статус каждой строки в `legacy-tomkvgpu-coverage-matrix.csv`.
3. `M2` Закрыть главный gap: production `IAutoSampleReductionProvider` + xUnit для `accurate/fast/hybrid` и условий skip.
4. `M3` Закрыть блоки `Downscale + bucket/matrix` до полного соответствия legacy-сценариям.
5. `M4` Дозакрыть `Info/Non-Info`, `sanitize`, `overlay`, `I/O` недостающими xUnit-кейсами.
6. `M5` Сжать Pester до wrapper-only контрактов (без дублирования бизнес-логики).
7. `M6` Финальный прогон: C# + wrapper Pester, фиксация покрытия и удаление устаревших дублей.

## Критерии Готовности
1. Для каждой из 104 legacy-строк в `legacy-tomkvgpu-coverage-matrix.csv` заполнены `Status` и `TargetTest`.
2. Нет сценариев со статусом `pending` без отдельного решения.
3. Pester содержит только тесты обертки, а бизнес-логика документируется C# тестами.
4. Все тесты зеленые, поведение ToMkvGPU сохраняется.

## Текущий Снимок (M1)
- `covered_core`: 101
- `pending`: 0
- `obsolete_replaced`: 1
- `covered_wrapper`: 0
- `covered_integration`: 0
- `wrapper_only_by_design`: 2

Фокус следующего шага:
1. Закрыть `pending` в блоке autosample runtime (production provider + тесты).
2. Добить pending по `Info` и по edge-cases `overlay/audio/sanitize`, где нет явного xUnit.

## Статусы Для `legacy-tomkvgpu-coverage-matrix.csv`
- `pending`
- `covered_core`
- `covered_wrapper`
- `covered_integration`
- `wrapper_only_by_design`
- `obsolete_replaced`
