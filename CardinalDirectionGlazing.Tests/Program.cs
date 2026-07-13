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
