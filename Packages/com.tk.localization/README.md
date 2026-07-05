# TK Localization

A localization framework built on **Unity Localization** (`com.unity.localization`): per-locale **TMP font
swapping** (font + material + text direction), an **RTL text-shaping pipeline** (Arabic/Farsi glyph shaping,
tashkeel, ligatures, rich-text-preserving reversal), and a clean, injectable **locale selection +
persistence** service. It extracts the genuinely reusable *mechanism* from a comprehensive game localization
system and fixes that code's rough edges — no Odin dependency, correct per-locale font fallback, no silent
null fonts, no fragile persistent-listener wiring, no hardcoded game keys, all pure logic unit-tested.

The **game** supplies the *composition*: the Unity Localization Settings, string tables, locale list, font
assets, and its own table-name / key conventions.

## What's inside

| Module | Location | What it gives you |
| --- | --- | --- |
| Font map | `Runtime/Fonts/LocaleFontMap.cs`, `LocaleFontInfo.cs` | `LocaleFontInfo` — one locale's TMP font + optional material + RTL flag (ScriptableObject). `LocaleFontMap` — locale→font map with a **mandatory Fallback**; `Resolve(locale)` returns the mapped font, else the **requested** locale's fallback chain, else Fallback — never null while Fallback is set. |
| Font components | `Runtime/Fonts/FontLocalizer.cs`, `Runtime/Text/LocalizedTmpText.cs` | `FontLocalizer` — drop-on `TMP_Text` component that applies the locale font/material/direction and re-applies on every locale change (font only). `LocalizedTmpText` — all-in-one: resolves a `LocalizedString`, RTL-shapes it, writes it to the text, and swaps the font — no `LocalizeStringEvent` wiring needed. |
| RTL | `Runtime/Rtl/RtlText.cs` (+ `Runtime/Rtl/` pipeline) | `RtlText` — pure `IsRtl(string)` / `Fix(string)`. Shapes Arabic/Farsi (glyph forms, tashkeel, ligatures), preserves rich-text tags, reverses for display; LTR passes through. Self-contained — no Unity Localization dependency. |
| Locale service | `Runtime/Locale/LocaleService.cs`, `LocaleSelection.cs`, `ILocalePersistence.cs`, `PlayerPrefsLocalePersistence.cs` | `LocaleService` — selection/persistence/init/events over Unity Localization; `IsRtl` via `CultureInfo` (no hardcoded code list). `ILocalePersistence` seam + `PlayerPrefsLocalePersistence` (game-provided key). `LocaleSelection.Choose` — pure saved→device→first decision. |

## Install

Add it via Package Manager → **Add package from git URL**:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.localization
```

Pinned to a released version:

```
https://github.com/tolgakurtdere/unity-packages.git?path=Packages/com.tk.localization#com.tk.localization/0.1.0
```

Its dependencies — `com.unity.localization` and `com.unity.ugui` (which provides TextMeshPro in Unity 6) —
are both on Unity's **default registry**, so there is **no scoped registry to add**; the git URL is all you
need. This package ships the localization *mechanism* only — **the game sets up its own Unity Localization
project**: the Localization Settings asset, Available Locales, String Table Collections, and the TMP font
assets. To see this package's EditMode tests in your own project's Test Runner, add `"com.tk.localization"`
to your project's `Packages/manifest.json` `testables`.

## Quickstart

1. Set up Unity Localization in your project (`Edit → Project Settings → Localization`): create the
   Localization Settings, add your locales, and create a String Table Collection with your entries.
2. Bootstrap the locale service once at startup (saved → device → first available):

   ```csharp
   var service = new LocaleService(new PlayerPrefsLocalePersistence("your.game.localeCode"));
   await service.InitializeAsync(); // waits for Localization init, then applies the chosen locale
   ```

3. On a `TMP_Text`, add **`LocalizedTmpText`** (`Add Component → TK Localization → Localized TMP Text`),
   assign a **Localized String** (your table + entry) and a **LocaleFontMap** (see **Fonts**). That's it —
   the text and font now follow the selected locale.

To switch language at runtime, call `service.SetLocale("tr")` (or pass a `Locale`); every component updates
on its own. See the **Integration Examples** sample for a generic language picker.

## Fonts

`LocaleFontMap` is a ScriptableObject (`Assets → Create → TK/Localization/Locale Font Map`) that maps each
locale to a `LocaleFontInfo` (font + optional material + RTL flag). It has a **mandatory Fallback**:

- `Resolve(locale)` returns the mapped `LocaleFontInfo` for that locale; if unmapped, it walks the
  **requested** locale's `GetFallbacks()` chain; if still nothing, it returns **Fallback**.
- With Fallback assigned it **never returns null**, so an unmapped locale still renders with a real font —
  no silent missing-font. If Fallback is left empty, `Resolve` logs an error and returns null (the only null
  path — a config mistake, not per-locale silence).

The font components apply the resolved info to the `TMP_Text`: `font`, `fontSharedMaterial` (only when the
info's Material is set), and `isRightToLeftText` (from the info's RTL flag). See the **Font Setup** sample
for the full walkthrough.

## RTL

`RtlText` is the pure shaping API:

```csharp
if (RtlText.IsRtl(value)) value = RtlText.Fix(value); // shape Arabic/Farsi for a left-to-right text box
```

`Fix` shapes glyphs by position, restores tashkeel, resolves ligatures, preserves rich-text tags, and
reverses the flow for display; non-RTL input (including null/empty) is returned unchanged. **`LocalizedTmpText`
does this for you** — it auto-shapes each resolved string, so you rarely call `RtlText` directly. The shaper
reuses a static buffer and is **main-thread-affine** — call it from Unity's main thread.

## Locale selection

`LocaleService` owns selection and persistence over Unity Localization:

- `InitializeAsync()` — awaits `LocalizationSettings.InitializationOperation`, then applies
  `LocaleSelection.Choose(saved, device, available)`: the saved code if still available, else the device
  language, else the first available locale.
- `SetLocale(string)` — returns `false` and does nothing if the code isn't available; otherwise sets the
  selected locale, persists the code, and raises `OnLocaleChanged`. `SetLocale(Locale)` is the typed form.
- `Current` / `CurrentCulture` / `Available` / `IsRtl` — `IsRtl` is `CurrentCulture.TextInfo.IsRightToLeft`
  (culture-driven, **no hardcoded `"ar"` list**). Never throws on an unknown code.

Persistence is a seam: `ILocalePersistence` with the default `PlayerPrefsLocalePersistence`, whose
**PlayerPrefs key is the game's choice** (namespace it to your app). Swap in your own storage (e.g. a save
system) by implementing the interface — the package stays standalone (no `com.tk.core` dependency).

## Gotchas

- **Main-thread-affine.** Unity Localization, TMP, and the RTL shaper are main-thread. Call the service and
  components from Unity's main thread (marshal from background callbacks first).
- **You provide the Unity Localization project config.** This package is the mechanism, not the content:
  create the Localization Settings, locales, String Table Collections, and font assets yourself. Without
  Available Locales, `InitializeAsync` has nothing to select.
- **`LocalizedTmpText` needs no `LocalizeStringEvent`.** It subscribes to its `LocalizedString.StringChanged`
  (Unity's own event — fires immediately and on every locale change), so string + RTL + font all refresh
  automatically. Don't also add a `LocalizeStringEvent` to the same field.
- **`FontLocalizer` is font-only.** It applies the locale font/material/direction but does **not** set the
  text — use it when the string is managed elsewhere. For all-in-one, use `LocalizedTmpText`.
- **Assign the map's Fallback.** A `LocaleFontMap` with no Fallback is the one case `Resolve` returns null
  (and logs an error). Always set it.

## v2 reserves

Reserved, not built (no API break expected):

- **Editor auto-wire tool** for bulk-tagging existing prefabs with the localizer components.
- **RTL as a separate `TK.Localization.Rtl` asmdef** if standalone reuse (shaping without Unity
  Localization/TMP) is wanted.
- **Addressable / async font loading** for large per-locale font atlases.
- **A ready-made `LanguageSelector` prefab** (not just the sample script).
- **A `com.tk.core` `ISaveSystem` persistence adapter** as a sample (wiring `ILocalePersistence` to
  core.Save) — kept out of the standalone package.
