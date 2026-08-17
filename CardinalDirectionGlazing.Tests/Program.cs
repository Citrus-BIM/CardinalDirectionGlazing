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
        AssertWindowAreaSettingsRoundTrip();
        AssertWindowAreaParameterControlsExist();

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
