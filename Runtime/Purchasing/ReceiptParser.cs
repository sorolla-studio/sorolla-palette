using System;
using UnityEngine;

namespace Sorolla.Palette.Purchasing
{
    /// <summary>
    ///     Parsed fields extracted from a Unity IAP receipt JSON.
    /// </summary>
    public readonly struct ParsedReceipt
    {
        /// <summary>Store name reported by Unity IAP (e.g. "GooglePlay", "AppleAppStore"). Null if parse failed.</summary>
        public readonly string Store;
        /// <summary>Top-level transaction ID from Unity IAP. Used for iOS App Store verification and Firebase dedup.</summary>
        public readonly string TransactionId;
        /// <summary>Play Billing purchaseToken (Android only). Used for Play Store verification.</summary>
        public readonly string PurchaseToken;

        public ParsedReceipt(string store, string transactionId, string purchaseToken)
        {
            Store = store;
            TransactionId = transactionId;
            PurchaseToken = purchaseToken;
        }
    }

    /// <summary>
    ///     Parses Unity IAP receipt JSON strings into transactionId + purchaseToken without depending on com.unity.purchasing.
    ///     Accepts plain strings so it works in any Unity project regardless of IAP package presence.
    /// </summary>
    /// <remarks>
    ///     Unity IAP 4.x/5.x receipt shape:
    ///     <code>
    ///     { "Store": "GooglePlay"|"AppleAppStore", "TransactionID": "...", "Payload": "..." }
    ///     </code>
    ///     On Android, Payload is itself JSON: <c>{ "json": "&lt;play-billing-json&gt;", "signature": "..." }</c>
    ///     where the nested <c>json</c> field contains a Play Billing dict including <c>purchaseToken</c>.
    ///     On iOS, Payload is the base64 App Store receipt; transactionId is already at top-level.
    /// </remarks>
    public static class ReceiptParser
    {
        const string Tag = "[Palette:ReceiptParser]";

        [Serializable] class OuterReceipt { public string Store; public string TransactionID; public string Payload; }
        [Serializable] class AndroidPayload { public string json; public string signature; }
        [Serializable] class PlayBillingJson { public string orderId; public string productId; public string purchaseToken; }

        /// <summary>
        ///     Parse a Unity IAP receipt JSON string. Returns default (all nulls) on invalid/empty input.
        /// </summary>
        public static ParsedReceipt Parse(string receiptJson)
        {
            if (string.IsNullOrEmpty(receiptJson)) return default;

            try
            {
                OuterReceipt outer = JsonUtility.FromJson<OuterReceipt>(receiptJson);
                if (outer == null) return default;

                string txId = outer.TransactionID;
                string token = null;

                if (!string.IsNullOrEmpty(outer.Payload)
                    && !string.IsNullOrEmpty(outer.Store)
                    && outer.Store.IndexOf("GooglePlay", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AndroidPayload payload = JsonUtility.FromJson<AndroidPayload>(outer.Payload);
                    if (payload != null && !string.IsNullOrEmpty(payload.json))
                    {
                        PlayBillingJson billing = JsonUtility.FromJson<PlayBillingJson>(payload.json);
                        if (billing != null) token = billing.purchaseToken;
                    }
                }

                return new ParsedReceipt(outer.Store, txId, token);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} Failed to parse Unity IAP receipt: {e.Message}");
                return default;
            }
        }
    }
}
