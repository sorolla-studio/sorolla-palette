using UnityEditor;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    /// Streamlined configuration window - KISS approach with single-page configuration
    /// </summary>
    public class SorollaPaletteWindow : EditorWindow
    {
        private SorollaPaletteConfig _config;
        private Vector2 _scrollPos;

        private void OnEnable()
        {
            LoadOrCreateConfig();
            // Defines are managed centrally by SorollaDefineSync
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            GUILayout.Space(10);

            DrawModeSection();
            GUILayout.Space(10);

            DrawSDKStatusSection();
            GUILayout.Space(10);

            DrawModuleToggles();
            GUILayout.Space(10);

            DrawConfigSection();
            GUILayout.Space(10);

            DrawActionsSection();

            EditorGUILayout.EndScrollView();
        }

        [MenuItem("Sorolla Palette/Configuration")]
        public static void ShowWindow()
        {
            var window = GetWindow<SorollaPaletteWindow>("Sorolla Palette");
            window.minSize = new Vector2(650, 600);
            window.Show();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("Sorolla Palette Configuration", titleStyle);

            GUILayout.Space(5);

            var versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label("v1.0.0", versionStyle);

            EditorGUILayout.EndVertical();
        }

        private void DrawModeSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Development Mode", EditorStyles.boldLabel);

            var currentMode = ModeManager.GetCurrentMode();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Current Mode: ", GUILayout.Width(100));

            var modeStyle = new GUIStyle(EditorStyles.boldLabel);
            if (currentMode == "Prototype")
            {
                modeStyle.normal.textColor = new Color(0.3f, 0.7f, 1f);
                GUILayout.Label("🧪 Prototype Mode", modeStyle);
            }
            else if (currentMode == "Full")
            {
                modeStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
                GUILayout.Label("🚀 Full Mode", modeStyle);
            }
            else
            {
                modeStyle.normal.textColor = Color.yellow;
                GUILayout.Label("⚠️ Not Selected", modeStyle);
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (GUILayout.Button("Change Mode")) SorollaPaletteModeSelector.ShowModeSelector();

            EditorGUILayout.EndVertical();
        }

        private void DrawSDKStatusSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("SDK Status", EditorStyles.boldLabel);
            GUILayout.Space(5);

            var currentMode = ModeManager.GetCurrentMode();

            DrawSDKStatus("GameAnalytics", SdkDetection.IsGameAnalyticsInstalled(), true);

            // Show MAX status in both modes
            DrawSDKStatus("AppLovin MAX", SdkDetection.IsMaxInstalled(), currentMode == "Full");

            // Facebook only in Prototype mode (required)
            if (currentMode == "Prototype") DrawSDKStatus("Facebook SDK", SdkDetection.IsFacebookInstalled(), true);

            // Adjust: Full only
            if (currentMode == "Full") DrawSDKStatus("Adjust SDK", SdkDetection.IsAdjustInstalled(), true);

            EditorGUILayout.EndVertical();
        }

        private void DrawSDKStatus(string sdkName, bool isInstalled, bool isRequired)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(sdkName, GUILayout.Width(150));

            var style = new GUIStyle(EditorStyles.label);

            if (isInstalled)
            {
                style.normal.textColor = Color.green;
                GUILayout.Label("✅ Installed", style);
            }
            else
            {
                style.normal.textColor = isRequired ? Color.red : Color.yellow;
                GUILayout.Label(isRequired ? "❌ Not Found (Required)" : "⚠️ Not Found", style);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawModuleToggles()
        {
            var currentMode = ModeManager.GetCurrentMode();
            if (currentMode == "Full") return;
            if (currentMode == "Prototype")
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("Optional Package", EditorStyles.boldLabel);
                GUILayout.Space(5);
                var isInstalled = SdkDetection.IsMaxInstalled();
                var desired = EditorGUILayout.Toggle("AppLovin MAX (Prototype)", isInstalled);
                if (desired && !isInstalled)
                {
                    SorollaPaletteSetup.InstallAppLovinMAX();
                    DefineManager.SetDefineEnabled(DefineManager.MAX_DEFINE, true);
                }
                else if (!desired && isInstalled)
                {
                    DefineManager.SetDefineEnabled(DefineManager.MAX_DEFINE, false);
                    SorollaPaletteSetup.UninstallAppLovinMAX();
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawConfigSection()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("No configuration found. Click 'Create Config' below.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Configuration", EditorStyles.boldLabel);
            GUILayout.Space(5);

            var serializedConfig = new SerializedObject(_config);

            var currentMode = ModeManager.GetCurrentMode();

            // Always show GA
            DrawConfigGroup(serializedConfig, "GameAnalytics", true, "gaGameKey", "gaSecretKey");

            // MAX: Required in Full, optional in Prototype
            if (currentMode == "Full" || SdkDetection.IsMaxInstalled())
                DrawConfigGroup(serializedConfig, "AppLovin MAX", true, "maxSdkKey", "maxRewardedAdUnitId",
                    "maxInterstitialAdUnitId");

            // Facebook: Prototype only
            if (currentMode == "Prototype") DrawConfigGroup(serializedConfig, "Facebook", true, "facebookAppId");

            // Adjust: Full only
            if (currentMode == "Full")
                DrawConfigGroup(serializedConfig, "Adjust", true, "adjustAppToken", "adjustEnvironment");

            serializedConfig.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        private void DrawConfigGroup(SerializedObject serializedConfig, string label, bool enabled,
            params string[] propertyNames)
        {
            if (!enabled) return;

            GUILayout.Label(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            foreach (var propertyName in propertyNames)
                EditorGUILayout.PropertyField(serializedConfig.FindProperty(propertyName));

            EditorGUI.indentLevel--;
            GUILayout.Space(10);
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Actions", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (_config == null)
            {
                if (GUILayout.Button("Create Configuration Asset", GUILayout.Height(30))) CreateConfig();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Save Configuration"))
                {
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Sorolla Palette] Configuration saved!");
                }

                if (GUILayout.Button("Locate Asset"))
                {
                    EditorGUIUtility.PingObject(_config);
                    Selection.activeObject = _config;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // Helper Methods

        private void LoadOrCreateConfig()
        {
            var guids = AssetDatabase.FindAssets("t:SorollaPaletteConfig");

            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _config = AssetDatabase.LoadAssetAtPath<SorollaPaletteConfig>(path);
            }
        }

        private void CreateConfig()
        {
            _config = CreateInstance<SorollaPaletteConfig>();

            var path = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder("Assets", "Resources");

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/SorollaPaletteConfig.asset");
            AssetDatabase.CreateAsset(_config, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Sorolla Palette] Configuration asset created at: {assetPath}");

            EditorGUIUtility.PingObject(_config);
            Selection.activeObject = _config;
        }


        private void SetDefineEnabled(string define, bool enabled)
        {
            DefineManager.SetDefineEnabled(define, enabled);
        }
    }
}