# Friendly Area Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Показывать в итоговой статистике фактические имена и область выбранных параметров площади, не выводя внутреннее имя `HOST_AREA_COMPUTED`.

**Architecture:** Добавить чистый formatter без зависимостей Revit API, который получает выбранные option-объекты и готовые summary-счётчики. Команда только передаёт существующие данные formatter-у и показывает непустой результат через прежний `TaskDialog`.

**Tech Stack:** C#, .NET Framework 4.8, Revit API 2019–2026, WPF, консольный regression harness на .NET 8.

---

### Task 1: Форматирование итоговой статистики

**Files:**
- Create: `CardinalDirectionGlazing/AreaUsageSummaryFormatter.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs:580-598`
- Modify: `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Write the failing test**

Добавить вызов `AssertFriendlyAreaUsageSummary()` в `Main`. В тесте создать instance-параметр окон и type-параметр панелей, зарегистрировать по одному использованию параметра и по одному fallback, вызвать:

```csharp
string actual = AreaUsageSummaryFormatter.Format(
    windowOption,
    windowSummary,
    panelOption,
    panelSummary);
```

и сравнить с точным текстом:

```text
Окна — параметр «Площадь окна» (экземпляр): 1
Окна — «Высота» × «Ширина»: 1

Витражные панели — параметр «Площадь стекла» (тип): 1
Витражные панели — системный параметр «Площадь»: 1
```

Также проверить, что результат не содержит `HOST_AREA_COMPUTED`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj`

Expected: FAIL на компиляции, потому что `AreaUsageSummaryFormatter` ещё не существует.

- [ ] **Step 3: Write minimal implementation**

Добавить `AreaUsageSummaryFormatter.Format`, который формирует `List<string>`, использует `DisplayName` выбранных option-объектов, сохраняет пустую строку между категориями и возвращает `string.Join(Environment.NewLine, lines)`.

Подключить новый файл в тестовый проект:

```xml
<Compile Include="..\CardinalDirectionGlazing\AreaUsageSummaryFormatter.cs" Link="AreaUsageSummaryFormatter.cs" />
```

В команде заменить ручное построение `areaSummaryLines` на:

```csharp
string areaSummary = AreaUsageSummaryFormatter.Format(
    selectedWindowAreaParameter,
    windowAreaUsageSummary,
    selectedCurtainPanelAreaParameter,
    curtainPanelAreaUsageSummary);
if (!string.IsNullOrEmpty(areaSummary))
    TaskDialog.Show("Остекление по сторонам", areaSummary);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj`

Expected: exit `0`.

- [ ] **Step 5: Commit**

```powershell
git add CardinalDirectionGlazing/AreaUsageSummaryFormatter.cs CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Показывать имена параметров в статистике"
```

### Task 2: Регрессия и публикация

**Files:**
- Verify only: all changed files and build outputs

- [ ] **Step 1: Run the complete harness**

Run: `dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj`

Expected: exit `0`.

- [ ] **Step 2: Restore and rebuild every supported configuration**

Для каждой конфигурации R2019–R2026 выполнить отдельные restore и build:

```powershell
foreach ($year in 2019..2026) {
    $configuration = "R$year"
    dotnet restore .\CardinalDirectionGlazing.sln -p:Configuration=$configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $configuration" }
    dotnet build .\CardinalDirectionGlazing.sln -c $configuration -t:Rebuild --no-restore --nologo --verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $configuration" }
}
```

Expected: восемь restore и восемь build с exit `0`; DLL существуют в папках `CardinalDirectionGlazing/bin/R2019/` — `CardinalDirectionGlazing/bin/R2026/`.

- [ ] **Step 3: Verify repository state**

Run: `git diff --check master...HEAD` and `git status --short --branch`.

Expected: diff-check exit `0`, рабочее дерево чистое.

- [ ] **Step 4: Independently review the diff**

Подтвердить, что изменилось только пользовательское формирование текста и тесты, а вызовы расчёта и статистической регистрации не изменились.

- [ ] **Step 5: Merge, verify merged master, and push**

После успешной проверки выполнить fast-forward `codex/friendly-area-summary` в `master`, повторно запустить harness и сборки R2019–R2026 в основном рабочем дереве, затем выполнить обычный non-force push веток в `origin`.
