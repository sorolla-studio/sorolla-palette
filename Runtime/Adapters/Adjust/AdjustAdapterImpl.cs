using System;
using AdjustSdk;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Adjust SDK adapter implementation. Registered at runtime.
    /// </summary>
    [Preserve]
    internal class AdjustAdapterImpl : IAdjustAdapter
    {
        private bool _init;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            Debug.Log("[Palette:Adjust] Register() called - assembly is loaded!");
            AdjustAdapter.RegisterImpl(new AdjustAdapterImpl());
        }

        public void Initialize(string appToken, AdjustEnvironment environment)
        {
            if (_init) return;

            Debug.Log($"[Palette:Adjust] Initializing ({environment})...");

            var config = new AdjustConfig(appToken, environment == AdjustEnvironment.Production
                ? AdjustSdk.AdjustEnvironment.Production
                : AdjustSdk.AdjustEnvironment.Sandbox);

            // Enable attribution logging
            config.LogLevel = AdjustLogLevel.Info;
            config.AttributionChangedDelegate = attribution =>
            {
                Debug.Log($"[Palette:Adjust] Attribution: network={attribution.Network}, campaign={attribution.Campaign}");
            };

            Adjust.InitSdk(config);
            _init = true;
            Debug.Log("[Palette:Adjust] Initialized");
        }

        public void TrackEvent(string eventToken)
        {
            if (!_init) return;
            Adjust.TrackEvent(new AdjustEvent(eventToken));
        }

        public void TrackRevenue(string eventToken, double amount, string currency)
        {
            if (!_init) return;
            var e = new AdjustEvent(eventToken);
            e.SetRevenue(amount, currency);
            Adjust.TrackEvent(e);
        }

        public void TrackAdRevenue(AdRevenueInfo info)
        {
            if (!_init)
            {
                Debug.LogWarning($"[Palette:Adjust] TrackAdRevenue called before init! Revenue: {info.Revenue} {info.Currency}");
                return;
            }

            Debug.Log($"[Palette:Adjust] TrackAdRevenue: {info.Revenue} {info.Currency} from {info.Network}");
            var adRevenue = new AdjustAdRevenue(info.Source ?? AdRevenueInfo.DefaultSource);
            adRevenue.SetRevenue(info.Revenue, info.Currency ?? "USD");
            adRevenue.AdRevenueNetwork = info.Network;
            adRevenue.AdRevenueUnit = info.AdUnit;
            adRevenue.AdRevenuePlacement = info.Placement;
            Adjust.TrackAdRevenue(adRevenue);
        }

        public void SetUserId(string userId)
        {
            if (!_init) return;
            Adjust.AddGlobalPartnerParameter("user_id", userId);
        }

        public void GetAttribution(Action<object> callback)
        {
            if (!_init) return;
            Adjust.GetAttribution(callback);
        }

        public void GetAdid(Action<string> callback)
        {
            if (!_init) return;
            Adjust.GetAdid(callback);
        }

        public void GetGoogleAdId(Action<string> callback)
        {
            if (!_init) return;
            Adjust.GetGoogleAdId(callback);
        }

        public void GetIdfa(Action<string> callback)
        {
            if (!_init) return;
            Adjust.GetIdfa(callback);
        }

        public void TrackPurchase(string eventToken, double amount, string currency,
            string productId, string transactionId, string purchaseToken)
        {
            if (!_init) return;

            if (Application.platform == RuntimePlatform.IPhonePlayer
                && !string.IsNullOrEmpty(transactionId))
            {
                TrackPurchaseIOS(eventToken, amount, currency, productId, transactionId, transactionId);
            }
            else if (Application.platform == RuntimePlatform.Android
                && !string.IsNullOrEmpty(purchaseToken))
            {
                TrackPurchaseAndroid(eventToken, amount, currency, productId, purchaseToken, transactionId);
            }
            else
            {
                TrackPurchaseSimple(eventToken, amount, currency, transactionId, productId);
            }
        }

        public void TrackPurchaseIOS(string eventToken, double amount, string currency, string productId, string transactionId, string deduplicationId)
        {
            if (!_init) return;
            var e = BuildPurchaseEvent(eventToken, amount, currency, productId, deduplicationId);
            e.TransactionId = transactionId;
            Adjust.VerifyAndTrackAppStorePurchase(e, verificationResult =>
            {
                Debug.Log($"[Palette:Adjust] iOS purchase verification: status={verificationResult.VerificationStatus}, message={verificationResult.Message}");
            });
        }

        public void TrackPurchaseAndroid(string eventToken, double amount, string currency, string productId, string purchaseToken, string deduplicationId)
        {
            if (!_init) return;
            var e = BuildPurchaseEvent(eventToken, amount, currency, productId, deduplicationId);
            e.PurchaseToken = purchaseToken;
            Adjust.VerifyAndTrackPlayStorePurchase(e, verificationResult =>
            {
                Debug.Log($"[Palette:Adjust] Android purchase verification: status={verificationResult.VerificationStatus}, message={verificationResult.Message}");
            });
        }

        public void TrackPurchaseSimple(string eventToken, double amount, string currency, string deduplicationId, string productId)
        {
            if (!_init) return;
            var e = BuildPurchaseEvent(eventToken, amount, currency, productId, deduplicationId);
            Adjust.TrackEvent(e);
        }

        private AdjustEvent BuildPurchaseEvent(string eventToken, double amount, string currency,
            string productId, string deduplicationId)
        {
            var e = new AdjustEvent(eventToken);
            e.SetRevenue(amount, currency);
            if (!string.IsNullOrEmpty(productId))
            {
                e.ProductId = productId;
                e.AddPartnerParameter("product_id", productId);
                e.AddCallbackParameter("product_id", productId);
            }
            if (!string.IsNullOrEmpty(deduplicationId))
            {
                e.DeduplicationId = deduplicationId;
                e.AddCallbackParameter("transaction_id", deduplicationId);
            }
            return e;
        }
    }
}
