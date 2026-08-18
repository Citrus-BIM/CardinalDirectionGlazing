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
        private readonly HashSet<SourceKey> _registeredKeys = new HashSet<SourceKey>();

        public int ParameterCount { get; private set; }
        public int HostAreaFallbackCount { get; private set; }

        public void Register(
            object documentIdentity,
            string uniqueId,
            CurtainPanelAreaValueSource source)
        {
            if (documentIdentity == null
                || string.IsNullOrWhiteSpace(uniqueId)
                || !_registeredKeys.Add(new SourceKey(documentIdentity, uniqueId)))
            {
                return;
            }

            if (source == CurtainPanelAreaValueSource.Parameter)
                ParameterCount++;
            else if (source == CurtainPanelAreaValueSource.HostAreaFallback)
                HostAreaFallbackCount++;
        }

        private readonly struct SourceKey : IEquatable<SourceKey>
        {
            private readonly object _documentIdentity;
            private readonly string _uniqueId;

            public SourceKey(object documentIdentity, string uniqueId)
            {
                _documentIdentity = documentIdentity;
                _uniqueId = uniqueId;
            }

            public bool Equals(SourceKey other)
            {
                return ReferenceEquals(_documentIdentity, other._documentIdentity)
                    && string.Equals(_uniqueId, other._uniqueId, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is SourceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_documentIdentity) * 397)
                        ^ StringComparer.Ordinal.GetHashCode(_uniqueId);
                }
            }
        }
    }
}
