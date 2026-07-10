using System;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     iOS advertising ID source for Prototype mode, where Adjust (and its IDFA getter) is not
    ///     compiled in. Wraps Unity's built-in <c>Application.RequestAdvertisingIdentifierAsync</c> -
    ///     still present in Unity 6, iOS-only (Unity dropped Android support in 2020; see
    ///     <see cref="AndroidAdvertisingId"/> for the Android path). Full mode keeps using
    ///     <c>AdjustAdapter.GetIdfa</c> - this type is never called when Adjust is compiled in.
    /// </summary>
    static class PrototypeAdvertisingId
    {
        internal static void GetIdfa(Action<string> callback)
        {
#if UNITY_IOS && !UNITY_EDITOR
            bool started = Application.RequestAdvertisingIdentifierAsync((advertisingId, trackingEnabled, error) =>
            {
                callback?.Invoke(trackingEnabled ? advertisingId : null);
            });

            if (!started)
                callback?.Invoke(null);
#else
            callback?.Invoke(null);
#endif
        }
    }
}
