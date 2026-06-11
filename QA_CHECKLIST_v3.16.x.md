# QA Checklist - SDK v3.16.x (device pass)

One device pass covers both the **v3.16.1 verification debt** (shipped unverified) and the **v3.16.2** changes, since the 3.16.2 build includes the 3.16.1 fixes. Source: [REMEDIATION_PLAN_v3.16.2.md](REMEDIATION_PLAN_v3.16.2.md), findings in [DESIGN_RISK_AUDIT_v3.md](DESIGN_RISK_AUDIT_v3.md).

Build the testbed (or a Full-mode game) at SDK **3.16.2**. Log markers below are quoted verbatim from the shipped code - grep the device log for them.

## Build-type split (do it in two installs max)

Verbose-gated lines only print when `Config.verboseLogging == true` **AND** `Debug.isDebugBuild` (development build). `Vital` / `Warning` / `Error` lines print in **both** build types. `[BOTH]` rows can be checked on either install; `[DEV]` needs a development build; `[REL]` needs a non-development (release) build.

| Build | Covers |
|---|---|
| **Install 1 - development build** (verbose on) | A1-A4, B1-B8, B10, C1-C9 (verbose revenue/consent diagnostics visible) |
| **Install 2 - release build** | B9 (release-only by definition), plus a regression spot-check of C1/C3/C5 |

---

## Section A - v3.16.1 P0 verification debt (released unverified)

| # | Build | Action | Expected signal | Pass |
|---|---|---|---|---|
| A1 | [BOTH] | DR-01 crash-replay: buy IAP, kill app **before** order confirmation, relaunch, let the store replay the pending order | Exactly **one** tracked purchase across the two launches. On the replay: `[Palette] TrackPurchase: duplicate purchase detected (dedupKey=...) - dropping duplicate event.` PlayerPrefs key `sorolla.purchase.processed_tx_ids` exists and contains the id. | ☐ |
| A2 | [BOTH] | DR-01 persistence: normal buy, relaunch, re-trigger `ProcessPurchase` / replay same TxID | Rejected with the same `duplicate purchase detected` Warning; no second fan-out. | ☐ |
| A3 | [BOTH] | DR-02 double-init: trigger a second `Palette.Initialize` inside the CMP window (see note) | `[Palette] Already initializing/initialized. Remove any manual Palette.Initialize() call - the SDK auto-initializes via SorollaBootstrapper.` fires. Afterwards exactly **one** `ad_impression` per ad shown, no doubled ad-revenue lines. A stray bootstrapper instead logs `[Palette] Extra SorollaBootstrapper found - ... Destroying this duplicate.` | ☐ |
| A4 | n/a | DR-01 caveat awareness (no action) | Note only: auto-renewable **subscriptions** share `OriginalTransactionID` across renewals and would break the dedup key. Not testable in the current portfolio (consumables + one-shot non-consumables). Listed as known-risk. | ☐ |

> A3 trigger: the testbed needs a dev-only way to call `Palette.Initialize` a second time during the ~1-3s CMP window. If none exists, add a dev-only hook in the **outer** testbed repo (testbed script or diagnostics-console action), **not** in the SDK package.

## Section B - v3.16.2 new behavior (one row per fix)

| # | Build | Action | Expected signal | Pass |
|---|---|---|---|---|
| B1 (DR-93) | [DEV] | Set RC defaults via `Palette.SetRemoteConfigDefaults(...)` from a `MonoBehaviour.Awake` **before** SDK init; for a key not present remotely, read it back | Default survives init: the absent-remote key resolves to the **in-code default**, not type-zero. Verbose: `[Palette:RemoteConfig] Defaults set (N values)` with N matching your defaults (not 0). | ☐ |
| B2 (DR-06) | [DEV] | Show a normal rewarded ad with real fill | `ad_impression` carries a `revenue_precision` param (`exact` / `estimated` / `publisher_defined`). Negative-revenue repro is vendor-dependent and **conditional**: if it occurs, `[Palette:MAX] Ad revenue unavailable (revenue=-1, network=..., format=...) - skipping revenue fan-out.` and no `ad_impression`/Adjust revenue for that impression. | ☐ |
| B3 (DR-12) | [BOTH] | Show rewarded + interstitial | `ad_format` on `ad_impression` is **lowercase** (`rewarded` / `interstitial`), identical casing to `ad_show_requested`. | ☐ |
| B4 (DR-14) | [BOTH] | `Palette.TrackEvent("error")` (diagnostics console or test script) | Rejected at the shared gate: `[Palette] Event rejected: 'error' is a GA4-reserved name and would be dropped by Firebase/GA4. Use a different name.` Event absent from **both** GameAnalytics and Firebase output. | ☐ |
| B5 (DR-15) | [BOTH] | `Palette.TrackEvent("test_value", { {"foo", 7}, {"value", 3} })` | GameAnalytics design-event value = **3** (the `value` key), not 7. With no `value` key present, design value = 0. | ☐ |
| B6 (DR-38) | [DEV] | Hard to repro a real vendor throw on device - **static-review row**, or editor-only fault injection in a flush loop | If injected: `[Palette] Queued event threw during flush: <msg>` (or `[Palette:Crashlytics] Queued action threw during flush:`), the remaining queued events still dispatch, and `OnInitialized` still fires. Verification level: **static / editor-injection only** unless a real throw is reproduced. | ☐ |
| B7 (DR-41) | [DEV] | Fresh install, accept CMP | In the Firebase DebugView / BQ stream (or Sorolla Vitals event log), `consent_resolved` appears **before** the flushed pre-consent events, not after. | ☐ |
| B8 (DR-42) | [DEV] | Call a `Palette.GetAttribution` / `GetAdjustId` / `GetAdvertisingId` getter pre-consent / pre-init | Callback fires with **null** instead of hanging. Sorolla Vitals identity rows no longer show timeout Warnings during the normal pre-consent window. | ☐ |
| B9 (DR-09) | [REL] | **Non-development build**, show one ad with real revenue | Sorolla Vitals "Ad revenue" row reads **Observed** (Pass), driven by `RecordAdRevenue` regardless of verbosity. (Before 3.16.2 this always read "No revenue callback observed" in release.) | ☐ |
| B10 (DR-28) | [BOTH] | If a test path allows lowercase-currency injection (e.g. force `usd`) | Output is uppercased: `[Palette] TrackPurchase: accepted ... currency='USD' ...`. Else **static row**. | ☐ |

## Section C - regression sweep (both versions' blast radius)

| # | Build | Action | Expected signal | Pass |
|---|---|---|---|---|
| C1 | [BOTH] | Cold start, CMP accept path | SDK initializes, `OnInitialized` fires once (`[Palette] Ready!`), Vitals required rows green. | ☐ |
| C2 | [BOTH] | CMP **decline** path | Analytics disabled per current design, no crash, no stranded queue (fix DR-38 touched this path). | ☐ |
| C3 | [DEV] | Rewarded ad full cycle | Exactly one of onComplete/onFailed, one `ad_impression` fired, revenue lines present. | ☐ |
| C4 | [DEV] | Interstitial full cycle | Same contract as C3. | ☐ |
| C5 | [BOTH] | Normal purchase | Tracked exactly once on all configured vendors (`TrackPurchase: accepted` once); a second **distinct** purchase also tracked (dedup set must not over-match). | ☐ |
| C6 | [BOTH] | Two purchases of the **same** product in one session (distinct TxIDs) | **Both** tracked (guards against over-aggressive dedup key). | ☐ |
| C7 | [DEV] | RC fetch | Values arrive (`[Palette:RemoteConfig] Fetch complete ...`), kill-switch pattern works (fix DR-93 touched the defaults path). | ☐ |
| C8 | [BOTH] | `consent_resolved` / `att_decision` | Emitted exactly once each (fixes DR-38/DR-41 reordered this code; A3's guard also lives here). | ☐ |
| C9 | [BOTH] | App backgrounded/resumed mid-session | Ads still load, no double subscriptions, no double revenue. | ☐ |

---

**Release gate:** all Section A + Section B rows pass (or are honestly marked static/conditional) before cutting the `v3.16.2` tag. 3.16.1 skipped this; 3.16.2 must not.
