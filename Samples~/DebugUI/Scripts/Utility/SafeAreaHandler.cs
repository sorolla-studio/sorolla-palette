using UnityEngine;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Adjusts RectTransform to respect device safe areas (notches, home indicators, etc.)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHandler : MonoBehaviour
    {

        [SerializeField] bool _conformX = true;
        [SerializeField] bool _conformY = true;
        RectTransform _rectTransform;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;
        ScreenOrientation _lastOrientation;

        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

#if UNITY_EDITOR
        void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height ||
                _lastOrientation != Screen.orientation)
            {
                ApplySafeArea();
            }
        }
#endif

        void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            if (!_conformX)
            {
                anchorMin.x = 0;
                anchorMax.x = 1;
            }

            if (!_conformY)
            {
                anchorMin.y = 0;
                anchorMax.y = 1;
            }

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Safe Area (Editor Test)")]
        void ApplySafeAreaEditor()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }
#endif
    }
}
