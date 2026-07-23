using System;
using System.Collections.Generic;
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
        ///     Call this <b>before</b> <c>StoreController.ConfirmPurchase(order)</c> - per Unity IAP v5.2
        ///     (https://docs.unity3d.com/Packages/com.unity.purchasing@5.2/api/UnityEngine.Purchasing.IOrderInfo.html),
        ///     for consumables <c>Order.Info.TransactionID</c> and <c>Order.Info.Receipt</c> are cleared after confirmation.
        ///     Tracking at OnPurchasePending is the only point that captures transactionId reliably on consumables.
        /// </summary>
        /// <param name="order">PendingOrder from <c>StoreController.OnPurchasePending</c>.</param>
        /// <remarks>
        ///     Internal since 3.14.1 - <see cref="AttachPurchaseTracking"/> is the only supported integration path
        ///     and subscribes this method to <c>OnPurchasePending</c> on the studio's behalf.
        /// </remarks>
        internal static void TrackPurchase(PendingOrder order)
        {
            if (order == null)
            {
                PaletteLog.Warning($"{Tag} TrackPurchase(PendingOrder): null order - skipping.");
                return;
            }

            RawPurchaseData? raw = PurchaseOrderAdapter.Extract(order);
            if (raw == null)
            {
                PaletteLog.Warning($"{Tag} TrackPurchase(PendingOrder): empty cart - skipping.");
                return;
            }

            PurchaseOrderAdapter.LogApplePurchasePayload(order, raw.Value.Receipt);

            // Defensive: Firebase strips `value` when `currency` is non-ISO (firebase_error=19,
            // error_value="currency" observed in BQ); MMPs reject non-ISO outright. Drop rather
            // than forward corrupt revenue.
            if (raw.Value.RawPrice <= 0m || !IsIso4217(raw.Value.RawCurrency))
            {
                SorollaDiagnostics.RecordPurchaseDropped(
                    $"TrackPurchase(PendingOrder): invalid metadata for product_id='{raw.Value.ProductId}'");
                PaletteLog.Error($"{Tag} TrackPurchase(PendingOrder): invalid metadata - " +
                    $"product_id='{raw.Value.ProductId}', localizedPrice={raw.Value.RawPrice}, isoCurrencyCode='{raw.Value.RawCurrency}'. " +
                    $"Dropping event.");
                ReportPurchaseDataQualityFailure(
                    reason: raw.Value.RawPrice <= 0m ? "non_positive_price" : "non_iso_currency",
                    productId: raw.Value.ProductId,
                    rawCurrency: raw.Value.RawCurrency,
                    rawPrice: (double)raw.Value.RawPrice,
                    source: "pending_order");
                return;
            }

            // Android purchaseToken via unified-receipt parse; iOS path doesn't need this.
            string purchaseToken = null;
            if (!string.IsNullOrEmpty(raw.Value.Receipt))
            {
                ParsedReceipt parsed = ReceiptParser.Parse(raw.Value.Receipt);
                purchaseToken = parsed.PurchaseToken;
            }

            TrackPurchase(
                amount:           (double)raw.Value.RawPrice,
                currency:         raw.Value.RawCurrency,
                productId:        raw.Value.ProductId,
                transactionId:    raw.Value.TransactionId,
                purchaseToken:    purchaseToken,
                storeEnvironment: raw.Value.StoreEnvironment,
                dedupKey:         raw.Value.DedupKey);
        }

#endif

        /// <summary>
        ///     Low-level purchase fan-out. Internal since 3.14.1 - no supported studio-facing entry point.
        ///     Reached only via the SDK-owned subscription installed by <see cref="AttachPurchaseTracking"/>.
        ///     Enforces ISO-4217 / positive-price validation and TxID dedup
        ///     before fanning out to Adjust / Firebase / GameAnalytics.
        /// </summary>
        /// <param name="amount">Purchase amount in local currency (must be &gt; 0).</param>
        /// <param name="currency">ISO 4217 currency code.</param>
        /// <param name="productId">Store product ID.</param>
        /// <param name="transactionId">Transaction ID. Non-empty values are deduped session-wide.</param>
        /// <param name="purchaseToken">Google Play purchase token (Android verification only).</param>
        /// <param name="storeEnvironment">Client-observed store environment: production, sandbox, xcode, or unknown.</param>
        /// <param name="dedupKey">Stable cross-restart dedup identity. When null, falls back to transactionId.
        ///     Callers on the iOS PendingOrder path pass OriginalTransactionID so restores dedup against the original.</param>
        internal static void TrackPurchase(double amount, string currency = "USD",
            string productId = null, string transactionId = null, string purchaseToken = null,
            string storeEnvironment = null, string dedupKey = null)
        {
            if (amount <= 0)
            {
                SorollaDiagnostics.RecordPurchaseDropped($"TrackPurchase: non-positive amount ({amount}) - dropping event");
                PaletteLog.Warning($"{Tag} TrackPurchase: non-positive amount ({amount}) - dropping event. " +
                    $"Pass the local price paid (e.g. Product.metadata.localizedPrice), not a tier index. " +
                    $"Recommended: Palette.AttachPurchaseTracking(store), which derives this automatically from Unity IAP.");
                return;
            }
            // Uppercase so a lowercase ISO code (e.g. "usd") is forwarded canonically. The gate
            // below is case-insensitive, but Firebase/GA4 and MMPs expect uppercase ISO-4217 and
            // would otherwise reject or fail to join the lowercased value (DR-28).
            currency = currency?.ToUpperInvariant();
            if (!IsIso4217(currency))
            {
                // Firebase strips `value` server-side on non-ISO currency (observed:
                // firebase_error=19, error_value="currency" in BQ), and MMPs reject
                // outright. Drop rather than forward corrupt revenue.
                SorollaDiagnostics.RecordPurchaseDropped($"TrackPurchase: currency '{currency}' is not ISO 4217 - dropping event");
                PaletteLog.Error($"{Tag} TrackPurchase: currency '{currency}' is not ISO 4217 - dropping event. " +
                    $"Pass Product.metadata.isoCurrencyCode or use Palette.AttachPurchaseTracking(store).");
                ReportPurchaseDataQualityFailure(
                    reason: "non_iso_currency_lowlevel",
                    productId: productId,
                    rawCurrency: currency,
                    rawPrice: amount,
                    source: "lowlevel");
                return;
            }

            // Validation passed: now enforce dedup so analytics fan-out fires at most once per purchase.
            // Placed AFTER validation so a bad-payload first call doesn't burn the dedup slot for a corrected retry.
            // Key prefers the caller-supplied original-transaction id, else the transaction id (DR-01/DR-07).
            // Two-phase: check here, COMMIT only after the fan-out runs (PurchaseDedupLedger.Commit) so a
            // dropped/never-run queued fan-out doesn't permanently dedup-away a real purchase (B-5).
            string effectiveDedupKey = !string.IsNullOrEmpty(dedupKey) ? dedupKey : transactionId;
            if (!PurchaseDedupLedger.TryBegin(effectiveDedupKey))
            {
                PaletteLog.Warning($"{Tag} TrackPurchase: duplicate purchase detected (dedupKey={PaletteLog.Present(effectiveDedupKey)}) - dropping duplicate event. " +
                    $"Unity IAP v5 can re-deliver purchases (Google Play in-session double on OnPurchaseConfirmed, " +
                    $"or OnPurchasePending crash-replay across restarts per Unity docs). Dedup is persisted so " +
                    $"the fan-out fires once per purchase even after a restart.");
                SorollaDiagnostics.RecordDuplicatePurchase();
                return;
            }

            storeEnvironment = StoreEnvironmentResolver.NormalizeStoreEnvironment(storeEnvironment);

            SorollaDiagnostics.RecordPurchaseAccepted();
            PaletteLog.Vital($"{Tag} TrackPurchase: accepted product_id='{productId ?? "unknown"}', currency='{currency}', transactionId={PaletteLog.Present(transactionId)}, purchaseToken={PaletteLog.Present(purchaseToken)}, storeEnvironment='{storeEnvironment}'.");

            QueueOrExecute(() =>
            {
                SorollaDiagnostics.RecordEventDispatch("purchase", "purchase", new Dictionary<string, object>
                {
                    { "product_id", productId ?? "unknown" },
                    { "value", amount },
                    { "currency", currency },
                    { "transaction_id", transactionId },
                    { "purchase_token", purchaseToken },
                    { "store_environment", storeEnvironment },
                });

#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
                if (!string.IsNullOrEmpty(Config?.adjustPurchaseEventToken))
                    AdjustAdapter.TrackPurchase(Config.adjustPurchaseEventToken, amount, currency,
                        productId, transactionId, purchaseToken);
                else
                    PaletteLog.WarningOnce("purchase.adjust.token_missing", $"{Tag} TrackPurchase: Adjust purchase event token not configured; Adjust purchase revenue skipped.");
#endif

#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackPurchase(productId, amount, currency, transactionId, storeEnvironment);
#endif

#if GAMEANALYTICS_INSTALLED
                // GA expects amount in cents (100x display price across all currencies, incl. JPY).
                // Math.Round avoids floating-point truncation (0.99 * 100 = 98.99999 -> 98 without rounding).
                // GA's business amount is an int; clamp so a very-high-denomination amount (e.g. large
                // VND/IDR purchases) can't silently overflow to a wrong/negative value (B-19).
                double cents = Math.Round(amount * 100);
                int amountInCents = cents >= int.MaxValue ? int.MaxValue : (int)cents;
                if (cents >= int.MaxValue)
                    PaletteLog.WarningOnce("purchase.ga.amount_overflow", $"{Tag} TrackPurchase: amount {amount} {currency} exceeds GameAnalytics' integer cent limit; clamped for GA only. Firebase/Adjust receive the exact value.");
                string gaItemId = string.IsNullOrEmpty(productId) ? "unknown" : productId;
                GameAnalyticsAdapter.TrackBusinessEvent(currency, amountInCents, "iap", gaItemId, null);
#endif

                // Dedup committed only now that the fan-out has actually run (B-5).
                PurchaseDedupLedger.Commit(effectiveDedupKey);
            });
        }

        static bool IsIso4217(string c) =>
            !string.IsNullOrEmpty(c) && c.Length == 3
            && char.IsLetter(c[0]) && char.IsLetter(c[1]) && char.IsLetter(c[2]);

        // Single builder for the purchase data-quality-failure diagnostic event so both TrackPurchase
        // overloads report the same shape instead of two independently drifting payloads.
        static void ReportPurchaseDataQualityFailure(string reason, string productId, string rawCurrency, double rawPrice, string source)
        {
#if FIREBASE_ANALYTICS_INSTALLED
            FirebaseAdapter.TrackEvent("sorolla_purchase_data_quality_failure", new Dictionary<string, object>
            {
                { "reason", reason },
                { "raw_currency", rawCurrency ?? "null" },
                { "raw_price", rawPrice },
                { "product_id", productId ?? "null" },
                { "platform", Application.platform.ToString() },
                { "source", source },
            });
#endif
        }
    }
}
