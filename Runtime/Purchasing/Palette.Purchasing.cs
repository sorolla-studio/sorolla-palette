#if UNITY_PURCHASING_INSTALLED
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Sorolla.Palette
{
    public static partial class Palette
    {
        const string PurchasingTag = "[Palette:Purchasing]";

        // Tracks which StoreControllers have already been wired this session so a second
        // AttachPurchaseTracking call is a no-op instead of double-subscribing the event.
        static readonly HashSet<StoreController> s_attachedStores = new HashSet<StoreController>();

        /// <summary>
        ///     Wire Palette purchase tracking to a Unity IAP v5 <see cref="StoreController"/> in a single call.
        ///     Call once immediately after <c>UnityIAPServices.StoreController()</c>, before <c>Connect()</c>.
        ///     Idempotent: subsequent calls with the same controller are no-ops.
        /// </summary>
        /// <remarks>
        ///     Subscribes <c>OnPurchasePending += Palette.TrackPurchase</c> on the SDK's behalf so analytics fan-out
        ///     is guaranteed even if the studio forgets the wiring. Studio fulfillment handlers (GrantRewards,
        ///     <c>ConfirmPurchase</c>, popups) can still subscribe to <c>OnPurchasePending</c> / <c>OnPurchaseConfirmed</c>
        ///     independently — this method only adds the analytics subscription.
        ///
        ///     TxID dedup is enforced inside <see cref="TrackPurchase(double, string, string, string, string)"/>
        ///     itself, so duplicate callbacks from Google Play or crash-replay cannot produce duplicate analytics events.
        /// </remarks>
        /// <param name="store">StoreController returned by <c>UnityIAPServices.StoreController()</c>.</param>
        /// <example>
        /// <code>
        /// _store = UnityIAPServices.StoreController();
        /// Palette.AttachPurchaseTracking(_store);     // analytics wired — unmissable
        ///
        /// _store.OnPurchasePending += order =>        // studio-owned fulfillment
        /// {
        ///     GrantRewards(order.CartOrdered);
        ///     _store.ConfirmPurchase(order);
        /// };
        /// await _store.Connect();
        /// </code>
        /// </example>
        public static void AttachPurchaseTracking(StoreController store)
        {
            if (store == null)
            {
                Debug.LogWarning($"{PurchasingTag} AttachPurchaseTracking: null StoreController - skipping.");
                return;
            }
            if (!s_attachedStores.Add(store))
            {
                Debug.LogWarning($"{PurchasingTag} AttachPurchaseTracking: StoreController already attached - skipping duplicate.");
                return;
            }
            store.OnPurchasePending += TrackPurchase;
            Debug.Log($"{PurchasingTag} AttachPurchaseTracking: wired OnPurchasePending -> Palette.TrackPurchase.");
        }
    }
}
#endif
