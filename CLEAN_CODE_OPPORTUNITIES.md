# Clean Code Opportunities

Generated: 2026-04-30  
Scope: `Packages/com.sorolla.sdk`  
Mode: review only; no behavior changes applied.

## 1. Findings

### F1. `Palette.cs` is carrying too many responsibilities

- Location: `Runtime/Palette.cs`
- Evidence: ~1,240 lines covering public facade, purchases, identity, attribution, initialization, queueing, remote config, Crashlytics, MAX consent/ad APIs, and event validation.
- Principle violated: focused modules; isolate side effects; keep functions/classes focused.
- Risk level: Caution.
- Suggested fix: continue the existing partial-class split. Move cohesive internal regions into files such as:
  - `Palette.PurchasingInternal.cs` for `TrackPurchase(...)`, TxID dedup, ISO validation, purchase data quality telemetry.
  - `Palette.Initialization.cs` for `Initialize`, `QueueOrExecute`, `FlushPending`, `InitializeAdjust`.
  - `Palette.Max.cs` for MAX init/consent/ad methods.
  - `Palette.EventValidation.cs` or reuse `Runtime/Adapters/EventNameSanitizer.cs` for event validation.
- Notes: do not change public APIs or callback timing. This is a file-organization and small extraction refactor only.

### F2. Purchase validation and data-quality telemetry are duplicated

- Location: `Runtime/Palette.cs` around the `PendingOrder`, legacy `Product`, and low-level purchase fan-out overloads.
- Evidence: invalid metadata handling repeats similar checks and `sorolla_purchase_data_quality_failure` payload construction.
- Principle violated: meaningful DRY; obvious code.
- Risk level: Safe to Caution, because this touches purchase analytics.
- Suggested fix: introduce a tiny private helper for validated purchase metadata and a single helper for purchase data-quality telemetry payloads. Keep all existing log text, event names, event parameter keys, and drop behavior unchanged during first pass.
- Verification: add focused EditMode tests for any pure helper extracted from receipt/metadata validation where Unity IAP types allow it. Otherwise characterize via compile + manual Debug UI purchase path.

### F3. Event name sanitization logic exists in two places

- Location:
  - `Runtime/Palette.cs` event validation helpers.
  - `Runtime/Adapters/EventNameSanitizer.cs`.
  - `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs` also has Firebase-specific reserved event/prefix validation.
- Principle violated: DRY; single source of truth.
- Risk level: Caution.
- Suggested fix: first add characterization tests for current event-name and parameter-name behavior. Then route `Palette` validation through the existing sanitizer or move shared rules into one internal runtime helper. Keep Firebase-specific GA4 reserved-name enforcement separate if semantics differ.
- Verification: EditMode tests for accepted, sanitized, truncated, rejected, reserved-prefix, and unsupported-parameter cases.

### F4. MAX rewarded and interstitial flows duplicate state-machine logic

- Location: `Runtime/Adapters/MAX/MaxAdapterImpl.cs`.
- Evidence: rewarded and interstitial sections each track ready state, user-waiting state, retry attempts, retry generation, overlay state, display failure, hidden callbacks, and retry scheduling.
- Principle violated: meaningful DRY; reduce branching; isolate side effects.
- Risk level: Caution.
- Suggested fix: do not jump to a generic ad framework. Start with tiny local helpers:
  - `SetUserWaiting(adType, false)` / `NotifyLoading(adType, bool)` equivalent.
  - a shared retry-delay calculation helper.
  - a callback-clear helper for each ad type.
  If that remains clean, consider a private `AdLoadState` struct for `ready`, `retryAttempt`, `retryGeneration`, and `userWaiting`.
- Verification: manual MAX rewarded/interstitial smoke test on device plus Debug UI ad buttons. Unit-test only pure retry-delay calculation if extracted.

### F5. MAX ad show telemetry bypasses the top-level `Palette` validation path

- Location: `Runtime/Adapters/MAX/MaxAdapterImpl.cs`, `TrackAdShowRequested` and `TrackAdShowFailed`.
- Evidence: implementation calls `FirebaseAdapter.TrackEvent(...)` directly, while public custom events go through `Palette.TrackEvent(...)` validation and pending queue.
- Principle violated: consistency; isolate cross-provider fan-out policy.
- Risk level: Caution.
- Suggested fix: decide whether internal SDK telemetry should intentionally bypass `Palette` validation. If not, add an internal analytics helper that validates once and fans out consistently. If yes, document that internal telemetry is already curated and adapter-local.
- Verification: Firebase event smoke test for `ad_show_requested`, `ad_show_failed`, and existing ad impression events.

### F6. `BuildValidator.cs` mixes orchestration, checks, auto-fixes, display, domain reload behavior, and pre-build behavior

- Location: `Editor/BuildValidator.cs`.
- Evidence: ~1,280 lines. `BuildValidator` owns manifest reading, validation orchestration, Gradle template mutation, result formatting, UI dialog display, domain reload notifier, and `IPreprocessBuildWithReport`.
- Principle violated: mixed responsibilities; testability blocked by direct filesystem/Unity static calls.
- Risk level: Caution.
- Suggested fix: split by responsibility in small steps:
  - `BuildValidationResult.cs` for result data.
  - `BuildValidationRunner.cs` for orchestration.
  - `GradleBuildChecks.cs` for Java/R8/Kotlin checks and auto-fixes.
  - keep `BuildValidator` as a compatibility facade so callers do not change.
- Verification: existing EditMode tests in `Tests/Editor/BuildValidatorTests.cs`; add tests for extracted pure Gradle checks before moving behavior.

### F7. Validation result construction is repetitive and string-heavy

- Location: `Editor/BuildValidator.cs`, most `Check*` methods.
- Evidence: many `new ValidationResult(...)` blocks manually repeat status, category, fix strings, and first-line messages.
- Principle violated: KISS/readability; reduce boilerplate that hides intent.
- Risk level: Safe.
- Suggested fix: add private factory helpers such as `Valid(category, message)`, `Warning(category, message, fix)`, and `Error(category, message, fix)`. This is a low-risk first step before any larger BuildValidator split.
- Verification: run existing BuildValidator EditMode tests and manually compare Build Health UI/console output for one healthy and one failing config.

### F8. Auto-fix side effects run from domain reload paths

- Location: `Editor/BuildValidator.cs`, `BuildHealthConsoleNotifier.CheckHealthOnReload`.
- Evidence: domain reload invokes `RunAutoFixes()`, which can write Android manifest/Gradle/MAX settings and refresh the AssetDatabase.
- Principle violated: isolate side effects; least surprise.
- Risk level: Danger if behavior changes; Safe only to document or extract.
- Suggested fix: do not change semantics without explicit approval. A safe planning step is to extract `RunAutoFixes` into individually named fix operations and make the reload path's side effects more visible in code. A later behavior decision could limit reload to read-only checks, but that would be a product/workflow change.
- Verification: Unity editor smoke test after domain reload, Palette window refresh, and Android pre-build validation.

### F9. `SorollaWindow.cs` repeats SDK overview row rendering logic

- Location: `Editor/SorollaWindow.cs`.
- Evidence: `DrawSdkRow`, `DrawMaxOverviewItem`, and `DrawFirebaseOverviewItem` repeat status icon, installing state, config style/text, and action-button layout.
- Principle violated: meaningful DRY; obvious UI code.
- Risk level: Safe to Caution.
- Suggested fix: extend `SdkRowData` enough to render MAX and Firebase through `DrawSdkRow`, or extract small helpers for status icon/config text/action area. Avoid a large UI framework or complex row model.
- Verification: open `Palette > Configuration` in Prototype and Full mode; verify package install/config states render as before.

### F10. Firebase Remote Config getters repeat the same fallback pattern

- Location: `Runtime/Adapters/Firebase/FirebaseRemoteConfigAdapterImpl.cs`.
- Evidence: `GetString`, `GetBool`, `GetLong`, and `GetDouble` repeat `_init` guards, `GetValue`, static-value fallback, exception logging, and default return.
- Principle violated: DRY; small focused functions.
- Risk level: Safe to Caution.
- Suggested fix: add a private `TryGetValue(key, out ConfigValue)` helper and optionally a `ReadOrDefault<T>` helper only if it stays obvious. Keep typed conversion behavior unchanged.
- Verification: Remote Config manual smoke test plus EditMode tests only if Firebase types are available in the test assembly.

### F11. Firebase adapter event parameter building has repeated reserved-key merge setup

- Location: `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`.
- Evidence: progression and resource tracking both create base `List<Parameter>`, build a local `HashSet<string>` of reserved keys, and call `MergeExtraParams`.
- Principle violated: minor DRY.
- Risk level: Safe.
- Suggested fix: promote stable reserved-key arrays/static readonly sets for progression and resource events, and keep `MergeExtraParams` as the single merge path.
- Verification: Firebase analytics smoke test for level and economy events.

### F12. Adapter stubs repeat the same nullable implementation forwarding pattern

- Location: `Runtime/Adapters/*Adapter.cs`.
- Evidence: `AdjustAdapter`, `FirebaseAdapter`, `FirebaseCrashlyticsAdapter`, `FirebaseRemoteConfigAdapter`, and `MaxAdapter` repeat `s_impl` registration, "Not installed" warnings, no-op forwarding, and fallback callbacks.
- Principle violated: repetition, but current explicitness is valuable for Unity asmdef clarity.
- Risk level: Caution.
- Suggested fix: avoid a generic base class for now; Unity static adapter explicitness is easier to debug. A safe improvement is a tiny local convention pass: consistent `Tag`, consistent callback fallback behavior, and consistent logging language.
- Verification: compile all modes: no optional SDKs, Prototype optional Firebase/MAX, and Full.

### F13. Android manifest sanitizer is large but has good pure seams

- Location: `Editor/AndroidManifestSanitizer.cs`.
- Evidence: ~826 lines, but many methods already have XML-string variants covered by tests.
- Principle violated: large class, but not urgently problematic.
- Risk level: Caution.
- Suggested fix: keep behavior stable and extract only along existing seams:
  - activity detection/fix helpers,
  - orphan cleanup,
  - launcher manifest handling,
  - diagnostics container.
  Prefer increasing XML-based characterization tests before moving logic.
- Verification: existing `Tests/Editor/AndroidManifestSanitizerTests.cs`; add tests for every extracted XML helper.

### F14. Public API cleanup opportunities remain product-blocked

- Location:
  - `Runtime/Palette.cs` `TrackEvent(...)`.
  - `Runtime/Palette.cs` `ShowRewardedAd(...)` / `ShowInterstitialAd(...)`.
  - `Runtime/Palette.cs` `SetUserProperty(...)` and `SetCrashlyticsKey(...)`.
- Principle violated: DX-first API design; avoid stringly typed surfaces.
- Risk level: Danger because this touches public API.
- Suggested fix: do not refactor opportunistically. Use the internal DX audit as the source of truth. These require taxonomy/schema/product decisions first.
- Verification: API reference regeneration, migration docs, sample updates, and real game integration compile checks.

## 2. Plan

### Phase 0. Establish verification

1. Confirm available Unity editor path and exact test command.
2. Run existing EditMode tests before refactoring editor code.
3. Add characterization tests for pure helpers before extracting logic from:
   - event sanitization,
   - Gradle/R8 text checks,
   - Android manifest XML operations,
   - purchase metadata helpers where practical.

Suggested smallest test command once Unity path is known:

```bash
/path/to/Unity -batchmode -quit \
  -projectPath /Users/arthur/unity-projects/sorolla-palette-testbed \
  -runTests -testPlatform EditMode \
  -testResults /tmp/sorolla-editmode-results.xml
```

### Phase 1. Safe editor cleanup

1. Add `BuildValidator` result factory helpers.
2. Add or tighten tests for `RemoveBuildscriptBlock`.
3. Extract Gradle/R8 pure text checks from `BuildValidator` without changing public callers.
4. Verify EditMode tests and inspect Build Health UI manually.

### Phase 2. Safe runtime extraction

1. Add event sanitization characterization tests.
2. Move `Palette` event validation into one internal helper.
3. Split `Palette.cs` by existing regions into partial files without changing code bodies.
4. Verify compile and Debug UI custom event tests.

### Phase 3. Adapter cleanup

1. Extract tiny MAX retry-delay helper and callback clearing helpers.
2. If still useful, introduce a private `AdLoadState` struct.
3. Consolidate Firebase Remote Config getter fallback handling.
4. Verify on-device MAX rewarded/interstitial, Firebase Remote Config, and analytics events.

### Phase 4. Larger design decisions

1. Decide whether domain-reload auto-fixes should remain write-capable.
2. Decide public `AdPlacement` taxonomy.
3. Decide `Palette.Events.*`, user property, Crashlytics key, and Remote Config schemas.
4. Treat these as planned feature/API work, not opportunistic cleanup.

## 3. Execution

No refactor execution was performed in this pass. The only change is this planning document.

Recommended first executable batch:

1. Add `BuildValidator` result factory helpers.
2. Keep all method names and callers stable.
3. Run EditMode tests.
4. Review diff for pure boilerplate reduction.

This is the lowest-risk cleanup because it does not touch runtime analytics, purchase, consent, ads, or public APIs.

## 4. Validation

Commands run during this review:

```bash
find Runtime Editor Samples~ Tests -name '*.cs' -type f -print0 | xargs -0 wc -l | sort -n
rg -n "TODO|FIXME|HACK|NOTE|XXX|Obsolete|public static void|static void|void Initialize|Action<|QueueOrExecute|Debug\\.LogWarning|Debug\\.LogError" Runtime Editor Samples~ Tests
```

No Unity tests were run. This pass intentionally produced a planning file only.

Follow-up validation before any refactor:

- Run existing EditMode tests.
- Add characterization tests around the specific logic being moved.
- For runtime adapter changes, verify with Debug UI and at least one real device path for affected SDKs.

