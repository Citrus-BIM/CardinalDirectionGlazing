using System;

namespace CardinalDirectionGlazing
{
    internal enum CardinalDirectionBucket
    {
        North,
        South,
        West,
        East,
        Northwest,
        Northeast,
        Southwest,
        Southeast
    }

    internal static class CardinalDirectionClassifier
    {
        public static bool TryClassify(
            double orientationX,
            double orientationY,
            double eastX,
            double eastY,
            double northX,
            double northY,
            out CardinalDirectionBucket bucket)
        {
            bucket = default;

            if (!TryNormalize(orientationX, orientationY, out double orientationUnitX, out double orientationUnitY))
                return false;

            if (!TryNormalize(eastX, eastY, out double eastUnitX, out double eastUnitY))
                return false;

            if (!TryNormalize(northX, northY, out double northUnitX, out double northUnitY))
                return false;

            Span<(CardinalDirectionBucket Bucket, double X, double Y)> candidates =
            [
                (CardinalDirectionBucket.North, northUnitX, northUnitY),
                (CardinalDirectionBucket.South, -northUnitX, -northUnitY),
                (CardinalDirectionBucket.West, -eastUnitX, -eastUnitY),
                (CardinalDirectionBucket.East, eastUnitX, eastUnitY),
                CreateDiagonal(CardinalDirectionBucket.Northwest, northUnitX - eastUnitX, northUnitY - eastUnitY),
                CreateDiagonal(CardinalDirectionBucket.Northeast, northUnitX + eastUnitX, northUnitY + eastUnitY),
                CreateDiagonal(CardinalDirectionBucket.Southwest, -northUnitX - eastUnitX, -northUnitY - eastUnitY),
                CreateDiagonal(CardinalDirectionBucket.Southeast, -northUnitX + eastUnitX, -northUnitY + eastUnitY)
            ];

            double bestDot = double.NegativeInfinity;
            CardinalDirectionBucket bestBucket = default;

            foreach ((CardinalDirectionBucket candidateBucket, double candidateX, double candidateY) in candidates)
            {
                double dot = orientationUnitX * candidateX + orientationUnitY * candidateY;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestBucket = candidateBucket;
                }
            }

            bucket = bestBucket;
            return true;
        }

        private static (CardinalDirectionBucket Bucket, double X, double Y) CreateDiagonal(
            CardinalDirectionBucket bucket,
            double x,
            double y)
        {
            if (!TryNormalize(x, y, out double normalizedX, out double normalizedY))
                return (bucket, 0, 0);

            return (bucket, normalizedX, normalizedY);
        }

        private static bool TryNormalize(double x, double y, out double normalizedX, out double normalizedY)
        {
            double length = Math.Sqrt((x * x) + (y * y));
            if (length <= 1e-9)
            {
                normalizedX = 0;
                normalizedY = 0;
                return false;
            }

            normalizedX = x / length;
            normalizedY = y / length;
            return true;
        }
    }
}
