# Sorolla SDK - Development Log (Agentic Hindsight)

> **Purpose**: Track validated learnings and insights for future AI agents
> **Format**: Reverse chronological (newest first)

---

## Quick Reference: Critical Learnings

These are the most important validated learnings. Read these first.

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

## Agent Instructions

**When to add entries**:
- After validating a fix that corrects previous mistakes
- When discovering non-obvious Unity behavior
- After fixing bugs (document root cause)

**Entry guidelines**:
- Only add VALIDATED learnings (tested, confirmed working)
- Focus on "what future me needs to know"
- Include official documentation sources when possible
- Delete/update entries that are proven wrong
