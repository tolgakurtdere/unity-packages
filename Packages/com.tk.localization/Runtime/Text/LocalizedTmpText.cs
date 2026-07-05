using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TK.Localization
{
    /// <summary>
    /// All-in-one localized TMP text: resolves a LocalizedString, RTL-shapes it when needed, writes it to the
    /// TMP_Text, and (if a font map is assigned) applies the locale font. Uses LocalizedString.StringChanged,
    /// which fires immediately and on every locale change — no LocalizeStringEvent wiring required.
    /// </summary>
    [AddComponentMenu("TK Localization/Localized TMP Text")]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTmpText : MonoBehaviour
    {
        [SerializeField] private LocalizedString _string;
        [SerializeField] private LocaleFontMap _map;   // optional

        private TMP_Text _text;

        private void OnEnable()
        {
            if (!_text) _text = GetComponent<TMP_Text>();
            _string.StringChanged += OnStringChanged;
        }

        private void OnDisable()
        {
            _string.StringChanged -= OnStringChanged;
        }

        public void Refresh() => _string.RefreshString();

        private void OnStringChanged(string value)
        {
            if (!_text) return;
            _text.text = RtlText.IsRtl(value) ? RtlText.Fix(value) : value;
            if (_map)
                TmpFontApplier.Apply(_text, _map.Resolve(LocalizationSettings.SelectedLocale));
        }
    }
}
