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
| `com.tk.core` | 0.1.0 | Utilities / Save / UI / App modules (à la carte asmdefs) |
| `com.tk.toolbar` | 0.1.0 | Editor time-scale + configurable scene buttons |
| `com.tk.iap` | 0.1.1 | AppLovin-independent; Unity IAP v5 wrapper (Unity IAP 5.4.0) |
| `com.tk.ads` | 0.1.2 | AppLovin MAX mediation (banner/interstitial/rewarded) (AppLovin MAX 8.6.4) |
| `com.tk.remoteconfig` | 0.1.0 | Backend-agnostic remote-config façade; feeds the IAP/Ads resolver seams (Firebase adapter as a sample) |

## Planned features in shipped packages

### com.tk.ads
See the package README's "v2 reserves" section for the committed detail. Summary:
- **App Open ads (v1.1)** — MAX app-open format, with safe-exit / fast-return heuristics so returning from your own fullscreen ad doesn't immediately trigger an app-open ad.
- **AdMob backfill (v2, if PM wants it)** — a `CompositeAdsGateway(primary, fallback)` implementing `IAdsGateway`: per-show fallback chain (MAX show-fail → AdMob non-mediation), with both gateways feeding `RevenuePaid` so backfill revenue is never dropped from reporting. The seam was designed for exactly this; no public API break expected.

### com.tk.iap
- **Subscriptions / VIP / PlayPass** — v1 is Consumable + NonConsumable only; the catalog already carries `ProductType` (warns on Subscription) and `Entitlements` is generic, so subs can be added without an API break. Needs: subscription state/expiry tracking, store-diff on fetch, VIP = any-of-named-subs.
- **`OnPurchaseDeferred` seam event** — surface Ask-to-Buy / deferred purchases (currently they arrive later as pending).
- **Async item handlers** — for server-authoritative wallets (v1 handlers are synchronous by deliberate scope decision).
- **Startup purchases-fetch retry** — Google ownership sync currently doesn't retry on a flaky first fetch.

## Candidate new packages

Ordered by recommended priority. Each would follow the standard flow: brainstorm → spec → plan → subagent-driven execution with per-task + whole-branch review.

### 1. com.tk.analytics ⭐ (recommended next)
Analytics façade. `IAnalytics` (`LogEvent(name, params)`, revenue logging, user properties). Game binds Firebase/Adjust via adapters (samples).
- **Why now:** `com.tk.iap`'s `IPurchaseReporter` and `com.tk.ads`'s `IAdRevenueReporter` are natural producers that would forward into this — unifies the monetization event stream. Pairs with the now-shipped `com.tk.remoteconfig`.
- **Reusable mechanism:** event dispatch, batching/consent gating, adapter fan-out. **Game supplies:** event taxonomy + backend adapters.

### 2. com.tk.audio
Audio service: music/SFX playback, named categories, volume + mute persisted via `ISaveSystem` (com.tk.core.Save), one-shot pooling, optional ducking.
- **Reusable mechanism:** playback/category/persistence/pooling. **Game supplies:** clips, mixer, category setup.

### 3. com.tk.haptics
Thin cross-platform haptic feedback: `Impact(light/medium/heavy)`, `Selection`, `Notification`, with an enable toggle persisted. iOS Taptic + Android vibrate impls.
- **Reusable mechanism:** the whole thing. **Game supplies:** nothing beyond the on/off preference. Small, high-reuse.

### 4. com.tk.transitions
Scene/level transition overlay: async `ShowAsync`/`HideAsync` that gates input during the transition (fade / loading indicator). Reuses/extends `com.tk.core.UI`.
- **Reusable mechanism:** overlay lifecycle + input gating + async sequencing. **Game supplies:** the visual prefab (UICatalog pattern). Could also land as a `com.tk.core.UI` addition rather than a standalone package — decide at brainstorm.

### 5. com.tk.logging (low priority)
Logger façade: levels, categories, release stripping, sink routing (console + optional forward to analytics/crashlytics).
- **Reusable mechanism:** level/category filtering, release-build stripping, sink fan-out. **Game supplies:** sink choice. Lower priority — `Debug.Log` suffices until category filtering / release stripping / crash-forwarding is actually needed.

## Notes

- Deep design analyses for shipped packages (reference-system teardowns, defect lists) currently live in gitignored session scratch (`.superpowers/`). If a future package's design leans on one, promote the relevant analysis into a committed `docs/` file so it's referenceable from the repo.
- Both monetization packages (`com.tk.iap`, `com.tk.ads`) are verified in-editor and against the vendor SDK sources, but **not yet on a real device with live store/ad accounts** — validate with sandbox purchases and the MAX Mediation Debugger during the first real game integration.
