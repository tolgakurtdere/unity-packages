using UnityEngine;

namespace TK.Localization.Samples.IntegrationExamples
{
    /// <summary>
    /// The one-time locale bootstrap you run at startup. It constructs a <see cref="LocaleService"/> over a
    /// <see cref="PlayerPrefsLocalePersistence"/> and awaits <see cref="LocaleService.InitializeAsync"/>,
    /// which waits for Unity Localization to finish initializing, then applies the chosen locale:
    /// saved (if still available) → device language (if available) → first available.
    ///
    /// Do this once, early. Keep the <see cref="LocaleService"/> instance around (a field on a bootstrapper,
    /// a service locator, your DI container) and hand it to whatever changes the language — e.g. the
    /// <see cref="LanguageSelectorExample"/>. In-scene <c>FontLocalizer</c> / <c>LocalizedTmpText</c>
    /// components need no reference to it: they subscribe to Unity Localization's own events and refresh
    /// whenever the selected locale changes.
    /// </summary>
    public sealed class LocaleBootstrapExample : MonoBehaviour
    {
        // The PlayerPrefs key is the GAME's choice — namespace it to your app so it can't collide with
        // another package's key. The package intentionally has no hardcoded key.
        private const string LocaleKey = "your.game.localeCode";

        public LocaleService Service { get; private set; }

        private async void Start()
        {
            Service = new LocaleService(new PlayerPrefsLocalePersistence(LocaleKey));
            await Service.InitializeAsync();

            Debug.Log($"[LocaleBootstrap] Ready. Current='{Service.Current?.LocaleName}', " +
                      $"IsRtl={Service.IsRtl}, available={Service.Available.Count}.");

            // Now that Available is populated, you can hand Service to your language picker, e.g.:
            //   FindObjectOfType<LanguageSelectorExample>()?.SetService(Service);
        }
    }
}
