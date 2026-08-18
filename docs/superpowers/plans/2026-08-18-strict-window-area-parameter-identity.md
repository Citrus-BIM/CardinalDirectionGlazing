# Strict Window Area Parameter Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent same-named shared parameters with different GUIDs from being confused, reduce parameter-catalog work, and cover the complete pure selection-to-area flow with regression tests.

**Architecture:** Shared parameters use `scope + GUID` identity and are never resolved by name when a GUID is saved. Non-shared parameters continue to use `scope + name`. The Revit adapter filters observations before allocation, excludes nested windows, and scans each window symbol once; the Revit-independent selector and calculator remain directly testable.

**Tech Stack:** C#, Autodesk Revit API 2019–2026, WPF, .NET Framework 4.8, .NET 8, console regression suite.

---

### Task 1: Lock strict shared-parameter identity with failing tests

**Files:**
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Replace the permissive restoration assertion with strict missing-GUID behavior**

In `AssertWindowAreaParameterRestorationUsesGuid`, keep the exact GUID assertion and replace the unavailable-GUID assertion with:

```csharp
const string unavailableGuid = "33333333-3333-3333-3333-333333333333";
WindowAreaParameterOption? unavailable = WindowAreaParameterSelection.Restore(
    new[] { first },
    first.Name,
    first.Scope.ToString(),
    unavailableGuid);
if (unavailable != null
    || first.SharedGuid != "11111111-1111-1111-1111-111111111111")
{
    throw new InvalidOperationException(
        "A missing saved shared GUID must not fall back to or mutate a same-named option.");
}
```

- [ ] **Step 2: Add a catalog regression for same name and different GUIDs**

Add `AssertWindowAreaCatalogKeepsDistinctSharedParameters()` and call it from `Main`:

```csharp
private static void AssertWindowAreaCatalogKeepsDistinctSharedParameters()
{
    var observations = new[]
    {
        new WindowAreaParameterObservation
        {
            DocumentKey = "Current",
            Name = "SameName",
            Scope = WindowAreaParameterScope.Instance,
            SharedGuid = "11111111-1111-1111-1111-111111111111",
            IsArea = true,
            IsDouble = true
        },
        new WindowAreaParameterObservation
        {
            DocumentKey = "Linked",
            Name = "SameName",
            Scope = WindowAreaParameterScope.Instance,
            SharedGuid = "22222222-2222-2222-2222-222222222222",
            IsArea = true,
            IsDouble = true
        },
        new WindowAreaParameterObservation
        {
            DocumentKey = "LinkedDuplicate",
            Name = "SameName",
            Scope = WindowAreaParameterScope.Instance,
            SharedGuid = "22222222-2222-2222-2222-222222222222",
            IsArea = true,
            IsDouble = true
        }
    };

    IReadOnlyList<WindowAreaParameterOption> options =
        WindowAreaParameterCatalogBuilder.Build(observations);
    if (options.Count != 2
        || !options.Any(item => item.SharedGuid == observations[0].SharedGuid)
        || !options.Any(item => item.SharedGuid == observations[1].SharedGuid))
    {
        throw new InvalidOperationException(
            "Same-named shared parameters with different GUIDs must remain distinct.");
    }
}
```

- [ ] **Step 3: Add a strict reader-selector regression**

Extend `AssertWindowAreaParameterValueSelectionRejectsWrongDataTypes` so an option with GUID `B`, no exact candidate, and a same-named candidate with GUID `A` returns `false` and `MissingParameter`:

```csharp
var wrongSharedIdentity = new WindowAreaParameterValueCandidate
{
    SharedGuid = "11111111-1111-1111-1111-111111111111",
    IsArea = true,
    IsDouble = true,
    Value = 99.0
};
if (WindowAreaParameterValueSelector.TrySelect(
        option,
        null,
        new[] { wrongSharedIdentity },
        out _,
        out string missingGuidReason)
    || missingGuidReason != "MissingParameter")
{
    throw new InvalidOperationException(
        "A shared option must not fall back to a same-named parameter with another GUID.");
}
```

- [ ] **Step 4: Run the suite and verify RED**

Run:

```powershell
dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj
```

Expected: FAIL because the catalog collapses the two GUIDs, restoration mutates the option, and `WindowAreaParameterValueCandidate.SharedGuid` does not exist.

### Task 2: Implement strict identity and reading

**Files:**
- Modify: `CardinalDirectionGlazing/WindowAreaParameterCatalogBuilder.cs`
- Modify: `CardinalDirectionGlazing/WindowAreaParameterSelection.cs`
- Modify: `CardinalDirectionGlazing/WindowAreaParameterReader.cs`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Key shared catalog entries by canonical GUID**

In `WindowAreaParameterCatalogBuilder.Build`, replace the name-only key with:

```csharp
string sharedGuid = NormalizeGuid(observation.SharedGuid);
string key = sharedGuid.Length > 0
    ? observation.Scope + "|Shared|" + sharedGuid
    : observation.Scope + "|Named|" + observation.Name;
```

Add:

```csharp
private static string NormalizeGuid(string value)
{
    return Guid.TryParse(value, out Guid guid)
        ? guid.ToString("D")
        : string.Empty;
}
```

Store the normalized GUID on the created option and remove the branch that copies a later GUID into a name-keyed option.

- [ ] **Step 2: Restore shared options only by GUID**

In `WindowAreaParameterSelection.Restore`, return the scope-matching exact GUID option when `savedGuid` is nonempty, otherwise return `null`. Only when `savedGuid` is empty may the method select a non-shared option by name:

```csharp
if (!string.IsNullOrWhiteSpace(savedGuid))
{
    return options.FirstOrDefault(item =>
        item.Scope == scope
        && string.Equals(item.SharedGuid, savedGuid, StringComparison.OrdinalIgnoreCase));
}

return options.FirstOrDefault(item =>
    item.Scope == scope
    && string.IsNullOrWhiteSpace(item.SharedGuid)
    && string.Equals(item.Name, savedName, StringComparison.Ordinal));
```

Do not mutate catalog options during restoration.

- [ ] **Step 3: Make candidate identity explicit**

Add this property to `WindowAreaParameterValueCandidate`:

```csharp
public string SharedGuid { get; set; } = string.Empty;
```

Update `WindowAreaParameterReader.CreateCandidate` to read `Parameter.GUID` only for shared parameters and assign the canonical string.

- [ ] **Step 4: Make selector fallback conditional on option identity**

At the start of `TrySelect`, after the null guard:

```csharp
if (!string.IsNullOrWhiteSpace(option.SharedGuid))
{
    if (exactGuidCandidate == null
        || !string.Equals(
            exactGuidCandidate.SharedGuid,
            option.SharedGuid,
            StringComparison.OrdinalIgnoreCase))
    {
        reason = "MissingParameter";
        return false;
    }

    return TryReadCandidate(exactGuidCandidate, out value, out reason);
}
```

For non-shared options, filter name candidates to those with an empty `SharedGuid` before data/storage validation.

- [ ] **Step 5: Avoid name lookup in the Revit adapter for shared options**

In `WindowAreaParameterReader.TryRead`, when `option.SharedGuid` is nonempty, parse and query the GUID. If parsing or lookup fails, return `MissingParameter`; otherwise pass only the exact candidate to the selector. Build `GetParameters(option.Name)` candidates only for a non-shared option.

- [ ] **Step 6: Run the suite and verify GREEN**

Run:

```powershell
dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj
```

Expected: exit code `0`.

### Task 3: Replace source-marker coverage with pure end-to-end value-flow coverage

**Files:**
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Add a failing pure flow test**

Add `AssertWindowAreaParameterSelectionFlowsIntoAreaCalculation()` and call it from `Main`. It must select a strict GUID candidate, feed the selected value to `WindowAreaCalculator.Resolve`, and assert the parameter area; it must also prove a missing exact GUID yields dimension fallback:

```csharp
private static void AssertWindowAreaParameterSelectionFlowsIntoAreaCalculation()
{
    var option = new WindowAreaParameterOption
    {
        Name = "SameName",
        Scope = WindowAreaParameterScope.Instance,
        SharedGuid = "22222222-2222-2222-2222-222222222222"
    };
    var exact = new WindowAreaParameterValueCandidate
    {
        SharedGuid = option.SharedGuid,
        IsArea = true,
        IsDouble = true,
        Value = 10.8
    };

    bool selected = WindowAreaParameterValueSelector.TrySelect(
        option, exact, Array.Empty<WindowAreaParameterValueCandidate>(),
        out double value, out _);
    WindowAreaResult parameterResult = WindowAreaCalculator.Resolve(
        true, selected ? value : null, 12.5);
    if (parameterResult.Source != WindowAreaValueSource.Parameter
        || Math.Abs(parameterResult.Area - 10.8) > 1e-9)
    {
        throw new InvalidOperationException(
            "The selected strict GUID value must flow into the calculated window area.");
    }

    bool missing = WindowAreaParameterValueSelector.TrySelect(
        option, null, new[] { exact }, out _, out _);
    WindowAreaResult fallbackResult = WindowAreaCalculator.Resolve(
        true, missing ? exact.Value : null, 12.5);
    if (fallbackResult.Source != WindowAreaValueSource.DimensionsFallback
        || Math.Abs(fallbackResult.Area - 12.5) > 1e-9)
    {
        throw new InvalidOperationException(
            "A missing exact GUID must flow into the dimensions fallback.");
    }
}
```

- [ ] **Step 2: Remove low-value source-marker tests**

Remove `AssertWindowAreaParameterIsIntegrated` and `AssertRevitWindowAreaAdaptersUseValidatedSelection`, plus their calls from `Main`. Keep the structural XAML contract test because runtime WPF automation is outside this console suite.

- [ ] **Step 3: Run the suite**

Run:

```powershell
dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj
```

Expected: exit code `0` with actual selector-to-calculator behavior covered.

### Task 4: Reduce Revit catalog work and align its window set

**Files:**
- Modify: `CardinalDirectionGlazing/WindowAreaParameterCatalog.cs`

- [ ] **Step 1: Filter the collector to processed top-level windows**

After `Cast<FamilyInstance>()`, add:

```csharp
.Where(window => window.SuperComponent == null)
```

- [ ] **Step 2: Scan each symbol once per document**

For each document, create `HashSet<ElementId>` collections for instance representatives and type parameters. For a window with a symbol, call `AddParameters` on the instance only when its symbol ID is first seen, and call it on the symbol only when that symbol ID is first seen. For a missing symbol, scan the instance safely and skip type parameters.

- [ ] **Step 3: Filter before observation allocation**

In `AddParameters`, before extracting GUID or constructing `WindowAreaParameterObservation`, require:

```csharp
if (parameter == null
    || definition == null
    || parameter.StorageType != StorageType.Double
    || !IsAreaDefinition(definition))
{
    continue;
}
```

Create observations only for valid area-double parameters.

- [ ] **Step 4: Build legacy and modern configurations**

Run:

```powershell
dotnet build .\CardinalDirectionGlazing.sln -c R2019 -t:Rebuild
dotnet build .\CardinalDirectionGlazing.sln -c R2026 -t:Rebuild
```

Expected: both exit `0`.

### Task 5: Full verification, independent review, integration, and publication

**Files:**
- Verify all changed source, tests, spec, and plan files.

- [ ] **Step 1: Run the complete console regression suite**

```powershell
dotnet run --project .\CardinalDirectionGlazing.Tests\CardinalDirectionGlazing.Tests.csproj
```

Expected: exit `0`.

- [ ] **Step 2: Rebuild every supported Revit configuration**

```powershell
foreach ($configuration in 'R2019','R2020','R2021','R2022','R2023','R2024','R2025','R2026') {
    dotnet build .\CardinalDirectionGlazing.sln -c $configuration -t:Rebuild
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

Expected: eight successful builds, zero errors.

- [ ] **Step 3: Run diff and worktree checks**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only intended changes are present.

- [ ] **Step 4: Request an independent review**

Provide the reviewer with the original base SHA, current HEAD, strict-GUID requirement, catalog-performance requirement, and instructions to run focused tests without modifying the worktree. Address every Critical and Important finding before proceeding.

- [ ] **Step 5: Commit the implementation**

```powershell
git add CardinalDirectionGlazing CardinalDirectionGlazing.Tests docs/superpowers
git commit -m "Исправить выбор параметра площади окон по GUID"
```

- [ ] **Step 6: Merge into master and push after final verification**

Verify master is clean and still at the expected lineage, merge `codex/window-glazing-area-parameter` into `master`, rerun the regression suite from master, and push `master` plus the feature branch to the configured remote.
