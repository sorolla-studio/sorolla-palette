# Plan: MAX Ad Network Integration Testing & Documentation

## SESSION STATUS: IN PROGRESS (Paused)
**Last Updated:** 2025-12-30
**Next Action:** User creates Google AdMob account

---

## Progress So Far

### Completed
- [x] Research easiest ad networks to integrate with MAX
- [x] Explore existing Sorolla SDK MAX implementation
- [x] Create implementation plan
- [x] Decide scope: Start with Google AdMob on Android

### In Progress
- [ ] Create Google AdMob account (USER ACTION NEEDED)

### Pending
- [ ] Create AdMob ad units (Rewarded, Interstitial)
- [ ] Install Google adapter via Unity Integration Manager
- [ ] Configure Google in AppLovin MAX dashboard
- [ ] Build to Android and test with Mediation Debugger
- [ ] Create `mediation-networks.md` documentation
- [ ] Update `ads-setup.md` with mediation section
- [ ] Commit and push

---

## Key Learnings from Research

1. **AppLovin Exchange is built-in** - Already working (user confirmed test placement works)
2. **Google AdMob is easiest** - Just needs App ID, one-click adapter install
3. **Unity Ads requires bidding setup** - Must map Placement IDs between dashboards
4. **Meta is most complex** - Requires FB app installed, SDK v11+, advertiser tracking
5. **Mediation Debugger** - Key tool for validating network integrations

---

## Who Does What

### User Actions (Manual/Browser)
1. Create Google AdMob account at [admob.google.com](https://admob.google.com)
2. Create app in AdMob → Get **App ID** (format: `ca-app-pub-XXX~YYY`)
3. Create Rewarded ad unit → Get **Ad Unit ID**
4. Create Interstitial ad unit → Get **Ad Unit ID**
5. Add Google network to ad units in [AppLovin MAX Dashboard](https://dash.applovin.com)
6. Build and test on Android device

### Claude Actions (Code/Docs)
1. Guide through Unity Integration Manager setup
2. Create `mediation-networks.md` documentation
3. Update `ads-setup.md` with mediation section
4. Update `full-setup.md` with link to mediation guide
5. Commit and push changes

---

## Goal
Create a robust, documented process for integrating and testing ad networks with AppLovin MAX, starting with the easiest networks and building up. This will be used for games transitioning from prototype to full launch.

---

## Research Summary

### Easiest Networks to Test (Ranked by Setup Complexity)

| Rank | Network | Setup Complexity | Requirements |
|------|---------|------------------|--------------|
| 1 | **AppLovin Exchange** | None | Already built-in with MAX |
| 2 | **Google AdMob/Bidding** | Low | App ID only (Integration Manager) |
| 3 | **Unity Ads** | Medium | Bidding Placement ID + dashboard mapping |
| 4 | **Meta Audience Network** | High | FB app installed, SDK v11+, advertiser tracking |

### Key Testing Tools
- **Mediation Debugger**: Built into MAX SDK, validates networks, enables test mode
- **Debug UI**: Sorolla's existing debug panel for load/show testing

### Sources
- [MAX Unity Mediated Networks](https://support.axon.ai/en/max/unity/preparing-mediated-networks/)
- [Mediation Debugger Guide](https://support.axon.ai/en/max/unity/testing-networks/mediation-debugger/)
- [Unity Ads Bidding Setup](https://docs.unity.com/en-us/grow/dashboard/bidding/applovin)
- [MAX Test Mode](https://developers.axon.ai/en/max/unity/testing-networks/test-mode/)

---

## Implementation Plan

### Phase 1: Test Google AdMob (Easiest)

**Dashboard Setup:**
1. Create Google AdMob account at [admob.google.com](https://admob.google.com)
2. Create app → Get **App ID** (format: `ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY`)
3. Create ad units (Rewarded, Interstitial) → Get **Ad Unit IDs**

**Unity Setup:**
1. Open **AppLovin > Integration Manager**
2. Install **Google bidding and Google AdMob** adapter
3. Enter App IDs in the Integration Manager fields
4. Build to device

**Testing:**
1. Launch app → Open Mediation Debugger (or Debug UI triple-tap)
2. Verify Google appears in "Completed Integrations"
3. Enable Test Mode for Google
4. Load/show test ads

---

### Phase 2: Test Unity Ads (Medium)

**Unity Dashboard Setup:**
1. Go to [Unity Monetization Dashboard](https://cloud.unity.com/monetization)
2. Create project (or select existing)
3. Set **Mediation Partner** to **AppLovin MAX**
4. Create **Bidding Placement** for Rewarded and Interstitial
5. Copy **Bidding Placement IDs**

**AppLovin Dashboard Setup:**
1. Go to [MAX Dashboard](https://dash.applovin.com/) → Ad Units
2. Edit your ad units → Add Unity Ads network
3. Paste **Bidding Placement IDs** (must be bidding, not waterfall)

**Unity Setup:**
1. Open **AppLovin > Integration Manager**
2. Install **Unity Ads** adapter
3. Build to device

**Testing:**
1. Open Mediation Debugger → Verify Unity Ads in "Completed Integrations"
2. Enable Test Mode → Load/show test ads

---

### Phase 3: Test Meta Audience Network (Complex)

**Prerequisites:**
- Facebook app installed on test device
- Logged into Facebook account
- Meta Audience Network SDK v11+

**Meta Dashboard Setup:**
1. Go to [Meta for Developers](https://developers.facebook.com)
2. Create/select app → Enable **Audience Network**
3. Create Placement IDs for Rewarded/Interstitial
4. Register test device in Audience Network settings

**AppLovin Dashboard Setup:**
1. Edit ad units → Add Meta Audience Network
2. Enter Placement IDs

**Unity Setup:**
1. Open **AppLovin > Integration Manager**
2. Install **Meta Audience Network** adapter
3. If using FB SDK, ensure v11+ compatibility
4. Build to device

**Testing:**
1. Ensure FB app installed and logged in on device
2. Open Mediation Debugger → Verify Meta in integrations
3. Enable Test Mode → Load/show test ads

---

## Documentation Deliverables

### 1. Update `ads-setup.md`
Add new section: **"Adding Mediation Networks"** with:
- Network comparison table (complexity, requirements)
- Link to detailed guides per network

### 2. Create `mediation-networks.md` (new file)
Comprehensive guide covering:
- Overview of mediation and why it matters
- Network-by-network setup guides
- Testing checklist per network
- Troubleshooting common issues
- Mediation Debugger usage

### 3. Update `full-setup.md`
- Link to mediation-networks.md
- Add to checklist: "Mediation networks configured"

---

## Files to Modify/Create

| File | Action |
|------|--------|
| `Documentation~/ads-setup.md` | Add "Adding Mediation Networks" section |
| `Documentation~/mediation-networks.md` | **CREATE** - Full mediation guide |
| `Documentation~/full-setup.md` | Add link to mediation guide |

---

## Testing Workflow (Per Network)

```
1. Dashboard Setup (network-specific)
   ↓
2. AppLovin Dashboard (add network to ad units)
   ↓
3. Unity Integration Manager (install adapter)
   ↓
4. Build to Device
   ↓
5. Mediation Debugger → Verify "Completed Integrations"
   ↓
6. Enable Test Mode → Load/Show Test Ads
   ↓
7. Document any issues/quirks
```

---

## Execution Order (Scoped to Google AdMob)

**Platform:** Android
**Accounts:** Creating from scratch

1. **Create Google AdMob account**
2. **Create app + ad units in AdMob**
3. **Install adapter via Integration Manager**
4. **Configure in AppLovin MAX dashboard**
5. **Build to Android device**
6. **Test with Mediation Debugger + Debug UI**
7. **Document the complete process**
8. **Update ads-setup.md + create mediation-networks.md**
9. **Commit and push**

Future networks (Unity Ads, Meta) can follow the same documented pattern.
