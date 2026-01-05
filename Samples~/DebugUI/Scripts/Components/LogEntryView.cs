using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    [RequireComponent(typeof(Image))]
    public class LogEntryView : UIComponentBase
    {
        [SerializeField] Image accentBar;
        [SerializeField] TextMeshProUGUI timestampLabel;
        [SerializeField] TextMeshProUGUI sourceBadgeLabel;
        [SerializeField] TextMeshProUGUI messageLabel;

        Image _backgroundImage;

        void Awake() => _backgroundImage = GetComponent<Image>();

        public void SetData(LogEntryData data)
        {
            _backgroundImage.color = new Color(data.accentColor.r, data.accentColor.g, data.accentColor.b, 0.05f);
            accentBar.color = data.accentColor;
            timestampLabel.text = data.timestamp;
            sourceBadgeLabel.text = data.source.ToString().ToUpper();
            messageLabel.text = data.message;
            messageLabel.color = GetMessageColor(data.level);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)sourceBadgeLabel.transform);
            
        }

        Color GetMessageColor(LogLevel level) => level switch
        {
            LogLevel.Warning => Theme.accentYellow,
            LogLevel.Error => Theme.accentRed,
            _ => Theme.textPrimary,
        };
    }
}
