using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Individual filter button in the log filter bar.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class LogFilterButton : UIComponentBase
    {

        static LogFilterButton s_currentSelected;
        [SerializeField] Image background;
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] LogLevel filterLevel;
        [SerializeField] bool startSelected;

        Button _button;

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        void OnDestroy() => _button.onClick.RemoveListener(OnClick);

        void Start()
        {
            if (startSelected) SetSelected(true);
        }

        void OnClick()
        {
            if (s_currentSelected != null)
            {
                s_currentSelected.SetSelected(false);
            }

            SetSelected(true);
            s_currentSelected = this;

            SorollaDebugEvents.RaiseLogFilterChanged(filterLevel);
        }

        public void SetSelected(bool selected)
        {
            background.color = selected ? Theme.cardBackgroundLight : Color.clear;
            label.color = selected ? Theme.textPrimary : Theme.textSecondary;

            if (selected) s_currentSelected = this;
        }
    }
}
