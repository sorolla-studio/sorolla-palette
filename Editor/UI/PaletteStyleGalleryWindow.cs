using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    /// Dev-only isolation surface for the UI-overhaul loop: renders every planned editor
    /// component/state on one scrollable page so a component can be iterated and screenshotted
    /// without opening the real Palette window. Sections start as placeholders and get replaced
    /// by real components one phase-2 cycle at a time (see docs/ui-overhaul/PLAN.md).
    /// Ships inside the SDK package but is editor-only tooling, never referenced at runtime.
    /// </summary>
    sealed class PaletteStyleGalleryWindow : EditorWindow
    {
        const string TokensUssPath = "Packages/com.sorolla.sdk/Editor/UI/tokens.uss";

        static readonly string[] Sections =
        {
            "StatusBadge",
            "CalloutCard",
            "SectionHeader",
            "CheckRow / CollapsibleCheckGroup",
            "ValidatedField",
            "CodeSnippetBlock",
            "HeroHeader",
        };

        [MenuItem("Palette/UI Lab/Style Gallery")]
        static void Open() => GetWindow<PaletteStyleGalleryWindow>("Palette Style Gallery");

        void CreateGUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            rootVisualElement.Add(scrollView);

            foreach (string section in Sections)
                scrollView.Add(BuildSectionPlaceholder(section));
        }

        static VisualElement BuildSectionPlaceholder(string title)
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");
            container.style.marginTop = 8;

            var label = new Label(title);
            label.AddToClassList("gallery-section-title");
            container.Add(label);

            var placeholder = new Label("Not yet implemented — placeholder for the phase-2 component cycle.");
            placeholder.AddToClassList("gallery-section-placeholder");
            container.Add(placeholder);

            return container;
        }
    }
}
