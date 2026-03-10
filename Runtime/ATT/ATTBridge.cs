using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

namespace Sorolla.Palette.ATT
{
    /// <summary>
    ///     Native ATT bridge. Replaces com.unity.ads.ios-support with direct [DllImport] calls.
    ///     On non-iOS or in Editor, returns Authorized / no-ops.
    /// </summary>
    public static class ATTBridge
    {
        public enum AuthorizationStatus
        {
            NotDetermined = 0,
            Restricted = 1,
            Denied = 2,
            Authorized = 3
        }

        public static AuthorizationStatus GetStatus()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return (AuthorizationStatus)_SorollaATT_GetStatus();
#else
            return AuthorizationStatus.Authorized;
#endif
        }

        public static void RequestAuthorization(Action<AuthorizationStatus> callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            s_callback = callback;
            _SorollaATT_RequestAuthorization(OnNativeResponse);
#else
            callback?.Invoke(AuthorizationStatus.Authorized);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static Action<AuthorizationStatus> s_callback;

        delegate void NativeATTCallback(int status);

        [MonoPInvokeCallback(typeof(NativeATTCallback))]
        static void OnNativeResponse(int status)
        {
            var cb = s_callback;
            s_callback = null;
            cb?.Invoke((AuthorizationStatus)status);
        }

        [DllImport("__Internal")]
        static extern int _SorollaATT_GetStatus();

        [DllImport("__Internal")]
        static extern void _SorollaATT_RequestAuthorization(NativeATTCallback callback);
#endif
    }
}
