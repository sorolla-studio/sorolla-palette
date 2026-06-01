using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sorolla.Palette.Adapters;
using Sorolla.Palette.Purchasing;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif
#if UNITY_PURCHASING_INSTALLED
using UnityEngine.Purchasing;
#endif

namespace Sorolla.Palette
{
    public static partial class Palette
    {
#if UNITY_PURCHASING_INSTALLED
        /// <summary>
        ///     <b>Canonical Unity IAP v5 path.</b> Track a purchase from a <see cref="PendingOrder"/>
        ///     received in <c>StoreController.OnPurchasePending</c>.
        ///     Call this <b>before</b> <c>StoreController.ConfirmPurchase(order)</c> — per Unity IAP v5.2
        ///     (https://docs.unity3d.com/Packages/com.unity.purchasing@5.2/api/UnityEngine.Purchasing.IOrderInfo.html),
        ///     for consumables <c>Order.Info.TransactionID</c> and <c>Order.Info.Receipt</c> are cleared after confirmation.
        ///     Tracking at OnPurchasePending is the only point that captures transactionId reliably on consumables.
        /// </summary>
        /// <param name="order">PendingOrder from <c>StoreController.OnPurchasePending</c>.</param>
        /// <remarks>
        ///     Internal since 3.14.1 — <see cref="AttachPurchaseTracking"/> is the only supported integration path
        ///     and subscribes this method to <c>OnPurchasePending</c> on the studio's behalf.
        /// </remarks>
        internal static void TrackPurchase(PendingOrder order)
        {
            if (order == null)
            {
                PaletteLog.Warning($"{Tag} TrackPurchase(PendingOrder): null order - skipping.");
                return;
            }

            var product = order.CartOrdered?.Items()?.FirstOrDefault()?.Product;
            if (product == null)
            {
                PaletteLog.Warning($"{Tag} TrackPurchase(PendingOrder): empty cart - skipping.");
                return;
            }

            var md = product.metadata;
            string rawCurrency = md?.isoCurrencyCode;
            decimal rawPrice = md?.localizedPrice ?? 0m;
            string productId = product.definition.id;
            string transactionId = order.Info?.TransactionID;
            string receipt = order.Info?.Receipt;

            LogApplePurchasePayload(order, receipt);

            // Defensive: Firebase strips `value` when `currency` is non-ISO (firebase_error=19,
            // error_value="currency" observed in BQ); MMPs reject non-ISO outright. Drop rather
            // than forward corrupt revenue.
            if (rawPrice <= 0m || !IsIso4217(rawCurrency))
            {
                PaletteLog.Error($"{Tag} TrackPurchase(PendingOrder): invalid metadata - " +
                    $"product_id='{productId}', localizedPrice={rawPrice}, isoCurrencyCode='{rawCurrency}'. " +
                    $"Dropping event.");
#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackEvent("sorolla_purchase_data_quality_failure", new Dictionary<string, object>
                {
                    { "reason", rawPrice <= 0m ? "non_positive_price" : "non_iso_currency" },
                    { "raw_currency", rawCurrency ?? "null" },
                    { "raw_price", (double)rawPrice },
                    { "product_id", productId ?? "null" },
                    { "platform", Application.platform.ToString() },
                    { "source", "pending_order" },
                });
#endif
                return;
            }

            // Android purchaseToken via unified-receipt parse; iOS path doesn't need this.
            string purchaseToken = null;
            if (!string.IsNullOrEmpty(receipt))
            {
                ParsedReceipt parsed = ReceiptParser.Parse(receipt);
                purchaseToken = parsed.PurchaseToken;
            }

            TrackPurchase(
                amount:        (double)rawPrice,
                currency:      rawCurrency,
                productId:     productId,
                transactionId: transactionId,
                purchaseToken: purchaseToken);
        }

        /// <summary>
        ///     <b>[Obsolete]</b> Track a purchase from a legacy Unity IAP <see cref="Product"/>.
        ///     Unity IAP v5 marks <c>Product.transactionID</c> and <c>Product.receipt</c> as
        ///     <see cref="System.ObsoleteAttribute">Obsolete</see>. On consumable products those fields
        ///     are empty after <c>StoreController.ConfirmPurchase</c>, causing missing transaction_id in
        ///     Firebase and breaking MMP deduplication. Use <see cref="TrackPurchase(PendingOrder)"/> instead.
        /// </summary>
        [Obsolete("Unity IAP v5 obsoleted Product.transactionID and Product.receipt. Use Palette.AttachPurchaseTracking(store) — it subscribes to StoreController.OnPurchasePending internally. Internal since 3.14.1; retained only for the legacy AutoTracker shim.")]
        internal static void TrackPurchase(Product product)
        {
            if (product == null)
            {
                PaletteLog.Warning($"{Tag} TrackPurchase(Product): null product - skipping.");
                return;
            }

            var md = product.metadata;
            string rawCurrency = md?.isoCurrencyCode;
            decimal rawPrice = md?.localizedPrice ?? 0m;
            string productId = product.definition.id;

            if (rawPrice <= 0m || !IsIso4217(rawCurrency))
            {
                PaletteLog.Error($"{Tag} TrackPurchase(Product): invalid metadata - " +
                    $"product_id='{productId}', localizedPrice={rawPrice}, isoCurrencyCode='{rawCurrency}'. " +
                    $"Dropping event. Migrate to TrackPurchase(PendingOrder) for Unity IAP v5.");
#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackEvent("sorolla_purchase_data_quality_failure", new Dictionary<string, object>
                {
                    { "reason", rawPrice <= 0m ? "non_positive_price" : "non_iso_currency" },
                    { "raw_currency", rawCurrency ?? "null" },
                    { "raw_price", (double)rawPrice },
                    { "product_id", productId ?? "null" },
                    { "platform", Application.platform.ToString() },
                    { "source", "product_legacy" },
                });
#endif
                return;
            }

            #pragma warning disable CS0618 // Legacy v4 fields — intentional in this obsolete overload
            ParsedReceipt parsed = ReceiptParser.Parse(product.receipt);
            string txId = !string.IsNullOrEmpty(product.transactionID) ? product.transactionID : parsed.TransactionId;
            #pragma warning restore CS0618

            TrackPurchase(
                amount:        (double)rawPrice,
                currency:      rawCurrency,
                productId:     productId,
                transactionId: txId,
                purchaseToken: parsed.PurchaseToken);
        }
#endif

        // Session-scoped TxID dedup for the entire TrackPurchase fan-out. Unity IAP v5 can
        // fire OnPurchasePending more than once (Unity-documented crash-replay), and the
        // PendingOrder overload funnels here after metadata validation. Internal since
        // 3.14.1 — the only reachable callers are the SDK-owned AttachPurchaseTracking
        // subscription and the legacy [Obsolete] AutoTracker/Product shims; studios have
        // no way to invoke this path directly, so duplicate analytics are structurally
        // impossible.
        static readonly HashSet<string> s_processedTxIds = new HashSet<string>();

        /// <summary>
        ///     Low-level purchase fan-out. Internal since 3.14.1 — no supported studio-facing entry point.
        ///     Reached only via the SDK-owned subscription installed by <see cref="AttachPurchaseTracking"/>
        ///     and the legacy Obsolete shims. Enforces ISO-4217 / positive-price validation and TxID dedup
        ///     before fanning out to Adjust / TikTok / Firebase / GameAnalytics.
        /// </summary>
        /// <param name="amount">Purchase amount in local currency (must be &gt; 0).</param>
        /// <param name="currency">ISO 4217 currency code.</param>
        /// <param name="productId">Store product ID.</param>
        /// <param name="transactionId">Transaction ID. Non-empty values are deduped session-wide.</param>
        /// <param name="purchaseToken">Google Play purchase token (Android verification only).</param>
        internal static void TrackPurchase(double amount, string currency = "USD",
            string productId = null, string transactionId = null, string purchaseToken = null)
        {
            if (amount <= 0)
            {
                PaletteLog.Warning($"{Tag} TrackPurchase: non-positive amount ({amount}) - dropping event. " +
                    $"Pass the local price paid (e.g. Product.metadata.localizedPrice), not a tier index. " +
                    $"Recommended: Palette.AttachPurchaseTracking(store), which derives this automatically from Unity IAP.");
                return;
            }
            if (!IsIso4217(currency))
            {
                // Firebase strips `value` server-side on non-ISO currency (observed:
                // firebase_error=19, error_value="currency" in BQ), and MMPs reject
                // outright. Drop rather than forward corrupt revenue.
                PaletteLog.Error($"{Tag} TrackPurchase: currency '{currency}' is not ISO 4217 - dropping event. " +
                    $"Pass Product.metadata.isoCurrencyCode or use Palette.AttachPurchaseTracking(store).");
#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackEvent("sorolla_purchase_data_quality_failure", new Dictionary<string, object>
                {
                    { "reason", "non_iso_currency_lowlevel" },
                    { "raw_currency", currency ?? "null" },
                    { "amount", amount },
                    { "product_id", productId ?? "null" },
                });
#endif
                return;
            }

            // Validation passed: now enforce TxID dedup so analytics fan-out fires at most once per purchase.
            // Placed AFTER validation so a bad-payload first call doesn't burn the TxID slot for a corrected retry.
            if (!string.IsNullOrEmpty(transactionId) && !s_processedTxIds.Add(transactionId))
            {
                PaletteLog.Warning($"{Tag} TrackPurchase: duplicate transactionId detected - dropping duplicate purchase event. " +
                    $"Unity IAP v5 can fire purchase callbacks twice (Google Play in-session double on OnPurchaseConfirmed, " +
                    $"or OnPurchasePending crash-replay per Unity docs). Session-wide dedup is enforced here so " +
                    $"analytics fan-out fires once per TxID.");
                return;
            }

            PaletteLog.Vital($"{Tag} TrackPurchase: accepted product_id='{productId ?? "unknown"}', currency='{currency}', transactionId={PaletteLog.Present(transactionId)}, purchaseToken={PaletteLog.Present(purchaseToken)}.");

            QueueOrExecute(() =>
            {
                SorollaDiagnostics.RecordEventDispatch("purchase", "purchase", new Dictionary<string, object>
                {
                    { "product_id", productId ?? "unknown" },
                    { "value", amount },
                    { "currency", currency },
                    { "transaction_id", transactionId },
                    { "purchase_token", purchaseToken },
                });

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
                if (!string.IsNullOrEmpty(Config?.adjustPurchaseEventToken))
                    AdjustAdapter.TrackPurchase(Config.adjustPurchaseEventToken, amount, currency,
                        productId, transactionId, purchaseToken);
                else
                    PaletteLog.WarningOnce("purchase.adjust.token_missing", $"{Tag} TrackPurchase: Adjust purchase event token not configured; Adjust purchase revenue skipped.");
#endif

                if (Config != null && Config.enableTikTok && !string.IsNullOrEmpty(Config.tiktokAppId?.Current))
                    TikTokAdapter.TrackPurchase(amount, currency);

#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackPurchase(productId, amount, currency, transactionId);
#endif

#if GAMEANALYTICS_INSTALLED
                // GA expects amount in cents (100x display price across all currencies, incl. JPY).
                // Math.Round avoids floating-point truncation (0.99 * 100 = 98.99999 -> 98 without rounding).
                int amountInCents = (int)Math.Round(amount * 100);
                string gaItemId = string.IsNullOrEmpty(productId) ? "unknown" : productId;
                GameAnalyticsAdapter.TrackBusinessEvent(currency, amountInCents, "iap", gaItemId, null);
#endif
            });
        }

        static bool IsIso4217(string c) =>
            !string.IsNullOrEmpty(c) && c.Length == 3
            && char.IsLetter(c[0]) && char.IsLetter(c[1]) && char.IsLetter(c[2]);

#if UNITY_PURCHASING_INSTALLED && UNITY_IOS
        static void LogApplePurchasePayload(PendingOrder order, string unifiedReceipt)
        {
            var apple = order?.Info?.Apple;
            if (apple == null)
            {
                PaletteLog.Verbose($"{Tag} TrackPurchase(PendingOrder): Apple payload unavailable.");
                return;
            }

            PaletteLog.Vital($"{Tag} TrackPurchase(PendingOrder): Apple payload " +
                $"unifiedReceipt={DescribePayload(unifiedReceipt)}, " +
                $"appReceipt={DescribePayload(apple.AppReceipt)}, " +
                $"jws={DescribePayload(apple.jwsRepresentation)}, " +
                $"originalTransactionId={PaletteLog.Present(apple.OriginalTransactionID)}, " +
                $"storeName='{apple.StoreName ?? "null"}'.");
        }
#else
        static void LogApplePurchasePayload(object order, string unifiedReceipt) { }
#endif

        static string DescribePayload(string value) =>
            string.IsNullOrEmpty(value) ? "missing" : $"present(len={value.Length})";
    }
}
