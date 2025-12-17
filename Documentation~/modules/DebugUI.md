# Debug UI Module - In-Game Debug Panel

> **RAG Query**: `debug UI panel testing ads analytics mobile`
> **Token Budget**: ~400 tokens | **Path**: `Samples~/DebugUI/`

## Purpose

Optional in-game debug overlay for on-device SDK testing. Event-driven architecture with tabbed interface.

## Activation

- **PC/Mac**: Press BackQuote (`) key
- **Mobile**: Triple-tap screen

## Architecture

```
DebugPanelManager (Singleton, DontDestroyOnLoad)
    ↓
SorollaDebugEvents (Static Event Hub)
    ├─ OnTabChanged, OnShowToast
    ├─ OnAdStatusChanged, OnSDKHealthChanged
    ├─ OnModeChanged, OnToggleChanged
    └─ OnLogAdded, OnLogsClear, OnLogFilterChanged
    ↓
Tab Controllers (Feature UI)
    ├─ IdentityCardController (GAID/IDFA)
    ├─ AdCardController (MAX testing)
    ├─ EventsTabController (GA/Firebase)
    ├─ PrivacyController (ATT/CMP)
    ├─ LogController (SDK logs)
    ├─ CrashlyticsController
    └─ RemoteConfigDisplay
```

## Components (31 scripts)

| Category | Count | Key Classes |
|----------|-------|-------------|
| Core | 5 | `DebugPanelManager`, `SorollaDebugEvents`, `SorollaDebugTheme` |
| Components | 6 | `StatusBadge`, `ToastNotification`, `ToggleSwitch` |
| Controllers | 11 | `AdCardController`, `LogController`, `PrivacyController` |
| Mode System | 7 | `ModeColor`, `ModeText`, `ModeGameObjectActive` |
| Utility | 2 | `SafeAreaHandler`, `ResetPosOnStart` |

## Key Types

```csharp
// Types.cs
public enum AdType { Interstitial, Rewarded, Banner }
public enum AdStatus { Idle, Loading, Loaded, Showing, Failed }
public enum LogLevel { All, Verbose, Info, Warning, Error }
public enum LogSource { UI, Game, Sorolla, GA, Firebase, MAX, Adjust }
public enum ToastType { Info, Success, Warning, Error }
```

## Theme System

`SorollaDebugTheme.cs` - ScriptableObject with 50+ UI properties:
- Colors: `canvasBackground`, `accentPurple`, `successGreen`
- Dimensions: `cardCornerRadius`, `buttonHeight`, `standardPadding`

## Tab Features

| Tab | Purpose |
|-----|---------|
| Identity | Show GAID/IDFA for ad verification |
| Ads | Load/show MAX rewarded & interstitial |
| Events | Fire custom GA/Firebase events |
| Privacy | Test ATT/CMP dialogs |
| Logs | Real-time SDK log filtering |
| Crashlytics | Send test crashes |
| Remote Config | Display fetched values |

## Mobile Considerations

- Triple-tap threshold: 500ms between taps
- Safe area handling for notched devices
- Performance: Event-driven updates minimize frame impact

---
*Related: [SorollaSDK.md](SorollaSDK.md) for API being tested*
