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

            Element? source = option.Scope == WindowAreaParameterScope.Instance
                ? window
                : window.Symbol;
            if (source == null)
            {
                reason = "MissingParameter";
                return false;
            }

            Parameter? exactParameter = null;
            if (Guid.TryParse(option.SharedGuid, out Guid guid))
                exactParameter = source.get_Parameter(guid);

            WindowAreaParameterValueCandidate? exactCandidate = exactParameter == null
                ? null
                : CreateCandidate(exactParameter);
            IEnumerable<WindowAreaParameterValueCandidate> nameCandidates =
                source.GetParameters(option.Name).Select(CreateCandidate);

            return WindowAreaParameterValueSelector.TrySelect(
                option,
                exactCandidate,
                nameCandidates,
                out value,
                out reason);
        }

        private static WindowAreaParameterValueCandidate CreateCandidate(Parameter parameter)
        {
            bool isDouble = parameter.StorageType == StorageType.Double;
            return new WindowAreaParameterValueCandidate
            {
                IsArea = IsAreaDefinition(parameter.Definition),
                IsDouble = isDouble,
                Value = isDouble ? parameter.AsDouble() : 0
            };
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
