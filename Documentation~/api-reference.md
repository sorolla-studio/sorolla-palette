<!-- DO NOT EDIT. This file is generated from XML doc comments in Runtime/*.cs.
     To regenerate: UNITY_PATH=/path/to/Unity/Editor bash Tools/build-docs.sh
     Source of truth: `///` XML comments on public members in Runtime/Palette.cs,
     Runtime/SorollaConfig.cs, etc. CI enforces staleness via .github/workflows/docs-check.yml. -->

# Sorolla SDK API Reference

Complete reference for the public `Sorolla.Palette` namespace. For task-oriented
guides and examples, see `Documentation~/guides/` and `Documentation~/quick-start.md`.

---

## Class Palette
Main API for Palette SDK.
Provides unified interface for analytics, ads, and attribution.
Auto-initialized - no manual setup required.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public static class Palette
```
### Properties
#### IsInitialized
Whether the SDK is initialized

```csharp title="Declaration"
public static bool IsInitialized { get; }
```
#### HasConsent
Current user consent status (legacy - use ConsentStatus for GDPR compliance)

```csharp title="Declaration"
public static bool HasConsent { get; }
```
#### Config
Current configuration (may be null)

```csharp title="Declaration"
public static SorollaConfig Config { get; }
```
#### ConsentStatus
Current consent status from MAX's UMP integration.
Use this to determine ad loading/showing in GDPR regions.

```csharp title="Declaration"
public static ConsentStatus ConsentStatus { get; }
```
#### CanRequestAds
Whether ads can be requested (consent obtained or not required).
Use this to gate ad loading/showing in GDPR regions.

```csharp title="Declaration"
public static bool CanRequestAds { get; }
```

###### Example

```csharp
if (Palette.CanRequestAds)
    Palette.ShowRewardedAd(onComplete, onFailed);
else
    Debug.Log("Consent required");
```
#### PrivacyOptionsRequired
Whether a privacy options button should be shown in settings.
Only true if MAX CMP is available and user is in a consent region.

```csharp title="Declaration"
public static bool PrivacyOptionsRequired { get; }
```

###### Example

```csharp
privacyButton.gameObject.SetActive(Palette.PrivacyOptionsRequired);
```
#### IsRewardedAdReady
Whether a rewarded ad is ready to show

```csharp title="Declaration"
public static bool IsRewardedAdReady { get; }
```
#### IsInterstitialAdReady
Whether an interstitial ad is ready to show

```csharp title="Declaration"
public static bool IsInterstitialAdReady { get; }
```
#### AutoActivateRemoteConfigUpdates
When true (default), real-time Remote Config updates are activated immediately.
Set false for games where mid-session config changes would be jarring.
Use ActivateRemoteConfigAsync() for manual control when disabled.

```csharp title="Declaration"
public static bool AutoActivateRemoteConfigUpdates { get; set; }
```
### Methods
#### ShowPrivacyOptions(Action)
Show privacy options form (UMP consent form) for users to update their consent.
Call this from your settings screen when PrivacyOptionsRequired is true.

```csharp title="Declaration"
public static void ShowPrivacyOptions(Action onComplete = null)
```

###### Example

```csharp
// In your settings UI
if (Palette.PrivacyOptionsRequired)
{
    privacyButton.onClick.AddListener(() =>
        Palette.ShowPrivacyOptions());
}
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `System.Action` | *onComplete* | Optional callback when form is dismissed |

#### RefreshConsentStatus()
Refresh consent status from MAX SDK.
Call this if consent may have changed externally.

```csharp title="Declaration"
public static void RefreshConsentStatus()
```
#### TrackEvent(string, Dictionary&lt;string, object&gt;)
Track a custom structured event with arbitrary parameters.
Firebase receives full structured params. GA receives best-effort design event.
Use GA4 recommended event names where possible (e.g. "post_score", "tutorial_begin").

```csharp title="Declaration"
public static void TrackEvent(string eventName, Dictionary<string, object> parameters = null)
```

###### Example

```csharp
Palette.TrackEvent("booster_used", new Dictionary<string, object>
{
    { "booster_id", "speed_2x" },
    { "level", 12 },
});
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `System.String` | *eventName* | GA4-compatible event name (lowercase, underscores, max 40 chars) |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *parameters* | Structured params. Supported types: string, int, long, float, double, bool, enum. |

#### TrackProgression(ProgressionStatus, string, string, string, int, Dictionary&lt;string, object&gt;)
Track a progression event (level start, complete, fail).
Firebase/GA4 mapping: Start -&gt; level_start, Complete -&gt; level_end{success=1}, Fail -&gt; level_end{success=0}.
When Complete/Fail and score &gt; 0, a separate post_score event is fired with the score.

```csharp title="Declaration"
public static void TrackProgression(ProgressionStatus status, string progression01, string progression02 = null, string progression03 = null, int score = 0, Dictionary<string, object> extraParams = null)
```

###### Example

```csharp
Palette.TrackProgression(ProgressionStatus.Complete, "World1", "Chapter2", "Level3",
    score: 1500,
    extraParams: new Dictionary<string, object> { { "duration_sec", 45 } });
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `ProgressionStatus` | *status* | Whether the player started, completed, or failed the progression. |
| `System.String` | *progression01* | First progression level (e.g. world name). Required. |
| `System.String` | *progression02* | Second progression level (e.g. chapter). Optional. |
| `System.String` | *progression03* | Third progression level (e.g. level number). Optional. |
| `System.Int32` | *score* | Score achieved on Complete/Fail. Fires a separate Firebase post_score event when &gt; 0. Ignored on Start. |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *extraParams* | Optional structured params for Firebase (e.g. world, game_mode, duration_sec).
        Ignored by GameAnalytics. Supported types: string, int, long, float, double, bool, enum. |

#### TrackDesign(string, float)
Track a design event (custom analytics).

```csharp title="Declaration"
[Obsolete("Use Palette.TrackEvent(eventName, parameters) for structured custom events with Firebase/BigQuery support.")]
public static void TrackDesign(string eventName, float value = 0)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *eventName* |
| `System.Single` | *value* |

#### TrackResource(ResourceFlowType, string, float, string, string, Dictionary&lt;string, object&gt;)
Track a resource event (economy source/sink).
Firebase mapping: Source -&gt; earn_virtual_currency, Sink -&gt; spend_virtual_currency.

```csharp title="Declaration"
public static void TrackResource(ResourceFlowType flowType, string currency, float amount, string itemType, string itemId, Dictionary<string, object> extraParams = null)
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `ResourceFlowType` | *flowType* | Whether the player gained (Source) or spent (Sink) resources. |
| `System.String` | *currency* | In-game currency name (e.g. "coins", "gems"). Not a real-world ISO code. |
| `System.Single` | *amount* | Amount of currency. Must be positive. |
| `System.String` | *itemType* | Category of the item (e.g. "booster", "outfit"). |
| `System.String` | *itemId* | Specific item ID (e.g. "speed_2x", "ninja_skin"). |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *extraParams* | Optional structured params for Firebase (e.g. source, level, world).
        Ignored by GameAnalytics. Supported types: string, int, long, float, double, bool, enum. |

#### TrackPurchase(double, string, string, string)
Track an in-app purchase. Fans out to Adjust, TikTok, and Firebase Analytics.
Do not double-log if you rely on automatic store-side collection.

```csharp title="Declaration"
public static void TrackPurchase(double amount, string currency = "USD", string productId = null, string transactionId = null)
```

###### Example

```csharp
Palette.TrackPurchase(4.99, "USD",
    productId: "com.mygame.coins_100",
    transactionId: storeReceipt.transactionId);
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `System.Double` | *amount* | Purchase amount (e.g. 4.99) |
| `System.String` | *currency* | ISO 4217 currency code (default: USD) |
| `System.String` | *productId* | Store product ID for Firebase deduplication |
| `System.String` | *transactionId* | Transaction ID for Firebase deduplication |

#### SetUserId(string)
Set the user ID for analytics, crash reporting, and attribution.
Pass null to clear.

```csharp title="Declaration"
public static void SetUserId(string userId)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *userId* |

#### SetUserProperty(string, string)
Set a user property for Firebase Analytics segmentation and audience building.
Register custom properties in Firebase Console &gt; Analytics &gt; User Properties.

```csharp title="Declaration"
public static void SetUserProperty(string name, string value)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *name* |
| `System.String` | *value* |

#### ShowDebugger()
Shows the Sorolla debug panel. Requires DebugUI sample imported and prefab in scene.

```csharp title="Declaration"
public static void ShowDebugger()
```
#### HideDebugger()
Hides the Sorolla debug panel.

```csharp title="Declaration"
public static void HideDebugger()
```
#### ToggleDebugger()
Toggles the Sorolla debug panel visibility.

```csharp title="Declaration"
public static void ToggleDebugger()
```
#### Initialize(bool)
Initialize Palette SDK. Called automatically by SorollaBootstrapper.
Do NOT call directly.

```csharp title="Declaration"
public static void Initialize(bool consent)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Boolean` | *consent* |

#### IsRemoteConfigReady()
Check if Remote Config is ready (Firebase if enabled, otherwise GameAnalytics)

```csharp title="Declaration"
public static bool IsRemoteConfigReady()
```

###### Returns

`System.Boolean`
#### FetchRemoteConfig(Action&lt;bool&gt;)
Fetch Remote Config values. Fetches from Firebase if installed, GameAnalytics is always ready.

```csharp title="Declaration"
public static void FetchRemoteConfig(Action<bool> onComplete = null)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Action<System.Boolean>` | *onComplete* |

#### GetRemoteConfig(string, string)
Get Remote Config string value. Checks Firebase first, then GameAnalytics.

```csharp title="Declaration"
public static string GetRemoteConfig(string key, string defaultValue = "")
```

###### Returns

`System.String`

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.String` | *defaultValue* |

#### GetRemoteConfigInt(string, int)
Get Remote Config int value. Checks Firebase first, then GameAnalytics.

```csharp title="Declaration"
public static int GetRemoteConfigInt(string key, int defaultValue = 0)
```

###### Returns

`System.Int32`

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.Int32` | *defaultValue* |

#### GetRemoteConfigFloat(string, float)
Get Remote Config float value. Checks Firebase first, then GameAnalytics.

```csharp title="Declaration"
public static float GetRemoteConfigFloat(string key, float defaultValue = 0)
```

###### Returns

`System.Single`

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.Single` | *defaultValue* |

#### GetRemoteConfigBool(string, bool)
Get Remote Config bool value. Checks Firebase first, then GameAnalytics.

```csharp title="Declaration"
public static bool GetRemoteConfigBool(string key, bool defaultValue = false)
```

###### Returns

`System.Boolean`

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.Boolean` | *defaultValue* |

#### SetRemoteConfigDefaults(Dictionary&lt;string, object&gt;)
Set in-app defaults for Remote Config. Works before or after initialization.
Values are used when no fetched or cached value exists.

```csharp title="Declaration"
public static void SetRemoteConfigDefaults(Dictionary<string, object> defaults)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *defaults* |

#### ActivateRemoteConfigAsync()
Manually activate fetched Remote Config values.
Use when AutoActivateRemoteConfigUpdates is false.

```csharp title="Declaration"
public static Task<bool> ActivateRemoteConfigAsync()
```

###### Returns

`System.Threading.Tasks.Task<System.Boolean>`
#### LogException(Exception)
Log an exception to crash reporting services (Firebase Crashlytics)

```csharp title="Declaration"
public static void LogException(Exception exception)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Exception` | *exception* |

#### LogCrashlytics(string)
Log a message to crash reporting services (Firebase Crashlytics)

```csharp title="Declaration"
public static void LogCrashlytics(string message)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *message* |

#### SetCrashlyticsKey(string, string)
Set a custom key for crash reports (Firebase Crashlytics)

```csharp title="Declaration"
public static void SetCrashlyticsKey(string key, string value)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.String` | *value* |

#### SetCrashlyticsKey(string, int)
Set a custom int key for crash reports (Firebase Crashlytics)

```csharp title="Declaration"
public static void SetCrashlyticsKey(string key, int value)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.Int32` | *value* |

#### SetCrashlyticsKey(string, float)
Set a custom float key for crash reports (Firebase Crashlytics)

```csharp title="Declaration"
public static void SetCrashlyticsKey(string key, float value)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.Single` | *value* |

#### SetCrashlyticsKey(string, bool)
Set a custom bool key for crash reports (Firebase Crashlytics)

```csharp title="Declaration"
public static void SetCrashlyticsKey(string key, bool value)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.String` | *key* |
| `System.Boolean` | *value* |

#### ShowRewardedAd(Action, Action)
Show rewarded ad

```csharp title="Declaration"
public static void ShowRewardedAd(Action onComplete, Action onFailed)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Action` | *onComplete* |
| `System.Action` | *onFailed* |

#### ShowInterstitialAd(Action)
Show interstitial ad

```csharp title="Declaration"
public static void ShowInterstitialAd(Action onComplete)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Action` | *onComplete* |

### Events
#### OnConsentStatusChanged
Event fired when consent status changes.
Subscribe to update UI or behavior based on consent.

```csharp title="Declaration"
public static event Action<ConsentStatus> OnConsentStatusChanged
```
###### Event Type
`System.Action<Sorolla.Palette.Adapters.ConsentStatus>`
#### OnInitialized
Event fired when SDK initialization completes

```csharp title="Declaration"
public static event Action OnInitialized
```
###### Event Type
`System.Action`
#### OnShowDebuggerRequested
Fired when `ShowDebugger()` is called. Subscribed by the DebugUI sample prefab.

```csharp title="Declaration"
public static event Action OnShowDebuggerRequested
```
###### Event Type
`System.Action`
#### OnHideDebuggerRequested
Fired when `HideDebugger()` is called. Subscribed by the DebugUI sample prefab.

```csharp title="Declaration"
public static event Action OnHideDebuggerRequested
```
###### Event Type
`System.Action`
#### OnToggleDebuggerRequested
Fired when `ToggleDebugger()` is called. Subscribed by the DebugUI sample prefab.

```csharp title="Declaration"
public static event Action OnToggleDebuggerRequested
```
###### Event Type
`System.Action`
#### OnRemoteConfigUpdated
Fired when a real-time Remote Config update is received.
Includes the set of updated keys so games can decide whether to react.
If AutoActivateRemoteConfigUpdates is true, values are already activated when this fires.

```csharp title="Declaration"
public static event Action<IReadOnlyCollection<string>> OnRemoteConfigUpdated
```
###### Event Type
`System.Action<System.Collections.Generic.IReadOnlyCollection<System.String>>`

---

## Class SorollaConfig
Configuration asset for Palette SDK.
Create via: Assets &gt; Create &gt; Palette &gt; Config
Save to: Assets/Resources/SorollaConfig.asset

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
[CreateAssetMenu(fileName = "SorollaConfig", menuName = "Palette/Config", order = 1)]
public class SorollaConfig : ScriptableObject
```
**Inheritance:** `System.Object` -> `UnityEngine.Object` -> `UnityEngine.ScriptableObject`

### Fields
#### isPrototypeMode
Prototype = Core SDKs only (GameAnalytics + Facebook, optional MAX/Firebase).
Full = Core + MAX + Adjust + Firebase. Set via the Configuration window.

```csharp title="Declaration"
[Header("Mode")]
[Tooltip("Prototype = Core SDKs only | Full = Core SDKs + MAX + Adjust")]
public bool isPrototypeMode
```
#### rewardedAdUnit
Rewarded ad unit IDs from AppLovin MAX (one per platform).

```csharp title="Declaration"
[Header("MAX Ad Units")]
[Tooltip("Rewarded ad unit IDs per platform")]
public PlatformAdUnitId rewardedAdUnit
```
#### interstitialAdUnit
Interstitial ad unit IDs from AppLovin MAX (one per platform).

```csharp title="Declaration"
[Tooltip("Interstitial ad unit IDs per platform")]
public PlatformAdUnitId interstitialAdUnit
```
#### bannerAdUnit
Banner ad unit IDs from AppLovin MAX (optional, one per platform).

```csharp title="Declaration"
[Tooltip("Banner ad unit IDs per platform (optional)")]
public PlatformAdUnitId bannerAdUnit
```
#### adjustAppToken
Adjust app token from the Adjust Dashboard. Required in Full mode.

```csharp title="Declaration"
[Header("Adjust (Full Mode Only)")]
[Tooltip("Adjust App Token")]
public string adjustAppToken
```
#### adjustSandboxMode
When true, Adjust runs in sandbox environment for testing.
Must be false for production store builds.

```csharp title="Declaration"
[Tooltip("Use Sandbox environment for testing")]
public bool adjustSandboxMode
```
#### adjustPurchaseEventToken
Adjust event token used by `TrackPurchase(System.Double%2cSystem.String%2cSystem.String%2cSystem.String)` for revenue tracking.
Create in Adjust Dashboard -&gt; Events.

```csharp title="Declaration"
[Tooltip("Adjust event token for purchase/revenue tracking (from Adjust Dashboard)")]
public string adjustPurchaseEventToken
```
#### enableTikTok
Master switch for TikTok Business SDK integration.
TikTok is also disabled if `tiktokAppId` is empty for the current platform.

```csharp title="Declaration"
[Header("TikTok (Optional)")]
[Tooltip("Enable TikTok Business SDK integration")]
public bool enableTikTok
```
#### tiktokAppId
TikTok App ID from Events Manager (long numeric ID). Empty = disabled for that platform.

```csharp title="Declaration"
[Tooltip("TikTok App ID from Events Manager (long numeric ID). Leave empty to disable.")]
public PlatformAdUnitId tiktokAppId
```
#### tiktokEmAppId
TikTok Events Manager App ID (maps to the SDK's `appId` parameter).

```csharp title="Declaration"
[Tooltip("App ID from TikTok Events Manager (maps to SDK appId parameter)")]
public PlatformAdUnitId tiktokEmAppId
```
#### tiktokAccessToken
TikTok Events Manager Access Token used by the server-side event API.

```csharp title="Declaration"
[Tooltip("App Secret (Access Token) from Events Manager.")]
public PlatformAdUnitId tiktokAccessToken
```
#### tiktokDebugMode
When true, TikTok SDK logs verbose debug output. MUST be false in distributed builds
or credentials may leak to logcat / device log.

```csharp title="Declaration"
[Tooltip("Enable TikTok SDK debug logging. Do NOT enable in distributed builds.")]
public bool tiktokDebugMode
```

---

## Class PlatformAdUnitId
Platform-specific ad unit ID container.
Use .Current to get the correct ID for the active build target.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
[Serializable]
public class PlatformAdUnitId
```
### Properties
#### Current
Returns the value for the current build target: `ios` on iOS, `android` everywhere else.

```csharp title="Declaration"
public string Current { get; }
```
#### IsConfigured
True when at least one platform value is populated.

```csharp title="Declaration"
public bool IsConfigured { get; }
```
### Fields
#### android
Android platform value (ad unit ID, app ID, access token, etc).

```csharp title="Declaration"
public string android
```
#### ios
iOS platform value (ad unit ID, app ID, access token, etc).

```csharp title="Declaration"
public string ios
```

---

## Enum ProgressionStatus
Progression status for tracking level/stage events.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum ProgressionStatus
```
### Fields
#### Start
Player started the level/stage

```csharp title="Declaration"
Start = 0
```
#### Complete
Player completed the level/stage successfully

```csharp title="Declaration"
Complete = 1
```
#### Fail
Player failed the level/stage

```csharp title="Declaration"
Fail = 2
```

---

## Enum ResourceFlowType
Resource flow type for tracking economy events.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum ResourceFlowType
```
### Fields
#### Source
Player gained resources

```csharp title="Declaration"
Source = 0
```
#### Sink
Player spent resources

```csharp title="Declaration"
Sink = 1
```

---

