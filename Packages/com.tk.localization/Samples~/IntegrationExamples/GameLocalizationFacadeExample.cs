using UnityEngine.Localization.Settings;

namespace TK.Localization.Samples.IntegrationExamples
{
    /// <summary>
    /// A thin, <b>game-owned</b> façade over Unity Localization's string tables — the cleaned-up equivalent
    /// of the reference project's <c>LocalizationHelper</c>. It wraps
    /// <see cref="LocalizedStringDatabase.GetLocalizedString(UnityEngine.Localization.Tables.TableReference, UnityEngine.Localization.Tables.TableEntryReference, object[])"/>
    /// so call sites read <c>GameLoc.Get(GameLoc.UiTable, "play")</c> instead of the full
    /// <c>LocalizationSettings.StringDatabase.GetLocalizedString(...)</c> every time.
    ///
    /// <b>This file belongs in the GAME, not the package.</b> Table names and entry-key conventions are
    /// yours: they depend on how you set up your Localization project, so the package deliberately does not
    /// ship them. Copy this into your project, rename it, and list your real tables as constants. For text
    /// that lives on a TMP object in a scene, prefer the drop-on <c>LocalizedTmpText</c> component; use a
    /// façade like this for strings you build in code (logs, formatted messages, dynamically chosen keys).
    /// </summary>
    public static class GameLocalizationFacadeExample
    {
        // Example table-name constants — replace with YOUR String Table Collection names.
        public const string UiTable = "UI";
        public const string DialogueTable = "Dialogue";

        /// <summary>
        /// Returns the localized string for <paramref name="entry"/> in <paramref name="table"/> for the
        /// currently selected locale. This is the synchronous accessor: it assumes Localization has finished
        /// initializing (e.g. after <see cref="LocaleService.InitializeAsync"/>) and the table's assets are
        /// loaded — otherwise Unity Localization may block to load them. <paramref name="args"/> feeds Smart
        /// String / <c>string.Format</c> placeholders.
        /// </summary>
        public static string Get(string table, string entry, params object[] args) =>
            LocalizationSettings.StringDatabase.GetLocalizedString(table, entry, args);
    }
}
