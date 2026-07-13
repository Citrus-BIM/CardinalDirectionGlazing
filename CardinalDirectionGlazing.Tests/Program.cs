using System;
using System.Text;

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
        AssertDesktopTracePathUsesStableTimestamp();
        AssertTraceWriteDoesNotOverwriteExistingFile();

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

        string json = Encoding.UTF8.GetString(CalculationTraceWriter.Serialize(trace));

        if (!json.Contains("LinkedWindows") || !json.Contains("host-panel-uid") || !json.Contains("roughHeight"))
        {
            throw new InvalidOperationException("Window trace must serialize its source pass, ownership and diagnostic values.");
        }
    }

    private static void AssertDesktopTracePathUsesStableTimestamp()
    {
        string path = CalculationTraceWriter.CreateDesktopPath(new DateTime(2026, 7, 13, 14, 30, 50));
        string fileName = System.IO.Path.GetFileName(path);

        if (!string.Equals(fileName, "CardinalDirectionGlazing_2026-07-13_143050.json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected trace file name: '{fileName}'.");
        }
    }

    private static void AssertTraceWriteDoesNotOverwriteExistingFile()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CardinalDirectionGlazing.Tests", Guid.NewGuid().ToString("N"));
        string requestedPath = System.IO.Path.Combine(directory, "CardinalDirectionGlazing_2026-07-13_143050.json");
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
