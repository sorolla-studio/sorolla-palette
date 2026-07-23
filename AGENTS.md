# AGENTS.md

Codex guidance for working in the Sorolla SDK package.

## Purpose

This directory is the `com.sorolla.sdk` Unity package: a plug-and-play mobile publisher SDK for Unity.
It wraps GameAnalytics, Facebook, AppLovin MAX, Adjust, Firebase, TikTok, Unity IAP, ATT, and GDPR/CMP flows behind the `Sorolla.Palette` API.

The parent Unity project is only a local testbed shell. SDK source changes belong in this package repo.

## Terminology — write the full name, never bare "GA"

Two unrelated vendors collide in the same two letters; bare "GA" in commits, docs, or chat has caused real confusion (and one shipped bug where review focused on the wrong vendor's spec). Always use the full name:

- **GameAnalytics** — the third-party `com.gameanalytics` vendor SDK. Adapter: `GameAnalyticsAdapter` (Real path under `#if GAMEANALYTICS_INSTALLED`). Note: the GameAnalytics whitelist drops some economy events server-side anyway, so this is the **secondary** analytics destination.
- **Firebase** — Firebase Analytics SDK; the client library wrapped by `FirebaseAdapter` / `FirebaseAdapterImpl`. Events flow Firebase → GA4 → BigQuery.
- **GA4** — Google Analytics 4, the **backend behind Firebase**. Where event-schema specs come from (`developers.google.com/analytics/devguides/collection/ga4/reference/events`). Same backend that auto-exports to BigQuery.
- **BigQuery** — the analyst-facing data warehouse populated by GA4's auto-export. Studios run their dashboards off BQ tables. Firebase / GA4 / BigQuery are one pipeline; getting Firebase emission right is what matters for studio analytics.

GameAnalytics ≠ GA4. Never write "the GA spec said so" — write "the GA4 spec said so" if you mean Firebase's backend, or "GameAnalytics' contract requires X" if you mean the vendor SDK. Same in commit messages.

## Current Context

- Package name: `com.sorolla.sdk`
- Current package version: read `package.json`
- Namespace: `Sorolla.Palette`
- Main public API: `Runtime/Palette.cs`
- Auto-init entry point: `Runtime/SorollaBootstrapper.cs`
- Public docs: `Documentation~/`
- Internal docs: a separate private internal-docs repo (kept outside this public repo)

## Authoritative Docs

Read these before substantial changes:

- `Documentation~/architecture.md` - SDK architecture, adapter pattern, DX-first API rules
- `Documentation~/api-reference.md` - generated public API reference
- `Documentation~/troubleshooting.md` - known integration failures
- The internal SDK index, the current public-API risk queue, and the authoritative QA greenlight workflow live in a separate private internal-docs repo (not part of this public repo).

## Internal Docs Routing

This repo is **public**. Never commit internal working docs here: audits, remediation plans, QA checklists/prep notes, devlogs, refactor backlogs, risk analyses. They live in a separate private internal-docs repo. Public-facing docs for SDK consumers go in `Documentation~/` and `CHANGELOG.md` only. `.gitignore` blocks the known internal-doc patterns as a backstop, but the rule is by intent, not pattern: if a doc is not for SDK consumers, it does not get committed here.

## Architecture Rules

- Optional SDKs use the stub plus implementation assembly pattern.
- Stubs live under `Runtime/Adapters/` and always compile.
- Implementations live under `Runtime/Adapters/{MAX,Adjust,Firebase}/`.
- Implementation `.asmdef` files need both `versionDefines` and `defineConstraints`.
- `SdkRegistry.cs` is the single source of truth for third-party SDK package IDs and versions.
- `manifest.json` is the source of truth for mode/package state; do not rely on assembly detection during Unity domain reloads.
- For IL2CPP runtime registration, implementation assemblies need linker preservation via `AlwaysLinkAssembly` and `Preserve`.

## Runtime Invariants (init, config, threading)

Hard invariants for the init/adapter/analytics paths, surfaced by a 2026-06 architecture review. Static-source / traced, not yet device-repro'd.

- **Init must never wedge on missing config.** On a MAX/Full build, `InitializeMax()` early-returns when `SorollaConfig` is null, BEFORE it subscribes the MAX init callback, so `IsInitialized` never flips, the pending-event queue never flushes, and `OnInitialized` never fires. "Ready" must be a guaranteed transition: never add an early return upstream of it, and a missing/misnamed config must fail loud or safe-degrade, not silently hang.
- **Vendor callbacks feed non-thread-safe state.** The pending-event queue and `Palette.Level` start-times assume a main-thread-only contract that is not enforced. MAX callbacks are NOT guaranteed main-thread (they follow `InvokeEventsOnUnityMainThread`); pin `MaxSdk.SetInvokeEventsOnUnityMainThread(true)` if you touch MAX init. Adjust already marshals to the main thread, and `SorollaDiagnostics` is lock-guarded. The QA-bridge worker-enqueue / main-thread-drain split is correct concurrency engineering; do not "simplify" it away.
- **`extraParams` on `Palette.Level.*` / `Palette.Economy.*` reach Firebase only, never GameAnalytics.** Per-vendor parameter parity is something to verify, not assume (see the Adapter Endpoint Review section).
- **The QA bridge and 5-tap diagnostics console are intentional control surfaces.** Keep their access, and what `/qa/exec` can do, narrow; do not widen either.

Verdict of that review: re-architect the runtime core in place behind the frozen public API (extract a pure consent resolver + an explicit init state machine, split the diagnostics core); do NOT rewrite. The static facade, the stub+impl asmdef split, and the explicit per-event vendor fan-out are deliberate and stay.

## DX-First API Rules

Apply these to any public API change:

- Prefer rich types and curated enums over primitive/string parameters.
- Prefer one-line "register once and forget" integrations.
- Treat silent data misuse as a critical bug: validate, drop, and warn loudly rather than forwarding corrupt analytics.
- Avoid flexible escape hatches for studio code; Sorolla owns event schemas, taxonomies, and dashboard semantics.
- Before merging a public API, ask:
  - What is the minimum studio code must pass?
  - Can the SDK derive the rest?
  - If the value is wrong, does it fail loud or silently corrupt data?

Known current DX-API queue:

- `TrackEvent(string, Dictionary<string, object>)` still needs a curated `Palette.Events.*` catalog.
- `ShowRewardedAd` / `ShowInterstitialAd` still need curated `AdPlacement` context.
- `SetUserProperty` and `SetCrashlyticsKey` remain stringly typed.
- Remote Config code generation remains deferred.

## Code Style

- Follow existing C# and Unity patterns in nearby files.
- Keep changes scoped to the requested behavior.
- Use XML documentation for public APIs.
- Avoid magic strings in new public surfaces; use constants/enums/schema-generated accessors.
- Never null-check `[SerializeField]` references purely defensively; missing inspector wiring should fail loudly.
- Subscribe methods directly to events where practical.
- Check existing SDK APIs before writing JNI/Objective-C/native wrappers.
- `UNITY_IOS` is defined in the Editor when target platform is iOS; gate native iOS calls with `#if !UNITY_EDITOR`.
- Mobile hot paths should avoid unnecessary GC.

## Adapter Endpoint Review (mandatory)

When touching any method under `Runtime/Adapters/*/`*AdapterImpl.cs* (Firebase, MAX, Adjust, TikTok, Facebook, GameAnalytics), apply this checklist **before staging the diff**. It exists because **v3.6.0** silently shipped a payload regression in `FirebaseAdapterImpl.TrackResourceEvent`: the prior implementation emitted both `itemType` (as `placement`) and `itemId` (as `item_id`) on **both** earn and spend; the "use GA4 canonical constants" rewrite (commit `4ad6891`) collapsed three populated slots to two on spend and one on earn — `placement` was deleted, `item_id` was deleted, and the remaining `item_name` slot was gated to spend only. The commit was framed as constants-conversion, so review optimized for "are the canonical names correct" and never noticed the silent payload reduction. Six weeks of `earn_virtual_currency` rows in BigQuery carried only `virtual_currency_name` + `value`; placement-attribution queries on earn were impossible until a downstream game integration caught it in 2026-05.

1. **Every input parameter must be referenced in the body, or there must be a one-line comment explaining why it's intentionally ignored.** Unused params on a public adapter surface are bugs.
2. **Symmetric event pairs (earn/spend, level_start/level_end, ad_loaded/ad_shown) must emit the same parameter keys** unless the GA4 spec explicitly differs. If a param is added on one half, audit the other half in the same commit.
3. **Map the call site to the adapter: read the caller (`Palette.*.cs`) and confirm every value it passes is either emitted by the adapter, validated and dropped with a `PaletteLog.Warning`, or has a code comment explaining the discard.** Silent drop is forbidden.
4. **`SorollaDiagnostics.RecordEventDispatch` is not a substitute for adapter emission.** Diagnostics records caller intent; the adapter is the source of truth for what hits Firebase / GameAnalytics / Adjust. Treat parity between the two as a hard invariant.
5. **Commit message must explicitly list any field that is added, renamed, dropped, or made conditional on the event payload.** "GA4 spec compliance" is not enough — name the exact params that change.

## Testing And Validation

- Use focused EditMode tests when touching editor/build validation behavior.
- Existing tests live under `Tests/Editor/`.
- Public docs API reference is generated from XML comments; regenerate with `Tools~/build-docs.sh` when public XML docs change.
- For build/integration confidence, use Unity and on-device QA rather than assuming compilation alone is enough.
- For release validation of a game using this SDK, use the `qa-greenlight` skill/workflow (internal QA docs live in the separate private internal-docs repo).

## Diagnostics Console Iteration Loop

- The runtime diagnostics console in `Runtime/Diagnostics/` is UI Toolkit: `UITK/SorollaDebugMenuOverlay*.cs` with USS/UXML under `UITK/Resources/` and a `PanelSettings` created at runtime. Keep it lightweight; do not add prefabs or Canvas UI, and do not reintroduce OnGUI.
- For small console UI/layout changes, use a lean loop: edit, search for dead references with `rg`, run `git diff --check`, then stop. Do not run Unity MCP compile checks or editor log searches by default.
- For C# API/signature changes that could break compilation, run at most one focused local compile check such as `dotnet build Sorolla.Runtime.csproj --no-restore`. Do not retry flaky Unity MCP checks unless the user explicitly asks or the local compile check reports a real current failure.
- Treat Unity Editor logs as noisy and potentially stale. Do not inspect them unless investigating a concrete current editor failure.
- When deleting console files or panes, trust source-level references plus `rg`; do not chase generated project artifacts unless they are the deliverable. The studio surface is one report pane with no tab bar; tabs exist only behind the internal 5-tap unlock.

## Git And Commits

- This package is its own git repo.
- The parent Unity testbed has a separate git repo.
- Do not stage unrelated changes.
- Prefer explicit staging by filename for SDK commits.
- Never use `git add -A` or broad staging unless the user explicitly asks and the diff has been reviewed.
- If the worktree is dirty, preserve user changes and work with them.
