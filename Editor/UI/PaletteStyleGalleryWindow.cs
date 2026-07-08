using UnityEditor;
using UnityEngine;

namespace Sorolla.Editor.UI
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

        Vector2 _scroll;

        /// <summary>Screen-space position of this window's own (0,0) GUI point, refreshed every
        /// repaint. UiLabCapture reads this via reflection for exact-origin framing instead of
        /// deriving it from EditorWindow.position + the main-window offset heuristic.</summary>
        internal Vector2 ScreenOrigin { get; private set; }

        /// <summary>True once ScreenOrigin has been recorded from an actual Repaint event - guards
        /// against a stale default (0,0) being read before the window has painted a single frame.</summary>
        internal bool ScreenOriginValid { get; private set; }

        [MenuItem("Palette/UI Lab/Style Gallery")]
        static void Open() => GetWindow<PaletteStyleGalleryWindow>("Palette Style Gallery");

        void OnGUI()
        {
            if (Event.current.type == EventType.Repaint)
            {
                ScreenOrigin = GUIUtility.GUIToScreenPoint(Vector2.zero);
                ScreenOriginValid = true;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (string section in Sections)
                DrawSectionPlaceholder(section);
            EditorGUILayout.EndScrollView();
        }

        static void DrawSectionPlaceholder(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Not yet implemented — placeholder for the phase-2 component cycle.", MessageType.None);
        }
    }
}
