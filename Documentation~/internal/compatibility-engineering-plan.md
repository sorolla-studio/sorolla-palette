# Sorolla SDK: Compatibility Engineering Plan

**Date:** 2026-03-31
**Origin:** Board of Advisors review of Unity 6 Android build debugging session
**Focus:** Studio-value features only - plug-and-play SDK experience

## Context

The Unity 6 debugging session exposed structural gaps in the SDK's studio experience. This plan focuses on making the SDK truly plug-and-play with zero publisher intervention.

**Key finding:** The sanitizer is remove-only (orphan cleanup, dedup, class correction). AAR can't do removal. The real wins: validate the environment, auto-fix issues, report clearly.

**Scope:** 2 Unity targets only (2022.3 LTS, Unity 6). Each Palette release ships one tested SDK combo via `SdkRegistry.cs` version pins.

---

## Phase 1: Facebook Fork Fix + Hook Ordering - DONE (2026-03-31)

**Goal:** Eliminate the two known time bombs.

### callbackOrder -100

`BuildValidatorPreprocessor.callbackOrder` changed from `0` to `-100`. Ensures Palette runs before Adjust (0) and AppLovin MAX (int.MaxValue - 10).

Execution order:
```
-2147483598  URP CorePreprocessBuild
-100         Palette BuildValidatorPreprocessor
0            Adjust, GameAnalytics, InputSystem
2147483637   AppLovin MAX
```

### Facebook fork bitmask fix

Replaced `#if UNITY_2023_1_OR_NEWER` in `FacebookManifestSanitizer.cs` with runtime PlayerSettings bitmask read via `Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings")`.

Bitmask: `androidApplicationEntry` - 1=Activity, 2=GameActivity, 3=both. Formula: `(value & 2) != 0` -> GameActivity.

Commit `41f5d8a` on `com.lacrearthur.facebook-sdk-for-unity`. No hash pin in manifest.json - Unity picks up on next resolve.

Why duplicate bitmask logic (also in Palette's `AndroidManifestSanitizer`): Facebook SDK must work without Sorolla. No cross-package dependency. 10 lines of code.

## Phase 2: R8/AGP Validator + Console Breadcrumb - DONE (2026-03-31)

**Goal:** Catch R8/AGP crashes at pre-build; surface issues early.

### R8/AGP check (`CheckR8AgpConfig()`)

Added to `BuildValidator.cs`. Detects:
- R8 pin (`com.android.tools:r8`) in `baseProjectTemplate.gradle` - Error on Unity 6, Valid on Unity 2022
- Kotlin stdlib version forcing in `mainTemplate.gradle` - Warning on Unity 6, Valid on Unity 2022

Uses `#if UNITY_6000_0_OR_NEWER` for compile-time branching.

### R8 auto-fix (in `RunAutoFixes()`)

On Unity 6 only: if `baseProjectTemplate.gradle` contains an R8 buildscript block, creates `.backup` and removes the block. Uses `RemoveBuildscriptBlock()` - brace-matching parser, not regex.

Kotlin stdlib forcing: warn only, no auto-fix (studio may have it intentionally).

### Console breadcrumb (`BuildHealthConsoleNotifier`)

`[InitializeOnLoad]` class with `EditorApplication.delayCall` callback. On domain reload, if build target is Android or iOS, runs `RunAllChecks()` and logs a single warning if errors are found. No popup, no window - just a breadcrumb in the console.

## Phase 3: CI Pipeline - DONE (2026-03-31)

**Goal:** Run tests automatically on push/PR to SDK paths.

### Test assembly

`Tests/Editor/Sorolla.Editor.Tests.asmdef` - references `Sorolla.Editor`, TestRunner assemblies. `InternalsVisibleTo` already configured in `Editor/AssemblyInfo.cs`.

### EditMode tests

**BuildValidatorTests.cs** (4 tests):
- `RemoveBuildscriptBlock` with R8 pin, without block, nested braces, unbalanced braces

**AndroidManifestSanitizerTests.cs** (8 tests):
- Bitmask formula verification (3 cases: values 1, 2, 3)
- `DetectWrongMainActivityInXml` - wrong class, correct class, no launcher activity
- `DetectLauncherManifestIssueInXml` - missing activity, wrong class, correct class, no application element

Refactored `DetectWrongMainActivity` and `DetectLauncherManifestIssue` to extract XML parsing into `internal` overloads (`*InXml`) that accept content + expected activity. Public methods delegate with file I/O + PlayerSettings.

### Test fixtures (`Tests/Editor/Fixtures/`)

- `manifest_wrong_activity.xml` - UnityPlayerActivity (wrong for GameActivity projects)
- `manifest_correct_gameactivity.xml` - correct GameActivity manifest
- `launcher_manifest_missing_activity.xml` - empty application element
- `base_project_template_with_r8.gradle` - with R8 buildscript block
- `base_project_template_clean.gradle` - clean, no R8

### GitHub Actions (`.github/workflows/sdk-validation.yml`)

Triggers on push/PR to `Packages/com.sorolla.sdk/**`. Matrix: `2022.3.62f1` + `6000.0.62f1`. Uses `game-ci/unity-test-runner@v4` in EditMode. Uploads test artifacts.

Requires `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` secrets (same as existing `android-build.yml`).

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| `callbackOrder -100` (not -1) | Leaves room for future hooks between Sorolla and Adjust |
| Duplicate bitmask logic in Facebook fork | No cross-package dependency - Facebook SDK must work without Sorolla |
| R8 auto-fix with backup | Clear-cut incompatibility (always crashes). Backup created, loud log message |
| Kotlin stdlib: warn only | May be intentional, doesn't cause crashes on AGP 8.x |
| Console breadcrumb, not popup | Studios may not open Palette window until build fails. Single log line, no disruption |
| Internal XML overloads for testing | Separates pure parsing logic from file I/O and PlayerSettings, enabling unit tests |

---

## Phase 4: Studio-Facing Deep Dive - PLANNED

**Goal:** Make the SDK truly plug-and-play with zero publisher intervention.

Topics to investigate:

- **Auto-fix scope expansion:** Identify which "detect-only" checks should become auto-fixes for zero-friction integration
- **Error message audit:** Review every BuildValidator error from a studio perspective - can they fix it without contacting us?
- **One-click mode switch:** Switching Prototype/Full should handle everything (SDK install/uninstall, defines, manifest cleanup) with a single button
- **First-build success rate:** Track how often a studio's first build after SDK import succeeds without manual intervention. Target: 100%
- **Silent vs interactive fixes:** Define policy for when the SDK should fix silently (safe, reversible) vs show a dialog (destructive, ambiguous)

## File Manifest

| File | Action | Status |
|------|--------|--------|
| `Editor/BuildValidator.cs` | Edit (R8 check, callbackOrder, breadcrumb) | Done |
| `Editor/AndroidManifestSanitizer.cs` | Edit (internal XML overloads) | Done |
| `facebook-unity-sdk-upm/.../FacebookManifestSanitizer.cs` | Edit (bitmask fix) | Done (41f5d8a) |
| `Tests/Editor/Sorolla.Editor.Tests.asmdef` | Create | Done |
| `Tests/Editor/BuildValidatorTests.cs` | Create | Done |
| `Tests/Editor/AndroidManifestSanitizerTests.cs` | Create | Done |
| `Tests/Editor/Fixtures/*.xml, *.gradle` | Create (5 files) | Done |
| `.github/workflows/sdk-validation.yml` | Create | Done |
