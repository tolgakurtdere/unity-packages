# Analytics Demo

Editor-runnable demo of the full `AnalyticsService` flow on the built-in `ConsoleAnalyticsBackend`,
driven entirely from `[ContextMenu]` entries. No SDK required — every logged call prints to the
Console, so you can watch the consent gate and the loss-free pre-init buffer without wiring Firebase
or Adjust.

## Running it

This sample ships as source only — there's no `.unity` scene file (a scene needs Editor-generated
GUIDs, which don't survive being authored outside the Editor). To try it:

1. Import this sample (Package Manager → **TK Analytics** → Samples → **Analytics Demo**).
2. Create a new empty scene.
3. Add an empty GameObject and attach the `AnalyticsDemo` component to it.
4. Press Play. Watch the Console for `[AnalyticsDemo]` and `[Analytics]` logs.

Then right-click the component's header in the Inspector (or use its "⋮" context menu) to reach the
demo actions:

| Menu entry | What it does |
| --- | --- |
| Start Backends | `StartAsync()` — initializes every backend, marks the service started. |
| Grant Consent | `SetConsent(true)` — opens the gate; if started, buffered ops flush now, in order. |
| Deny Consent | `SetConsent(false)` — clears the buffer and blocks dispatch (GDPR-safe). |
| Log Test Event | `demo_event` with a `score` (long) and `mode` (string) parameter. |
| Log Test Purchase | `LogPurchase` for a demo product. |
| Log Test Ad Revenue | `LogAdRevenue` for a demo rewarded impression. |
| Set User | `SetUserId` + `SetUserProperty("tier", "gold")`. |
| Flush | `Flush()` — asks each backend to flush its own native buffer (no-op if dispatch isn't allowed yet). |

## See the buffer + consent gate

The point of this demo is the gating/buffering behavior, so try this order:

1. Press Play, then **Log Test Event** — nothing prints. The event is buffered because dispatch isn't
   allowed yet (the service is neither started nor consented).
2. **Start Backends**, then **Grant Consent** — the buffered event now flushes to the Console, in
   order, with its parameters intact.
3. Restart and instead run **Deny Consent** after buffering an event — the buffer is cleared and the
   event is dropped, never sent.

`AnalyticsDemo` sets itself as the ambient `Analytics.Instance` in `Awake`, so the `Log*` context
menu entries go through the static `Analytics` façade — the same way you'd log from anywhere in a
real game. For a real backend, see the `IntegrationExamples` sample's `FirebaseAnalyticsBackend` /
`AdjustAnalyticsBackend`.
