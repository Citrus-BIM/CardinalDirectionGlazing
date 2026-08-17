using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    public static class WindowAreaParameterSelection
    {
        public static WindowAreaParameterOption? Restore(
            IReadOnlyList<WindowAreaParameterOption> options,
            string savedName,
            string savedScope,
            string savedGuid)
        {
            if (!Enum.TryParse(savedScope, out WindowAreaParameterScope scope))
                return null;

            if (!string.IsNullOrWhiteSpace(savedGuid))
            {
                WindowAreaParameterOption? exact = options.FirstOrDefault(item =>
                    item.Scope == scope
                    && string.Equals(item.SharedGuid, savedGuid, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    return exact;
            }

            WindowAreaParameterOption? byName = options.FirstOrDefault(item =>
                item.Scope == scope
                && string.Equals(item.Name, savedName, StringComparison.Ordinal));
            if (byName == null || string.IsNullOrWhiteSpace(savedGuid))
                return byName;

            byName.SharedGuid = savedGuid;
            return byName;
        }
    }

    public sealed class WindowAreaParameterValueCandidate
    {
        public bool IsArea { get; set; }
        public bool IsDouble { get; set; }
        public double Value { get; set; }
    }

    public static class WindowAreaParameterValueSelector
    {
        public static bool TrySelect(
            WindowAreaParameterOption option,
            WindowAreaParameterValueCandidate? exactGuidCandidate,
            IEnumerable<WindowAreaParameterValueCandidate> nameCandidates,
            out double value,
            out string reason)
        {
            value = 0;
            reason = string.Empty;
            if (option == null)
            {
                reason = "MissingParameter";
                return false;
            }

            if (exactGuidCandidate != null)
                return TryReadCandidate(exactGuidCandidate, out value, out reason);

            List<WindowAreaParameterValueCandidate> candidates =
                (nameCandidates ?? Enumerable.Empty<WindowAreaParameterValueCandidate>()).ToList();
            WindowAreaParameterValueCandidate? valid =
                candidates.FirstOrDefault(candidate => candidate.IsArea && candidate.IsDouble);
            if (valid != null)
            {
                value = valid.Value;
                return true;
            }

            if (candidates.Count == 0)
                reason = "MissingParameter";
            else if (candidates.Any(candidate => candidate.IsArea))
                reason = "InvalidStorageType";
            else
                reason = "InvalidDataType";
            return false;
        }

        private static bool TryReadCandidate(
            WindowAreaParameterValueCandidate candidate,
            out double value,
            out string reason)
        {
            value = 0;
            reason = string.Empty;
            if (!candidate.IsArea)
            {
                reason = "InvalidDataType";
                return false;
            }
            if (!candidate.IsDouble)
            {
                reason = "InvalidStorageType";
                return false;
            }

            value = candidate.Value;
            return true;
        }
    }
}
