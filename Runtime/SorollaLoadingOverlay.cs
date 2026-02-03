using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette
{
    public class SorollaLoadingOverlay : MonoBehaviour
    {
        const float PollIntervalSeconds = 0.2f;

        static SorollaLoadingOverlay s_instance;

        GameObject _canvasObject;
        Text _loadingText;
        WaitForSeconds _pollWait;
        Coroutine _waitRoutine;

        void Awake()
        {
            _pollWait = new WaitForSeconds(PollIntervalSeconds);
            CreateUI();
            InternalHide();
        }

        public static void Show(string message = "Loading Ad...")
        {
            EnsureInstance();
            s_instance.InternalShow(message);
        }

        public static void Hide()
        {
            if (s_instance != null) s_instance.InternalHide();
        }

        public static void WaitForAd(Func<bool> isReadyCheck, Action onReady, Action onFailed, float timeout = 5.0f)
        {
            EnsureInstance();
            Show();

            if (s_instance._waitRoutine != null)
                s_instance.StopCoroutine(s_instance._waitRoutine);

            s_instance._waitRoutine = s_instance.StartCoroutine(s_instance.WaitForAdRoutine(isReadyCheck, onReady, onFailed, timeout));
        }

        static void EnsureInstance()
        {
            if (s_instance != null) return;

            var go = new GameObject("SorollaLoadingOverlay");
            s_instance = go.AddComponent<SorollaLoadingOverlay>();
            DontDestroyOnLoad(go);
        }

        void InternalShow(string message)
        {
            if (_canvasObject == null) return;

            _canvasObject.SetActive(true);
            if (_loadingText != null) _loadingText.text = message;
        }

        void InternalHide()
        {
            if (_canvasObject != null) _canvasObject.SetActive(false);
        }

        IEnumerator WaitForAdRoutine(Func<bool> isReadyCheck, Action onReady, Action onFailed, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (isReadyCheck())
                {
                    Hide();
                    onReady?.Invoke();
                    _waitRoutine = null;
                    yield break;
                }

                yield return _pollWait;
                elapsed += PollIntervalSeconds;
            }

            Hide();
            Debug.LogWarning("[Palette:LoadingOverlay] Ad load timeout");
            onFailed?.Invoke();
            _waitRoutine = null;
        }

        void CreateUI()
        {
            _canvasObject = new GameObject("Canvas");
            _canvasObject.transform.SetParent(transform);

            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasObject.AddComponent<GraphicRaycaster>();

            // Background
            var bgObj = new GameObject("Background");
            bgObj.transform.SetParent(_canvasObject.transform, false);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);
            bgImage.raycastTarget = false; // overlay must NOT block game inputs
            bgImage.rectTransform.anchorMin = Vector2.zero;
            bgImage.rectTransform.anchorMax = Vector2.one;
            bgImage.rectTransform.sizeDelta = Vector2.zero;

            // Text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(_canvasObject.transform, false);
            _loadingText = textObj.AddComponent<Text>();
            _loadingText.text = "Loading Ad...";
            _loadingText.fontSize = 30;
            _loadingText.color = Color.white;
            _loadingText.alignment = TextAnchor.MiddleCenter;
            _loadingText.raycastTarget = false; // overlay must NOT block game inputs
            _loadingText.rectTransform.anchorMin = Vector2.zero;
            _loadingText.rectTransform.anchorMax = Vector2.one;
            _loadingText.rectTransform.sizeDelta = Vector2.zero;
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
