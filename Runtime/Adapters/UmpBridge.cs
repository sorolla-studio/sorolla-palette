using System;
using System.Threading;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#elif UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Scripting;
#endif

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Google UMP, called directly: the consent information update, the consent form when UMP requires
    ///     one, and the privacy options form. UMP writes the user's answer to the IAB TCF record that
    ///     <see cref="IabTcf"/> reads. Every completion runs on the Unity main thread, error or not, and a
    ///     failure only leaves the previous record in place. Editor and other platforms complete at once.
    /// </summary>
    internal static class UmpBridge
    {
        const string Tag = "[Palette:UMP]";

        /// <summary>Consent information update, then the consent form if UMP requires one.</summary>
        internal static void Gather(Action onDone)
        {
#if UNITY_IOS && !UNITY_EDITOR
            s_gathered = OnMainThread("Consent update", onDone);
            _SorollaUMP_Gather(OnGathered);
#elif UNITY_ANDROID && !UNITY_EDITOR
            Action<string> done = OnMainThread("Consent update", onDone);
            RunOnUiThread(done, activity =>
            {
                using var ump = new AndroidJavaClass(Ump + "UserMessagingPlatform");
                using var parameters = new AndroidJavaObject(Ump + "ConsentRequestParameters$Builder");
                ump.CallStatic<AndroidJavaObject>("getConsentInformation", activity).Call("requestConsentInfoUpdate",
                    activity, parameters.Call<AndroidJavaObject>("build"),
                    new Listener(Ump + "ConsentInformation$OnConsentInfoUpdateSuccessListener", _ =>
                    {
                        try
                        {
                            using var form = new AndroidJavaClass(Ump + "UserMessagingPlatform");
                            form.CallStatic("loadAndShowConsentFormIfRequired", activity,
                                new Listener(Ump + "ConsentForm$OnConsentFormDismissedListener", done));
                        }
                        catch (Exception e) { done(e.Message); }
                    }),
                    new Listener(Ump + "ConsentInformation$OnConsentInfoUpdateFailureListener", done));
            });
#else
            onDone?.Invoke();
#endif
        }

        /// <summary>Whether UMP requires a privacy options entry point (the user is in a consent region).</summary>
        internal static bool PrivacyOptionsRequired
        {
            get
            {
                try
                {
#if UNITY_IOS && !UNITY_EDITOR
                    return _SorollaUMP_PrivacyOptionsRequired() != 0;
#elif UNITY_ANDROID && !UNITY_EDITOR
                    using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    using var ump = new AndroidJavaClass(Ump + "UserMessagingPlatform");
                    using var info = ump.CallStatic<AndroidJavaObject>("getConsentInformation", activity);
                    using var status = info.Call<AndroidJavaObject>("getPrivacyOptionsRequirementStatus");
                    return status.Call<string>("name") == "REQUIRED";
#else
                    return false;
#endif
                }
                catch (Exception e)
                {
                    PaletteLog.Verbose($"{Tag} Privacy options status unavailable: {e.Message}");
                    return false;
                }
            }
        }

        /// <summary>UMP's privacy options form, where the user can change their consent answer.</summary>
        internal static void ShowPrivacyOptions(Action onClosed)
        {
#if UNITY_IOS && !UNITY_EDITOR
            s_privacyOptionsClosed = OnMainThread("Privacy options", onClosed);
            _SorollaUMP_ShowPrivacyOptions(OnPrivacyOptionsClosed);
#elif UNITY_ANDROID && !UNITY_EDITOR
            Action<string> done = OnMainThread("Privacy options", onClosed);
            RunOnUiThread(done, activity =>
            {
                using var ump = new AndroidJavaClass(Ump + "UserMessagingPlatform");
                ump.CallStatic("showPrivacyOptionsForm", activity,
                    new Listener(Ump + "ConsentForm$OnConsentFormDismissedListener", done));
            });
#else
            onClosed?.Invoke();
#endif
        }

        // UMP answers on its own thread (the Android UI thread, the iOS main queue). Callers are on the
        // Unity main thread, so post the completion back to the context they called from.
        static Action<string> OnMainThread(string step, Action then)
        {
            SynchronizationContext mainThread = SynchronizationContext.Current;
            return error => mainThread.Post(_ =>
            {
                if (error != null)
                    PaletteLog.Warning($"{Tag} {step} failed: {error}");
                then?.Invoke();
            }, null);
        }

#if UNITY_IOS && !UNITY_EDITOR
        static Action<string> s_gathered;
        static Action<string> s_privacyOptionsClosed;

        delegate void NativeUMPCallback(string error);

        [MonoPInvokeCallback(typeof(NativeUMPCallback))]
        static void OnGathered(string error)
        {
            Action<string> cb = s_gathered;
            s_gathered = null;
            cb?.Invoke(error);
        }

        [MonoPInvokeCallback(typeof(NativeUMPCallback))]
        static void OnPrivacyOptionsClosed(string error)
        {
            Action<string> cb = s_privacyOptionsClosed;
            s_privacyOptionsClosed = null;
            cb?.Invoke(error);
        }

        [DllImport("__Internal")]
        static extern void _SorollaUMP_Gather(NativeUMPCallback callback);

        [DllImport("__Internal")]
        static extern int _SorollaUMP_PrivacyOptionsRequired();

        [DllImport("__Internal")]
        static extern void _SorollaUMP_ShowPrivacyOptions(NativeUMPCallback callback);
#elif UNITY_ANDROID && !UNITY_EDITOR
        const string Ump = "com.google.android.ump.";

        // UMP's forms and consent update run on the UI thread. The activity stays referenced until UMP
        // answers. A synchronous throw (UMP missing from the build) completes with its message.
        static void RunOnUiThread(Action<string> done, Action<AndroidJavaObject> body)
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try { body(activity); }
                    catch (Exception e) { done(e.Message); }
                }));
            }
            catch (Exception e)
            {
                done(e.Message);
            }
        }

        // One proxy type for UMP's three listener interfaces; each instance only receives the method of the
        // interface it was created for. A null FormError is success.
        sealed class Listener : AndroidJavaProxy
        {
            readonly Action<string> _onResult;

            public Listener(string javaInterface, Action<string> onResult) : base(javaInterface)
            {
                _onResult = onResult;
            }

            [Preserve] void onConsentInfoUpdateSuccess() => _onResult(null);
            [Preserve] void onConsentInfoUpdateFailure(AndroidJavaObject error) => _onResult(Message(error));
            [Preserve] void onConsentFormDismissed(AndroidJavaObject error) => _onResult(Message(error));

            static string Message(AndroidJavaObject error) =>
                error == null ? null : error.Call<string>("getMessage") ?? "error without message";
        }
#endif
    }
}
