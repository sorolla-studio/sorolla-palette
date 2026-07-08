using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Icon + title + version line + mode toggle pill, for the window's top hero section.</summary>
    static class HeroHeader
    {
        internal static VisualElement Create(string title, string version, string modeLabel, bool modeIsFull)
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

            var modePill = new VisualElement();
            modePill.AddToClassList("sorolla-hero-mode");
            modePill.AddToClassList(modeIsFull ? "sorolla-hero-mode-full" : "sorolla-hero-mode-prototype");
            var modeLabelElement = new Label(modeLabel);
            modeLabelElement.AddToClassList("sorolla-hero-mode-label");
            modePill.Add(modeLabelElement);
            row.Add(modePill);

            return row;
        }
    }
}
