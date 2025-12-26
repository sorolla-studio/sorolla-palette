# Sorolla SDK - Development Log (Agentic Hindsight)

> **Purpose**: Track changes, learnings, and insights for future AI agents
> **Format**: Reverse chronological (newest first)

---

## Entry Template

```markdown
## YYYY-MM-DD: [Brief Title]

**Changes**:
- What was modified

**Learnings**:
- What was discovered

**Hindsight Insights**:
- What future agents should know

**Metrics** (optional):
- Performance/token impact
```

---

## 2025-12-26: Auto-Copy link.xml to Assets/

**Summary**:
Added auto-copy of link.xml from package to Assets/ folder on setup, since Unity does NOT auto-include link.xml from UPM packages.

**Changes**:
- `Editor/SorollaSetup.cs`: Added `CopyLinkXmlToAssets()` method
- Bumped SetupVersion to "v5" to trigger on existing installs
- Copies to `Assets/Sorolla.link.xml` (named to avoid conflicts)
- Skips if file already exists (preserves user customizations)

**Why**:
Research confirmed that Unity only processes `link.xml` files from `Assets/` folder. While `[AlwaysLinkAssembly]` + `[Preserve]` should be sufficient, the link.xml provides fallback protection for:
- High stripping level edge cases
- Future Unity versions
- Users who disabled attributes accidentally

**Protection Strategy (3 layers)**:
1. `[assembly: AlwaysLinkAssembly]` - Forces linker to process assembly
2. `[Preserve]` attributes - Marks specific code as roots
3. `link.xml` (auto-copied) - Fallback manual override

**Files Modified**:
- `Editor/SorollaSetup.cs` - Added CopyLinkXmlToAssets()
- `Runtime/link.xml` - Updated documentation

---

## 2025-12-26: Unity 6 Best Practices & UMP Complete ✅

**Summary**:
Researched Unity 6.3 LTS best practices for IL2CPP stripping and code preservation. Implemented `[assembly: AlwaysLinkAssembly]` - the official Unity recommendation for packages using `[RuntimeInitializeOnLoadMethod]`. UMP integration confirmed working.

**Changes**:
- Added `AssemblyInfo.cs` with `[assembly: AlwaysLinkAssembly]` to each implementation assembly:
  - `Runtime/Adapters/MAX/AssemblyInfo.cs`
  - `Runtime/Adapters/Adjust/AssemblyInfo.cs`
  - `Runtime/Adapters/Firebase/AssemblyInfo.cs`
- Updated `Runtime/link.xml` with `ignoreIfMissing="1"` for optional adapter assemblies
- Deleted duplicate `Assets/link.xml` (was confusing for external developers)

**Research Findings**:

1. **link.xml in UPM Packages Does NOT Auto-Include**
   - Unity does NOT automatically pick up `link.xml` from UPM packages
   - Must be in `Assets/` folder to be processed
   - Source: [Unity Docs](https://docs.unity3d.com/6000.2/Documentation/Manual/managed-code-stripping-preserving.html)

2. **AlwaysLinkAssembly is the Solution**
   - Official Unity attribute for packages with `[RuntimeInitializeOnLoadMethod]`
   - Forces linker to process assembly even if no types directly referenced
   - Source: [Unity Scripting API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Scripting.AlwaysLinkAssemblyAttribute.html)

3. **ignoreIfMissing for Optional Assemblies**
   - Use `ignoreIfMissing="1"` in link.xml for assemblies that may not exist
   - Prevents build errors when optional SDKs not installed
   - Source: [Link XML Reference](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-xml-formatting.html)

**Code Preservation Strategy (Belt & Suspenders)**:
```
1. [assembly: AlwaysLinkAssembly]  → Forces linker to process assembly
2. [Preserve] on class             → Marks class as root
3. [Preserve] on Register()        → Marks method as root
4. link.xml (fallback)             → Manual override if needed
```

**UMP/GDPR Status**: ✅ **COMPLETE**
- Tested on Android builds - UMP popup appears correctly
- MAX SDK handles UMP automatically via `CmpService`
- Consent status properly detected and propagated

**FakeCMPDialog/FakeATTDialog Clarification**:
- These are **NOT deprecated** - they're essential for Editor testing
- Real UMP/ATT only work in actual device builds
- Used by `ContextScreenView.cs` and DebugUI sample

**Hindsight Insights**:
- **CRITICAL**: Use `[assembly: AlwaysLinkAssembly]` for all package assemblies with `[RuntimeInitializeOnLoadMethod]`
- **CRITICAL**: Don't rely on `link.xml` in packages - it won't be auto-included
- `[Preserve]` attribute alone may not be enough in High stripping mode
- The `ignoreIfMissing` attribute prevents errors for optional assemblies
- Keep Fake dialogs for Editor testing - they serve a real purpose

**Files Modified**:
- `Runtime/Adapters/MAX/AssemblyInfo.cs` - created
- `Runtime/Adapters/Adjust/AssemblyInfo.cs` - created
- `Runtime/Adapters/Firebase/AssemblyInfo.cs` - created
- `Runtime/link.xml` - updated with ignoreIfMissing and documentation

**Status**: ✅ Architecture is Unity 6 compliant and future-proof

---

## 2025-12-26: Adapter Architecture Fix Verified ✅

**Summary**:
Full Android build verification confirmed all SDK adapters (MAX, Adjust, Firebase) now register and initialize correctly.

**Verified Behavior**:
```
[Sorolla:MAX] Register() called - assembly is loaded!
[Sorolla:Adjust] Register() called - assembly is loaded!
[Sorolla:Firebase] Register() called - assembly is loaded!
[Sorolla:MAX] Initialized
[Sorolla:MAX] ConsentStatus: Obtained (Geography: Gdpr)
[Sorolla:Adjust] Initializing (Sandbox)...
[Sorolla:Adjust] Initialized
```

**Architecture Confirmed**:
The Stub + Implementation pattern now works correctly:
1. **Stubs** (`MaxAdapter.cs`, etc.) - No external refs, always compile
2. **Implementations** (`MaxAdapterImpl.cs`, etc.) - Reference SDK assemblies, compile only when SDK installed
3. **Registration** - `[RuntimeInitializeOnLoadMethod]` auto-registers impl at runtime
4. **Stripping Protection** - `[Preserve]` + `link.xml` prevent IL2CPP removal

**Key Takeaway**:
`defineConstraints` was the problem, not the solution. Assembly references provide natural conditional compilation - if the referenced assembly doesn't exist, Unity simply doesn't compile the dependent assembly.

**Production Ready**: Yes - all adapters working in Android IL2CPP builds.

---

## 2025-12-26: defineConstraints Removed - Assembly References ARE the Constraint

**Problem**:
MAX/Firebase/Adjust adapters showed `[Sorolla:X] Not installed` in Android builds despite being installed.

**Investigation Journey**:

1. **Initial hypothesis (WRONG)**: `versionDefines` don't propagate between asmdefs

2. **Second hypothesis (PARTIALLY CORRECT)**: `defineConstraints` check global scripting define symbols, not asmdef `versionDefines`
   - Added `DefineSymbols.cs` to set global defines from packages
   - Defines were added correctly ✓
   - Still failed in Android builds ✗

3. **Real Root Cause**: The build process was still not correctly evaluating `defineConstraints` at build time, even with global defines set.

**Final Solution**: **Remove `defineConstraints` entirely!**

The assembly references themselves act as the constraint:
```json
"references": [
    "MaxSdk.Scripts",  // ← If this doesn't exist, assembly won't compile
    "Sorolla.Adapters"
]
```

If MAX SDK isn't installed, `MaxSdk.Scripts` doesn't exist → Unity **automatically excludes** the assembly. The `defineConstraints` was redundant and causing issues.

**Changes**:
- Removed `defineConstraints` from all adapter asmdefs:
  - `Sorolla.Adapters.MAX.asmdef` → `"defineConstraints": []`
  - `Sorolla.Adapters.Adjust.asmdef` → `"defineConstraints": []`
  - `Sorolla.Adapters.Firebase.asmdef` → `"defineConstraints": []`
- Added `[Preserve]` attributes to prevent IL2CPP stripping
- Added debug logging in `Register()` methods for troubleshooting
- Created `link.xml` for IL2CPP stripping protection (belt and suspenders)
- `DefineSymbols.cs` still useful for `#if` blocks in user code

**Learnings**:
- `defineConstraints` check is unreliable for cross-assembly dependencies
- Assembly references are a **natural constraint** - missing ref = no compile
- The Stub+Impl pattern works WITHOUT `defineConstraints`:
  - Stub always compiles (no external refs)
  - Impl only compiles when SDK is installed (refs exist)
  - `[RuntimeInitializeOnLoadMethod]` registers impl if it compiled
- `[Preserve]` attribute prevents IL2CPP from stripping unused code

**Hindsight Insights**:
- **CRITICAL**: For Stub+Impl pattern, DON'T use `defineConstraints` - let assembly refs do the work
- `versionDefines` are still useful for `#if` blocks within the assembly
- `DefineSymbols.cs` is useful for global defines that user code might need
- Always add `[Preserve]` to `RuntimeInitializeOnLoadMethod` methods

**Files Modified**:
- `Runtime/Adapters/MAX/Sorolla.Adapters.MAX.asmdef` - removed defineConstraints
- `Runtime/Adapters/Adjust/Sorolla.Adapters.Adjust.asmdef` - removed defineConstraints
- `Runtime/Adapters/Firebase/Sorolla.Adapters.Firebase.asmdef` - removed defineConstraints
- `Runtime/Adapters/MAX/MaxAdapterImpl.cs` - added [Preserve]
- `Runtime/Adapters/Adjust/AdjustAdapterImpl.cs` - added [Preserve]
- `Runtime/Adapters/Firebase/FirebaseAdapterImpl.cs` - added [Preserve]
- `Runtime/link.xml` - created for IL2CPP protection
- `Editor/Sdk/DefineSymbols.cs` - SDK detection for global defines

**Status**: ✅ VERIFIED WORKING - Android build shows MAX/Adjust initializing

---

## 2025-12-24: Adapter Architecture Overhaul (v2.3.0)

**Changes**:
- Refactored all optional SDK adapters (Firebase, MAX, Adjust) to Stub + Implementation pattern
- Created separate assemblies with `defineConstraints` for each SDK
- Stubs always compile, implementations only compile when SDK installed
- Runtime registration via `RuntimeInitializeOnLoadMethod`

**Problem Solved**:
Unity resolves assembly references BEFORE evaluating `#if` preprocessor blocks. Previous approach:
```csharp
#if FIREBASE_INSTALLED
using Firebase;  // ← Unity tries to resolve this even if FIREBASE_INSTALLED is false!
#endif
```
This caused "Firebase assembly not found" errors in Prototype mode.

**Solution**:
- `Sorolla.Adapters.asmdef` - no external references (always compiles)
- `Sorolla.Adapters.Firebase.asmdef` - `defineConstraints: ["FIREBASE_*_INSTALLED"]`
- If constraint not met, Unity skips the entire assembly (no reference resolution)

**New Structure**:
```
Adapters/
├── MaxAdapter.cs (stub)
├── MAX/
│   ├── Sorolla.Adapters.MAX.asmdef
│   └── MaxAdapterImpl.cs
```

**Learnings**:
- `defineConstraints` are evaluated BEFORE assembly reference resolution
- `versionDefines` set symbols **for that assembly only** (NOT project-wide - see 2025-12-26 fix)
- `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` runs before any scene loads
- Cross-assembly type references need shared types (created `AdRevenueInfo` struct)

**Hindsight Insights**:
- For future SDKs: use Stub + Impl pattern from the start
- `defineConstraints` can use `||` for OR logic: `"A || B || C"`
- Interface must be `internal` so users only see the public static class
- Events need to be forwarded from impl to stub (subscribe in RegisterImpl)

**Files Modified**:
- `Adapters/Sorolla.Adapters.asmdef` - removed all external refs
- `Adapters/MaxAdapter.cs` - converted to stub
- `Adapters/AdjustAdapter.cs` - converted to stub + added AdRevenueInfo
- `Adapters/MAX/` - new folder with impl
- `Adapters/Adjust/` - new folder with impl
- `Adapters/Firebase/` - new folder (created in earlier session)

---

## 2025-12-18: Developer Pain Points Analysis & Implementation Plan

**Changes**:
- Analyzed 9 developer pain points from market research
- Validated against codebase to assess current state
- Checked latest SDK versions (GA 7.10.6, MAX 8.x, Firebase 13.6.0, Adjust 5.5.0)
- Created comprehensive implementation plans in plan.md
- Prioritized GDPR/UMP as critical gap

**Pain Points Assessment**:
| # | Pain Point | Status | Priority |
|---|------------|--------|----------|
| 1 | SDK dependency conflicts | ✅ Addressed | - |
| 2 | Gradle/manifest merging | ⚠️ Partial | High |
| 3 | Multi-SDK overhead | ✅ Core value | - |
| 4 | Documentation quality | ✅ Improved | Medium |
| 5 | Unity version compat | ⚠️ Needs test | High |
| 6 | GDPR/ATT consent | ❌ Critical gap | **CRITICAL** |
| 7 | Testing without live | ⚠️ Partial | Medium |
| 8 | App size bloat | ❌ Not addressed | Medium |
| 9 | Update fatigue | ⚠️ Partial | Low |

**Learnings**:
- Google UMP deadline was Jan 2024 - we're behind on compliance
- Firebase 13.x available but 12.10.1 still fine (evaluate migration)
- GameAnalytics version is current (7.10.6)
- Adjust uses Git URL so auto-updates to latest

**Hindsight Insights**:
- **CRITICAL**: UMP integration is blocking for EU/UK ad revenue
- UMP API pattern: `Update()` → `LoadAndShowConsentFormIfRequired()` → `CanRequestAds()`
- AppLovin MAX automates UMP but we need explicit support for non-MAX users
- For Unity 6 testing: check Firebase 12.x compatibility first
- Build validation tool would catch many support issues preemptively

**Proposed API for UMP**:
```csharp
SorollaSDK.RequestConsentUpdate(callback);
SorollaSDK.ShowConsentFormIfRequired(onComplete);
SorollaSDK.CanRequestAds { get; }
SorollaSDK.ConsentStatus { get; }
```

**Sources Validated**:
- [AppLovin MAX Changelog](https://github.com/AppLovin/AppLovin-MAX-Unity-Plugin/releases)
- [GameAnalytics Releases](https://github.com/GameAnalytics/GA-SDK-UNITY/releases) - v7.10.6
- [Firebase Unity SDK](https://github.com/firebase/firebase-unity-sdk/releases) - v13.6.0
- [Adjust Unity SDK](https://github.com/adjust/unity_sdk/releases) - v5.5.0
- [Google UMP Unity](https://developers.google.com/admob/unity/privacy)

---

## 2025-12-17: ByteBrew Business Model Deep-Dive

**Changes**:
- Added ByteBrew funding details ($4M Seed, Konvoy Ventures)
- Documented "Land & Expand" business model strategy
- Added future monetization plans (paid UA tools, enterprise SLAs)
- Identified key competitive insight: data ownership differentiator

**Learnings**:
- ByteBrew is VC-funded ($4M from gaming-focused VCs)
- "100% free" is growth strategy, not sustainable business model
- Future paid products will be additive, not restrictive
- Their model: Slack/Figma playbook (free → indispensable → monetize/exit)

**Hindsight Insights**:
- **Sorolla's key differentiator vs ByteBrew**: You own your data
  - Sorolla wraps YOUR accounts (GA, MAX, Adjust)
  - ByteBrew owns the data layer = future lock-in risk
- ByteBrew validates free SDK market, but different trust model
- For positioning: emphasize "your accounts, your data, no middleman"

**Sources**:
- [ByteBrew Crunchbase](https://www.crunchbase.com/organization/bytebrew)
- [ByteBrew Docs](https://docs.bytebrew.io/startup/home)

---

## 2025-12-17: Expanded Competitive Research

**Changes**:
- Added CrazyLabs CLIK platform analysis
- Added ByteBrew SDK as independent alternative
- Expanded LevelPlay/ironSource profile
- Added GameAnalytics A/B testing API details
- Added Attribution SDK landscape (AppsFlyer, Adjust, Branch, AppMetrica)
- Created technical requirements comparison table
- Added market statistics (publisher scale, SDK market share)
- Added detailed SDK best practices section to market-research.md
- Added AppLovin MAX, Firebase, Adjust detailed best practices

**New Competitors Analyzed**:
| Competitor | Type | Key Insight |
|------------|------|-------------|
| CrazyLabs CLIK | Publisher SDK | Cloud build system, PFA program |
| ByteBrew | Independent | Free all-in-one, closest competitor model |
| LevelPlay | Unity/ironSource | Native Unity integration post-merger |

**Learnings**:
- ByteBrew is closest competitor to Sorolla's positioning (free, independent, all-in-one)
- AppsFlyer leads attribution (48% Android), Adjust at 30% is solid choice
- "10-minute integration" is industry benchmark for SDK setup
- GameAnalytics A/B testing requires remote config readiness check
- Hybrid-casual games driving eCPM increases

**Hindsight Insights**:
- For competitor comparison: competitive-analysis.md now has 3 comparison matrices
- For SDK best practices: market-research.md has detailed code examples
- ByteBrew feature parity analysis useful for roadmap prioritization
- Consider IAP validation feature (ByteBrew has server-side validation)

**Research Sources Added**:
- Foresight Mobile, Mike Smales, Luciq AI (SDK best practices)
- Embrace Unity Pain Points Report 2024
- Mobio Group SDK Leaders 2024
- Tenjin Ad Monetization Report 2025

---

## 2025-12-17: Public/Internal Documentation Split

**Changes**:
- Split documentation into public and internal sections
- Created `internal/` directory for SDK development docs
- Added competitive analysis (VoodooSauce, Homa Belly)
- Added market research (developer pain points, trends)
- Added product roadmap (v2.2-v3.1 planning)
- Moved ai-agents.md, plan.md, devlog.md, architecture.md to internal/
- Created contributing.md for public contributor guide

**New Structure**:
```
Documentation~/
├── getting-started.md       ← Public: Quick start
├── prototype-setup.md       ← Public: Prototype mode
├── full-setup.md            ← Public: Full mode
├── firebase.md              ← Public: Firebase add-on
├── api-reference.md         ← Public: API docs
├── troubleshooting.md       ← Public: Issues/fixes
├── contributing.md          ← Public: How to contribute
└── internal/
    ├── README.md            ← Internal index
    ├── ai-agents.md         ← AI agent guide
    ├── architecture.md      ← Technical deep-dive
    ├── plan.md              ← Sprint/backlog
    ├── devlog.md            ← Change history
    ├── competitive-analysis.md  ← Competition research
    ├── market-research.md   ← Developer needs
    └── product-roadmap.md   ← Feature planning
```

**Learnings**:
- Public docs should focus on "how to use"
- Internal docs should focus on "how to develop" and "why decisions"
- Competitive analysis valuable for product decisions
- Developer pain points inform feature prioritization

**Hindsight Insights**:
- For product decisions: competitive-analysis.md → product-roadmap.md
- For feature requests: market-research.md → plan.md
- Public docs: ../getting-started.md (relative from internal/)
- Internal docs contain sensitive competitive intelligence

**Metrics**:
- Public docs: 7 files (~3,500 tokens)
- Internal docs: 8 files (~5,000 tokens)
- Total: 15 files covering both audiences

---

## 2025-12-17: Documentation Reorganization (Developer-First)

**Changes**:
- Reorganized 21 docs → 10 focused files
- Consolidated redundant guides (index.md, SDK-Setup-Overview.md, QuickStart.md)
- Merged ARCHITECTURE.md + ARCHITECTURE_SUMMARY.md → architecture.md
- Created unified api-reference.md and troubleshooting.md
- Renamed setup files to lowercase (prototype-setup.md, full-setup.md, firebase.md)
- Removed modules/ folder, consolidated into architecture.md

**New Structure**:
```
Documentation~/
├── getting-started.md   ← Quick start for developers
├── prototype-setup.md   ← GameAnalytics + Facebook
├── full-setup.md        ← GameAnalytics + MAX + Adjust
├── firebase.md          ← Firebase add-on
├── api-reference.md     ← Unified API docs
├── troubleshooting.md   ← iOS + common issues
├── architecture.md      ← Contributors & AI agents
├── ai-agents.md         ← RAG optimization
├── plan.md              ← Task tracking
└── devlog.md            ← Change history
```

**Learnings**:
- Developers want task-focused navigation (setup, api, troubleshooting)
- AI agents need quick reference section in architecture.md
- Modular files were too granular for human UX

**Hindsight Insights**:
- For API: Start with `api-reference.md`
- For setup: Load `prototype-setup.md` or `full-setup.md`
- For issues: Load `troubleshooting.md`
- For code changes: Load `architecture.md` (includes AI quick reference)

**Metrics**:
- Reduced from 21 to 10 documentation files
- Total: ~4,500 tokens (compressed from ~8,000)
- Developer navigation: 3 clicks max to any content

---

## 2025-12-01: Firebase Suite Integration (v2.1.0)

**Changes**:
- Added FirebaseAdapter, FirebaseCrashlyticsAdapter, FirebaseRemoteConfigAdapter
- Created FirebaseCoreManager for centralized init
- Added Firebase toggles to SorollaConfig
- Updated SorollaWindow with Firebase section

**Learnings**:
- Firebase requires async initialization via CheckAndFixDependenciesAsync
- Remote config needs explicit FetchAndActivate call
- Crashlytics auto-captures Unity log messages

**Hindsight Insights**:
- Firebase adapters have ~20 LOC stubs when SDK not installed
- Remote config fallback chain: Firebase → GA → default
- Config files MUST match bundle ID exactly

---

## 2025-11-26: SDK Installation Refactor (v2.0.1)

**Changes**:
- Moved SDK installation to manifest.json manipulation
- Centralized all SDK versions in SdkRegistry
- Fixed GameAnalytics installation race condition

**Learnings**:
- Direct manifest editing more reliable than PackageManager API
- Need to add scoped registries before dependencies
- Assembly detection is sync, package manager is async

**Hindsight Insights**:
- SdkRegistry is single source of truth for versions
- Installation order matters: EDM → GA → platform SDKs
- Mode switch requires AssetDatabase.Refresh()

---

## 2025-11-25: Namespace Migration (v2.0.0)

**Changes**:
- Renamed `SorollaPalette` namespace to `Sorolla`
- Renamed package ID `com.sorolla.palette` to `com.sorolla.sdk`
- Restructured folder layout

**Learnings**:
- Breaking changes require major version bump
- Need to update all using statements
- Package ID change requires reimport

**Hindsight Insights**:
- Main namespace is `Sorolla` (not SorollaSDK)
- Public API is `SorollaSDK` static class
- Editor namespace is `Sorolla.Editor`

---

## 2025-11-10: Initial Release (v1.0.0)

**Changes**:
- Created Prototype and Full modes
- Implemented GA, Facebook, MAX, Adjust adapters
- Built SorollaWindow configuration UI
- Added ATT handling for iOS

**Learnings**:
- ATT must happen before SDK init for best ad fill
- Facebook requires both App ID AND Client Token
- MAX SDK key is account-level, ad units are app-level

**Hindsight Insights**:
- Prototype mode = rapid testing, no attribution
- Full mode = production with revenue tracking
- Auto-init via RuntimeInitializeOnLoadMethod

---

## Agent Instructions

**When to add entries**:
- After any code change that affects behavior
- When discovering non-obvious implementation details
- After fixing bugs (document root cause)
- After performance optimizations (document metrics)

**Entry guidelines**:
- Keep entries concise (<200 words)
- Focus on "what future me needs to know"
- Include file paths when relevant
- Add metrics when measurable

**RAG Query Examples**:
- `devlog.md Firebase` - Get Firebase-related history
- `devlog.md hindsight` - Get all insights
- `devlog.md v2.0` - Get version-specific changes
