using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    public class ToastNotification : UIComponentBase
    {
        [SerializeField] Image dotIndicator;
        [SerializeField] float displayDuration = 3f;
        [SerializeField] float fadeDuration = 0.3f;

        Button _button;
        Image _background;
        CanvasGroup _canvasGroup;
        TextMeshProUGUI _messageText;
        Coroutine _hideCoroutine;

        void Awake()
        {
            _button = GetComponent<Button>();
            _background = GetComponent<Image>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _messageText = GetComponentInChildren<TextMeshProUGUI>();
            _button.onClick.AddListener(() => StartHideRoutine(0f));
        }

        void OnDestroy() => _button.onClick.RemoveAllListeners();

        public void Show(string message, ToastType type)
        {
            _button.interactable = true;
            gameObject.SetActive(true);
            _messageText.text = message;

            Color toastColor = type switch
            {
                ToastType.Success => Theme.accentGreen,
                ToastType.Warning => Theme.accentYellow,
                ToastType.Error => Theme.accentRed,
                _ => Theme.accentPurple,
            };

            dotIndicator.color = toastColor;
            _background.color = new Color(toastColor.r * 0.3f, toastColor.g * 0.3f, toastColor.b * 0.3f, 0.95f);

            StartHideRoutine(displayDuration);
        }

        void StartHideRoutine(float delay)
        {
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideRoutine(delay));
        }

        IEnumerator HideRoutine(float delay)
        {
            _canvasGroup.alpha = 1f;

            if (delay > 0f) yield return new WaitForSeconds(delay);

            _button.interactable = false;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = 1f - elapsed / fadeDuration;
                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}
