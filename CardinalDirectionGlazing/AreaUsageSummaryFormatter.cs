using System;
using System.Collections.Generic;

namespace CardinalDirectionGlazing
{
    public static class AreaUsageSummaryFormatter
    {
        public static string Format(
            WindowAreaParameterOption? windowOption,
            WindowAreaUsageSummary windowSummary,
            CurtainPanelAreaParameterOption? panelOption,
            CurtainPanelAreaUsageSummary panelSummary)
        {
            var lines = new List<string>();

            if (windowOption != null)
            {
                lines.Add(
                    "Окна — параметр "
                    + FormatParameterDisplayName(windowOption.DisplayName)
                    + ": "
                    + windowSummary.ParameterCount);
                lines.Add(
                    "Окна — «Высота» × «Ширина»: "
                    + windowSummary.DimensionsFallbackCount);
            }

            if (panelOption != null)
            {
                if (lines.Count > 0)
                    lines.Add(string.Empty);

                lines.Add(
                    "Витражные панели — параметр "
                    + FormatParameterDisplayName(panelOption.DisplayName)
                    + ": "
                    + panelSummary.ParameterCount);
                lines.Add(
                    "Витражные панели — системный параметр «Площадь»: "
                    + panelSummary.HostAreaFallbackCount);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatParameterDisplayName(string displayName)
        {
            int scopeIndex = displayName.LastIndexOf(" (", StringComparison.Ordinal);
            if (scopeIndex < 0)
                return "«" + displayName + "»";

            return "«"
                + displayName.Substring(0, scopeIndex)
                + "»"
                + displayName.Substring(scopeIndex);
        }
    }
}
