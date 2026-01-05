using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Manages toast notification display. Pools toast instances and handles queuing.
    /// </summary>
    public class ToastController : UIComponentBase
    {
        [SerializeField] GameObject toastPrefab;
        [SerializeField] Transform toastContainer;
        [SerializeField] int maxVisibleToasts = 3;

        readonly Queue<ToastNotification> _toastPool = new Queue<ToastNotification>();
        readonly List<ToastNotification> _activeToasts = new List<ToastNotification>();

        void Awake()
        {

            // Pre-warm pool
            for (int i = 0; i < maxVisibleToasts; i++)
            {
                CreatePooledToast();
            }
        }

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnShowToast += HandleShowToast;

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnShowToast -= HandleShowToast;

        void HandleShowToast(string message, ToastType type) => ShowToast(message, type);

        public void ShowToast(string message, ToastType type)
        {
            ToastNotification toast = GetToastFromPool();
            toast.Show(message, type);
            _activeToasts.Add(toast);

            // Limit visible toasts
            while (_activeToasts.Count > maxVisibleToasts)
            {
                ToastNotification oldest = _activeToasts[0];
                _activeToasts.RemoveAt(0);
                ReturnToPool(oldest);
            }
            RebuildLayout(toast.transform);
        }

        ToastNotification GetToastFromPool()
        {
            if (_toastPool.Count > 0)
            {
                return _toastPool.Dequeue();
            }
            return CreatePooledToast();
        }

        void ReturnToPool(ToastNotification toast)
        {
            toast.gameObject.SetActive(false);
            _toastPool.Enqueue(toast);
        }

        ToastNotification CreatePooledToast()
        {
            GameObject toastGO = Instantiate(toastPrefab, toastContainer);
            toastGO.SetActive(false);
            var toast = toastGO.GetComponent<ToastNotification>();
            _toastPool.Enqueue(toast);
            return toast;
        }

        void RebuildLayout(Transform toast)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(toast as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(toastContainer as RectTransform);
        }
    }
}
