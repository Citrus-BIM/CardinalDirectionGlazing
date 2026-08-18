using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace CardinalDirectionGlazing.Tests;

internal static class Program
{
    private static void Main()
    {
        AssertBucket("north", 0, 1, CardinalDirectionBucket.North);
        AssertBucket("south", 0, -1, CardinalDirectionBucket.South);
        AssertBucket("east", 1, 0, CardinalDirectionBucket.East);
        AssertBucket("west", -1, 0, CardinalDirectionBucket.West);
        AssertBucket("northeast", 1, 1, CardinalDirectionBucket.Northeast);
        AssertBucket("northwest", -1, 1, CardinalDirectionBucket.Northwest);
        AssertBucket("southeast", 1, -1, CardinalDirectionBucket.Southeast);
        AssertBucket("southwest", -1, -1, CardinalDirectionBucket.Southwest);
        AssertBucket("south-at-21.29-degrees", Math.Sin(DegreesToRadians(21.29)), -Math.Cos(DegreesToRadians(21.29)), CardinalDirectionBucket.South);
        AssertBucket("west-at-21.36-degrees", -Math.Cos(DegreesToRadians(21.36)), -Math.Sin(DegreesToRadians(21.36)), CardinalDirectionBucket.West);
        AssertClassified("south-boundary-22.5-degrees", Math.Sin(DegreesToRadians(22.5)), -Math.Cos(DegreesToRadians(22.5)), 1, 0, 0, 1);
        AssertClassified("west-boundary-22.5-degrees", -Math.Cos(DegreesToRadians(22.5)), -Math.Sin(DegreesToRadians(22.5)), 1, 0, 0, 1);
        AssertClassified("valid-orientation-with-distorted-bases", 0, 1, 1, 0, 0.98, 0);
        AssertTraceSerializationIncludesSkippedWindow();
        AssertWindowTraceSerializesDiagnosticMetadata();
        AssertRootCollectionDiagnosticsSerializeOnce();
        AssertDesktopTracePathUsesStableTimestamp();
        AssertTraceWriteDoesNotOverwriteExistingFile();
        AssertWindowAreaResolution();
        AssertWindowAreaUsageSummaryDeduplicatesWindows();
        AssertCurtainPanelAreaResolution();
        AssertCurtainPanelAreaUsageSummaryDeduplicatesPanels();
        AssertCurtainPanelParameterSelection();
        AssertCurtainPanelCatalogBuilder();
        AssertWindowAreaSettingsRoundTrip();
        AssertCurtainPanelAreaSettingsRoundTrip();
        AssertWindowAreaParameterControlsExist();
        AssertCurtainPanelAreaParameterControlsExist();
        AssertCurtainPanelCatalogUsesCurrentGlazingClassifier();
        AssertCurtainPanelAreaParameterReaderGuardsIdentityAndDataType();
        AssertCurtainPanelAreaParameterFlowsToDirectionalTotals();
        AssertWindowAreaParameterIsIntegrated();
        AssertWindowAreaParameterRestorationUsesGuid();
        AssertWindowAreaParameterValueSelectionRejectsWrongDataTypes();
        AssertWindowAreaParameterSelectionFlowsIntoAreaCalculation();
        AssertWindowAreaCatalogCombinesCurrentAndLinkedObservations();
        AssertWindowAreaCatalogKeepsDistinctSharedParameters();

        if (CardinalDirectionClassifier.TryClassify(0, 0, 1, 0, 0, 1, out _))
        {
            throw new InvalidOperationException("Zero vector must not be classified.");
        }
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static void AssertTraceSerializationIncludesSkippedWindow()
    {
        var trace = new CalculationTrace("2026.1", "Rooms");
        TargetTrace target = trace.StartTarget("target-unique-id");
        SourceTrace source = target.StartSource("Window", "window-unique-id");
        source.Complete("Skipped", "NoArea");

        string json = Encoding.UTF8.GetString(CalculationTraceWriter.Serialize(trace));

        if (!json.Contains("NoArea") || !json.Contains("window-unique-id"))
        {
            throw new InvalidOperationException("Serialized trace must include the skipped reason and window unique id.");
        }
    }

    private static void AssertWindowTraceSerializesDiagnosticMetadata()
    {
        var trace = new CalculationTrace("2026.1", "Spaces");
        SourceTrace source = trace.StartTarget("space-uid").StartSource("Window", "window-uid");
        source.SourcePass = "LinkedWindows";
        source.Document = new DocumentTrace { Title = "AR" };
        source.SuperComponent = "host-panel-uid";
        TraceStep step = source.StartStep("Area");
        step.Details["roughHeight"] = "1.5";
        TraceStep fallback = source.StartStep("FallbackSolidRay");
        fallback.Complete("Skipped", "NoBoundingBox");
        fallback.Details["frontOtherSpatialLookupOutcome"] = "NotEvaluated";
        fallback.Details["frontOtherSpatialLookupReason"] = "EqualMembershipNoOutsidePoint";
        TraceStep probe = source.StartStep("SpatialProbe");
        probe.Complete("Skipped", "EqualSpatialMembership");
        trace.CollectionDiagnostics.Add(new CollectionDiagnosticTrace
        {
            SourcePass = "CurrentWindows",
            SourceType = "Window",
            Outcome = "Skipped",
            ReasonCode = "HasSuperComponent",
            SuperComponentElementId = "100",
            SuperComponentUniqueId = "host-window-uid"
        });

        string json = Encoding.UTF8.GetString(CalculationTraceWriter.Serialize(trace));

        if (!json.Contains("LinkedWindows") || !json.Contains("host-panel-uid") || !json.Contains("roughHeight") || !json.Contains("NoBoundingBox") || !json.Contains("EqualMembershipNoOutsidePoint") || !json.Contains("EqualSpatialMembership") || !json.Contains("HasSuperComponent") || !json.Contains("host-window-uid"))
        {
            throw new InvalidOperationException("Window trace must serialize its source pass, ownership and diagnostic values.");
        }
    }

    private static void AssertRootCollectionDiagnosticsSerializeOnce()
    {
        var trace = new CalculationTrace("2026.1", "Rooms");
        trace.CollectionDiagnostics.Add(new CollectionDiagnosticTrace
        {
            SourcePass = "CurrentGlazingWalls",
            ElementId = "42",
            UniqueId = "wall-uid",
            ReasonCode = "NotGlazingMarker"
        });

        string json = Encoding.UTF8.GetString(CalculationTraceWriter.Serialize(trace));
        if (!json.Contains("CurrentGlazingWalls") || !json.Contains("wall-uid") || !json.Contains("NotGlazingMarker"))
        {
            throw new InvalidOperationException("Root-level collection diagnostics must serialize independently of targets.");
        }
    }

    private static void AssertDesktopTracePathUsesStableTimestamp()
    {
        string path = CalculationTraceWriter.CreateDesktopPath(new DateTime(2026, 7, 13, 14, 30, 50));
        string fileName = System.IO.Path.GetFileName(path);

        if (!string.Equals(fileName, "CardinalDirectionGlazing_20260713_143050_000.json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected trace file name: '{fileName}'.");
        }
    }

    private static void AssertTraceWriteDoesNotOverwriteExistingFile()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CardinalDirectionGlazing.Tests", Guid.NewGuid().ToString("N"));
        string requestedPath = System.IO.Path.Combine(directory, "CardinalDirectionGlazing_20260713_143050_000.json");
        var trace = new CalculationTrace("2026.1", "Rooms");

        try
        {
            if (!CalculationTraceWriter.TryWrite(trace, requestedPath, out string firstPath, out string firstError))
            {
                throw new InvalidOperationException($"First trace write failed: {firstError}");
            }

            if (!CalculationTraceWriter.TryWrite(trace, requestedPath, out string secondPath, out string secondError))
            {
                throw new InvalidOperationException($"Second trace write failed: {secondError}");
            }

            if (string.Equals(firstPath, secondPath, StringComparison.Ordinal) || !System.IO.File.Exists(firstPath) || !System.IO.File.Exists(secondPath))
            {
                throw new InvalidOperationException("Repeated trace writes must create two distinct files.");
            }
        }
        finally
        {
            if (System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.Delete(directory, true);
            }
        }
    }

    private static void AssertWindowAreaResolution()
    {
        AssertAreaResult(false, 10.8, 12.5, 12.5, WindowAreaValueSource.Dimensions, string.Empty);
        AssertAreaResult(true, 10.8, 12.5, 10.8, WindowAreaValueSource.Parameter, string.Empty);
        AssertAreaResult(true, null, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "MissingParameter");
        AssertAreaResult(true, 0.0, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonPositiveParameter");
        AssertAreaResult(true, -1.0, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonPositiveParameter");
        AssertAreaResult(true, double.NaN, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonFiniteParameter");
        AssertAreaResult(true, double.PositiveInfinity, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonFiniteParameter");
        AssertAreaResult(true, double.NegativeInfinity, 12.5, 12.5, WindowAreaValueSource.DimensionsFallback, "NonFiniteParameter");
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
        {
            throw new InvalidOperationException("Unexpected curtain panel area result.");
        }
    }

    private static void AssertCurtainPanelAreaUsageSummaryDeduplicatesPanels()
    {
        var summary = new CurtainPanelAreaUsageSummary();
        summary.Register("doc-a|panel-1", CurtainPanelAreaValueSource.Parameter);
        summary.Register("doc-a|panel-1", CurtainPanelAreaValueSource.Parameter);
        summary.Register("doc-b|panel-1", CurtainPanelAreaValueSource.HostAreaFallback);
        summary.Register("doc-c|panel-2", CurtainPanelAreaValueSource.HostArea);

        if (summary.ParameterCount != 1 || summary.HostAreaFallbackCount != 1)
        {
            throw new InvalidOperationException("Curtain panel summary must deduplicate by document and UniqueId.");
        }
    }

    private static void AssertCurtainPanelParameterSelection()
    {
        var first = new CurtainPanelAreaParameterOption
        {
            Name = "Площадь стекла",
            Scope = CurtainPanelAreaParameterScope.Instance,
            SharedGuid = "11111111-1111-1111-1111-111111111111"
        };
        var second = new CurtainPanelAreaParameterOption
        {
            Name = first.Name,
            Scope = first.Scope,
            SharedGuid = "22222222-2222-2222-2222-222222222222"
        };
        var named = new CurtainPanelAreaParameterOption
        {
            Name = first.Name,
            Scope = CurtainPanelAreaParameterScope.Type
        };

        CurtainPanelAreaParameterOption? exact = CurtainPanelAreaParameterSelection.Restore(
            new[] { first, second, named },
            second.Name,
            second.Scope.ToString(),
            second.SharedGuid);
        if (!ReferenceEquals(exact, second))
            throw new InvalidOperationException("Curtain panel selection must restore a shared parameter by GUID.");

        CurtainPanelAreaParameterOption? unavailable = CurtainPanelAreaParameterSelection.Restore(
            new[] { first, named },
            first.Name,
            first.Scope.ToString(),
            "33333333-3333-3333-3333-333333333333");
        if (unavailable != null)
            throw new InvalidOperationException("A missing panel GUID must not fall back to the same name.");

        CurtainPanelAreaParameterOption? namedRestored = CurtainPanelAreaParameterSelection.Restore(
            new[] { first, named },
            named.Name,
            named.Scope.ToString(),
            string.Empty);
        if (!ReferenceEquals(namedRestored, named))
            throw new InvalidOperationException("A non-shared panel parameter must restore by name and scope.");

        var exactCandidate = new CurtainPanelAreaParameterValueCandidate
        {
            SharedGuid = second.SharedGuid,
            IsArea = true,
            IsDouble = true,
            Value = 10.8
        };
        if (!CurtainPanelAreaParameterValueSelector.TrySelect(
                second,
                exactCandidate,
                Array.Empty<CurtainPanelAreaParameterValueCandidate>(),
                out double exactValue,
                out string exactReason)
            || Math.Abs(exactValue - 10.8) > 1e-9
            || exactReason.Length != 0)
        {
            throw new InvalidOperationException("A valid exact panel GUID candidate must be selected.");
        }

        if (CurtainPanelAreaParameterValueSelector.TrySelect(
                second,
                null,
                new[] { new CurtainPanelAreaParameterValueCandidate { IsArea = true, IsDouble = true, Value = 99 } },
                out _,
                out string missingReason)
            || missingReason != "MissingParameter")
        {
            throw new InvalidOperationException("A shared panel option must not use name fallback.");
        }

        var namedLength = new CurtainPanelAreaParameterValueCandidate
        {
            IsArea = false,
            IsDouble = true,
            Value = 25
        };
        if (CurtainPanelAreaParameterValueSelector.TrySelect(
                named,
                null,
                new[] { namedLength },
                out _,
                out string dataTypeReason)
            || dataTypeReason != "InvalidDataType")
        {
            throw new InvalidOperationException("A panel parameter with a non-area data type must be rejected.");
        }

        var namedWrongStorage = new CurtainPanelAreaParameterValueCandidate
        {
            IsArea = true,
            IsDouble = false
        };
        if (CurtainPanelAreaParameterValueSelector.TrySelect(
                named,
                null,
                new[] { namedWrongStorage },
                out _,
                out string storageReason)
            || storageReason != "InvalidStorageType")
        {
            throw new InvalidOperationException("A panel area parameter with non-Double storage must be rejected.");
        }
    }

    private static void AssertCurtainPanelCatalogBuilder()
    {
        var observations = new[]
        {
            new CurtainPanelAreaParameterObservation { DocumentKey = "Current", Name = "Area", Scope = CurtainPanelAreaParameterScope.Instance, SharedGuid = "11111111-1111-1111-1111-111111111111", IsArea = true, IsDouble = true },
            new CurtainPanelAreaParameterObservation { DocumentKey = "Linked", Name = "Area", Scope = CurtainPanelAreaParameterScope.Instance, SharedGuid = "22222222-2222-2222-2222-222222222222", IsArea = true, IsDouble = true },
            new CurtainPanelAreaParameterObservation { DocumentKey = "Duplicate", Name = "Area", Scope = CurtainPanelAreaParameterScope.Instance, SharedGuid = "22222222-2222-2222-2222-222222222222", IsArea = true, IsDouble = true },
            new CurtainPanelAreaParameterObservation { DocumentKey = "Named", Name = "Area", Scope = CurtainPanelAreaParameterScope.Type, IsArea = true, IsDouble = true },
            new CurtainPanelAreaParameterObservation { DocumentKey = "Length", Name = "Wrong", Scope = CurtainPanelAreaParameterScope.Type, IsArea = false, IsDouble = true },
            new CurtainPanelAreaParameterObservation { DocumentKey = "Text", Name = "Wrong", Scope = CurtainPanelAreaParameterScope.Type, IsArea = true, IsDouble = false }
        };

        IReadOnlyList<CurtainPanelAreaParameterOption> options =
            CurtainPanelAreaParameterCatalogBuilder.Build(observations);
        if (options.Count != 3
            || !options.Any(item => item.SharedGuid == observations[0].SharedGuid)
            || !options.Any(item => item.SharedGuid == observations[1].SharedGuid)
            || !options.Any(item => item.Scope == CurtainPanelAreaParameterScope.Type && item.SharedGuid.Length == 0))
        {
            throw new InvalidOperationException(
                "Panel catalog must keep GUID identity and reject non-area/non-double parameters.");
        }
    }

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
        {
            throw new InvalidOperationException("Window area parameter settings did not round-trip.");
        }
    }

    private static void AssertCurtainPanelAreaSettingsRoundTrip()
    {
        const string oldXml = "<CardinalDirectionGlazingSettings><SpacesForProcessingButtonName>radioButton_All</SpacesForProcessingButtonName></CardinalDirectionGlazingSettings>";
        var serializer = new XmlSerializer(typeof(CardinalDirectionGlazingSettings));
        CardinalDirectionGlazingSettings oldSettings;
        using (var reader = new StringReader(oldXml))
            oldSettings = (CardinalDirectionGlazingSettings)serializer.Deserialize(reader)!;

        if (oldSettings.UseCurtainPanelAreaParameter
            || oldSettings.CurtainPanelAreaParameterName.Length != 0
            || oldSettings.CurtainPanelAreaParameterScope.Length != 0
            || oldSettings.CurtainPanelAreaParameterGuid.Length != 0)
        {
            throw new InvalidOperationException("Legacy settings must keep HOST_AREA_COMPUTED for curtain panels.");
        }

        var expected = new CardinalDirectionGlazingSettings
        {
            UseCurtainPanelAreaParameter = true,
            CurtainPanelAreaParameterName = "Площадь стекла",
            CurtainPanelAreaParameterScope = "Type",
            CurtainPanelAreaParameterGuid = "33333333-3333-3333-3333-333333333333"
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

        if (!actual.UseCurtainPanelAreaParameter
            || actual.CurtainPanelAreaParameterName != expected.CurtainPanelAreaParameterName
            || actual.CurtainPanelAreaParameterScope != expected.CurtainPanelAreaParameterScope
            || actual.CurtainPanelAreaParameterGuid != expected.CurtainPanelAreaParameterGuid)
        {
            throw new InvalidOperationException("Curtain panel area parameter settings did not round-trip.");
        }
    }

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
        {
            if (!xaml.Contains(marker))
                throw new InvalidOperationException($"Window area UI marker is missing: {marker}");
        }
    }

    private static void AssertCurtainPanelAreaParameterControlsExist()
    {
        string path = Path.Combine(Environment.CurrentDirectory, "CardinalDirectionGlazing", "CardinalDirectionGlazingWPF.xaml");
        string xaml = File.ReadAllText(path);
        string[] required =
        {
            "x:Name=\"checkBox_CurtainPanelAreaFromParameter\"",
            "Content=\"Витражные панели\"",
            "x:Name=\"comboBox_CurtainPanelAreaParameter\"",
            "ElementName=checkBox_CurtainPanelAreaFromParameter"
        };

        foreach (string marker in required)
        {
            if (!xaml.Contains(marker))
                throw new InvalidOperationException($"Curtain panel area UI marker is missing: {marker}");
        }
    }

    private static void AssertCurtainPanelCatalogUsesCurrentGlazingClassifier()
    {
        string project = Path.Combine(Environment.CurrentDirectory, "CardinalDirectionGlazing");
        string command = File.ReadAllText(Path.Combine(project, "CardinalDirectionGlazingCommand.cs"));
        string catalogPath = Path.Combine(project, "CurtainPanelAreaParameterCatalog.cs");
        string classifierPath = Path.Combine(project, "CurtainGridFillGlazingClassifier.cs");
        if (!File.Exists(catalogPath) || !File.Exists(classifierPath))
            throw new InvalidOperationException("Curtain panel catalog and classifier must exist.");

        string catalog = File.ReadAllText(catalogPath);
        string classifier = File.ReadAllText(classifierPath);
        string[] commandMarkers =
        {
            "CurtainGridFillGlazingClassifier.IsGlazing(fill, sourceTrace)"
        };
        string[] catalogMarkers =
        {
            "GetPanelIds()",
            "fill is Panel panel",
            "CurtainGridFillGlazingClassifier.IsGlazing(panel)",
            "StorageType.Double",
            "SpecTypeId.Area",
            "ParameterType.Area"
        };
        string[] classifierMarkers =
        {
            "CURTAIN_WALL_PANELS_CONSTRUCTION_TYPE",
            "FindHostPanel",
            "fill is Wall wall",
            "UnsupportedFillType"
        };

        foreach (string marker in commandMarkers)
            if (!command.Contains(marker))
                throw new InvalidOperationException($"Command classifier marker is missing: {marker}");
        foreach (string marker in catalogMarkers)
            if (!catalog.Contains(marker))
                throw new InvalidOperationException($"Panel catalog marker is missing: {marker}");
        foreach (string marker in classifierMarkers)
            if (!classifier.Contains(marker))
                throw new InvalidOperationException($"Panel classifier marker is missing: {marker}");
    }

    private static void AssertCurtainPanelAreaParameterReaderGuardsIdentityAndDataType()
    {
        string path = Path.Combine(
            Environment.CurrentDirectory,
            "CardinalDirectionGlazing",
            "CurtainPanelAreaParameterReader.cs");
        if (!File.Exists(path))
            throw new InvalidOperationException("Curtain panel parameter reader must exist.");

        string source = File.ReadAllText(path);
        string[] required =
        {
            "option.Scope == CurtainPanelAreaParameterScope.Instance",
            "panel.Symbol",
            "source.get_Parameter(guid)",
            "source.GetParameters(option.Name)",
            "CurtainPanelAreaParameterValueSelector.TrySelect",
            "StorageType.Double",
            "ParameterType.Area",
            "SpecTypeId.Area"
        };

        foreach (string marker in required)
        {
            if (!source.Contains(marker))
                throw new InvalidOperationException($"Curtain panel reader marker is missing: {marker}");
        }
    }

    private static void AssertCurtainPanelAreaParameterFlowsToDirectionalTotals()
    {
        string path = Path.Combine(
            Environment.CurrentDirectory,
            "CardinalDirectionGlazing",
            "CardinalDirectionGlazingCommand.cs");
        string source = File.ReadAllText(path);
        string[] required =
        {
            "CurtainPanelAreaParameterCatalog.Collect",
            "new CardinalDirectionGlazingWPF(revitLinkInstanceList, windowAreaParameters, curtainPanelAreaParameters)",
            "SelectedCurtainPanelAreaParameter",
            "CurtainPanelAreaParameterReader.TryRead",
            "CurtainPanelAreaCalculator.Resolve",
            "CurtainPanelAreaUsageSummary",
            "double hostArea = GetFillHostArea(fill)",
            "if (fill is Panel panel)",
            "areaResult.Area",
            "if (areaCounted)",
            "curtainPanelAreaUsageSummary?.Register",
            "Площадь витражных панелей из параметра",
            "Площадь витражных панелей по HOST_AREA_COMPUTED"
        };

        foreach (string marker in required)
        {
            if (!source.Contains(marker))
                throw new InvalidOperationException($"Curtain panel end-to-end marker is missing: {marker}");
        }
    }

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
        {
            if (!source.Contains(marker))
                throw new InvalidOperationException($"Window area integration marker is missing: {marker}");
        }
    }

    private static void AssertWindowAreaParameterRestorationUsesGuid()
    {
        if (WindowAreaParameterSelection.Restore(
                null!,
                "Unused",
                WindowAreaParameterScope.Instance.ToString(),
                string.Empty) != null)
        {
            throw new InvalidOperationException("A missing option catalog must restore no selection.");
        }

        var first = new WindowAreaParameterOption
        {
            Name = "В_Площадь остекления",
            Scope = WindowAreaParameterScope.Instance,
            SharedGuid = "11111111-1111-1111-1111-111111111111"
        };
        var second = new WindowAreaParameterOption
        {
            Name = "В_Площадь остекления",
            Scope = WindowAreaParameterScope.Instance,
            SharedGuid = "22222222-2222-2222-2222-222222222222"
        };

        WindowAreaParameterOption? exact = WindowAreaParameterSelection.Restore(
            new[] { first, second },
            second.Name,
            second.Scope.ToString(),
            second.SharedGuid);
        if (!ReferenceEquals(exact, second))
            throw new InvalidOperationException("Saved shared GUID must win over a duplicate parameter name.");

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
    }

    private static void AssertWindowAreaParameterValueSelectionRejectsWrongDataTypes()
    {
        var option = new WindowAreaParameterOption
        {
            Name = "В_Площадь остекления",
            Scope = WindowAreaParameterScope.Type,
            SharedGuid = "22222222-2222-2222-2222-222222222222"
        };
        var exact = new WindowAreaParameterValueCandidate
        {
            SharedGuid = option.SharedGuid,
            IsArea = true,
            IsDouble = true,
            Value = 10.8
        };
        var namedArea = new WindowAreaParameterValueCandidate
        {
            IsArea = true,
            IsDouble = true,
            Value = 9.4
        };
        var namedLength = new WindowAreaParameterValueCandidate
        {
            IsArea = false,
            IsDouble = true,
            Value = 25.0
        };
        var namedOption = new WindowAreaParameterOption
        {
            Name = option.Name,
            Scope = option.Scope
        };

        if (!WindowAreaParameterValueSelector.TrySelect(
                option,
                exact,
                new[] { namedArea },
                out double exactValue,
                out string exactReason)
            || Math.Abs(exactValue - 10.8) > 1e-9
            || exactReason.Length != 0)
        {
            throw new InvalidOperationException("A valid GUID match must be used before name fallback.");
        }

        var wrongExactStorage = new WindowAreaParameterValueCandidate
        {
            SharedGuid = option.SharedGuid,
            IsArea = true,
            IsDouble = false
        };
        if (WindowAreaParameterValueSelector.TrySelect(
                option,
                wrongExactStorage,
                Array.Empty<WindowAreaParameterValueCandidate>(),
                out _,
                out string wrongStorageReason)
            || wrongStorageReason != "InvalidStorageType")
        {
            throw new InvalidOperationException(
                "An exact area parameter with the wrong storage type must be rejected.");
        }

        var wrongExactDataType = new WindowAreaParameterValueCandidate
        {
            SharedGuid = option.SharedGuid,
            IsArea = false,
            IsDouble = true,
            Value = 25.0
        };
        if (WindowAreaParameterValueSelector.TrySelect(
                option,
                wrongExactDataType,
                Array.Empty<WindowAreaParameterValueCandidate>(),
                out _,
                out string wrongDataTypeReason)
            || wrongDataTypeReason != "InvalidDataType")
        {
            throw new InvalidOperationException(
                "An exact shared parameter with a non-area data type must be rejected.");
        }

        if (!WindowAreaParameterValueSelector.TrySelect(
                namedOption,
                null,
                new[] { namedLength, namedArea },
                out double fallbackValue,
                out string fallbackReason)
            || Math.Abs(fallbackValue - 9.4) > 1e-9
            || fallbackReason.Length != 0)
        {
            throw new InvalidOperationException("Name fallback must select the area candidate among duplicate names.");
        }

        if (WindowAreaParameterValueSelector.TrySelect(
                namedOption,
                null,
                new[] { namedLength },
                out _,
                out string invalidReason)
            || invalidReason != "InvalidDataType")
        {
            throw new InvalidOperationException("A same-named non-area Double must be rejected.");
        }

        if (WindowAreaParameterValueSelector.TrySelect(
                option,
                null,
                new[] { namedArea },
                out _,
                out string missingGuidReason)
            || missingGuidReason != "MissingParameter")
        {
            throw new InvalidOperationException(
                "A shared option must not fall back to a same-named parameter when the exact GUID is missing.");
        }

        var wrongExactIdentity = new WindowAreaParameterValueCandidate
        {
            SharedGuid = "11111111-1111-1111-1111-111111111111",
            IsArea = true,
            IsDouble = true,
            Value = 99.0
        };
        if (WindowAreaParameterValueSelector.TrySelect(
                option,
                wrongExactIdentity,
                Array.Empty<WindowAreaParameterValueCandidate>(),
                out _,
                out string wrongIdentityReason)
            || wrongIdentityReason != "MissingParameter")
        {
            throw new InvalidOperationException(
                "An exact candidate with another shared GUID must be rejected.");
        }

        var sharedNamedCandidate = new WindowAreaParameterValueCandidate
        {
            SharedGuid = option.SharedGuid,
            IsArea = true,
            IsDouble = true,
            Value = 88.0
        };
        if (!WindowAreaParameterValueSelector.TrySelect(
                namedOption,
                null,
                new[] { sharedNamedCandidate, namedArea },
                out double nonSharedValue,
                out _)
            || Math.Abs(nonSharedValue - namedArea.Value) > 1e-9)
        {
            throw new InvalidOperationException(
                "A non-shared option must ignore a same-named shared candidate.");
        }
    }

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
            option,
            exact,
            Array.Empty<WindowAreaParameterValueCandidate>(),
            out double value,
            out _);
        WindowAreaResult parameterResult = WindowAreaCalculator.Resolve(
            true,
            selected ? value : null,
            12.5);
        if (parameterResult.Source != WindowAreaValueSource.Parameter
            || Math.Abs(parameterResult.Area - 10.8) > 1e-9)
        {
            throw new InvalidOperationException(
                "The selected exact GUID value must flow into the calculated window area.");
        }

        bool missing = WindowAreaParameterValueSelector.TrySelect(
            option,
            null,
            new[] { exact },
            out _,
            out _);
        WindowAreaResult fallbackResult = WindowAreaCalculator.Resolve(
            true,
            missing ? exact.Value : null,
            12.5);
        if (fallbackResult.Source != WindowAreaValueSource.DimensionsFallback
            || Math.Abs(fallbackResult.Area - 12.5) > 1e-9)
        {
            throw new InvalidOperationException(
                "A missing exact GUID must flow into the dimensions fallback.");
        }
    }

    private static void AssertWindowAreaCatalogCombinesCurrentAndLinkedObservations()
    {
        var observations = new[]
        {
            new WindowAreaParameterObservation
            {
                DocumentKey = "Current",
                Name = "В_Площадь остекления",
                Scope = WindowAreaParameterScope.Instance,
                IsArea = false,
                IsDouble = true
            },
            new WindowAreaParameterObservation
            {
                DocumentKey = "Current",
                Name = "В_Площадь остекления",
                Scope = WindowAreaParameterScope.Type,
                SharedGuid = "11111111-1111-1111-1111-111111111111",
                IsArea = true,
                IsDouble = true
            },
            new WindowAreaParameterObservation
            {
                DocumentKey = "Linked",
                Name = "В_Площадь остекления",
                Scope = WindowAreaParameterScope.Instance,
                SharedGuid = "22222222-2222-2222-2222-222222222222",
                IsArea = true,
                IsDouble = true
            },
            new WindowAreaParameterObservation
            {
                DocumentKey = "Linked",
                Name = "В_Площадь остекления",
                Scope = WindowAreaParameterScope.Instance,
                SharedGuid = "22222222-2222-2222-2222-222222222222",
                IsArea = true,
                IsDouble = true
            }
        };

        IReadOnlyList<WindowAreaParameterOption> options =
            WindowAreaParameterCatalogBuilder.Build(observations);
        WindowAreaParameterOption? instance = options.FirstOrDefault(item => item.Scope == WindowAreaParameterScope.Instance);
        WindowAreaParameterOption? type = options.FirstOrDefault(item => item.Scope == WindowAreaParameterScope.Type);
        if (options.Count != 2
            || instance?.SharedGuid != "22222222-2222-2222-2222-222222222222"
            || type?.SharedGuid != "11111111-1111-1111-1111-111111111111")
        {
            throw new InvalidOperationException("The catalog must merge valid area parameters from current and linked documents by scope.");
        }
    }

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
            },
            new WindowAreaParameterObservation
            {
                DocumentKey = "CurrentNonShared",
                Name = "SameName",
                Scope = WindowAreaParameterScope.Instance,
                IsArea = true,
                IsDouble = true
            }
        };

        IReadOnlyList<WindowAreaParameterOption> options =
            WindowAreaParameterCatalogBuilder.Build(observations);
        if (options.Count != 3
            || !options.Any(item => item.SharedGuid == observations[0].SharedGuid)
            || !options.Any(item => item.SharedGuid == observations[1].SharedGuid)
            || !options.Any(item => item.SharedGuid.Length == 0))
        {
            throw new InvalidOperationException(
                "Same-named shared parameters with different GUIDs and a non-shared parameter must remain distinct.");
        }
    }

    private static void AssertClassified(
        string caseName,
        double orientationX,
        double orientationY,
        double eastX,
        double eastY,
        double northX,
        double northY)
    {
        bool ok = CardinalDirectionClassifier.TryClassify(
            orientationX,
            orientationY,
            eastX,
            eastY,
            northX,
            northY,
            out _);

        if (!ok)
        {
            throw new InvalidOperationException($"Case '{caseName}' was not classified.");
        }
    }

    private static void AssertBucket(string caseName, double orientationX, double orientationY, CardinalDirectionBucket expected)
    {
        bool ok = CardinalDirectionClassifier.TryClassify(
            orientationX,
            orientationY,
            eastX: 1,
            eastY: 0,
            northX: 0,
            northY: 1,
            out CardinalDirectionBucket actual);

        if (!ok)
        {
            throw new InvalidOperationException($"Case '{caseName}' was not classified.");
        }

        if (actual != expected)
        {
            throw new InvalidOperationException($"Case '{caseName}' expected {expected}, got {actual}.");
        }
    }
}
