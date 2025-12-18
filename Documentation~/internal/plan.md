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
| SDK | Sorolla Version | Latest Available | Status |
|-----|-----------------|------------------|--------|
| GameAnalytics | 7.10.6 | 7.10.6 | ✅ Current |
| AppLovin MAX | 8.5.0 | ~8.x (13.2.0 native) | ⚠️ Check update |
| Firebase | 12.10.1 | 13.6.0 | ⚠️ Major update available |
| Adjust | Git (latest) | v5.5.0 | ✅ Auto-updated |

**Action Items**:
- [ ] Evaluate Firebase 13.x migration (breaking changes?)
- [ ] Test MAX plugin against latest native SDKs
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
**Current State**: ⚠️ NEEDS VALIDATION
**Current Support**: Unity 2022.3 LTS+

**Validation Needed**:
- [ ] **Unity 6 LTS (6000.0.x)**: Test full integration
- [ ] **Unity 6.1**: Test bleeding edge compatibility
- [ ] Document minimum/recommended versions

**Research Findings**:
- GameAnalytics: Requires Unity 2019.4+
- AppLovin MAX: Requires Unity 2019.4+
- Firebase: Requires Unity 2021 LTS+ (2020 deprecated)

**Action**: Firebase requirement (2021+) is stricter than our stated 2022.3. We're safe.

---

### Pain Point #6: GDPR/ATT Consent Complexity ⚠️ CRITICAL GAP
**Impact**: Legal compliance risk
**Current State**: ❌ INCOMPLETE
**What We Have**:
- iOS ATT handling (`ContextScreenView.cs`, `FakeATTDialog.cs`)
- Basic consent flag (`HasConsent`)
- `FakeCMPDialog.cs` for editor testing

**What's Missing**:
- Google UMP (User Messaging Platform) integration
- TCF 2.0 (Transparency and Consent Framework) support
- GDPR consent storage
- Consent revocation UI

**Research Findings** (Google Docs):
> "Failure to adopt a Google-certified CMP by January 16, 2024, will limit eligible ad serving to only Limited Ads for EEA and UK traffic."

**Implementation Plan**:
```
Phase 1: UMP Integration (v2.2.0) - HIGH PRIORITY
├── Add Google UMP SDK dependency
├── Create UmpAdapter.cs
├── Add SorollaSDK.ShowConsentDialog()
├── Add SorollaSDK.HasConsentForAds property
├── Add SorollaSDK.HasConsentForAnalytics property
└── Update SorollaConfig with consent options

Phase 2: Consent Flow (v2.2.0)
├── Call UMP Update() on every app launch
├── Check CanRequestAds() before loading ads
├── Implement privacy options entry point
└── Add consent revocation support

Phase 3: Testing (v2.2.0)
├── Add debug geography simulation
├── Update FakeCMPDialog for UMP flow
└── Document EEA/UK testing process
```

**API Design**:
```csharp
// New APIs for v2.2.0
SorollaSDK.RequestConsentUpdate(Action<ConsentStatus> callback);
SorollaSDK.ShowConsentFormIfRequired(Action onComplete);
SorollaSDK.ShowPrivacyOptions(); // For settings screen
SorollaSDK.CanRequestAds { get; }
SorollaSDK.ConsentStatus { get; } // Required, NotRequired, Obtained, Unknown
```

**Files to Create**: `Runtime/Adapters/UmpAdapter.cs`, `Runtime/ConsentStatus.cs`
**Files to Modify**: `Runtime/SorollaSDK.cs`, `Runtime/SorollaConfig.cs`, `Runtime/SorollaBootstrapper.cs`

---

### Pain Point #7: Testing Without Live Environment
**Impact**: QA difficulty
**Current State**: ⚠️ PARTIAL
**What We Have**:
- `FakeATTDialog.cs` - Editor ATT simulation
- `FakeCMPDialog.cs` - Editor CMP simulation
- `SorollaTestingTools.cs` - Reset/debug utilities

**What's Missing**:
- Mock ad responses in Editor
- Simulated ad revenue events
- Test mode for analytics (prevent polluting real data)
- Network failure simulation

**Implementation Plan**:
```
Phase 1: Mock Ads (v2.3.0)
├── Add EditorMockAds.cs
├── Simulate rewarded ad flow (load → show → reward)
├── Simulate interstitial flow
├── Fire OnAdRevenuePaidEvent with mock data
└── Add "Test Mode" toggle in SorollaConfig

Phase 2: Analytics Sandbox (v2.3.0)
├── GA already has development mode (we enable it)
├── Add visual indicator for test mode
├── Log all events to Console in test mode
└── Prevent production data pollution
```

**Files to Create**: `Editor/MockAds/EditorMockAdProvider.cs`
**Files to Modify**: `Runtime/Adapters/MaxAdapter.cs`, `Runtime/SorollaConfig.cs`

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

## Current Sprint (Updated)

### Priority: Critical
- [ ] **GDPR/UMP Consent**: Implement Google UMP integration (Pain Point #6)
- [ ] **Banner Ads**: Complete MAX banner implementation

### Priority: High
- [ ] **Unity 6 Support**: Validate package compatibility (Pain Point #5)
- [ ] **Firebase 13.x**: Evaluate migration path
- [ ] **Build Validation**: Add pre-build conflict detection (Pain Point #2)

### Priority: Medium
- [ ] **Mock Ads**: Editor ad simulation (Pain Point #7)
- [ ] **Size Documentation**: Document build size impact (Pain Point #8)
- [ ] **Video Tutorials**: Create getting started video (Pain Point #4)

### Priority: Low
- [ ] **Update Notifications**: SDK update checker (Pain Point #9)
- [ ] **Sample Game**: Demo integration project

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
- UMP consent integration
- Unity 6 official support

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
