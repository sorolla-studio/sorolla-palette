using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Single check line: status icon, label, right-aligned status text.</summary>
    static class CheckRow
    {
        internal enum Status
        {
            Pass,
            Warn,
            Fail,
            Wait,
            /// <summary>Neutral notice, not a pass/fail/pending - e.g. an optional asset is missing or a
            /// check does not apply to this row's configuration.</summary>
            Info,
        }

        internal static VisualElement Create(string label, Status status, string statusText = null)
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-check-row");

            var icon = new Label(IconFor(status));
            icon.AddToClassList("sorolla-check-row-icon");
            icon.AddToClassList(ClassFor(status));
            row.Add(icon);

            var labelElement = new Label(label);
            labelElement.AddToClassList("sorolla-check-row-label");
            row.Add(labelElement);

            var statusElement = new Label(statusText ?? status.ToString().ToUpperInvariant());
            statusElement.AddToClassList("sorolla-check-row-status");
            statusElement.AddToClassList(ClassFor(status));
            row.Add(statusElement);

            return row;
        }

        static string IconFor(Status status)
        {
            switch (status)
            {
                case Status.Pass: return "✓";
                case Status.Fail: return "✕";
                case Status.Warn: return "⚠";
                case Status.Info: return "ℹ";
                default: return "•";
            }
        }

        static string ClassFor(Status status)
        {
            switch (status)
            {
                case Status.Pass: return "sorolla-check-pass";
                case Status.Warn: return "sorolla-check-warn";
                case Status.Fail: return "sorolla-check-fail";
                case Status.Info: return "sorolla-check-info";
                default: return "sorolla-check-wait";
            }
        }
    }
}
