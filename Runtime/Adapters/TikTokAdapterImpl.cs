using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     TikTok Business SDK native bridge.
    ///     Android: JNI calls to com.tiktok.TikTokBusinessSdk
    ///     iOS: DllImport to SorollaTikTok.mm ObjC bridge
    ///     Editor: Log-only stub
    /// </summary>
    [Preserve]
    internal class TikTokAdapterImpl : ITikTokAdapter
    {
        const string Tag = "[Palette:TikTok]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        static void Register() => TikTokAdapter.RegisterImpl(new TikTokAdapterImpl());

        public void Initialize(string appId, string tiktokAppId, string accessToken)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Capture Unity-thread-only values before switching to Android UI thread
            var debugBuild = Debug.isDebugBuild;
            // TikTok SDK requires initialization on Android's UI thread
            var runnable = new AndroidJavaRunnable(() =>
            {
                try
                {
                    using var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                        .GetStatic<AndroidJavaObject>("currentActivity");
                    using var app = activity.Call<AndroidJavaObject>("getApplication");
                    using var config = new AndroidJavaObject("com.tiktok.TikTokBusinessSdk$TTConfig", app, accessToken);
                    config.Call<AndroidJavaObject>("setAppId", appId);
                    config.Call<AndroidJavaObject>("setTTAppId", tiktokAppId);
                    if (debugBuild)
                    {
                        config.Call<AndroidJavaObject>("openDebugMode");
                        using var logLevel = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk$LogLevel")
                            .GetStatic<AndroidJavaObject>("DEBUG");
                        config.Call<AndroidJavaObject>("setLogLevel", logLevel);
                    }

                    using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                    sdkClass.CallStatic("initializeSdk", config);
                    sdkClass.CallStatic("startTrack");
                    Debug.Log($"{Tag} Initialized (Android, appId: {appId}, ttAppId: {tiktokAppId})");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"{Tag} Android init failed: {e.Message}");
                }
            });
            using var uiActivity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity");
            uiActivity.Call("runOnUiThread", runnable);
#elif UNITY_IOS && !UNITY_EDITOR
            _SorollaTikTok_Initialize(appId, tiktokAppId, accessToken, Debug.isDebugBuild);
            Debug.Log($"{Tag} Initialized (iOS, appId: {appId}, ttAppId: {tiktokAppId})");
#else
            Debug.Log($"{Tag} Editor mode - init skipped (appId: {appId}, ttAppId: {tiktokAppId})");
#endif
        }

        public void TrackEvent(string eventName)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var eventBuilder = new AndroidJavaClass("com.tiktok.appevents.base.TTBaseEvent")
                    .CallStatic<AndroidJavaObject>("newBuilder", eventName);
                using var ttEvent = eventBuilder.Call<AndroidJavaObject>("build");
                using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                sdkClass.CallStatic("trackTTEvent", ttEvent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} TrackEvent failed: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _SorollaTikTok_TrackEvent(eventName);
#else
            Debug.Log($"{Tag} TrackEvent: {eventName}");
#endif
        }

        public void TrackPurchase(double value, string currency)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var currencyEnum = new AndroidJavaClass(
                    "com.tiktok.appevents.contents.TTContentsEventConstants$Currency");
                using var currencyValue = currencyEnum.GetStatic<AndroidJavaObject>(currency);
                using var eventBuilder = new AndroidJavaClass("com.tiktok.appevents.contents.TTPurchaseEvent")
                    .CallStatic<AndroidJavaObject>("newBuilder");
                eventBuilder.Call<AndroidJavaObject>("setCurrency", currencyValue);
                eventBuilder.Call<AndroidJavaObject>("setValue", value);
                using var ttEvent = eventBuilder.Call<AndroidJavaObject>("build");
                using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                sdkClass.CallStatic("trackTTEvent", ttEvent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} TrackPurchase failed: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _SorollaTikTok_TrackPurchase(value, currency);
#else
            Debug.Log($"{Tag} TrackPurchase: {value} {currency}");
#endif
        }

        public void TrackAdRevenue(double value, string currency, string networkName,
            string adFormat, string adUnitId, string placement)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var props = new AndroidJavaObject("org.json.JSONObject");
                props.Call<AndroidJavaObject>("put", "revenue", value);
                props.Call<AndroidJavaObject>("put", "currency", currency);
                if (networkName != null)
                {
                    props.Call<AndroidJavaObject>("put", "device_ad_mediation_platform", "applovin_max_sdk");
                    props.Call<AndroidJavaObject>("put", "network_name", networkName);
                }
                if (adFormat != null) props.Call<AndroidJavaObject>("put", "ad_format", adFormat);
                if (adUnitId != null) props.Call<AndroidJavaObject>("put", "ad_unit_id", adUnitId);
                if (placement != null) props.Call<AndroidJavaObject>("put", "placement", placement);
                using var eventBuilder = new AndroidJavaClass("com.tiktok.appevents.base.TTAdRevenueEvent")
                    .CallStatic<AndroidJavaObject>("newBuilder", props);
                using var ttEvent = eventBuilder.Call<AndroidJavaObject>("build");
                using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                sdkClass.CallStatic("trackTTEvent", ttEvent);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} TrackAdRevenue failed: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _SorollaTikTok_TrackAdRevenue(value, currency, networkName);
#else
            Debug.Log($"{Tag} TrackAdRevenue: {value} {currency} (network: {networkName})");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _SorollaTikTok_Initialize(string appId, string tiktokAppId, string accessToken, bool debugBuild);
        [DllImport("__Internal")] static extern void _SorollaTikTok_TrackEvent(string eventName);
        [DllImport("__Internal")] static extern void _SorollaTikTok_TrackPurchase(double value, string currency);
        [DllImport("__Internal")] static extern void _SorollaTikTok_TrackAdRevenue(double value, string currency, string networkName);
#endif
    }
}
