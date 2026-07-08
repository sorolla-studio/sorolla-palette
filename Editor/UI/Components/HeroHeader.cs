using System;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Icon + title + version line + Prototype|Full segmented mode switch, for the
    /// window's top hero section.</summary>
    static class HeroHeader
    {
        /// <summary>modeIsFull reflects the CURRENT mode (drives which segment renders filled).
        /// onSwitchRequested fires only when the user clicks the INACTIVE segment - the caller owns
        /// the actual switch flow (confirmation dialog + SorollaSettings.SetMode); clicking the
        /// active segment is a no-op here, never invoking the callback.</summary>
        internal static VisualElement Create(string title, string version, bool modeIsFull, Action onSwitchRequested)
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-hero");

            var icon = new Label("🎨");
            icon.AddToClassList("sorolla-hero-icon");
            row.Add(icon);

            var textColumn = new VisualElement();
            textColumn.AddToClassList("sorolla-hero-text");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("sorolla-hero-title");
            textColumn.Add(titleLabel);

            var versionLabel = new Label(version);
            versionLabel.AddToClassList("sorolla-hero-version");
            textColumn.Add(versionLabel);

            row.Add(textColumn);

            row.Add(BuildModeSwitch(modeIsFull, onSwitchRequested));

            return row;
        }

        static VisualElement BuildModeSwitch(bool modeIsFull, Action onSwitchRequested)
        {
            var switchRow = new VisualElement();
            switchRow.AddToClassList("sorolla-mode-switch");

            var prototypeSegment = new Label("Prototype");
            prototypeSegment.AddToClassList("sorolla-mode-segment");
            prototypeSegment.AddToClassList(modeIsFull ? "sorolla-mode-segment-inactive" : "sorolla-mode-segment-active");

            var fullSegment = new Label("Full");
            fullSegment.AddToClassList("sorolla-mode-segment");
            fullSegment.AddToClassList(modeIsFull ? "sorolla-mode-segment-active" : "sorolla-mode-segment-inactive");

            // Only the inactive segment can trigger a switch; clicking the already-active segment
            // is a no-op (no manipulator registered on it at all, not just a guarded callback).
            // Label defaults to PickingMode.Ignore (not interactive), so the clickable segment
            // needs an explicit PickingMode.Position or ClickEvent never reaches it.
            if (modeIsFull)
            {
                prototypeSegment.pickingMode = PickingMode.Position;
                prototypeSegment.RegisterCallback<ClickEvent>(_ => onSwitchRequested());
            }
            else
            {
                fullSegment.pickingMode = PickingMode.Position;
                fullSegment.RegisterCallback<ClickEvent>(_ => onSwitchRequested());
            }

            switchRow.Add(prototypeSegment);
            switchRow.Add(fullSegment);
            return switchRow;
        }
    }
}
