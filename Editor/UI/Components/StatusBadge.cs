using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    /// Small colored pill labeling a status. ADVISORY/PASS/FAIL/WAIT render as solid fills
    /// (confident, resolved states). GATED renders as a muted solid fill - the vendor is absent or
    /// switched off, so nothing was verified and the pill must not look like a pass.
    /// </summary>
    static class StatusBadge
    {
        internal enum Severity
        {
            Advisory,
            Pass,
            Fail,
            Wait,
            Gated,
        }

        internal static VisualElement Create(string text, Severity severity)
        {
            var pill = new VisualElement();
            pill.AddToClassList("sorolla-badge");
            pill.AddToClassList(ClassFor(severity));

            var label = new Label(text);
            label.AddToClassList("sorolla-badge-label");
            pill.Add(label);
            return pill;
        }

        static string ClassFor(Severity severity) => severity switch
        {
            Severity.Fail => "sorolla-badge-fail",
            Severity.Advisory => "sorolla-badge-warn",
            Severity.Pass => "sorolla-badge-pass",
            Severity.Wait => "sorolla-badge-wait",
            _ => "sorolla-badge-gated",
        };
    }
}
