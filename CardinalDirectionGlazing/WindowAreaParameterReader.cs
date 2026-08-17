using Autodesk.Revit.DB;
using System;

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

            Parameter? parameter = null;
            if (Guid.TryParse(option.SharedGuid, out Guid guid))
                parameter = source.get_Parameter(guid);
            if (parameter == null)
                parameter = source.LookupParameter(option.Name);
            if (parameter == null)
            {
                reason = "MissingParameter";
                return false;
            }

            if (parameter.StorageType != StorageType.Double)
            {
                reason = "InvalidStorageType";
                return false;
            }

            value = parameter.AsDouble();
            return true;
        }
    }
}
