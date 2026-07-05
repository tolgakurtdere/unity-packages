# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-05

### Added

- `LocaleFontInfo` / `LocaleFontMap` — per-locale TMP font bundle (font + optional material + RTL flag) and
  a locale→font map with a **mandatory Fallback**. `Resolve(locale)` returns the mapped font, else the
  **requested** locale's `GetFallbacks()` chain, else Fallback — never null while Fallback is set (fixes the
  reference's wrong-locale fallback and silent-null-font defects).
- `FontLocalizer` — drop-on `TMP_Text` component that applies the active locale's font/material/direction and
  re-applies on `SelectedLocaleChanged` (font only; text managed elsewhere).
- `LocalizedTmpText` — all-in-one component: resolves a `LocalizedString`, RTL-shapes it, writes it to the
  `TMP_Text`, and applies the locale font. Uses `LocalizedString.StringChanged`, so no `LocalizeStringEvent`
  wiring is required. Both components self-subscribe in `OnEnable` and unsubscribe in `OnDisable`.
- `RtlText` — pure `IsRtl(string)` / `Fix(string)` RTL text-shaping pipeline (Arabic/Farsi glyph forms,
  tashkeel, ligatures), preserving rich-text tags and reversing for display; LTR passes through. Self-
  contained — no Unity Localization dependency.
- `LocaleService` — locale selection/persistence/init/events over Unity Localization: `InitializeAsync`
  (saved → device → first available), `SetLocale(string)` / `SetLocale(Locale)`, `Current`, `CurrentCulture`,
  `Available`, `IsRtl` (via `CultureInfo`, no hardcoded code list), and `OnLocaleChanged`. Never throws on an
  unknown code.
- `ILocalePersistence` seam plus `PlayerPrefsLocalePersistence` (game-provided key) and the pure
  `LocaleSelection.Choose` decision helper.
- Samples: **Font Setup** (walkthrough for building a `LocaleFontMap` and wiring the components) and
  **Integration Examples** (`LocaleBootstrapExample`, a generic `LanguageSelectorExample`, and a game-owned
  `GameLocalizationFacadeExample` over string tables).

[0.1.0]: https://github.com/tolgakurtdere/unity-packages/releases/tag/com.tk.localization/0.1.0
