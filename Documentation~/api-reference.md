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
iOS AppTrackingTransparency authorization status. Returns Authorized on non-iOS / Editor.
Canonical read for game code and debug UI — prefer this over reaching into ATTBridge.

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
#### RemoteConfigStatus
Freshness of the values currently served by the getters. Monotonic within a session:
Defaults -&gt; Cached (previous session's values loaded from disk) -&gt; Live (fetched this
session). Gate anything that must not run on stale balance (A/B bucketing, gameplay
start behind a network wall) on Cached or Live.

```csharp title="Declaration"
public static RemoteConfigStatus RemoteConfigStatus { get; }
```
#### AutoActivateRemoteConfigUpdates
When true (default), real-time Remote Config updates are activated immediately and
`OnRemoteConfigChanged` fires. Set false for games where mid-session value
flips would be jarring; `OnRemoteConfigUpdateAvailable` then fires instead
and the game activates via `ActivateRemoteConfigAsync()` when safe.

```csharp title="Declaration"
public static bool AutoActivateRemoteConfigUpdates { get; set; }
```
### Fields
#### SdkVersion
Package version of the Sorolla Palette SDK.

```csharp title="Declaration"
public const string SdkVersion = "3.18.1"
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
Show an interstitial ad. &lt;code class="paramref"&gt;onComplete&lt;/code&gt; fires after the user
dismisses the ad. &lt;code class="paramref"&gt;onFailed&lt;/code&gt; fires when the ad cannot be
shown (no fill, display error at runtime, ad subsystem unavailable). Exactly
one of the two callbacks fires per call — studios must handle failure to keep
game flow alive when interstitials no-fill.

```csharp title="Declaration"
public static void ShowInterstitialAd(Action onComplete, Action onFailed)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Action` | *onComplete* |
| `System.Action` | *onFailed* |

#### ShowMediationDebugger()
Opens AppLovin's Mediation Debugger — an in-app modal listing every
integrated ad network, its adapter SDK version, config status, and
a per-network "Live Test Ads" button to force end-to-end delivery
from each network. Canonical tool for verifying ad-network wiring.

```csharp title="Declaration"
public static void ShowMediationDebugger()
```
#### ShowCreativeDebugger()
Opens AppLovin's Creative Debugger. While enabled, long-pressing a
displayed ad overlays its network, ad unit, bid price, and creative
ID — diagnostic for "why did that specific ad show" questions.

```csharp title="Declaration"
public static void ShowCreativeDebugger()
```
#### WaitForRemoteConfig(float, RemoteConfigStatus)
Completes true as soon as `RemoteConfigStatus` reaches
&lt;code class="paramref"&gt;minStatus&lt;/code&gt;, or false after &lt;code class="paramref"&gt;timeoutSeconds&lt;/code&gt;
(a timeout of 0 or less waits indefinitely).
Typical gate before gameplay start: `await Palette.WaitForRemoteConfig(5f)`.
Devices that have fetched before pass instantly via the disk cache.

```csharp title="Declaration"
public static Task<bool> WaitForRemoteConfig(float timeoutSeconds = 5, RemoteConfigStatus minStatus = RemoteConfigStatus.Cached)
```

###### Returns

`System.Threading.Tasks.Task<System.Boolean>`

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Single` | *timeoutSeconds* |
| `RemoteConfigStatus` | *minStatus* |

#### GetRemoteConfig(string, string)
Get Remote Config string value. Resolution order, identical for every type:
Firebase (remote, cached, or registered in-app default) -&gt; GameAnalytics -&gt;
defaults registered via `SetRemoteConfigDefaults(System.Collections.Generic.Dictionary%7bSystem.String%2cSystem.Object%7d)` -&gt; &lt;code class="paramref"&gt;defaultValue&lt;/code&gt;.

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
Get Remote Config int value. Decimal values truncate toward zero.
See `GetRemoteConfig(System.String%2cSystem.String)` for resolution order.

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
Get Remote Config float value. See `GetRemoteConfig(System.String%2cSystem.String)` for resolution order.

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
Get Remote Config bool value. Accepts true/false, 1/0, yes/no, on/off (case-insensitive)
on every tier. See `GetRemoteConfig(System.String%2cSystem.String)` for resolution order.

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
Register in-app defaults. Works before or after initialization; values are served
when no fetched or cached value exists for a key, on every provider tier.
Also registered with Firebase so dashboard `useInAppDefault` parameters resolve to them.

```csharp title="Declaration"
public static void SetRemoteConfigDefaults(Dictionary<string, object> defaults)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *defaults* |

#### ActivateRemoteConfigAsync()
Manually activate fetched Remote Config values.
Use when `AutoActivateRemoteConfigUpdates` is false.
Returns true when new values were activated (`OnRemoteConfigChanged`
fires); false when there was nothing new to apply or activation failed.

```csharp title="Declaration"
public static Task<bool> ActivateRemoteConfigAsync()
```

###### Returns

`System.Threading.Tasks.Task<System.Boolean>`
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
#### OnRemoteConfigChanged
Fired whenever the served values may have changed: first cached load, fetch
activation, real-time update, or GameAnalytics configs becoming ready.
The collection holds the updated keys when known, and is empty when the change
is unspecified (re-read everything you care about).
If values are already readable when you subscribe, the handler fires immediately -
late subscribers never miss the initial load.

```csharp title="Declaration"
public static event Action<IReadOnlyCollection<string>> OnRemoteConfigChanged
```
###### Event Type
`System.Action<System.Collections.Generic.IReadOnlyCollection<System.String>>`
#### OnRemoteConfigUpdateAvailable
Fired when a real-time update arrived but was NOT activated because
`AutoActivateRemoteConfigUpdates` is false. Call
`ActivateRemoteConfigAsync()` at a safe moment (between rounds) to apply it;
`OnRemoteConfigChanged` then fires as usual.

```csharp title="Declaration"
public static event Action<IReadOnlyCollection<string>> OnRemoteConfigUpdateAvailable
```
###### Event Type
`System.Action<System.Collections.Generic.IReadOnlyCollection<System.String>>`

---

## Class Palette.Level
Typed level progression tracking. Auto-tracks duration between `Sorolla.Palette.Palette.Level.Start(System.Int32%2cSystem.Nullable%7bSystem.Int32%7d%2cSystem.Collections.Generic.Dictionary%7bSystem.String%2cSystem.Object%7d)`
and `Sorolla.Palette.Palette.Level.Complete(System.Int32%2cSystem.Nullable%7bSystem.Int32%7d%2cSystem.Int32%2cSystem.Collections.Generic.Dictionary%7bSystem.String%2cSystem.Object%7d)`/`Sorolla.Palette.Palette.Level.Fail(System.Int32%2cSystem.Nullable%7bSystem.Int32%7d%2cSystem.Int32%2cSystem.Collections.Generic.Dictionary%7bSystem.String%2cSystem.Object%7d)`. Wire format:
`level_name = "world_{W}_level_{L}"` when world is supplied, else `"level_{L}"`.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public static class Palette.Level
```
### Methods
#### Start(int, int?, Dictionary&lt;string, object&gt;)
Mark the start of a level. Fires level_start (Firebase) and records start time for auto-duration.

```csharp title="Declaration"
public static void Start(int level, int? world = null, Dictionary<string, object> extraParams = null)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Int32` | *level* |
| `System.Nullable<System.Int32>` | *world* |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *extraParams* |

#### Complete(int, int?, int, Dictionary&lt;string, object&gt;)
Mark a level completed. Fires level_end{success=1}, auto-fills duration_sec if Start was called.

```csharp title="Declaration"
public static void Complete(int level, int? world = null, int score = 0, Dictionary<string, object> extraParams = null)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Int32` | *level* |
| `System.Nullable<System.Int32>` | *world* |
| `System.Int32` | *score* |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *extraParams* |

#### Fail(int, int?, int, Dictionary&lt;string, object&gt;)
Mark a level failed. Fires level_end{success=0}, auto-fills duration_sec if Start was called.

```csharp title="Declaration"
public static void Fail(int level, int? world = null, int score = 0, Dictionary<string, object> extraParams = null)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `System.Int32` | *level* |
| `System.Nullable<System.Int32>` | *world* |
| `System.Int32` | *score* |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *extraParams* |


---

## Class Palette.Economy
Typed economy tracking. Curated `CurrencyId` + `EconomySource`
/ `EconomySink` so cross-game analytics aggregate correctly and typos are impossible.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public static class Palette.Economy
```
### Methods
#### Earn(CurrencyId, int, EconomySource, string, Dictionary&lt;string, object&gt;)
Track currency earned. Fires earn_virtual_currency (Firebase) / GameAnalytics Source event.

```csharp title="Declaration"
public static void Earn(CurrencyId currency, int amount, EconomySource source, string itemId = null, Dictionary<string, object> extraParams = null)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `CurrencyId` | *currency* |
| `System.Int32` | *amount* |
| `EconomySource` | *source* |
| `System.String` | *itemId* |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *extraParams* |

#### Spend(CurrencyId, int, EconomySink, string, Dictionary&lt;string, object&gt;)
Track currency spent. Fires spend_virtual_currency (Firebase) / GameAnalytics Sink event.

```csharp title="Declaration"
public static void Spend(CurrencyId currency, int amount, EconomySink sink, string itemId = null, Dictionary<string, object> extraParams = null)
```

###### Parameters

| Type | Name |
|:--- |:--- |
| `CurrencyId` | *currency* |
| `System.Int32` | *amount* |
| `EconomySink` | *sink* |
| `System.String` | *itemId* |
| `System.Collections.Generic.Dictionary<System.String,System.Object>` | *extraParams* |


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
Adjust event token used by `TrackPurchase(System.Double%2cSystem.String%2cSystem.String%2cSystem.String%2cSystem.String%2cSystem.String%2cSystem.String)` for revenue tracking.
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
In-game currency. Curated by Sorolla. Add a new value via SDK PR when a game
introduces a currency that isn't listed - fails at compile time rather than
silently fragmenting analytics with typo'd strings.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum CurrencyId
```
### Fields
#### Coins


```csharp title="Declaration"
Coins = 0
```
#### Gems


```csharp title="Declaration"
Gems = 1
```
#### Stars


```csharp title="Declaration"
Stars = 2
```
#### Energy


```csharp title="Declaration"
Energy = 3
```
#### Lives


```csharp title="Declaration"
Lives = 4
```
#### Other


```csharp title="Declaration"
Other = 5
```

---

## Enum EconomySource
Source category for `Sorolla.Palette.Palette.Economy.Earn(Sorolla.Palette.CurrencyId%2cSystem.Int32%2cSorolla.Palette.EconomySource%2cSystem.String%2cSystem.Collections.Generic.Dictionary%7bSystem.String%2cSystem.Object%7d)`. Curated by Sorolla so
cross-game analytics aggregate correctly. Use `Other` if no existing
category fits - logs a warning so the taxonomy can be extended in a patch release.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum EconomySource
```
### Fields
#### LevelReward


```csharp title="Declaration"
LevelReward = 0
```
#### DailyBonus


```csharp title="Declaration"
DailyBonus = 1
```
#### AdReward


```csharp title="Declaration"
AdReward = 2
```
#### IapGrant


```csharp title="Declaration"
IapGrant = 3
```
#### Achievement


```csharp title="Declaration"
Achievement = 4
```
#### Gift


```csharp title="Declaration"
Gift = 5
```
#### Starter


```csharp title="Declaration"
Starter = 6
```
#### Other


```csharp title="Declaration"
Other = 7
```

---

## Enum EconomySink
Sink category for `Sorolla.Palette.Palette.Economy.Spend(Sorolla.Palette.CurrencyId%2cSystem.Int32%2cSorolla.Palette.EconomySink%2cSystem.String%2cSystem.Collections.Generic.Dictionary%7bSystem.String%2cSystem.Object%7d)`. Curated by Sorolla so
cross-game analytics aggregate correctly. Use `Other` if no existing
category fits - logs a warning so the taxonomy can be extended in a patch release.

###### **Assembly**: Sorolla.Runtime.dll

```csharp title="Declaration"
public enum EconomySink
```
### Fields
#### Booster


```csharp title="Declaration"
Booster = 0
```
#### Continue


```csharp title="Declaration"
Continue = 1
```
#### Unlock


```csharp title="Declaration"
Unlock = 2
```
#### Cosmetic


```csharp title="Declaration"
Cosmetic = 3
```
#### ShopPurchase


```csharp title="Declaration"
ShopPurchase = 4
```
#### Upgrade


```csharp title="Declaration"
Upgrade = 5
```
#### Other


```csharp title="Declaration"
Other = 6
```

---
