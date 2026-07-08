# TK Packages — Roadmap

Planned work across the TK Unity package suite. This file is the durable, referenceable record — any developer or agent can be pointed here to pick up a piece of work. It complements each package's own README (which documents shipped behavior and per-package "reserves").

## Inclusion criterion for new packages

A package belongs here only if it is:

- **Genuinely cross-project** — needed by essentially every game, not one genre or title.
- **Reusable via seams** — the package owns the *mechanism*; the game supplies *composition* (content, config, backend adapters) through interfaces/ScriptableObjects/delegates, following the pattern already proven in `com.tk.core`, `com.tk.iap`, and `com.tk.ads`.
- **Open to light per-project tweaks** — swap an implementation, override a hook, or feed different config without editing package code.

Game-specific logic does **not** belong in a package. Deliberately excluded for this reason: `OrthoCameraFitter` (camera framing depends on the game's content, board size, and safe areas), per-game level loading (already kept out of `com.tk.core` — varies per title: JSON/scene/prefab/daily), and anything whose behavior can't be expressed as mechanism + game-supplied composition.

## Shipped

| Package | Version | Notes |
| --- | --- | --- |
| `com.tk.core` | 0.4.0 | Utilities / Save / UI / App modules (à la carte asmdefs); sliding tab bar + animated presenter |
| `com.tk.toolbar` | 0.1.0 | Editor time-scale + configurable scene buttons |
| `com.tk.iap` | 0.1.1 | AppLovin-independent; Unity IAP v5 wrapper (Unity IAP 5.4.0) |
| `com.tk.ads` | 0.1.2 | AppLovin MAX mediation (banner/interstitial/rewarded) (AppLovin MAX 8.6.4) |
| `com.tk.remoteconfig` | 0.1.0 | Backend-agnostic remote-config façade; feeds the IAP/Ads resolver seams (Firebase adapter as a sample) |
| `com.tk.analytics` | 0.1.0 | Backend-agnostic analytics façade with consent gate + loss-free buffering; unifies the IAP/Ads monetization event stream (Firebase/Adjust adapters as samples) |
| `com.tk.notification` | 0.1.0 | Local mobile-notification framework (scheduling, quiet-hours, channels, permission, launch routing) on `com.unity.mobile.notifications`; no-op on non-mobile targets; local-only (no push in v1) |
| `com.tk.localization` | 0.1.0 | Localization framework on `com.unity.localization`: per-locale TMP font swapping, RTL text-shaping pipeline (Arabic/Farsi), injectable locale selection/persistence; standalone (no `com.tk.*` deps), no scoped registries |
| `com.tk.audio` | 0.3.0 | Audio framework: Music/Sfx channels + optional save-backed settings, ref-counted temporary mute (ads seam), pooled one-shot SFX (variations/pitch/retrigger throttle), crossfading music with playlists + per-entry Addressables for music; requires `com.tk.core`. Play-mode verified in game-shikaku |

## Planned features in shipped packages

### com.tk.core

Findings from the first real game integration (game-shikaku, a level-based mobile puzzle; `com.tk.core` 0.1.0 + `com.tk.toolbar` 0.1.0 tag-pinned, Unity 6000.5). Everything is additive/non-breaking unless noted. **The v0.1.1 quick wins shipped 2026-07-06** — `OnBootAsync()` boot-policy hook, `LevelProgressService` saveKey + advance-policy seam, `AppBootstrapper` scene-name overrides + editor splash skip, domain-reload-off static resets, README clarifications. **The v0.2.0 headline shipped 2026-07-06 as 0.2.0** — `AppRootBase` decomposition (`AppFlowBase : AppRootBase`, zero API break; three adoption tiers AppContext → AppRootBase → AppFlowBase), the standalone `NavigationGate` transition lock, and the level lifecycle hooks (`OnBeforeLevelStartAsync` with veto + `OnAfterLevelEndAsync`); see the package CHANGELOG. **0.3.0 shipped 2026-07-07** — the sliding tab bar system (`TabBarView`/`TabBarConfig`/`LayoutSlideNavigator`/`IOrderedTabTransition`) promoted from game-shikaku into `TK.Core.UI` after play-mode verification there, plus `UIBase.SetRaycastsBlocked` and the `AppRootBase.CompletedAwaitable` helpers; the game-side coalescing loop deliberately stayed composition glue (same promotion discipline as `ISceneFlow` — no package `TabNavigationController` until a second consumer proves the seam shape). **0.3.1 + 0.4.0 shipped 2026-07-07** — tab bar hardening (two-level back-input control, config guards, teardown safety) with DOTween/PrimeTween tab bar samples, then `AnimatedTabButtonPresenter` (sprite swap + bottom-pivot scale + `LayoutElement` width reflow + label fade) and per-tab `TabBarConfig` icons, play-mode verified in game-shikaku before tagging. Remaining:

**App module**

- **`ISceneFlow` seam (deferred; decision re-affirmed 2026-07-06)** — interface over the scene flow, with the current behavior as an `AdditiveSceneFlow` default; keep the static `SceneLoader` as a thin facade. Purpose: make "Splash → Main → Game is a default, not a requirement" true in code, not just in docs. Deliberately **not** built yet, for two reasons beyond YAGNI: (1) the seam has **no package-side consumer** — `AppRootBase`/`AppFlowBase` never call `SceneLoader`, so today the interface would be surface without mechanism (a game can define its own five-line equivalent locally with zero loss); (2) with one consumer the divergent verbs can't be pinned — in a single-scene model "unload game" *is* "load menu", and a one-scene (content-swap) game would no-op every verb. Build trigger: a **second consumer whose scene model actually diverges**. Until then, a game wanting the seam implements it game-side first and the proven shape gets promoted into the package. Do not ship a speculative `SingleSceneFlow` sample before that.

**UI module**

- **UICatalog provider seam (low priority)** — the catalog is Addressables-only (`AssetReferenceGameObject`); a direct-reference variant would let small/prototype games adopt TK.Core.UI without any Addressables setup.
- **Back-input polling (very low)** — `UIManager.Update` polls Keyboard/Gamepad every frame; could move to an InputAction binding.

**AppContext**

- **Keyed registrations (optional)** — `Register<T>` is one-instance-per-type; a game with multiple progression tracks needs a game-side hub/wrapper today. Either document the hub pattern in the README or add `Register<T>(string key)` / `Get<T>(string key)` overloads.

### com.tk.ads
See the package README's "v2 reserves" section for the committed detail. Summary:
- **App Open ads (v1.1)** — MAX app-open format, with safe-exit / fast-return heuristics so returning from your own fullscreen ad doesn't immediately trigger an app-open ad.
- **AdMob backfill (v2, if PM wants it)** — a `CompositeAdsGateway(primary, fallback)` implementing `IAdsGateway`: per-show fallback chain (MAX show-fail → AdMob non-mediation), with both gateways feeding `RevenuePaid` so backfill revenue is never dropped from reporting. The seam was designed for exactly this; no public API break expected.

### com.tk.iap
- **Subscriptions / VIP / PlayPass** — v1 is Consumable + NonConsumable only; the catalog already carries `ProductType` (warns on Subscription) and `Entitlements` is generic, so subs can be added without an API break. Needs: subscription state/expiry tracking, store-diff on fetch, VIP = any-of-named-subs.
- **`OnPurchaseDeferred` seam event** — surface Ask-to-Buy / deferred purchases (currently they arrive later as pending).
- **Async item handlers** — for server-authoritative wallets (v1 handlers are synchronous by deliberate scope decision).
- **Startup purchases-fetch retry** — Google ownership sync currently doesn't retry on a flaky first fetch.

### com.tk.audio
Backlog derived from the g-brain MasterAudio usage teardown (`PlaySoundAndForget` ×1723, handle-based `PlaySound` ×46 in 39 files, `[SoundGroup]` attribute ×1494) + the "BT5 Ses Ekleme" agent doc.

**Shipped 2026-07-08:** **0.1.0** (Music/Sfx channels, optional save-backed settings, ref-counted mute, pooled one-shot SFX with variations/pitch/retrigger-throttle, crossfading music + playlists, per-entry Addressables for music). **0.2.0** — SFX control + editor authoring: loop/stop SFX via `AudioHandle`, per-key `maxConcurrentVoices` cap, `PlaySfx(key, delay)`, `StopSfx`/`StopAllSfx`, `[AudioKey]`/`[AudioPlaylistKey]` drawers (first `Editor/` asmdef), drag-drop catalog auto-populate. **0.3.0** — music/settings polish: ad-mute now PAUSES music (settings-mute STOPS + replays from top), `PauseMusic`/`ResumeMusic`, `FadeChannelVolume`, settings `Changed` event, `PreloadAsync`.

**v0.4 — deferred, each trigger-gated (build when a consumer actually needs it, not speculatively):**
- **Level-scoped additional catalogs** — `RegisterAdditionalCatalog`/`Unregister`, the runtime counterpart to MA's `DynamicSoundGroupCreator` (per-level sound sets loaded on enter, freed on exit). **Trigger:** a large-content game (many levels, per-level unique audio) adopts the package; casual single-catalog games (game-shikaku) don't need it.
- **Addressable SFX + delayed-release warm window** — on-demand SFX loading with an MA-style "keep resident N s after last use" cache (v0.1–0.3 only stream music addressably; SFX must be direct clips). **Trigger:** a game with a large streamed SFX library.
- **Weighted variation selection** — per-clip weights on an entry's variation set (reference had `AddSoundGroup(path, weight)`); today the random pick is uniform. Cheap; do opportunistically when a consumer wants non-uniform variations.
- **`ReleasePreload(key)` / preload eviction** — `PreloadAsync` currently holds the resident clip until `Dispose`; add selective release for menu→gameplay handoffs.
- **Ducking presets** — auto-duck music under SFX/voice (today `FadeChannelVolume` is the manual primitive).
- **AudioMixer integration** — route channels through an `AudioMixer` for effects/snapshots.
- **Named categories** — free-form channels beyond Music/Sfx (Voice, UI, Ambient…).
- **Excluded (game-specific):** 3D positional one-shots — the package is 2D-mobile-scoped.

**Assessment:** feature-complete for casual consumers (game-shikaku uses it fully). The first three v0.4 items are exactly the reference project's *large-content* infrastructure (per-level sets, streamed SFX, weighted variations) — deliberately left until a g-brain-style consumer pulls them.

## Candidate new packages

Ordered by recommended priority. Each would follow the standard flow: brainstorm → spec → plan → subagent-driven execution with per-task + whole-branch review.

### 1. com.tk.haptics ⭐ (recommended next)
Thin cross-platform haptic feedback: `Impact(light/medium/heavy)`, `Selection`, `Notification`, with an enable toggle persisted. iOS Taptic + Android vibrate impls.
- **Reusable mechanism:** the whole thing. **Game supplies:** nothing beyond the on/off preference. Small, high-reuse. Note: game-shikaku's `SettingsService` already carries a `Vibration` flag with no consumer wired — a ready first consumer, same as audio's Sound/Music flags were.

### 2. com.tk.transitions
Scene/level transition overlay: async `ShowAsync`/`HideAsync` that gates input during the transition (fade / loading indicator). Reuses/extends `com.tk.core.UI`.
- **Reusable mechanism:** overlay lifecycle + input gating + async sequencing. **Game supplies:** the visual prefab (UICatalog pattern). Could also land as a `com.tk.core.UI` addition rather than a standalone package — decide at brainstorm.

### 3. com.tk.logging (low priority)
Logger façade: levels, categories, release stripping, sink routing (console + optional forward to analytics/crashlytics).
- **Reusable mechanism:** level/category filtering, release-build stripping, sink fan-out. **Game supplies:** sink choice. Lower priority — `Debug.Log` suffices until category filtering / release stripping / crash-forwarding is actually needed.

### 4. com.tk.lives (candidate; build only when a second game actually needs it)
Lives/energy system: count + max, UTC-timestamp regeneration (computed on load/focus via `AppContext.OnAppFocus`, no background tick), consume-on-lose via `OnGameEnded`, refill seams (rewarded ad / coins / IAP), optional infinite-lives window. Persisted via `ISaveSystem`; clock-cheat clamped on load.
- **Reusable mechanism:** counting/regen/persistence/gating seams. **Game supplies:** max, costs, refill sources, UI.
- Note: shikaku's current design has no lives (its pressure mechanic is a level timer + paid continue), so there is no consumer yet — do not build speculatively.

## Notes

- Deep design analyses for shipped packages (reference-system teardowns, defect lists) currently live in gitignored session scratch (`.superpowers/`). If a future package's design leans on one, promote the relevant analysis into a committed `docs/` file so it's referenceable from the repo.
- Both monetization packages (`com.tk.iap`, `com.tk.ads`) are verified in-editor and against the vendor SDK sources, but **not yet on a real device with live store/ad accounts** — validate with sandbox purchases and the MAX Mediation Debugger during the first real game integration.
