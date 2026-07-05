using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace TK.Localization
{
    /// <summary>
    /// Maps locales to <see cref="LocaleFontInfo"/> with a mandatory fallback. Resolve() tries the requested
    /// locale, then that locale's own fallbacks, then the mandatory Fallback — never returns null while
    /// Fallback is assigned. (Reference used SelectedLocale's fallbacks and could return null; both fixed.)
    /// </summary>
    [CreateAssetMenu(fileName = "LocaleFontMap", menuName = "TK/Localization/Locale Font Map")]
    public sealed class LocaleFontMap : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            public Locale Locale;
            public LocaleFontInfo Font;
        }

        [SerializeField] private LocaleFontInfo _fallback;
        [SerializeField] private List<Entry> _entries = new();

        public LocaleFontInfo Fallback => _fallback;

        public LocaleFontInfo Resolve(Locale locale)
        {
            if (locale)
            {
                var direct = Find(locale.Identifier.Code);
                if (direct) return direct;

                foreach (var fallback in locale.GetFallbacks())   // the REQUESTED locale's fallbacks
                {
                    if (!fallback) continue;
                    var hit = Find(fallback.Identifier.Code);
                    if (hit) return hit;
                }
            }

            if (!_fallback) Debug.LogError($"[TK.Localization] LocaleFontMap '{name}' has no Fallback assigned; returning null.");
            return _fallback;
        }

        private LocaleFontInfo Find(string code)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry?.Locale && entry.Font && entry.Locale.Identifier.Code == code)
                    return entry.Font;
            }

            return null;
        }
    }
}
