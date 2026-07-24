# TK Packages — Roadmap

Planned work across the TK Unity package suite. This file is the durable, referenceable record — any developer or agent can be pointed here to pick up a piece of work. It complements each package's own README (which documents shipped behavior and per-package "reserves").

## Inclusion criterion for new packages

A package belongs here only if it is:

- **Genuinely cross-project** — needed by essentially every game, not one genre or title.
- **Reusable via seams** — the package owns the *mechanism*; the game supplies *composition* (content, config, backend adapters) through interfaces/ScriptableObjects/delegates, following the pattern already proven in `com.tk.core`, `com.tk.iap`, and `com.tk.ads`.
- **Open to light per-project tweaks** — swap an implementation, override a hook, or feed different config without editing package code.

Game-specific logic does **not** belong in a package. Deliberately excluded for this reason: `OrthoCameraFitter` (camera framing depends on the game's content, board size, and safe areas), per-game level loading (already kept out of `com.tk.core` — varies per title: JSON/scene/prefab/daily), and anything whose behavior can't be expressed as mechanism + game-supplied composition.

## Convention: who owns a setting's persistence

Two patterns, chosen by what the state *is* — keep them straight so packages stay consistent:

- **Game-owned (runtime state + `Changed` event; the game persists it).** Use for **user-facing settings-screen toggles** — sound, music, vibration, notifications-on. Games put these in one Settings screen backed by one settings service, which is the single source of truth; a package persisting its own copy creates two sources that fight on boot. The package holds the value as runtime state, the game pushes it on boot and persists it. Applies to `com.tk.audio` (Music/Sfx enabled + volume, since 0.4.0), `com.tk.haptics` (Enabled), `com.tk.analytics` (consent), `com.tk.notification` (enable preference). `com.tk.localization` is the seam variant — game-owned via `ILocalePersistence` with a `PlayerPrefs` default impl.
- **Package-owned (via `ISaveSystem`).** Use for **domain state that is NOT a settings toggle and that the package owns end-to-end** — `com.tk.core` level progress, `com.tk.iap` purchase entitlements. The game has no "settings toggle" for these; the package persists them through the injected `ISaveSystem`. (Note: a settings screen may *show* a purchased entitlement like "Remove Ads" as a switch, but it's owned by `com.tk.iap`, not a free preference.)

Rule of thumb: **if it belongs on the Settings screen as a free on/off/volume the player flips, it's game-owned. If it's progress or a purchase the package computes, it's package-owned.**

## Shipped

| Package | Version | Notes |
| --- | --- | --- |
| `com.tk.core` | 0.7.0 | Utilities / Save / UI / App modules (à la carte asmdefs); sliding tab bar + animated presenter; startup settings (frame rate / sleep timeout / log policy) applied before the splash screen; transition curtain (`RunUnderCurtainAsync`) masking scene/state swaps, now with a pre-covered boot door (`CoverCurtainInstantlyAsync`) for a single animated reveal at app start |
| `com.tk.toolbar` | 0.1.0 | Editor time-scale + configurable scene buttons |
| `com.tk.iap` | 0.1.2 | AppLovin-independent; Unity IAP v5 wrapper (Unity IAP 5.4.0); deferred purchases (Ask to Buy) logged instead of warning |
| `com.tk.ads` | 0.2.0 | AppLovin MAX mediation (banner/interstitial/rewarded) (AppLovin MAX 8.6.4); official no-network FakeAdsGateway for editor/test builds |
| `com.tk.remoteconfig` | 0.1.0 | Backend-agnostic remote-config façade; feeds the IAP/Ads resolver seams (Firebase adapter as a sample) |
| `com.tk.analytics` | 0.1.0 | Backend-agnostic analytics façade with consent gate + loss-free buffering; unifies the IAP/Ads monetization event stream (Firebase/Adjust adapters as samples) |
| `com.tk.notification` | 0.2.0 | Local mobile-notification framework (scheduling, quiet-hours, channels, permission, launch routing) on `com.unity.mobile.notifications`; no-op on non-mobile targets; local-only (no push in v1) |
| `com.tk.localization` | 0.1.0 | Localization framework on `com.unity.localization`: per-locale TMP font swapping, RTL text-shaping pipeline (Arabic/Farsi), injectable locale selection/persistence; standalone (no `com.tk.*` deps), no scoped registries |
| `com.tk.audio` | 0.4.0 | Audio framework: Music/Sfx channels + game-owned settings, ref-counted temporary mute (ads seam), pooled one-shot SFX (variations/pitch/retrigger throttle), crossfading music with playlists + per-entry Addressables for music; requires `com.tk.core`. Play-mode verified in game-shikaku |
| `com.tk.haptics` | 0.2.0 | Cross-platform haptic feedback: `Impact` / `Selection` / `Notification` with an enable toggle + per-type throttling; native iOS (Taptic) + Android (Vibrator) backends behind a testable seam, no-op fallback; standalone (no `com.tk.*` deps), no scoped registries. Android vibrations are classified by `VibrationAttributes` usage (API 33+) so gameplay/notification haptics are not gated by the OS touch-vibration setting, plus a `SystemTouchVibrationDisabled` advisory for settings-screen hints. Device-verified on Android 14 |

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
- **`OnPurchaseDeferred` seam event** — surface Ask-to-Buy / deferred purchases to game UI ("awaiting approval"). 0.1.2 already subscribes and logs them (silencing v5's per-purchase warning); the grant path is covered because approved orders re-arrive as pending. A public event stays deferred until a game wants the UI.
- **Async item handlers** — for server-authoritative wallets (v1 handlers are synchronous by deliberate scope decision).
- **Startup purchases-fetch retry** — Google ownership sync currently doesn't retry on a flaky first fetch.

### com.tk.audio
Backlog derived from the g-brain MasterAudio usage teardown (`PlaySoundAndForget` ×1723, handle-based `PlaySound` ×46 in 39 files, `[SoundGroup]` attribute ×1494) + the "BT5 Ses Ekleme" agent doc.

**Shipped 2026-07-08:** **0.1.0** (Music/Sfx channels, optional save-backed settings, ref-counted mute, pooled one-shot SFX with variations/pitch/retrigger-throttle, crossfading music + playlists, per-entry Addressables for music). **0.2.0** — SFX control + editor authoring: loop/stop SFX via `AudioHandle`, per-key `maxConcurrentVoices` cap, `PlaySfx(key, delay)`, `StopSfx`/`StopAllSfx`, `[AudioKey]`/`[AudioPlaylistKey]` drawers (first `Editor/` asmdef), drag-drop catalog auto-populate. **0.3.0** — music/settings polish: ad-mute now PAUSES music (settings-mute STOPS + replays from top), `PauseMusic`/`ResumeMusic`, `FadeChannelVolume`, settings `Changed` event, `PreloadAsync`. **0.4.0** — settings are now game-owned: dropped the optional `ISaveSystem` persistence, so the game owns Music/Sfx enabled + volume as runtime state (+ `Changed`), matching the suite-wide settings-ownership convention (only `TK.Core.Utilities` is still referenced; `com.tk.core` remains a prerequisite).

**Deferred backlog — each trigger-gated (build when a consumer actually needs it, not speculatively):**
- **Level-scoped additional catalogs** — `RegisterAdditionalCatalog`/`Unregister`, the runtime counterpart to MA's `DynamicSoundGroupCreator` (per-level sound sets loaded on enter, freed on exit). **Trigger:** a large-content game (many levels, per-level unique audio) adopts the package; casual single-catalog games (game-shikaku) don't need it.
- **Addressable SFX + delayed-release warm window** — on-demand SFX loading with an MA-style "keep resident N s after last use" cache (v0.1–0.3 only stream music addressably; SFX must be direct clips). **Trigger:** a game with a large streamed SFX library.
- **Weighted variation selection** — per-clip weights on an entry's variation set (reference had `AddSoundGroup(path, weight)`); today the random pick is uniform. Cheap; do opportunistically when a consumer wants non-uniform variations.
- **`ReleasePreload(key)` / preload eviction** — `PreloadAsync` currently holds the resident clip until `Dispose`; add selective release for menu→gameplay handoffs.
- **Ducking presets** — auto-duck music under SFX/voice (today `FadeChannelVolume` is the manual primitive).
- **AudioMixer integration** — route channels through an `AudioMixer` for effects/snapshots.
- **Named categories** — free-form channels beyond Music/Sfx (Voice, UI, Ambient…).
- **Excluded (game-specific):** 3D positional one-shots — the package is 2D-mobile-scoped.

**Assessment:** feature-complete for casual consumers (game-shikaku uses it fully). The first three deferred items are exactly the reference project's *large-content* infrastructure (per-level sets, streamed SFX, weighted variations) — deliberately left until a g-brain-style consumer pulls them.

### com.tk.haptics
**Shipped 2026-07-08:** **0.1.0** — `Impact` (Light/Medium/Heavy) / `Selection` / `Notification` (Success/Warning/Error); game-owned `Enabled` toggle + `Changed`; per-type unscaled-time throttle; `IsSupported`. Native iOS (UIFeedbackGenerator via an embedded `.mm`) + Android (Vibrator/VibratorManager JNI) backends behind `IHapticBackend`, with a no-op fallback in the Editor / on non-mobile targets. Standalone (no `com.tk.*` deps); static `Haptics` façade. First consumer: game-shikaku's settings `Vibration` toggle.

**Deferred (trigger-gated):**
- **Custom vibration patterns** — arbitrary duration/amplitude arrays beyond the fixed Impact/Notification taxonomy. Trigger: a game wanting bespoke patterns (e.g. rhythm).
- **Continuous / scripted haptics** — sustained or Core-Haptics-style (iOS AHAP) patterns. Trigger: a game needing richer haptic design.

## Candidate new packages

Ordered by recommended priority. Each would follow the standard flow: brainstorm → spec → plan → subagent-driven execution with per-task + whole-branch review.

### 1. ~~com.tk.transitions~~ — SHIPPED into com.tk.core 0.6.0
Landed as the `TransitionCurtain` addition to `TK.Core.UI` rather than a standalone package — see `docs/specs/2026-07-23-tk-core-v0.6-transition-curtain-design.md`.

### 2. com.tk.logging (low priority)
Logger façade: levels, categories, release stripping, sink routing (console + optional forward to analytics/crashlytics).
- **Reusable mechanism:** level/category filtering, release-build stripping, sink fan-out. **Game supplies:** sink choice. Lower priority — `Debug.Log` suffices until category filtering / release stripping / crash-forwarding is actually needed.

### 3. com.tk.lives (candidate; build only when a second game actually needs it)
Lives/energy system: count + max, UTC-timestamp regeneration (computed on load/focus via `AppContext.OnAppFocus`, no background tick), consume-on-lose via `OnGameEnded`, refill seams (rewarded ad / coins / IAP), optional infinite-lives window. Persisted via `ISaveSystem`; clock-cheat clamped on load.
- **Reusable mechanism:** counting/regen/persistence/gating seams. **Game supplies:** max, costs, refill sources, UI.
- Note: shikaku's current design has no lives (its pressure mechanic is a level timer + paid continue), so there is no consumer yet — do not build speculatively.

## Notes

- Deep design analyses for shipped packages (reference-system teardowns, defect lists) currently live in gitignored session scratch (`.superpowers/`). If a future package's design leans on one, promote the relevant analysis into a committed `docs/` file so it's referenceable from the repo.
- Both monetization packages (`com.tk.iap`, `com.tk.ads`) are verified in-editor and against the vendor SDK sources, but **not yet on a real device with live store/ad accounts** — validate with sandbox purchases and the MAX Mediation Debugger during the first real game integration.
