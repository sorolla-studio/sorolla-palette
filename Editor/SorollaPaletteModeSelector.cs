using System;
using UnityEditor;
using UnityEngine;

namespace SorollaPalette.Editor
{
    public class SorollaPaletteModeSelector : EditorWindow
    {
        private const string MODE_KEY = "SorollaPalette_Mode";
        private const string MODE_SELECTED_KEY = "SorollaPalette_ModeSelected";

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
            // Show mode selector on first import
            if (!SessionState.GetBool(MODE_SELECTED_KEY, false))
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
            EditorPrefs.SetString(MODE_KEY, mode);
            SessionState.SetBool(MODE_SELECTED_KEY, true);

            Debug.Log($"[Sorolla Palette] Mode set to: {mode}");

            // Auto-install MAX and Adjust if Full Mode is selected
            if (mode == "Full")
            {
                // Check if MAX is already installed
                var isMaxInstalled = Type.GetType("MaxSdk, MaxSdk.Scripts") != null ||
                                      Type.GetType("MaxSdkBase, MaxSdk.Scripts") != null;

                if (!isMaxInstalled)
                {
                    Debug.Log("[Sorolla Palette] Full Mode requires AppLovin MAX. Installing automatically...");
                    SorollaPaletteSetup.InstallAppLovinMAX();
                }

                // Check if Adjust is already installed
                var isAdjustInstalled = Type.GetType("com.adjust.sdk.Adjust, com.adjust.sdk") != null;

                if (!isAdjustInstalled)
                {
                    Debug.Log("[Sorolla Palette] Full Mode requires Adjust SDK. Installing automatically...");
                    SorollaPaletteSetup.InstallAdjustSDK();
                }
            }

            Debug.Log("[Sorolla Palette] Open 'Sorolla Palette → Configuration' to set up your SDKs.");

            Close();

            // Open configuration window
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Sorolla Palette/Configuration");
            };
        }
    }
}