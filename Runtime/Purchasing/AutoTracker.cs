#if UNITY_PURCHASING_INSTALLED
using System;
using Sorolla.Palette.Adapters;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Sorolla.Palette.Purchasing
{
    /// <summary>
    ///     <b>[Obsolete]</b> Drop-in <see cref="IDetailedStoreListener"/> decorator that auto-tracks every confirmed purchase.
    ///     Unity IAP v5 obsoleted <c>IDetailedStoreListener</c> / <c>IStoreListener</c> / <c>UnityPurchasing.Initialize</c>
    ///     (https://docs.unity.com/en-us/iap/upgrade-to-iap-v5) — this decorator no longer works on v5 projects using
    ///     <c>UnityIAPServices.StoreController</c>. Subscribe to <c>StoreController.OnPurchasePending</c> and call
    ///     <c>Palette.TrackPurchase(pendingOrder)</c> directly. See architecture.md for migration.
    /// </summary>
    [Obsolete("Unity IAP v5 obsoleted IDetailedStoreListener. AutoTracker does not work with UnityIAPServices.StoreController. Subscribe to StoreController.OnPurchasePending and call Palette.TrackPurchase(pendingOrder) directly.")]
    public class AutoTracker : IDetailedStoreListener
    {
        const string Tag = "[Palette:AutoTracker]";

        readonly IDetailedStoreListener _inner;

        public AutoTracker(IDetailedStoreListener inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            try
            {
                #pragma warning disable CS0618 // calling the obsolete overload is intentional inside this obsolete class
                Palette.TrackPurchase(e?.purchasedProduct);
                #pragma warning restore CS0618
            }
            catch (Exception ex)
            {
                PaletteLog.Warning($"{Tag} TrackPurchase failed (forwarding to game listener regardless). Rebuild with verbose logging to inspect purchase details.");
                PaletteLog.Verbose($"{Tag} TrackPurchase failed: {ex.Message}");
            }

            return _inner.ProcessPurchase(e);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions) =>
            _inner.OnInitialized(controller, extensions);

        public void OnInitializeFailed(InitializationFailureReason error) =>
            _inner.OnInitializeFailed(error);

        public void OnInitializeFailed(InitializationFailureReason error, string message) =>
            _inner.OnInitializeFailed(error, message);

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason) =>
            _inner.OnPurchaseFailed(product, failureReason);

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription) =>
            _inner.OnPurchaseFailed(product, failureDescription);
    }
}
#endif
