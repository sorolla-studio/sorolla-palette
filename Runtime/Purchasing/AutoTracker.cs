#if UNITY_PURCHASING_INSTALLED
using System;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Sorolla.Palette.Purchasing
{
    /// <summary>
    ///     Drop-in <see cref="IDetailedStoreListener"/> decorator that auto-tracks every confirmed purchase via <see cref="Palette.TrackPurchase(Product)"/>.
    ///     Wrap your existing listener once at <c>UnityPurchasing.Initialize</c> and every purchase is tracked with correct amount, currency,
    ///     productId, transactionId, and (on Android) purchaseToken - no per-purchase code required.
    /// </summary>
    /// <example>
    /// <code>
    /// // Before:
    /// UnityPurchasing.Initialize(this, builder);
    ///
    /// // After (single-line change):
    /// UnityPurchasing.Initialize(new Palette.Purchasing.AutoTracker(this), builder);
    /// </code>
    /// </example>
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
                Palette.TrackPurchase(e?.purchasedProduct);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} TrackPurchase failed (forwarding to game listener regardless): {ex.Message}");
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
