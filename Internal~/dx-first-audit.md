# DX-First Audit of `Palette` Public API

Audit ran 2026-04-17 after shipping the `3.9.2` `TrackPurchase` hotfix. Purpose: apply the newly-encoded [DX-First API Design principle](../Documentation~/architecture.md#dx-first-api-design) to the rest of the `Palette` surface, find the same class of landmine (primitive-accepting silent-corruption APIs), rank by severity, plan fixes for `3.10.0`.

**Context**: Sorolla is the publisher and tracker. Studios focus on making games - they don't do custom analytics, they don't know MMP verification formats, they don't speak ISO 4217. Every primitive parameter (`double`, `string`, `bool`) on a public API is a future silent-data-corruption bug.

---

## Methodology

For each public `Palette.*` method, answered three questions:
1. What's the minimum the studio must pass? Can we derive the rest?
2. If they pass wrong values, does it fail loud or silent? (Silent = fix it.)
3. Can we offer a one-line "register once and forget" automation?

Ranked findings by severity of silent-correctness risk.

---

## P0 - Same class as `TrackPurchase`, fix next

### 1. `TrackEvent(string eventName, Dictionary<string, object> parameters)` @ `Runtime/Palette.cs:198`

**Failure mode**: accepts any event name, any params. Zero type safety. Directly contradicts the "studios don't do custom analytics, we do it for them" principle. Studio can typo event names, pass wrong param keys, send garbage - everything fires, GA/Firebase/BigQuery accept it, data pipeline corrupts silently.

**Fix**:
- Mark `[Obsolete]` on the public signature.
- Introduce curated event catalog: `Palette.Events.OnLevelStart(int level)`, `OnTutorialStep(TutorialStep step)`, `OnEconomySink(EconomySinkId id, int amount)`, etc.
- Sorolla owns the schema - one place to add events, typed everywhere.
- Keep `TrackEvent(string, Dict)` internal for Sorolla's own adapter use.

### 2. Remote Config `Get*(string key, T defaultValue)` x4 @ `Runtime/Palette.cs:660-712`

**Failure mode**: stringly-typed keys, stringly-typed defaults. Studio typos a key (`"maxLevels"` vs `"max_levels"`) - silently gets the default value forever. A/B test runs, variant "works" because everyone's on default, no alert fires.

**Fix**:
- Code-generate typed accessors from the Remote Config schema: `Palette.Config.MaxLevels` returns `int` with the right default baked in at generation time.
- Schema lives in `Internal~/remote-config-schema.json` (or similar) - single source of truth.
- `Get*(string key, ...)` signatures go `internal`, stay available for Sorolla tooling.

---

## P1 - Silent-correctness risk, fix soon

### 3. `TrackProgression(ProgressionStatus, string p01, string p02, string p03, int score, Dictionary)` @ `Runtime/Palette.cs:234`

**Failure mode**: three string slots. Typo-prone. No auto-hook despite Unity having `SceneManager.sceneLoaded`, `Time.timeSinceLevelLoad`, etc.

**Fix**:
- `Palette.Level.Start(LevelId)` / `.Complete(LevelId, int score)` / `.Fail(LevelId)` - enum-based `LevelId` or strongly-typed struct.
- Drop-in `PaletteLevelTracker : MonoBehaviour` component studios attach to their level GameObject.
  - Auto-fires Start on Awake.
  - Complete/Fail via `UnityEvent` hooks.
  - Auto-tracks `duration_sec`.
- Studio integration: drag component onto level prefab. One line of code = zero lines of code.

### 4. `ShowRewardedAd(Action, Action)` / `ShowInterstitialAd(Action)` @ `Runtime/Palette.cs:1000-1018`

**Failure mode**: no `placement` parameter. MAX and Adjust both benefit from placement labels for ad-revenue segmentation. Studios omit - can't segment monetization by context (level_complete_gift vs level_fail_continue vs shop_entry). Critical revenue analytics blind spot.

**Fix**:
- Require `AdPlacement` enum (Sorolla-curated): `Palette.ShowRewardedAd(AdPlacement.LevelFailContinue, onComplete, onFailed)`.
- Placement flows to MAX `ShowRewardedAd(placement)` + Adjust event params automatically.
- Old signature marked `[Obsolete]`.

### 5. `TrackResource(ResourceFlowType, string currency, float amount, string itemType, string itemId, Dictionary)` @ `Runtime/Palette.cs:287`

**Failure mode**: four stringly-typed slots. Exact same risk profile as `TrackPurchase` pre-hotfix. A typo in `currency` (`"Coin"` vs `"coins"`) silently fragments economy data in GA - reports show two currencies where there should be one, analysts chase phantom.

**Fix**:
- Enum-based API: `Palette.Economy.Earn(CurrencyId, int amount, ItemId)` / `.Spend(...)`.
- `CurrencyId` and `ItemId` enums defined by Sorolla, generated from schema if studios need per-game additions.

---

## P2 - Lossy or untyped, fix opportunistically

### 6. `Initialize(bool consent)` @ `Runtime/Palette.cs:549`

**Failure mode**: doc says "Do NOT call directly" yet it's `public`. Takes `bool` for consent when the real state has 5 values (`Unknown`, `NotApplicable`, `Required`, `Obtained`, `Denied`). Bool collapses `Unknown` and `Denied` together - loses information the consent system actually has.

**Fix**:
- Make it `internal` (only `SorollaBootstrapper` calls it anyway).
- Signature takes `ConsentStatus`. Bootstrap resolves consent from CMP before calling.

### 7. `SetUserProperty(string name, string value)` @ `Runtime/Palette.cs:426`

**Failure mode**: untyped property name + value. Sorolla owns the segmentation schema - magic strings leak into studio code.

**Fix**:
- Curated per-property setters or enum-keyed: `Palette.SetUserProperty(UserProperty.Country, "FR")`.
- Most user properties should auto-populate (country, app version, install date) - studios shouldn't touch them at all.

### 8. `SetCrashlyticsKey(string key, T value)` x4 @ `Runtime/Palette.cs:806-838`

**Failure mode**: stringly-typed key. Studios put different typos across different screens - segmented crash reports that should aggregate.

**Fix**:
- Curated `CrashlyticsKey` enum or per-key setters: `SetCrashlyticsUserSegment(...)`, `SetCrashlyticsLastScene(string)`.

---

## Not a landmine (verified clean)

- `SetUserId(string)` - IDs are inherently strings, fine.
- `GetAttribution/GetAdjustId/GetAdvertisingId(Action<T>)` - callback-based, clean.
- `LogException(Exception)` - rich type already, fine.
- `FetchRemoteConfig(Action<bool>)` - no surface area for silent bugs.
- Consent readers (`ConsentStatus`, `CanRequestAds`, `PrivacyOptionsRequired`) - already use `ConsentStatus` enum.
- `ShowDebugger/HideDebugger/ToggleDebugger` - void, no params, trivially correct.

---

## Recommendation for `3.10.0`

Close P0 first - `TrackEvent` is the biggest unfixed landmine (it's the pattern that created the `TrackPurchase` bug in the first place: accept anything, validate nothing). Next `TrackProgression` because the `PaletteLevelTracker` MonoBehaviour is the purest expression of the principle - zero code change, one component drag.

**Scope for `3.10.0`** (minor bump - API additions, not breaking):
- Curated `Palette.Events.*` catalog, mark `TrackEvent(string, Dict)` as `[Obsolete]`.
- `Palette.Level.*` API + `PaletteLevelTracker` component.
- `AdPlacement` enum for ad methods.

**Deferred to `3.11.0` / later**:
- Remote Config code-gen (needs schema input, bigger design discussion).
- `Palette.Economy` + `CurrencyId`/`ItemId` enums (needs Sorolla economy taxonomy).
- `Initialize(bool)` -> `internal` + `ConsentStatus` (backwards-compatibility cleanup).
- `SetUserProperty` / `SetCrashlyticsKey` typed setters.

**Blocker before any P0/P1 code**: need Sorolla's event taxonomy.

What events does the publisher want tracked across all games? Sources:
- Current studio integrations (grep their codebases for `Palette.TrackEvent` calls).
- MMP dashboards (what's already being sent to Adjust / Firebase / GA).
- Product/analytics team input.

That list becomes the first `Palette.Events.*` catalog. Until it exists, the refactor has no anchor.

---

## Progress tracking

| Item | Status |
|---|---|
| `3.9.2` `TrackPurchase` hotfix (Product overload, AutoTracker, ReceiptParser, validation) | SHIPPED `464502a`, tag `v3.9.2` |
| DX-First principle in `architecture.md` | SHIPPED (commit with this audit) |
| DX-First principle in local `CLAUDE.md` | WRITTEN (gitignored, local-only) |
| P0.1 `TrackEvent` -> `Palette.Events.*` catalog | BLOCKED on event taxonomy from Sorolla |
| P0.2 Remote Config code-gen | DEFERRED to `3.11.0` |
| P1.3 `Palette.Level.*` + `PaletteLevelTracker` | READY - can ship without taxonomy blocker |
| P1.4 `AdPlacement` enum | READY - can curate placements from Sorolla product team |
| P1.5 `Palette.Economy` | BLOCKED on economy taxonomy |
