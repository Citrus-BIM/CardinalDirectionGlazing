using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    internal static class WindowAreaParameterReader
    {
        public static bool TryRead(
            FamilyInstance window,
            WindowAreaParameterOption option,
            out double value,
            out string reason)
        {
            value = 0;
            reason = string.Empty;
            if (window == null || option == null)
            {
                reason = "MissingParameter";
                return false;
            }

            Element? source = option.Scope == WindowAreaParameterScope.Instance
                ? window
                : window.Symbol;
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

                Parameter? exactParameter = source.get_Parameter(guid);
                if (exactParameter == null)
                {
                    reason = "MissingParameter";
                    return false;
                }

                return WindowAreaParameterValueSelector.TrySelect(
                    option,
                    CreateCandidate(exactParameter),
                    Enumerable.Empty<WindowAreaParameterValueCandidate>(),
                    out value,
                    out reason);
            }

            if (string.IsNullOrWhiteSpace(option.Name))
            {
                reason = "MissingParameter";
                return false;
            }

            IEnumerable<WindowAreaParameterValueCandidate> nameCandidates =
                source.GetParameters(option.Name).Select(CreateCandidate);
            return WindowAreaParameterValueSelector.TrySelect(
                option,
                null,
                nameCandidates,
                out value,
                out reason);
        }

        private static WindowAreaParameterValueCandidate CreateCandidate(Parameter parameter)
        {
            bool isDouble = parameter.StorageType == StorageType.Double;
            return new WindowAreaParameterValueCandidate
            {
                SharedGuid = GetSharedGuid(parameter),
                IsArea = IsAreaDefinition(parameter.Definition),
                IsDouble = isDouble,
                Value = isDouble ? parameter.AsDouble() : 0
            };
        }

        private static string GetSharedGuid(Parameter parameter)
        {
            if (!parameter.IsShared)
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

        private static bool IsAreaDefinition(Definition? definition)
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
