#if UNITY_PURCHASING_INSTALLED
using System.Linq;
using Sorolla.Palette.Adapters;
using UnityEngine.Purchasing;

namespace Sorolla.Palette.Purchasing
{
    /// <summary>Primitive purchase fields extracted from a Unity IAP PendingOrder, before any validation.</summary>
    internal readonly struct RawPurchaseData
    {
        public readonly string ProductId;
        public readonly string RawCurrency;
        public readonly decimal RawPrice;
        public readonly string TransactionId;
        public readonly string Receipt;
        public readonly string DedupKey;
        public readonly string StoreEnvironment;

        public RawPurchaseData(string productId, string rawCurrency, decimal rawPrice, string transactionId,
            string receipt, string dedupKey, string storeEnvironment)
        {
            ProductId = productId;
            RawCurrency = rawCurrency;
            RawPrice = rawPrice;
            TransactionId = transactionId;
            Receipt = receipt;
            DedupKey = dedupKey;
            StoreEnvironment = storeEnvironment;
        }
    }

    /// <summary>
    ///     Pure extraction of primitive purchase fields from Unity IAP's PendingOrder object graph.
    ///     No business validation - just field mapping (plus Apple payload diagnostic logging,
    ///     which also only reads the order graph).
    /// </summary>
    internal static class PurchaseOrderAdapter
    {
        const string Tag = "[Palette:PurchaseOrderAdapter]";

        /// <summary>Returns null when the order has no cart/product to extract (nothing to track).</summary>
        public static RawPurchaseData? Extract(PendingOrder order)
        {
            var product = order?.CartOrdered?.Items()?.FirstOrDefault()?.Product;
            if (product == null) return null;

            var md = product.metadata;
            string transactionId = order.Info?.TransactionID;

            return new RawPurchaseData(
                productId: product.definition.id,
                rawCurrency: md?.isoCurrencyCode,
                rawPrice: md?.localizedPrice ?? 0m,
                transactionId: transactionId,
                receipt: order.Info?.Receipt,
                dedupKey: ResolveDedupKey(order, transactionId),
                storeEnvironment: ResolveStoreEnvironment(order));
        }

        // Resolves the stable dedup identity for a purchase. On iOS, StoreKit's
        // OriginalTransactionID points back to the first purchase of a non-consumable, so a
        // restore / re-delivery shares it (while TransactionID is fresh). For consumables each
        // purchase gets its own original, so distinct buys stay distinct. Falls back to
        // transactionId when no original is available (Android, undecodable).
        static string ResolveDedupKey(PendingOrder order, string transactionId)
        {
#if UNITY_IOS
            string original = order?.Info?.Apple?.OriginalTransactionID;
            if (!string.IsNullOrEmpty(original)) return original;
#endif
            return transactionId;
        }

        // Resolves the store's reported environment for the purchase. On iOS, this is decoded
        // from the StoreKit JWS `environment` claim, independent of build type (TestFlight is a
        // release build but transacts against "sandbox"). This is still an unverified client
        // label, not receipt validation. Returns null on Android / other, where there is no
        // reliable client-side sandbox signal (that is a server-side Play Developer API
        // determination), so the event is labelled "unknown" rather than guessed.
        static string ResolveStoreEnvironment(PendingOrder order)
        {
#if UNITY_IOS
            return StoreEnvironmentResolver.DecodeJwsEnvironment(order?.Info?.Apple?.jwsRepresentation);
#else
            return null;
#endif
        }

#if UNITY_IOS
        public static void LogApplePurchasePayload(PendingOrder order, string unifiedReceipt)
        {
            var apple = order?.Info?.Apple;
            if (apple == null)
            {
                PaletteLog.Verbose($"{Tag} Apple payload unavailable.");
                return;
            }

            PaletteLog.Vital($"{Tag} Apple payload " +
                $"unifiedReceipt={DescribePayload(unifiedReceipt)}, " +
                $"appReceipt={DescribePayload(apple.AppReceipt)}, " +
                $"jws={DescribePayload(apple.jwsRepresentation)}, " +
                $"originalTransactionId={PaletteLog.Present(apple.OriginalTransactionID)}, " +
                $"storeName='{apple.StoreName ?? "null"}'.");
        }
#else
        public static void LogApplePurchasePayload(PendingOrder order, string unifiedReceipt) { }
#endif

        static string DescribePayload(string value) =>
            string.IsNullOrEmpty(value) ? "missing" : $"present(len={value.Length})";
    }
}
#endif
