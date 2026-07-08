using System;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    /// Caps label + horizontal rule + optional right-aligned action, used above a group of
    /// checks/fields/rows. The rule fills remaining space so the action (when present) sits
    /// flush right without extra layout code at each call site.
    /// </summary>
    static class SectionHeader
    {
        internal static VisualElement Create(string label, string actionLabel = null, Action onAction = null)
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-section-header");

            var labelElement = new Label(label.ToUpperInvariant());
            labelElement.AddToClassList("sorolla-section-header-label");
            row.Add(labelElement);

            var rule = new VisualElement();
            rule.AddToClassList("sorolla-section-header-rule");
            row.Add(rule);

            if (!string.IsNullOrEmpty(actionLabel))
            {
                var action = new Button(onAction) { text = actionLabel };
                action.AddToClassList("sorolla-section-header-action");
                row.Add(action);
            }

            return row;
        }
    }
}
