namespace TK.Analytics
{
    public enum AnalyticsParamType { String, Long, Double, Bool }

    /// <summary>
    /// SDK-agnostic analytics parameter. Allocation-free (no boxing): the value lives in one of
    /// four typed fields selected by <see cref="Type"/>. Create via the static factories.
    /// </summary>
    public readonly struct AnalyticsParam
    {
        public string Key { get; }
        public AnalyticsParamType Type { get; }
        public string StringValue { get; }   // valid when Type == String
        public long   LongValue { get; }      // valid when Type == Long
        public double DoubleValue { get; }    // valid when Type == Double
        public bool   BoolValue { get; }      // valid when Type == Bool

        private AnalyticsParam(string key, AnalyticsParamType type, string s, long l, double d, bool b)
        {
            Key = key; Type = type; StringValue = s; LongValue = l; DoubleValue = d; BoolValue = b;
        }

        public static AnalyticsParam String(string key, string value) => new(key, AnalyticsParamType.String, value, 0, 0, false);
        public static AnalyticsParam Long(string key, long value)     => new(key, AnalyticsParamType.Long, null, value, 0, false);
        public static AnalyticsParam Double(string key, double value) => new(key, AnalyticsParamType.Double, null, 0, value, false);
        public static AnalyticsParam Bool(string key, bool value)     => new(key, AnalyticsParamType.Bool, null, 0, 0, value);

        public override string ToString()
        {
            object v = Type switch
            {
                AnalyticsParamType.String => StringValue,
                AnalyticsParamType.Long   => LongValue,
                AnalyticsParamType.Double => DoubleValue,
                AnalyticsParamType.Bool   => BoolValue,
                _ => null
            };
            return $"{Key}={v}";
        }
    }
}
