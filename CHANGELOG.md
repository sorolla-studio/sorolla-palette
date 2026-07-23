# Changelog

All notable changes to this project will be documented in this file.

## [4.0.0] - Unreleased

Self-serve integration health. The Palette window now produces one Launch Readiness verdict for the
game in front of it, backed by a canonical gate catalog and a single evaluator, and a copyable
report that carries every row plus the SDK commit recorded when the connected binary was built.

This line is unreleased until the `v4.0.0` tag. The version string stays `4.0.0` across the whole
development line, so it cannot identify a development commit: use the SDK commit printed in the
report. Do not pin a game to the development checkout, and do not treat a `HEALTHY` badge as a
release verdict.

### Added

- **Launch Readiness report** (Palette window): one verdict - `HEALTHY`, `N ISSUES`, `INCOMPLETE`,
  `FAILING` - aggregated by a single evaluator over a 25-gate catalog, with fail/warn/pending/pass
  counts. It is fail-closed: a gate whose evidence is missing, pending, unverifiable, or supplied
  without the proof it requires reads `INCOMPLETE`, never green. A fresh project with nothing checked
  reads `INCOMPLETE`.
- **The report evaluates the active build target.** Checks about the platform the project is not
  building do not appear in the verdict, the counts, or the rows, and apply again when the build
  target switches. A game shipping one platform can reach a green verdict.
- **Copy Report**: the full report as text - every gate with its stable id, definition version,
  requirement and reason, disposition, outcome, required and observed proof, evidence, and fix -
  including the inert rows the window filters out of view. It carries a fingerprint: application id,
  app version, Unity build GUID, mode, platform, and the SDK commit from the matching post-build receipt.
- **Device snapshot from the editor**: Connect Device pulls the live `/qa/snapshot` from a
  USB-connected device, Android over `adb forward` and iOS over `iproxy` (libimobiledevice). The
  snapshot is bound to the editor's post-build receipt, so a wrong-game, wrong-build-GUID, or
  unsupported-schema snapshot is rejected instead of trusted. The editor consumes the authoritative
  runtime Vitals verdict from that snapshot.
- **Per-build coverage ledger**: verified facts (consent resolved, a level played to completion, an
  ad watched to the end) persist for the exact build across relaunches, keyed to app version, Unity
  build GUID and SDK version, so proving the integration is one pass through the game rather than a
  per-launch chore. Any rebuild starts from an empty ledger. A game with no ad units configured owes
  no ad evidence.
- **Firebase config active-app matching**: the config check parses `google-services.json` /
  `GoogleService-Info.plist` and confirms it carries the active application id (Android
  `package_name`, iOS `BUNDLE_ID`). A copied wrong-game config fails with both identifiers named
  instead of passing and breaking Firebase only at runtime. Several candidate files, an unreadable
  file, or one that will not parse read `INCOMPLETE`, never a pass. Honest limit: matching the bundle
  id catches a wrong-GAME file but does not prove Firebase PROJECT identity.
- **GameAnalytics per-platform keys** (closes #8): the check reads the active build target's game key
  and secret key pair instead of passing on any key existing for any platform.
- **GameAnalytics credential probe**: the key/secret pair is validated against the vendor endpoint,
  so syntactically populated but invalid credentials no longer read as configured.
- **Facebook platform and credential probe**: an async, non-blocking Graph API call verifies that the
  active platform is registered on the Facebook app and that the app id / client token pair is
  accepted. A missing platform and a rejected credential pair report distinctly.
- **AppLovin MAX ad unit check**: in Full mode, the rewarded and interstitial ad unit ids for the
  active build target must be set.
- **Build Health checks**: verbose logging left on, Development Build left on, Adjust sandbox mode
  left on, missing Android release keystore, a machine-local `org.gradle.java.home` committed into
  `gradleTemplate.properties`, an empty GameAnalytics `ResourceCurrencies` whitelist, missing
  Addressables content, and `com.sorolla.sdk` pinned to a branch instead of a published tag.
- **`unverifiable` validation state** for network-dependent checks (offline, endpoint unreachable).
  It never blocks a build and never renders as a pass.
- **Copy SDK state** (on-device debug console): copies the same data `/qa/snapshot` serves as plain
  text, so a studio can paste device state to Sorolla when an on-device problem cannot be self-fixed.
- **SDK-only Vitals verdict**: Unity, plugin, and studio-game exceptions never appear as Vitals rows,
  affect its verdict, or enter the QA snapshot. The hidden internal console can still retain them for
  investigation without presenting them as SDK health.
- **QA data files ship with the SDK** (`QA~/red-flags.txt`, `QA~/signal-markers.txt`,
  `QA~/known-non-blockers.txt`): the QA-pass grep patterns are version-pinned with the SDK.

### Changed

- **Definite active-platform data loss blocks the build**: missing/rejected GameAnalytics or Facebook
  credentials/platform registration, missing AppLovin MAX ad units in Full mode, the Adjust app
  token, and the active platform's Firebase config. Unreachable network probes remain incomplete
  rather than blocking or passing.
- **One Palette window, grouped by vendor.** Each vendor has one foldout holding its status, its
  check rows, and its own configuration fields; the separate SDK Keys section is gone. A check row
  reads as its name and a short status word, with the message and fix wrapping underneath. Clicking
  anywhere on a group header folds it.
- **Configuration edits re-run validation as you type**, with no Refresh press, and a check waiting on
  a network answer updates itself when the probe returns.
- **The window states which platform the report judged**, and the Adjust sandbox checkbox sits under
  the warning that explains it.
- **A user's privacy choice is no longer reported as a problem**: ATT denied or restricted and a
  zeroed advertising id are informational, not warnings.
- **Sorolla Vitals redraws while open**: a fact landing on device (an ad finishing, a level
  completing) updates the report within a second.
- **Vitals and `/qa/snapshot` report the platform they run on**: the iOS-only ATT row no longer
  renders off iOS, and the snapshot sends `att: null` there instead of a constant "authorized".
- **Facebook readiness failures name the cause**: when the Graph readiness probe fails, the adapter
  reports whether the running platform is registered on the Facebook app instead of the vendor's raw
  SSL/transport error.
- **On-device fix text points only at controls the studio can see**, and editor fix text no longer
  tells you to open the window you are already in.

### Fixed

- **`MiniJson` no longer hangs on truncated JSON**: parsing input that ended mid-object or mid-array
  looped forever at 100% CPU, so a truncated config file could wedge the editor.
- **`google-services.json` detection no longer matches the desktop file**: the fuzzy search that
  matched the auto-generated `google-services-desktop.json` reported a missing Android config as
  present.
- **A stray `GoogleService-Info.plist` no longer pins the report amber**: iOS config discovery
  searched the whole project, so any second copy made the shipping config permanently ambiguous. Both
  platforms now check the exact paths Unity ships from.
- **Quick Start code snippets render.** They displayed blank, though the copy button still worked.
- **Detail and fix text wraps** instead of truncating off the right edge.
- **The duplicate-purchase counter fires.** It watched for wording the drop site never logged, so a
  replayed purchase was never counted.
- **`/qa/exec` actions are attributable as they happen**: every accepted or refused call logs a
  receipt to the console stream rather than surfacing only in the next snapshot's counters.

### Removed

- **QA bridge password gate** (breaking): the `/qa/*` bridge no longer requires a password. The
  password was a constant compiled into every build, so it protected nothing; the real boundary is
  that the bridge binds loopback only and is reachable only by someone who can already USB-forward
  the port. Migration: remove the `X-Sorolla-QA-Password` header or `qa_password` parameter from your
  scripts (a stale one is ignored). Treat snapshot output as potentially readable.

### Documentation

- **TikTok is marked parked** across the public docs and in the `SorollaConfig` field comments and
  inspector headers. The adapter still ships and existing integrations keep working; parked is
  neither deprecated nor removed.
- **`api-reference.md` is regenerated and its freshness is enforced in CI.** The DocFX shadow project
  had failed to compile since the UI Toolkit overlay landed, extracting zero public API; CI now fails
  on a zero or partial extraction and on a stale committed file.

## [3.18.3] - 2026-07-08

Editor UI overhaul on top of 3.18.2. Editor-side: the Palette window (SorollaWindow) is fully rebuilt on UI Toolkit with a shared design-token system; runtime-side changes are confined to the debug console overlay's draw code/theme (no init, consent, analytics, ads, or QA-bridge logic touched). Design pass reviewed and accepted screen-by-screen by Arthur on 2026-07-08.

### Added
- **Design-token system** (`Editor/UI/tokens.uss` + `TOKENS.md`): status colors, radii, spacing, type scale as USS custom properties; background/text tokens derive from the live editor skin (dark verified; light implemented, pending human verification) instead of hardcoded values.
- **Reusable editor UI components** (`Editor/UI/`, namespace `Sorolla.Palette.Editor.UI`): StatusBadge, CalloutCard, SectionHeader, CheckRow/CollapsibleCheckGroup, ValidatedField (live per-keystroke validation, neutral-when-empty semantics), CodeSnippetBlock (copy-to-clipboard), HeroHeader (🎨 icon + Prototype|Full segmented mode switch).
- **PaletteStyleGalleryWindow** (`Palette/Style Gallery`): renders every component/state on one page, including a debug forced-light-mode toggle for skin testing without re-skinning the editor.

### Changed
- **SorollaWindow rebuilt on UI Toolkit**: hero header with segmented Prototype|Full mode switch (replaces the old Mode box; same confirmation + package flow), collapsible Build Health checks (collapsed by default), SDK overview vendor rows, ValidatedFields for MAX ad-unit IDs (16-hex format check, empty stays neutral) and Adjust tokens (contextual "required for Full-mode builds" hint), real compilable Quick Start snippets (source-verified API calls) replacing prose pseudo-code, unified small-button style, footer links. Opens as a utility window (no dock-tab strip), single-instance.
- **Runtime console styling only**: rounded row cards and rectangular severity badges via `SorollaConsoleTheme` (radius 4), honest VERIFIED column (neutral "—" without positive evidence; narrated Gated/Unverifiable states in amber), "N adapters can't be verified" explainer.

### Fixed
- **Quick Start snippets rendered blank after a domain reload**: `Font.CreateDynamicFontFromOSFont` returns null when called during `CreateGUI()` construction; the monospace font is now assigned via the scheduler with a null-guard fallback.

## [3.18.2] - 2026-07-06

Editor-tooling fixes + a runtime refactor batch (behavior-preserving intended, compile-verified only) on top of 3.18.1, plus two init/consent-path robustness fixes (DR-129, DR-133-residual), per-key Remote Config visibility in the QA bridge, and public purchasing-API docs. One telemetry payload changed and one public type was removed (see below). Verified on-device (romba iOS, Full mode, release build) before tagging.

### Added
- **QA bridge: per-key Remote Config values + sources** (`remote_config.values` in `/qa/snapshot`): every key Firebase knows plus every registered in-app default, each with the value the Palette getters would serve and its true source (`firebase_remote` fetched/cached, `firebase_default`, `gameanalytics`, `in_app_default`, `missing`). Reads live adapter state rather than logs, so a release (non-development) build can be QA'd for Remote Config delivery - "did the console change reach the device" is now answerable from one snapshot.

### Fixed
- **Editor version sync no longer downgrades a manual AppLovin MAX upgrade** (B-4): `SdkVersionSync` forced every installed package to the registry version on each domain reload, reverting a MAX version the developer had bumped via `MaxVersionChecker`. It now only raises semver-pinned packages up to the registry floor and never downgrades (reuses `MaxVersionChecker.IsNewerVersion`); git-URL-pinned packages stay exact-match enforced.
- **Build no longer commits a machine-local JDK path** (B-16, Unity 2022 only): the auto-fixer appended `org.gradle.java.home=<absolute local path>` into the version-controlled `gradleTemplate.properties`, breaking every other machine. The JDK home is now injected at build time into the generated `gradle.properties` (via `GradlePropertiesFixer`, mirroring the existing dexing fix), leaving the committed template portable. The build validator no longer errors when the committed template lacks the line.
- **Auto-fixers no longer write `.backup` files into the tracked `Assets/` tree** (B-17): the manifest and gradle sanitizers copied `<file>.backup` next to version-controlled files on every reload, polluting each game repo's git tree. Dropped; the fixes stay logged and are revertable via git.
- **MAX ad-revenue relay guarded against double subscription**: prevents duplicated ad-revenue fan-out in Editor sessions with domain reload disabled.
- **A mid-session ATT change now reaches every vendor** (DR-129): when the app returns to the foreground, Palette re-resolves consent and re-fans it, so a user who toggles tracking in iOS Settings while the app is backgrounded has the new status propagate to all vendors exactly once (change-gated on ATT, inert off-iOS and when ATT did not move). This is the only path that grants a Prototype build attribution when ATT is authorized after launch.
- **Progression `extraParams` are now snapshotted at enqueue** (DR-145-residual): `Level.Start/Complete/Fail` captured the caller's dictionary by reference in the pre-consent queue, so a caller that mutated or reused the same dict before the 1-3s flush dispatched the mutated values. It now defensive-copies on the queued path exactly like `TrackEvent` and `Economy.*` (B-13 posture). No change on the initialized path (dispatch is synchronous there).
- **A throwing or silently-failing vendor init can no longer strand SDK initialization** (DR-133-residual): each vendor boot init runs behind a catch-continue guard (`SafeInit`), so one vendor throwing no longer skips the others or the transition to `IsInitialized`; and if MAX never fires `OnSdkInitialized`, a 30s foreground watchdog completes init in a degraded no-ads state instead of wedging forever. `CompleteInitialization` is now idempotent, so the watchdog and the MAX callback racing can never double-flush the pending queue or fire `OnInitialized` twice.

### Changed
- **Runtime refactor (behavior-preserving intended)**: `ConsentCoordinator` extracted from the `Palette` facade; purchase tracking split into `PurchaseDedupLedger` / `PurchaseOrderAdapter` / `StoreEnvironmentResolver`; shared `PendingActionQueue` and `MaxAdRevenueRelay` extracted; init ready-path unified into one `CompleteInitialization` with a pre-flush hook; diagnostics monolith split (`SorollaDiagnostics` 1,985 -> 538 lines across 11 extractions: 7 partials + `SorollaRuntimeProblemClassifier` + console `Theme`/`TapUnlock`/`ScrollDrag`). `StoreEnvironmentResolver` is `internal` (editor tests reach it via `InternalsVisibleTo`); it was briefly public in intermediate commits, never part of the intended public surface.
- **Telemetry**: the `sorolla_purchase_data_quality_failure` low-level payload is unified with the pending-order builder: param `amount` renamed to `raw_price`, `platform` and `source` added. The low-level path has been internal-only since 3.14.1 and is nearly unreachable in production; the pending-order payload is byte-identical.
- **iOS Apple-payload log line re-tagged** from `[Palette]` to `[Palette:PurchaseOrderAdapter]` (log-grep scripts keying on the old tag need updating).

### Removed
- **`FakeCMPDialog`** (public MonoBehaviour, dead editor-only consent stub): deleted. A scene or prefab still carrying it gets a missing-script warning on upgrade.

### Documentation
- **Public purchasing API is now in the API reference**: `Palette.AttachPurchaseTracking(StoreController)` documented with a register-once usage example; the `TrackPurchase` doc cross-references were repaired (stale `TrackPurchase(double, string, ...)` signatures updated to the current `TrackPurchase(PendingOrder)` overload). DocFX references Unity's own compiled `Unity.Purchasing.dll` at generation time so the purchasing files compile without vendoring or stubbing Unity IAP, and the build stays green when the DLL is absent (standalone checkout / docs CI). `Level.*` / `Economy.*` `extraParams` are now documented as Firebase-only (GameAnalytics receives the curated fields, per DR-135).

## [3.18.1] - 2026-06-24

### Changed
- **Facebook diagnostics no longer pass on init alone**: after Facebook SDK init the adapter validates the app credentials through a managed Graph probe. Facebook reads "ready" in Sorolla Vitals and `/qa/snapshot` only once that probe succeeds; while it is pending it reads "validating", and a failing or never-returning probe (offline, or a VPN/ad-blocker/DNS blocking the Graph domain) reads "failed"/"waiting" instead of false-greening a build whose Facebook credentials are broken.
- **QA bridge request handling is capped**: `/qa/exec` rejects oversized request bodies (413) and bodies with an undeclared length (411), and the bridge drains a small fixed number of queued requests per frame.
- **Sorolla Vitals gesture hardened**: the on-device unlock remains five top-right taps, but the fifth tap must now be held for 0.8 seconds.

## [3.18.0] - 2026-06-23

Consent model unification + SDK remediation. The boot path and the CMP-resolution path now run through one resolver and one idempotent fan-out, so Prototype inherits the same consent-mode model (analytics / ad_storage / ad_personalization) the Full/MAX path already used, plus a batch of correctness fixes from the 2026-06 architecture audit.

### Fixed
- **iOS ATT denial no longer blacks out GameAnalytics** in Prototype / no-MAX builds: analytics consent is governed by GDPR decline, not ATT, so denying ATT (which has no bearing on first-party analytics) no longer disables GameAnalytics submission. GameAnalytics also survives across relaunch.
- **Ad signals no longer granted without a dialog** on the non-iOS no-MAX path: `ad_storage` / `ad_personalization` / `ad_user_data` are denied in Prototype (no ad-consent basis, no ads) instead of being granted from a boot `consent:true`.
- **Facebook attribution preserved in Prototype**: Facebook advertiser tracking follows ad-consent + iOS ATT but is independent of whether the build serves in-app ads, so Prototype builds that use Facebook solely for attribution keep attributing installs (matches pre-3.18.0 Facebook behavior on every init path).
- **Missing `SorollaConfig` no longer wedges a Full/MAX build** (B-1): init could hang forever (`IsInitialized` never set, queued events never flushed) when the config asset was missing on a MAX build. It now degrades to a ready no-ads state with a loud error; analytics, consent, and IAP still work.
- **Purchases can no longer be permanently deduped away on a dropped fan-out** (B-5): the dedup slot is committed only after the analytics fan-out actually dispatches, so a purchase whose fan-out is dropped or never runs re-fires on next launch instead of being lost. Strengthens the crash-replay guarantee.
- **Purchase dedup ledger no longer goes stale in-editor** (B-10) under Enter-Play-Mode-Options with domain reload disabled.
- **Custom-event params are snapshotted** (B-13) when an event is queued during the pre-consent window, so a caller reusing the dictionary cannot change the dispatched event.
- **Level start-time map no longer leaks** (B-18) when a level is started but never completed/failed.
- **High-denomination purchase amounts no longer overflow** the GameAnalytics business-event integer (B-19): clamped with a one-time warning; Firebase/Adjust receive the exact value.

### Changed
- **GameAnalytics now counts the pre-CMP window on the Full path**: during the ~1-3s before UMP resolves, analytics is ON by default (Google Consent Mode default-then-update, matching Firebase), then downgrades to OFF only on a confirmed GDPR decline. Ad signals, Adjust, ATT handling, consent events, and init ordering are unchanged.
- **MAX callbacks pinned to the Unity main thread** (B-2): `InvokeEventsOnUnityMainThread` is set true at MAX init so vendor callbacks cannot race the non-thread-safe pending queues.
- **`extraParams` on `Palette.Level.*` / `Palette.Economy.*` documented Firebase-only** (B-3): GameAnalytics receives the curated taxonomy fields, not the extras. No silent divergence.

### Removed
- **`Palette.HasConsent`** (public property): a legacy getter documented "use `ConsentStatus` instead". It only mirrored the internal ad-storage flag, which has no studio-facing meaning. Read `ConsentStatus` (GDPR/UMP decision) or `CanRequestAds` (ad gating) instead.
- **`Palette.Purchasing.AutoTracker`** and the `Palette.TrackPurchase(Product)` overload: Unity IAP v5 obsoleted `IDetailedStoreListener`, so `AutoTracker` did not function on `UnityIAPServices.StoreController`, and the `Product` overload existed only to back it. `Palette.AttachPurchaseTracking(store)` is the sole supported purchase-tracking path.

### Notes
- Invariants preserved: analytics broader than ad consent (off only on GDPR decline); `ad_personalization` / `ad_user_data` require ad-consent AND iOS ATT; Adjust gated on ad-consent NOT ATT; consent analytics events stay change-gated with DR-41 ordering (markers lead `FlushPending`); stub/impl `#if` guards intact so Prototype compiles without MAX/Adjust.

## [3.17.5] - 2026-06-17

Diagnostics parity patch: Sorolla Vitals and `/qa/snapshot` now read the same adapter outcome state instead of independently inferring readiness from log text.

### Added
- **Internal adapter diagnostics channel** (`Runtime/Adapters/AdapterDiagnostics.cs`): MAX, Adjust, Firebase Core/Analytics/Crashlytics/Remote Config, GameAnalytics, and Facebook report registered/initializing/ready/dispatch/warning/failure/unavailable outcomes into one runtime state surface.

### Changed
- **Vitals and `/qa/snapshot` adapter statuses now share one source of truth**: real adapter outcomes can override stale log-scraped state, so failures become visible in both QA surfaces consistently.
- **Adjust init is treated as verification-gated**: Adjust v5 has no init callback, so Vitals stays in a verifying state after init dispatch until an ADID or attribution callback proves native reachability.
- **MAX ad rows are current-readiness based**: one historical ad completion no longer greens the row forever; load/show warnings can recover after later load/ready callbacks.
- **Remote Config diagnostics recover after success**: fetch/defaults/activation warnings surface when they happen and clear when a later Remote Config operation succeeds.

### Fixed
- **GameAnalytics no longer passes from init request alone**: Vitals and `/qa/snapshot` require real GameAnalytics readiness or a dispatch outcome instead of treating the old initializing log as success.
- **Firebase unavailable/failure states no longer wait forever**: dependency, native-library, and init failures surface as failed/unavailable adapter statuses.
- **Runtime SDK version truth**: `Palette.SdkVersion`, `package.json`, and the public API reference now report `3.17.5`.

### Notes
- No analytics payload fields were added, renamed, dropped, or made conditional in this patch; adapter dispatch instrumentation is diagnostics-only.

## [3.17.4] - 2026-06-17

### Changed
- **QA bridge auto-starts in all builds** (`Runtime/Diagnostics/QaBridge/`): release builds now bind the loopback bridge on launch instead of staying dormant until a debug-console arm tap. The bridge still binds only `127.0.0.1` / `localhost` and is reached by local process or USB forward.
- **QA bridge password gate**: every `/qa/*` request now requires the shared QA bridge password via `X-Sorolla-QA-Password`, `Authorization: Bearer ...`, or `qa_password=...`. There is one auth rule across Editor, development, and release builds.
- **Diagnostics console bridge row simplified**: the old `Dormant` / `Arm` / `Disarm` release-state UI is replaced by an auto-running status with a `Restart` / `Retry` recovery button for port-bind failures.

### Fixed
- **Runtime SDK version truth**: `Palette.SdkVersion` and `package.json` now both report `3.17.4`, carrying forward the local `3.17.3` cleanup so Sorolla Vitals and `/qa/snapshot` identify the tested SDK line correctly.

## [3.17.3] - 2026-06-16

### Changed
- **Vendor dependency bumps** in `SdkRegistry.cs` (the SSOT the package resolver reconciles `manifest.json` against): GameAnalytics `7.10.6` -> `8.0.1`, AppLovin MAX `8.6.2` -> `8.6.4`. Both device-validated on Android (`com.sorolla.palette`): clean init, GameAnalytics ships events, AppLovin MAX reports `Max-Unity-8.6.4`, `[Palette] Ready!` reached. GameAnalytics 8.x's `configurations` -> `configurations_v3` Remote Config export change does not affect us (we use Firebase Remote Config, not GameAnalytics Remote Config). Firebase/Facebook/Adjust deps unchanged.

### Fixed
- **Runtime SDK version truth**: `Palette.SdkVersion` now matches `package.json` (`3.17.3`). This fixes Sorolla Vitals and `/qa/snapshot` reporting `3.17.2` on builds that include the GameAnalytics / AppLovin MAX dependency bumps.

## [3.17.2] - 2026-06-16

Remote Config freshness is now first-class in the QA surfaces: the bridge snapshot and the on-device console report RC status from the authoritative `Palette.RemoteConfigStatus`, not from log scraping.

### Added
- **`remote_config` block in `GET /qa/snapshot`**: `{ status, fetch_seen, fetch_success }`. `status` is `defaults` | `cached` | `live`, sourced from `Palette.RemoteConfigStatus` (verbose-independent, so it is correct on prod / non-verbose builds). Lets the qa-greenlight gate assert RC freshness on a release-candidate build instead of grepping suppressible logs. `fetch_seen` / `fetch_success` are secondary signals from the Firebase fetch-complete log and only reflect a Firebase fetch this session, so gate on `status`.

### Fixed
- **Console "Remote Config" row no longer stalls at "Waiting for fetch" on a cached relaunch**: it was driven by the `Fetch complete` log scrape, which never matches the disk-cache load path (`Cached config available`), so a fetch-less relaunch read "Waiting for fetch" forever while values were being served from cache. It now reads `Palette.RemoteConfigStatus` directly (`Defaults` -> info, `Cached`/`Live` -> pass); the scraped fetch line stays as secondary detail.

## [3.17.1] - 2026-06-12

QA agent bridge (Phase 1+2): a loopback HTTP bridge inside the diagnostics layer so QA tooling reads structured SDK state instead of grepping device logs. One diagnostics core, two frontends: anything the bridge exposes is also visible/tappable in the on-screen debug console.

### Added
- **QA bridge `GET /qa/snapshot`** (`Runtime/Diagnostics/QaBridge/`): structured JSON of SDK state (sdk/mode/build, consent + resolved consent-mode signals + IABTCF presence, adapter statuses, identity/attribution, ads, runtime problems) on `127.0.0.1:8765`. Built only from state the SDK already tracks (no ad-adapter changes). Reached over a USB forward (`adb forward tcp:8765 tcp:8765` / usbmux `iproxy 8765 8765`).
- **Access-gated bridge lifecycle**: compiled into ALL builds (no compile define). Auto-starts in the Editor and development builds; in release builds it stays dormant until a human arms it from the debug console ("QA Bridge" section), and a relaunch starts dormant again. Binds loopback only, never `0.0.0.0` (no iOS Local Network prompt).
- **QA bridge `POST /qa/exec`** (fire-and-ack): `{"action":"..."}` dispatches a registered action on the main thread and replies `{"ok":true}` immediately (`{"ok":false,"detail":"unknown_action"}` otherwise); the snapshot is the source of truth for the outcome. Ships `show_rewarded`, `show_interstitial`, `open_privacy_options`, `refresh_consent`, `track_test_event`, `level_start`, `level_complete`, `economy_earn`, `economy_spend`.
- **Shared action registry** (`QaActionRegistry`, one core / two frontends): the debug console buttons and the bridge dispatch through the exact same delegates. The delegate signature already accepts an args bag so game-registered actions slot in later without re-shaping the registry.
- **`reset_consent` action** drives AppLovin MAX `CmpService.ShowCmpForExistingUser`, which both re-shows the consent form and resets existing consent (verified against AppLovin docs). Re-testing a consent scenario no longer needs a reinstall (only iOS ATT still does). Same supported call as `open_privacy_options`; recorded with a distinct marker.
- **Resolved consent-mode signals + form-shown flag** recorded at the Palette consent layer for the snapshot (`analytics_storage`/`ad_storage`/`ad_personalization`/`ad_user_data`, and `form_shown_this_session` for relaunch-persistence assertions).
- **Per-name event aggregation** (`events[]`: name, count, last params) so one end-of-run snapshot is sufficient even after the 40-entry recency ring evicts boot events. IAP facts (`iap{}`: tracking attached, purchase/duplicate counts, verification, last issue); per-purchase product visibility comes through the `purchase` event aggregation.

### Changed
- **Test/QA events excluded from the Activity health rows** (DR-33/DR-60): events fired by the debug console or the bridge, and SDK-self events (`consent_resolved`, `consent_changed`, `att_decision`), no longer green the progression/economy/custom-event game-integration counters. They are still logged and aggregated for visibility.
- **Console/bridge test events tagged for Firebase** with a reserved `sorolla_qa_test` param, so test traffic against production vendor endpoints (QA happens on release-candidate builds) is filterable in BigQuery. GameAnalytics progression/economy/design APIs are schema-fixed and cannot carry it, so GameAnalytics test events stay untagged; broader vendor tagging (GameAnalytics custom dimension, Adjust) is a follow-up that needs vendor-doc verification and an Adapter Endpoint Review.

### Notes
- The discoverable 5-tap gesture to open the debug console is intentionally retained for now; replacing it with a hardened access path (the release-build half of DR-33) is deferred and tracked separately. The bridge's release-build dormancy (armed only from the debug UI) already gates the new surface.

## [3.17.0] - 2026-06-11

Remote Config redesign: the SDK now owns the fetch lifecycle (auto-fetch, retry, real-time), and the public API shrinks to declare/read/react. The operation-sequence APIs whose ordering and semantics every studio had to get right (and that produced the audit's RC trap cluster DR-45/55/97/113 plus real default-drift bugs in two games) are deleted.

### Added
- **`Palette.RemoteConfigStatus`** (`Defaults` -> `Cached` -> `Live`, monotonic per session): which generation of values the getters serve. `Cached` = last session's fetched values served from Firebase's disk cache; `Live` = fetched or real-time-updated this session. Closes DR-45's invisible three-phase drift: gate A/B bucketing or gameplay start on `>= Cached`.
- **`Palette.OnRemoteConfigChanged(IReadOnlyCollection<string> changedKeys)`**: fires on every served-value swap - first cached load, fetch activation, real-time update, GameAnalytics configs ready. Late subscribers fire immediately if values are already readable, so there is no subscribe-before-fetch ordering to get right. Keys are empty when the change is unspecified (re-read what you care about). Replaces both the `FetchRemoteConfig` callback and `OnRemoteConfigUpdated`.
- **`Palette.OnRemoteConfigUpdateAvailable(keys)`**: fires instead of activation when `AutoActivateRemoteConfigUpdates` is false; the game applies the update via `ActivateRemoteConfigAsync()` at a safe moment (then `OnRemoteConfigChanged` fires).
- **`Palette.WaitForRemoteConfig(timeoutSeconds = 5, minStatus = Cached)`**: awaitable gate for fetch-critical moments (loading screen, behind a network wall). Devices that have fetched before pass instantly via the disk cache.
- **Fetch retry** (`FirebaseRemoteConfigAdapterImpl`): the boot fetch was one-shot - a transient network blip at cold start (cellular waking, captive wifi) lost the whole session to defaults with no retry (observed as intermittent "RC not fetched" sessions on romba). Failed fetches now retry at 5s/30s/120s and on every app-foreground until one lands.
- **Dev-build key diagnostics**: once values are readable, reading a key that exists in no tier (remote, GA, registered defaults) warns once per key - typos and unpublished parameters were previously silent forever. Unparseable values (e.g. `"abc"` read as int) also warn.
- **Cached-freshness signal at boot**: values activated in a previous session are detected (`ConfigInfo.FetchTime`) and reported as `Cached` before the fetch completes.

### Changed
- **One resolution path for every getter** (`Palette.RemoteConfig.cs`): Firebase (remote/cached/in-app default) -> GameAnalytics -> `SetRemoteConfigDefaults` values -> call-site default, identical for string and typed reads. Previously the string getter fell back per-key to GA while typed getters never consulted GA once Firebase was ready (DR-97), so one logical key could be served by two backends in one session.
- **Uniform value parsing**: bools accept `true/false`, `1/0`, `yes/no`, `on/off` case-insensitively on every tier (GA-tier bools previously only parsed `true`/`false`, silently dropping dashboard-typical `1`/`0` - DR-113); numbers parse invariant-culture on every tier. A remote value explicitly set to `""` is now delivered (previously mapped to the call-site default).
- **`SetRemoteConfigDefaults` is vendor-neutral**: values are kept in SDK state (serving the GA-only path, previously a silent no-op without Firebase) and still registered with Firebase so dashboard `useInAppDefault` parameters resolve. Defaults set before init now merge instead of queue-replace.
- **GameAnalytics remote configs join the lifecycle**: GA configs becoming ready raises `OnRemoteConfigChanged` (and drives status when Firebase RC is absent or failed). Previously there was no readiness signal at all on the GA path - studios had to poll.

### Removed (no deprecation shims)
- **`Palette.FetchRemoteConfig(Action<bool>)`** - it stopped fetching after the first success and reported `true` without touching the network (DR-55), and its callback bool conflated fresh-fetch / already-fetched / GA-readable / failure. The SDK fetches and retries on its own; subscribe `OnRemoteConfigChanged` or await `WaitForRemoteConfig` instead.
- **`Palette.IsRemoteConfigReady()`** - "ready" meant "readable, maybe stale, maybe never fetched". Use `RemoteConfigStatus`.
- **`Palette.OnRemoteConfigUpdated`** - replaced by `OnRemoteConfigChanged` (fires on first load too) and `OnRemoteConfigUpdateAvailable` (manual-activation mode).
- **`Palette.GetRemoteConfigKeys()`** - Firebase-only enumeration with no consumer; misleading on the GA tier.
- Internal: `IFirebaseRemoteConfigAdapter` shrinks to lifecycle + `TryGetRaw`; typed parsing moved to the shared resolution layer; new `RemoteConfigState` (Runtime/Adapters) holds freshness, defaults, subscribers, and waiters.

### Migration
- `Palette.FetchRemoteConfig(cb)` -> delete the call; move the callback body to `Palette.OnRemoteConfigChanged += keys => ...` (it also fires on first load and immediately at subscribe time if values are readable). For a boot gate: `await Palette.WaitForRemoteConfig(5f)`.
- `Palette.IsRemoteConfigReady()` -> `Palette.RemoteConfigStatus >= RemoteConfigStatus.Cached` (or `Live` for this-session freshness).
- `Palette.OnRemoteConfigUpdated += h` -> `Palette.OnRemoteConfigChanged += h` (same signature). If you set `AutoActivateRemoteConfigUpdates = false`, subscribe `OnRemoteConfigUpdateAvailable` for the not-yet-activated signal.
- Getter signatures (`GetRemoteConfig/Int/Float/Bool`) and `SetRemoteConfigDefaults` are unchanged - read sites need no edits.
- Behavior seam: with Firebase RC installed, a key registered via `SetRemoteConfigDefaults` resolves at the Firebase tier (in-app default) and is never served from GameAnalytics. If you run GA A/B experiments alongside Firebase RC, do not register defaults for GA-experiment keys.
- Game-side wrappers that existed to sequence boot (subscribe `Palette.OnInitialized` -> fetch -> reload-on-callback) collapse to one `OnRemoteConfigChanged` subscription.

## [3.16.2] - 2026-06-11

Correctness batch: stop two silent revenue/analytics-quality leaks (negative ad-revenue sentinel, wiped Remote Config defaults), make event rejection and the ad-revenue health row consistent across vendors, and harden the pre-consent flush so one vendor throw can't strand the queue. No public `Palette` API changed.

### Fixed
- **Negative MAX ad-revenue (`Revenue = -1`) was forwarded as real revenue** (`Runtime/Adapters/MAX/MaxAdapterImpl.cs`). AppLovin MAX sets `AdInfo.Revenue = -1` when an impression has no valid revenue (error or test mode, per AppLovin docs). The adapter forwarded it verbatim to Adjust / Firebase (`ad_impression` `value`) / TikTok, pushing negative revenue into ROAS. `TrackAdRevenue` now drops the fan-out and logs a Warning with the raw value when `Revenue < 0`. (DR-06)
- **Studio Remote Config defaults set before SDK init were wiped** (`Runtime/Adapters/Firebase/FirebaseRemoteConfigAdapterImpl.cs`). `Palette.Initialize` calls `FirebaseRemoteConfigAdapter.Initialize(defaults: null)`, and the impl unconditionally assigned `_pendingDefaults = defaults`, so defaults a studio set from `Awake`/`OnEnable` via `SetRemoteConfigDefaults` were overwritten with null and a key absent remotely resolved to type-zero instead of the in-code default. `Initialize` now merges and only touches `_pendingDefaults` when the caller actually supplied defaults. (DR-93)
- **GA4 reserved event names were dropped Firebase-side only** (`Runtime/Palette.EventValidation.cs`, `Runtime/Adapters/EventNameSanitizer.cs`, `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`). The reserved-name rejection lived two layers below the shared `Palette` validation gate, so `Palette.TrackEvent("error")` was dropped by Firebase but still reached GameAnalytics, invisible in BigQuery. The reserved-name list moved to the shared `EventNameSanitizer.ReservedEventNames` and is now rejected at the shared gate for every vendor; the Firebase-side name check is removed (the prefix check stays as a defensive layer for internal callers that bypass the gate). (DR-14)
- **GameAnalytics design-event value depended on Dictionary iteration order** (`Runtime/Palette.EventValidation.cs`). `ExtractFirstNumericValue` returned the first numeric param it happened to iterate, so the same `TrackEvent` could send different GA values run to run. Replaced with `ExtractDesignEventValue`, which reads only the documented `value` key (0 if absent). (DR-15)
- **Pre-consent flush loops aborted on the first vendor throw** (`Runtime/Palette.cs`, `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`, `Runtime/Adapters/Firebase/FirebaseCrashlyticsAdapterImpl.cs`). A single exception while draining a queued-event/action queue stranded the rest and, for `Palette.FlushPending`, also skipped `OnInitialized` and the consent markers (it runs inside `OnMaxSdkInitialized`). All three loops now catch-and-continue per item with a Warning. (DR-38)
- **`consent_resolved` was emitted after the pre-consent events it should precede** (`Runtime/Palette.cs`). `OnMaxSdkInitialized` ran `FlushPending()` before `TrackEvent("consent_resolved")` (and `att_decision`), so every pre-consent event dispatched before the session's consent marker and BigQuery queries windowing on it missed the whole batch. The markers are now emitted before the flush. (DR-41)
- **Adjust attribution/ID getters dropped their callback before init** (`Runtime/Adapters/Adjust/AdjustAdapterImpl.cs`). `GetAttribution`/`GetAdid`/`GetGoogleAdId`/`GetIdfa` did `if (!_init) return;` without invoking the callback, breaking the documented `callback(null)` contract, hanging callers and producing false timeout Warnings on the Sorolla Vitals identity rows. They now invoke `callback(null)` on the early-out. (DR-42, also clears the DR-64 false Warnings)
- **Ad-revenue Vitals row was a verbose-only log sniff** (`Runtime/Adapters/MAX/MaxAdapterImpl.cs`, `Runtime/Diagnostics/SorollaDiagnostics.cs`). The "Ad revenue" health row was driven by a `"TrackAdRevenue:"` log match, and the only emitters were Verbose logs gated behind `Debug.isDebugBuild`, so release builds always read "No revenue callback observed". The row is now driven directly by `SorollaDiagnostics.RecordAdRevenue`, called inside `MaxAdapterImpl.TrackAdRevenue` independent of log verbosity and installed vendors; the log-sniff is removed. (DR-09)
- **ISO-4217 currency gate accepted lowercase but forwarded it un-normalized** (`Runtime/Purchasing/Palette.PurchaseTracking.cs`). A lowercase code like `usd` passed the case-insensitive gate and was forwarded verbatim, where Firebase/GA4 and MMPs expect uppercase. `TrackPurchase` now uppercases the currency before the gate and fan-out. (DR-28)

### Added
- **`revenue_precision` param on the Firebase `ad_impression` event** (`Runtime/Adapters/MAX/MaxAdapterImpl.cs`, `Runtime/Adapters/FirebaseAdapter.cs`, `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`). MAX's `AdInfo.RevenuePrecision` (`publisher_defined` / `exact` / `estimated` / `undefined` / empty) is now forwarded when present, so revenue can be filtered by estimate quality. The internal `IFirebaseAdapter.TrackAdImpression` signature gained a `revenuePrecision` parameter; no public API changed. (DR-06)

### Changed
- **`ad_format` on `ad_impression` is now lowercase** (`Runtime/Adapters/MAX/MaxAdapterImpl.cs`), matching the higher-volume `ad_show_requested` / `ad_show_failed` funnel events, which were already lowercase. Previously `ad_impression` sent `REWARDED` / `INTERSTITIAL`. **Historical-data seam:** any BigQuery/GA4 query that does `GROUP BY ad_format` across impression and funnel events, or that spans the upgrade date, must `LOWER(ad_format)` to join the pre- and post-3.16.2 casing. (DR-12)

## [3.16.1] - 2026-06-10

Stop two silent revenue-inflation paths (persisted purchase dedup, single ad-revenue subscription) and tag Firebase purchase events with a client-observed store environment so iOS sandbox/TestFlight revenue is filterable.

### Fixed
- **Purchase dedup was session-memory only, so a crash/restart re-counted the purchase** (`Runtime/Purchasing/Palette.PurchaseTracking.cs`). Unity IAP v5 re-delivers an unconfirmed purchase by re-firing `OnPurchasePending` on the next app launch, but the dedup set was an in-memory `HashSet` that died with the process, so the same purchase fanned out to every vendor again after a restart while the code comments and `architecture.md` claimed crash-replay immunity. The set is now persisted to `PlayerPrefs` (FIFO, capped at 512 ids) so dedup survives process death and the immunity is real. The dedup key also now prefers the store's original-transaction id when available, so an iOS restore / re-delivery of a non-consumable (new `TransactionID`, same `OriginalTransactionID`) is recognised as already-counted instead of new revenue. Note: this key assumes consumables + one-shot non-consumables (the current portfolio); auto-renewable subscriptions share an `OriginalTransactionID` across renewals and would need a different key.
- **A second `Palette.Initialize` in the CMP window doubled ad revenue for the whole session** (`Runtime/Palette.cs`, `Runtime/Adapters/MAX/MaxAdapterImpl.cs`, `Runtime/SorollaBootstrapper.cs`). On the MAX path `IsInitialized` only flips true after the CMP resolves (~1-3s), so the `if (IsInitialized) return` guard did not block a second `Initialize()` in that window: it re-ran `InitializeMax()`, which re-subscribed `OnSdkInitializedEvent` via `+=`, so when MAX finished it re-registered the ad-revenue callbacks and every impression paid out twice to Adjust / Firebase / TikTok. Closed with three guards set synchronously at entry: `s_initStarted` in `Palette.Initialize` (the choke point), `_initStarted` in `MaxAdapterImpl.Initialize` (defense-in-depth, same window bug at the impl layer), and an `s_instance != this` reject in `SorollaBootstrapper.Start` (kills a stray manually-placed bootstrapper).

### Added
- **`store_environment` param on the Firebase GA4 `purchase` event** (`Runtime/Purchasing/Palette.PurchaseTracking.cs`, `Runtime/Adapters/FirebaseAdapter.cs`, `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`). The SDK logs `purchase` after local metadata validation (positive price, ISO-4217 currency, TxID dedup) but with no store verification and no environment marker, so TestFlight/sandbox purchases land in the same GA4 stream as production revenue and could not be filtered out. The event now carries `store_environment` so dashboards/BigQuery can filter client-side purchase telemetry. On **iOS** the value is decoded from the StoreKit JWS (`order.Info.Apple.jwsRepresentation`) `environment` claim and normalized to `production`, `sandbox`, `xcode`, or `unknown`, independent of build type (TestFlight is a release build but transacts against `sandbox`). This is an unverified client label for analytics only: no purchase is verified or dropped, and canonical production revenue still requires server-side / Adjust receipt verification. On **Android**, legacy `Product` tracking, and when the iOS JWS is absent/undecodable, the value is `unknown`: there is no reliable client-side sandbox signal on Google Play (that is a server-side Play Developer API determination), so it is labelled honestly rather than guessed. The value is also recorded in the Sorolla Vitals diagnostics dispatch and the `TrackPurchase: accepted` log line.

  Note: the internal `FirebaseAdapter.TrackPurchase` signature gained a `storeEnvironment` parameter. No public `Palette` API changed: `Palette.AttachPurchaseTracking` is unchanged and derives the value automatically.

## [3.16.0] - 2026-06-01

Firebase install-count recovery (iOS Consent Mode), iOS ATT-gated ad personalization, consent-telemetry schema reshape, diagnostics console device-input fix, and dead-code/API cleanup.

### Fixed
- **Firebase severely undercounted installs on iOS — `first_open` fired cookieless and was uncountable in GA4** (`Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`, `Runtime/Adapters/FirebaseAdapter.cs`, `Runtime/Palette.cs`, `Editor/SorollaIOSPostProcessor.cs`, `Editor/AndroidManifestSanitizer.cs`, `Editor/GradlePropertiesFixer.cs`). On the MAX (Full-mode) path the SDK booted Firebase with analytics consent **denied** and waited for the CMP to resolve. iOS fires the native `first_open` at launch — before the CMP/ATT resolves — so the install was tagged `analytics_storage=denied` with no app-instance-id (`user_pseudo_id` null), and GA4 standard reports cannot count cookieless events as installs/users below the ~1000/day behavioral-modeling threshold. Adjust and GameAnalytics register the install at SDK init regardless of consent, which is why their numbers matched each other and ran well above Firebase across all games. Confirmed in BigQuery: ~50% of iOS `first_open` cookieless on Sweep Collector, ~27% on Hungry Snake; Android ~0–10% (consent resolves fast, no ATT). Regression dates to v3.6.0 (consent-gated collection + boot-denied); the cookieless signature began at v3.9.1 (Consent Mode v2).

  Fix: analytics consent is now **decoupled from ad consent**. `analytics_storage` defaults **granted** so `first_open` is counted with an app-instance-id; the three ad signals (`ad_storage` / `ad_personalization` / `ad_user_data`) stay **denied** until the CMP resolves. The defaults are written as platform Consent Mode v2 defaults so they govern the first native ping: Android `<meta-data google_analytics_default_allow_*>` injected into the merged manifest at `IPostGenerateGradleAndroidProject`, and iOS `GOOGLE_ANALYTICS_DEFAULT_ALLOW_*` `Info.plist` keys at `OnPostProcessBuild`. At runtime `analytics_storage` is downgraded to denied only for a confirmed GDPR decline (`ConsentStatus.Denied`); every other state (NotApplicable / Required / Unknown / Obtained) keeps analytics granted. The hard `SetAnalyticsCollectionEnabled(false)` call is removed — it didn't reliably suppress on iOS and only stripped the install's identifier; collection is now always enabled, which also clears the persisted-disabled state older builds left on returning devices.

- **Sorolla Vitals diagnostics console could not be opened on device (worked in the Editor)** (`Runtime/Diagnostics/InputSystem/AssemblyInfo.cs` (new), `Runtime/Diagnostics/InputSystem/SorollaDiagnosticsInputSystemBackend.cs`, `Runtime/link.xml`). The new-Input-System touch backend lives in an optional companion assembly that nothing references — it self-registers via `[RuntimeInitializeOnLoadMethod]`. Unlike every adapter assembly it had no stripping protection, so IL2CPP managed stripping removed it on device builds: the backend never registered, the console received no touch input, and the 5-tap open gesture did nothing (the Editor works because Mono doesn't strip). Fix: added `[assembly: AlwaysLinkAssembly]` (`AssemblyInfo.cs`), `[Preserve]` on the backend class + its `Register()` method, and a `link.xml` entry — matching the adapter assemblies. The protection is gated by the assembly's `ENABLE_INPUT_SYSTEM` define constraint, so legacy-Input-Manager-only projects (where the assembly does not compile) are unaffected.

- **iOS: ad personalization was signaled to Firebase/Facebook as "granted" for ATT-denied users who accepted the CMP** (`Runtime/Palette.cs`, `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`, `Runtime/Adapters/FirebaseAdapter.cs`, `Runtime/SorollaBootstrapper.cs`). `ad_personalization` / `ad_user_data` (and Facebook advertiser tracking) followed the GDPR/UMP decision alone, so an EEA user who consented in the CMP but denied Apple ATT was reported personalized — contrary to Apple's rule that personalized ads require **both** ATT-authorized **and** consent. Fix: those two ad signals + Facebook now require GDPR consent **AND** (on iOS) ATT-authorized, via `AdPersonalizationAllowed`; `ad_storage` continues to follow the GDPR/UMP decision and `analytics_storage` continues to follow the decoupled rule above. `AttStatus` is always `Authorized` off-iOS, so this is a no-op on Android. Facebook `UpdateConsent` is now re-asserted on every consent resolution (moved above the ad-bucket early-return) so an ATT change is never missed when the GDPR bucket is unchanged. Adjust stays gated on the GDPR decision only — its native ATT handling withholds the IDFA — so SKAdNetwork / organic install attribution is unaffected.

- **Firebase analytics events were dropped silently after a failed init** (`Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`). When Firebase init failed (e.g. missing `GoogleService-Info.plist` or dependencies) queued events were discarded with no signal — they appeared in Sorolla Vitals but never reached Firebase ("in the game, not in Firebase realtime"). Now logs a one-time warning naming the likely cause instead of dropping silently.

### Changed
- **Consent telemetry param schema reshaped** (`Runtime/Palette.cs`, `Runtime/SorollaBootstrapper.cs`). `consent_resolved` / `consent_changed` / `att_decision` now emit `{ gdpr, att_status (iOS only), personalized_ads, analytics }`, separating the raw user decisions (`gdpr`, `att_status`) from the SDK-resolved outcomes (`personalized_ads`, `analytics`). Key renames from the previous shape: `max_status` → `gdpr`, `consent` → `personalized_ads`, and `analytics_consent` → `analytics`; `att_status` keeps its name but is now also emitted on `consent_resolved` / `consent_changed` (previously `att_decision` only). All enum values are now lowercase snake_case (`gdpr`: `obtained|denied|not_applicable|required|unknown`; `att_status`: `authorized|denied|restricted|not_determined` — previously PascalCase), and the constant `source` discriminator is dropped from `consent_resolved` / `consent_changed` (kept on `att_decision`). **Breaking for first-party GA4 / BigQuery dashboards or saved queries keyed on the old `max_status` / `consent` / `analytics_consent` names, or filtering `att_status` on PascalCase values** — they will return empty / mismatch after this release.
- **Trimmed dead adapter API and duplicated editor/sanitizer logic** (`Runtime/Adapters/*`, `Editor/*`). Removed unused adapter methods with no remaining consumer; deduplicated the Android manifest / gradle sanitizers and editor define logic. No public `Palette` API change. The legacy `AutoTracker` / `TrackPurchase(Product)` purchase shim is **retained** — game backends still on the legacy Unity IAP integration path consume it; its removal is deferred until those migrate to `Palette.AttachPurchaseTracking` (Unity IAP v5 `StoreController`).

### Behavior change
- Builds now ship Consent Mode v2 default keys in the Android manifest and iOS `Info.plist` (analytics granted, ads denied), and Firebase analytics collection is always enabled. An EEA user emits one identified `first_open` before the CMP resolves; ad personalization remains gated by the CMP at all times. This restores install parity with Adjust / GameAnalytics. Studios with strict EEA analytics requirements should review the posture in `FirebaseAdapterImpl.ApplyConsentSignals` and the injected Consent Mode defaults.

### Migration
No public `Palette` C# API change — `Palette.Initialize`, the economy / purchase / ad APIs, and `SorollaConfig` are unchanged. The consent **flow** (CMP → ATT, owned by MAX) is unchanged, but the consent **posture** is refined — on iOS, ATT now gates ad personalization (see Fixed) — and the first-party consent **telemetry** params changed shape (see Changed): update any GA4 / BigQuery dashboards keyed on `max_status` / `consent` / `analytics_consent` to `gdpr` / `personalized_ads` / `analytics`, and switch `att_status` filters from PascalCase to lowercase values. Rebuild iOS/Android for the new Consent Mode defaults and the ATT-gating to take effect; on iOS the cookieless-`first_open` share should collapse toward the Android baseline within a reporting window.

## [3.15.5] - 2026-05-13

Diagnostics input-system compatibility release.

### Fixed
- **Sorolla Vitals input handling now follows the host project's active Unity input backend** (`Runtime/Diagnostics/*`): new Input System-only games compile and run the diagnostics gesture/mouse path without touching `UnityEngine.Input`, while old Input Manager-only projects keep the legacy path without taking a dependency on `com.unity.inputsystem`.
- **Input System support is isolated in an optional companion assembly** (`Runtime/Diagnostics/InputSystem/*`): the new backend is compiled only when `ENABLE_INPUT_SYSTEM` is present and registers itself at runtime, avoiding compile-time dependency issues for legacy projects.

### Notes
- The SDK does not mutate a game's `EventSystem`. Projects using uGUI still need the EventSystem input module that matches their Player Settings (`InputSystemUIInputModule` for new Input System-only, `StandaloneInputModule` for old Input Manager-only).

## [3.15.4] - 2026-05-11

Firebase economy event placement-attribution fix.

### Fixed
- **Economy earn events lose source category in BigQuery** (`Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`, `Runtime/Palette.Economy.cs`): `TrackResourceEvent` previously dropped the `EconomySource`/`EconomySink` enum entirely on both flows and gated the granular itemId to spend events only. Earn events arrived in BigQuery with `virtual_currency_name` + `value` only — analysts could not tell apart `LevelReward` run-rewards from `LevelReward` chest claims, or distinguish `AdReward` bonuses from progression rewards. Reported by boat-runner (Raft Evolution) integration QA.

  Param shape, post-fix — mirrors GA4's spec-defined asymmetry between earn and spend (canonical `item_name` slot exists on spend, not on earn):
  - `earn_virtual_currency` emits `virtual_currency_name`, `value`, **`source`** (snake-cased `EconomySource` enum), and **`source_item`** (the granular `itemId` string when the caller supplies one — absent otherwise).
  - `spend_virtual_currency` emits `virtual_currency_name`, `value`, **`item_name`** (canonical GA4 slot, the granular `itemId` when supplied), and **`sink`** (snake-cased `EconomySink` enum).

  The Vitals diagnostics console records the same schema-owned keys per direction so what shows in-app matches what lands in BigQuery. Non-reserved context extras such as `map` / `level` continue to pass through, but attempts to pass Sorolla-owned economy keys (`virtual_currency_name`, `value`, `source`, `source_item`, `item_name`, `sink`) via `extraParams` are rejected with a warning before Firebase dispatch. `GameAnalyticsAdapter.TrackResourceEvent` is unchanged — it keeps receiving the synthesized fallback itemId because its native call requires both `itemType` and `itemId` strings.

### Migration
No public Palette API change: `Palette.Economy.Earn(currency, amount, source, itemId)` and `Palette.Economy.Spend(currency, amount, sink, itemId)` keep the same signature. The change is purely in the param names that land in Firebase / BigQuery. Studios should query `source` / `sink` for the category dimension and `source_item` / `item_name` for the granular itemId.

## [3.15.3] - 2026-05-06

Code-only Sorolla Vitals and studio documentation refresh.

### Added
- **Sorolla Vitals runtime console** (`Runtime/Diagnostics/*`): code-only OnGUI diagnostics console opened by five taps in the top-left safe area or `Palette.ShowDebugger()`, with SDK health, event/ad/purchase smoke checks, copied reports, and runtime problem summaries.
- **Validation checklist** (`Documentation~/validation.md`): separate Prototype and Full-mode soft-launch validation tracks.

### Changed
- **Debug UI sample removed** (`Samples~/DebugUI`, `package.json`): the prefab/sample-based Debug UI is replaced by the built-in Vitals console; package dependencies are reduced to `com.unity.ugui`.
- **Diagnostics capture expanded** (`Runtime/Palette*.cs`, `Runtime/Adapters/*`): level, economy, custom event, ad, MAX, Adjust, and purchase signals feed Vitals.
- **Mode source of truth hardened** (`Editor/SorollaSettings.cs`, `Editor/Sdk/DefineSymbols.cs`): editor mode resolves from `Assets/Resources/SorollaConfig.asset` and removes legacy mode defines.
- **Studio docs reframed** (`Documentation~/quick-start.md`, `Documentation~/switching-to-full.md`, `Documentation~/guides/*`): Prototype is the fast GameAnalytics/Facebook/Firebase path; Full mode is the soft-launch migration path; stale public API references were replaced with `Palette.Level.*`, `Palette.Economy.*`, and `Palette.AttachPurchaseTracking(...)`.

### Fixed
- **Docs build references** (`Documentation~/docfx/sdk.csproj`): added Unity Input Legacy and Text Rendering module references required by the Vitals console.

## [3.15.2] - 2026-05-05

Follow-up editor settings hardening.

### Fixed
- **MAX consent flow sync** (`Runtime/PaletteConstants.cs`, `Editor/MaxSettingsSanitizer.cs`, `Editor/BuildValidationVendorSettings.cs`): the publisher privacy policy URL is now centralized in `PaletteConstants` and enforced exactly in `AppLovinSettings`, same as the shared MAX SDK key. Build Health verifies that consent flow is enabled and the URL matches the expected shared value.

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
