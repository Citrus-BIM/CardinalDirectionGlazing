# Glazing Calculation JSON Log Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce one complete, non-blocking JSON trace on the desktop for every glazing calculation run so an omitted or misclassified source can be diagnosed from the file alone.

**Architecture:** A Revit-independent `CalculationTrace` model records run, target, pass, element and decision data. `CalculationTraceWriter` serializes it using `DataContractJsonSerializer`, which is available on both .NET Framework 4.8 and .NET 8. The command creates one trace at startup, passes a target trace through the existing calculation methods, and records every observed branch without changing the calculation result.

**Tech Stack:** C#, Autodesk Revit API, .NET Framework 4.8, .NET 8, `System.Runtime.Serialization.Json`, console regression project.

---

### Task 1: Add a cross-target serializable trace model

**Files:**
- Create: `CardinalDirectionGlazing/CalculationTrace.cs`
- Modify: `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Write a failing serialization test.**

```csharp
CalculationTrace trace = new CalculationTrace("test-run");
TargetTrace target = trace.StartTarget("Space", 10, "space-uid", "101", "Test space");
SourceTrace source = target.StartSource("LinkedWindows", "Window", 20, "window-uid");
source.Complete("Skipped", "NoArea");

string json = CalculationTraceWriter.Serialize(trace);
if (!json.Contains("NoArea") || !json.Contains("window-uid"))
    throw new InvalidOperationException("Trace serialization lost source decision data.");
```

- [ ] **Step 2: Run the test and verify it fails because trace types do not exist.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

Expected: compilation failure naming `CalculationTrace`.

- [ ] **Step 3: Implement the DTOs and serializer.**

```csharp
[DataContract]
internal sealed class SourceTrace
{
    [DataMember] public string SourcePass { get; set; }
    [DataMember] public string SourceKind { get; set; }
    [DataMember] public int ElementId { get; set; }
    [DataMember] public string UniqueId { get; set; }
    [DataMember] public List<TraceStep> Steps { get; } = new();
    [DataMember] public string Outcome { get; private set; }
    [DataMember] public string Reason { get; private set; }
}
```

Use primitive `double`, `bool?`, `string`, `List<T>` and explicit `TracePoint`/`TraceVector` DTOs; do not serialize Revit API objects. `CalculationTraceWriter.Serialize` must use `DataContractJsonSerializer` and UTF-8.

- [ ] **Step 4: Link the new file into the console test project.**

```xml
<Compile Include="..\\CardinalDirectionGlazing\\CalculationTrace.cs" Link="CalculationTrace.cs" />
```

- [ ] **Step 5: Run the test and verify it passes.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

### Task 2: Capture run and target-level context

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs:22-430`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Add a failing test for the desktop filename format.**

```csharp
string path = CalculationTraceWriter.CreateDesktopPath(new DateTime(2026, 7, 13, 14, 30, 50, 123, DateTimeKind.Utc));
if (!path.EndsWith("CardinalDirectionGlazing_20260713_143050_123.json"))
    throw new InvalidOperationException("Trace filename is not deterministic.");
```

- [ ] **Step 2: Run the test and verify it fails because `CreateDesktopPath` does not exist.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

- [ ] **Step 3: Implement path creation and safe file writing.**

```csharp
public static string CreateDesktopPath(DateTime localNow) =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        $"CardinalDirectionGlazing_{localNow:yyyyMMdd_HHmmss_fff}.json");

public static bool TryWrite(CalculationTrace trace, out string path, out string error)
```

`TryWrite` catches only I/O and serialization exceptions, returns their message in `error`, and never throws into the Revit calculation path.

- [ ] **Step 4: Add trace initialization before the selection dialog and fill run metadata after the dialog.**

Record the execution result, host document title/path, mode, selected link instance ID/name, linked document title/path, transform basis/origin, true-north bases, source-collection counts and selected target count.

- [ ] **Step 5: Add a `TargetTrace` before `GetSolidFromElement`.**

Record target identity, target type, number/name, all eight parameter values before calculation, solid acquisition route/volume, and `NoTargetSolid` before the existing `continue` branch. Record all eight parameter write attempts, old/new values, read-only state and exceptions.

- [ ] **Step 6: In `finally`, write the trace and show only a write-failure TaskDialog.**

The successful run remains silent; write failure must not alter `Result`, transaction completion or parameter values.

### Task 3: Trace windows without changing their decisions

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs:485-725`

- [ ] **Step 1: Thread `TargetTrace` and a source-pass name through `ProcessWindows` and its three direction helpers.**

```csharp
private void ProcessWindows(..., TargetTrace targetTrace, string sourcePass, ref double windowsAreaNorth, ...)
```

- [ ] **Step 2: Record every window before its first skip.**

Record document identity, `SuperComponent`, area inputs and selected area; complete with `NoArea` when area is non-positive.

- [ ] **Step 3: Record all spatial-probe steps actually evaluated.**

For each 150/300/700 mm probe log front/back coordinates, `frontInside`, `backInside`, and the other Room/Space identity for the outside point. Record `SpatialProbe`, `InteriorOpening` or `NoSpatialAssociation` exactly when that branch decides the outcome.

- [ ] **Step 4: Record From/To calculation-point and fallback-ray branches.**

Record `HasSpatialElementFromToCalculationPoints`, transformed points, containment results, chosen exterior vector, fallback ray endpoints and `SegmentCount`.

- [ ] **Step 5: Record classification and outcome.**

Change `UpdateWindowAreas` to return a `DirectionTrace` containing accepted state, bucket, orientation, true-north bases and the bucket total before/after. Mark the source `Counted` only after the returned trace is accepted.

### Task 4: Trace curtain fills and glazing walls

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs:760-1190`

- [ ] **Step 1: Pass `TargetTrace` and source-pass name to `ProcessCurtainWallFills` and `ProcessGlazingBasicWalls`.**

- [ ] **Step 2: Record curtain-wall and fill filtering.**

For every curtain wall record `CurtainGrid` state and outer-curtain marker. For every panel ID record fill lookup, construction type, panel model group, host-panel lookup and host model group before deciding `NotGlazingMarker`.

- [ ] **Step 3: Record bounding-box route, area, probes and both rays.**

Make `TryGetFillBoundingBox` return its route (`ElementBoundingBox`, `HostBoundingBox`, `GeometryBoundingBox`, `NoBoundingBox`) together with the box. Record `HOST_AREA_COMPUTED`, transformed center/facing, probe trace, each ray endpoint/segment count and chosen exterior direction.

- [ ] **Step 4: Record the same decision sequence for basic glazing walls.**

Record curtain-grid exclusion, model-group marker, orientation, area, probes, both rays and classification outcome.

### Task 5: Verify all supported targets

**Files:**
- Modify: `CardinalDirectionGlazing/CalculationTrace.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Modify: `CardinalDirectionGlazing.Tests/CardinalDirectionGlazing.Tests.csproj`

- [ ] **Step 1: Run the console regressions.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

Expected: exit code 0.

- [ ] **Step 2: Build every supported Revit configuration.**

Run: `foreach ($c in 'R2019','R2020','R2021','R2022','R2023','R2024','R2025','R2026') { dotnet build .\\CardinalDirectionGlazing.sln -c $c -v:q; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }`

Expected: every configuration exits 0.

- [ ] **Step 3: Inspect whitespace and scope.**

Run: `git diff --check; git status --short`

Expected: no whitespace errors; only trace-related files changed.
