using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_PURCHASING_INSTALLED
using UnityEngine.Purchasing;
#endif

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Debug UI purchase harness for validating the canonical Unity IAP v5
    ///     <see cref="Palette.AttachPurchaseTracking"/> integration path.
    /// </summary>
    public static class DebugPurchaseTester
    {
        public const string QaProductId = "com.sorolla.palette.qa_coins_100";
        public const string Tag = "[SorollaQA:IAP]";

        public static void Purchase(LogSource source)
        {
#if UNITY_PURCHASING_INSTALLED
            DebugPurchaseTesterRuntime.Ensure().Purchase(source);
#else
            Debug.LogWarning($"{Tag} Unity IAP is not installed; cannot run purchase QA.");
            DebugPanelManager.Instance?.Log("Unity IAP is not installed", source, LogLevel.Warning);
            SorollaDebugEvents.RaiseShowToast("Unity IAP missing", ToastType.Warning);
#endif
        }

    }

#if UNITY_PURCHASING_INSTALLED
    public sealed class DebugPurchaseTesterRuntime : MonoBehaviour
    {
        const string Tag = DebugPurchaseTester.Tag;
        const string QaProductId = DebugPurchaseTester.QaProductId;

        static DebugPurchaseTesterRuntime s_instance;

        StoreController _store;
        Product _product;
        bool _connecting;
        bool _connected;
        bool _fetching;
        bool _fetchAttempted;
        int _coinsGranted;

        public static DebugPurchaseTesterRuntime Ensure()
        {
            if (s_instance != null) return s_instance;

            var go = new GameObject("[Sorolla QA IAP]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_instance = go.AddComponent<DebugPurchaseTesterRuntime>();
            return s_instance;
        }

        public void Purchase(LogSource source)
        {
            EnsureInitialized();

            if (_product == null)
            {
                string state = _fetching ? "fetching products" : _fetchAttempted ? "product unavailable" : "connecting store";
                Debug.LogWarning($"{Tag} Purchase requested before product is ready ({state}). SKU='{QaProductId}'.");
                DebugPanelManager.Instance?.Log($"IAP product not ready ({state})", source, LogLevel.Warning);
                SorollaDebugEvents.RaiseShowToast("IAP warming up", ToastType.Warning);
                return;
            }

            Debug.Log($"{Tag} Purchase requested: {QaProductId}");
            DebugPanelManager.Instance?.Log($"Purchase requested: {QaProductId}", source);
            SorollaDebugEvents.RaiseShowToast("Opening store purchase", ToastType.Info);
            _store.PurchaseProduct(DebugPurchaseTester.QaProductId);
        }

        void EnsureInitialized()
        {
            if (_store != null || _connecting) return;
            InitializeStore();
        }

        async void InitializeStore()
        {
            _connecting = true;

            try
            {
                Debug.Log($"{Tag} Initializing Unity IAP v5 StoreController for SKU '{QaProductId}'...");
                _store = UnityIAPServices.StoreController();

                // Attach Palette first so the SDK sees PendingOrder before the
                // test harness confirms the purchase for fulfillment.
                Palette.AttachPurchaseTracking(_store);

                _store.OnStoreConnected += OnStoreConnected;
                _store.OnStoreDisconnected += OnStoreDisconnected;
                _store.OnProductsFetched += OnProductsFetched;
                _store.OnProductsFetchFailed += OnProductsFetchFailed;
                _store.OnPurchasePending += OnPurchasePending;
                _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _store.OnPurchaseFailed += OnPurchaseFailed;
                _store.OnPurchaseDeferred += OnPurchaseDeferred;

                await _store.Connect();
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} Store initialization failed. Rebuild with verbose logging to inspect Unity IAP details.");
                LogVerbose($"{Tag} Store initialization failed: {e.Message}");
                DebugPanelManager.Instance?.Log("IAP init failed", LogSource.Firebase, LogLevel.Error);
                SorollaDebugEvents.RaiseShowToast("IAP init failed", ToastType.Error);
                _connecting = false;
            }
        }

        void OnStoreConnected()
        {
            _connected = true;
            _connecting = false;
            Debug.Log($"{Tag} Store connected.");
            DebugPanelManager.Instance?.Log("IAP store connected", LogSource.Firebase);
            FetchProducts();
        }

        void OnStoreDisconnected(StoreConnectionFailureDescription description)
        {
            _connected = false;
            _connecting = false;
            Debug.LogWarning($"{Tag} Store disconnected: {description.message}");
            DebugPanelManager.Instance?.Log($"IAP store disconnected: {description.message}", LogSource.Firebase, LogLevel.Warning);
        }

        void FetchProducts()
        {
            if (!_connected || _fetching) return;

            _fetching = true;
            _fetchAttempted = true;
            var products = new List<ProductDefinition>
            {
                new ProductDefinition(DebugPurchaseTester.QaProductId, ProductType.Consumable),
            };

            Debug.Log($"{Tag} Fetching products: {DebugPurchaseTester.QaProductId}");
            _store.FetchProducts(products);
        }

        void OnProductsFetched(List<Product> products)
        {
            _fetching = false;
            _product = products.FirstOrDefault(product => product.definition.id == DebugPurchaseTester.QaProductId);

            int matched = _product == null ? 0 : 1;
            Debug.Log($"{Tag} Products fetched: {matched}/1");
            DebugPanelManager.Instance?.Log($"Products fetched: {matched}/1", LogSource.Firebase, matched == 1 ? LogLevel.Info : LogLevel.Warning);

            if (_product == null)
            {
                Debug.LogError($"{Tag} QA product missing from fetched products. SKU='{DebugPurchaseTester.QaProductId}'. Check store dashboard product state.");
                SorollaDebugEvents.RaiseShowToast("QA SKU missing", ToastType.Error);
                return;
            }

            var metadata = _product.metadata;
            Debug.Log($"{Tag} Product ready: {DebugPurchaseTester.QaProductId}, price={metadata.localizedPrice}, currency={metadata.isoCurrencyCode}");
            SorollaDebugEvents.RaiseShowToast("QA purchase ready", ToastType.Success);
        }

        void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            _fetching = false;
            _product = null;

            Debug.LogError($"{Tag} Products fetch FAILED: {failure.FailedFetchProducts.Count}/1, reason={failure.FailureReason}");
            DebugPanelManager.Instance?.Log($"Products fetch FAILED: {failure.FailureReason}", LogSource.Firebase, LogLevel.Error);
            SorollaDebugEvents.RaiseShowToast("IAP product fetch failed", ToastType.Error);
        }

        void OnPurchasePending(PendingOrder order)
        {
            Product product = GetFirstProduct(order);
            string productId = GetProductId(product);
            string txId = order.Info?.TransactionID;

            Debug.Log($"{Tag} Purchase pending: product={productId}, transactionId={Present(txId)}");
            Debug.Log($"[IAP] Reward granted: {productId}");
            _coinsGranted += 100;
            DebugPanelManager.Instance?.Log($"Reward granted: +100 QA coins (total {_coinsGranted})", LogSource.Firebase);

            _store.ConfirmPurchase(order);
        }

        void OnPurchaseConfirmed(Order order)
        {
            Product product = GetFirstProduct(order);
            string productId = GetProductId(product);

            switch (order)
            {
                case ConfirmedOrder:
                    Debug.Log($"[IAP] Purchase confirmed by store: {productId}");
                    Debug.Log($"{Tag} Purchase confirmed by store: {productId}");
                    DebugPanelManager.Instance?.Log($"Purchase confirmed: {productId}", LogSource.Firebase);
                    SorollaDebugEvents.RaiseShowToast("Purchase confirmed", ToastType.Success);
                    break;
                case FailedOrder failed:
                    Debug.LogError($"{Tag} Purchase confirmation failed: product={productId}, reason={failed.FailureReason}. Rebuild with verbose logging to inspect store details.");
                    LogVerbose($"{Tag} Purchase confirmation failed details: {failed.Details}");
                    DebugPanelManager.Instance?.Log($"Purchase confirmation failed: {failed.FailureReason}", LogSource.Firebase, LogLevel.Error);
                    SorollaDebugEvents.RaiseShowToast("Purchase confirm failed", ToastType.Error);
                    break;
            }
        }

        void OnPurchaseFailed(FailedOrder order)
        {
            Product product = GetFirstProduct(order);
            Debug.LogError($"{Tag} Purchase failed: product={GetProductId(product)}, reason={order.FailureReason}. Rebuild with verbose logging to inspect store details.");
            LogVerbose($"{Tag} Purchase failed details: {order.Details}");
            DebugPanelManager.Instance?.Log($"Purchase failed: {order.FailureReason}", LogSource.Firebase, LogLevel.Error);
            SorollaDebugEvents.RaiseShowToast("Purchase failed", ToastType.Error);
        }

        void OnPurchaseDeferred(DeferredOrder order)
        {
            Product product = GetFirstProduct(order);
            Debug.LogWarning($"{Tag} Purchase deferred: product={GetProductId(product)}");
            DebugPanelManager.Instance?.Log($"Purchase deferred: {GetProductId(product)}", LogSource.Firebase, LogLevel.Warning);
            SorollaDebugEvents.RaiseShowToast("Purchase deferred", ToastType.Warning);
        }

        static Product GetFirstProduct(Order order) =>
            order?.CartOrdered?.Items()?.FirstOrDefault()?.Product;

        static string GetProductId(Product product) =>
            product?.definition?.id ?? "unknown";

        static string Present(string value) => string.IsNullOrEmpty(value) ? "missing" : "present";

        static void LogVerbose(string message)
        {
            if (Palette.VerboseLogging) Debug.Log(message);
        }
    }
#endif
}
