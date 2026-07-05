namespace TK.RemoteConfig
{
    public sealed partial class RemoteConfigService
    {
        // ── Single-key typed factories (register default, wire a getter through the raw reads) ──

        public ConfigParam<int> Int(string key, int def)
        {
            RegisterDefault(key, (long)def);
            return new ConfigParam<int>(key, def, () => GetInt(key, def));
        }

        public ConfigParam<long> Long(string key, long def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<long>(key, def, () => GetLong(key, def));
        }

        public ConfigParam<double> Double(string key, double def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<double>(key, def, () => GetDouble(key, def));
        }

        public ConfigParam<float> Float(string key, float def)
        {
            RegisterDefault(key, (double)def);
            return new ConfigParam<float>(key, def, () => GetFloat(key, def));
        }

        public ConfigParam<bool> Bool(string key, bool def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<bool>(key, def, () => GetBool(key, def));
        }

        public ConfigParam<string> String(string key, string def)
        {
            RegisterDefault(key, def);
            return new ConfigParam<string>(key, def, () => GetString(key, def));
        }

        // ── Per-platform overloads (Android + Editor use android; iOS uses ios) ──

        public ConfigParam<int> Int(string androidKey, int androidDef, string iosKey, int iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Int);

        public ConfigParam<long> Long(string androidKey, long androidDef, string iosKey, long iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Long);

        public ConfigParam<double> Double(string androidKey, double androidDef, string iosKey, double iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Double);

        public ConfigParam<float> Float(string androidKey, float androidDef, string iosKey, float iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Float);

        public ConfigParam<bool> Bool(string androidKey, bool androidDef, string iosKey, bool iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, Bool);

        public ConfigParam<string> String(string androidKey, string androidDef, string iosKey, string iosDef)
            => SelectPlatform(androidKey, androidDef, iosKey, iosDef, String);

        private static ConfigParam<T> SelectPlatform<T>(
            string androidKey, T androidDef, string iosKey, T iosDef, System.Func<string, T, ConfigParam<T>> factory)
        {
#if UNITY_IOS
            return factory(iosKey, iosDef);
#else
            return factory(androidKey, androidDef); // Android + Editor + other targets
#endif
        }
    }
}
