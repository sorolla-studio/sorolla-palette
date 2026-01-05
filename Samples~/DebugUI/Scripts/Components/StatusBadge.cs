using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    public class StatusBadge : UIComponentBase
    {
        [SerializeField] Image _background;
        [SerializeField] TextMeshProUGUI _label;

        public void SetStatus(string text, Color color)
        {
            _label.text = text;
            _label.color = color;
            _background.color = new Color(color.r, color.g, color.b, 0.2f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        public void SetIdle() => SetStatus("IDLE", Theme.statusIdle);
        public void SetLoaded() => SetStatus("LOADED", Theme.accentGreen);
        public void SetLoading() => SetStatus("LOADING", Theme.accentYellow);
        public void SetFailed() => SetStatus("FAILED", Theme.accentRed);
    }
}
