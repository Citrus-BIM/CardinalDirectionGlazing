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
            return new WindowAreaResult
            {
                Area = area,
                Source = source,
                FallbackReason = reason
            };
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
