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

        /// <summary>Screen-space position of this window's own (0,0) GUI point, refreshed every
        /// repaint. UiLabCapture reads this via reflection for exact-origin framing instead of
        /// deriving it from EditorWindow.position + the main-window offset heuristic.</summary>
        internal Vector2 ScreenOrigin { get; private set; }

        /// <summary>True once ScreenOrigin has been recorded from an actual Repaint event - guards
        /// against a stale default (0,0) being read before the window has painted a single frame.</summary>
        internal bool ScreenOriginValid { get; private set; }

        [MenuItem("Palette/UI Lab/Style Gallery")]
        static void Open() => GetWindow<PaletteStyleGalleryWindow>("Palette Style Gallery");

        void CreateGUI()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            // GUIUtility.GUIToScreenPoint only resolves correctly inside an active IMGUI OnGUI
            // callback (it reads the current GUIClip stack, which UI Toolkit's own event
            // callbacks - e.g. GeometryChangedEvent - never push). A zero-size IMGUIContainer
            // gives us that real OnGUI context to record an accurate origin from.
            var originProbe = new IMGUIContainer(RecordScreenOrigin) { style = { height = 0 } };
            rootVisualElement.Add(originProbe);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            rootVisualElement.Add(scrollView);

            foreach (string section in Sections)
                scrollView.Add(BuildSectionPlaceholder(section));
        }

        void RecordScreenOrigin()
        {
            if (Event.current.type != EventType.Repaint) return;
            ScreenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
            ScreenOriginValid = true;
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
