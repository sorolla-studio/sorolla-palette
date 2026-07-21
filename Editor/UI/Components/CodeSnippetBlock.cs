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

            // Same bordered-button style as every other row action (Edit/Console/Open Dashboard) - was
            // its own "sorolla-snippet-copy" chip, which read as bare text next to those (refuter
            // follow-up, 2026-07-21).
            var copyButton = new Button(() => GUIUtility.systemCopyBuffer = code) { text = "Copy" };
            copyButton.AddToClassList("sorolla-button-small");
            titleBar.Add(copyButton);

            card.Add(titleBar);

            var body = new Label(code);
            body.AddToClassList("sorolla-snippet-body");
            ApplyMonoFont(body);
            card.Add(body);

            return card;
        }

        /// <summary>Font.CreateDynamicFontFromOSFont silently returns null when called synchronously
        /// during CreateGUI() construction (verified: the identical call succeeds when made from any
        /// other context) - assigning a FontDefinition wrapping a null Font then renders the Label
        /// completely blank instead of falling back to the default font, which is how this shipped
        /// with invisible Quick Start snippet bodies. Deferring via the UI Toolkit scheduler runs the
        /// assignment on a later update tick, outside CreateGUI's synchronous call stack, where the
        /// same API call succeeds; if it's ever still null, skip the assignment entirely rather than
        /// risk another null-FontDefinition blank-render.</summary>
        static void ApplyMonoFont(Label label)
        {
            label.schedule.Execute(() =>
            {
                Font font = MonoFont;
                if (font != null)
                    label.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            });
        }
    }
}
