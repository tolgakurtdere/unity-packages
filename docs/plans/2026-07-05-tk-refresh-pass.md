# TK Packages Maintenance / Refresh Pass — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax. **Opus-only** — dispatch every implementer AND reviewer on Opus (user requirement for these packages).

**Goal:** Bring the four shipped runtime packages up to current stable SDKs and to a single consistency standard: bump Unity IAP + AppLovin MAX to newest stable (re-verifying each SDK's API against the newly resolved source), move all package specs/plans into committed `docs/`, standardize the two JsonUtility samples onto Newtonsoft, and align Newtonsoft to one version everywhere.

**Architecture:** This is a maintenance pass over shipped, tagged packages — not a new package. Each SDK bump is a mini-release: bump the pin → resolve → **re-verify the gateway's exact SDK member usage against the newly installed package source** (this is where MAX/IAP surprises live — we already caught two MAX deltas and IAP's non-faulting Connect at the original versions) → gate → patch re-tag. Consistency fixes (docs move, Newtonsoft alignment, sample JSON) ride along. Nothing changes public API; all changes are patch-level.

**Tech Stack:** Unity 6000.3.6f1 host; Unity IAP (`com.unity.purchasing`), AppLovin MAX (`com.applovin.mediation.ads`), Newtonsoft (`com.unity.nuget.newtonsoft-json`); NUnit EditMode.

## Global Constraints

- Repo: `/Users/tolgahankurtdere/Documents/GitHub/unity-packages`, branch `main`. Base = current `main` tip when execution begins (after this plan commit; RC shipped at `a0601ad`).
- **NEVER run Unity CLI against the host project.** Harness: `/private/tmp/claude-501/-Users-tolgahankurtdere-Documents-GitHub-unity-packages/125643b5-4b33-48e0-b763-cca5d06442d8/scratchpad/tk-verify` (has all 5 TK packages via `file:` + AppLovin & OpenUPM registries + `testables`). If missing, recreate per prior plans. Gate command (from harness dir; NEVER `-quit` with `-runTests`; Bash timeout 600000):
  ```bash
  /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults "$(pwd)/results.xml" -logFile "$(pwd)/unity.log"
  ```
  Success = exit 0 AND results.xml `result="Passed"` AND zero `error CS`/`warning CS` under `Packages/com.tk`. Current baseline = 174. An SDK bump may change the harness's resolved SDK — that's the point; the gate validates our code against the NEW SDK. If a bump surfaces new warnings from OUR code, fix them; SDK-package-internal warnings (not under `Packages/com.tk`) don't fail the gate but must be noted.
- **Version targets are RESOLVED, not guessed.** Each bump task first resolves the newest STABLE (no beta/preview/pre) version from the package's registry in the harness and records the exact number, then pins it. Known approximate targets (confirm exact at execution): Unity IAP `5.4.x` (latest 5.x, D2C support); AppLovin MAX `8.x` (newest — resolve from the AppLovin registry); Newtonsoft `com.unity.nuget.newtonsoft-json` (currently 3.2.2 wrapping Json.NET 13.0.2 — confirm whether a newer STABLE Unity package exists).
- **SDK API re-verification is mandatory** on each bump: after resolving the new version, read the newly installed package source under the harness `Library/PackageCache/<pkg>@*/` and confirm EVERY SDK member the gateway uses still exists with the same signature/semantics. On any drift, adapt the gateway and record the delta. Do NOT assume a patch/minor bump is API-safe.
- **Newtonsoft: one version everywhere.** Whatever version Task 1 picks, ALL packages that declare or transitively rely on Newtonsoft use exactly that version (core, remoteconfig, and — after this pass — iap, ads). A version split would make a multi-TK-package project fail to resolve.
- **Commit trailer: `Co-Authored-By: Claude <noreply@anthropic.com>` — NO model name.** Conventional commits. Committing to `docs/` is expected. Do NOT push mid-pass (a single finish step pushes + tags); do NOT commit `.superpowers/` (except when MOVING files out of it in Task 1) or unrelated host churn.
- Every changed/added file under `Packages/` ends with a committed `.meta`. Samples~ get NO `.meta`.
- Public API must not change (patch-level pass). If a re-verify forces an API change, STOP and report — that's a minor bump decision for the controller, not a silent change.

---

### Task 1: Docs standardization + Newtonsoft target discovery

**Files:**
- Move (git mv equivalent — they're currently gitignored, so `git add` at the new path + remove the old): `.superpowers/plans/2026-07-03-tk-packages-v1.md` → `docs/plans/2026-07-03-tk-packages-v1.md`; `.superpowers/plans/2026-07-04-tk-iap-v1.md` → `docs/plans/2026-07-04-tk-iap-v1.md`; `.superpowers/plans/2026-07-04-tk-ads-v1.md` → `docs/plans/2026-07-04-tk-ads-v1.md`; `.superpowers/specs/2026-07-04-tk-ads-design.md` → `docs/specs/2026-07-04-tk-ads-design.md`.
- Create: `docs/README.md` (a short index of what specs/plans exist and the standard).

**Interfaces:** produces the Newtonsoft target version consumed by Tasks 2-4 (record it in the progress ledger + docs/README.md).

- [ ] **Step 1: Move the four docs into `docs/`.** Copy each `.superpowers/` file to the `docs/` path shown above, then delete the `.superpowers/` original. These are the design records for core/toolbar (v1 plan), iap (plan), and ads (spec+plan). Do NOT rewrite content; do NOT manufacture retroactive specs for core/toolbar/iap (their plan IS the design record — per user decision).
- [ ] **Step 2: Write `docs/README.md`** — a short index: one line per package pointing to its spec (if any) and plan under `docs/specs`/`docs/plans`, plus a one-line statement that `docs/specs` + `docs/plans` are the committed, referenceable home for all package design docs going forward (superseding the old gitignored `.superpowers/` location). List: core/toolbar → plan only; iap → plan only; ads → spec + plan; remoteconfig → spec + plan; this refresh pass → this plan.
- [ ] **Step 3: Newtonsoft target discovery.** In the harness, determine the newest STABLE `com.unity.nuget.newtonsoft-json` available from Unity's default registry (e.g. temporarily add it at a high version or check the registry; do NOT leave the probe in the harness manifest). Record the exact newest stable version. Decision rule: if newest > 3.2.2, the pass aligns ALL packages to it (Task 2 bumps core+RC); if 3.2.2 is newest, the pass keeps 3.2.2 and Task 2 becomes a no-op. Write the chosen `NEWTONSOFT_TARGET` into the progress ledger and `docs/README.md`.
- [ ] **Step 4: Commit** — `docs: move package specs/plans into committed docs/ and add docs index`. (No gate — docs only. Verify the moved files are git-tracked and the `.superpowers/` originals are gone.)

---

### Task 2: Newtonsoft alignment for core + remoteconfig (conditional)

**Only run if Task 1's `NEWTONSOFT_TARGET` > 3.2.2. If 3.2.2 is newest, mark this task done as a no-op with a one-line ledger note and skip to Task 3.**

**Files:**
- Modify: `Packages/com.tk.core/package.json` (newtonsoft dependency → `NEWTONSOFT_TARGET`; bump package version 0.1.0 → 0.1.1)
- Modify: `Packages/com.tk.core/CHANGELOG.md` (add `## [0.1.1]` — Newtonsoft bump)
- Modify: `Packages/com.tk.remoteconfig/package.json` (newtonsoft dependency → `NEWTONSOFT_TARGET`; bump 0.1.0 → 0.1.1)
- Modify: `Packages/com.tk.remoteconfig/CHANGELOG.md` (add `## [0.1.1]`)

- [ ] **Step 1: Bump both package.json** newtonsoft deps to `NEWTONSOFT_TARGET` and both package versions to 0.1.1.
- [ ] **Step 2: CHANGELOGs** — keep-a-changelog `## [0.1.1] - <today>` entries: "Bumped Newtonsoft to `NEWTONSOFT_TARGET`."
- [ ] **Step 3: Gate.** The harness re-resolves Newtonsoft at the new version; expected 174, zero com.tk errors/warnings. If the new Newtonsoft surfaces API changes affecting `PlayerPrefsJsonSaveSystem` (JsonConvert.SerializeObject/DeserializeObject — extremely stable) or RC's `GetObject` (JsonConvert.DeserializeObject), fix; unlikely.
- [ ] **Step 4: Commit** — `chore(core,remoteconfig): align Newtonsoft to <NEWTONSOFT_TARGET>`.

---

### Task 3: iap SDK refresh (Unity IAP → newest stable 5.x) + Newtonsoft + sample

**Files:**
- Modify: `Packages/com.tk.iap/package.json` (`com.unity.purchasing` 5.1.2 → newest stable 5.x; ADD `com.unity.nuget.newtonsoft-json` = `NEWTONSOFT_TARGET`; bump version 0.1.0 → 0.1.1)
- Modify (only if re-verify finds drift): `Packages/com.tk.iap/Runtime/Gateway/UnityIapGateway.cs`
- Modify: `Packages/com.tk.iap/Samples~/IntegrationExamples/RemoteConfigAmountResolverExample.cs` (JsonUtility → Newtonsoft) + its `README.md` note
- Modify: `Packages/com.tk.iap/CHANGELOG.md` (`## [0.1.1]`)

**Interfaces:** no public API change.

- [ ] **Step 1: Resolve + pin purchasing.** In the harness, resolve the newest STABLE `com.unity.purchasing` in the 5.x line (confirm it's not a pre/beta). Record the exact version. Set it in `Packages/com.tk.iap/package.json`. Also add `"com.unity.nuget.newtonsoft-json": "<NEWTONSOFT_TARGET>"` (iap already gets it transitively via core, but declare it explicitly now that a sample uses it — Task 3 Step 4).
- [ ] **Step 2: Re-verify the v5 gateway API** against the newly installed source at the harness `Library/PackageCache/com.unity.purchasing@*/`. Confirm every member `UnityIapGateway.cs` uses still exists with the same shape: `UnityIAPServices.StoreController()`; `StoreController.Connect()` (still returns a Task that resolves on success AND failure — the `_connectFailed` latch depends on this; RE-CHECK, 5.4 may have changed disconnection semantics), `ProcessPendingOrdersOnPurchasesFetched(bool)` (we set TRUE — confirm still the crash-recovery-correct value), `SetStoreReconnectionRetryPolicyOnDisconnection`, `FetchProducts`, `FetchPurchases`, `PurchaseProduct`, `ConfirmPurchase(PendingOrder)`, `GetProductById`; callbacks `OnStoreDisconnected/OnProductsFetched/OnProductsFetchFailed/OnPurchasesFetched/OnPurchasesFetchFailed/OnPurchasePending/OnPurchaseConfirmed/OnPurchaseFailed/OnPurchaseDeferred` and their payload types (`Order/PendingOrder/FailedOrder/Orders/ProductFetchFailed/PurchasesFetchFailureDescription`, `Order.Info.TransactionID`, `PurchasedProductInfo.productId`, `product.metadata.*`). On any drift, adapt the gateway and record the delta in the report. If ProcessPendingOrders semantics changed, re-confirm the Google crash-redelivery behavior (the whole reason we set the flag true).
- [ ] **Step 3: Gate after the bump** (before the sample change) to isolate SDK-bump breakage. Expected 174 (iap's 47 tests must still pass against 5.x). Fix any drift; re-gate.
- [ ] **Step 4: Convert the sample to Newtonsoft.** In `RemoteConfigAmountResolverExample.cs`, replace `JsonUtility.FromJson<OverrideList>(json)` with `Newtonsoft.Json.JsonConvert.DeserializeObject<OverrideList>(json)` (add `using Newtonsoft.Json;`), keeping the same sanitize/fallback behavior; update the class doc + folder `README.md` line that said "JsonUtility — no Newtonsoft dependency" to reflect Newtonsoft (now a declared dep). Note in the sample that `com.tk.remoteconfig`'s `GetObject<T>` supersedes hand-rolling this for RC consumers.
- [ ] **Step 5: CHANGELOG** `## [0.1.1] - <today>`: "Bumped Unity IAP to `<version>` (API re-verified); declared Newtonsoft `<target>`; sample now uses Newtonsoft." **Step 6: Gate** (174, zero com.tk warnings). **Step 7: Commit** — `chore(iap): bump Unity IAP to <version>, align Newtonsoft, sample to Newtonsoft`.

---

### Task 4: ads SDK refresh (AppLovin MAX → newest stable 8.x) + Newtonsoft + sample

**Files:**
- Modify: `Packages/com.tk.ads/package.json` (`com.applovin.mediation.ads` 8.6.2 → newest stable 8.x; ADD `com.unity.nuget.newtonsoft-json` = `NEWTONSOFT_TARGET`; bump version 0.1.1 → 0.1.2)
- Modify (only if re-verify finds drift): `Packages/com.tk.ads/Runtime/Gateway/MaxAdsGateway.cs`
- Modify: `Packages/com.tk.ads/Samples~/IntegrationExamples/LevelLadderPacingResolverExample.cs` (JsonUtility → Newtonsoft) + its `README.md`
- Modify: `Packages/com.tk.ads/CHANGELOG.md` (`## [0.1.2]`)

**Interfaces:** no public API change.

- [ ] **Step 1: Resolve + pin MAX.** In the harness, resolve the newest STABLE `com.applovin.mediation.ads` from the AppLovin scoped registry (confirm not a beta). Record exact version. Set it in `Packages/com.tk.ads/package.json`. Add `"com.unity.nuget.newtonsoft-json": "<NEWTONSOFT_TARGET>"` — NOTE: ads is standalone (no core dep) so it does NOT get Newtonsoft transitively; this explicit dep is required now that the sample uses Newtonsoft. Confirm the AppLovin registry still resolves the transitive `com.google.external-dependency-manager` from OpenUPM (the harness has both registries).
- [ ] **Step 2: Re-verify the MAX gateway API** against the newly installed source at `Library/PackageCache/com.applovin.mediation.ads@*/Scripts/` (MaxSdkCallbacks.cs, MaxSdk*.cs, MaxSdkBase.cs). Confirm every member `MaxAdsGateway.cs` uses: `MaxSdk.InitializeSdk()`, `MaxSdkCallbacks.OnSdkInitializedEvent`; banner `CreateBanner(string, AdViewConfiguration)` + `AdViewConfiguration(AdViewPosition)` + `AdViewPosition` enum (we switched OFF the `[Obsolete]` BannerPosition — CHECK whether the new version removed the obsolete API or changed AdViewConfiguration), `SetBannerBackgroundColor`, `ShowBanner/HideBanner/DestroyBanner`; `LoadInterstitial/ShowInterstitial(string, string)/IsInterstitialReady`; `LoadRewardedAd/ShowRewardedAd(string,string)/IsRewardedAdReady`; callback groups `Banner/Interstitial/Rewarded` with `OnAdLoadedEvent/OnAdLoadFailedEvent/OnAdClickedEvent/OnAdDisplayedEvent/OnAdDisplayFailedEvent(string,ErrorInfo,AdInfo — 3-arg, CONFIRM still 3-arg)/OnAdHiddenEvent/OnAdRevenuePaidEvent` + rewarded `OnAdReceivedRewardEvent(string,Reward,AdInfo)`; `AdInfo.{NetworkName,AdUnitIdentifier,Revenue}`, `ErrorInfo.Message`; `MaxSdk.CmpService.HasSupportedCmp` + `ShowCmpForExistingUser(Action<MaxCmpError>)` + `MaxCmpError.ErrorCode.FormNotRequired`. On any drift, adapt + record.
- [ ] **Step 3: Gate after the bump** (before the sample change) to isolate SDK-bump breakage. Expected 174 (ads' tests pass against the new MAX; MaxAdsGateway has no EditMode tests — the gate proves it compiles clean against the new SDK). Fix drift; re-gate.
- [ ] **Step 4: Convert the sample to Newtonsoft.** In `LevelLadderPacingResolverExample.cs`, replace `JsonUtility.FromJson<Ladder>(json)` with `JsonConvert.DeserializeObject<Ladder>(json)` (`using Newtonsoft.Json;`), same fallback; update the class doc + `README.md` line about JsonUtility. Note `com.tk.remoteconfig.GetObject<T>` supersedes this for RC consumers.
- [ ] **Step 5: CHANGELOG** `## [0.1.2] - <today>`. **Step 6: Gate** (174, zero com.tk warnings; confirm MaxAdsGateway + TK.Ads recompiled). **Step 7: Commit** — `chore(ads): bump AppLovin MAX to <version>, align Newtonsoft, sample to Newtonsoft`.

---

### Task 5: Root docs + finish (whole-branch review → push → tags)

**Files:**
- Modify: root `README.md` (package version table + Versioning tag list + install pin examples for bumped packages)
- Modify: `ROADMAP.md` (Shipped table versions)

- [ ] **Step 1: Update root README + ROADMAP** to the new versions: iap 0.1.1, ads 0.1.2, and core 0.1.1 + remoteconfig 0.1.1 IF Task 2 bumped them (else unchanged). Update the Versioning tag list and the per-package pinned-URL examples to the new tags. (Toolbar unchanged at 0.1.0.)
- [ ] **Step 2: Final gate** (174, zero com.tk warnings) + `git status` clean apart from known host churn (commit `Packages/packages-lock.json` if the SDK bumps changed it).
- [ ] **Step 3: Commit** — `docs: update README + ROADMAP for refreshed package versions`.
- [ ] **Step 4: FINISH (controller, after whole-branch Opus review of the full pass diff):** push `main`; create + push the new patch tags for exactly the packages whose version changed — `com.tk.iap/0.1.1`, `com.tk.ads/0.1.2`, and (if Task 2 ran) `com.tk.core/0.1.1`, `com.tk.remoteconfig/0.1.1`. Do NOT move existing tags. Verify remote heads/tags after push.

## Notes for reviewers

- The two highest-risk items are the SDK API re-verifications (Tasks 3-4 Step 2). A green gate proves compilation + our tests, but the reviewer should confirm the re-verify actually diffed the NEW installed source (not the old 5.1.2/8.6.2), especially: IAP `Connect()` still-never-faults + `ProcessPendingOrdersOnPurchasesFetched(true)` crash-recovery semantics; MAX `AdViewConfiguration`/`AdViewPosition` + `OnAdDisplayFailedEvent` arity + `MaxCmpError.ErrorCode.FormNotRequired`.
- Newtonsoft version parity across all packages is a hard invariant — the reviewer verifies every `com.unity.nuget.newtonsoft-json` pin is identical.
