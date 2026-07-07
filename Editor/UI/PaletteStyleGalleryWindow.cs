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

        [MenuItem("Palette/UI Lab/Style Gallery")]
        static void Open() => GetWindow<PaletteStyleGalleryWindow>("Palette Style Gallery");

        void OnGUI()
        {
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
