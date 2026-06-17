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
            PaletteLog.Verbose("[Palette:Adjust] Register() called - assembly is loaded!");
            AdjustAdapter.RegisterImpl(new AdjustAdapterImpl());
        }

        public void Initialize(string appToken, AdjustEnvironment environment, bool verboseLogging = false)
        {
            if (_init) return;

            PaletteLog.Vital($"[Palette:Adjust] Initializing ({environment}, verbose: {verboseLogging})...");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.Initializing,
                "init_requested", $"Initializing ({environment})");

            var config = new AdjustConfig(appToken, environment == AdjustEnvironment.Production
                ? AdjustSdk.AdjustEnvironment.Production
                : AdjustSdk.AdjustEnvironment.Sandbox);

            config.LogLevel = verboseLogging ? AdjustLogLevel.Verbose : AdjustLogLevel.Warn;

            // iOS only: delay the install event up to N seconds so ATT prompt can resolve
            // before the first install fires. Without this, Adjust can send install without
            // IDFA when the ATT dialog is still pending — degraded attribution for non-SKAN
            // paths. Docs: https://dev.adjust.com/en/sdk/unity/features/privacy
            // 60s matches Adjust's own example; safe upper bound.
            config.AttConsentWaitingInterval = 60;

            config.AttributionChangedDelegate = attribution =>
            {
                PaletteLog.Verbose($"[Palette:Adjust] Attribution: network={attribution.Network}, campaign={attribution.Campaign}");
            };

            Adjust.InitSdk(config);
            _init = true;
            PaletteLog.Vital("[Palette:Adjust] Initialized");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.Initializing,
                "init_dispatched", "Init dispatched; waiting for ADID callback");
        }

        public void UpdateConsent(bool consent)
        {
            if (!_init)
            {
                PaletteLog.Verbose("[Palette:Adjust] UpdateConsent called before init - Palette will apply consent after Adjust initialization when MAX finishes.");
                return;
            }

            if (consent) Adjust.Enable();
            else         Adjust.Disable();
            PaletteLog.Vital($"[Palette:Adjust] UpdateConsent({consent}) -> Adjust.{(consent ? "Enable" : "Disable")}()");
        }

        public void TrackAdRevenue(AdRevenueInfo info)
        {
            if (!_init)
            {
                PaletteLog.Warning("[Palette:Adjust] TrackAdRevenue called before init - dropping ad revenue event.");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchDropped,
                    "ad_revenue_before_init", "TrackAdRevenue called before init");
                return;
            }

            PaletteLog.Verbose($"[Palette:Adjust] TrackAdRevenue: {info.Revenue} {info.Currency} from {info.Network}");
            var adRevenue = new AdjustAdRevenue(info.Source ?? AdRevenueInfo.DefaultSource);
            adRevenue.SetRevenue(info.Revenue, info.Currency ?? "USD");
            adRevenue.AdRevenueNetwork = info.Network;
            adRevenue.AdRevenueUnit = info.AdUnit;
            adRevenue.AdRevenuePlacement = info.Placement;
            Adjust.TrackAdRevenue(adRevenue);
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchAccepted,
                "ad_revenue_sent", "Ad revenue sent");
        }

        public void SetUserId(string userId)
        {
            if (!_init)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchDropped,
                    "set_user_id_before_init", "SetUserId called before init");
                return;
            }
            Adjust.AddGlobalPartnerParameter("user_id", userId);
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchAccepted,
                "set_user_id_sent", "SetUserId sent");
        }

        public void GetAttribution(Action<AttributionData?> callback)
        {
            // Honor the documented contract (Palette.GetAttribution): always invoke the callback,
            // with null when attribution is unavailable. Dropping it pre-init hangs callers and
            // makes the Vitals identity rows log false timeout Warnings (DR-42/DR-64).
            if (!_init) { callback?.Invoke(null); return; }
            Adjust.GetAttribution(attr =>
            {
                if (attr == null)
                {
                    callback?.Invoke(null);
                    return;
                }
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.Ready,
                    "attribution_received", "Attribution callback returned");
                callback?.Invoke(new AttributionData
                {
                    Network = attr.Network,
                    Campaign = attr.Campaign,
                    Adgroup = attr.Adgroup,
                    Creative = attr.Creative,
                    TrackerName = attr.TrackerName,
                });
            });
        }

        public void GetAdid(Action<string> callback)
        {
            if (!_init) { callback?.Invoke(null); return; }
            Adjust.GetAdid(adid =>
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust,
                    string.IsNullOrEmpty(adid) ? AdapterDiagnosticStatus.Warning : AdapterDiagnosticStatus.Ready,
                    string.IsNullOrEmpty(adid) ? "adid_unavailable" : "adid_received",
                    string.IsNullOrEmpty(adid) ? "ADID unavailable" : "ADID callback returned");
                callback?.Invoke(adid);
            });
        }

        public void GetGoogleAdId(Action<string> callback)
        {
            if (!_init) { callback?.Invoke(null); return; }
            Adjust.GetGoogleAdId(callback);
        }

        public void GetIdfa(Action<string> callback)
        {
            if (!_init) { callback?.Invoke(null); return; }
            Adjust.GetIdfa(callback);
        }

        public void TrackPurchase(string eventToken, double amount, string currency,
            string productId, string transactionId, string purchaseToken)
        {
            if (!_init)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchDropped,
                    "purchase_before_init", "Purchase tracked before init");
                return;
            }

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
            if (!_init)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchDropped,
                    "purchase_before_init", "iOS purchase tracked before init");
                return;
            }
            var e = BuildPurchaseEvent(eventToken, amount, currency, productId, deduplicationId);
            e.TransactionId = transactionId;
            Adjust.VerifyAndTrackAppStorePurchase(e, verificationResult =>
            {
                PaletteLog.Vital($"[Palette:Adjust] iOS purchase verification: status={verificationResult.VerificationStatus}, code={verificationResult.Code}");
                PaletteLog.Verbose($"[Palette:Adjust] iOS purchase verification message: {verificationResult.Message}");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchAccepted,
                    "ios_purchase_verification", $"iOS purchase verification: {verificationResult.VerificationStatus}");
            });
        }

        public void TrackPurchaseAndroid(string eventToken, double amount, string currency, string productId, string purchaseToken, string deduplicationId)
        {
            if (!_init)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchDropped,
                    "purchase_before_init", "Android purchase tracked before init");
                return;
            }
            var e = BuildPurchaseEvent(eventToken, amount, currency, productId, deduplicationId);
            e.PurchaseToken = purchaseToken;
            Adjust.VerifyAndTrackPlayStorePurchase(e, verificationResult =>
            {
                PaletteLog.Vital($"[Palette:Adjust] Android purchase verification: status={verificationResult.VerificationStatus}, code={verificationResult.Code}");
                PaletteLog.Verbose($"[Palette:Adjust] Android purchase verification message: {verificationResult.Message}");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchAccepted,
                    "android_purchase_verification", $"Android purchase verification: {verificationResult.VerificationStatus}");
            });
        }

        public void TrackPurchaseSimple(string eventToken, double amount, string currency, string deduplicationId, string productId)
        {
            if (!_init)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchDropped,
                    "purchase_before_init", "Purchase tracked before init");
                return;
            }
            var e = BuildPurchaseEvent(eventToken, amount, currency, productId, deduplicationId);
            Adjust.TrackEvent(e);
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.Adjust, AdapterDiagnosticStatus.DispatchAccepted,
                "purchase_sent", "Purchase event sent");
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
