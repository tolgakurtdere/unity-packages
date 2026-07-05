using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TK.Localization.Samples.IntegrationExamples
{
    /// <summary>
    /// A <b>generic</b> runtime language picker: spawns one uGUI button per available locale and switches to
    /// it on click. This is the cleaned-up, package-friendly form of the reference project's
    /// <c>LanguageSelector</c> — the reference was a game-specific <c>PopupBase</c>; this is a plain
    /// <see cref="MonoBehaviour"/> that touches nothing but a <see cref="LocaleService"/> and uGUI, so it
    /// drops into any UI. Copy it and restyle to taste (this is a <b>Sample</b>, not production UI).
    ///
    /// Wire-up: assign a <see cref="Button"/> to use as the row template (its child <c>TMP_Text</c> is the
    /// label) plus its parent container, then hand the same <see cref="LocaleService"/> you bootstrapped
    /// elsewhere via <see cref="SetService"/>. It lists <see cref="LocaleService.Available"/> (populated once
    /// <see cref="LocaleService.InitializeAsync"/> has run) and calls
    /// <see cref="LocaleService.SetLocale(UnityEngine.Localization.Locale)"/> on click — which persists the
    /// choice and raises <see cref="LocaleService.OnLocaleChanged"/>, so every <c>FontLocalizer</c> /
    /// <c>LocalizedTmpText</c> in the scene refreshes on its own.
    /// </summary>
    public sealed class LanguageSelectorExample : MonoBehaviour
    {
        [Tooltip("A Button (with a child TMP_Text) used as the per-locale row template. It is cloned per " +
                 "locale; the original stays hidden.")]
        [SerializeField] private Button _buttonTemplate;

        [Tooltip("Parent the cloned buttons under. Defaults to the template's parent when left empty.")]
        [SerializeField] private Transform _container;

        // The LocaleService is created and initialized by your bootstrap (see LocaleBootstrapExample); this
        // picker never owns one. Hand it in before building the list.
        private LocaleService _service;

        public void SetService(LocaleService service)
        {
            _service = service;
            Build();
        }

        private void Build()
        {
            if (_service == null || _buttonTemplate == null) return;

            var parent = _container != null ? _container : _buttonTemplate.transform.parent;
            _buttonTemplate.gameObject.SetActive(false); // template is a blueprint, never shown

            var current = _service.Current; // used to disable the active locale's button
            foreach (var locale in _service.Available)
            {
                if (locale == null) continue;

                var row = Instantiate(_buttonTemplate, parent);
                row.gameObject.SetActive(true);

                // Locale.LocaleName is the human-readable label (e.g. "English (en)"); Identifier.Code is
                // the code SetLocale(string) takes. Here we pass the Locale object directly.
                var label = row.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = locale.LocaleName;

                row.interactable = locale != current;

                var captured = locale; // capture per-iteration for the closure
                row.onClick.AddListener(() => _service.SetLocale(captured));
            }
        }
    }
}
