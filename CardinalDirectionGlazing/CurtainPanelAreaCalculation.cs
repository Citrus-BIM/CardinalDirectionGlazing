using System;
using System.Collections.Generic;

namespace CardinalDirectionGlazing
{
    public enum CurtainPanelAreaValueSource
    {
        HostArea,
        Parameter,
        HostAreaFallback
    }

    public sealed class CurtainPanelAreaResult
    {
        public double Area { get; set; }
        public CurtainPanelAreaValueSource Source { get; set; }
        public string FallbackReason { get; set; } = string.Empty;
    }

    public static class CurtainPanelAreaCalculator
    {
        public static CurtainPanelAreaResult Resolve(
            bool useParameter,
            double? parameterArea,
            double hostArea)
        {
            if (!useParameter)
                return Create(hostArea, CurtainPanelAreaValueSource.HostArea, string.Empty);
            if (!parameterArea.HasValue)
                return Create(hostArea, CurtainPanelAreaValueSource.HostAreaFallback, "MissingParameter");
            if (double.IsNaN(parameterArea.Value) || double.IsInfinity(parameterArea.Value))
                return Create(hostArea, CurtainPanelAreaValueSource.HostAreaFallback, "NonFiniteParameter");
            if (parameterArea.Value <= 0)
                return Create(hostArea, CurtainPanelAreaValueSource.HostAreaFallback, "NonPositiveParameter");

            return Create(parameterArea.Value, CurtainPanelAreaValueSource.Parameter, string.Empty);
        }

        private static CurtainPanelAreaResult Create(
            double area,
            CurtainPanelAreaValueSource source,
            string reason)
        {
            return new CurtainPanelAreaResult
            {
                Area = area,
                Source = source,
                FallbackReason = reason
            };
        }
    }

    public sealed class CurtainPanelAreaUsageSummary
    {
        private readonly HashSet<string> _registeredKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public int ParameterCount { get; private set; }
        public int HostAreaFallbackCount { get; private set; }

        public void Register(string sourceKey, CurtainPanelAreaValueSource source)
        {
            if (string.IsNullOrWhiteSpace(sourceKey) || !_registeredKeys.Add(sourceKey))
                return;

            if (source == CurtainPanelAreaValueSource.Parameter)
                ParameterCount++;
            else if (source == CurtainPanelAreaValueSource.HostAreaFallback)
                HostAreaFallbackCount++;
        }
    }
}
