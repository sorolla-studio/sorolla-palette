# Sorolla SDK - Hierarchical Memory Buffer (Plan)

> **Purpose**: Dynamic task tracker for AI agents and developers
> **Last Updated**: 2025-12-17 | **Status**: Active

---

## Current Sprint

### Priority: High
- [ ] **Android Performance**: Profile ad loading on low-end devices
- [ ] **iOS 18 Compatibility**: Test ATT flow on latest iOS
- [ ] **Unity 6 Support**: Verify package compatibility

### Priority: Medium
- [ ] **Banner Ads**: Complete MAX banner implementation
- [ ] **Consent V2**: Migrate to Google's User Messaging Platform (UMP)
- [ ] **Firebase Crashlytics**: Add non-fatal error tracking API

### Priority: Low
- [ ] **Documentation**: Add video tutorials
- [ ] **Sample Game**: Create demo integration project

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
