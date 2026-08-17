# Robust Glazing Direction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every valid horizontal glazing orientation count in exactly one cardinal bucket and make window-to-room fallback detection robust near curved or angled boundaries.

**Architecture:** `CardinalDirectionClassifier` remains a pure, Revit-independent classifier and always selects the highest dot-product bucket after XY-vector validation. `CardinalDirectionGlazingCommand` retains Revit calculation points as its primary association method, adds multi-distance spatial-point probing before the existing solid-ray fallback, and preserves exclusion of interior glazing.

**Tech Stack:** C#; .NET 8; Autodesk Revit API 2025–2027; console regression test project.

---

### Task 1: Lock in directional classification regressions

**Files:**
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Write failing tests for the two reported angles and boundary angles.**

```csharp
AssertBucket("south-at-21.29-degrees", Math.Sin(DegreesToRadians(21.29)), -Math.Cos(DegreesToRadians(21.29)), CardinalDirectionBucket.South);
AssertBucket("west-at-21.36-degrees", -Math.Cos(DegreesToRadians(21.36)), -Math.Sin(DegreesToRadians(21.36)), CardinalDirectionBucket.West);
AssertBucket("south-boundary-22.5-degrees", Math.Sin(DegreesToRadians(22.5)), -Math.Cos(DegreesToRadians(22.5)), CardinalDirectionBucket.South);
AssertBucket("west-boundary-22.5-degrees", -Math.Cos(DegreesToRadians(22.5)), -Math.Sin(DegreesToRadians(22.5)), CardinalDirectionBucket.West);
```

- [ ] **Step 2: Run the regression executable.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

Expected: the new tests expose the former rejection condition only after a non-orthogonal-but-valid input test is added, while the reported angles establish the required sector assignment.

### Task 2: Remove the classification rejection gap

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionClassifier.cs:18-71`
- Test: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Add a failing test for a valid orientation against slightly non-orthogonal supplied bases.**

```csharp
bool classified = CardinalDirectionClassifier.TryClassify(1, 0, 1, 0, 0.01, 1, out _);
if (!classified) throw new InvalidOperationException("A non-zero horizontal orientation must always be classified.");
```

- [ ] **Step 2: Run the executable and confirm the failure is due to the minimum cosine threshold.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

- [ ] **Step 3: Delete `MinimumMatchCosine` and the `bestDot` threshold return from `TryClassify`.**

```csharp
bucket = bestBucket;
return true;
```

- [ ] **Step 4: Run the executable and confirm all classifier tests pass.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

### Task 3: Strengthen room/space association fallback

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs:493-590`

- [ ] **Step 1: Insert a spatial-probe fallback before `TryGetWindowExteriorDirectionFromFallbackRay`.**

```csharp
if (TryGetWindowExteriorDirectionFromSpatialProbes(window, sourceToHostTransform, targetElement, hostDocument, out exteriorDirection))
    return true;

return TryGetWindowExteriorDirectionFromFallbackRay(window, sourceToHostTransform, targetSolid, out exteriorDirection);
```

- [ ] **Step 2: Implement probing at 150, 300, and 600 mm on both normal sides.**

```csharp
foreach (double distance in new[] { 150.0 / 304.8, 300.0 / 304.8, 600.0 / 304.8 })
{
    XYZ front = center + facing * distance;
    XYZ back = center - facing * distance;
    bool frontInside = IsPointInSpatialElement(targetElement, front);
    bool backInside = IsPointInSpatialElement(targetElement, back);
    if (frontInside == backInside) continue;
    XYZ outside = frontInside ? back : front;
    if (IsPointInAnotherSpatialElement(hostDocument, targetElement, outside)) return false;
    exteriorDirection = frontInside ? facing.Negate() : facing;
    return true;
}
```

- [ ] **Step 3: Build the R2026 target.**

Run: `dotnet build .\\CardinalDirectionGlazing.sln -c R2026`

Expected: exit code 0 with no compilation errors.

### Task 4: Final verification

**Files:**
- Modify: `CardinalDirectionGlazing/CardinalDirectionClassifier.cs`
- Modify: `CardinalDirectionGlazing/CardinalDirectionGlazingCommand.cs`
- Modify: `CardinalDirectionGlazing.Tests/Program.cs`

- [ ] **Step 1: Run classifier regressions.**

Run: `dotnet run --project .\\CardinalDirectionGlazing.Tests\\CardinalDirectionGlazing.Tests.csproj`

- [ ] **Step 2: Build R2025, R2026, and R2027 assemblies.**

Run: `dotnet build .\\CardinalDirectionGlazing.sln -c R2025; dotnet build .\\CardinalDirectionGlazing.sln -c R2026; dotnet build .\\CardinalDirectionGlazing.sln -c R2027`

- [ ] **Step 3: Inspect the diff and confirm no unrelated user changes are staged.**

Run: `git diff --check; git status --short`
