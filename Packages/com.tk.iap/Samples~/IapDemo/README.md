# IAP Demo

Runtime demo of the full init → purchase → confirm flow. `DemoItemHandlers` builds a 3-entry
catalog **in code** (via `IapCatalog.SetEntries`, no ScriptableObject asset required), registers
logging item handlers for `"coins"` and `"life"`, and drives the service from `Start`.

## Catalog

| Catalog id | Store id | Type | Items |
| --- | --- | --- | --- |
| `pack1` | `com.example.pack1` | Consumable | `coins` × 300 |
| `remove_ads` | `com.example.removeads` | NonConsumable | none — ownership auto-grants the `remove_ads` entitlement |
| `pack10` | `com.example.pack10` | Consumable | `coins` × 300 + `entitlement` → `remove_ads` |

`pack10` demonstrates a bundle that also grants an entitlement, and its code comment calls out the
reinstall caveat: entitlements bundled inside a *consumable* do not survive a reinstall, because
store purchase history only restores NonConsumables. Prefer a NonConsumable product (like
`remove_ads`) for anything meant to be permanent.

## Running it

This sample ships as source only — there's no `.unity` scene file (a scene needs Editor-generated
GUIDs, which don't survive being authored outside the Editor). To try it:

1. Import this sample (Package Manager → TK IAP → Samples → **IAP Demo**).
2. Create a new empty scene.
3. Add an empty GameObject and attach the `DemoItemHandlers` component to it.
4. Press Play.

In the Editor, Unity IAP (`com.unity.purchasing` v5) runs against Unity's built-in fake store, so
every purchase **auto-succeeds** — there's nothing to configure and no real store connection is
made. Watch the Console for `[DemoItemHandlers]` logs tracing init, purchases, failures, and
restores.

To actually exercise a purchase, call `IapService.Instance.Purchase("pack1")` (e.g. from another
script, a debug button, or the Console via a quick test script) once you see the
`Init finished with state: Initialized` log.
