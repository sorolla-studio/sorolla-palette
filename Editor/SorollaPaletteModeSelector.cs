using UnityEditor;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// Simplified mode selector - KISS approach with single method calls
    /// </summary>
    public class SorollaPaletteModeSelector : EditorWindow
    {
        private void OnGUI()
        {
            GUILayout.Space(20);

            // Title
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Welcome to Sorolla Palette!", titleStyle);

            GUILayout.Space(10);

            var descStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Choose your development mode:", descStyle);

            GUILayout.Space(30);

            // Prototype Mode Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("🧪 Prototype Mode", EditorStyles.boldLabel);
            GUILayout.Space(5);

            var protoDesc = new GUIStyle(EditorStyles.label) { wordWrap = true };
            GUILayout.Label(
                "For CPI testing and early prototypes:\n" +
                "• GameAnalytics (analytics + remote config)\n" +
                "• Facebook SDK (UA tracking)\n" +
                "• AppLovin MAX (optional - for testing ads)",
                protoDesc
            );

            GUILayout.Space(10);

            if (GUILayout.Button("Select Prototype Mode", GUILayout.Height(40))) SelectMode("Prototype");

            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            // Full Mode Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("🚀 Full Mode", EditorStyles.boldLabel);
            GUILayout.Space(5);

            var fullDesc = new GUIStyle(EditorStyles.label) { wordWrap = true };
            GUILayout.Label(
                "For production-ready games:\n" +
                "• GameAnalytics (analytics + remote config)\n" +
                "• AppLovin MAX (monetization)\n" +
                "• Adjust SDK (attribution tracking)",
                fullDesc
            );

            GUILayout.Space(10);

            if (GUILayout.Button("Select Full Mode", GUILayout.Height(40))) SelectMode("Full");

            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            var noteStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            GUILayout.Label("You can change this later via: Sorolla Palette → Select Mode", noteStyle);
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Show mode selector on first import if no mode selected
            if (!ModeManager.IsModeSelected())
                EditorApplication.delayCall += () =>
                {
                    // Wait a bit for package resolution to complete
                    EditorApplication.delayCall += ShowModeSelector;
                };
        }

        [MenuItem("Sorolla Palette/Select Mode")]
        public static void ShowModeSelector()
        {
            var window = GetWindow<SorollaPaletteModeSelector>(true, "Sorolla Palette - Select Mode", true);
            window.minSize = new Vector2(500, 450);
            window.maxSize = new Vector2(500, 450);
            window.ShowUtility();
        }

        private void SelectMode(string mode)
        {
            // Single method call handles everything
            ModeManager.SetMode(mode);

            Close();

            // Open configuration window
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Sorolla Palette/Configuration");
            };
        }
    }
}