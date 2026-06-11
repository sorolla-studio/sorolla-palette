using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

            // iOS: client-observed store environment ("production"/"sandbox"/"xcode")
            // from the StoreKit JWS. TestFlight reports "sandbox". Null (-> "unknown")
            // on Android and when undecodable.
            string storeEnvironment = ResolveStoreEnvironment(order);

            // iOS: dedup against the original transaction so a restored/re-delivered
            // non-consumable (new TransactionID, same OriginalTransactionID) is not counted
            // as new revenue (DR-07). Null on Android / new purchases -> falls back to transactionId.
            string dedupKey = ResolveDedupKey(order, transactionId);

            TrackPurchase(
                amount:           (double)rawPrice,
                currency:         rawCurrency,
                productId:        productId,
                transactionId:    transactionId,
                purchaseToken:    purchaseToken,
                storeEnvironment: storeEnvironment,
                dedupKey:         dedupKey);
        }

        // Resolves the stable dedup identity for a purchase. On iOS, StoreKit's
        // OriginalTransactionID points back to the first purchase of a non-consumable, so a
        // restore / re-delivery shares it (while TransactionID is fresh). For consumables each
        // purchase gets its own original, so distinct buys stay distinct. Returns null when no
        // original is available (Android, undecodable) so the caller falls back to transactionId.
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
            return DecodeJwsEnvironment(order?.Info?.Apple?.jwsRepresentation);
#else
            return null;
#endif
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

        // Cross-restart purchase dedup for the entire TrackPurchase fan-out (DR-01).
        // Unity IAP v5 re-delivers an unconfirmed purchase by re-firing OnPurchasePending
        // on the NEXT app launch (Unity-documented crash-replay), so an in-memory-only set
        // would let the same purchase inflate revenue once per restart while the docs claim
        // crash-replay immunity. The set is persisted to PlayerPrefs so dedup survives
        // process death and the immunity is real.
        //
        // The dedup key (DR-07) prefers the store's original-transaction id when the caller
        // supplies one, so an iOS restore / re-delivery of a non-consumable (new TransactionID,
        // same OriginalTransactionID) is recognised as already-counted instead of new revenue.
        // Assumption (hyper-casual portfolio): products are consumables + one-shot
        // non-consumables, NOT auto-renewable subscriptions. Subscriptions share an
        // OriginalTransactionID across renewals, so this key would collapse legitimate renewal
        // revenue. Revisit before shipping a subscription product.
        const string ProcessedTxPrefsKey = "sorolla.purchase.processed_tx_ids";
        // Bound the persisted set so PlayerPrefs stays small; FIFO eviction of the oldest ids.
        // Crash-replay re-delivery happens on the immediate next launch, far inside this window,
        // so eviction never reopens the replay hole it closes.
        const int ProcessedTxMax = 512;
        static HashSet<string> s_processedTxIds;
        static readonly List<string> s_processedTxOrder = new List<string>();

        // Records dedupKey as processed and returns true if this is the FIRST time it is seen.
        // Empty key (no transaction id available) cannot be deduped, so it is always allowed
        // through - matching the previous behavior, which skipped dedup on empty TxIDs.
        static bool MarkPurchaseProcessed(string dedupKey)
        {
            if (string.IsNullOrEmpty(dedupKey)) return true;
            LoadProcessedTxIds();
            if (!s_processedTxIds.Add(dedupKey)) return false;
            s_processedTxOrder.Add(dedupKey);
            while (s_processedTxOrder.Count > ProcessedTxMax)
            {
                string evicted = s_processedTxOrder[0];
                s_processedTxOrder.RemoveAt(0);
                s_processedTxIds.Remove(evicted);
            }
            PlayerPrefs.SetString(ProcessedTxPrefsKey, string.Join("\n", s_processedTxOrder));
            PlayerPrefs.Save();
            return true;
        }

        static void LoadProcessedTxIds()
        {
            if (s_processedTxIds != null) return;
            s_processedTxIds = new HashSet<string>();
            string raw = PlayerPrefs.GetString(ProcessedTxPrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (string id in raw.Split('\n'))
                if (!string.IsNullOrEmpty(id) && s_processedTxIds.Add(id))
                    s_processedTxOrder.Add(id);
        }

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
        /// <param name="storeEnvironment">Client-observed store environment: production, sandbox, xcode, or unknown.</param>
        /// <param name="dedupKey">Stable cross-restart dedup identity. When null, falls back to transactionId.
        ///     Callers on the iOS PendingOrder path pass OriginalTransactionID so restores dedup against the original.</param>
        internal static void TrackPurchase(double amount, string currency = "USD",
            string productId = null, string transactionId = null, string purchaseToken = null,
            string storeEnvironment = null, string dedupKey = null)
        {
            if (amount <= 0)
            {
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

            // Validation passed: now enforce dedup so analytics fan-out fires at most once per purchase.
            // Placed AFTER validation so a bad-payload first call doesn't burn the dedup slot for a corrected retry.
            // Key prefers the caller-supplied original-transaction id, else the transaction id (DR-01/DR-07).
            string effectiveDedupKey = !string.IsNullOrEmpty(dedupKey) ? dedupKey : transactionId;
            if (!MarkPurchaseProcessed(effectiveDedupKey))
            {
                PaletteLog.Warning($"{Tag} TrackPurchase: duplicate purchase detected (dedupKey={PaletteLog.Present(effectiveDedupKey)}) - dropping duplicate event. " +
                    $"Unity IAP v5 can re-deliver purchases (Google Play in-session double on OnPurchaseConfirmed, " +
                    $"or OnPurchasePending crash-replay across restarts per Unity docs). Dedup is persisted so " +
                    $"the fan-out fires once per purchase even after a restart.");
                return;
            }

            storeEnvironment = NormalizeStoreEnvironment(storeEnvironment);

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

                if (Config != null && Config.enableTikTok && !string.IsNullOrEmpty(Config.tiktokAppId?.Current))
                    TikTokAdapter.TrackPurchase(amount, currency);

#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackPurchase(productId, amount, currency, transactionId, storeEnvironment);
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

        [Serializable] class JwsEnvironmentClaim { public string environment; }

        // Apple's jwsRepresentation is a JWS compact token (header.payload.signature, base64url).
        // The decoded payload JSON carries an `environment` claim: "Production"|"Sandbox"|"Xcode"
        // (App Store Server API environment field). This is a client-observed label only: we do
        // not verify the signature here, and canonical revenue still needs server-side / Adjust
        // verification. Returns a bounded lower-case value, or null if absent/undecodable.
        static string DecodeJwsEnvironment(string jws)
        {
            if (string.IsNullOrEmpty(jws)) return null;
            string[] parts = jws.Split('.');
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1])) return null;
            try
            {
                string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                JwsEnvironmentClaim claim = JsonUtility.FromJson<JwsEnvironmentClaim>(payloadJson);
                string env = claim?.environment;
                return string.IsNullOrEmpty(env) ? null : NormalizeStoreEnvironment(env);
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"{Tag} Could not decode JWS environment claim: {e.Message}");
                return null;
            }
        }

        static string NormalizeStoreEnvironment(string storeEnvironment)
        {
            if (string.IsNullOrEmpty(storeEnvironment)) return "unknown";

            string normalized = storeEnvironment.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "production":
                case "sandbox":
                case "xcode":
                    return normalized;
                default:
                    return "unknown";
            }
        }

        static byte[] Base64UrlDecode(string input)
        {
            string s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }

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
