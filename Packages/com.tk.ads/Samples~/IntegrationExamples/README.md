# Integration Examples

Reference implementations for `com.tk.ads`'s two analytics/remote-config seams. Both compile
standalone (no third-party SDK required) and are meant to be copied into your project and adapted,
not used as-is.

## FirebaseAdRevenueReporterExample (`IAdRevenueReporter`)

Reports every paid ad impression to Firebase Analytics as an `ad_impression` event, using the
parameter set commonly associated with AppLovin MAX ILRD (impression-level revenue data) reporting.
The actual `FirebaseAnalytics.LogEvent` call is commented out so this compiles without the Firebase
SDK installed — uncomment it (and add the Firebase Analytics package) to wire it up for real.

Register it in `AdsOptions`:

```csharp
var options = new AdsOptions { RevenueReporter = new FirebaseAdRevenueReporterExample() };
var ads = new AdsService(settings, options);
```

Parameter mapping from `AdRevenueInfo`:

| `ad_impression` param | Source |
| --- | --- |
| `ad_platform` | Always `"AppLovin"` (MAX is the sole mediation source this package integrates) |
| `ad_source` | `info.NetworkName` — the winning mediated network |
| `ad_unit_name` | `info.AdUnitId` |
| `ad_format` | `info.Format` (`"banner"` / `"interstitial"` / `"rewarded"`) |
| `value` | `info.Revenue` |
| `currency` | `info.Currency` (always `"USD"` — the only currency MAX reports revenue in) |

`OnAdRevenue` must never throw — `AdsService` already wraps every reporter call in a try/catch and
logs+continues if it does, but a reporting implementation should still fail safe on its own (e.g.
guard against a Firebase SDK that isn't initialized yet).

## LevelLadderPacingResolverExample (`IAdsPacingResolver`)

Ramps the interstitial interval up as the player progresses through levels — new players see
interstitials less often, veteran players see the "steady state" interval — driven by a remote-config
JSON string (e.g. Firebase Remote Config, Unity Remote Config, or your own backend), parsed with
`JsonUtility` — no Newtonsoft dependency.

```csharp
var resolver = new LevelLadderPacingResolverExample(() => progression.CurrentLevel);
resolver.SetConfigJson(remoteConfigJsonString); // call again whenever the config re-fetches

var options = new AdsOptions { PacingResolver = resolver };
var ads = new AdsService(settings, options);
```

Expected JSON shape — two parallel arrays defining level bands:

```json
{ "ChangingIntervalLevels": [5, 15], "IntervalsInSecond": [30, 60, 90] }
```

Reads as: levels 1-5 → 30s, levels 6-15 → 60s, levels 16+ → 90s. `IntervalsInSecond` always has
exactly one more entry than `ChangingIntervalLevels` — the last entry is the steady-state interval
once the player is past every threshold.

Behavior:

- Only overrides `AdsPacingKeys.InterstitialInterval`. Every other key — notably
  `AdsPacingKeys.CooldownAfterRewarded` — passes through untouched (`ResolveSeconds` returns
  `defaultSeconds`), so `InterstitialPacer`'s post-rewarded cooldown keeps using whatever
  `AdsSettings.cooldownAfterRewardedSeconds` (or a different resolver) provides.
- The current level and the ladder are both read at check time (inside `ResolveSeconds`, which
  `InterstitialPacer.CanShow()` calls on every pacing check) — a level-up or a live remote-config
  change both apply to the very next check, with no extra wiring needed.
- A malformed, empty, or not-yet-fetched JSON payload falls back to `defaultSeconds` rather than
  risking a bad interval value — matching the production reference this sample is ported from, which
  hard-falls-back to a fixed interval on any parse exception.
