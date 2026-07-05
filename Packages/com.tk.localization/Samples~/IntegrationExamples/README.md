# Integration Examples

Reference implementations that show how a game composes on top of `com.tk.localization`'s mechanism: a
**generic language picker**, the **locale bootstrap**, and a **game-owned string-table façade**. Copy what
you need into your project and adapt it — these are examples, not drop-in production code.

The package supplies the *mechanism* (font swapping, RTL shaping, locale selection/persistence). The game
supplies the *composition*: your Unity Localization Settings, string tables, locale list, font assets, and
your own table-name / key conventions.

## LocaleBootstrapExample

The one-time startup bootstrap. Constructs a `LocaleService` over a `PlayerPrefsLocalePersistence` and
awaits `InitializeAsync()`, which waits for Unity Localization to initialize and then applies the chosen
locale (saved → device → first available):

```csharp
var service = new LocaleService(new PlayerPrefsLocalePersistence("your.game.localeCode"));
await service.InitializeAsync();
```

The PlayerPrefs key is **the game's choice** — namespace it to your app. Keep the `service` instance around
and hand it to whatever changes the language. In-scene components (`FontLocalizer` / `LocalizedTmpText`)
need no reference to it — they subscribe to Unity Localization's own events and refresh on their own.

## LanguageSelectorExample

A generic uGUI language picker (a plain `MonoBehaviour`, not the reference's game-specific `PopupBase`). It
lists `service.Available` and calls `service.SetLocale(locale)` on click:

```csharp
selector.SetService(service); // service.Available is populated after InitializeAsync()
```

`SetLocale` persists the choice and raises `OnLocaleChanged`, so every `FontLocalizer` / `LocalizedTmpText`
in the scene updates automatically — the picker doesn't touch them.

## GameLocalizationFacadeExample

A thin, **game-owned** static façade over string tables — the cleaned-up equivalent of the reference's
`LocalizationHelper`. It wraps the string-database call so code reads
`GameLoc.Get(GameLoc.UiTable, "play")`:

```csharp
string label = GameLocalizationFacadeExample.Get(GameLocalizationFacadeExample.UiTable, "play");
```

> Table names and entry-key conventions are **yours** — they depend on your Localization project, so this
> file lives in the **game**, not the package. For text on a TMP object in a scene, prefer the drop-on
> `LocalizedTmpText` component; use a façade like this for strings you build in code (logs, formatted
> messages, dynamically chosen keys).

## Component wire-up one-liners

These are the in-scene components you drop on TMP objects — no code, just the Inspector:

- **`LocalizedTmpText`** — add to a `TMP_Text`, assign a **Localized String** (pick your table + entry), and
  optionally a **LocaleFontMap**. It resolves the string, RTL-shapes it when needed, writes it to the text,
  and swaps the font per locale — all via `LocalizedString.StringChanged`, so no `LocalizeStringEvent`
  wiring is required.
- **`FontLocalizer`** — add to a `TMP_Text` whose string is managed elsewhere (a `LocalizeStringEvent`, or
  your code) and assign a **LocaleFontMap**. It applies only the locale's font/material/direction, and
  re-applies whenever the selected locale changes.

See the **Font Setup** sample for how to build the `LocaleFontMap` these reference.
