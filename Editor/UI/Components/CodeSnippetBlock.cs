using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Title bar (label + Copy button) over a code body. Copy writes the code text to the
    /// system clipboard via GUIUtility.systemCopyBuffer.
    /// The body deliberately uses the editor's default font. It used to request a monospace OS font via
    /// Font.CreateDynamicFontFromOSFont, which returns null during window construction; assigning a
    /// FontDefinition wrapping a null Font renders the Label completely blank, so the snippets shipped
    /// invisible while Copy (which closes over the string, not the Label) kept working. Deferring the
    /// assignment did not reliably fix it either. A styled-but-unreadable snippet is strictly worse than
    /// a readable one in the default face, and nothing else in the SDK needs a font at runtime.</summary>
    static class CodeSnippetBlock
    {
        internal static VisualElement Create(string title, string code)
        {
            var card = new VisualElement();
            card.AddToClassList("sorolla-snippet");

            var titleBar = new VisualElement();
            titleBar.AddToClassList("sorolla-snippet-titlebar");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("sorolla-snippet-title");
            titleBar.Add(titleLabel);

            // Same bordered-button style as every other row action (Edit/Console/Open Dashboard) - was
            // its own "sorolla-snippet-copy" chip, which read as bare text next to those (refuter
            // follow-up, 2026-07-21).
            var copyButton = new Button(() => GUIUtility.systemCopyBuffer = code) { text = "Copy" };
            copyButton.AddToClassList("sorolla-button-small");
            titleBar.Add(copyButton);

            card.Add(titleBar);

            var body = new Label(code);
            body.AddToClassList("sorolla-snippet-body");
            card.Add(body);

            return card;
        }
    }
}
