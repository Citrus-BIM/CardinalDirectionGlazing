using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    public sealed class WindowAreaParameterObservation
    {
        public string DocumentKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public WindowAreaParameterScope Scope { get; set; }
        public string SharedGuid { get; set; } = string.Empty;
        public bool IsArea { get; set; }
        public bool IsDouble { get; set; }
    }

    public static class WindowAreaParameterCatalogBuilder
    {
        public static IReadOnlyList<WindowAreaParameterOption> Build(
            IEnumerable<WindowAreaParameterObservation> observations)
        {
            var options = new Dictionary<string, WindowAreaParameterOption>(StringComparer.OrdinalIgnoreCase);
            foreach (WindowAreaParameterObservation observation in
                observations ?? Enumerable.Empty<WindowAreaParameterObservation>())
            {
                if (!observation.IsArea
                    || !observation.IsDouble
                    || string.IsNullOrWhiteSpace(observation.Name))
                    continue;

                string key = observation.Scope + "|" + observation.Name;
                if (!options.TryGetValue(key, out WindowAreaParameterOption? option))
                {
                    option = new WindowAreaParameterOption
                    {
                        Name = observation.Name,
                        Scope = observation.Scope,
                        SharedGuid = observation.SharedGuid
                    };
                    options.Add(key, option);
                }
                else if (option.SharedGuid.Length == 0 && observation.SharedGuid.Length > 0)
                {
                    option.SharedGuid = observation.SharedGuid;
                }
            }

            return options.Values
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Scope)
                .ToList();
        }
    }
}
