# AGENTS.md

Codex guidance for working in the Sorolla SDK package.

## Purpose

This directory is the `com.sorolla.sdk` Unity package: a plug-and-play mobile publisher SDK for Unity.
It wraps GameAnalytics, Facebook, AppLovin MAX, Adjust, Firebase, TikTok, Unity IAP, ATT, and GDPR/CMP flows behind the `Sorolla.Palette` API.

The parent Unity project is only a local testbed shell. SDK source changes belong in this package repo.

## Current Context

- Package name: `com.sorolla.sdk`
- Current package version: read `package.json`
- Namespace: `Sorolla.Palette`
- Main public API: `Runtime/Palette.cs`
- Auto-init entry point: `Runtime/SorollaBootstrapper.cs`
- Public docs: `Documentation~/`
- Internal docs: `/Users/arthur/workspace/sorolla-docs/platform/sdk/`

## Authoritative Docs

Read these before substantial changes:

- `Documentation~/architecture.md` - SDK architecture, adapter pattern, DX-first API rules
- `Documentation~/api-reference.md` - generated public API reference
- `Documentation~/troubleshooting.md` - known integration failures
- `/Users/arthur/workspace/sorolla-docs/platform/sdk/index.md` - internal SDK index
- `/Users/arthur/workspace/sorolla-docs/platform/sdk/research/dx-first-audit-2026-04.md` - current public API risk queue
- `/Users/arthur/workspace/sorolla-docs/platform/sdk/qa/agent/SKILL.md` - authoritative QA greenlight workflow

## Architecture Rules

- Optional SDKs use the stub plus implementation assembly pattern.
- Stubs live under `Runtime/Adapters/` and always compile.
- Implementations live under `Runtime/Adapters/{MAX,Adjust,Firebase}/`.
- Implementation `.asmdef` files need both `versionDefines` and `defineConstraints`.
- `SdkRegistry.cs` is the single source of truth for third-party SDK package IDs and versions.
- `manifest.json` is the source of truth for mode/package state; do not rely on assembly detection during Unity domain reloads.
- For IL2CPP runtime registration, implementation assemblies need linker preservation via `AlwaysLinkAssembly` and `Preserve`.

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

Known current queue from the internal audit:

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

## Testing And Validation

- Use focused EditMode tests when touching editor/build validation behavior.
- Existing tests live under `Tests/Editor/`.
- Public docs API reference is generated from XML comments; regenerate with `Tools~/build-docs.sh` when public XML docs change.
- For build/integration confidence, use Unity and on-device QA rather than assuming compilation alone is enough.
- For release validation of a game using this SDK, use the `qa-greenlight` skill/workflow and the internal docs under `/Users/arthur/workspace/sorolla-docs/platform/sdk/qa/`.

## Diagnostics Console Iteration Loop

- The runtime diagnostics console in `Runtime/Diagnostics/` is code-only OnGUI. Keep it lightweight; do not add assets, prefabs, UI Toolkit, or Canvas UI.
- For small console UI/layout changes, use a lean loop: edit, search for dead references with `rg`, run `git diff --check`, then stop. Do not run Unity MCP compile checks or editor log searches by default.
- For C# API/signature changes that could break compilation, run at most one focused local compile check such as `dotnet build Sorolla.Runtime.csproj --no-restore`. Do not retry flaky Unity MCP checks unless the user explicitly asks or the local compile check reports a real current failure.
- Treat Unity Editor logs as noisy and potentially stale. Do not inspect them unless investigating a concrete current editor failure.
- When deleting console files or tabs, trust source-level references plus `rg`; do not chase generated project artifacts unless they are the deliverable.

## Git And Commits

- This package is its own git repo.
- The parent Unity testbed has a separate git repo.
- Do not stage unrelated changes.
- Prefer explicit staging by filename for SDK commits.
- Never use `git add -A` or broad staging unless the user explicitly asks and the diff has been reviewed.
- If the worktree is dirty, preserve user changes and work with them.
