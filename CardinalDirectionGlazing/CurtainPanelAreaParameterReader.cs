using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    internal static class CurtainPanelAreaParameterReader
    {
        public static bool TryRead(
            Panel panel,
            CurtainPanelAreaParameterOption option,
            out double value,
            out string reason)
        {
            value = 0;
            reason = string.Empty;
            try
            {
                return TryReadCore(panel, option, out value, out reason);
            }
            catch (Exception)
            {
                value = 0;
                reason = "ApiException";
                return false;
            }
        }

        private static bool TryReadCore(
            Panel panel,
            CurtainPanelAreaParameterOption option,
            out double value,
            out string reason)
        {
            value = 0;
            reason = string.Empty;
            if (panel == null || option == null)
            {
                reason = "MissingParameter";
                return false;
            }

            Element source = option.Scope == CurtainPanelAreaParameterScope.Instance
                ? panel
                : panel.Symbol;
            if (source == null)
            {
                reason = "MissingParameter";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(option.SharedGuid))
            {
                if (!Guid.TryParse(option.SharedGuid, out Guid guid))
                {
                    reason = "MissingParameter";
                    return false;
                }

                Parameter exactParameter = source.get_Parameter(guid);
                if (exactParameter == null)
                {
                    reason = "MissingParameter";
                    return false;
                }

                return CurtainPanelAreaParameterValueSelector.TrySelect(
                    option,
                    CreateCandidate(exactParameter),
                    Enumerable.Empty<CurtainPanelAreaParameterValueCandidate>(),
                    out value,
                    out reason);
            }

            if (string.IsNullOrWhiteSpace(option.Name))
            {
                reason = "MissingParameter";
                return false;
            }

            IEnumerable<CurtainPanelAreaParameterValueCandidate> nameCandidates =
                source.GetParameters(option.Name).Select(CreateCandidate);
            return CurtainPanelAreaParameterValueSelector.TrySelect(
                option,
                null,
                nameCandidates,
                out value,
                out reason);
        }

        private static CurtainPanelAreaParameterValueCandidate CreateCandidate(Parameter parameter)
        {
            bool isDouble = parameter.StorageType == StorageType.Double;
            return new CurtainPanelAreaParameterValueCandidate
            {
                SharedGuid = GetSharedGuid(parameter),
                IsArea = IsAreaDefinition(parameter.Definition),
                IsDouble = isDouble,
                Value = isDouble ? parameter.AsDouble() : 0
            };
        }

        private static string GetSharedGuid(Parameter parameter)
        {
            if (parameter == null || !parameter.IsShared)
                return string.Empty;

            try
            {
                return parameter.GUID.ToString("D");
            }
            catch (InvalidOperationException)
            {
                return string.Empty;
            }
        }

        private static bool IsAreaDefinition(Definition definition)
        {
            if (definition == null)
                return false;
#if REVIT_2019 || REVIT_2020 || REVIT_2021
            return definition.ParameterType == ParameterType.Area;
#else
            return definition.GetDataType() == SpecTypeId.Area;
#endif
        }
    }
}
