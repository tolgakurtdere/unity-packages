using System.Collections.Generic;

namespace TK.Localization
{
    /// <summary>Pure locale-choice policy: saved (if available) → device (if available) → first available → null.</summary>
    public static class LocaleSelection
    {
        public static string Choose(string savedCode, string deviceCode, IReadOnlyList<string> availableCodes)
        {
            if (availableCodes == null || availableCodes.Count == 0) return null;
            if (!string.IsNullOrEmpty(savedCode) && Contains(availableCodes, savedCode)) return savedCode;
            if (!string.IsNullOrEmpty(deviceCode) && Contains(availableCodes, deviceCode)) return deviceCode;
            return availableCodes[0];
        }

        private static bool Contains(IReadOnlyList<string> list, string code)
        {
            for (var i = 0; i < list.Count; i++)
                if (list[i] == code) return true;
            return false;
        }
    }
}
