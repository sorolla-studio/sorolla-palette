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
