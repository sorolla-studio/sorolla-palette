# Changelog

All notable changes to this project will be documented in this file.

## [3.15.1] - 2026-05-05

Hotfix for Palette editor state sync.

### Fixed
- **MAX shared SDK key sync** (`Runtime/PaletteConstants.cs`, `Editor/MaxSettingsSanitizer.cs`, `Editor/SorollaWindow.cs`, `Editor/BuildValidationVendorSettings.cs`): restores the embedded publisher-level AppLovin MAX SDK key. Palette auto-syncs `AppLovinSettings` from this exact key during editor auto-fixes before validation; MAX is no longer treated as a user-configured SDK key.
- **Mode source of truth** (`Editor/SorollaSettings.cs`, `Editor/SorollaWindow.cs`, `Editor/BuildValidationConfig.cs`): existing `Resources/SorollaConfig.asset` now drives Palette editor mode on reload. Fresh EditorPrefs can no longer auto-select Prototype and overwrite a project that is already configured for Full mode.

## [3.15.0] - 2026-05-04

SDK QA hardening release. Adds purchase-verification diagnostics, MAX retry hardening, modularized build validation, and Debug UI purchase tooling.

### Added
- **Adjust iOS purchase verification diagnostics** (`Runtime/Purchasing/Palette.PurchaseTracking.cs`, `Runtime/Adapters/Adjust/AdjustAdapterImpl.cs`): logs the purchase verification callback status, numeric code, and message. Also logs Apple payload presence for the unified receipt, app receipt, JWS representation, original transaction ID, and store name. This makes sandbox/production environment mismatches and dashboard setup issues visible during QA without exposing raw receipt payloads.
- **MAX ad load retry backoff** (`Runtime/Adapters/MAX/MaxAdapterImpl.cs`): failed rewarded/interstitial load attempts now retry through SDK-managed coroutine timing instead of immediately hammering reload calls.
- **Debug UI IAP test harness** (`Samples~/DebugUI/Scripts/Controllers/DebugPurchaseTester.cs`): adds a Unity IAP v5 QA purchase path wired through `Palette.AttachPurchaseTracking` so SDK purchase tracking can be exercised from the sample UI.
- **Build health notifier and modular build-validation files** (`Editor/BuildValidation*.cs`, `Editor/BuildHealthConsoleNotifier.cs`, `Editor/BuildValidatorPreprocessor.cs`): splits the build validator into focused checks for packages, Firebase, Gradle, Android manifest, config, vendor settings, and console reporting.
- **Economy item ID sanitizer tests** (`Tests/Editor/EconomyItemIdSanitizerTests.cs`): covers item-id normalization behavior used by economy events.
- **Firebase Remote Config public guide** (`Documentation~/guides/firebase-remote-config.md`).

### Changed
- **Runtime internals split into focused partials**: purchase tracking, remote config, and event validation were moved out of the monolithic `Palette.cs` into dedicated files. Public integration paths remain the same.
- **Adapter logging now routes through `PaletteLog`** where touched, giving clearer presence/absence diagnostics without exposing sensitive raw payloads.
- **Debug UI and docs updated** for the current Firebase, TikTok, Adjust, and QA purchase flows.
- **Internal-only documents moved out of the package** so shipped UPM contents stay focused on public SDK docs and runtime/editor code.

### Notes
- For iOS sandbox purchases, Adjust Production can return `code=20040` because the App Store transaction is sandbox while Adjust is in Production. Use Adjust Sandbox for sandbox StoreKit verification checks; a real production purchase is required for end-to-end Adjust Production verification.

## [3.14.4] - 2026-04-24

`Palette.Level.Start/Complete/Fail` no longer drops events on `level <= 0` or `world <= 0`. The SDK was rejecting `level=0` as invalid, but **0-indexed level schemes are valid production data**. Some integrations use `0` for the first playable level/map, and the previous `<= 0` validation bounced legitimate events and made PROGRESSION funnels appear silently empty in dashboards.

Policy split:

- **0 is valid.** Passes through silently. 0-indexed or 1-indexed — both are legitimate, the SDK doesn't impose a convention.
- **Negative values pass through with a warning.** Almost always an uninitialized int or off-by-one bug, but clamping to 0 would merge the broken events into the legit `level_0` cohort and silently corrupt *that* funnel — the same class of silent-corruption the drop-vs-warn fix was meant to avoid. Passing through keeps the anomaly visibly separate in dashboards while the warning flags the caller-side bug.
- **Curated enums (`CurrencyId`, `EconomySource`, `EconomySink`) and `Economy.Earn/Spend` amount** stay strict (drop on invalid). Finite known values / impossible transactions are corruption, not recoverable signal.

### Changed
- **`Palette.Level.Start/Complete/Fail`** (`Runtime/Palette.Level.cs`): removed the `level <= 0` / `world <= 0` validation. `level == 0` / `world == 0` are now silent (valid). `level < 0` / `world < 0` log a warning and pass through: `Level.{verb}: level={N} is negative; event passed through. Check for uninitialized int or off-by-one.`
- Internal: `Validate(...) -> bool` replaced by `WarnIfNegative(string, int, int?)` — no clamping, no return value gating the emit.

## [3.14.3] - 2026-04-22

Interstitial-ad callback symmetry + a pre-existing bug fix. Rewarded has had `(onComplete, onFailed)` since 3.x; interstitial had only `(onComplete)` and — worse — internally fired `_onInterstitialComplete` on `OnInterstitialAdDisplayFailed` and on the not-ready-at-show guard, mis-reporting failure as success. Studios relying on onComplete to gate game-flow transitions were getting told the ad played when it didn't.

### Fixed
- **`OnInterstitialAdDisplayFailed` mis-invoked `onComplete`** (`MaxAdapterImpl`): when a mediated network failed to display an interstitial mid-show, the SDK fired the studio's completion callback, signalling success. Now fires `onFailed`.
- **Not-ready-at-show guard mis-invoked `onComplete`** (`MaxAdapterImpl.ShowInterstitialAd`): if `ShowInterstitialAd` was called while `!_init`, `!_interstitialReady`, or `!MaxSdk.IsInterstitialReady`, the SDK called `onComplete` as if the ad had shown. Now calls `onFailed`.
- **Unavailable-MAX fallback mis-invoked `onComplete`** (`Palette.ShowInterstitialAd` on builds without MAX): now calls `onFailed`. Aligns with the rewarded no-MAX branch which already routes to `onFailed`.

### Changed (BREAKING)
- **`Palette.ShowInterstitialAd(Action onComplete)` → `Palette.ShowInterstitialAd(Action onComplete, Action onFailed)`**: `onFailed` is a required parameter, matching `ShowRewardedAd(onComplete, onFailed)`. Studios must explicitly handle the failure path — previously a no-fill / display-error silently fired `onComplete` and looked like a successful show. The compile error forces studios to acknowledge the case rather than shipping a silent game hang on no-fill.
- Internal: `IMaxAdapter.ShowInterstitialAd` + `MaxAdapter.ShowInterstitialAd` + `MaxAdapterImpl.ShowInterstitialAd` all take `(onComplete, onFailed)`. `MaxAdapterImpl` now stores `_onInterstitialFailed` alongside `_onInterstitialComplete` and routes each event to the correct slot; both slots are cleared after either fires so a stale callback cannot leak into the next Show.

### Migration
```csharp
// Before:
Palette.ShowInterstitialAd(() => ResumeGame());

// After (minimum — route both to same handler if game-flow semantics are identical):
Palette.ShowInterstitialAd(
    onComplete: () => ResumeGame(),
    onFailed:   () => ResumeGame());

// Or (recommended — distinguish for telemetry / retry):
Palette.ShowInterstitialAd(
    onComplete: () => ResumeGame(),
    onFailed:   () => { Palette.TrackEvent("interstitial_no_fill"); ResumeGame(); });
```

Affected downstream integrations: any project using the old single-callback `Palette.ShowInterstitialAd` signature must pass an explicit failure callback. Each integration fix is a small call-site update.

## [3.14.2] - 2026-04-22

Foolproof-path cleanup, same pattern as 3.14.1. Four deprecated-but-public APIs with typed canonical replacements already in place: the SDK was telling studios "use the typed version" via `[Obsolete]` warnings while still exposing the legacy surface for them to misuse. Every known misuse (stringly-typed progression slots, wrong currency codes, bool-arg `Initialize` races with bootstrap, duplicate custom-event tracking) went through these surfaces. Now gone.

### Removed
- **`Palette.TrackProgression(ProgressionStatus, string, string, string, int, Dictionary)`**: deleted. Canonical path is `Palette.Level.Start/Complete/Fail` (typed int levels, auto-duration tracking, no stringly-typed slots).
- **`Palette.TrackResource(ResourceFlowType, string, float, string, string, Dictionary)`**: deleted. Canonical path is `Palette.Economy.Earn/Spend` (typed `CurrencyId` + curated `EconomySource`/`EconomySink` enums).
- **`Palette.TrackDesign(string, float)`**: deleted. Canonical path is `Palette.TrackEvent(eventName, parameters)` — structured Firebase/BigQuery params instead of a single numeric value.
- **`Sorolla.Palette.ProgressionStatus` enum**: deleted along with `TrackProgression`. Use `Palette.Level.Start/Complete/Fail` directly.
- **`Sorolla.Palette.ResourceFlowType` enum**: deleted along with `TrackResource`. Use `Palette.Economy.Earn/Spend` directly.
- Internal `ToGA(ProgressionStatus)` / `ToGA(ResourceFlowType)` helpers: deleted (orphaned after the public methods were removed).

### Changed
- **`Palette.Initialize(bool consent)`**: `public` → `internal`. Documented "Do NOT call directly" since 3.x; only caller was `SorollaBootstrapper`. Locking it down eliminates the `IsInitialized`-race footgun where a studio manually called `Initialize` before (or after) the bootstrapper's auto-init.
- **Editor tooltip (`SorollaWindow.cs`)**: `Palette.TrackDesign()` replaced with modern `Palette.TrackEvent` / `Palette.Level.*` / `Palette.Economy.*` pointers.
- **`Documentation~/architecture.md` + `Documentation~/quick-start.md` + `README.md`**: all examples migrated from the legacy `TrackProgression` / `TrackResource` calls to the typed `Palette.Level.*` / `Palette.Economy.*` API.

### Migration
All four removed APIs had `[Obsolete]` warnings in prior releases pointing at the typed replacements. Studios who acted on those warnings are already on the canonical path and need no migration. Studios still on the legacy API should follow the Obsolete message: `TrackProgression` → `Palette.Level.Start/Complete/Fail`, `TrackResource` → `Palette.Economy.Earn/Spend`, `TrackDesign` → `Palette.TrackEvent`.

### Notes
- `Documentation~/api-reference.md` is auto-regenerated; stale 3.14.1 / 3.14.2 entries will remain until the next `Tools~/build-docs.sh` regen. Source of truth is the access modifier on `Runtime/Palette.cs`.

## [3.14.1] - 2026-04-22

Fool-proof the canonical purchase-tracking path: `Palette.TrackPurchase` is no longer reachable from studio code. 3.14.0 made `AttachPurchaseTracking` the canonical wiring; 3.14.1 finishes the job by removing the direct-call escape hatch so there's literally one way to integrate purchase tracking and it cannot be miswired, double-called, or called with malformed data.

### Changed
- **`Palette.TrackPurchase(PendingOrder)`**: `public` → `internal`. SDK subscribes this to `OnPurchasePending` via `AttachPurchaseTracking`; studios have no reason to call it directly (and every known misuse — double-calling, calling with stale metadata, calling outside the Pending lifecycle — went through this surface). Sorolla does not support non-Unity-IAP revenue sources, so the escape hatch had no real users.
- **`Palette.TrackPurchase(double, string, string, string, string)` low-level**: `public` → `internal`. Same rationale. Sorolla does not maintain a custom / web-checkout / server-side revenue tracker; the low-level surface existed only to back the higher-level overloads.
- **`Palette.TrackPurchase(Product)`**: `public` → `internal` (was already `[Obsolete]`). Legacy Unity IAP v4 is unsupported by Sorolla; the shim is retained only for the internal `AutoTracker` class which itself is `[Obsolete]`.
- **`Documentation~/architecture.md` Purchase Attribution diagram**: collapsed to a single path. `AttachPurchaseTracking(store)` is now documented as the only studio-facing entry point; the downstream chain (`TrackPurchase(PendingOrder)` → low-level → adapters) is all `internal`.

### Migration
```csharp
// Before (3.14.0 and earlier):
_store.OnPurchasePending += order =>
{
    Palette.TrackPurchase(order);        // no longer compiles in 3.14.1
    GrantRewards(order.CartOrdered);
    _store.ConfirmPurchase(order);
};

// After (3.14.1):
Palette.AttachPurchaseTracking(_store);   // once at init — SDK handles analytics

_store.OnPurchasePending += order =>     // studio keeps fulfillment-only handler
{
    GrantRewards(order.CartOrdered);
    _store.ConfirmPurchase(order);
};
```

### Notes
- `Documentation~/api-reference.md` is auto-regenerated from XML doc comments via `Tools~/build-docs.sh`. The pre-3.14.1 `TrackPurchase` entries will remain stale in the markdown until the next regen; source of truth is the `internal` modifier on `Runtime/Palette.cs`.

## [3.14.0] - 2026-04-22

Purchase-tracking hardening + fullscreen-ad screen-wake. Purchase side was triggered by QA evidence that `OnPurchaseConfirmed` can fire twice per purchase about one second apart on Google Play in-session, doubling downstream analytics revenue (Unity IAP v5 + Google Play framework quirk, separate from Unity's documented `OnPurchasePending` crash-replay behaviour). The dedup guard is now enforced SDK-side so integrations cannot produce duplicate purchase analytics regardless of which callback they subscribe to, and the wiring has been collapsed to a single idempotent call that cannot be miswired. Ads side closes a long-standing UX hole where some mediated networks don't set `FLAG_KEEP_SCREEN_ON` reliably during fullscreen ads, letting the device dim or sleep mid-impression.

### Added
- **`Palette.AttachPurchaseTracking(StoreController store)`**: one-call wiring for Unity IAP v5 purchase tracking. Subscribes `OnPurchasePending += Palette.TrackPurchase` on the SDK's behalf so analytics fan-out cannot be forgotten or miswired. Idempotent via a session-scoped `HashSet<StoreController>` — repeat calls with the same controller are logged and dropped. Manual subscription (`_store.OnPurchasePending += Palette.TrackPurchase;`) still works identically for studios that want to own the wiring. Gated on `UNITY_PURCHASING_INSTALLED`.

### Fixed
- **Duplicate purchase analytics from Google Play `OnPurchaseConfirmed` double-fire**: QA observed `OnPurchaseConfirmed` firing twice about one second apart per purchase on Google Play, inflating Firebase/Adjust/GA/TikTok revenue by 2x. Fix is session-wide TxID dedup enforced inside the low-level `Palette.TrackPurchase(double, string, ...)` chokepoint that every overload funnels through. All three entry points (`TrackPurchase(PendingOrder)`, `TrackPurchase(Product)`, `TrackPurchase(double, ...)` low-level) are now idempotent per `transactionId` for the session: second call with the same non-empty TxID is dropped before fan-out to Adjust/Firebase/GA/TikTok with a `Debug.LogWarning`. Fails open on empty/null TxID (cannot dedup what we cannot observe). Placed after price/currency validation so a bad-payload first call does not burn the TxID slot for a corrected retry. Also covers Unity-documented `OnPurchasePending` crash-replay (see https://docs.unity.com/ugs/en-us/manual/iap/manual/purchases — "may be called at any point following a successful initialization ... consider implementing your own de-duplication logic"). Studios are **not required** to keep their own TxID HashSet for analytics any more.
- **Screen sleeping / dimming during fullscreen ads** (`MaxAdapterImpl`): MAX and mediated ad networks do not consistently set `FLAG_KEEP_SCREEN_ON` on every adapter, so on long rewarded/interstitial impressions the device could dim or sleep — ruining the impression and the reward handshake. `AcquireScreenWake()` (`Screen.sleepTimeout = NeverSleep`) now wraps `MaxSdk.ShowRewardedAd` / `ShowInterstitial`, paired with `ReleaseScreenWake()` in `OnRewardedAdHidden` / `OnRewardedAdDisplayFailed` / `OnInterstitialAdHidden` / `OnInterstitialAdDisplayFailed` (saves and restores the prior timeout rather than hardcoding back to `SystemSetting`). `Application.focusChanged` is subscribed as a safety net — if a callback is somehow missed, the wake lock is released the moment the app regains focus, so the device can never get stuck in never-sleep mode after an ad.

### Changed
- **`Documentation~/architecture.md` Purchase Attribution diagram**: canonical wiring is now `Palette.AttachPurchaseTracking(_store)`; dedup chokepoint documented on the low-level `TrackPurchase`.

### Expected dashboard deltas after rollout
- **Android Firebase / Adjust / GA purchase revenue may drop** on integrations where the studio subscribed `OnPurchaseConfirmed` and called `Palette.TrackPurchase` (or any analytics event) from there without their own dedup. Correction of the in-session double, not regression.

## [3.13.0] - 2026-04-21

Revenue-integrity release. Triggered by a live-fire BigQuery audit that exposed Android purchases landing in Firebase with `currency="Tier"` / `value=NULL` (`firebase_error=19 / error_value="currency"` observed in raw events) and an iOS `transaction_id` regression after a Unity IAP v5 CorePro migration. A full vendor-deprecation audit ran in parallel: every third-party API the SDK calls was cross-checked against live 2026 documentation (AppLovin / Axon, Adjust v5, Firebase Unity 13.x, Facebook v18, GameAnalytics, Unity IAP 5.2.1). This release ships the Unity IAP v5 migration path plus three best-practice gaps closed (Crashlytics fatal routing, Adjust iOS ATT wait, GA4 `items[]` shape).

### Added
- **`Palette.TrackPurchase(PendingOrder)`**: canonical Unity IAP v5 overload. Reads `order.Info.TransactionID` + `order.Info.Receipt` while the order is still in `Pending` state — the only lifecycle point that captures `transactionId` reliably on consumables. Subscribe to `StoreController.OnPurchasePending` and call **before** `StoreController.ConfirmPurchase(order)`. Per [Unity IAP 5.2.1 `IOrderInfo` docs](https://docs.unity3d.com/Packages/com.unity.purchasing@5.2/api/UnityEngine.Purchasing.IOrderInfo.html), both fields are cleared for consumables once the order transitions to `ConfirmedOrder`. Preserves the existing price/currency validation and fires `sorolla_purchase_data_quality_failure` with `source: "pending_order"` on invalid metadata.
- **`sorolla_purchase_data_quality_failure` Firebase event**: diagnostic event fired whenever `TrackPurchase` drops a call for data-quality reasons. Params: `reason` (`non_positive_price` | `non_iso_currency` | `non_iso_currency_lowlevel`), `raw_currency`, `raw_price`, `product_id`, `platform`, `source` (`pending_order` | `product_legacy` | low-level). Queryable in BQ to detect upstream Unity IAP breakage or consumer-side plumbing regressions without logcat.

### Fixed
- **Android `currency="Tier"` / `value=NULL` data corruption**: `Palette.TrackPurchase` no longer silently forwards invalid currency or non-positive price to Firebase / Adjust / GA / TikTok. Firebase strips `value` server-side on non-ISO 4217 currency (`firebase_error=19 / error_value="currency"`, observed in BQ) and MMPs reject outright — forwarding corrupted revenue attribution across every downstream pipeline. All three validation paths (`PendingOrder` overload, legacy `Product` overload, and the low-level `TrackPurchase(double, string, …)` entry point) now drop + `Debug.LogError` + emit the diagnostic event. Upstream cause of bad Unity IAP metadata on Android is not yet identified; the diagnostic event captures the raw shape for forensic analysis on next repro.
- **iOS `transaction_id` missing on consumables post Unity IAP v5 upgrade**: root cause is the `PendingOrder` vs `ConfirmedOrder` lifecycle behaviour above. Fixed by migrating consumer tracking to the new `PendingOrder` overload (see Migration below).

### Changed
- **Low-level `Palette.TrackPurchase(double amount, string currency, …)` currency guard**: upgraded from warn-and-proceed to **drop** on non-ISO 4217 currency. Revenue integrity > coverage — a wrong-currency event corrupts every downstream pipeline. Better no event than broken revenue.
- **Firebase Crashlytics** (`FirebaseCrashlyticsAdapterImpl`):
  - `Crashlytics.ReportUncaughtExceptionsAsFatal = true` set on init (v10.4.0+ recommended pattern, per https://firebase.google.com/docs/crashlytics/unity/customize-crash-reports). Uncaught C# exceptions now surface as **fatal** in the Crashlytics dashboard; previously they were either missed or miscategorized as non-fatal.
  - `Application.logMessageReceived` handler no longer calls `LogException` for `LogType.Exception` — native auto-capture handles those now, and manual logging would double-report (fatal + non-fatal) the same exception. `LogType.Error` and `LogType.Assert` are still captured as `Crashlytics.Log` breadcrumbs for context.
- **Firebase `purchase` event items[]** (`FirebaseAdapterImpl.TrackPurchase`): now includes `ParameterPrice` (= event value, single-item IAP) and `ParameterQuantity` (= 1) alongside the existing `ParameterItemID`. Required by GA4's canonical purchase shape for the Monetization > In-app purchases per-product breakdown to populate; previously only total revenue flowed via top-level `value` and the per-product drill-down was degraded.
- **Adjust iOS `AttConsentWaitingInterval = 60`** on `AdjustConfig` (`AdjustAdapterImpl.Initialize`). Delays install event up to 60s so Adjust captures IDFA after the ATT prompt resolves; previously installs could fire before ATT landed → IDFA missing → degraded attribution on non-SKAN paths. No-op on Android.
- **`Documentation~/architecture.md` Purchase Attribution diagram**: now documents the v5 entry point (`OnPurchasePending` → `TrackPurchase(PendingOrder)`) as the canonical path; legacy `Product` / `AutoTracker` paths shown as transition shims.

### Deprecated
- **`Palette.TrackPurchase(Product)`** marked `[Obsolete]`: Unity IAP v5 marks `Product.transactionID` and `Product.receipt` as `[Obsolete]` (see [upgrade-to-iap-v5](https://docs.unity.com/en-us/iap/upgrade-to-iap-v5)), and for consumables both fields are empty after `ConfirmPurchase`. Migrate to `TrackPurchase(PendingOrder)`. Still functional for v4 projects during the transition; `sorolla_purchase_data_quality_failure` tags these with `source: "product_legacy"` for migration-tracking.
- **`Palette.Purchasing.AutoTracker`** marked `[Obsolete]`: built on `IDetailedStoreListener` which Unity obsoleted in v5. Does not work with `UnityIAPServices.StoreController`. No direct replacement — subscribe `StoreController.OnPurchasePending += order => Palette.TrackPurchase(order);` at init instead (still a one-line integration).

### Migration
```csharp
// Before (Unity IAP v4, now Obsolete):
public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
{
    Palette.TrackPurchase(e.purchasedProduct);
    return PurchaseProcessingResult.Complete;
}

// After (Unity IAP v5 CorePro):
_storeController.OnPurchasePending += order =>
{
    Palette.TrackPurchase(order);        // track first — transactionID still valid
    GrantRewards(order.CartOrdered);     // fulfillment
    _storeController.ConfirmPurchase(order);
};
```

### Expected dashboard deltas after rollout
- **iOS Firebase revenue may drop ~50%** once consumer-side duplicate-fire in `HandlePurchaseConfirmed` is resolved (tracked separately by the integration agent). Each purchase was firing twice ~1s apart with the same payload, inflating aggregate revenue. Correction, not regression.
- **Android Firebase `purchase` events may drop** as the SDK starts rejecting invalid-currency payloads. Affected purchases surface in the `sorolla_purchase_data_quality_failure` event stream with raw metadata; these were previously landing with `value=NULL` and unattributable anyway.
- **Crashlytics fatal count will rise**: C# uncaught exceptions now land in the fatal bucket rather than non-fatal. Not new crashes — re-categorization.

## [3.12.1] - 2026-04-21

Surfaces AppLovin's built-in ad-network debug tools through the `Palette` API so game code and the DebugUI sample don't need to reach into `MaxSdk.*` directly.

### Added
- **`Palette.ShowMediationDebugger()`**: opens AppLovin's Mediation Debugger — an in-app modal listing every integrated ad network, its adapter SDK version, adapter class findability, ad-unit config, and a per-network "Live Test Ads" button to force end-to-end delivery from each network. The canonical tool for verifying "is this ad network actually wired up". No-op with warning log when MAX isn't installed.
- **`Palette.ShowCreativeDebugger()`**: opens AppLovin's Creative Debugger — while enabled, long-pressing a displayed ad overlays its network, ad unit, bid price, waterfall position, and creative ID. Diagnostic for "why did that specific ad show" post-mortems. No-op with warning log when MAX isn't installed.

## [3.12.0] - 2026-04-21

Follow-up to `3.11.0`. Cleaned up consent fan-out to remove a redundant second propagation pass, deleted a dead Adjust init branch that no deployment path actually reaches, and reshaped ad-failure telemetry toward user-intent events so offline / VPN / no-fill sessions show up in the in-app funnel instead of disappearing into MAX's dashboard.

### Added
- **`ad_show_requested` analytics event**: fired on every `Palette.ShowRewardedAd` / `Palette.ShowInterstitialAd` call. Params: `ad_format` (`rewarded` | `interstitial`). Pairs with the existing `ad_impression` event so studios can compute `show_rate = ad_impression / ad_show_requested` in Firebase / BigQuery - the missing denominator for ads-not-shown analysis.
- **`ad_show_failed` analytics event**: fired when a show call returns without displaying an ad. Params: `ad_format`, `reason` (`not_ready` | `not_initialized` | `display_error`), plus `network` + `error_code` + `mediated_error_code` when `reason == display_error`. `not_ready` is the signal for offline / VPN-blocked mediation / no-fill sessions; `display_error` catches the rarer "loaded but crashed on show" case.

### Changed
- **Consent fan-out in `Palette.OnMaxSdkInitialized` deduped**: `MaxAdapterImpl.UpdateConsentStatusFromConfig` already fires `OnConsentStatusChanged` before `OnSdkInitialized` (default status `Unknown` always transitions to a resolved value on first init), which reaches `Palette.OnMaxConsentChanged` and propagates to GA / Firebase / Facebook. The second pass in `OnMaxSdkInitialized` was redundant and fired back-to-back `UpdateConsent` calls on the same adapters. Removed; Adjust init + `UpdateConsent` stay in `OnMaxSdkInitialized` per MAX SDK docs ("initialize other SDKs INSIDE the MAX callback").

### Removed
- **Dead `#elif SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED` branch in `Palette.Initialize`**: assumed "Adjust without MAX" was a real deployment path. It isn't - Full mode always ships MAX + Adjust together; Prototype mode never ships Adjust. The branch also silently skipped `UpdateConsent` (pre-existing bug that would have bitten if the path ever ran). Adjust initialization is now solely inside `OnMaxSdkInitialized`, single source of truth.

## [3.11.0] - 2026-04-21

Consent propagation hardening, prompted by a production consent drop where ATT/CMP decisions were invisible in our own analytics. Three coupled fixes so Adjust honors mid-session consent, no events are lost during the MAX CMP resolution window, and the ATT / CMP decision is queryable from our own data.

### Added
- **`AdjustAdapter.UpdateConsent(bool)`**: consent now propagates to Adjust on both initial MAX CMP resolution and mid-session changes via `ShowPrivacyOptions()`. Denied consent calls `Adjust.Disable()` (reversible - user can re-grant later via the privacy form); consent granted calls `Adjust.Enable()`. `GdprForgetMe` deliberately not used here - reserved for explicit "delete my data" user actions.
- **`Palette.AttStatus`**: canonical read of iOS AppTrackingTransparency status. Wraps `ATTBridge.GetStatus()` so game code and debug UIs have one API (returns `Authorized` on non-iOS / Editor).
- **`att_decision` analytics event**: fired once per session on both standalone and MAX-path iOS builds. Params: `att_status`, `source` (standalone | max). Available in Firebase DebugView and GameAnalytics design events.
- **`consent_resolved` analytics event**: fired once per session on the MAX path after CMP resolves. Params: `max_status`, `consent`, `source`.
- **`consent_changed` analytics event**: fired each time `Palette.OnMaxConsentChanged` transitions (privacy-options revoke / grant). Params: `max_status`, `consent`.

### Changed
- **Pre-init events now buffered, not dropped**: events fired from game `Awake` / `Start` on the iOS + MAX path used to be silently dropped during the ~1-3s window before MAX CMP resolved (`IsInitialized` stayed `false` until `OnMaxSdkInitialized` fired). Added a Palette-level `QueueOrExecute` + `FlushPending` (mirrors the pattern already used inside `FirebaseAdapterImpl`). All analytics entry points - `TrackEvent`, `TrackDesign`, `TrackProgression`, `TrackResource`, `TrackPurchase`, `Palette.Level.Start/Complete/Fail`, `Palette.Economy.Earn/Spend`, `SetUserId`, `SetUserProperty`, Crashlytics helpers - now queue instead of drop and flush with resolved consent. Queue capped at 256 events (oldest dropped + warn logged). `Palette.Level.Start` keeps timestamp capture synchronous so auto-duration still reflects player wall-time, not flush time.
- **`EnsureInit` helper removed** from `Palette.cs`: no callers left after the queue refactor. `GameAnalyticsAdapter` keeps its own separate `EnsureInit` (adapter-internal, unchanged).

### Fixed
- **Adjust ignored mid-session consent changes**: `Palette.OnMaxConsentChanged` propagated to GA / Firebase / Facebook but not Adjust. EU users who revoked via the privacy form kept getting attribution events - GDPR exposure. Now propagates to Adjust too alongside the others.
- **Adjust enabled despite initial consent denied**: `Palette.OnMaxSdkInitialized` unconditionally called `InitializeAdjust` after CMP resolved regardless of the resolved `consent` bool, so tracking began for users who said no. Now calls `AdjustAdapter.UpdateConsent(consent)` immediately after init to disable if denied.
- **ATT / CMP decisions invisible in our own analytics**: SDK logged decisions locally only. When consent rates dropped overnight, there was no first-party event to query against GA / Firebase. The three new events above close that gap - cohorts can be built on `att_status` / `max_status` directly.

## [3.10.0] - 2026-04-20

DX-first pass on progression + economy APIs. Continues the `3.9.2` `TrackPurchase` hotfix pattern (see `Internal~/dx-first-audit.md`): primitive-accepting, stringly-typed entry points get typed wrappers so studios can't silently corrupt data via typos.

### Added
- **`Palette.Level.Start(int level, int? world=null)` / `Complete(int level, int? world=null, int score=0)` / `Fail(int level, int? world=null, int score=0)`**: typed level progression API. Replaces `TrackProgression(ProgressionStatus, string, string, string, int, Dictionary)` - no more 3-slot string arrays, no more stringly-typed status, no manual duration math. Optional trailing `Dictionary<string, object> extraParams` preserves the escape hatch for Firebase-specific context. Input validated: non-positive `level` or `world` is rejected with a clear log.
- **Wire format**: `level_name = "world_{W}_level_{L}"` when `world` supplied, else `"level_{L}"`. Consistent across studios - no more spelling drift.
- **Auto-duration tracking**: `Palette.Level.Start` records `Time.realtimeSinceStartup`; matching `Complete`/`Fail` auto-fills `duration_sec` on the Firebase `level_end` event. Studios never touch a stopwatch.
- **`Palette.Economy.Earn(CurrencyId, int, EconomySource, string itemId=null)` / `Spend(CurrencyId, int, EconomySink, string itemId=null)`**: typed economy API. Replaces `TrackResource(ResourceFlowType, string, float, string, string, Dictionary)`.
- **`CurrencyId` enum**: `Coins`, `Gems`, `Energy`, `Lives`. Curated by Sorolla - new currencies require an SDK PR, which fails at compile time rather than silently fragmenting analytics with typo'd strings.
- **`EconomySource` enum**: `LevelReward`, `DailyBonus`, `AdReward`, `IapGrant`, `Achievement`, `Gift`, `Starter`, `Other`. Curated by Sorolla so cross-game analytics aggregate correctly.
- **`EconomySink` enum**: `Booster`, `Continue`, `Unlock`, `Cosmetic`, `ShopPurchase`, `Upgrade`, `Other`. `Other` logs a warning when hit so missing categories surface in telemetry and can be added in `3.10.x`.
- **Input validation on Economy**: rejects non-positive amounts at the entry point, logs a clear diagnostic pointing at the offending call.

### Deprecated
- **`Palette.TrackProgression`**: marked `[Obsolete]`, points studios at `Palette.Level.*`. Wire format unchanged - still routes to the same GA4 / Firebase adapter calls underneath.
- **`Palette.TrackResource`**: marked `[Obsolete]`, points studios at `Palette.Economy.*`. Wire format unchanged.

### Changed
- **`Palette` is now `partial`**: new surface (`Palette.Level`, `Palette.Economy`) split into `Palette.Level.cs` + `Palette.Economy.cs` for readability without shuffling the existing ~1,200-line `Palette.cs`.

### Fixed
- **IAP events never reached GameAnalytics**: `Palette.TrackPurchase` fanned out to Adjust + TikTok + Firebase but never called `GameAnalyticsAdapter.TrackBusinessEvent`. Adapter methods existed (`NewBusinessEvent` / `NewBusinessEventGooglePlay` / `NewBusinessEventIOS`) but were orphaned - GA business-event dashboards showed zero revenue across all games. Now calls the generic `TrackBusinessEvent(currency, amountInCents, "iap", productId, null)` on every purchase. Receipt-validated GooglePlay/iOS variants deferred to a follow-up (requires wiring `ReceiptParser` output through to the GA adapter). `amountInCents` uses `Math.Round(amount * 100)` to avoid float-truncation bugs (`0.99 * 100 -> 98` without rounding).

### Removed
- **`MaxAdapterImpl.SubscribeILRD()`**: was attempting to wire MAX's Impression Level Revenue Data to GameAnalytics via `GameAnalyticsSDK.GameAnalyticsILRD.SubscribeMaxImpressions()`. That type does **not** ship in GA's UPM package - per [GA's own docs](https://github.com/GameAnalytics/GA-SDK-UNITY/blob/master/README.md) it's distributed as a separate `.unitypackage` add-on due to ad-SDK dependency conflicts. Studios using GA via UPM without importing the ILRD add-on would hit `CS0234: 'GameAnalyticsILRD' does not exist in namespace 'GameAnalyticsSDK'`. Dropped entirely rather than adding a reflection shim - Firebase is replacing GA in the near term. **Ad-revenue fan-out to Adjust + Firebase + TikTok is unaffected** - that path goes through `MaxAdapterImpl.TrackAdRevenue` listening to `OnAdRevenuePaidEvent`, which is entirely separate from GA's ILRD callback.

## [3.9.3] - 2026-04-17

### Fixed
- **`Sorolla.Runtime` asmdef missing `Unity.Purchasing` reference**: 3.9.2 added the `UNITY_PURCHASING_INSTALLED` version define and the `Palette.TrackPurchase(Product)` / `Palette.Purchasing.AutoTracker` code paths, but forgot to list `Unity.Purchasing` in `Sorolla.Runtime.asmdef` `references`. Games with `com.unity.purchasing` installed failed to compile with `CS0234: The type or namespace 'Purchasing' does not exist in the namespace 'UnityEngine'`. Fixed by adding `Unity.Purchasing` to the references array. Projects without the IAP package are unaffected (Unity emits a harmless missing-reference warning; all IAP-using code is still guarded by `#if UNITY_PURCHASING_INSTALLED`).

## [3.9.2] - 2026-04-17

### Added
- **`Palette.TrackPurchase(Product)` overload**: Takes a Unity IAP `Product` directly. Derives amount, currency, productId, transactionId, and (on Android) purchaseToken automatically from `Product.metadata` + `Product.receipt` + `Product.transactionID`. Impossible to pass wrong data. Gated on `com.unity.purchasing` presence (`UNITY_PURCHASING_INSTALLED`).
- **`Palette.Purchasing.AutoTracker`**: Drop-in `IDetailedStoreListener` decorator. Wrap your listener once at `UnityPurchasing.Initialize(new Palette.Purchasing.AutoTracker(this), builder)` and every confirmed purchase is auto-tracked with verified receipt - no per-purchase call required. Gated on `com.unity.purchasing` presence.
- **`Sorolla.Palette.Purchasing.ReceiptParser`**: Parses Unity IAP 4.x/5.x receipt JSON (three-level: outer -> Android Payload -> Play Billing json) using `JsonUtility` only. No dependency on `com.unity.purchasing`. Powers the automated extraction inside the `Product` overload.

### Changed
- **Input validation on primitive `TrackPurchase`**: The low-level 5-arg overload now warns on non-ISO-4217 currency codes and drops events with non-positive amounts. Catches "tier index as price / `Tier` as currency" class of bugs at integration time instead of after weeks of polluted Adjust/MMP data. Log message points studios at the recommended `Palette.TrackPurchase(product)` entry point.

## [3.9.1] - 2026-04-16

### Added
- **Master verbose logging toggle**: Single toggle to enable/disable verbose logging across all vendor SDKs.
- **Facebook `UpdateConsent` method**: Update Facebook SDK consent state after initial initialization.
- **Auto-set MAX consent flow privacy policy URL**: Configuration window automatically populates the privacy policy URL for MAX's CMP consent flow.
- **Debug UI decoupled from vendor SDKs**: Debug UI cards no longer depend on vendor SDK assemblies; card visibility is mode-aware (Prototype vs Full).
- **Internal SDK onboarding checklist**: Step-by-step runbook for integrating the SDK into a new game.

### Changed
- **Adapter classes made internal**: All adapter classes are now `internal`; all SDK access routes through the `Palette` public API.

### Fixed
- **MAX Ad Review (Quality Service)**: Build validator now auto-enables instead of auto-disabling Quality Service.

## [3.9.0] - 2026-04-14

### Added
- **`EventNameSanitizer` adapter**: Sanitizes analytics event names before forwarding to downstream SDKs.
- **Adjust purchase event token in Configuration window**: Token was only accessible on the raw `SorollaConfig` asset; now exposed in **Palette > Configuration** under the Adjust section.

### Changed
- **Purchase verification routing**: `Palette.TrackPurchase` now routes to Adjust's `VerifyAndTrack` APIs for receipt verification. iOS uses App Store verification when `transactionId` is provided; Android uses Play Store verification when `purchaseToken` is provided. Falls back to simple event tracking otherwise.
- **DRY purchase event setup**: Adjust adapter uses shared `BuildPurchaseEvent` helper for revenue, productId, deduplicationId, and partner/callback parameters across all purchase paths.
- **New `purchaseToken` parameter**: `TrackPurchase` accepts an optional `purchaseToken` for Android Play Store verification. Existing callers are unaffected (default null).

### Fixed
- **Remote Config decoupled from MAX consent gate**: `GetRemoteConfig*` and `IsRemoteConfigReady` no longer wait for `Palette.IsInitialized` (which blocks on MAX consent). Firebase RC is independently ready earlier. Also fixes a first-launch race where `IsReady` was set before `SetDefaultsAsync` completed, causing reads to return zeros.
- **Remote Config callback queuing**: Callbacks passed to `FetchRemoteConfig` during an in-flight fetch were silently dropped. Now queued and invoked when the single fetch completes.
- **Remote Config redundant fetch skip**: When auto-fetch completes before game code calls `FetchRemoteConfig`, the game's call no longer triggers a second unnecessary network fetch.

## [3.8.0] - 2026-04-09

### Changed
- **Manifest ownership refactor**: `AndroidManifestSanitizer` is now the single owner of all Android manifest logic - source manifests (pre-build), launcher manifests, and auto-generated Gradle manifests (post-export). `GradlePropertiesFixer` delegates all manifest work to it.
- **Conditional tools:replace**: `FixMainActivity()` only adds `tools:replace="android:theme"` when a custom launcher manifest exists. Previously added unconditionally, which could break Unity's deployment code.
- **Single-pass detection**: `SanitizeWithDiagnostics()` captures remaining issues after fixing, eliminating redundant re-detection in `CheckAndroidManifest()`.
- **Auto-fix on domain reload**: `BuildHealthConsoleNotifier` runs `RunAutoFixes()` on every domain reload. Manifest, Gradle, and MAX problems are fixed automatically.

### Fixed
- **Gradle manifest merge conflicts**: Activity gets `tools:replace="android:theme"` (conditional on launcher ownership) to prevent theme collisions. Fixes `Colliding Attributes` build errors on Unity 6.
- **Post-export launcher manifest patching**: `IPostGenerateGradleAndroidProject` hook enforces activity class, theme, and LAUNCHER intent invariants on auto-generated Gradle manifests.
- **Post-export LAUNCHER safety**: `StripLibraryLauncherIntent` now verifies the launcher module actually has a LAUNCHER activity before stripping it from unityLibrary. Unity 6 always generates the launcher directory even with `useCustomLauncherManifest` OFF, but the auto-generated manifest may be empty. Fixes `DeploymentOperationFailedException: No activity with action MAIN and category LAUNCHER`.
- **Pre-build auto-fix persistence**: `Sanitize()` no longer calls `AssetDatabase.Refresh()` during build preprocessing, preventing Firebase Editor from regenerating the manifest and undoing the fix.
- **Launcher manifest fixes absorbed into Sanitize()**: Both library and launcher manifests are handled in a single `Sanitize()` call. `RunAutoFixes()` reduced from 7 lines to 1.

## [3.7.2] - 2026-04-08

### Fixed
- **`FetchRemoteConfig` log clarity**: The adapter logged `activated: False` on every fetch after the first, even when Remote Config was fully live. The bool reflects whether *this specific call* applied newly-fetched values (Firebase's `FetchAndActivateAsync()` semantics), not whether config is active - subsequent fetches within `MinimumFetchInterval` correctly return `false` since there's nothing new to activate. Log now shows `newValuesActivated` alongside `lastFetchStatus` (Success/Failure/Pending) so the actual fetch health is visible.

## [3.7.1] - 2026-04-08

### Added
- **DocFX full site + GitHub Pages pipeline**: Public API reference now builds into a browsable site and deploys to GitHub Pages on every push to master. `Tools~/build-docs.sh` regenerates the in-repo `Documentation~/api-reference.md` from `///` XML comments.

### Changed
- **`Tools/` renamed to `Tools~/`**: Unity's trailing-tilde convention hides the directory from the asset pipeline - studios no longer see phantom `.meta` files or re-imports triggered by the docs-build scripts.

### Fixed (Firebase / GA4 spec compliance)
- **`level_fail` is gone**: Failed levels now fire `level_end` with `success=0` (and Complete fires `success=1`). `level_fail` was never a real GA4 event - the built-in Games > Levels report aggregates only `level_end`, so every failed attempt was previously invisible in reports.
- **`purchase` populates the In-app purchases report**: GA4 requires an `items[]` array on `purchase` to surface per-product revenue. `TrackPurchase()` now builds a single-item array with `item_id = productId`. Total revenue still flows; per-pack breakdowns now flow too.
- **Score moved off `level_end`**: Score is now fired as a separate `post_score` event with just the score, matching the canonical GA4 shape. Studios that need score-with-context join via session_id in BigQuery.
- **`ad_impression` uses GA4 constants**: All param names switched from string literals to `FirebaseAnalytics.Parameter*` constants (no behavior change, just typo-proof).
- **`item_name` constant**: Spend events use `FirebaseAnalytics.ParameterItemName` instead of the literal.
- **`SetUserProperty` validation**: Names > 24 chars, names with reserved prefixes (`ga_`/`google_`/`firebase_`/`_`) are dropped with a warning. Values > 36 chars are truncated.
- **Reserved event names blocked client-side**: `TrackEvent()` rejects GA4-reserved names (`session_start`, `screen_view`, `error`, `first_open`, etc.) and reserved prefixes with a warning instead of letting Firebase silently swallow them server-side.

### Fixed (Firebase adapters - init failure paths)
- **Zombie `FetchRemoteConfig` callback when Firebase core init fails terminally**: `FirebaseRemoteConfigAdapterImpl` would park the `onComplete` callback in `_pendingFetchCallback` forever whenever `FirebaseCoreManager.Initialize` reported `available=false` (Editor without Firebase native, failed dependency check, etc.). Consumers waiting on `Palette.OnInitialized` before calling `Palette.FetchRemoteConfig(cb)` would never see `cb` fire, and any `RemoteConfig.Reload()` chained after it would hang. Adapter now tracks a terminal `_initFailed` state and fails the callback fast with `false` on the same frame.
- **`FirebaseAdapterImpl` / `FirebaseCrashlyticsAdapterImpl` pending-queue leak**: Same root cause - when Firebase core failed terminally, `TrackEvent`/`Log`/`SetCustomKey` calls kept accumulating in the pending queues forever with no flush path. Both adapters now drop silently after terminal init failure and clear any backlog that queued before the failure callback arrived.

### Migration
- Studios with custom GA4 dashboards keyed on `level_fail`: switch the filter to `level_end` where `success = 0`.
- Studios reading `score` off `level_end` rows in BigQuery: read it from the matching `post_score` event in the same session instead.
- Studios firing `Palette.TrackEvent("session_start", ...)` or any other reserved name: rename it. The event was being dropped by Firebase already - now you'll see a warning.
- Studios that gated on `Palette.IsRemoteConfigReady()` as a client-side workaround for the RemoteConfig hang can drop the gate. `Palette.FetchRemoteConfig(cb)` now always invokes `cb` (true on success, false on fetch/init failure).

## [3.7.0] - 2026-04-03

### Added
- **`Palette.TrackEvent(name, params)`**: Structured custom events with full Firebase/GA4 parameter support. Replaces `TrackDesign()` for new code. GA receives best-effort design event fallback.
- **`extraParams` on TrackProgression/TrackResource**: Optional `Dictionary<string, object>` for Firebase-specific context (ignored by GameAnalytics)
- **`Palette.SetUserId(userId)`**: Unified user identity across Firebase Analytics, Crashlytics, and Adjust
- **`Palette.SetUserProperty(name, value)`**: Firebase audience segmentation
- **Real-time Remote Config**: `OnRemoteConfigUpdated` event fires when Firebase config changes server-side. `AutoActivateRemoteConfigUpdates` controls whether values apply immediately or on manual `ActivateRemoteConfigAsync()` call
- **`Palette.SetRemoteConfigDefaults(defaults)`**: Set in-app fallback values before Firebase loads
- **Event validation**: Reserved prefix rejection (`firebase_`, `google_`, `ga_`), param type/count limits (25 params, supported types only), name sanitization (40 char max, alphanumeric + underscore)
- **Debug UI**: Custom events tab with structured event buttons, real-time Remote Config controls (SetDefaults, Activate, AutoActivate toggle), validation test button

### Changed
- **`TrackDesign()` deprecated**: Use `TrackEvent()` for new code. `TrackDesign()` still works but marked `[Obsolete]`
- **`TrackPurchase()` extended**: Now accepts `productId` and `transactionId` for Firebase deduplication
- **Firebase progression mapping**: `level_fail` added (was only `level_start`/`level_end`). Canonical level name built from progression parts (`"world3_level12"`)

### Documentation
- Removed internal/AI files from repo (CLAUDE.md, ralph.md, bug reports, completed plans, AI agent reference)
- Consolidated LEARNINGS.md into DEVLOG.md
- Promoted architecture.md and dashboard-setup.md from internal/ to public docs
- Updated api-reference.md, firebase guide, README, quick-start for v3.7.0 API

## [3.6.1] - 2026-04-01

### Fixed
- **Firebase Editor Play mode**: Clear error message instead of cryptic `TypeInitializationException` when native plugin unavailable in Editor (does not affect device builds)
- **BuildValidator**: Checks for `google-services.json` (Android) / `GoogleService-Info.plist` (iOS) when Firebase is installed, blocks build with actionable error if missing

## [3.6.0] - 2026-04-01

### Added
- **`Palette.TrackPurchase(amount, currency)`**: Unified purchase tracking that fans out to Adjust, TikTok, and Firebase in one call
- **`Palette.ShowDebugger()`**: Public API to open Debug UI programmatically + drop-in `Sorolla Debugger.prefab` (DontDestroyOnLoad, triple-tap or API)
- **TikTok Debug UI card**: Shows TikTok SDK status, config fields, and test event buttons
- **Editor tests**: AndroidManifestSanitizer and BuildValidator test suites with fixture manifests
- **Android build compatibility guide**: Unity version matrix, R8/AGP, split manifests, `androidApplicationEntry` bitmask
- **External studio onboarding docs**: Restructured `switching-to-full.md` with RACI matrix, identifier cross-reference table, QA preflight checklist, consent ordering. Added `index.md` landing page, `guides/tiktok.md`, internal dashboard setup runbook.

### Fixed
- **SDK initialization deferred until MAX consent resolves**: `Palette.IsInitialized` and `OnInitialized` now fire after MAX CMP completes, preventing pre-consent data from reaching downstream SDKs. `SorollaBootstrapper` passes `consent:false` when MAX is installed.
- **Google Ad Manager required for UMP consent**: MAX needs a Google mediated network adapter (Ad Manager or AdMob) installed in Integration Manager for the UMP form to render. Without it, only the MAX privacy popup appeared - GDPR CMP dialog was silently missing. Documented across gdpr.md, switching-to-full.md, CLAUDE.md, and internal runbook.
- **Firebase event names**: Remapped to GA4 official constants (`level_start`, `level_end`, `level_up`, `earn_virtual_currency`, `spend_virtual_currency`)
- **GameActivity detection**: Uses `SerializedObject` to read `androidApplicationEntry` for Unity 2022-6 compatibility (was using compile-time version checks)
- **`DexingArtifactTransform` fix**: Guarded to Unity < 6 only (AGP 8.10.0 doesn't need it)

### Changed
- **BuildValidator hardened**: R8/AGP version checks, `androidApplicationEntry` validation, LauncherManifest activity detection
- **AndroidManifestSanitizer**: Activity-class detection and LauncherManifest fixes for Unity 6 split manifest architecture
- **GDPR duplication removed**: `switching-to-full.md` links to `guides/gdpr.md` instead of reproducing it
- Removed Phase 1 test scaffolding, kept studio-value features
- Added `PaletteConstants` for shared string literals

## [3.5.0] - 2026-03-24

### Fixed
- **GDPR consent propagation**: GameAnalytics and Firebase Analytics now receive consent flags at init and dynamically when MAX CMP (UMP) resolves. Previously both initialized unconditionally regardless of consent status.
- **TikTok credential logging**: Removed app IDs and TikTok app IDs from Debug.Log output during initialization
- **TikTok debug mode**: Decoupled from `Debug.isDebugBuild` — now controlled via explicit `tiktokDebugMode` toggle in SorollaConfig. Prevents verbose TikTok logging in distributed beta builds.

### Changed
- **AppLovin MAX SDK**: Updated from 8.5.0 to 8.6.1
- `GameAnalyticsAdapter.Initialize()` now accepts `bool consent` and calls `SetEnabledEventSubmission`
- `FirebaseAdapter.Initialize()` now accepts `bool consent` and calls `SetAnalyticsCollectionEnabled`
- `Palette` subscribes to `MaxAdapter.OnConsentStatusChanged` to propagate consent updates to GA and Firebase after CMP resolves

### Added
- `GameAnalyticsAdapter.UpdateConsent(bool)` — runtime consent update for GA event submission
- `FirebaseAdapter.UpdateConsent(bool)` — runtime consent update for Firebase analytics collection
- `SorollaConfig.tiktokDebugMode` field — explicit opt-in for TikTok SDK debug logging

## [3.4.2] - 2026-03-10

### Fixed
- **ATT: Removed `com.unity.ads.ios-support` dependency**: Replaced with native Objective-C bridge (`ATTBridge.cs` + `SorollaATT.mm`). Root cause of compilation failures when the package was absent - previous patches addressed symptoms but not the hard asmdef reference. Zero-dependency pattern, same as TikTok.
- **SorollaBootstrapper persistence**: Removed over-engineered `MakePersistent` scene validity check that could *prevent* `DontDestroyOnLoad` at `BeforeSceneLoad` timing. Added `EnsurePersistent()` fallback in `Start()` - guarantees the SDK survives fast scene transitions (e.g. splash screens that unload immediately).
- **Double-init warning**: `Palette.Initialize()` now tells developers to remove manual calls instead of a generic "Already initialized" message.

### Removed
- **Firebase migration popup**: The v3.1.0 one-time popup about Firebase being required is no longer relevant for new developers.

## [3.4.1] - 2026-03-06

### Fixed
- **GameAnalyticsAdapter**: Added `#if UNITY_ANDROID` guard to `TrackBusinessEventGooglePlay` - previously called an Android-only GA API on all platforms (would fail in Editor/iOS). Falls back to generic `NewBusinessEvent` on non-Android.
- **SorollaBootstrapper**: Fixed iOS ATT dialog silently dropping when called too early. Added 1-frame + 1-second delay before `RequestAuthorizationTracking`. Switched to callback-based wait (was polling status in a loop). Skips request if status is already determined.

## [3.4.0] - 2026-02-18

### Added
- **TikTok Business SDK integration**: Native Bridge pattern — config-driven init, no compilation impact when unconfigured. Android (JNI) + iOS (Objective-C runtime). Supports `identify`, `track`, and custom events.
- **ProGuard rules for TikTok**: `-keep class com.tiktok.**` shipped in SDK package for release Android builds
- **SdkVersionSync editor utility**: Auto-updates stale manifest entries on domain reload

### Fixed
- **Firebase asmdef references**: Switched to `precompiledReferences` for custom Firebase UPM compatibility
- **GameActivityTheme.androidlib**: Fixes Unity 6000.3.x Android build failures
- **iOS ATT regression**: Restored `com.unity.ads.ios-support` dependency and correct namespace
- **SorollaTikTok.mm.meta**: Added explicit PluginImporter with iOS-only platform settings

## [3.3.1] - 2026-02-18

### Fixed
- **iOS ATT prompt in Prototype mode**: Standalone ATT dialog now shows automatically when MAX is not installed. Fixes App Store rejection for apps without AppLovin MAX.

## [3.3.0] - 2026-02-10

### Changed
- **Firebase is now optional in Prototype mode**: Only required in Full mode, can be manually installed in Prototype via UI
- **Mode table updated**: Prototype mode lists Firebase as optional, Full mode still requires it
- **SDK Overview UI**: Firebase row now shows Install button in Prototype mode, mode-aware required/optional labels

### Added
- **SdkVersionSync**: Auto-syncs installed SDK versions with `SdkRegistry` constants on domain reload
- **MAX version checker**: Queries AppLovin registry for latest MAX version, prompts Update/Skip/Later once per session
- **Firebase config sub-rows**: Build Health shows individual google-services.json / GoogleService-Info.plist status

### Fixed
- **Build validator**: Improved Firebase coherence checks for optional Firebase in Prototype mode
- **Migration popup**: Updated messaging for Firebase-optional flow
- **EDM4U sanitizer**: Better handling of dependency resolution edge cases

## [3.2.2] - 2026-01-26

### Added
- **GameAnalytics ILRD**: Automatic impression-level revenue tracking via `GameAnalyticsILRD.SubscribeMaxImpressions()`
- **Firebase `ad_impression` events**: Ad revenue now logged to Firebase Analytics with full parameters (`ad_platform`, `ad_source`, `ad_format`, `ad_unit_name`, `value`, `currency`)
- `FirebaseAdapter.TrackAdImpression()` public API for ad revenue tracking

### Changed
- Ad revenue now tracked to all three platforms: GameAnalytics (ILRD), Firebase (`ad_impression`), and Adjust
- `TrackAdRevenue` now includes ad format (INTERSTITIAL/REWARDED) for better analytics segmentation

## [3.2.1] - 2026-01-26

### Fixed
- **Build Health now shows missing required SDKs**: Error displayed when SDKs like Facebook are not installed
- **Improved Adjust Settings messages**: Clearer status when Adjust is not required or not installed

## [3.2.0] - 2026-01-26

### Added
- **Auto-install missing SDKs**: Clicking "Refresh" in Build Health or switching modes now auto-installs missing required SDKs
  - Fixes edge case where Full mode was active but Adjust SDK was missing
  - Only triggers on explicit user action (not on window open)
- **MAX SDK version checker**: Build Health now validates MAX SDK version against expected version
- **EDM4U duplicate detection**: Warns about duplicate External Dependency Manager installations

### Changed
- **Facebook SDK now Core**: Always installed in both Prototype and Full modes (was FullRequired)
- **Improved SDK installation UX**: Better feedback during SDK install/uninstall operations
- **Simplified editor UI**: Removed welcome screen, streamlined SorollaWindow

### Fixed
- **Unity 6 DontDestroyOnLoad**: Added robust handling to prevent assertion failures
- **Code preservation**: Added `[Preserve]` attributes to prevent IL2CPP stripping
- **Standardized log tags**: Consistent `[Palette]` prefix across all adapters

## [3.1.0] - 2026-01-13

### Changed
- **Firebase is now required** in all modes (Prototype + Full)
- Firebase packages auto-install on SDK import/upgrade
- SetupVersion bumped to v7 (triggers setup for upgrading users)
- **SdkInfo refactored to immutable struct** - prevents accidental mutation
- **SorollaWindow editor cleanup**:
  - Cached GUIStyles and SerializedObject to avoid GC pressure
  - Removed style mutation in OnGUI (uses DrawIcon helper)
  - Merged Firebase Configuration into Build Health as sub-rows
  - Moved MAX Ad Units to SDK Keys section
  - Simplified SDK overview rendering

### Added
- **Migration popup**: One-time guide for Firebase setup after SDK upgrade
- Links to Firebase Console and configuration window

### Fixed
- Removed module toggles for Firebase (no longer optional)
- Cleaned up stale Firebase references throughout codebase

### Documentation
- Updated `firebase.md` to reflect required status
- Added Firebase config step to `prototype-setup.md`
- Updated `full-setup.md` to show Firebase as required

## [3.0.0] - 2025-01-12

### Breaking Changes
- **Ad Unit IDs**: Replaced single fields with `PlatformAdUnitId` class containing Android/iOS variants
  - `maxRewardedAdUnitId` → `rewardedAdUnit.android` / `rewardedAdUnit.ios`
  - `maxInterstitialAdUnitId` → `interstitialAdUnit.android` / `interstitialAdUnit.ios`
  - `maxBannerAdUnitId` → `bannerAdUnit.android` / `bannerAdUnit.ios`
  - **Migration**: Re-enter ad unit IDs in the new platform-specific fields

### Added
- **Platform-Specific Ad Units**: `PlatformAdUnitId` class with `.Current` property for automatic platform selection
- **AppLovin Quality Service Auto-Fix**: Build validator auto-disables Quality Service to prevent 401 build failures
- **Duplicate Activity Detection**: AndroidManifestSanitizer now detects and removes duplicate SDK activities

### Fixed
- Setup version key updated from v3 to v6
- `s_config` → `Config` reference in `TrackDesign` method

### Changed
- Merged `FirebaseCoreManager` into `FirebaseAdapter.cs`
- Merged `BuildValidationWindow` menu into `SorollaWindow.cs`
- Merged `SorollaMode` enum into `SorollaSettings.cs`
- Removed unused `GetCrashlyticsStatus`/`GetRemoteConfigStatus` methods
- Removed unused `IsValid()` from `SorollaConfig`
- Various code simplifications and refactoring

## [2.3.3] - 2025-12-29

### Fixed
- **Fresh Import Compilation Errors**: Re-added `defineConstraints` to implementation assemblies
  - v2.3.2 incorrectly removed constraints, causing CS0246 errors on fresh imports without SDKs
  - Unity compiles C# files before checking assembly references, so constraints are required
- **EDM4U Gradle Java 17+ Compatibility**: Auto-configures EDM4U to use Unity's Gradle templates
  - Mitigates `java.lang.NoClassDefFoundError` on Unity 6+ (initial resolution may still show error)
  - EDM4U bundles Gradle 5.1.1 which is incompatible with Java 17+ (Unity 6 default JDK)
  - Automatically sets `PatchMainTemplateGradle`, `PatchPropertiesTemplateGradle`, `PatchSettingsTemplateGradle`
  - Error is transient: re-resolving after mode selection works correctly

### Technical Details
The v2.3.2 "assembly references as constraints" approach was wrong. Unity still attempts to compile C# files before checking if referenced assemblies exist.

**Correct pattern (now implemented):**
```
versionDefines: SDK installed → sets DEFINE (e.g., APPLOVIN_MAX_INSTALLED)
defineConstraints: ["DEFINE"] → assembly only compiles if DEFINE is set
```

**EDM4U Gradle fix:**
EDM4U's bundled Gradle 5.1.1 doesn't support Java 17+ (used by Unity 6). By enabling template patching, EDM4U integrates with Unity's Gradle version instead of using its bundled one.

## [2.3.2] - 2025-12-26

### Fixed
- **IL2CPP Stripping Protection**: Complete overhaul of code preservation strategy for Unity 6 compatibility
  - Added `[assembly: AlwaysLinkAssembly]` to all implementation assemblies (MAX, Adjust, Firebase)
  - Added `[Preserve]` attributes to Register() methods
  - Auto-copies `link.xml` to Assets folder on setup (Unity doesn't process link.xml from packages)
- **Loading Overlay**: Removed built-in Arial font dependency for cross-platform compatibility

### Added
- `SorollaSetup.CopyLinkXmlToAssets()` - Auto-copies stripping protection on package setup
- Three-layer code preservation: AlwaysLinkAssembly → [Preserve] → link.xml fallback

## [2.3.1] - 2025-12-24

### Fixed
- **Firebase Uninstall Config Sync**: Config flags now disabled **before** package removal to prevent domain reload interruption
- **Build Health Auto-Fix**: Refresh button now auto-fixes config sync issues (Firebase flags, mode mismatch)
  - Shows "AUTO-FIXED: Synced SorollaConfig with installed SDKs" when fixes applied

### Added
- `BuildValidator.FixConfigSync()` method for programmatic config repair

## [2.3.0] - 2025-12-24

### Changed
- **Adapter Architecture Overhaul**: Refactored all optional SDK adapters to use Interface + Registration pattern
  - Stubs always compile (no external dependencies)
  - Implementations in separate assemblies with `defineConstraints`
  - Runtime registration via `RuntimeInitializeOnLoadMethod`
- **Assembly Structure**: Split adapters into isolated assemblies
  - `Sorolla.Adapters` - Core stubs (always compiles)
  - `Sorolla.Adapters.Firebase` - Firebase implementations (compiles only when Firebase installed)
  - `Sorolla.Adapters.MAX` - MAX implementation (compiles only when MAX installed)
  - `Sorolla.Adapters.Adjust` - Adjust implementation (compiles only when Adjust installed)

### Added
- `AdRevenueInfo` struct for cross-SDK ad revenue tracking
- `IMaxAdapter`, `IAdjustAdapter`, `IFirebaseAdapter` internal interfaces

### Fixed
- **Prototype Mode Compilation**: SDK now compiles cleanly without Firebase/MAX/Adjust installed
  - Root cause: Unity resolves assembly references BEFORE evaluating `#if` preprocessor blocks
  - Solution: `defineConstraints` in child asmdefs prevent assembly compilation entirely when SDKs missing
- Removed unused `s_consentStatusChanged` field in SorollaSDK.cs

### Technical Details
The previous approach used `#if/#else` blocks with assembly references in a single asmdef. This failed because Unity's assembly resolution happens before C# compilation, causing "assembly not found" errors even for code inside `#if false` blocks.

New pattern:
```
Adapters/
├── Sorolla.Adapters.asmdef      (no external refs)
├── MaxAdapter.cs                 (stub → delegates to impl)
├── Firebase/
│   ├── Sorolla.Adapters.Firebase.asmdef  (defineConstraints: FIREBASE_*_INSTALLED)
│   └── FirebaseAdapterImpl.cs            (registers at runtime)
├── MAX/
│   └── ...
└── Adjust/
    └── ...
```

## [2.2.1] - 2025-12-23

### Fixed
- **MAX SDK Initialization**: Added missing `MaxSdk.SetSdkKey()` call - SDK key was passed but never used
- **Duplicate Registry Scope**: Fixed "com.applovin defined in multiple registries" error
  - Added `RemoveScopeFromRegistry()` to clean up duplicate scopes
  - Future installs now prevent `com.applovin` from being added to OpenUPM (should only be in AppLovin registry)
- **Prototype Mode Compilation**: Fixed "MaxAdapter does not exist" errors when MAX not installed
  - Added `#if` guards around all MaxAdapter references in SorollaSDK.cs
  - Removed hard assembly references from asmdef files (relies on auto-referencing + versionDefines)
  - All optional SDK adapters now compile cleanly when their packages aren't installed
- **SorollaSDK.cs**: Fixed `s_config` typo → `Config` on line 275

### Upgrade Notes
If you encounter errors after updating to 2.2.1, manual manifest fixes may be required **before opening Unity**:

**"com.applovin defined in multiple registries" error:**
1. Open `Packages/manifest.json` in a text editor
2. Find the `scopedRegistries` section with `"url": "https://package.openupm.com"`
3. Remove `"com.applovin"` from its `scopes` array (keep it only in AppLovin MAX registry)
4. Save and reopen Unity

**"AdjustSdk could not be found" error (Prototype mode only):**
1. Open `Packages/manifest.json`
2. Remove `"com.adjust.sdk"` from dependencies (not needed in Prototype mode)
3. Delete `Library/PackageCache/com.adjust.sdk*` folder if present
4. Save and reopen Unity

## [2.2.0] - 2025-12-18

### Added
- **SDK Overview Section**: Unified view combining install status + config status per SDK
  - Shows all SDKs with: ✓/✗/○ install icon, config status, single action button
  - Firebase shows nested module status (Analytics, Crashlytics, Remote Config)
  - Replaces previous "Setup Checklist" and "SDK Status" sections
- **Build Health Validator**: Pre-build validation integrated into Configuration window
  - 6 validation checks: SDK Versions, Mode Consistency, Scoped Registries, Firebase Coherence, Config Sync, Android Manifest
  - Visual display of all checks with status icons (✓/⚠/✗)
  - Auto-runs on window open and after mode switch
  - Pre-build hook via `IPreprocessBuildWithReport` - errors block builds
- **AndroidManifest Sanitizer**: Auto-detects and removes orphaned SDK entries
  - Fixes `ClassNotFoundException` crashes when switching modes
  - Creates backup before modifying manifest
  - Menu item: `SorollaSDK > Tools > Sanitize Android Manifest`
- **UMP Consent Integration**: GDPR/ATT consent API via MAX (see `gdpr-consent-setup.md`)
  - `SorollaSDK.ConsentStatus` - Current consent state
  - `SorollaSDK.CanRequestAds` - Whether ads can be shown
  - `SorollaSDK.ShowPrivacyOptions()` - Opens UMP privacy form
  - `OnConsentStatusChanged` event for UI updates

### Changed
- **UI Consolidation**: Merged 3 sections into 2 for cleaner, less redundant interface
  - SDK Overview: per-SDK install + config status (was: Setup Checklist + SDK Status)
  - Build Health: technical validation checks (removed "Required SDKs" - now in SDK Overview)
- Version mismatch warnings only trigger for outdated versions (newer is OK)

### Fixed
- Fixed runtime crash when switching from Prototype to Full mode due to orphaned Facebook SDK entries in AndroidManifest.xml

## [2.1.0] - 2025-12-01

### Added
- **Firebase Suite**: Full Firebase integration with Analytics, Crashlytics, and Remote Config
- `FirebaseAdapter` with async-safe initialization and event queuing
- `FirebaseCrashlyticsAdapter` with automatic exception capture and custom logging
- `FirebaseRemoteConfigAdapter` with typed getters and fetch/activate support
- `FirebaseCoreManager` for centralized Firebase initialization (prevents race conditions)
- Firebase section in Configuration window with install button, module toggles, and config file checklist
- Firebase in Setup Checklist (shown when installed, as optional item)
- Detection for `google-services.json` and `GoogleService-Info.plist` config files
- `enableFirebaseAnalytics`, `enableCrashlytics`, `enableRemoteConfig` toggles in SorollaConfig
- **[Firebase Setup Guide](Documentation~/FirebaseSetup.md)**: Documentation for Firebase Console setup and usage
- New public APIs:
  - **Crashlytics**:
    - `Sorolla.LogException(Exception)` - Log non-fatal exceptions
    - `Sorolla.LogCrashlytics(string)` - Add breadcrumb logs
    - `Sorolla.SetCrashlyticsKey(string, value)` - Set custom keys
  - **Unified Remote Config** (Firebase → GameAnalytics → default fallback):
    - `Sorolla.IsRemoteConfigReady()` - Check if Remote Config is available
    - `Sorolla.FetchRemoteConfig(Action<bool>)` - Fetch remote values
    - `Sorolla.GetRemoteConfig(key, default)` - Get string value
    - `Sorolla.GetRemoteConfigInt(key, default)` - Get int value
    - `Sorolla.GetRemoteConfigFloat(key, default)` - Get float value
    - `Sorolla.GetRemoteConfigBool(key, default)` - Get bool value

### Changed
- All analytics events (`TrackProgression`, `TrackDesign`, `TrackResource`) now dispatch to Firebase when enabled
- Firebase SDK installed via Git UPM from `github.com/LaCreArthur/unity-firebase-app`
- Single "Install Firebase" button installs all 4 packages (App, Analytics, Crashlytics, Remote Config)
- **Unified Remote Config API**: Single set of methods that checks Firebase first, then falls back to GameAnalytics

### SDK Versions
- Firebase App: 12.10.1 (Git UPM)
- Firebase Analytics: 12.10.1 (Git UPM)
- Firebase Crashlytics: 12.10.1 (Git UPM)
- Firebase Remote Config: 12.10.1 (Git UPM)
- GameAnalytics: 7.10.6
- External Dependency Manager: 1.2.186
- AppLovin MAX: 8.5.0
- Facebook SDK: 18.0.1 (Git URL)

## [2.0.1] - 2025-11-26

### Changed
- **Refactored**: SDK installation now uses manifest.json directly instead of `Client.Add()` queue
- **Refactored**: Core dependencies (GA, EDM, iOS Support) installed via OpenUPM scoped registry
- SDK versions now centralized in `SdkRegistry` for easy updates

### Fixed
- Fixed GameAnalytics installation failing due to EDM dependency order
- Fixed potential infinite loading when Package Manager was busy during installation

### SDK Versions
- GameAnalytics: 7.10.6
- External Dependency Manager: 1.2.186
- AppLovin MAX: 8.5.0
- Facebook SDK: 18.0.1 (Git URL)

## [2.0.0] - 2025-11-25

### Changed
- **Renamed**: Package from `com.sorolla.palette` to `com.sorolla.sdk`
- **Renamed**: Namespace from `SorollaPalette` to `Sorolla`
- **Renamed**: Main API class from `SorollaPalette` to `Sorolla`
- **Renamed**: Config asset from `SorollaPaletteConfig` to `SorollaConfig`
- **Refactored**: Mode system now uses `enum SorollaMode { None, Prototype, Full }`
- **Moved**: Adapters from `Modules/` to `Runtime/Adapters/`
- **Moved**: SDK utilities to `Editor/Sdk/` subfolder

### Added
- Setup Checklist in Configuration window with SDK status detection
- "Open Settings" buttons for quick access to GA/FB/MAX configuration
- Links section with Documentation, GitHub, and Issue tracker
- `SdkConfigDetector` for detecting SDK configuration status
- `SdkRegistry` as single source of truth for SDK metadata

### Improved
- Configuration window UX with cleaner layout
- SDK detection with better error handling
- Mode switching with confirmation dialog

## [1.0.0] - 2025-11-10

### Added
- Initial release
- Prototype Mode support (GA + Facebook + optional MAX)
- Full Mode support (GA + MAX + Adjust)
- Configuration window with DRY refactoring
- Mode selection wizard with auto-MAX and auto-Adjust installation for Full Mode
- SDK adapters for Facebook, MAX, and Adjust
- Auto-installation of GameAnalytics SDK
- On-demand AppLovin MAX SDK installation
- On-demand Adjust SDK installation via UPM
- DRY refactored codebase with reusable helpers
- Generic SDK detection pattern
- Reusable manifest modification helpers
- Modular config section rendering
