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

                // Unity does not auto-attach raw C# threads to the JVM - AndroidJavaObject/Class
                // construction throws on an unattached thread. Must attach before any JNI call and
                // always detach before the thread exits, or repeated calls leak attached threads and
                // break the JVM.
                if (AndroidJNI.AttachCurrentThread() != 0)
                {
                    callback?.Invoke(null);
                    return;
                }

                try
                {
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
                }
                finally
                {
                    AndroidJNI.DetachCurrentThread();
                }

                callback?.Invoke(gaid);
            }) { IsBackground = true }.Start();
#else
            callback?.Invoke(null);
#endif
        }
    }
}
