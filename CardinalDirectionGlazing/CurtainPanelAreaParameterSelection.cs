using System;
using System.Collections.Generic;
using System.Linq;

namespace CardinalDirectionGlazing
{
    public static class CurtainPanelAreaParameterSelection
    {
        public static CurtainPanelAreaParameterOption? Restore(
            IReadOnlyList<CurtainPanelAreaParameterOption> options,
            string savedName,
            string savedScope,
            string savedGuid)
        {
            if (options == null)
                return null;
            if (!Enum.TryParse(savedScope, out CurtainPanelAreaParameterScope scope))
                return null;

            if (!string.IsNullOrWhiteSpace(savedGuid))
            {
                return options.FirstOrDefault(item =>
                    item.Scope == scope
                    && SameGuid(item.SharedGuid, savedGuid));
            }

            return options.FirstOrDefault(item =>
                item.Scope == scope
                && string.IsNullOrWhiteSpace(item.SharedGuid)
                && string.Equals(item.Name, savedName, StringComparison.Ordinal));
        }

        private static bool SameGuid(string left, string right)
        {
            return Guid.TryParse(left, out Guid leftGuid)
                && Guid.TryParse(right, out Guid rightGuid)
                && leftGuid == rightGuid;
        }
    }

    public sealed class CurtainPanelAreaParameterValueCandidate
    {
        public string SharedGuid { get; set; } = string.Empty;
        public bool IsArea { get; set; }
        public bool IsDouble { get; set; }
        public double Value { get; set; }
    }

    public static class CurtainPanelAreaParameterValueSelector
    {
        public static bool TrySelect(
            CurtainPanelAreaParameterOption option,
            CurtainPanelAreaParameterValueCandidate? exactGuidCandidate,
            IEnumerable<CurtainPanelAreaParameterValueCandidate> nameCandidates,
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

            if (!string.IsNullOrWhiteSpace(option.SharedGuid))
            {
                if (exactGuidCandidate == null
                    || !SameGuid(exactGuidCandidate.SharedGuid, option.SharedGuid))
                {
                    reason = "MissingParameter";
                    return false;
                }

                return TryReadCandidate(exactGuidCandidate, out value, out reason);
            }

            List<CurtainPanelAreaParameterValueCandidate> candidates =
                (nameCandidates ?? Enumerable.Empty<CurtainPanelAreaParameterValueCandidate>())
                .Where(candidate => string.IsNullOrWhiteSpace(candidate.SharedGuid))
                .ToList();
            CurtainPanelAreaParameterValueCandidate? valid =
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
            CurtainPanelAreaParameterValueCandidate candidate,
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

        private static bool SameGuid(string left, string right)
        {
            return Guid.TryParse(left, out Guid leftGuid)
                && Guid.TryParse(right, out Guid rightGuid)
                && leftGuid == rightGuid;
        }
    }
}
