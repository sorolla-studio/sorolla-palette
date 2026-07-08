using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Title bar (label + Copy button) over a monospace code body. Copy writes the code
    /// text to the system clipboard via GUIUtility.systemCopyBuffer.</summary>
    static class CodeSnippetBlock
    {
        static Font s_monoFont;

        static Font MonoFont => s_monoFont != null
            ? s_monoFont
            : s_monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Menlo", "Consolas", "Courier New" }, 12);

        internal static VisualElement Create(string title, string code)
        {
            var card = new VisualElement();
            card.AddToClassList("sorolla-snippet");

            var titleBar = new VisualElement();
            titleBar.AddToClassList("sorolla-snippet-titlebar");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("sorolla-snippet-title");
            titleBar.Add(titleLabel);

            var copyButton = new Button(() => GUIUtility.systemCopyBuffer = code) { text = "Copy" };
            copyButton.AddToClassList("sorolla-snippet-copy");
            titleBar.Add(copyButton);

            card.Add(titleBar);

            var body = new Label(code);
            body.AddToClassList("sorolla-snippet-body");
            body.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(MonoFont));
            card.Add(body);

            return card;
        }
    }
}
