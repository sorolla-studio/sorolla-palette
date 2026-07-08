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

        static readonly (string Label, string BgClass)[] BgSwatches =
        {
            ("bg-page", "sorolla-bg-page"),
            ("bg-card", "sorolla-bg-card"),
            ("bg-card-alt", "sorolla-bg-card-alt"),
            ("bg-section", "sorolla-bg-section"),
            ("bg-elevated", "sorolla-bg-elevated"),
            ("bg-elevated-hover", "sorolla-bg-elevated-hover"),
            ("bg-accent-muted", "sorolla-bg-accent-muted"),
            ("bg-accent", "sorolla-bg-accent"),
            ("fail-tint", "sorolla-status-fail-tint-bg"),
            ("warn-tint", "sorolla-status-warn-tint-bg"),
        };

        static readonly (string Label, string PillClass)[] StatusPills =
        {
            ("PASS", "sorolla-pill-pass"),
            ("WARN", "sorolla-pill-warn"),
            ("FAIL", "sorolla-pill-fail"),
            ("WAIT", "sorolla-pill-wait"),
            ("INFO", "sorolla-pill-info"),
        };

        static readonly (string Text, string TypeClass)[] TypeScale =
        {
            ("Title / editor-type-title 18px", "sorolla-type-title"),
            ("Section / editor-type-section 13px", "sorolla-type-section"),
            ("Body / editor-type-body 12px", "sorolla-type-body"),
            ("Small / editor-type-small 11px", "sorolla-type-small"),
        };

        void CreateGUI()
        {
            rootVisualElement.AddToClassList("sorolla-root");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            rootVisualElement.Add(scrollView);

            scrollView.Add(BuildTokenSwatchSection());
            foreach (string section in Sections)
                scrollView.Add(BuildSectionPlaceholder(section));
        }

        static VisualElement BuildTokenSwatchSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("Design Tokens (TOKENS.md swatch sheet)");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            var bgRow = new VisualElement();
            bgRow.AddToClassList("sorolla-swatch-row");
            foreach ((string label, string bgClass) in BgSwatches)
            {
                var swatch = new VisualElement();
                swatch.AddToClassList("sorolla-swatch");
                swatch.AddToClassList(bgClass);
                var swatchLabel = new Label(label);
                swatchLabel.AddToClassList("sorolla-swatch-label");
                swatch.Add(swatchLabel);
                bgRow.Add(swatch);
            }
            container.Add(bgRow);

            var pillRow = new VisualElement();
            pillRow.AddToClassList("sorolla-swatch-row");
            foreach ((string label, string pillClass) in StatusPills)
            {
                var pill = new VisualElement();
                pill.AddToClassList("sorolla-swatch-pill");
                pill.AddToClassList(pillClass);
                var pillLabel = new Label(label);
                pillLabel.AddToClassList("sorolla-swatch-pill-label");
                pill.Add(pillLabel);
                pillRow.Add(pill);
            }
            container.Add(pillRow);

            foreach ((string text, string typeClass) in TypeScale)
            {
                var typeLabel = new Label(text);
                typeLabel.AddToClassList(typeClass);
                container.Add(typeLabel);
            }

            return container;
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
