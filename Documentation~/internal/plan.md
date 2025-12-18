# Sorolla SDK - Hierarchical Memory Buffer (Plan)

> **Purpose**: Dynamic task tracker for AI agents and developers
> **Last Updated**: 2025-12-18 | **Status**: Active

---

## Developer Pain Points - Implementation Plan

Based on market research, these are the critical issues facing mobile game SDK developers and our plans to address them.

### Pain Point #1: SDK Dependency Conflicts
**Impact**: Build failures, days of debugging
**Current State**: ✅ ADDRESSED
**How Sorolla Solves It**:
- Centralized `SdkRegistry.cs` with pinned versions
- Single source of truth for all SDK versions
- EDM (External Dependency Manager) handles native dependencies

**Validation**:
| SDK | Sorolla Version | Latest Unity Plugin | Native SDK | Status |
|-----|-----------------|---------------------|------------|--------|
| GameAnalytics | 7.10.6 | 7.10.6 | - | ✅ Current |
| AppLovin MAX | 8.5.0 | 8.5.1 | 13.3.1 | ✅ UMP ready (minor bump available) |
| Firebase | 12.10.1 | 13.6.0 (.unitypackage) | - | ⚠️ See note below |
| Adjust | Git (latest) | v5.5.0 | - | ✅ Auto-updated |

**MAX Version Note**:
Unity plugin version (8.5.x) ≠ native SDK version (13.x). The Unity plugin wraps the native SDK.
MAX 8.5.0 already includes UMP automation via native SDK 13.x - just needs to be enabled.

**Firebase Note**:
Google doesn't properly maintain Firebase Unity SDK for UPM. Official releases are `.unitypackage` only.
Current UPM version (12.10.1) is manually maintained on GitHub by project maintainer.
Upgrading to 13.x requires manual conversion - evaluate only if critical features needed.

**Action Items**:
- [x] GameAnalytics - current
- [x] MAX SDK 8.5.0 - UMP ready (wraps native 13.x)
- [ ] MAX 8.5.0 → 8.5.1 - minor bump, optional
- [ ] Firebase 13.x - evaluate only if needed (manual maintenance burden)
- [ ] Document version compatibility matrix

---

### Pain Point #2: Android Gradle/Manifest Merging
**Impact**: Platform-specific build issues
**Current State**: ✅ PARTIALLY ADDRESSED
**How Sorolla Solves It**:
- Uses EDM4U (External Dependency Manager) for dependency resolution
- `ManifestManager.cs` handles manifest modifications
- `SorollaIOSPostProcessor.cs` handles iOS-specific setup

**Gaps**:
- No automatic conflict detection
- No build validation step
- No clear error messages for Gradle failures

**Implementation Plan**:
```
Phase 1: Detection
- [ ] Add pre-build validation script
- [ ] Check for common conflicts (duplicate dependencies, version mismatches)
- [ ] Surface clear error messages in Unity Console

Phase 2: Resolution
- [ ] Auto-resolve simple conflicts (pick higher version)
- [ ] Document manual resolution steps for complex cases
- [ ] Add "SorollaSDK > Tools > Validate Build" menu item
```

**Files to Modify**: `Editor/SorollaSetup.cs`, new `Editor/BuildValidator.cs`

---

### Pain Point #3: Multiple SDK Integration Overhead
**Impact**: Weeks of development time
**Current State**: ✅ CORE VALUE PROPOSITION
**How Sorolla Solves It**:
- Single API: `SorollaSDK.TrackDesign()`, `SorollaSDK.ShowRewardedAd()`
- Auto-initialization via `[RuntimeInitializeOnLoadMethod]`
- Mode system (Prototype/Full) for different integration levels
- One-click SDK installation via `SorollaWindow.cs`

**Benchmark**: Industry target is 10-minute integration
**Sorolla Target**: < 5 minutes for Prototype mode

**Validation**: ✅ Already best-in-class, maintain advantage

---

### Pain Point #4: Documentation Quality
**Impact**: Onboarding friction, support tickets
**Current State**: ✅ RECENTLY IMPROVED
**What We Did**:
- Reorganized into public/internal split
- Created getting-started.md (10-minute guide)
- Added troubleshooting.md
- API reference with examples

**Remaining Gaps**:
- [ ] Video tutorials (high priority per research)
- [ ] Sample project/demo game
- [ ] Error message documentation
- [ ] Migration guide (v1.x → v2.x)

---

### Pain Point #5: Unity Version Compatibility
**Impact**: Upgrade blocking
**Current State**: ✅ VALIDATED
**Supported Versions**: Unity 2022.3 LTS → Unity 6.3 LTS

**Why not Unity 2021?**
Unity 2021 uses a different asset serialization format, making backwards compatibility maintenance burdensome. 2022.3 LTS is the minimum supported version.

**Validation Status**:
- [x] **Unity 2022.3 LTS**: Fully tested
- [x] **Unity 6.3 LTS**: Fully tested and working
- [ ] Document version requirements in getting-started.md

**SDK Requirements** (all satisfied by 2022.3+):
- GameAnalytics: Unity 2019.4+
- AppLovin MAX: Unity 2019.4+
- Firebase: Unity 2021 LTS+ (we exceed this)

---

### Pain Point #6: GDPR/ATT Consent Complexity ✅ IMPLEMENTED
**Impact**: Legal compliance risk - Limited Ads in EU/UK since Jan 2024
**Current State**: ✅ COMPLETE (API implemented, requires configuration)
**What We Have**:
- iOS ATT handling (`ContextScreenView.cs`, `FakeATTDialog.cs`)
- Basic consent flag (`HasConsent`)
- `FakeCMPDialog.cs` for editor testing
- **NEW**: Full UMP consent API via MAX integration

**Implementation Complete** (v2.2.0):
- [x] `ConsentStatus` enum: Unknown, NotApplicable, Required, Obtained, Denied
- [x] `SorollaSDK.ConsentStatus` - Current consent state from MAX
- [x] `SorollaSDK.CanRequestAds` - Whether ads can be shown
- [x] `SorollaSDK.PrivacyOptionsRequired` - Whether to show privacy button
- [x] `SorollaSDK.ShowPrivacyOptions()` - Opens UMP privacy form
- [x] `OnConsentStatusChanged` event for UI updates

**Configuration Required** (Developer Setup):
```
1. Enable "MAX Terms and Privacy Policy Flow" in AppLovin Integration Manager
2. Set Privacy Policy URL and User Tracking Usage Description
3. Configure consent form in Google AdMob dashboard
4. Test regional flow (MAX auto-detects GDPR regions)
```

**API Design** (implemented):
```csharp
// Consent status (read from MAX)
SorollaSDK.ConsentStatus { get; } // Unknown, NotApplicable, Required, Obtained, Denied
SorollaSDK.CanRequestAds { get; } // True if consent obtained or not required

// Privacy options (for settings screen)
SorollaSDK.ShowPrivacyOptions(Action onComplete = null); // Shows UMP privacy form
SorollaSDK.PrivacyOptionsRequired { get; } // Whether to show button

// Event
SorollaSDK.OnConsentStatusChanged += (status) => { /* update UI */ };
```

**Files Modified**:
- `Runtime/Adapters/MaxAdapter.cs` - Added consent status tracking, ShowPrivacyOptions()
- `Runtime/SorollaSDK.cs` - Exposed consent API

**Documentation**:
- [x] Created `gdpr-consent-setup.md` - Complete setup guide with verification checklist
- [x] AdMob consent form setup documented
- [x] EEA/UK testing guide (debug geography) included
- [x] Troubleshooting section added

**Source**: [AppLovin MAX UMP Automation](https://developers.axon.ai/en/max/unity/overview/terms-and-privacy-policy-flow/)

---

### Pain Point #7: Testing Without Live Environment
**Impact**: QA difficulty
**Current State**: ⚠️ PARTIAL
**What We Have**:
- `FakeATTDialog.cs` - Editor ATT simulation
- `FakeCMPDialog.cs` - Editor CMP simulation
- `SorollaTestingTools.cs` - Reset/debug utilities

**Reality Check**:
Most ad networks provide test ads when registering a test device (IDFA/GAID). Full mock implementation is low ROI.

**Recommended Approach** (v2.3.0):
```
Phase 1: Minimal Mock Ads in Debug UI
├── Add simple mock ad buttons to existing Debug Panel
├── Simulate rewarded flow (shows placeholder → callback)
├── Simulate interstitial flow
└── Log events to Console

Phase 2: Test Device Documentation
├── Document how to register test devices per network
├── Document MAX test mode settings
└── Add troubleshooting for "no fill" issues
```

**Files to Modify**: `Runtime/DebugUI/` (existing debug panel)

---

### Pain Point #8: App Size Bloat
**Impact**: User acquisition impact (each MB = lower conversion)
**Current State**: ⚠️ NOT ADDRESSED
**Research**: Each SDK adds 2-10MB

**Implementation Plan**:
```
Phase 1: Measurement (v2.2.0)
├── Document SDK size contribution
├── Add build size report to docs
└── Compare Prototype vs Full mode sizes

Phase 2: Optimization (v2.3.0)
├── Evaluate stripping options
├── Document ProGuard/R8 rules
├── Consider optional adapter packages
└── IL2CPP code size optimization
```

**Documentation to Add**: `Documentation~/full-setup.md` → "Build Size Impact" section

---

### Pain Point #9: Update Fatigue
**Impact**: Maintenance burden (monthly SDK updates)
**Current State**: ✅ PARTIALLY ADDRESSED
**How Sorolla Helps**:
- Central version management in `SdkRegistry.cs`
- Single update point for all SDKs
- Mode system reduces required dependencies

**Gaps**:
- No automated update notifications
- No changelog aggregation
- Manual version bumping required

**Implementation Plan** (v2.3.0):
```
├── Add update check in SorollaWindow
├── Compare installed vs latest versions
├── Show notification badge when updates available
└── Link to SDK changelogs
```

---

## Current Sprint (Updated 2025-12-18)

### Priority: Critical (v2.2.0)
- [x] **UMP Integration** - Unblock EU/UK ad revenue ✅ COMPLETE
  - [x] MAX 8.5.0 already supports UMP (wraps native 13.x) ✓
  - [x] Expose consent status via SorollaSDK API ✓
  - [x] Add SorollaSDK.ShowPrivacyOptions() for settings screen ✓
  - [x] Add SorollaSDK.CanRequestAds property ✓
  - [x] Add SorollaSDK.PrivacyOptionsRequired property ✓
  - [x] Add OnConsentStatusChanged event ✓
  - [ ] Enable "MAX Terms and Privacy Policy Flow" in Integration Manager (user setup)
  - [ ] Configure AdMob consent form in Google AdMob dashboard (user setup)
- [x] **Build Validator** - Must work out-of-box for every studio ✅ COMPLETE
  - [x] Create `Editor/BuildValidator.cs` with 7 validation checks ✓
  - [x] Create `Editor/AndroidManifestSanitizer.cs` for auto-fixing manifest issues ✓
  - [x] Integrated into main SorollaWindow as "Build Health" section ✓
  - [x] Auto-runs on window open and after mode switch ✓
  - [x] Auto-fixes orphaned AndroidManifest entries (e.g., Facebook SDK entries in Full mode) ✓
  - [x] Pre-build validation via IPreprocessBuildWithReport ✓
  - [x] Menu item "SorollaSDK > Tools > Validate Build" opens main window ✓

### Priority: High (v2.2.0)
- [ ] **Banner Ads** - Easy win (rarely used but simple to add)
  - [ ] Implement ShowBanner/HideBanner in MaxAdapter
  - [ ] Add position option (top/bottom)

### Priority: Medium (v2.3.0)
- [ ] **Mock Ads in Debug UI** - Minimal implementation
  - [ ] Add test buttons to existing debug panel
  - [ ] Document test device registration per network
- [ ] **Size Documentation** - Document build size impact
- [ ] **Video Tutorials** - Create getting started video

### Priority: Low (Backlog)
- [ ] **Update Notifications** - SDK update checker
- [ ] **Sample Game** - Demo integration project
- [ ] **Firebase 13.x** - Only if critical features needed (manual maintenance)

### ✅ Completed
- [x] **Unity 6 Support** - Validated on Unity 6.3 LTS
- [x] **Documentation Reorganization** - Public/internal split complete
- [x] **UMP Consent Integration** - GDPR/ATT consent API via MAX (2025-12-18)
- [x] **Build Validator** - Pre-build validation with 6 checks + auto-fix (2025-12-18)
- [x] **UI Consolidation** - Merged Setup Checklist + SDK Status into SDK Overview (2025-12-18)

---

## Backlog

### Features
- [ ] AdMob adapter (alternative to MAX)
- [ ] Unity Ads adapter
- [ ] ironSource adapter
- [ ] In-app purchase tracking
- [ ] A/B test assignment API
- [ ] User segmentation support

### Technical Debt
- [ ] Refactor adapter pattern to interface-based
- [ ] Add unit tests for adapters
- [ ] Improve error messages for missing config
- [ ] Add timeout handling for Firebase init

### Documentation
- [x] Reorganize docs for developer UX (2025-12-17)
- [x] Create troubleshooting guide
- [x] Consolidate API reference
- [ ] Migration guide from v1.x to v2.x
- [ ] Performance best practices
- [ ] Ad placement guidelines
- [ ] Video tutorials

---

## Architecture Decisions

### ADR-001: Adapter Pattern for SDKs
- **Status**: Accepted
- **Context**: Need to support multiple third-party SDKs
- **Decision**: Static adapter classes with conditional compilation
- **Consequences**: Simple, low overhead, but no runtime SDK switching

### ADR-002: ScriptableObject Config
- **Status**: Accepted
- **Context**: Need persistent, editable configuration
- **Decision**: SorollaConfig as ScriptableObject in Resources
- **Consequences**: Easy editor integration, loaded once at startup

### ADR-003: Event-Driven Debug UI
- **Status**: Accepted
- **Context**: Debug panel needs loose coupling
- **Decision**: Static event hub (SorollaDebugEvents)
- **Consequences**: Easy to add new panels, but events can be missed

---

## Open Issues

### TBD: MAX Banner Position API
- Should we expose banner position (top/bottom)?
- Current: Hardcoded to bottom
- Options: Enum parameter, config setting

### TBD: Firebase Auth Integration
- User requested Firebase Auth support
- Scope: Add authentication adapter?
- Impact: Significant, new module needed

### TBD: Offline Event Queueing
- GA handles this internally
- Should we add explicit queue API?
- Risk: Duplicate events if GA already queues

---

## Version Roadmap

### v2.2.0 (Next)
- Banner ads support
- ~~UMP consent integration~~ ✅ Complete
- ~~Unity 6 official support~~ ✅ Complete

### v2.3.0
- AdMob alternative adapter
- Enhanced crash reporting
- Performance dashboard

### v3.0.0 (Future)
- Interface-based adapters
- Runtime SDK configuration
- Plugin architecture

---

## Agent Quick Reference

**To update this file**:
```
Edit plan.md → Update checkbox [ ] to [x]
Add new items under appropriate section
```

**Task Status Legend**:
- `[ ]` Pending
- `[x]` Completed
- `[~]` In Progress
- `[!]` Blocked

**RAG Query Examples**:
- `plan.md current sprint` - Get active tasks
- `plan.md backlog features` - Get feature requests
- `plan.md ADR` - Get architecture decisions
