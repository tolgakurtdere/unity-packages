using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TK.Localization
{
    /// <summary>
    /// Applies the active locale's font/material/direction to this TMP_Text, and re-applies automatically when
    /// the selected locale changes. Handles the FONT only — the text/string is managed elsewhere (e.g. a
    /// LocalizeStringEvent, or code). Subscribes in OnEnable, unsubscribes in OnDisable (no persisted wiring).
    /// </summary>
    [AddComponentMenu("TK Localization/Font Localizer")]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FontLocalizer : MonoBehaviour
    {
        [SerializeField] private LocaleFontMap _map;

        private TMP_Text _text;

        private void OnEnable()
        {
            if (!_text) _text = GetComponent<TMP_Text>();
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            Apply();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }

        private void OnSelectedLocaleChanged(Locale locale) => ApplyFor(locale);

        public void Apply() => ApplyFor(LocalizationSettings.SelectedLocale);

        private void ApplyFor(Locale locale)
        {
            if (!_map || !_text) return;
            TmpFontApplier.Apply(_text, _map.Resolve(locale));
        }
    }
}
