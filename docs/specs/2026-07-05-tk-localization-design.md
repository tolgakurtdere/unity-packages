# com.tk.localization v0.1.0 — Design Spec

Status: APPROVED by Tolgahan 2026-07-05 (direction + all 4 decisions + the judgment calls). Committed to `docs/specs/` (referenceable), following the standard set with the other TK packages. Reviewed/executed Opus-only.

## Purpose

A reusable, professional localization package for TK games, built on Unity's Localization package (`com.unity.localization`): per-locale **font swapping** (TMP font + material + text direction), an **RTL text-shaping pipeline** (Arabic/Farsi glyph shaping, tashkeel, ligatures, rich-text-preserving reversal), and a clean, injectable **locale selection + persistence** service. It extracts the genuinely reusable *mechanism* from the reference project's comprehensive-but-not-UPM-grade localization code, and deliberately fixes that code's defects (hard Odin dependency, a wrong-locale font fallback bug, silent null-font failures, fragile persistent-listener wiring, hardcoded game keys, no test seams). The game supplies *composition*: the Unity Localization Settings, string tables, locale list, font assets, and its own table-name/key conventions.

## Reference system (analyzed, read-only)

`g-brain_test_5`, deep read-only teardown performed 2026-07-05 (~1200 LOC + 20 RTL helpers). Two layers: (A) **reusable font+RTL mechanism** under `Assets/UnicoStudio/UnicoLibs/UnityLocalizationExtensions/` — `FontLocalizer.cs` (per-locale TMP font/material/RTL applier), `TextMeshProFontMapInfoAsset.cs` (Locale→font mapping SO with a lazy dict), `TextMeshProFontInfoAsset.cs` (one locale's font/material/RTL), plus the RTL pipeline (`RTLSupport`, `GlyphFixer`, `TashkeelFixer`, `LigatureFixer`, `RichTextFixer`, `TextUtils`, `FastStringBuilder`); (B) **game-specific façade** — `LocalizationHelper.cs` (hardcoded table names, `unico.braintest` PlayerPrefs key, `LevelType` coupling, `IsRTL == "ar"`), `LanguageSelector.cs` (PopupBase UI), `BrainQuestTextLocalizer`/`FailFlowWarningTextLocalizer` (game templates), `Tool_UiLocalization.cs` (editor prefab auto-wiring via persistent UnityEvent listeners). Built on `com.unity.localization` 1.5.9. 18 locales incl. ar/zh-TW/ja/ko/th/tr.

Defects this package fixes (verified in the teardown):
1. **Odin hard dependency** — `[AssetSelector]`/`[Required]`/`[ListDrawerSettings]` in runtime code (`FontLocalizer.cs`, `TextMeshProFontMapInfoAsset.cs`). The repo standard is Odin-free (verified: no `Sirenix`/`OdinInspector` anywhere in shipped packages). **Fix: strip Odin, use built-in attributes / a small PropertyDrawer.**
2. **Wrong-locale font fallback** — when the requested locale's font is missing, `TextMeshProFontMapInfoAsset`/`FontLocalizer` use `LocalizationSettings.SelectedLocale.GetFallbacks()` instead of the *requested* locale's fallbacks (`FontLocalizer.cs:56,62`; `TextMeshProFontMapInfoAsset.cs:42`). **Fix: use `requestedLocale.GetFallbacks()`.**
3. **Silent null font** — an unmapped locale returns `null`, and the text renders with no font (`TextMeshProFontMapInfoAsset.cs:51`, `FontLocalizer.cs:76`). **Fix: a mandatory explicit fallback `LocaleFontInfo` — never null for an unmapped locale.**
4. **Fragile binding** — the editor tool injects `FontLocalizer.Localize` as a persistent UnityEvent listener (`Tool_UiLocalization.cs:239`); a signature change silently breaks prefabs. **Fix: a runtime component that subscribes to `OnSelectedLocaleChanged`/`LocalizedString.StringChanged` in `OnEnable` — no persisted wiring.**
5. **Hardcoded game specifics** — `"unico.braintest.languageCode"` key, table names, `LevelType`, `IsRTL == "ar"`. **Fix: game-provided persistence key via a seam; RTL via `CultureInfo.TextInfo.IsRightToLeft`, no hardcoded code list.**
6. **No test seams; text-mutation conflated with font swap.** **Fix: pure RTL + font-map + selection logic (unit-tested); RTL shaping separated from font application.**

## Locked decisions

1. **Separate package `com.tk.localization`** (not a `com.tk.core` module). `com.unity.localization` is a heavy dependency (it pulls its own Addressables-backed string-table infrastructure); forcing it on every `com.tk.core` consumer — including one that only wants Save or Utilities — is wrong, and package.json dependencies are package-level (the à-la-carte asmdef split does not scope them). Every heavy-dep TK system (iap/ads/remoteconfig/analytics/notification) is its own package for exactly this reason.
2. **RTL shaping pipeline is in v1.** It is the hardest, highest-value part (Arabic/Farsi markets); the reference already has a working pipeline to port, so deferring it would waste that.
3. **Depend on Unity Localization directly; test the pure logic.** `com.unity.localization` is the substrate (like `com.unity.mobile.notifications` for the notification package), not a swappable backend — no `ILocalizationProvider` wrapper (that would be over-abstraction; nobody swaps Unity Localization). The pure logic (RTL shaping, font-map resolution, locale-selection decision, persistence) is unit-tested; the thin `LocalizationSettings.SelectedLocale` touch is review-verified.
4. **Locale persistence is game-owned via a seam.** `ILocalePersistence` (default `PlayerPrefsLocalePersistence` with a **game-provided key**), so the package stays standalone — no `com.tk.core`/`ISaveSystem` dependency, no hardcoded key.
5. **Dependencies:** `com.unity.localization` (newest stable — reference 1.5.9; verify + pin at execution) and `com.unity.ugui` (2.0.0, provides TextMeshPro in Unity 6). Both on Unity's default registry → no scoped registry. No Odin, no `com.tk.core`.
6. **Judgment calls (approved):** clean names (`LocaleFontMap`/`LocaleFontInfo`/`FontLocalizer`/`LocalizedTmpText`/`RtlText`/`LocaleService`) instead of the reference's long `TextMeshProFontMapInfoAsset` names; RTL lives in a `Runtime/Rtl/` subfolder of the single `TK.Localization` asmdef (not a separate asmdef in v1); the editor auto-wire tool is deferred to v2 (the auto-subscribing components remove its main need); ship a `LocalizedTmpText` all-in-one component (drop-on; no separate `LocalizeStringEvent` wiring required) alongside the pure `RtlText` API.
7. **No SDK/vendor type leak beyond Unity's own:** the public API uses Unity Localization types (`Locale`, `LocalizedString`) and TMP types (`TMP_FontAsset`) — those ARE the domain — but no third-party (Odin) types appear anywhere.

## Package layout

```
Packages/com.tk.localization/
  package.json                       # com.tk.localization 0.1.0, unity 6000.0, deps: com.unity.localization (newest), com.unity.ugui 2.0.0
  Runtime/
    TK.Localization.asmdef           # rootNamespace TK.Localization; references the Unity Localization + TextMeshPro asmdefs (verify exact names at execution); autoReferenced true
    Fonts/
      LocaleFontInfo.cs              # one locale's TMP font + material + RTL flag (ScriptableObject)
      LocaleFontMap.cs              # Locale→LocaleFontInfo map + mandatory Fallback; Resolve() with correct fallback chain
      FontLocalizer.cs             # MonoBehaviour: applies the locale font on enable + OnSelectedLocaleChanged
    Text/
      LocalizedTmpText.cs          # MonoBehaviour: LocalizedString → RTL-fix → TMP.text (+ optional font) via StringChanged
    Locale/
      ILocalePersistence.cs        # persistence seam
      PlayerPrefsLocalePersistence.cs  # default impl, game-provided key
      LocaleService.cs             # selection/persistence/init/events; IsRtl via CultureInfo
      LocaleSelection.cs           # pure decision helper: choose locale from saved/device/available
    Rtl/
      RtlText.cs                   # public static: IsRtl(string), Fix(string)
      (internal) GlyphShaper / TashkeelFixer / LigatureFixer / RichTextFixer / RtlStringBuilder  # ported pipeline
  Tests/Editor/
    TK.Localization.Tests.asmdef   # references TK.Localization + TestRunner + nunit
    FakeLocalePersistence.cs
    RtlTextTests.cs
    LocaleFontMapTests.cs
    LocaleSelectionTests.cs
    LocaleServiceTests.cs
  Samples~/
    FontSetup/                     # how to build a LocaleFontMap (+ README)
    IntegrationExamples/           # generic LanguageSelector, LocaleService bootstrap, thin game-façade example (+ README)
  README.md / CHANGELOG.md
```

## Public API surface

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Localization;

// ── Fonts ──
public sealed class LocaleFontInfo : ScriptableObject   // [CreateAssetMenu "TK/Localization/Locale Font Info"]
{
    public TMP_FontAsset Font { get; }
    public Material Material { get; }     // optional material preset; may be null
    public bool RightToLeft { get; }
}

public sealed class LocaleFontMap : ScriptableObject    // [CreateAssetMenu "TK/Localization/Locale Font Map"]
{
    public LocaleFontInfo Fallback { get; }             // mandatory
    /// <summary>Mapped locale → its font; else the requested locale's GetFallbacks() chain; else Fallback. Never null when Fallback is set.</summary>
    public LocaleFontInfo Resolve(Locale locale);
}

[AddComponentMenu("TK Localization/Font Localizer"), RequireComponent(typeof(TMP_Text))]
public sealed class FontLocalizer : MonoBehaviour       // font/material/direction only; for text managed elsewhere
{
    public void Apply();   // resolve + apply for LocalizationSettings.SelectedLocale
}

// ── Text (all-in-one) ──
[AddComponentMenu("TK Localization/Localized TMP Text"), RequireComponent(typeof(TMP_Text))]
public sealed class LocalizedTmpText : MonoBehaviour    // LocalizedString → RTL-fix → TMP.text (+ font if a map is assigned)
{
    public void Refresh();
}

// ── RTL (pure) ──
public static class RtlText
{
    public static bool IsRtl(string text);   // true if the first strong character is RTL (Arabic/Hebrew ranges)
    public static string Fix(string text);   // shape + tashkeel + ligatures + rich-text-preserving reversal
}

// ── Locale selection + persistence ──
public interface ILocalePersistence
{
    string Load();                 // saved locale code, or null if none
    void Save(string localeCode);
}

public sealed class PlayerPrefsLocalePersistence : ILocalePersistence
{
    public PlayerPrefsLocalePersistence(string playerPrefsKey);   // key is the game's choice
}

public sealed class LocaleService
{
    public LocaleService(ILocalePersistence persistence);

    public Task InitializeAsync();          // await LocalizationSettings.InitializationOperation, then apply the chosen locale
    public bool SetLocale(string localeCode);  // false if not available; else set + persist + raise OnLocaleChanged
    public void SetLocale(Locale locale);
    public Locale Current { get; }
    public CultureInfo CurrentCulture { get; }
    public bool IsRtl { get; }              // CurrentCulture.TextInfo.IsRightToLeft — no hardcoded code list
    public IReadOnlyList<Locale> Available { get; }
    public event Action<Locale> OnLocaleChanged;
}

// pure, testable
public static class LocaleSelection
{
    /// <summary>Pick a locale code: savedCode if available, else deviceCode if available, else the first available (or null).</summary>
    public static string Choose(string savedCode, string deviceCode, IReadOnlyList<string> availableCodes);
}
```

`FontLocalizer` subscribes to `LocalizationSettings.OnSelectedLocaleChanged` (`OnEnable` → apply; `OnDisable` → unsubscribe). `LocalizedTmpText` subscribes to its `LocalizedString.StringChanged` (Unity's own event — fires immediately and on every locale change), so string + RTL + font all refresh with no manual wiring. Both use `RequireComponent(TMP_Text)`.

## Behavioral contracts (reference-derived, test-pinned)

- **`LocaleFontMap.Resolve`:** returns the mapped `LocaleFontInfo` for `locale` (by `locale.Identifier.Code`); if unmapped, walks the **requested** locale's `GetFallbacks()` in order and returns the first mapped one; if none match, returns `Fallback`. Never returns null while `Fallback` is assigned. If `Fallback` is null (misconfiguration), it logs an error and returns null — the only null path, and a config error, not a per-locale silent failure. (Fixes reference defects #2, #3.)
- **`FontLocalizer`:** on `OnEnable` caches the `TMP_Text`, subscribes to `OnSelectedLocaleChanged`, and applies `Resolve(SelectedLocale)` — setting `font`, `fontSharedMaterial` (only when the info's `Material` is non-null), and `isRightToLeftText` (from the info's `RightToLeft`). On `OnDisable` it unsubscribes (no leaked handler). `Apply()` re-applies on demand.
- **`LocalizedTmpText`:** on `OnEnable` subscribes to `LocalizedString.StringChanged`; on each callback it takes the resolved string, applies `RtlText.Fix` when `RtlText.IsRtl(value)` is true, assigns to `TMP.text`, and — if a `LocaleFontMap` is assigned — applies the locale font. On `OnDisable` it unsubscribes.
- **`RtlText`:** `IsRtl` returns true when the first strong-directional character is in an RTL range (Arabic/Hebrew), false for LTR/empty/null. `Fix` shapes Arabic/Farsi letters by position, handles tashkeel and ligatures, preserves rich-text tags, and reverses for display; LTR input passes through unchanged. Pure `string`→`string`, no Unity Localization dependency.
- **`LocaleSelection.Choose`:** returns `savedCode` if it is in `availableCodes`; otherwise `deviceCode` if available; otherwise the first available code; otherwise null. Pure.
- **`LocaleService`:** `InitializeAsync` awaits `LocalizationSettings.InitializationOperation`, then selects via `LocaleSelection.Choose(persistence.Load(), CultureInfo.CurrentCulture.TwoLetterISOLanguageName, availableCodes)` and applies it. `SetLocale(code)` returns false and does nothing when the code is not available; otherwise sets `LocalizationSettings.SelectedLocale`, calls `persistence.Save(code)`, and raises `OnLocaleChanged`. `SetLocale(Locale)` sets + persists its code + raises. `IsRtl` is `CurrentCulture.TextInfo.IsRightToLeft`. Never throws on an unknown code.
- **Main-thread affine** (Unity Localization and TMP are main-thread; documented).

## Samples

- `Samples~/FontSetup/` — a walkthrough (README + a small example `LocaleFontMap` + `LocaleFontInfo` layout) showing how to map locales to TMP fonts/materials and set the mandatory fallback.
- `Samples~/IntegrationExamples/`:
  - `LanguageSelectorExample.cs` — a **generic** runtime language picker (plain `MonoBehaviour`/uGUI, not the reference's `PopupBase`) that lists `LocaleService.Available` and calls `SetLocale`.
  - `LocaleBootstrapExample.cs` — awaits `LocaleService.InitializeAsync()` at startup with a `PlayerPrefsLocalePersistence("your.game.localeCode")`.
  - `GameLocalizationFacadeExample.cs` — a thin, game-owned façade over string tables (the cleaned-up equivalent of the reference's `LocalizationHelper`: your table names + key conventions live here, in the game, not the package).
  - README with the wire-up one-liners.

## Testing

EditMode suite (Unity Localization present in the harness — tests use real `Locale` objects and `ScriptableObject` instances; no vendor mocking). Target ≈25–30 tests:
- **`RtlText`** (pure): `IsRtl` true for an Arabic string, false for English/empty/null; `Fix` shapes a known Arabic input to the expected output, preserves a rich-text tag, and passes LTR input through unchanged (port the reference's cases).
- **`LocaleFontMap.Resolve`** (via `Locale.CreateLocale` + `ScriptableObject.CreateInstance<LocaleFontInfo>()`): mapped hit; unmapped-but-fallback-chain hit; unmapped → `Fallback`; never null while `Fallback` set; `Fallback` null → logs error + null (`LogAssert`).
- **`LocaleSelection.Choose`** (pure): saved-available → saved; saved-unavailable-but-device-available → device; neither → first; empty available → null.
- **`LocaleService`** (via `FakeLocalePersistence`): `SetLocale(available)` persists the code + raises `OnLocaleChanged` + returns true; `SetLocale(unknown)` → false, no persist, no event; `InitializeAsync` applies the saved code when available; `IsRtl` reflects the current culture. The `LocalizationSettings.SelectedLocale` assignment itself is review-verified; the decision + persistence + event logic is what these tests pin.
- `FakeLocalePersistence` smoke.

The components (`FontLocalizer`, `LocalizedTmpText`) are exercised where feasible in EditMode (applying a resolved font to a `TMP_Text`); their `OnSelectedLocaleChanged`/`StringChanged` subscriptions are review-verified. RE-VERIFY the Unity Localization members used (`LocalizationSettings.SelectedLocale`/`OnSelectedLocaleChanged`/`AvailableLocales`/`InitializationOperation`, `Locale.Identifier.Code/CultureInfo`, `Locale.GetFallbacks()`, `LocalizedString.StringChanged`) against the installed `com.unity.localization` version at execution; on drift, trust the installed source and note it. Harness gate identical to the other packages (scratch project via `file:`, `-batchmode -runTests -testPlatform EditMode`, never `-quit` with `-runTests`, zero warnings under the package).

## v2+ reserves (no API breaks planned)

- **Editor auto-wire tool** (generalized from the reference's `Tool_UiLocalization`: a configurable font-map path, robust binding) for bulk-tagging existing prefabs.
- **RTL as a separate `TK.Localization.Rtl` asmdef** if standalone reuse (RTL shaping without Unity Localization/TMP) is wanted.
- **Addressable / async font loading** for large per-locale font atlases.
- **A ready-made `LanguageSelector` prefab** (not just a script) in samples.
- **`com.tk.core` `ISaveSystem` persistence adapter** as a sample (wiring `ILocalePersistence` to core.Save) — kept out of the standalone package.
- Additional RTL edge cases (Hebrew niqqud, mixed bidi runs) as they surface in real content.
