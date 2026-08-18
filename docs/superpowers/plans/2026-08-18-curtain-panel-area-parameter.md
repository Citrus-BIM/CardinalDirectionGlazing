# Curtain Panel Area Parameter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить независимый выбор параметра площади для витражных `Panel`, сохранив текущий `HOST_AREA_COMPUTED` как fallback и не меняя расчёт окон, `Wall`-заполнителей и стен-остекления.

**Architecture:** Оконные классы `WindowAreaParameter*` остаются без изменений. Для панелей создаётся параллельный набор чистых моделей/селекторов и Revit-адаптеров; существующая классификация заполнителей выносится без изменения условий в один сервис, который используют каталог и расчёт. В `ProcessCurtainWallFills` новый источник площади подключается только для runtime-типа `Panel`, после чего площадь проходит существующий путь определения направления и суммирования.

**Tech Stack:** C#, WPF, Autodesk Revit API 2019–2026, SDK-style projects, custom executable regression test harness on .NET 8, XML serialization.

---

## Карта файлов

- Create `CardinalDirectionGlazing/CurtainPanelAreaCalculation.cs` — чистый выбор parameter/fallback и дедуплицированная статистика.
- Create `CardinalDirectionGlazing/CurtainPanelAreaParameterOption.cs` — область и выбранный параметр панели.
- Create `CardinalDirectionGlazing/CurtainPanelAreaParameterCatalogBuilder.cs` — чистая фильтрация и агрегация наблюдений каталога.
- Create `CardinalDirectionGlazing/CurtainPanelAreaParameterSelection.cs` — восстановление выбора и строгая проверка кандидата по GUID/имени.
- Create `CardinalDirectionGlazing/CurtainPanelAreaParameterReader.cs` — чтение экземплярного или типового параметра Revit `Panel`.
- Create `CardinalDirectionGlazing/CurtainGridFillGlazingClassifier.cs` — единая существующая классификация `Panel`/`Wall` с трассировкой.
- Create `CardinalDirectionGlazing/CurtainPanelAreaParameterCatalog.cs` — сбор подходящих параметров из принятых `Panel` текущего и связанных документов.
- Modify `CardinalDirectionGlazing/CardinalDirectionGlazingSettings.cs` — четыре обратносуместимых XML-поля панелей.
- Modify `CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml` — независимая строка `Витражные панели`.
- Modify `CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml.cs` — каталог, восстановление, валидация и сохранение выбора панелей.
- Modify `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs` — полный поток каталога → UI → чтение → площадь → направления → статистика.
- Modify `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj` — подключение чистых файлов панели к harness.
- Modify `CardinalDirectionGlazing.Tests/Program.cs` — TDD и регрессионные сценарии.

### Task 1: Чистый выбор площади и статистика панели

**Files:**
- Create: `CardinalDirectionGlazing/CurtainPanelAreaCalculation.cs`
- Modify: `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Подключить ещё не существующий файл и написать падающие тесты**

В test csproj добавить:

```xml
<Compile Include="..\CardinalDirectionGlazing\CurtainPanelAreaCalculation.cs" Link="CurtainPanelAreaCalculation.cs" />
```

В `Main()` вызвать `AssertCurtainPanelAreaResolution()` и `AssertCurtainPanelAreaUsageSummaryDeduplicatesPanels()`, затем добавить:

```csharp
private static void AssertCurtainPanelAreaResolution()
{
    AssertCurtainPanelArea(false, 10.8, 12.5, 12.5, CurtainPanelAreaValueSource.HostArea, string.Empty);
    AssertCurtainPanelArea(true, 10.8, 12.5, 10.8, CurtainPanelAreaValueSource.Parameter, string.Empty);
    AssertCurtainPanelArea(true, null, 12.5, 12.5, CurtainPanelAreaValueSource.HostAreaFallback, "MissingParameter");
    AssertCurtainPanelArea(true, 0, 12.5, 12.5, CurtainPanelAreaValueSource.HostAreaFallback, "NonPositiveParameter");
    AssertCurtainPanelArea(true, -1, 12.5, 12.5, CurtainPanelAreaValueSource.HostAreaFallback, "NonPositiveParameter");
    AssertCurtainPanelArea(true, double.NaN, 12.5, 12.5, CurtainPanelAreaValueSource.HostAreaFallback, "NonFiniteParameter");
    AssertCurtainPanelArea(true, double.PositiveInfinity, 12.5, 12.5, CurtainPanelAreaValueSource.HostAreaFallback, "NonFiniteParameter");
    AssertCurtainPanelArea(true, double.NegativeInfinity, 12.5, 12.5, CurtainPanelAreaValueSource.HostAreaFallback, "NonFiniteParameter");
}

private static void AssertCurtainPanelArea(
    bool useParameter,
    double? parameterArea,
    double hostArea,
    double expectedArea,
    CurtainPanelAreaValueSource expectedSource,
    string expectedReason)
{
    CurtainPanelAreaResult result = CurtainPanelAreaCalculator.Resolve(useParameter, parameterArea, hostArea);
    if (Math.Abs(result.Area - expectedArea) > 1e-9
        || result.Source != expectedSource
        || result.FallbackReason != expectedReason)
        throw new InvalidOperationException("Unexpected curtain panel area result.");
}

private static void AssertCurtainPanelAreaUsageSummaryDeduplicatesPanels()
{
    var summary = new CurtainPanelAreaUsageSummary();
    summary.Register("doc-a|panel-1", CurtainPanelAreaValueSource.Parameter);
    summary.Register("doc-a|panel-1", CurtainPanelAreaValueSource.Parameter);
    summary.Register("doc-b|panel-1", CurtainPanelAreaValueSource.HostAreaFallback);
    summary.Register("doc-c|panel-2", CurtainPanelAreaValueSource.HostArea);
    if (summary.ParameterCount != 1 || summary.HostAreaFallbackCount != 1)
        throw new InvalidOperationException("Curtain panel summary must deduplicate by document and UniqueId.");
}
```

- [ ] **Step 2: Запустить тест и подтвердить RED**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: FAIL, потому что `CurtainPanelAreaCalculation.cs` или его типы ещё отсутствуют.

- [ ] **Step 3: Реализовать минимальный чистый расчёт**

Создать `CurtainPanelAreaCalculation.cs` с `CurtainPanelAreaValueSource { HostArea, Parameter, HostAreaFallback }`, `CurtainPanelAreaResult`, `CurtainPanelAreaCalculator.Resolve(bool useParameter, double? parameterArea, double hostArea)` и `CurtainPanelAreaUsageSummary`. Логика `Resolve`: выключено → `HostArea`; null → fallback `MissingParameter`; NaN/Infinity → `NonFiniteParameter`; `<= 0` → `NonPositiveParameter`; иначе `Parameter`. Summary хранит `HashSet<string>(StringComparer.Ordinal)` и считает только `Parameter` и `HostAreaFallback`.

- [ ] **Step 4: Запустить harness и подтвердить GREEN**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: exit code 0.

- [ ] **Step 5: Закоммитить**

```powershell
git add CardinalDirectionGlazing/CurtainPanelAreaCalculation.cs CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Добавить расчет площади витражной панели"
```

### Task 2: Идентичность, каталог и восстановление параметра

**Files:**
- Create: `CardinalDirectionGlazing/CurtainPanelAreaParameterOption.cs`
- Create: `CardinalDirectionGlazing/CurtainPanelAreaParameterCatalogBuilder.cs`
- Create: `CardinalDirectionGlazing/CurtainPanelAreaParameterSelection.cs`
- Modify: `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Написать падающие тесты строгого GUID и фильтрации**

Подключить три новых чистых файла к test csproj. В `Main()` вызвать `AssertCurtainPanelParameterSelection()` и `AssertCurtainPanelCatalogBuilder()`. Тест выбора должен создать два `CurtainPanelAreaParameterOption` с одинаковыми именем/областью и разными GUID, проверить восстановление второго по GUID, null для отсутствующего GUID, восстановление необщего варианта только по имени+области, а также причины `InvalidDataType`, `InvalidStorageType` и `MissingParameter` в `CurtainPanelAreaParameterValueSelector.TrySelect`.

Тест каталога должен передать наблюдения current/linked и проверить:

```csharp
var observations = new[]
{
    new CurtainPanelAreaParameterObservation { DocumentKey = "Current", Name = "Area", Scope = CurtainPanelAreaParameterScope.Instance, SharedGuid = "11111111-1111-1111-1111-111111111111", IsArea = true, IsDouble = true },
    new CurtainPanelAreaParameterObservation { DocumentKey = "Linked", Name = "Area", Scope = CurtainPanelAreaParameterScope.Instance, SharedGuid = "22222222-2222-2222-2222-222222222222", IsArea = true, IsDouble = true },
    new CurtainPanelAreaParameterObservation { DocumentKey = "Duplicate", Name = "Area", Scope = CurtainPanelAreaParameterScope.Instance, SharedGuid = "22222222-2222-2222-2222-222222222222", IsArea = true, IsDouble = true },
    new CurtainPanelAreaParameterObservation { DocumentKey = "Named", Name = "Area", Scope = CurtainPanelAreaParameterScope.Type, IsArea = true, IsDouble = true },
    new CurtainPanelAreaParameterObservation { DocumentKey = "Length", Name = "Wrong", Scope = CurtainPanelAreaParameterScope.Type, IsArea = false, IsDouble = true },
    new CurtainPanelAreaParameterObservation { DocumentKey = "Text", Name = "Wrong", Scope = CurtainPanelAreaParameterScope.Type, IsArea = true, IsDouble = false }
};
IReadOnlyList<CurtainPanelAreaParameterOption> options = CurtainPanelAreaParameterCatalogBuilder.Build(observations);
if (options.Count != 3)
    throw new InvalidOperationException("Panel catalog must keep GUID identity and reject non-area/non-double parameters.");
```

- [ ] **Step 2: Запустить тест и подтвердить RED**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: compiler errors for missing curtain-panel parameter types.

- [ ] **Step 3: Реализовать модели без изменения оконных классов**

Создать панельные аналоги существующих чистых оконных классов:

```csharp
public enum CurtainPanelAreaParameterScope { Instance, Type }

public sealed class CurtainPanelAreaParameterOption
{
    public string Name { get; set; } = string.Empty;
    public CurtainPanelAreaParameterScope Scope { get; set; }
    public string SharedGuid { get; set; } = string.Empty;
    public string DisplayName => Name + (Scope == CurtainPanelAreaParameterScope.Instance ? " (экземпляр)" : " (тип)");
}
```

`CurtainPanelAreaParameterCatalogBuilder` должен нормализовать валидный GUID в формат `D`, строить ключ `scope|Shared|guid` либо `scope|Named|name`, отбрасывать не-Area/не-Double/пустое имя и сортировать по имени, затем области. `CurtainPanelAreaParameterSelection.Restore` для непустого сохранённого GUID ищет только GUID+scope; для пустого GUID — только необщий вариант name+scope. `CurtainPanelAreaParameterValueSelector` повторно проверяет GUID, `IsArea` и `IsDouble`, не разрешая name fallback общему параметру.

- [ ] **Step 4: Запустить harness и подтвердить GREEN**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: exit code 0; существующие оконные тесты тоже проходят.

- [ ] **Step 5: Закоммитить**

```powershell
git add CardinalDirectionGlazing/CurtainPanelAreaParameterOption.cs CardinalDirectionGlazing/CurtainPanelAreaParameterCatalogBuilder.cs CardinalDirectionGlazing/CurtainPanelAreaParameterSelection.cs CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Добавить идентификацию параметра площади панелей"
```

### Task 3: Настройки и независимая строка WPF

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingSettings.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Написать падающие XML/UI-тесты**

Расширить legacy round-trip проверкой, что старый XML даёт `UseCurtainPanelAreaParameter == false` и пустые строки. Добавить round-trip объекта:

```csharp
UseCurtainPanelAreaParameter = true,
CurtainPanelAreaParameterName = "Площадь стекла",
CurtainPanelAreaParameterScope = "Type",
CurtainPanelAreaParameterGuid = "33333333-3333-3333-3333-333333333333"
```

Добавить `AssertCurtainPanelAreaParameterControlsExist()` с маркерами `checkBox_CurtainPanelAreaFromParameter`, `Content="Витражные панели"`, `comboBox_CurtainPanelAreaParameter` и binding `ElementName=checkBox_CurtainPanelAreaFromParameter`.

- [ ] **Step 2: Запустить тест и подтвердить RED**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: compiler error по новым settings properties или runtime failure по отсутствующим XAML-маркерам.

- [ ] **Step 3: Добавить обратносуместимые свойства настроек**

В `CardinalDirectionGlazingSettings` добавить:

```csharp
public bool UseCurtainPanelAreaParameter { get; set; }
public string CurtainPanelAreaParameterName { get; set; } = string.Empty;
public string CurtainPanelAreaParameterScope { get; set; } = string.Empty;
public string CurtainPanelAreaParameterGuid { get; set; } = string.Empty;
```

- [ ] **Step 4: Добавить вторую строку UI и её поток состояния**

В Grid группы площади добавить две строки; существующим оконным контролам назначить `Grid.Row="0"`, а в `Grid.Row="1"` добавить checkbox `Витражные панели` и ComboBox с `DisplayMemberPath="DisplayName"`, включаемый через binding на checkbox.

Расширить конструктор WPF третьим аргументом `IReadOnlyList<CurtainPanelAreaParameterOption>`, сохранить список, назначить `ItemsSource`. Добавить свойства `UseCurtainPanelAreaParameter` и `SelectedCurtainPanelAreaParameter`, метод восстановления через `CurtainPanelAreaParameterSelection.Restore`, проверку обязательного выбора с текстом `Выберите параметр площади витражных панелей.`, и сохранение всех четырёх полей.

- [ ] **Step 5: Запустить harness и сборку R2026**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Run: `dotnet build CardinalDirectionGlazing/CardinalDirectionGlazing.csproj -c R2026`

Expected: обе команды завершаются с exit code 0.

- [ ] **Step 6: Закоммитить**

```powershell
git add CardinalDirectionGlazing/CardinalDirectionGlazingSettings.cs CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml.cs CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Добавить выбор параметра площади панелей в интерфейс"
```

### Task 4: Единая классификация и Revit-каталог принятых панелей

**Files:**
- Create: `CardinalDirectionGlazing/CurtainGridFillGlazingClassifier.cs`
- Create: `CardinalDirectionGlazing/CurtainPanelAreaParameterCatalog.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Написать падающую структурную регрессию**

Добавить проверку исходников, требующую вызовы `CurtainGridFillGlazingClassifier.IsGlazing` одновременно из command и catalog, фильтр `fill is Panel`, `CurtainGrid.GetPanelIds`, `StorageType.Double`, `SpecTypeId.Area` и старую ветку `ParameterType.Area`. Проверка также должна запрещать использование `CurtainPanelAreaParameterReader` для `Wall` до Task 5.

- [ ] **Step 2: Запустить тест и подтвердить RED**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: missing classifier/catalog integration marker.

- [ ] **Step 3: Вынести классификацию без изменения условий**

Перенести тело `IsCurtainGridFillGlazing` в `CurtainGridFillGlazingClassifier.IsGlazing(Element, SourceTrace?)` с тем же порядком:

1. `Panel.Symbol.CURTAIN_WALL_PANELS_CONSTRUCTION_TYPE`;
2. `Panel.Symbol.ALL_MODEL_MODEL`;
3. `Panel.FindHostPanel()` и `ALL_MODEL_MODEL` типа хоста;
4. для runtime-`Wall` — `WallType.ALL_MODEL_MODEL`;
5. иначе `UnsupportedFillType`.

Маркер принимает с trim и без регистра только `Остекление` или `Остекления`. Сохранить существующие reason codes и trace details. В command оставить делегирующий метод:

```csharp
private bool IsCurtainGridFillGlazing(Element fill, SourceTrace sourceTrace = null)
{
    return CurtainGridFillGlazingClassifier.IsGlazing(fill, sourceTrace);
}
```

- [ ] **Step 4: Реализовать каталог текущего и связанных документов**

`CurtainPanelAreaParameterCatalog.Collect` формирует уникальный список non-null документов, обходит `OST_Walls`/`Wall` с `CurtainGrid`, затем уникальные `GetPanelIds()`. Элемент допускается только при `fill is Panel panel && CurtainGridFillGlazingClassifier.IsGlazing(panel)`. Экземпляры дедуплицируются в пределах документа по `ElementId`, типы сканируются один раз по `ElementId`. `AddParameters` принимает только `Definition != null`, `StorageType.Double` и Area, безопасно извлекает GUID общего параметра. Ошибка одного wall/grid/panel/parameter ловится на минимальном уровне и не прекращает сбор остальных документов.

- [ ] **Step 5: Запустить harness и сборку R2026**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Run: `dotnet build CardinalDirectionGlazing/CardinalDirectionGlazing.csproj -c R2026`

Expected: exit code 0; classification path in command still delegates to identical logic.

- [ ] **Step 6: Закоммитить**

```powershell
git add CardinalDirectionGlazing/CurtainGridFillGlazingClassifier.cs CardinalDirectionGlazing/CurtainPanelAreaParameterCatalog.cs CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Собирать параметры только остекленных панелей"
```

### Task 5: Чтение Revit-параметра панели

**Files:**
- Create: `CardinalDirectionGlazing/CurtainPanelAreaParameterReader.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Написать падающую проверку адаптера**

Добавить структурную проверку, что reader выбирает `panel` для `Instance`, `panel.Symbol` для `Type`, использует `get_Parameter(guid)` только для общего параметра, `GetParameters(option.Name)` только для необщего, повторно формирует кандидата с `StorageType.Double` и Area, и содержит обе условно-компилируемые проверки API.

- [ ] **Step 2: Запустить test harness и подтвердить RED**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: missing reader file/integration markers.

- [ ] **Step 3: Реализовать безопасный reader**

Создать `TryRead(Panel panel, CurtainPanelAreaParameterOption option, out double value, out string reason)`. Null panel/option/type, невалидный GUID, отсутствующий параметр и API-ошибка возвращают false с `MissingParameter` либо `ApiException`. Для общего параметра передать точный кандидат в `CurtainPanelAreaParameterValueSelector`; для необщего — все одноимённые кандидаты. `CreateCandidate` повторно проверяет Area и Double до `AsDouble()`.

- [ ] **Step 4: Собрать крайние API-конфигурации**

Run: `dotnet build CardinalDirectionGlazing/CardinalDirectionGlazing.csproj -c R2019`

Run: `dotnet build CardinalDirectionGlazing/CardinalDirectionGlazing.csproj -c R2026`

Expected: exit code 0 для старой и новой веток Revit API.

- [ ] **Step 5: Закоммитить**

```powershell
git add CardinalDirectionGlazing/CurtainPanelAreaParameterReader.cs CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Читать параметр площади витражной панели"
```

### Task 6: Сквозной поток до итоговых площадей по сторонам света

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Написать падающую интеграционную регрессию**

Проверка исходника должна требовать все маркеры: `CurtainPanelAreaParameterCatalog.Collect`, передачу каталога в WPF, `SelectedCurtainPanelAreaParameter`, `CurtainPanelAreaParameterReader.TryRead`, `CurtainPanelAreaCalculator.Resolve`, передачу `areaResult.Area` в `UpdateWindowAreas`, регистрацию summary только внутри `if (areaCounted)`, и строки статистики `Площадь витражных панелей из параметра`/`HOST_AREA_COMPUTED`. Отдельно проверить наличие ветки `if (fill is Panel panel)` и сохранение `double hostArea = GetFillHostArea(fill)` вне неё.

- [ ] **Step 2: Запустить harness и подтвердить RED**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: missing end-to-end integration markers.

- [ ] **Step 3: Подключить каталог и выбранную настройку**

В `Execute` собрать `CurtainPanelAreaParameterCatalog.Collect(doc, revitLinkInstanceList)`, передать в WPF, после подтверждения получить nullable selected option и создать `CurtainPanelAreaUsageSummary`. Передать оба объекта в оба вызова `ProcessCurtainWallFills` — current и linked.

- [ ] **Step 4: Подменять площадь только для runtime-Panel**

Добавить `GetCurtainPanelArea(Panel panel, double hostArea, CurtainPanelAreaParameterOption? selectedOption, SourceTrace?)`, который читает параметр, вызывает `CurtainPanelAreaCalculator.Resolve`, при reader failure сохраняет точную причину fallback и пишет trace details: host area, name, scope, GUID, value, source, reason, final area.

В `ProcessCurtainWallFills` оставить `hostArea = GetFillHostArea(fill)`. Для `Panel` получить `areaResult`; для `Wall` оставить `area = hostArea` без reader/calculator/summary. Оба существующих вызова `UpdateWindowAreas` должны получать окончательную `area`. Их bool-результат сохраняется; summary регистрируется только когда `areaCounted && fill is Panel`, с ключом `RuntimeHelpers.GetHashCode(fill.Document) + "|" + fill.UniqueId`.

- [ ] **Step 5: Сформировать одно итоговое сообщение**

Собрать список строк: оконные две строки добавлять только при выбранном оконном параметре, панельные две строки — только при выбранном параметре панелей. Показывать один `TaskDialog`, если список не пуст, не меняя значения счётчиков окон.

- [ ] **Step 6: Запустить harness и R2026**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Run: `dotnet build CardinalDirectionGlazing/CardinalDirectionGlazing.csproj -c R2026`

Expected: exit code 0; все прежние тесты окон и направлений проходят.

- [ ] **Step 7: Закоммитить**

```powershell
git add CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Применить параметр площади к витражным панелям"
```

### Task 7: Полная регрессия и совместимость R2019–R2026

**Files:**
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Modify: `docs/superpowers/plans/2026-08-18-curtain-panel-area-parameter.md`

- [ ] **Step 1: Усилить регрессионные проверки границ**

Добавить проверки, что:

- `GetWindowArea` по-прежнему использует `WindowAreaParameterReader` и `WindowAreaCalculator`;
- `GetFillHostArea` по-прежнему читает `HOST_AREA_COMPUTED`;
- basic-wall путь не получает `CurtainPanelAreaParameterOption`;
- runtime-`Wall` ветка `ProcessCurtainWallFills` не вызывает reader;
- catalog и calculation используют один classifier;
- summary регистрируется после успешного `UpdateWindowAreas`, а не до него;
- XAML сохраняет независимые оконный и панельный checkbox/ComboBox.

- [ ] **Step 2: Запустить весь test harness**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Expected: exit code 0.

- [ ] **Step 3: Собрать каждую конфигурацию отдельно**

```powershell
$configs = 'R2019','R2020','R2021','R2022','R2023','R2024','R2025','R2026'
foreach ($config in $configs) {
    dotnet build CardinalDirectionGlazing/CardinalDirectionGlazing.csproj -c $config
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $config" }
}
```

Expected: восемь успешных сборок, по одной для R2019–R2026.

- [ ] **Step 4: Проверить артефакты и рабочее дерево**

Run: `Get-ChildItem CardinalDirectionGlazing/bin/R2019,CardinalDirectionGlazing/bin/R2020,CardinalDirectionGlazing/bin/R2021,CardinalDirectionGlazing/bin/R2022,CardinalDirectionGlazing/bin/R2023,CardinalDirectionGlazing/bin/R2024,CardinalDirectionGlazing/bin/R2025,CardinalDirectionGlazing/bin/R2026 -Filter CardinalDirectionGlazing.dll`

Run: `git status --short`

Expected: DLL присутствует во всех восьми папках; в status нет неожиданных отслеживаемых файлов.

- [ ] **Step 5: Закоммитить финальные тесты**

```powershell
git add CardinalDirectionGlazing.Tests/Program.cs docs/superpowers/plans/2026-08-18-curtain-panel-area-parameter.md
git commit -m "Проверить регрессию площади витражных панелей"
```

### Task 8: Независимое ревью и публикационный gate

**Files:**
- Review only: complete diff from `537f4b4` to feature HEAD

- [ ] **Step 1: Передать независимому reviewer полный diff и спецификацию**

Reviewer должен проверить фактический поток parameter → `CurtainPanelAreaResult.Area` → `UpdateWindowAreas` → directional totals → запись GUID-параметров, linked document/type lookup, строгий GUID, runtime-`Wall` isolation, null paths, settings/UI и Revit 2019–2026 conditionals.

- [ ] **Step 2: Исправить каждое подтверждённое замечание через RED/GREEN**

Для каждого дефекта сначала добавить воспроизводящий тест в `Program.cs`, запустить его до FAIL, затем внести минимальное исправление и повторить harness плюс затронутые сборки.

- [ ] **Step 3: Повторить финальную верификацию после review**

Run: `dotnet run --project CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

Run: восемь сборок из Task 7 Step 3.

Expected: тесты и R2019–R2026 проходят после последнего изменения. Только после этого ветка может быть слита и отправлена по отдельному подтверждённому публикационному шагу.
