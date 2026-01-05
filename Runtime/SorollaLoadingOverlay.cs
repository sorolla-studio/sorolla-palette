using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette
{
    public class SorollaLoadingOverlay : MonoBehaviour
    {
        private static SorollaLoadingOverlay _instance;
        private GameObject _canvasObject;
        private Text _loadingText;

        public static void Show(string message = "Loading Ad...")
        {
            EnsureInstance();
            _instance.InternalShow(message);
        }

        public static void Hide()
        {
            if (_instance != null)
            {
                _instance.InternalHide();
            }
        }

        public static void WaitForAd(Func<bool> isReadyCheck, Action onReady, Action onFailed, float timeout = 5.0f)
        {
            EnsureInstance();
            Show();
            _instance.StartCoroutine(_instance.WaitForAdRoutine(isReadyCheck, onReady, onFailed, timeout));
        }

        private static void EnsureInstance()
        {
            if (_instance == null)
            {
                var go = new GameObject("SorollaLoadingOverlay");
                _instance = go.AddComponent<SorollaLoadingOverlay>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            CreateUI();
            InternalHide();
        }

        private void InternalShow(string message)
        {
            if (_canvasObject != null)
            {
                _canvasObject.SetActive(true);
                if (_loadingText != null) _loadingText.text = message;
            }
        }

        private void InternalHide()
        {
            if (_canvasObject != null) _canvasObject.SetActive(false);
        }

        private IEnumerator WaitForAdRoutine(Func<bool> isReadyCheck, Action onReady, Action onFailed, float timeout)
        {
            float elapsed = 0;
            while (elapsed < timeout)
            {
                if (isReadyCheck())
                {
                    Hide();
                    onReady?.Invoke();
                    yield break;
                }
                yield return new WaitForSeconds(0.2f);
                elapsed += 0.2f;
            }
            
            Hide();
            Debug.LogWarning("[SorollaLoadingOverlay] Ad load timeout");
            onFailed?.Invoke();
        }

        private void CreateUI()
        {
            // Create Canvas
            _canvasObject = new GameObject("Canvas");
            _canvasObject.transform.SetParent(transform);
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            _canvasObject.AddComponent<CanvasScaler>();
            _canvasObject.AddComponent<GraphicRaycaster>();

            // Create Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_canvasObject.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);
            var bgRect = bgImage.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            // Create Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(_canvasObject.transform, false);
            _loadingText = textObj.AddComponent<Text>();
            _loadingText.text = "Loading Ad...";
            _loadingText.fontSize = 30;
            _loadingText.color = Color.white;
            _loadingText.alignment = TextAnchor.MiddleCenter;
            var textRect = _loadingText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
        }
    }
}
