# ATT & Privacy Module - iOS App Tracking Transparency

> **RAG Query**: `ATT iOS privacy consent IDFA tracking`
> **Token Budget**: ~350 tokens | **Path**: `Runtime/ATT/`

## Purpose

Handles iOS 14.5+ App Tracking Transparency (ATT) prompt flow and GDPR consent for ad personalization. Includes editor testing utilities.

## Components

| File | Purpose | Platform |
|------|---------|----------|
| `ContextScreenView.cs` | Pre-ATT context screen UI | iOS/Editor |
| `FakeATTDialog.cs` | Mock ATT dialog for Editor | Editor only |
| `FakeCMPDialog.cs` | Mock GDPR consent dialog | Editor only |

## ATT Flow

```
App Launch
    ↓
SorollaBootstrapper.Start()
    ↓
┌─ iOS ─────────────────────────────────────┐
│ Check ATTrackingStatusBinding             │
│   ├─ NOT_DETERMINED → Show ContextScreen  │
│   │       └─ User taps → Native ATT prompt│
│   ├─ AUTHORIZED → consent = true          │
│   └─ DENIED/RESTRICTED → consent = false  │
└───────────────────────────────────────────┘
┌─ Android ─────────────────────────────────┐
│ Skip ATT (no iOS equivalent)              │
│ consent = true                            │
└───────────────────────────────────────────┘
┌─ Editor ──────────────────────────────────┐
│ Show FakeATTDialog prefab                 │
│ Allow/Deny buttons for testing            │
└───────────────────────────────────────────┘
    ↓
SorollaSDK.Initialize(consent)
```

## ContextScreenView

```csharp
// File: Runtime/ATT/ContextScreenView.cs (50 LOC)
namespace Sorolla.ATT {
    public class ContextScreenView : MonoBehaviour {
        public Button continueButton;
        // Loaded from Resources/ContextScreen.prefab
        // Shows explanation before native ATT prompt
    }
}
```

## FakeATTDialog

```csharp
// File: Runtime/ATT/FakeATTDialog.cs (57 LOC)
namespace Sorolla.ATT {
    public class FakeATTDialog : MonoBehaviour {
        public Button allowButton;
        public Button denyButton;
        public Action<bool> OnResult;
        // Mimics iOS ATT UI in Editor
    }
}
```

## Required Assets

| Asset | Location | Purpose |
|-------|----------|---------|
| ContextScreen | `Resources/ContextScreen.prefab` | Pre-ATT UI |
| FakeATTDialog | `Resources/FakeATTDialog.prefab` | Editor testing |
| FakeCMPDialog | `Resources/FakeCMPDialog.prefab` | GDPR testing |

## iOS Build Setup

`SorollaIOSPostProcessor` automatically adds to Info.plist:
- `NSUserTrackingUsageDescription` - ATT prompt text
- `SKAdNetworkItems` - Required for MAX

## Mobile Considerations

- ATT must be shown before ad SDK init for optimal fill rates
- Context screen improves opt-in rates ~20-30%
- IDFA unavailable if denied - affects attribution accuracy

---
*Related: [SorollaSDK.md](SorollaSDK.md) for HasConsent property*
