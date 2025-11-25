using UnityEditor;
using UnityEngine;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Configuration window for Sorolla SDK.
    /// </summary>
    public class SorollaWindow : EditorWindow
    {
        private SorollaConfig _config;
        private Vector2 _scrollPos;

        [MenuItem("Sorolla/Configuration")]
        public static void ShowWindow()
        {
            var window = GetWindow<SorollaWindow>("Sorolla");
            window.minSize = new Vector2(500, 450);
            window.Show();
        }

        [InitializeOnLoadMethod]
        private static void AutoOpenOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SorollaSettings.IsConfigured && !Application.isPlaying)
                    ShowWindow();
            };
        }

        private void OnEnable() => LoadOrCreateConfig();

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space(10);

            if (!SorollaSettings.IsConfigured)
                DrawWelcomeScreen();
            else
                DrawMainUI();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Sorolla SDK", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("v1.0.0 - Plug & Play Publisher Stack", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
        }

        private void DrawWelcomeScreen()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(10);

            GUILayout.Label("Welcome! Select a mode to get started:", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.Space(15);

            if (GUILayout.Button("🧪  Prototype Mode\n(Facebook SDK for UA)", GUILayout.Height(45)))
                SorollaSettings.SetPrototypeMode();

            EditorGUILayout.Space(8);

            if (GUILayout.Button("🚀  Full Mode\n(Adjust + MAX for Production)", GUILayout.Height(45)))
                SorollaSettings.SetFullMode();

            EditorGUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        private void DrawMainUI()
        {
            DrawModeSection();
            EditorGUILayout.Space(10);
            DrawSdkStatusSection();
            EditorGUILayout.Space(10);
            DrawConfigSection();
            EditorGUILayout.Space(10);
            DrawInfoSection();
        }

        private void DrawModeSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Mode", EditorStyles.boldLabel);

            var mode = SorollaSettings.Mode;
            var isPrototype = mode == SorollaMode.Prototype;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Current:", GUILayout.Width(60));

            var modeStyle = new GUIStyle(EditorStyles.boldLabel);
            modeStyle.normal.textColor = isPrototype ? new Color(0.3f, 0.7f, 1f) : new Color(0.3f, 1f, 0.3f);
            GUILayout.Label(isPrototype ? "🧪 Prototype" : "🚀 Full", modeStyle);

            GUILayout.FlexibleSpace();

            var otherMode = isPrototype ? SorollaMode.Full : SorollaMode.Prototype;
            if (GUILayout.Button($"Switch to {otherMode}", GUILayout.Width(130)))
            {
                if (EditorUtility.DisplayDialog($"Switch to {otherMode} Mode?",
                    "This will install/uninstall SDKs as needed.", "Switch", "Cancel"))
                    SorollaSettings.SetMode(otherMode);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawSdkStatusSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("SDK Status", EditorStyles.boldLabel);

            var isPrototype = SorollaSettings.IsPrototype;

            // Show required SDKs for current mode
            foreach (var sdk in SdkRegistry.GetRequired(isPrototype))
                DrawSdkStatus(sdk);

            // Show optional MAX in prototype mode
            if (isPrototype)
                DrawSdkStatus(SdkRegistry.All[SdkId.AppLovinMAX], isOptional: true);

            EditorGUILayout.EndVertical();
        }

        private void DrawSdkStatus(SdkInfo sdk, bool isOptional = false)
        {
            var isInstalled = SdkDetector.IsInstalled(sdk);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(sdk.Name + (isOptional ? " (optional)" : ""), GUILayout.Width(180));

            var style = new GUIStyle(EditorStyles.label);
            if (isInstalled)
            {
                style.normal.textColor = Color.green;
                GUILayout.Label("✓ Installed", style);
            }
            else
            {
                style.normal.textColor = isOptional ? Color.yellow : Color.red;
                GUILayout.Label(isOptional ? "○ Not installed" : "✗ Missing", style);

                if (GUILayout.Button("Install", GUILayout.Width(60)))
                    SdkInstaller.Install(sdk.Id);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigSection()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("No config found. Click below to create one.", MessageType.Warning);
                if (GUILayout.Button("Create Configuration Asset"))
                    CreateConfig();
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Configuration", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox("GameAnalytics: Use GameAnalytics → Setup Wizard", MessageType.Info);

            var serializedConfig = new SerializedObject(_config);
            var isPrototype = SorollaSettings.IsPrototype;

            // MAX config (if installed)
            if (SdkDetector.IsInstalled(SdkId.AppLovinMAX))
            {
                EditorGUILayout.Space(5);
                GUILayout.Label("AppLovin MAX", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxSdkKey"));
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxRewardedAdUnitId"));
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxInterstitialAdUnitId"));
                EditorGUI.indentLevel--;
            }

            // Adjust config (full mode only)
            if (!isPrototype)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label("Adjust", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("adjustAppToken"));
                EditorGUI.indentLevel--;
            }

            serializedConfig.ApplyModifiedProperties();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();
                Debug.Log("[Sorolla] Configuration saved.");
            }
            if (GUILayout.Button("Select Asset"))
            {
                Selection.activeObject = _config;
                EditorGUIUtility.PingObject(_config);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawInfoSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("ℹ️ Auto-Initialization", EditorStyles.boldLabel);
            GUILayout.Label(
                "The SDK auto-initializes when your game starts.\n" +
                "• iOS: Shows ATT consent dialog automatically\n" +
                "• Use Sorolla.TrackDesignEvent() to track events",
                new GUIStyle(EditorStyles.label) { wordWrap = true });
            EditorGUILayout.EndVertical();
        }

        private void LoadOrCreateConfig()
        {
            var guids = AssetDatabase.FindAssets("t:SorollaConfig");
            if (guids.Length > 0)
                _config = AssetDatabase.LoadAssetAtPath<SorollaConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            else
                CreateConfig();
        }

        private void CreateConfig()
        {
            _config = CreateInstance<SorollaConfig>();

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/SorollaConfig.asset");
            AssetDatabase.CreateAsset(_config, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Sorolla] Config created at: {path}");
            Selection.activeObject = _config;
        }
    }
}
