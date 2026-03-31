# Sorolla SDK: Compatibility Engineering Plan

**Date:** 2026-03-31
**Origin:** Board of Advisors review of Unity 6 Android build debugging session
**Focus:** Studio-value features only - plug-and-play SDK experience

## Context

The Unity 6 debugging session exposed structural gaps in the SDK's studio experience. This plan focuses on making the SDK truly plug-and-play with zero publisher intervention.

**Key finding:** The sanitizer is remove-only (orphan cleanup, dedup, class correction). AAR can't do removal. The real wins: validate the environment, auto-fix issues, report clearly.

---

## Phase 1: Facebook Fork Fix + Hook Ordering (Week 1) - PENDING

**Goal:** Eliminate the two known time bombs.

- Replace `#if UNITY_2023_1_OR_NEWER` in Facebook fork's `FacebookManifestSanitizer.cs` with runtime PlayerSettings bitmask read
- Change `BuildValidatorPreprocessor.callbackOrder` from 0 to -100
- Document hook execution order

## Phase 2: R8/AGP Validator + Detect-vs-Fix Separation (Week 2) - PENDING

**Goal:** Catch R8/AGP crashes at pre-build; make SDK modifications transparent.

- New `R8AgpValidator.cs` - detect AND auto-fix incompatible R8 pins (remove buildscript block, log the change)
- Wire into BuildValidator.RunAllChecks() and RunAutoFixes()
- Separate diagnostic logs from fix logs in OnPreprocessBuild output

## Phase 3: CI Pipeline (Week 3) - PENDING

**Goal:** Run all tests automatically on push/PR.

- New `.github/workflows/sdk-validation.yml`
- Matrix: Unity 2022.3 LTS + Unity 6
- `game-ci/unity-test-runner@v4` with EditMode tests
- Test fixtures in `Tests/Editor/Fixtures/`

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| `callbackOrder -100` (not -1) | Leaves room for future hooks between Sorolla and Adjust |
| Duplicate bitmask logic in Facebook fork | No cross-package dependency - Facebook SDK must work without Sorolla |
| R8/AGP validator auto-fixes | For plug-and-play, studios shouldn't need to manually edit Gradle files. Auto-fix with logged message, like the manifest sanitizer. |
| Keep manifest sanitizer | Sanitizer removes orphans - AAR can't do removal |

---

## Phase 4: Studio-Facing Deep Dive (Week 4) - PLANNED

**Goal:** Make the SDK truly plug-and-play with zero publisher intervention.

This plan (Phases 1-4) builds a safe development base: tested logic, CI, and validated builds. Phase 5 is a separate deep dive focused on the studio experience. Topics to investigate:

- **Post-import health check:** On first SDK import, surface a validation window showing environment status (JDK, Gradle, activity mode, manifest state) instead of waiting for build time
- **Auto-fix scope expansion:** Identify which "detect-only" checks should become auto-fixes for zero-friction integration
- **Error message audit:** Review every BuildValidator error from a studio perspective - can they fix it without contacting us?
- **One-click mode switch:** Switching Prototype/Full should handle everything (SDK install/uninstall, defines, manifest cleanup) with a single button
- **First-build success rate:** Track how often a studio's first build after SDK import succeeds without manual intervention. Target: 100%
- **Silent vs interactive fixes:** Define policy for when the SDK should fix silently (safe, reversible) vs show a dialog (destructive, ambiguous)
