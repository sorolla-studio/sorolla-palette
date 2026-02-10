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
            try
            {
                using var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");
                using var app = activity.Call<AndroidJavaObject>("getApplication");
                // TTConfig is inner class of TikTokBusinessSdk
                using var config = new AndroidJavaObject("com.tiktok.TikTokBusinessSdk$TTConfig", app);
                config.Call<AndroidJavaObject>("setAppId", appId);
                config.Call<AndroidJavaObject>("setTTAppId", tiktokAppId);
                // autoStart defaults to true — no call needed

                using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                sdkClass.CallStatic("initializeSdk", config);
                Debug.Log($"{Tag} Initialized (Android, appId: {appId}, ttAppId: {tiktokAppId})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} Android init failed: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _SorollaTikTok_Initialize(appId, tiktokAppId, accessToken);
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
                using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                sdkClass.CallStatic("trackEvent", eventName);
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
                using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                using var props = new AndroidJavaObject("org.json.JSONObject");
                props.Call<AndroidJavaObject>("put", "value", value.ToString("F6"));
                props.Call<AndroidJavaObject>("put", "currency", currency);
                sdkClass.CallStatic("trackEvent", "Purchase", props);
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

        public void TrackAdRevenue(double value, string currency)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var sdkClass = new AndroidJavaClass("com.tiktok.TikTokBusinessSdk");
                using var props = new AndroidJavaObject("org.json.JSONObject");
                props.Call<AndroidJavaObject>("put", "value", value.ToString("F6"));
                props.Call<AndroidJavaObject>("put", "currency", currency);
                sdkClass.CallStatic("trackEvent", "ImpressionLevelAdRevenue", props);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{Tag} TrackAdRevenue failed: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            _SorollaTikTok_TrackAdRevenue(value, currency);
#else
            Debug.Log($"{Tag} TrackAdRevenue: {value} {currency}");
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _SorollaTikTok_Initialize(string appId, string tiktokAppId, string accessToken);
        [DllImport("__Internal")] static extern void _SorollaTikTok_TrackEvent(string eventName);
        [DllImport("__Internal")] static extern void _SorollaTikTok_TrackPurchase(double value, string currency);
        [DllImport("__Internal")] static extern void _SorollaTikTok_TrackAdRevenue(double value, string currency);
#endif
    }
}
