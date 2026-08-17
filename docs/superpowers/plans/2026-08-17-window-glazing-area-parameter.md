# Window Glazing Area Parameter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional window-only area source that reads a user-selected area parameter and falls back to the existing height × width calculation when the value is unavailable or invalid.

**Architecture:** Keep parameter discovery and Revit element access in small Revit-aware classes, while putting source selection and usage counting in a Revit-independent core covered by console regression tests. The existing WPF dialog receives a catalog of window area parameters, persists the selected instance/type option, and passes it to the command. `CardinalDirectionGlazingCommand` continues to own spatial association and directional totals, but delegates value validation to the new core and reports deduplicated usage statistics after the transaction commits.

**Tech Stack:** C#, WPF/XAML, Autodesk Revit API 2019–2026, .NET Framework 4.8, .NET 8, XML serialization, console regression executable.

---

### Task 1: Add the tested area-source decision core

**Files:**
- Create: `CardinalDirectionGlazing/WindowAreaCalculation.cs`
- Modify: `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Write failing tests for parameter selection and fallback.**

Add these calls to `Main`:

```csharp
AssertWindowAreaResolution();
AssertWindowAreaUsageSummaryDeduplicatesWindows();
```

Add these test methods:

```csharp
private static void AssertWindowAreaResolution()
{
    AssertAreaResult(false, 10.8, 12.5, 12.5, WindowAreaValueSource.Dimensions, string.Empty);
    AssertAreaResult(true, 10.8, 12.5, 10.8, WindowAreaValueSource.Parameter, string.Empty);
    AssertAreaResult(true, null, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "MissingParameter");
    AssertAreaResult(true, 0.0, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonPositiveParameter");
    AssertAreaResult(true, -1.0, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonPositiveParameter");
    AssertAreaResult(true, double.NaN, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonFiniteParameter");
}

private static void AssertAreaResult(
    bool useParameter,
    double? parameterArea,
    double dimensionsArea,
    double expectedArea,
    WindowAreaValueSource expectedSource,
    string expectedReason)
{
    WindowAreaResult result = WindowAreaCalculator.Resolve(useParameter, parameterArea, dimensionsArea);
    if (Math.Abs(result.Area - expectedArea) > 1e-9
        || result.Source != expectedSource
        || !string.Equals(result.FallbackReason, expectedReason, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Unexpected area result: area={result.Area}, source={result.Source}, reason={result.FallbackReason}.");
    }
}

private static void AssertWindowAreaUsageSummaryDeduplicatesWindows()
{
    var summary = new WindowAreaUsageSummary();
    summary.Register("CurrentWindows|window-1", WindowAreaValueSource.Parameter);
    summary.Register("CurrentWindows|window-1", WindowAreaValueSource.Parameter);
    summary.Register("LinkedWindows|window-2", WindowAreaValueSource.DimensionsFallback);

    if (summary.ParameterCount != 1 || summary.DimensionsFallbackCount != 1)
    {
        throw new InvalidOperationException("Window area usage summary must count each source window once.");
    }
}
```

- [ ] **Step 2: Run the regression executable and verify RED.**

Run:

```powershell
dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj
```

Expected: compilation fails because `WindowAreaCalculator`, `WindowAreaResult`, `WindowAreaValueSource`, and `WindowAreaUsageSummary` do not exist.

- [ ] **Step 3: Add the pure calculation core and link it into the tests.**

Create `WindowAreaCalculation.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace CardinalDirectionGlazing
{
    public enum WindowAreaValueSource
    {
        Dimensions,
        Parameter,
        DimensionsFallback
    }

    public sealed class WindowAreaResult
    {
        public double Area { get; set; }
        public WindowAreaValueSource Source { get; set; }
        public string FallbackReason { get; set; } = string.Empty;
    }

    public static class WindowAreaCalculator
    {
        public static WindowAreaResult Resolve(bool useParameter, double? parameterArea, double dimensionsArea)
        {
            if (!useParameter)
                return Create(dimensionsArea, WindowAreaValueSource.Dimensions, string.Empty);
            if (!parameterArea.HasValue)
                return Create(dimensionsArea, WindowAreaValueSource.DimensionsFallback, "MissingParameter");
            if (double.IsNaN(parameterArea.Value) || double.IsInfinity(parameterArea.Value))
                return Create(dimensionsArea, WindowAreaValueSource.DimensionsFallback, "NonFiniteParameter");
            if (parameterArea.Value <= 0)
                return Create(dimensionsArea, WindowAreaValueSource.DimensionsFallback, "NonPositiveParameter");
            return Create(parameterArea.Value, WindowAreaValueSource.Parameter, string.Empty);
        }

        private static WindowAreaResult Create(double area, WindowAreaValueSource source, string reason)
        {
            return new WindowAreaResult { Area = area, Source = source, FallbackReason = reason };
        }
    }

    public sealed class WindowAreaUsageSummary
    {
        private readonly HashSet<string> _registeredKeys = new HashSet<string>(StringComparer.Ordinal);

        public int ParameterCount { get; private set; }
        public int DimensionsFallbackCount { get; private set; }

        public void Register(string sourceKey, WindowAreaValueSource source)
        {
            if (string.IsNullOrWhiteSpace(sourceKey) || !_registeredKeys.Add(sourceKey))
                return;
            if (source == WindowAreaValueSource.Parameter)
                ParameterCount++;
            else if (source == WindowAreaValueSource.DimensionsFallback)
                DimensionsFallbackCount++;
        }
    }
}
```

Add this item to the test project:

```xml
<Compile Include="..\CardinalDirectionGlazing\WindowAreaCalculation.cs" Link="WindowAreaCalculation.cs" />
```

- [ ] **Step 4: Run the regression executable and verify GREEN.**

Run the command from Step 2. Expected: exit code `0`.

- [ ] **Step 5: Commit the calculation core.**

```powershell
git add CardinalDirectionGlazing/WindowAreaCalculation.cs CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Добавить расчет площади окна с резервным значением"
```

### Task 2: Persist the window parameter selection compatibly

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingSettings.cs`
- Modify: `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Write failing old/new XML compatibility tests.**

Add `using System.IO;` and `using System.Xml.Serialization;`, call `AssertWindowAreaSettingsRoundTrip();` from `Main`, and add:

```csharp
private static void AssertWindowAreaSettingsRoundTrip()
{
    const string oldXml = "<CardinalDirectionGlazingSettings><SpacesForProcessingButtonName>radioButton_All</SpacesForProcessingButtonName></CardinalDirectionGlazingSettings>";
    var serializer = new XmlSerializer(typeof(CardinalDirectionGlazingSettings));
    CardinalDirectionGlazingSettings oldSettings;
    using (var reader = new StringReader(oldXml))
        oldSettings = (CardinalDirectionGlazingSettings)serializer.Deserialize(reader)!;

    if (oldSettings.UseWindowAreaParameter || oldSettings.WindowAreaParameterName.Length != 0)
        throw new InvalidOperationException("Legacy settings must keep dimension-based window areas.");

    var expected = new CardinalDirectionGlazingSettings
    {
        UseWindowAreaParameter = true,
        WindowAreaParameterName = "В_Площадь остекления",
        WindowAreaParameterScope = "Instance",
        WindowAreaParameterGuid = "820af414-f6ec-472d-887c-a2046a0c5988"
    };
    string xml;
    using (var writer = new StringWriter())
    {
        serializer.Serialize(writer, expected);
        xml = writer.ToString();
    }
    CardinalDirectionGlazingSettings actual;
    using (var reader = new StringReader(xml))
        actual = (CardinalDirectionGlazingSettings)serializer.Deserialize(reader)!;

    if (!actual.UseWindowAreaParameter
        || actual.WindowAreaParameterName != expected.WindowAreaParameterName
        || actual.WindowAreaParameterScope != expected.WindowAreaParameterScope
        || actual.WindowAreaParameterGuid != expected.WindowAreaParameterGuid)
        throw new InvalidOperationException("Window area parameter settings did not round-trip.");
}
```

- [ ] **Step 2: Run tests and verify RED.**

Run the regression executable. Expected: compilation fails because the four new settings properties do not exist.

- [ ] **Step 3: Add backward-compatible settings properties.**

Add to `CardinalDirectionGlazingSettings`:

```csharp
public bool UseWindowAreaParameter { get; set; }
public string WindowAreaParameterName { get; set; } = string.Empty;
public string WindowAreaParameterScope { get; set; } = string.Empty;
public string WindowAreaParameterGuid { get; set; } = string.Empty;
```

Link `CardinalDirectionGlazingSettings.cs` into the test project:

```xml
<Compile Include="..\CardinalDirectionGlazing\CardinalDirectionGlazingSettings.cs" Link="CardinalDirectionGlazingSettings.cs" />
```

- [ ] **Step 4: Run tests and verify GREEN.**

Run the regression executable. Expected: exit code `0`.

- [ ] **Step 5: Commit settings compatibility.**

```powershell
git add CardinalDirectionGlazing/CardinalDirectionGlazingSettings.cs CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Сохранять выбор параметра площади окон"
```

### Task 3: Discover compatible window parameters and add the UI row

**Files:**
- Create: `CardinalDirectionGlazing/WindowAreaParameterOption.cs`
- Create: `CardinalDirectionGlazing/WindowAreaParameterCatalog.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Add a failing XAML contract test.**

Call `AssertWindowAreaParameterControlsExist();` from `Main` and add:

```csharp
private static void AssertWindowAreaParameterControlsExist()
{
    string path = Path.Combine(Environment.CurrentDirectory, "CardinalDirectionGlazing", "CardinalDirectionGlazingWPF.xaml");
    string xaml = File.ReadAllText(path);
    string[] required =
    {
        "Header=\"Площадь остекления из параметра\"",
        "x:Name=\"checkBox_WindowAreaFromParameter\"",
        "Content=\"Окна\"",
        "x:Name=\"comboBox_WindowAreaParameter\""
    };
    foreach (string marker in required)
        if (!xaml.Contains(marker))
            throw new InvalidOperationException($"Window area UI marker is missing: {marker}");
}
```

- [ ] **Step 2: Run tests and verify RED.**

Run the regression executable. Expected: it fails with `Window area UI marker is missing`.

- [ ] **Step 3: Add the parameter option and Revit catalog.**

Create `WindowAreaParameterOption.cs`:

```csharp
namespace CardinalDirectionGlazing
{
    public enum WindowAreaParameterScope
    {
        Instance,
        Type
    }

    public sealed class WindowAreaParameterOption
    {
        public string Name { get; set; } = string.Empty;
        public WindowAreaParameterScope Scope { get; set; }
        public string SharedGuid { get; set; } = string.Empty;

        public string DisplayName => Name + (Scope == WindowAreaParameterScope.Instance
            ? " (экземпляр)"
            : " (тип)");
    }
}
```

Create `WindowAreaParameterCatalog.cs`:

```csharp
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    internal static class WindowAreaParameterCatalog
    {
        public static IReadOnlyList<WindowAreaParameterOption> Collect(
            Document currentDocument,
            IEnumerable<RevitLinkInstance> links)
        {
            var documents = new List<Document> { currentDocument };
            foreach (RevitLinkInstance link in links ?? Enumerable.Empty<RevitLinkInstance>())
            {
                Document linked = link?.GetLinkDocument();
                if (linked != null && !documents.Any(item => ReferenceEquals(item, linked)))
                    documents.Add(linked);
            }

            var options = new Dictionary<string, WindowAreaParameterOption>(StringComparer.OrdinalIgnoreCase);
            foreach (Document document in documents)
            {
                IEnumerable<FamilyInstance> windows = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .OfClass(typeof(FamilyInstance))
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>();

                foreach (FamilyInstance window in windows)
                {
                    AddParameters(window, WindowAreaParameterScope.Instance, options);
                    if (window.Symbol != null)
                        AddParameters(window.Symbol, WindowAreaParameterScope.Type, options);
                }
            }

            return options.Values
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Scope)
                .ToList();
        }

        private static void AddParameters(
            Element element,
            WindowAreaParameterScope scope,
            IDictionary<string, WindowAreaParameterOption> options)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                Definition definition = parameter?.Definition;
                if (parameter == null
                    || parameter.StorageType != StorageType.Double
                    || definition == null
                    || string.IsNullOrWhiteSpace(definition.Name)
                    || !IsAreaDefinition(definition))
                    continue;

                string key = scope + "|" + definition.Name;
                if (!options.TryGetValue(key, out WindowAreaParameterOption option))
                {
                    option = new WindowAreaParameterOption
                    {
                        Name = definition.Name,
                        Scope = scope
                    };
                    options.Add(key, option);
                }

                if (option.SharedGuid.Length == 0 && parameter.IsShared)
                {
                    try { option.SharedGuid = parameter.GUID.ToString("D"); }
                    catch (InvalidOperationException) { }
                }
            }
        }

        private static bool IsAreaDefinition(Definition definition)
        {
#if REVIT_2019 || REVIT_2020 || REVIT_2021
            return definition.ParameterType == ParameterType.Area;
#else
            return definition.GetDataType() == SpecTypeId.Area;
#endif
        }
    }
}
```

- [ ] **Step 4: Add the GroupBox and bind the options.**

Add a fourth outer grid row, place the new GroupBox in row `2`, move the action border to row `3`, and add:

```xml
<GroupBox Grid.Row="2"
          Header="Площадь остекления из параметра"
          Style="{StaticResource CompactGroupBox}">
    <Grid Margin="8,6,8,8">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="16"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        <CheckBox x:Name="checkBox_WindowAreaFromParameter"
                  Grid.Column="0"
                  Content="Окна"
                  VerticalAlignment="Center"/>
        <ComboBox x:Name="comboBox_WindowAreaParameter"
                  Grid.Column="2"
                  MinWidth="360"
                  DisplayMemberPath="DisplayName"
                  IsEnabled="{Binding IsChecked, ElementName=checkBox_WindowAreaFromParameter}"/>
    </Grid>
</GroupBox>
```

Add these members to `CardinalDirectionGlazingWPF.xaml.cs`:

```csharp
private readonly IReadOnlyList<WindowAreaParameterOption> _windowAreaParameters;
public bool UseWindowAreaParameter { get; private set; }
public WindowAreaParameterOption? SelectedWindowAreaParameter { get; private set; }
```

Change the constructor signature to:

```csharp
public CardinalDirectionGlazingWPF(
    List<RevitLinkInstance> revitLinkInstanceList,
    IReadOnlyList<WindowAreaParameterOption> windowAreaParameters)
```

Assign `_windowAreaParameters` immediately after `_revitLinkInstances`, assign the ComboBox source after `InitializeComponent`, and call the restore helper immediately after the existing settings restoration branch:

```csharp
_windowAreaParameters = windowAreaParameters ?? new List<WindowAreaParameterOption>();
comboBox_WindowAreaParameter.ItemsSource = _windowAreaParameters;
RestoreWindowAreaParameterSelection();
```

Add the helper:

```csharp
private void RestoreWindowAreaParameterSelection()
{
    if (CardinalDirectionGlazingSettingsItem?.UseWindowAreaParameter != true)
        return;
    checkBox_WindowAreaFromParameter.IsChecked = true;
    if (!Enum.TryParse(CardinalDirectionGlazingSettingsItem.WindowAreaParameterScope, out WindowAreaParameterScope scope))
        return;
    comboBox_WindowAreaParameter.SelectedItem = _windowAreaParameters.FirstOrDefault(item =>
        item.Scope == scope
        && string.Equals(item.Name, CardinalDirectionGlazingSettingsItem.WindowAreaParameterName, StringComparison.Ordinal));
}
```

Before the existing mode/link validation in `TryConfirm`, add:

```csharp
if (checkBox_WindowAreaFromParameter.IsChecked == true
    && comboBox_WindowAreaParameter.SelectedItem == null)
{
    MessageBox.Show(
        "Выберите параметр площади окон.",
        "Остекление по сторонам",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
    return false;
}
```

At the beginning of `SaveSettings`, assign:

```csharp
UseWindowAreaParameter = checkBox_WindowAreaFromParameter.IsChecked == true;
SelectedWindowAreaParameter = UseWindowAreaParameter
    ? comboBox_WindowAreaParameter.SelectedItem as WindowAreaParameterOption
    : null;
```

Include these properties in the settings initializer:

```csharp
UseWindowAreaParameter = UseWindowAreaParameter,
WindowAreaParameterName = SelectedWindowAreaParameter?.Name ?? string.Empty,
WindowAreaParameterScope = SelectedWindowAreaParameter?.Scope.ToString() ?? string.Empty,
WindowAreaParameterGuid = SelectedWindowAreaParameter?.SharedGuid ?? string.Empty
```

Before opening the window, create the catalog in the command:

```csharp
IReadOnlyList<WindowAreaParameterOption> windowAreaParameters =
    WindowAreaParameterCatalog.Collect(doc, revitLinkInstanceList);
var cardinalDirectionGlazingWPF =
    new CardinalDirectionGlazingWPF(revitLinkInstanceList, windowAreaParameters);
```

- [ ] **Step 5: Run tests and build one modern and one legacy configuration.**

Run:

```powershell
dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj
dotnet build .\CardinalDirectionGlazing.sln -c R2019 --no-restore
dotnet build .\CardinalDirectionGlazing.sln -c R2026 --no-restore
```

Expected: tests and both builds exit `0`.

- [ ] **Step 6: Commit parameter discovery and UI.**

```powershell
git add CardinalDirectionGlazing/WindowAreaParameterOption.cs CardinalDirectionGlazing/WindowAreaParameterCatalog.cs CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml CardinalDirectionGlazing/CardinalDirectionGlazingWPF.xaml.cs CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Добавить выбор параметра площади окон"
```

### Task 4: Read the selected value and integrate fallback statistics

**Files:**
- Create: `CardinalDirectionGlazing/WindowAreaParameterReader.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Add a failing source integration contract test.**

Call `AssertWindowAreaParameterIsIntegrated();` from `Main` and add:

```csharp
private static void AssertWindowAreaParameterIsIntegrated()
{
    string path = Path.Combine(Environment.CurrentDirectory, "CardinalDirectionGlazing", "CardinalDirectionGlazingCommand.cs");
    string source = File.ReadAllText(path);
    string[] required =
    {
        "WindowAreaParameterReader.TryRead",
        "WindowAreaCalculator.Resolve",
        "windowAreaUsageSummary.Register",
        "Площадь окон из параметра"
    };
    foreach (string marker in required)
        if (!source.Contains(marker))
            throw new InvalidOperationException($"Window area integration marker is missing: {marker}");
}
```

- [ ] **Step 2: Run tests and verify RED.**

Run the regression executable. Expected: it fails with `Window area integration marker is missing`.

- [ ] **Step 3: Implement the Revit parameter reader.**

Create `WindowAreaParameterReader.cs`:

```csharp
using Autodesk.Revit.DB;
using System;

namespace CardinalDirectionGlazing
{
    internal static class WindowAreaParameterReader
    {
        public static bool TryRead(
            FamilyInstance window,
            WindowAreaParameterOption option,
            out double value,
            out string reason)
        {
            value = 0;
            reason = string.Empty;
            Element source = option.Scope == WindowAreaParameterScope.Instance
                ? (Element)window
                : window.Symbol;
            if (source == null)
            {
                reason = "MissingParameter";
                return false;
            }

            Parameter parameter = null;
            if (Guid.TryParse(option.SharedGuid, out Guid guid))
                parameter = source.get_Parameter(guid);
            if (parameter == null)
                parameter = source.LookupParameter(option.Name);
            if (parameter == null)
            {
                reason = "MissingParameter";
                return false;
            }
            if (parameter.StorageType != StorageType.Double)
            {
                reason = "InvalidStorageType";
                return false;
            }

            value = parameter.AsDouble();
            return true;
        }
    }
}
```

- [ ] **Step 4: Integrate the selected source into window processing.**

Change `GetWindowArea` to return `WindowAreaResult`. Preserve the four current dimension reads and their product, call the reader only when an option is selected, and then call:

```csharp
WindowAreaResult result = WindowAreaCalculator.Resolve(
    selectedOption != null,
    parameterValue,
    dimensionsArea);
```

Write `parameterName`, `parameterScope`, `parameterValue`, `areaSource`, and `fallbackReason` into the existing `Area` trace step.

Create one `WindowAreaUsageSummary` before the transaction. Pass it and the selected option into both `ProcessWindows` calls. Change `UpdateWindowAreas` to return `bool`; return `false` for an invalid direction and `true` after adding the area to a bucket. When it returns `true`, register:

```csharp
windowAreaUsageSummary.Register(sourcePass + "|" + window.UniqueId, areaResult.Source);
```

After `t.Commit()`, show the summary only when parameter mode is enabled:

```csharp
TaskDialog.Show(
    "Остекление по сторонам",
    "Площадь окон из параметра: " + windowAreaUsageSummary.ParameterCount + Environment.NewLine
    + "Площадь окон по высоте × ширине: " + windowAreaUsageSummary.DimensionsFallbackCount);
```

- [ ] **Step 5: Run the full regression executable and verify GREEN.**

Run the regression executable. Expected: exit code `0` and no output.

- [ ] **Step 6: Commit runtime integration.**

```powershell
git add CardinalDirectionGlazing/WindowAreaParameterReader.cs CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs CardinalDirectionGlazing.Tests/Program.cs
git commit -m "Использовать параметр площади окон в расчете"
```

### Task 5: Verify every supported Revit build and publish the branch

**Files:**
- Modify only if verification exposes a defect in files changed by Tasks 1–4.

- [ ] **Step 1: Run all regression tests.**

```powershell
dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj
```

Expected: exit code `0`, no exceptions.

- [ ] **Step 2: Build Revit 2019–2026 sequentially against the matching API package.**

```powershell
$configurations = 2019..2026 | ForEach-Object { "R$_" }
foreach ($configuration in $configurations) {
    dotnet build .\CardinalDirectionGlazing.sln -c $configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $configuration" }
}
```

Expected: eight successful builds with zero errors.

- [ ] **Step 3: Check repository hygiene and final diff.**

```powershell
git diff --check
git status --short
git diff master...HEAD --stat
```

Expected: no whitespace errors; only intended source, test, spec, and plan changes are present. Generated build outputs remain ignored.

- [ ] **Step 4: Commit any verification-only corrections and push the feature branch.**

```powershell
git add CardinalDirectionGlazing CardinalDirectionGlazing.Tests docs/superpowers
git commit -m "Проверить площадь окон из параметра" # run only when Step 2 required corrections
git push -u origin codex/window-glazing-area-parameter
```

Expected: the remote branch points to the fully verified implementation.
