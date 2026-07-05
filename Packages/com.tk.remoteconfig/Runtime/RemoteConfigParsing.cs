using System.Collections.Generic;

namespace TK.RemoteConfig
{
    /// <summary>CSV parse helpers for string config values (e.g. "4,12,20" → list).</summary>
    public static class RemoteConfigParsing
    {
        public static List<int> ParseIntList(string csv)
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(csv)) return list;

            foreach (var part in csv.Split(','))
            {
                if (int.TryParse(part.Trim(), out var n)) list.Add(n);
            }

            return list;
        }

        public static List<string> ParseStringList(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(csv)) return list;

            foreach (var part in csv.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) list.Add(trimmed);
            }

            return list;
        }

        public static List<int> ParseIntList(this ConfigParam<string> param) => ParseIntList(param.Value);
        public static List<string> ParseStringList(this ConfigParam<string> param) => ParseStringList(param.Value);
    }
}
