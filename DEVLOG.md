# Sorolla SDK - Development Log

> **Purpose**: Validated learnings, architectural decisions, debugging insights.
> **Format**: Reverse chronological (newest first)

---

## Recent Session Learnings

### 2026-04-07 - v3.7.1 GA4 spec audit (Firebase adapter)

**Summary:** Audited `FirebaseAdapterImpl.cs` against the GA4 protocol reference (https://firebase.google.com/docs/reference/cpp/group/event-names) and found a P0 plus a chain of P1s. Fixed all of them in a single Firebase-only patch with no `Palette` API surface change.

**What was wrong:**

1. **`level_fail` is not a GA4 event.** The Firebase progression mapping fired a literal `"level_fail"` for failed levels. GA4's built-in Games > Levels report only aggregates `level_end` - so every failed attempt was invisible in reports and the funnel falsely looked like every started level completed. This bug shipped in v3.6.0 (the "added level_fail" line in the v3.6.0 changelog was the regression).
2. **`purchase` had no `items[]`.** Firebase's `purchase` event needs an items array to populate the Monetization > In-app purchases (aka "Ecommerce purchases") report. We were passing `ParameterItemID` as a scalar top-level param. Total revenue still flowed but per-product breakdowns (which IAP pack sells best, ARPDAU-by-pack, LTV-by-pack) were empty.
3. **`level_end` had no `success` param.** Even when complete fired correctly, GA4 had no way to distinguish complete from fail at the report level. Symptom of the same root cause as #1.
4. **Score on `level_end` is non-canonical.** GA4's `post_score` is its own event. Studios that need score-with-context join via session_id in BigQuery.
5. **String literals everywhere.** `ad_impression`, `item_name`, `ad_platform`, etc. were string literals instead of `FirebaseAnalytics.Event*` / `Parameter*` constants. One typo away from a silent drop.
6. **No client-side validation on user properties or reserved event names.** Firebase silently rejects names > 24 chars, names with `ga_`/`google_`/`firebase_` prefixes, and reserved event names like `session_start`. The SDK was forwarding these straight through and getting them dropped server-side with no studio-facing signal.

**Fixes (all in `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs`):**
- Failed levels now fire `level_end` with `success=0`. Complete fires `success=1`. `level_fail` is gone.
- `TrackPurchase` builds an `IDictionary<string,object>[]` items array with `item_id = productId` and passes it via `ParameterItems`.
- Score is no longer attached to `level_start`/`level_end`. After the level event, if score > 0 and status != start, we fire a separate `post_score` event with `ParameterScore`.
- All literals replaced with `FirebaseAnalytics.*` constants.
- `SetUserProperty` validates name length (≤24), reserved prefixes (`ga_`/`google_`/`firebase_`/`_`), value length (truncate at 36).
- `SanitizeEventName` extended with a static `HashSet<string>` of GA4 reserved event names + reserved-prefix check. Returns null with a `LogWarning` so existing callers' null-guard early-returns.

**Key insight:** the Firebase Unity `Parameter` ctor for items arrays takes `IEnumerable<IDictionary<string,object>>`. Use `new IDictionary<string,object>[] { new Dictionary<string,object> { ... } }` - C# array → IEnumerable inference handles it. The single-dict overload `(string, IDictionary<string,object>)` doesn't conflict because an array isn't a dict.

**Why no `Palette.cs` change:** All validation lives in the adapter for locality with the rest of the sanitize/build helpers. `Palette.TrackProgression` / `TrackPurchase` / `SetUserProperty` signatures stay byte-identical. Studio code is unchanged. Only the wire shape on the GA4 side moves.

**Migration impact for studios:** Custom dashboards keyed on `level_fail` need to switch to `level_end` filtered by `success=0`. BigQuery readers pulling `score` off `level_end` need to read it from the matching `post_score` row in the same session. Documented in CHANGELOG.md.

**Out of scope (deferred):** New `Palette` surface for tutorial phases, level-up, unlock-achievement. Confirmed with Arthur: hypercasual mobile games don't use `level_up` / `unlock_achievement`, and the built-in `tutorial_begin`/`tutorial_complete` aren't granular enough - studios should use `Palette.TrackEvent("tutorial_phase", { phase, status })` as a custom event.

### 2026-04-01 - v3.6.0 Release: Firebase androidlib + asmdef defineConstraints

**Summary:** Merged feat/tiktok into master, released v3.6.0. Two bugs surfaced when Sweep Collector switched from local symlink to UPM git URL.

**Bug 1: Firebase asmdef `defineConstraints` OR syntax**
- `Sorolla.Adapters.Firebase.asmdef` had `"FIREBASE_ANALYTICS_INSTALLED || FIREBASE_CRASHLYTICS_INSTALLED || FIREBASE_REMOTE_CONFIG_INSTALLED"`
- Unity `defineConstraints` does NOT support `||` in a single string - it silently treats unrecognized expressions as always-true
- With local symlink, Firebase DLLs were always discoverable so it compiled fine even though the constraint was broken
- Via UPM git URL without Firebase installed: CS0246 errors because the assembly compiled but couldn't find Firebase types
- **Fix:** Single constraint `"FIREBASE_ANALYTICS_INSTALLED"` (all Firebase packages are always installed as a bundle)

**Bug 2: Firebase Android crash - missing androidlib folders**
- `Firebase.Editor.dll` auto-generates `Assets/Plugins/Android/FirebaseApp.androidlib/` on domain reload
- Contains `res/values/google-services.xml` (processed from `google-services.json`)
- Also generates `FirebaseCrashlytics.androidlib/` and `Assets/GeneratedLocalRepo/`
- Without these, Android crash at launch: "Default FirebaseApp failed to initialize because no default options were found"
- The `**APPLY_PLUGINS**` placeholder in mainTemplate.gradle resolves to empty without these
- **Fix:** Domain reload in Unity after Firebase packages resolve, then Force Resolve if needed

**Root cause for both:** Testing with local symlinks masks issues that only surface with real UPM resolution. Always test package changes via git URL, not just symlink.

### 2026-03-30 - Firebase Event Remapping + Unified Purchase Tracking

**Summary:** Remapped Firebase analytics events from GA-style names to GA4 official game events. Added `Palette.TrackPurchase()` to unify attribution calls.

**What Changed:**
- `FirebaseAdapterImpl.TrackProgressionEvent`: `"progression"` → `EventLevelStart`/`EventLevelEnd`, `progression_01` → `ParameterLevelName`, added `success` param
- `FirebaseAdapterImpl.TrackResourceEvent`: `"resource_flow"` → `EventEarnVirtualCurrency`/`EventSpendVirtualCurrency`, params remapped to GA4 schema
- `Palette.TrackPurchase(amount, currency)`: New unified method fans out to Adjust, TikTok, Firebase
- `SorollaConfig.adjustPurchaseEventToken`: New config field for Adjust revenue event token
- Consolidated test controllers into SDK DebugUI sample (deleted standalone AdjustTestController, TikTokTestController)
- Added `TikTokCardController` to DebugUI, added `TikTok` to `LogSource` enum

**Key Decisions:**
- Use `FirebaseAnalytics.Event*`/`Parameter*` constants instead of string literals - ensures forward compat with GA4
- GA-specific params (itemType/itemId split) dropped on Firebase side - GA4 only has `item_name`
- Purchase attribution stays opt-in per adapter (Adjust requires event token in config, TikTok requires app ID)
- `level_category`/`level_subcategory` are custom params (no GA4 equivalent for progression_02/03)

**Gotcha:** Always check GA4's official recommended game events before inventing custom event names. We shipped `earn_item`/`spend_item` then had to fix to `earn_virtual_currency`/`spend_virtual_currency`.

### 2026-02-09 - Firebase 13.7.0 Upgrade + EDM4U Gradle/Java Incompatibility

**Summary:** Upgraded Firebase UPM fork from 12.10.1 → 13.7.0. EDM4U Android resolver fails on first run due to Gradle/Java version mismatch.

**What Changed (Firebase fork):**
- Repo: `https://github.com/LaCreArthur/unity-firebase-app` tag `13.7.0`
- FirebaseApp, Analytics, Crashlytics, RemoteConfig binaries updated
- EDM4U updated 1.2.185 → 1.2.187
- New: `Firebase.Crashlytics.Editor.dll` (new in 13.7.0)
- Removed deprecated `fb_dynamic_links` and `fb_invites` icons
- `.gitattributes` cleaned up (added `*.srcaar`, `*.pdb`, `*.exe` LFS patterns)
- SdkRegistry: `FIREBASE_VERSION` → `"13.7.0"`, `EDM_VERSION` → `"1.2.187"`

**EDM4U Gradle/Java Bug — Root Cause Chain:**
```
EDM4U Android Resolve triggers
  → Generates Gradle project in Temp/PlayServicesResolverGradle/
    → gradle-wrapper.properties hardcodes Gradle 5.1.1 (embedded in Google.JarResolver.dll)
      → gradlew picks up system Java via JAVA_HOME or PATH
        → Gradle 5.1.1 uses Groovy 2.5.x which requires Java 8-12
          → Java 17+ → NoClassDefFoundError: org.codehaus.groovy.vmplugin.v7.Java7
```

**Key facts:**
- Gradle 5.1.1 supports Java 8-12 only
- Gradle 7.3+ supports Java 17, Gradle 8.5+ supports Java 21
- The Gradle version is **hardcoded inside Google.JarResolver.dll** — cannot be patched via text files
- System Java (Homebrew OpenJDK 21) or Unity 6 bundled JDK (17+) both trigger this
- This is a **Google/EDM4U upstream bug** — they never updated for Java 17+

**What actually happens (validated 2026-02-09):**
1. First run: EDM4U tries bundled Gradle 5.1.1 → fails with Java 21
2. Second run (restart Unity): EDM4U falls back to **Gradle template mode**
3. Instead of downloading .aar files, it writes dependency declarations into `Assets/Plugins/Android/mainTemplate.gradle`
4. Actual Maven downloads happen at Android build time via Unity's own Gradle (which is Java-compatible)
5. This means dependencies resolve at build time, not in Editor — Editor uses the x86_64 binaries from the UPM packages

**Result:** The first-run Gradle error is cosmetic — EDM4U auto-recovers on restart by switching strategy. No manual fix needed.

**Files generated by Gradle template mode:**
- `Assets/Plugins/Android/mainTemplate.gradle` — dependency declarations
- `Assets/Plugins/Android/gradleTemplate.properties`
- `Assets/Plugins/Android/settingsTemplate.gradle`

**Reproduction:**
1. Import Firebase 13.7.0 packages into a Unity 6 project
2. EDM4U auto-resolves Android dependencies → Gradle 5.1.1 crashes with Java7 NoClassDefFoundError
3. Restart Unity → EDM4U switches to Gradle template mode → succeeds

**iOS minimum deployment target:** Firebase 13.x requires iOS 15.0+

### 2026-01-26 - Facebook SDK Now Core (Always Installed)

**Summary:** Changed Facebook SDK from `PrototypeOnly` to `Core` requirement.

**What Changed:**
- `SdkRegistry.cs`: Facebook requirement `PrototypeOnly` → `Core`
- `Palette.cs`: Facebook initializes in ALL modes (removed `if (isPrototype)` check)
- `AndroidManifestSanitizer.cs`: Removed Facebook from orphan cleanup patterns (no longer uninstalled)
- Updated mode documentation across README, CLAUDE.md, architecture.md, SorollaConfig, SorollaSettings

**Rationale:**
- Facebook provides valuable attribution data in both modes
- Simplifies codebase (fewer conditional paths)
- No downside to having Facebook always on

**Mode Summary (Post-Change):**
| Mode | Core SDKs |
|------|-----------|
| Prototype | GameAnalytics, Facebook, Firebase |
| Full | GameAnalytics, Facebook, MAX, Adjust, Firebase |

---

### 2026-01-21 - DontDestroyOnLoad Fix & KISS Reminder

**Problem Solved:** hungrysnake UIManager assertion failure during initialization

**Solution Implemented:**
1. Triple-defense `MakePersistent()` in SorollaBootstrapper
2. `[DefaultExecutionOrder(-1000)]` attribute for timing guarantee

**Key Learning:** Initially created 105-line auto-setup Editor script before realizing Unity's built-in `[DefaultExecutionOrder]` attribute was sufficient. **Overengineered due to defensive "what if" thinking instead of KISS.**

**Commits:**
- b670230: Added MakePersistent() + auto-setup script
- 635c97d: Removed auto-setup script (overengineered)
- 1c77409: Updated DEVLOG with DefaultExecutionOrder

**Takeaway:** Always ask "what's the simplest solution?" before coding. Unity built-ins exist for a reason.

---

## Quick Reference: Critical Learnings

These are the most important validated learnings. Read these first.

### DontDestroyOnLoad Conflicts (Unity 6+)
```
Problem: Assertion failure when client MonoSingletons call DontDestroyOnLoad during BeforeSceneLoad
Solution: Triple-defense in SorollaBootstrapper.MakePersistent()
  1. Check HideFlags.DontSave before calling DontDestroyOnLoad
  2. Verify scene.IsValid() && scene.isLoaded
  3. Try-catch as final safety net
  4. [DefaultExecutionOrder(-1000)] to initialize before client code
Result: SDK works at all costs, no silent failures, no user intervention
```

### Unity Assembly Definitions (asmdef)
```
versionDefines: PER-ASSEMBLY only (NOT project-wide!)
defineConstraints: Required to prevent compilation when SDK missing
Both are needed together for optional SDK assemblies
```

### IL2CPP Code Stripping (Packages)
```
link.xml: NOT auto-included from UPM packages - must be in Assets/
[AlwaysLinkAssembly]: Forces linker to PROCESS (not preserve)
[Preserve]: Actually marks code as roots
Both needed for [RuntimeInitializeOnLoadMethod] in packages
```

### The Correct asmdef Pattern
```json
{
    "defineConstraints": ["APPLOVIN_MAX_INSTALLED"],
    "versionDefines": [
        {
            "name": "com.applovin.mediation.ads",
            "expression": "",
            "define": "APPLOVIN_MAX_INSTALLED"
        }
    ]
}
```

### EDM4U Gradle Compatibility (Unity 6+)
```
EDM4U bundles Gradle 5.1.1 → incompatible with Java 17+ (Unity 6 default)
First resolution may fail, but works after selecting mode and re-resolving
Auto-config runs too late to catch initial EDM4U resolution
```

### MAX SDK Key Location
```
SDK key is in AppLovinSettings (Integration Manager), NOT SorollaConfig
MaxSdk.SetSdkKey() is deprecated - SDK reads from settings automatically
Use reflection to access: Type.GetType("AppLovinSettings, MaxSdk.Scripts.IntegrationManager.Editor")
```

### Namespace Translation (Legacy References)
```
When reading older docs/context, mentally translate:
SorollaSDK. → Palette.
using Sorolla; → using Sorolla.Palette;
SorollaSDK.cs → Palette.cs
```

### Firebase Git Package Installation
```
Multiple packages from same git repo cause "Directory not empty" errors
UPM clones same repo multiple times simultaneously → race condition
Solution: Clear Library/PackageCache/.tmp* and com.google.firebase* before install
```

### Android Build System (Unity 6)

**R8 version pins**: R8 8.1.56 pin needed for AGP 7.4.2 (Unity 2022) crashes AGP 8.10.0 (Unity 6) with `setBuildMetadataConsumer` NoSuchMethodError. AGP 8.10.0's bundled R8 handles Kotlin 2.0 metadata natively - remove the pin after upgrading.

**androidApplicationEntry bitmask**: In ProjectSettings.asset, `1` = Activity (legacy), `2` = GameActivity, `3` = both. Unity 2022 only has Activity. Unity 6 defaults to GameActivity for new projects but preserves Activity when upgrading.

**Split Gradle modules (Unity 6)**: When `useCustomLauncherManifest: 1`, `AndroidManifest.xml` goes to unityLibrary module, `LauncherManifest.xml` goes to launcher. The launcher manifest must override with `enabled="true"` + `tools:replace="android:enabled"`.

**Deployment checker vs Gradle merge**: Unity 6's deployment checker reads source manifests, not the Gradle-merged result. Launcher activity must be in BOTH AndroidManifest.xml and LauncherManifest.xml to avoid `DeploymentOperationFailedException`.

**Facebook manifest sanitizer**: `FacebookManifestSanitizer` fires on every AndroidManifest.xml import via AssetPostprocessor. Uses compile-time version checks instead of reading `androidApplicationEntry`. Fights any manual manifest fix. Must read PlayerSettings instead.

**Manifest patcher conflicts**: Multiple patchers (Palette, Facebook, Unity auto-gen) fight each other. When a manifest edit keeps reverting, check for AssetPostprocessors: `grep OnPostprocessAllAssets`.

**Unity version compatibility matrix**:

| Unity Version | AGP | R8 Pin Needed | Activity Class | androidApplicationEntry |
|--------------|-----|---------------|----------------|------------------------|
| 2022.3 LTS | 7.4.2 | Yes (8.1.56+) for Kotlin 2.0 libs | UnityPlayerActivity | 1 (only option) |
| 2023.1-2023.2 | 7.x-8.x | Depends on AGP version | Both available | 1=Activity, 2=GameActivity |
| 6000.x (Unity 6) | 8.10.0 | No - bundled R8 is sufficient | Both, GameActivity default | 1=Activity, 2=GameActivity |

---

## 2026-01-20: Firebase Cache Cleanup Fix

**Summary**: Added automatic cache cleanup before Firebase installation to prevent "Directory not empty" errors.

**The Problem**:
Firebase uses 4 packages from the same git repo with different `?path=` parameters:
```json
"com.google.firebase.analytics": "...unity-firebase-app.git?path=FirebaseAnalytics#12.10.1"
"com.google.firebase.app": "...unity-firebase-app.git?path=FirebaseApp#12.10.1"
```

Unity Package Manager clones the same repo multiple times simultaneously, creating race conditions:
- Temporary directories conflict (`Library/PackageCache/.tmp-*`)
- Incomplete checkouts leave stale directories blocking subsequent installations

This manifests as `ENOTEMPTY: directory not empty` errors (known Unity 6 bug).

**Solution**:
Clear stale cache before Firebase installation in `SdkInstaller.cs`:
- `.tmp*` directories (failed checkouts)
- `com.google.firebase*` directories (forces fresh clone)

Non-fatal on failure - installation continues even if cleanup fails.

**Documentation Sources**:
- [Unity Manual: Git dependencies](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-git.html)
- [Unity Forum: ENOTEMPTY error](https://discussions.unity.com/t/error-while-resolving-packages-enoempty-and-einval/942440)

**Files Changed**:
- `Editor/Sdk/SdkInstaller.cs`: Added `ClearFirebasePackageCache()`

---

## 2026-01-13: v3.1.0 - Firebase Mandatory

**Summary**: Made Firebase required in all modes (Prototype + Full) with zero-friction upgrade path.

**Changes**:
- `SdkRegistry.cs`: Changed all 4 Firebase packages from `Optional` to `Core`
- `SorollaSetup.cs`: Bumped SetupVersion to v7, added `FirebaseMigrationKey` for one-time popup
- `MigrationPopup.cs`: New EditorWindow guides users through Firebase setup
- `package.json`: Version 3.0.0 → 3.1.0

**Follow-up fix**: Removed stale "optional" Firebase references from:
- `SorollaWindow.cs`: UI now shows red ✗ when Firebase not installed
- `README.md`: Mode tables updated
- `BuildValidator.cs`: "No Firebase" now shows as Warning
- `SorollaConfig.cs`: Header attributes no longer say "(Optional)"
- `CLAUDE.md`: Mode system table updated

**Learnings**:
- SetupVersion bump triggers full setup for existing users (installs new Core packages)
- Separate migration key needed for one-time UI (separate from setup key)
- EditorWindow.ShowUtility() creates modal-like behavior without blocking
- When changing requirement levels, grep for "optional" mentions across entire codebase

---

## 2026-01-09: SDK Simplification - Complete

**Summary**: Completed all 11 tasks in the simplification plan.

**Bug Fixes**:
- `Palette.cs:202`: `s_config` → `Config` (undefined variable)
- `SorollaTestingTools.cs:18`: `v3` → `v6` (stale EditorPrefs key)

**Dead Code Removed**:
- `SorollaConfig.IsValid()` - no callers
- `SdkConfigDetector.GetCrashlyticsStatus()` - no callers
- `SdkConfigDetector.GetRemoteConfigStatus()` - no callers

**Deduplication**:
- `SorollaWindow.cs`: Added `SdkRowData` struct + `DrawSdkRow()` helper
- `SdkInstaller.cs`: Extracted `EnsureMaxRegistry()` for MAX registry setup
- `MaxSettingsSanitizer.cs`: Cached `AppLovinSettings` Type lookup

**Files Merged/Deleted**:
- `SorollaMode.cs` → merged into `SorollaSettings.cs` (deleted)
- `BuildValidationWindow.cs` → merged into `SorollaWindow.cs` (deleted)
- `FirebaseCoreManager.cs` → merged into `FirebaseAdapter.cs` (deleted)

**Cleanup**:
- Trimmed verbose comments in 3 AssemblyInfo.cs files

---

## 2026-01-08: Remove deprecated MaxSdk.SetSdkKey()

**Summary**: SDK key now lives in AppLovinSettings (Integration Manager), not SorollaConfig.

**Changes**:
- Removed `maxSdkKey` from `SorollaConfig.cs`
- Removed `sdkKey` parameter from `MaxAdapter.Initialize()` and `IMaxAdapter`
- Added SDK key helpers to `MaxSettingsSanitizer.cs`: `GetSdkKey()`, `IsSdkKeyConfigured()`
- Added SDK key validation to `BuildValidator.CheckMaxSettings()`

**Learnings**:
- `MaxSdk.SetSdkKey()` deprecated - SDK reads from `AppLovinSettings.Instance.SdkKey`
- `AppLovinSettings` is unnamespaced, requires reflection to access
- Must search all loaded assemblies as fallback (assembly name varies by MAX SDK version)
- Unity meta files MUST be committed with new .cs files - missing meta = compile errors

---

## 2026-01-06: Namespace Refactor & Documentation Consolidation

**Summary**:
Major internal documentation update reflecting v2.3.3 changes.

**Key Changes**:
1. **Namespace refactored**: `SorollaSDK` class → `Palette` class, `using Sorolla;` → `using Sorolla.Palette;`
2. **Facebook SDK**: Required for prototype mode UA campaigns
3. **GDPR/UMP complete**: Full consent flow via MAX SDK (CmpService)
4. **Debug UI moved**: Now a UPM sample (`Samples~/DebugUI`)

**Documentation Consolidated**:
- Removed `plan.md` (outdated task tracking)
- Removed `max-mediation-plan.md` (session-specific, paused)
- Updated all internal docs to use correct `Palette` namespace
- Updated feature matrices to reflect completed GDPR/UMP
- ADRs moved to `product-roadmap.md`

**Files Changed**:
- All internal documentation files updated
- Version references updated to 2.3.3

---

## 2025-12-29: EDM4U Gradle Java 17+ (Partial Fix)

**Summary**:
Auto-configure EDM4U to use Unity's Gradle templates. Note: Initial resolution may still fail.

**The Problem**:
On Unity 6+ (Java 17+ default), EDM4U's first Android resolution may fail:
```
java.lang.NoClassDefFoundError: Could not initialize class org.codehaus.groovy.vmplugin.v7.Java7
```

**Root Cause**:
EDM4U bundles Gradle 5.1.1 which doesn't support Java 17+. EDM4U triggers resolution immediately on import, before our setup code runs.

**Partial Solution**:
Auto-configure EDM4U via reflection in `SorollaSetup.cs`:
```csharp
GooglePlayServices.SettingsDialog.PatchMainTemplateGradle = true;
GooglePlayServices.SettingsDialog.PatchPropertiesTemplateGradle = true;
GooglePlayServices.SettingsDialog.PatchSettingsTemplateGradle = true;
```

**Actual Behavior**:
1. First import → EDM4U resolves before setup → may fail with Java error
2. User selects mode → triggers re-resolution → works (settings now applied)
3. Acceptable UX - error is transient and self-resolves

---

## 2025-12-29: Assembly Definition Pattern Validated

**Summary**:
After multiple iterations and a broken release (v2.3.2), the correct pattern for optional SDK assemblies is now validated.

**The Problem**:
Fresh imports without SDKs installed caused CS0246 errors:
```
error CS0246: The type or namespace name 'Firebase' could not be found
```

**Root Cause**:
Unity compiles C# files **before** checking if referenced assemblies exist. Without `defineConstraints`, Unity attempts to compile implementation files even when the SDK isn't installed.

**The Solution**:
Each implementation asmdef needs BOTH:
1. `versionDefines` - detects package presence, sets symbol (per-assembly only!)
2. `defineConstraints` - prevents compilation if symbol not set

**Key Documentation Finding**:
> "Symbols defined in the Assembly Definition are only in scope for the scripts in the assembly created for that definition."
> — Unity Manual: Conditionally including assemblies

**Hindsight Insights**:
- `versionDefines` are **PER-ASSEMBLY**, not project-wide
- "Assembly references as constraints" does NOT work - that was a bad theory
- Always test fresh imports WITHOUT optional SDKs to verify compilation

---

## 2025-12-26: IL2CPP Stripping Protection (Unity 6)

**Summary**:
Implemented code preservation strategy for packages using `[RuntimeInitializeOnLoadMethod]`.

**Key Findings from Unity Docs**:

1. **link.xml in packages**: NOT auto-included by Unity. Must be in `Assets/` folder.
2. **[AlwaysLinkAssembly]**: Forces linker to **process** assembly, but doesn't preserve it.
3. **[Preserve]**: Actually marks code as roots and prevents stripping.

**Implementation**:
```csharp
// AssemblyInfo.cs in each impl assembly
[assembly: AlwaysLinkAssembly]

// On implementation class
[Preserve]
internal class MaxAdapterImpl : IMaxAdapter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    [Preserve]
    private static void Register() { ... }
}
```

**Protection Strategy (3 layers)**:
1. `[assembly: AlwaysLinkAssembly]` - Forces linker to process assembly
2. `[Preserve]` attributes - Marks specific code as roots
3. `link.xml` (auto-copied to Assets/) - Fallback manual override

**UMP/GDPR**: MAX SDK handles UMP automatically via `CmpService`. Tested working on Android.

**Files Created**:
- `Runtime/Adapters/MAX/AssemblyInfo.cs`
- `Runtime/Adapters/Adjust/AssemblyInfo.cs`
- `Runtime/Adapters/Firebase/AssemblyInfo.cs`

---

## 2025-12-24: Stub + Implementation Pattern (v2.3.0)

**Summary**:
Refactored optional SDK adapters to handle Unity's assembly resolution order.

**The Problem**:
Unity resolves assembly references BEFORE evaluating `#if` preprocessor blocks:
```csharp
#if FIREBASE_INSTALLED
using Firebase;  // Unity tries to resolve this even if false!
#endif
```

**The Solution**: Stub + Implementation pattern with separate assemblies.

**Structure**:
```
Adapters/
├── Sorolla.Adapters.asmdef      (no external refs - always compiles)
├── MaxAdapter.cs                 (stub → delegates to impl)
├── MAX/
│   ├── Sorolla.Adapters.MAX.asmdef  (defineConstraints + versionDefines)
│   ├── AssemblyInfo.cs              ([AlwaysLinkAssembly])
│   └── MaxAdapterImpl.cs            (registers at runtime)
```

**How It Works**:
1. SDK not installed → `versionDefines` not met → `defineConstraints` fail → assembly skipped
2. SDK installed → assembly compiles → `[RuntimeInitializeOnLoadMethod]` registers impl
3. At runtime → stub delegates to impl if registered

**Hindsight Insights**:
- Use Stub + Impl pattern for all optional SDK adapters
- `defineConstraints` can use `||` for OR logic
- Interface must be `internal` so users only see public static class
- Events need forwarding from impl to stub

---

## 2025-12-18: Pain Points & UMP Priority

**Summary**:
Analyzed developer pain points and prioritized GDPR/UMP as critical gap.

**Key Findings**:
- Google UMP deadline was Jan 2024
- MAX SDK automates UMP via CmpService (no manual integration needed)
- Firebase 12.x still compatible, 13.x available

**UMP Status**: Complete (MAX handles automatically)

---

## 2025-12-01: Firebase Integration (v2.1.0)

**Key Learnings**:
- Firebase requires `CheckAndFixDependenciesAsync` before use
- Remote config needs explicit `FetchAndActivate` call
- Config files MUST match bundle ID exactly
- Fallback chain: Firebase → GA → default

---

## 2025-11-26: SDK Installation (v2.0.1)

**Key Learnings**:
- Direct manifest.json editing more reliable than PackageManager API
- Add scoped registries before dependencies
- Installation order: EDM → GA → platform SDKs
- `SdkRegistry.cs` is single source of truth for versions

---

## 2025-11-10: Initial Release (v1.0.0)

**Key Learnings**:
- ATT must happen before SDK init for best ad fill
- Facebook requires App ID AND Client Token
- MAX SDK key is account-level, ad units are app-level
- Prototype mode = no attribution, Full mode = production

---

## How to Use This Log

- Add entries after validating a fix or discovering non-obvious behavior
- Focus on "what would I need to know if I hit this again?"
- Include official documentation sources when possible
- Update or remove entries that are proven wrong
