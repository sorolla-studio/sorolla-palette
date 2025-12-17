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

## 2025-12-17: Documentation System Created

**Changes**:
- Created modular documentation in `Documentation~/modules/`
- Added ARCHITECTURE_SUMMARY.md for quick orientation
- Added plan.md for task tracking
- Added devlog.md for hindsight logging
- Token-optimized all files (<500 tokens each)

**Learnings**:
- Codebase has 59 C# scripts across 4 namespaces
- Adapter pattern used consistently for SDK integration
- Conditional compilation guards all optional features

**Hindsight Insights**:
- For API questions: Start with `modules/SorollaSDK.md`
- For SDK integration: Load `modules/Adapters.md`
- For debugging: Load `modules/DebugUI.md`
- Architecture uses static classes - no DI container

**Metrics**:
- Total documentation: ~3,000 tokens
- Per-module average: ~400 tokens
- RAG retrieval optimized with query hints

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
