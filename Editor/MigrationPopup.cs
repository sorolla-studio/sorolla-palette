using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     One-time migration popup for v3.1.0 Firebase mandatory upgrade.
    ///     Guides users through Firebase setup after SDK upgrade.
    /// </summary>
    public class MigrationPopup : EditorWindow
    {
        const float WindowWidth = 400f;
        const float WindowHeight = 280f;

        public static void Display()
        {
            var window = GetWindow<MigrationPopup>(true, "Sorolla SDK 3.1 - Firebase Required");
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.maxSize = new Vector2(WindowWidth, WindowHeight);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);

            // Header
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Firebase is now required for all modes.", headerStyle);

            EditorGUILayout.Space(5);

            var subHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            EditorGUILayout.LabelField("Firebase packages have been auto-installed.", subHeaderStyle);

            EditorGUILayout.Space(15);

            // Setup checklist box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Complete setup:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Step 1
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("1. Create Firebase project", GUILayout.Width(200));
            if (GUILayout.Button("Open Firebase Console", GUILayout.Width(150)))
            {
                Application.OpenURL("https://console.firebase.google.com/");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Step 2
            EditorGUILayout.LabelField("2. Download config files to Assets/", EditorStyles.label);
            EditorGUILayout.Space(3);

            var bulletStyle = new GUIStyle(EditorStyles.miniLabel) { padding = new RectOffset(20, 0, 0, 0) };
            EditorGUILayout.LabelField("- google-services.json (Android)", bulletStyle);
            EditorGUILayout.LabelField("- GoogleService-Info.plist (iOS)", bulletStyle);

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(15);

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Configuration", GUILayout.Width(130), GUILayout.Height(25)))
            {
                SorollaWindow.ShowWindow();
                Close();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Close", GUILayout.Width(80), GUILayout.Height(25)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
        }
    }
}
