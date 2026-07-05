using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TK.Localization
{
    /// <summary>
    /// Locale selection + persistence over Unity Localization. Picks saved → device → first available on init,
    /// persists on change, exposes culture/RTL/change-event. Main-thread-affine. Never throws on an unknown code.
    /// </summary>
    public sealed class LocaleService
    {
        private readonly ILocalePersistence _persistence;

        public LocaleService(ILocalePersistence persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public event Action<Locale> OnLocaleChanged;

        public IReadOnlyList<Locale> Available => LocalizationSettings.AvailableLocales.Locales;
        public Locale Current => LocalizationSettings.SelectedLocale;
        public CultureInfo CurrentCulture =>
            Current != null ? Current.Identifier.CultureInfo : CultureInfo.InvariantCulture;
        public bool IsRtl => CurrentCulture.TextInfo.IsRightToLeft;

        public async Task InitializeAsync()
        {
            await LocalizationSettings.InitializationOperation.Task;
            var codes = ToCodes(Available);
            var device = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            var chosen = LocaleSelection.Choose(_persistence.Load(), device, codes);
            if (chosen != null) SetLocale(chosen);
        }

        public bool SetLocale(string localeCode)
        {
            var locale = FindAvailable(localeCode);
            if (locale == null) return false;
            SetLocale(locale);
            return true;
        }

        public void SetLocale(Locale locale)
        {
            if (locale == null) throw new ArgumentNullException(nameof(locale));
            LocalizationSettings.SelectedLocale = locale;
            _persistence.Save(locale.Identifier.Code);
            OnLocaleChanged?.Invoke(locale);
        }

        private Locale FindAvailable(string code)
        {
            var locales = Available;
            for (var i = 0; i < locales.Count; i++)
                if (locales[i] != null && locales[i].Identifier.Code == code) return locales[i];
            return null;
        }

        private static IReadOnlyList<string> ToCodes(IReadOnlyList<Locale> locales)
        {
            var codes = new List<string>(locales.Count);
            for (var i = 0; i < locales.Count; i++)
                if (locales[i] != null) codes.Add(locales[i].Identifier.Code);
            return codes;
        }
    }
}
