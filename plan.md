# Sorolla SDK v3.1.0 - Firebase Mandatory

## Goal
Make Firebase mandatory in all paths (Prototype + Full) with zero-friction upgrade for existing users.

## User Experience After Upgrade
1. Unity imports updated SDK
2. Firebase packages auto-install silently (idempotent if already present)
3. One-time migration popup appears with simple checklist
4. User's only manual tasks: create Firebase project + download JSON configs

---

## Implementation Plan

### [x] 1. Change Firebase Requirement Level
**File:** `Editor/Sdk/SdkRegistry.cs`

Change all Firebase packages from `Optional` → `Core`:
```csharp
// Lines 168, 178, 188, 198
Requirement = SdkRequirement.Core  // was Optional
```

### [x] 2. Auto-Install Firebase on Upgrade
**File:** `Editor/SorollaSetup.cs`

Add versioned upgrade hook (runs once per project):
```csharp
const string SetupVersion = "v7";  // bump from v6
static string FirebaseMigrationKey => $"Sorolla_Firebase31_{Application.dataPath.GetHashCode()}";

// In RunSetup() or new method:
if (!EditorPrefs.GetBool(FirebaseMigrationKey, false))
{
    if (!SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
        SdkInstaller.InstallFirebase();

    EditorPrefs.SetBool(FirebaseMigrationKey, true);

    // Trigger migration popup
    EditorApplication.delayCall += MigrationPopup.Show;
}
```

### [x] 3. Migration Popup Window
**New file:** `Editor/MigrationPopup.cs`

A minimal EditorWindow that appears once:

```
┌─────────────────────────────────────────────┐
│  Sorolla SDK 3.1 - Firebase Now Required    │
├─────────────────────────────────────────────┤
│                                             │
│  Firebase is now required for all modes.    │
│  Packages have been auto-installed.         │
│                                             │
│  Complete setup:                            │
│  ┌───────────────────────────────────────┐  │
│  │ 1. Create Firebase project            │  │
│  │    [Open Firebase Console]            │  │
│  │                                       │  │
│  │ 2. Download config files to Assets/   │  │
│  │    • google-services.json (Android)   │  │
│  │    • GoogleService-Info.plist (iOS)   │  │
│  └───────────────────────────────────────┘  │
│                                             │
│  [Open Configuration]      [Close]          │
└─────────────────────────────────────────────┘
```

Features:
- Opens once after upgrade (tracked via EditorPrefs)
- "Open Firebase Console" button → `Application.OpenURL("https://console.firebase.google.com/")`
- "Open Configuration" button → `SorollaWindow.ShowWindow()`
- Simple, scannable, actionable

### [x] 4. Version Bump
**File:** `package.json`
```json
"version": "3.1.0"
```

### [x] 5. Update Documentation

**File:** `Documentation~/firebase.md`
- Add note: "Firebase is required as of v3.1"
- Simplify: remove "optional" language

**File:** `Documentation~/prototype-setup.md`
- Add Firebase config step (was only in full-setup)
- Keep it brief: "Download Firebase configs to Assets/"

**File:** `Documentation~/full-setup.md`
- Update to reflect Firebase is now required, not optional
- Remove "optional" badge from Firebase section

**File:** `CHANGELOG.md`
```markdown
## [3.1.0] - 2025-01-XX
### Changed
- Firebase is now required in all modes (Prototype + Full)
- Firebase packages auto-install on SDK import/upgrade

### Added
- Migration popup guides users through Firebase setup
- Auto-detection of Firebase config files in Configuration window
```

---

## Files to Modify

| File | Change |
|------|--------|
| `Editor/Sdk/SdkRegistry.cs` | Firebase requirement: Optional → Core |
| `Editor/SorollaSetup.cs` | Add upgrade hook + bump SetupVersion |
| `Editor/MigrationPopup.cs` | **NEW** - One-time setup checklist window |
| `package.json` | Version 3.0.0 → 3.1.0 |
| `CHANGELOG.md` | Document changes |
| `Documentation~/firebase.md` | Remove "optional" language |
| `Documentation~/prototype-setup.md` | Add Firebase config step |
| `Documentation~/full-setup.md` | Update Firebase section |

---

## Verification

1. **Fresh install test:**
   - Import SDK into new Unity project
   - Verify Firebase packages install automatically
   - Verify migration popup appears

2. **Upgrade test:**
   - Have project with v3.0.0 installed (no Firebase)
   - Update to v3.1.0
   - Verify Firebase packages auto-install
   - Verify migration popup appears once
   - Verify popup doesn't reappear after dismissing

3. **Idempotent test:**
   - Project already has Firebase installed
   - Update to v3.1.0
   - Verify no duplicate installs, no errors

4. **Build validation:**
   - Run `Palette > Tools > Validate Build`
   - Should warn if Firebase config files missing

---

## Design Decisions

- **Build validation**: Warning only (don't block) - allows local testing without Firebase
- **Popup style**: EditorWindow with checklist and action buttons
- **Escape hatch**: None - Firebase is truly mandatory, no hidden overrides
