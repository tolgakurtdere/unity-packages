# Font Setup

A walkthrough for building a **`LocaleFontMap`** — the asset that tells the localizer which TMP font to use
for each locale — and wiring the `FontLocalizer` / `LocalizedTmpText` components to it.

There is no script to import here: a font map is made of ScriptableObject assets and a couple of components
you drop on TMP objects in the Inspector. This is the sequence.

## 1. Prepare a TMP font asset per locale

For each script your game ships (Latin, Arabic, CJK, Thai, …) you need a **TMP Font Asset** whose atlas
contains that script's glyphs. Create them the usual way: `Window → TextMeshPro → Font Asset Creator`, or
right-click a `.ttf`/`.otf` → `Create → TextMeshPro → Font Asset`. One well-chosen font can cover several
locales (e.g. one Latin font for `en`/`fr`/`de`/`tr`); RTL scripts (Arabic/Farsi) and CJK usually need
their own.

## 2. Create a `LocaleFontInfo` per distinct font

`Assets → Create → TK/Localization/Locale Font Info`. Each asset is **one locale's font bundle**:

- **Font** — the `TMP_FontAsset` for this script (required).
- **Material** — an optional material preset (outline, gradient, …). Leave empty to use the font's default
  material; the localizer only overrides the material when this is set.
- **Right To Left** — tick for Arabic/Farsi/Hebrew fonts. This sets `TMP_Text.isRightToLeftText` when the
  font is applied. (Text *shaping* is separate — `LocalizedTmpText` handles that via the RTL pipeline.)

Make one per distinct font (not necessarily one per locale — several locales can share a `LocaleFontInfo`).

## 3. Create the `LocaleFontMap`

`Assets → Create → TK/Localization/Locale Font Map`. This maps locales to their `LocaleFontInfo`:

- **Fallback** — the `LocaleFontInfo` used for any locale you don't map explicitly. **Mandatory** — assign
  it. With a fallback set, `Resolve` never returns null, so an unmapped locale still renders with a real
  font (it never silently loses its font). Point it at your most-covering font (usually your Latin one).
- **Entries** — add one row per locale you want to override: pick the **Locale** (from your project's
  Available Locales) and the **Locale Font Info** to use for it. Locales you don't list fall through to the
  requested locale's own fallback chain, and finally to **Fallback**.

Example layout for a game shipping English, Turkish, Arabic, and Japanese, sharing one Latin font:

| Slot | Locale | Locale Font Info |
| --- | --- | --- |
| Fallback | — | `Font_Latin` (covers en, tr, and anything unmapped) |
| Entry | Arabic (ar) | `Font_Arabic` (Right To Left ✓) |
| Entry | Japanese (ja) | `Font_CJK` |

English and Turkish aren't listed — they resolve to **Fallback** (`Font_Latin`). Arabic and Japanese get
their own fonts.

## 4. Attach the components

On each `TMP_Text` you want localized:

- **`LocalizedTmpText`** (`Add Component → TK Localization → Localized TMP Text`) — assign a **Localized
  String** (your table + entry) and the **LocaleFontMap** from step 3. It writes the localized, RTL-shaped
  text *and* swaps the font per locale.
- **`FontLocalizer`** (`Add Component → TK Localization → Font Localizer`) — use this instead when the
  string is managed elsewhere (a `LocalizeStringEvent`, or your own code) and you only want the font swap.
  Assign just the **LocaleFontMap**.

Both re-apply automatically when the selected locale changes (they subscribe in `OnEnable`, unsubscribe in
`OnDisable`) — no manual event wiring.

## Notes

- The map resolves against the **requested** locale's `GetFallbacks()` chain (not the globally selected
  locale's), so per-locale fallbacks behave correctly.
- If you forget to assign **Fallback**, `Resolve` logs an error and returns null (the only null path — a
  configuration mistake, not a per-locale silent failure). Always assign it.
- See the **Integration Examples** sample for the `LocaleService` bootstrap and a generic language picker.
