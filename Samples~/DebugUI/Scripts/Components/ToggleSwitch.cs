using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    public class ToggleSwitch : UIComponentBase, IPointerClickHandler
    {
        [SerializeField] Image trackImage;
        [SerializeField] Image knobImage;
        [SerializeField] ToggleType toggleType;
        [SerializeField] bool isOn;
        [SerializeField] float animationDuration = 0.15f;

        float _knobOffX;
        float _knobOnX;
        Coroutine _animationCoroutine;
        RectTransform _knobTransform;

        void Awake()
        {
            _knobTransform = knobImage.GetComponent<RectTransform>();

            float trackWidth = trackImage.rectTransform.rect.width;
            _knobOffX = -trackWidth / 5f;
            _knobOnX = trackWidth / 5f;

            UpdateVisual(false);
        }

        public void OnPointerClick(PointerEventData eventData) => SetValue(!isOn, true);

        public void SetValue(bool value, bool animate = false)
        {
            isOn = value;
            UpdateVisual(animate);

            if (toggleType == ToggleType.None) return;

            SorollaDebugEvents.RaiseToggleChanged(toggleType, isOn);

            // Log and toast using enum name
            string label = toggleType.ToString();
            DebugPanelManager.Instance?.Log($"{label}: {(isOn ? "ON" : "OFF")}");
            if (isOn)
                SorollaDebugEvents.RaiseShowToast($"{label} Enabled", ToastType.Success);
        }

        void UpdateVisual(bool animate)
        {
            Color trackColor = isOn ? Theme.accentPurple : Theme.cardBackgroundLight;
            trackImage.color = trackColor;

            float targetX = isOn ? _knobOnX : _knobOffX;

            if (animate && gameObject.activeInHierarchy)
            {
                if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
                _animationCoroutine = StartCoroutine(AnimateKnob(targetX));
            }
            else
            {
                Vector2 pos = _knobTransform.anchoredPosition;
                pos.x = targetX;
                _knobTransform.anchoredPosition = pos;
            }
        }

        IEnumerator AnimateKnob(float targetX)
        {
            float startX = _knobTransform.anchoredPosition.x;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                t = t * t * (3f - 2f * t); // Smoothstep

                Vector2 pos = _knobTransform.anchoredPosition;
                pos.x = Mathf.Lerp(startX, targetX, t);
                _knobTransform.anchoredPosition = pos;
                yield return null;
            }
        }
    }
}
