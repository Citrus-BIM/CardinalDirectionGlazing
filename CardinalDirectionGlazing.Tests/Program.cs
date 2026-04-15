using System;

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

        if (CardinalDirectionClassifier.TryClassify(0, 0, 1, 0, 0, 1, out _))
        {
            throw new InvalidOperationException("Zero vector must not be classified.");
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
