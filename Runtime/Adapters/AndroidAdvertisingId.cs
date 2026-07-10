using System;
using System.Threading;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Android advertising ID (GAID) source for Prototype mode, where Adjust (and its own
    ///     GAID getter) is not compiled in. Unity has no built-in Android getter (dropped in 2020) -
    ///     this is a thin JNI shim over <c>AdvertisingIdClient.getAdvertisingIdInfo</c>, which the
    ///     Play Services dependency already vendors transitively via GA/Firebase. Must run off the
    ///     main thread - the vendor API throws if called on it. The callback body itself only writes
    ///     into <c>SorollaDiagnostics</c>' lock-guarded fields, so invoking it from a background
    ///     thread is safe (DR-134 threading discipline).
    /// </summary>
    static class AndroidAdvertisingId
    {
        internal static void GetGoogleAdId(Action<string> callback)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            new Thread(() =>
            {
                string gaid = null;
                try
                {
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    using var clientClass = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
                    using var info = clientClass.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", activity);
                    bool limitAdTracking = info.Call<bool>("isLimitAdTrackingEnabled");
                    gaid = limitAdTracking ? null : info.Call<string>("getId");
                }
                catch (Exception)
                {
                    // Play Services / AdvertisingIdClient class missing, or the call failed for any
                    // other reason - "unavailable", never crash the caller.
                    gaid = null;
                }

                callback?.Invoke(gaid);
            }) { IsBackground = true }.Start();
#else
            callback?.Invoke(null);
#endif
        }
    }
}
