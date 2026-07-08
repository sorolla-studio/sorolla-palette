using System;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    /// Tinted banner with a colored left accent bar, title, body, and an optional inline action
    /// button - the "callout banners with inline actions" from PLAN.md's mockup direction.
    /// </summary>
    static class CalloutCard
    {
        internal enum Severity
        {
            Blocker,
            Advisory,
            Success,
            Info,
        }

        internal static VisualElement Create(Severity severity, string title, string body, string actionLabel = null, Action onAction = null)
        {
            var card = new VisualElement();
            card.AddToClassList("sorolla-callout");
            card.AddToClassList(TintClassFor(severity));

            var accent = new VisualElement();
            accent.AddToClassList("sorolla-callout-accent");
            accent.AddToClassList(AccentClassFor(severity));
            card.Add(accent);

            var content = new VisualElement();
            content.AddToClassList("sorolla-callout-content");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("sorolla-callout-title");
            content.Add(titleLabel);

            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("sorolla-callout-body");
            content.Add(bodyLabel);

            if (!string.IsNullOrEmpty(actionLabel))
            {
                var actionRow = new VisualElement();
                actionRow.AddToClassList("sorolla-callout-actions");
                var button = new Button(onAction) { text = actionLabel };
                button.AddToClassList("sorolla-callout-button");
                actionRow.Add(button);
                content.Add(actionRow);
            }

            card.Add(content);
            return card;
        }

        static string TintClassFor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Blocker: return "sorolla-callout-blocker";
                case Severity.Advisory: return "sorolla-callout-advisory";
                case Severity.Success: return "sorolla-callout-success";
                default: return "sorolla-callout-info";
            }
        }

        static string AccentClassFor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Blocker: return "sorolla-callout-accent-fail";
                case Severity.Advisory: return "sorolla-callout-accent-warn";
                case Severity.Success: return "sorolla-callout-accent-pass";
                default: return "sorolla-callout-accent-info";
            }
        }
    }
}
