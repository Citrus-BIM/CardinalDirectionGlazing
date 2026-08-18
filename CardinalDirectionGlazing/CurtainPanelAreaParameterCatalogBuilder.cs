using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    public sealed class CurtainPanelAreaParameterObservation
    {
        public string DocumentKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CurtainPanelAreaParameterScope Scope { get; set; }
        public string SharedGuid { get; set; } = string.Empty;
        public bool IsArea { get; set; }
        public bool IsDouble { get; set; }
    }

    public static class CurtainPanelAreaParameterCatalogBuilder
    {
        public static IReadOnlyList<CurtainPanelAreaParameterOption> Build(
            IEnumerable<CurtainPanelAreaParameterObservation> observations)
        {
            var options = new Dictionary<string, CurtainPanelAreaParameterOption>(
                StringComparer.OrdinalIgnoreCase);

            foreach (CurtainPanelAreaParameterObservation observation in
                observations ?? Enumerable.Empty<CurtainPanelAreaParameterObservation>())
            {
                if (!observation.IsArea
                    || !observation.IsDouble
                    || string.IsNullOrWhiteSpace(observation.Name))
                {
                    continue;
                }

                string sharedGuid = NormalizeGuid(observation.SharedGuid);
                string key = sharedGuid.Length > 0
                    ? observation.Scope + "|Shared|" + sharedGuid
                    : observation.Scope + "|Named|" + observation.Name;

                if (!options.ContainsKey(key))
                {
                    options.Add(key, new CurtainPanelAreaParameterOption
                    {
                        Name = observation.Name,
                        Scope = observation.Scope,
                        SharedGuid = sharedGuid
                    });
                }
            }

            return options.Values
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Scope)
                .ToList();
        }

        private static string NormalizeGuid(string value)
        {
            return Guid.TryParse(value, out Guid guid)
                ? guid.ToString("D")
                : string.Empty;
        }
    }
}
