# Handoff: Unity 6 Android Build Debugging Session

**Date:** 2026-03-31
**Project:** Raft Evolution (GembirdsGaming)
**Branch:** `sorolla-sdk-integration`
**Context:** First Android build after merging SoftLaunch (Unity 6000.4.0f1 upgrade) into our SDK integration branch.

---

## Starting State

- Studio upgraded from Unity 2022.3.62f2 to Unity 6000.4.0f1 (Unity 6 LTS patch 4)
- We merged 9 SoftLaunch commits into `sorolla-sdk-integration`, preserving all SOROLLA_FULL scripting defines
- SDK integration was complete (MAX, Firebase, Adjust, TikTok, SorollaEventBridge)
- No Android or iOS build had been attempted on Unity 6 yet

---

## Issue 1: AGP Auto-Upgrade Prompts

**What happened:** Unity detected outdated Gradle templates and offered to fix them:
- baseProjectTemplate.gradle: AGP 7.4.2 -> 8.10.0
- mainTemplate.gradle: deprecated attributes (aaptOptions, lintOptions, minSdkVersion, etc.)
- launcherTemplate.gradle: same deprecated attributes

**Decision:** Accepted all three auto-upgrades. Unity 6 requires AGP 8.x and the new Gradle DSL.

---

## Issue 2: R8 Version Mismatch (CRITICAL)

**Error:** `NoSuchMethodError: setBuildMetadataConsumer` during dexing.

**Root cause:** `baseProjectTemplate.gradle` had a `buildscript` block pinning R8 to 8.1.56. This was added in commit `3efb2c8d` for Unity 2022 (AGP 7.4.2) because AGP 7's bundled R8 couldn't handle Kotlin 2.0 metadata from AppLovin SDK 13.x and Firebase 23.x. AGP 8.10.0 bundles a modern R8 that handles Kotlin 2.0 natively - the pin conflicts with it.

**Fix:** Removed the entire `buildscript { ... }` block from `baseProjectTemplate.gradle`.

**Consideration:** This breaks Unity 2022 compatibility if the same file is used. But `baseProjectTemplate.gradle` is a per-project file, not part of the SDK package, so each project gets the right version for its Unity/AGP combo.

**SDK action item:** Added to roadmap - SDK setup validator should detect R8 pins incompatible with the current AGP version and warn (not auto-fix, since it's a project file).

---

## Issue 3: Missing Launcher Activity

**Error:** `DeploymentOperationFailedException: No activity in the manifest with action MAIN and category LAUNCHER`

**Investigation chain:**

### Attempt 1: Disable useCustomLauncherManifest
SoftLaunch had `useCustomLauncherManifest: 1` with an empty `LauncherManifest.xml` (no activity declaration). The studio enabled all custom template flags at once in commit `d8133236` ("panels and ui") without actually needing them.

We set `useCustomLauncherManifest: 0` hoping Unity would auto-generate the launcher manifest. **Result:** Same error. Unity's auto-generation doesn't work correctly when `useCustomMainManifest: 1` is also set.

### Attempt 2: Add activity to LauncherManifest.xml
Re-enabled `useCustomLauncherManifest: 1` and added `UnityPlayerGameActivity` with MAIN/LAUNCHER intent filter to `LauncherManifest.xml`. Also removed the intent filter from `AndroidManifest.xml` (library module) to avoid duplicates.

**Result:** Build succeeded but deployment still failed. Discovered this is a **Unity bug** - the deployment checker reads source manifests, not the Gradle-merged result. The APK itself was correct (verified via aapt2).

### Attempt 3: Manual APK install
Installed the APK via adb. App launched but immediately crashed.

**Crash:** `ClassNotFoundException: Didn't find class "com.unity3d.player.UnityPlayerGameActivity"`

This was the real breakthrough. The class didn't exist in the APK because the project was configured for legacy Activity mode.

---

## Issue 4: androidApplicationEntry Mapping (ROOT CAUSE)

**Discovery:** `ProjectSettings.asset` had `androidApplicationEntry: 1`. SoftLaunch also had `1`. We assumed this meant GameActivity. It doesn't.

**Actual mapping (bitmask):**
- `0` = none (error: "At least one application entry has to be selected")
- `1` = Activity (legacy `UnityPlayerActivity`)
- `2` = GameActivity (`UnityPlayerGameActivity`)
- `3` = both

**What this meant:** The project was set to legacy Activity mode, but all manifests referenced `UnityPlayerGameActivity`. Unity only compiles the selected entry point class into the APK, so `UnityPlayerGameActivity` simply didn't exist at runtime.

### The SDK bug
The Palette SDK's `GetExpectedMainActivity()` had the mapping inverted:
```csharp
// WRONG (was):
if (prop != null && prop.intValue == 1)
    return GameActivityClass;  // Thought 1 = GameActivity

// CORRECT (fixed to):
if (prop != null && (prop.intValue & 2) != 0)
    return GameActivityClass;  // 2 = GameActivity (bitmask)
```

This meant the SDK's pre-build sanitizer was actively enforcing the wrong activity class.

### The Facebook SDK complication
Our Facebook SDK fork (`com.lacrearthur.facebook-sdk-for-unity`) has a `FacebookManifestSanitizer` that's an `AssetPostprocessor`. It fires on every import of `AndroidManifest.xml` and forces `UnityPlayerGameActivity` based on `#if UNITY_2023_1_OR_NEWER`. This kept reverting our manual fixes to the manifest.

Unlike the Palette SDK bug, the Facebook SDK's behavior is technically correct for the final state (we switched to GameActivity), but it was fighting us during debugging when we were trying to test with `UnityPlayerActivity`.

### Resolution
Changed `androidApplicationEntry` from `1` to `2` (GameActivity). This aligns everything:
- Unity compiles `UnityPlayerGameActivity` into the APK
- Facebook SDK smart-patch targets `UnityPlayerGameActivity` (matches)
- Palette SDK (with bitmask fix) expects `UnityPlayerGameActivity` (matches)
- Both manifest files reference `UnityPlayerGameActivity` (matches)

**Why switch to GameActivity instead of fixing manifests for Activity?** The studio is on Unity 6, where GameActivity is the default and recommended path. They had `androidApplicationEntry: 1` only because Unity preserves the old value when upgrading from 2022. Keeping legacy Activity would mean fighting both the Facebook SDK patcher and Unity 6's defaults.

---

## Issue 5: SDK Assembly Not Recompiling

After fixing `GetExpectedMainActivity()` in the symlinked SDK package, the old code kept running. The SDK is a symlink (`Packages/com.sorolla.sdk -> /path/to/unity-fastlane-ci/Packages/com.sorolla.sdk`), and editing .cs files through the symlink doesn't always trigger Unity recompilation.

**Attempted fixes:**
- Touching the .asmdef file
- Relaunching Unity
- Creating a script to call `AssetDatabase.ImportAsset` with `ForceUpdate`

**Status:** Recompilation issue was still being resolved when session paused for build testing.

---

## Issue 6: Unity Deployment Checker Bug

Even with correct manifests and correct activity class, Unity's "Build and Run" reports:
```
DeploymentOperationFailedException: No activity in the manifest with action MAIN and category LAUNCHER
```

**Root cause:** Unity's deployment code checks source manifests before Gradle merges them. It sees `android:enabled="false"` on the activity in the library manifest (Unity's own generated manifest does this for the non-selected entry point) and fails, even though the launcher manifest correctly overrides it with `tools:replace="android:enabled"`.

This is a known class of Unity bug, related to the activity-alias deployment fix shipped in 6000.0.63f1. The `enabled="false"` variant is not yet fixed.

**Workaround:** Having the launcher activity with MAIN/LAUNCHER intent filter declared in BOTH `AndroidManifest.xml` and `LauncherManifest.xml` helps Unity's checker find it. The APK produced is correct regardless.

---

## Final File State

### baseProjectTemplate.gradle
- Removed R8 8.1.56 `buildscript` block
- AGP 8.10.0 (auto-upgraded by Unity)

### AndroidManifest.xml (library module)
- `UnityPlayerGameActivity` with MAIN/LAUNCHER intent filter (for Unity's deployment checker)
- Facebook, Adjust activities/receivers/permissions unchanged
- Facebook SDK's `AssetPostprocessor` maintains the activity class on every import

### LauncherManifest.xml (launcher module)
- `UnityPlayerGameActivity` with MAIN/LAUNCHER intent filter
- `android:enabled="true"` with `tools:replace="android:enabled"` (overrides Unity's `enabled="false"`)
- `useCustomLauncherManifest: 1` in ProjectSettings

### ProjectSettings.asset
- `androidApplicationEntry: 2` (GameActivity)
- `useCustomLauncherManifest: 1`
- All SOROLLA_FULL scripting defines preserved

### SDK Changes (Packages/com.sorolla.sdk)

**AndroidManifestSanitizer.cs:**
- Fixed `GetExpectedMainActivity()`: bitmask check `(intValue & 2) != 0` instead of `intValue == 1`
- Added `UsesCustomLauncherManifest()` helper
- Added `DetectLauncherManifestIssue()` - checks for missing/wrong activity in LauncherManifest.xml
- Added `FixLauncherManifest()` - auto-fixes LauncherManifest.xml with correct activity, theme, enabled override

**BuildValidator.cs:**
- `RunAutoFixes()`: added LauncherManifest sanitization after AndroidManifest sanitization
- `CheckAndroidManifest()`: added LauncherManifest validation with error reporting

---

## SDK Bugs Found (Summary)

| # | Component | Bug | Status |
|---|-----------|-----|--------|
| 1 | Palette `GetExpectedMainActivity()` | Bitmask mapping inverted (thought 1=GameActivity) | Fixed |
| 2 | Palette sanitizer | No LauncherManifest.xml awareness | Fixed |
| 3 | Facebook fork `SmartPatch` | Uses `#if UNITY_2023_1_OR_NEWER` instead of PlayerSettings | Not fixed (lower priority - behavior is correct for GameActivity projects) |
| 4 | Palette setup validator | No R8/AGP compatibility check | Not yet implemented |

---

## Documentation Created

- `LEARNINGS.md` - 7 tagged entries + version compatibility matrix
- `Documentation~/troubleshooting.md` - 3 new sections (wrong activity class, LauncherManifest, R8/AGP)
- `Documentation~/guides/android-build-compatibility.md` - comprehensive guide covering all Unity versions
- `Documentation~/internal/2026-03-31-unity6-android-build-debugging.md` - this handoff document

---

## Still Pending

1. **Confirm Android build launches correctly** - build succeeds, deployment checker may still complain, APK should be correct
2. **iOS build** - not attempted yet
3. **Commit SDK changes** to the SDK repo (unity-fastlane-ci)
4. **Verify SDK recompilation** - the bitmask fix and LauncherManifest support need to be confirmed active in Unity
5. **Clean up** - remove `Assets/Editor/ReimportSDK.cs` (temporary script)
6. **Decide on Facebook SDK fix** - whether to update `FacebookManifestSanitizer` to read PlayerSettings instead of compile-time check

---

## Key Lessons

1. **Verify enum/bitmask values empirically.** The `androidApplicationEntry` mapping cost the entire session because three systems (Unity, Palette SDK, Facebook SDK) each assumed different mappings.

2. **When a file edit keeps reverting, find the patcher.** `grep OnPostprocessAllAssets` across all Editor scripts. AssetPostprocessors fire on every import and will always get the last word.

3. **Unity's deployment checker is not authoritative.** It reads source manifests, not the merged result. A failing deployment check doesn't mean the APK is wrong.

4. **Symlinked packages don't reliably trigger recompilation.** After editing SDK code through a symlink, verify the change took effect before debugging further.

5. **Unity version upgrades preserve old settings.** `androidApplicationEntry: 1` (Activity) survives the Unity 2022 -> 6 upgrade even though Unity 6 defaults to GameActivity for new projects. This silent mismatch is the root of many post-upgrade issues.
