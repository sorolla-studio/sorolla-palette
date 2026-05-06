<!-- DO NOT EDIT. This file is generated from XML doc comments in Runtime/*.cs.
     To regenerate: UNITY_PATH=/path/to/Unity/Editor bash Tools~/build-docs.sh
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
#### VerboseLogging
Whether detailed diagnostics are active. Resolved from config + build type.
Always false in non-development builds regardless of config; production-safe
health markers, warnings, and errors are still logged when this is false.

```csharp title="Declaration"
public static bool VerboseLogging { get; }
```
#### ConsentStatus
Current consent status from MAX's UMP integration.
Use this to determine ad loading/showing in GDPR regions.

```csharp title="Declaration"
public static ConsentStatus ConsentStatus { get; }
```
#### AttStatus
iOS App Tracking Transparency authorization status. Returns Authorized on non-iOS platforms and in the Editor.

```csharp title="Declaration"
public static ATTBridge.AuthorizationStatus AttStatus { get; }
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
Shows the code-only Sorolla Vitals debug console. No prefab or sample scene is required.

```csharp title="Declaration"
public static void ShowDebugger()
```
#### HideDebugger()
Hides the code-only Sorolla Vitals debug console.

```csharp title="Declaration"
public static void HideDebugger()
```
#### ToggleDebugger()
Toggles the code-only Sorolla Vitals debug console visibility.

```csharp title="Declaration"
public static void ToggleDebugger()
```
#### GetAttribution(Action&lt;AttributionData?&gt;)
Get Adjust attribution data (network, campaign, tracker).
Returns null to callback if attribution is not yet available.

```csharp title="Declaration"
public static void GetAttribution(Action<AttributionData?> callback)
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `System.Action<System.Nullable<Sorolla.Palette.Adapters.AttributionData>>` | *callback* | Callback with attribution data, or null if unavailable |

#### GetAdjustId(Action&lt;string&gt;)
Get the Adjust device ID (ADID).

```csharp title="Declaration"
public static void GetAdjustId(Action<string> callback)
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `System.Action<System.String>` | *callback* | Callback with the ADID string, or null if unavailable |

#### GetAdvertisingId(Action&lt;string&gt;)
Get the platform advertising ID (GAID on Android, IDFA on iOS).

```csharp title="Declaration"
public static void GetAdvertisingId(Action<string> callback)
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `System.Action<System.String>` | *callback* | Callback with the advertising ID string, or null if unavailable |

#### IsRemoteConfigReady()
Check if Remote Config is ready. Does not require `IsInitialized` -
returns true as soon as the underlying provider (Firebase or GameAnalytics) is ready.

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
#### GetRemoteConfigKeys()
Get all available Remote Config keys from Firebase.
Returns empty if Firebase Remote Config is not installed or not ready.

```csharp title="Declaration"
public static IEnumerable<string> GetRemoteConfigKeys()
```

###### Returns

`System.Collections.Generic.IEnumerable<System.String>`
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

#### ShowInterstitialAd(Action, Action)
Show an interstitial ad. `onComplete` fires after the user dismisses the ad. `onFailed` fires when the ad cannot be shown.

```csharp title="Declaration"
public static void ShowInterstitialAd(Action onComplete, Action onFailed)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Action` | *onComplete* |
| `System.Action` | *onFailed* |

#### ShowMediationDebugger()
Open AppLovin's Mediation Debugger.

```csharp title="Declaration"
public static void ShowMediationDebugger()
```
#### ShowCreativeDebugger()
Open AppLovin's Creative Debugger.

```csharp title="Declaration"
public static void ShowCreativeDebugger()
```
#### AttachPurchaseTracking(StoreController)
Wire Palette purchase tracking to a Unity IAP v5 `StoreController`. Available when Unity IAP is installed.
Call once immediately after `UnityIAPServices.StoreController()`, before `Connect()`.

```csharp title="Declaration"
public static void AttachPurchaseTracking(StoreController store)
```

###### Example

```csharp
_store = UnityIAPServices.StoreController();
Palette.AttachPurchaseTracking(_store);

_store.OnPurchasePending += order =>
{
    GrantRewards(order.CartOrdered);
    _store.ConfirmPurchase(order);
};

await _store.Connect();
```

###### Parameters

| Type | Name | Description |
|:--- |:--- |:--- |
| `UnityEngine.Purchasing.StoreController` | *store* | StoreController returned by `UnityIAPServices.StoreController()` |

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

## Class Palette.Level
Typed level progression tracking. Duration is automatically measured between `Start` and `Complete`/`Fail`.

###### **Assembly**: Sorolla.Runtime.dll

### Methods
#### Start(int, int?, Dictionary&lt;string, object&gt;)
Mark the start of a level.

```csharp title="Declaration"
public static void Start(int level, int? world = null, Dictionary<string, object> extraParams = null)
```

#### Complete(int, int?, int, Dictionary&lt;string, object&gt;)
Mark a level completed. Auto-fills `duration_sec` if `Start` was called.

```csharp title="Declaration"
public static void Complete(int level, int? world = null, int score = 0, Dictionary<string, object> extraParams = null)
```

#### Fail(int, int?, int, Dictionary&lt;string, object&gt;)
Mark a level failed. Auto-fills `duration_sec` if `Start` was called.

```csharp title="Declaration"
public static void Fail(int level, int? world = null, int score = 0, Dictionary<string, object> extraParams = null)
```

###### Example

```csharp
Palette.Level.Start(level: 4, world: 2);
Palette.Level.Complete(level: 4, world: 2, score: 1500);
```

---

## Class Palette.Economy
Typed in-game currency tracking using curated currency and source/sink enums.

###### **Assembly**: Sorolla.Runtime.dll

### Methods
#### Earn(CurrencyId, int, EconomySource, string, Dictionary&lt;string, object&gt;)
Track currency earned.

```csharp title="Declaration"
public static void Earn(CurrencyId currency, int amount, EconomySource source, string itemId = null, Dictionary<string, object> extraParams = null)
```

#### Spend(CurrencyId, int, EconomySink, string, Dictionary&lt;string, object&gt;)
Track currency spent.

```csharp title="Declaration"
public static void Spend(CurrencyId currency, int amount, EconomySink sink, string itemId = null, Dictionary<string, object> extraParams = null)
```

###### Example

```csharp
Palette.Economy.Earn(CurrencyId.Coins, 100, EconomySource.LevelReward, itemId: "level_3");
Palette.Economy.Spend(CurrencyId.Gems, 5, EconomySink.Booster, itemId: "speed_2x");
```

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
Prototype = Core SDKs only (GameAnalytics + Facebook + Firebase).
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
Adjust event token used by SDK-owned purchase tracking after `Palette.AttachPurchaseTracking(store)` is wired.
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
#### verboseLogging
Enables detailed SDK diagnostics and vendor debug logging for QA investigation.
Automatically forced OFF in non-development builds as a safety net.
Production-safe SDK health markers, warnings, and errors are always logged even when this is OFF.

```csharp title="Declaration"
[Header("Logging")]
[Tooltip("Enable detailed SDK diagnostics and vendor debug logs. Forced OFF in release builds; production-safe health logs remain on.")]
public bool verboseLogging
```
#### tiktokDebugMode
When true, TikTok SDK logs verbose debug output. MUST be false in distributed builds
or credentials may leak to logcat / device log.

```csharp title="Declaration"
[Tooltip("Enable TikTok SDK debug logging. Do NOT enable in distributed builds.")]
[Obsolete("Use verboseLogging instead. This field is kept for migration only.")]
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

## Enum CurrencyId
Curated in-game currency identifiers.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum CurrencyId
```
### Fields
`Coins`, `Gems`, `Stars`, `Energy`, `Lives`, `Other`

---

## Enum EconomySource
Curated source categories for currency earned through `Palette.Economy.Earn`.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum EconomySource
```
### Fields
`LevelReward`, `DailyBonus`, `AdReward`, `IapGrant`, `Achievement`, `Gift`, `Starter`, `Other`

---

## Enum EconomySink
Curated sink categories for currency spent through `Palette.Economy.Spend`.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum EconomySink
```
### Fields
`Booster`, `Continue`, `Unlock`, `Cosmetic`, `ShopPurchase`, `Upgrade`, `Other`
---
