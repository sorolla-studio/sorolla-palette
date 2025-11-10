using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using System;
using System.Linq;

namespace SorollaPalette.Editor
{
    public class SorollaPaletteWindow : EditorWindow
    {
        private const string MODE_KEY = "SorollaPalette_Mode";
        private const string FACEBOOK_DEFINE = "SOROLLA_FACEBOOK_ENABLED";
        private const string MAX_DEFINE = "SOROLLA_MAX_ENABLED";
        private const string ADJUST_DEFINE = "SOROLLA_ADJUST_ENABLED";
        
        private SorollaPaletteConfig _config;
        private Vector2 _scrollPos;
        
        [MenuItem("Sorolla Palette/Configuration")]
        public static void ShowWindow()
        {
            var window = GetWindow<SorollaPaletteWindow>("Sorolla Palette");
            window.minSize = new Vector2(650, 600);
            window.Show();
        }
        
        private void OnEnable()
        {
            LoadOrCreateConfig();
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
            
            string currentMode = EditorPrefs.GetString(MODE_KEY, "Not Selected");
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Current Mode: ", GUILayout.Width(100));
            
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
            
            if (GUILayout.Button("Change Mode"))
            {
                SorollaPaletteModeSelector.ShowModeSelector();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawSDKStatusSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("SDK Status", EditorStyles.boldLabel);
            GUILayout.Space(5);

            string currentMode = EditorPrefs.GetString(MODE_KEY, "Not Selected");

            DrawSDKStatus("GameAnalytics", IsGameAnalyticsInstalled(), true);

            // Show MAX status in both modes
            DrawSDKStatus("AppLovin MAX", IsMaxInstalled(), currentMode == "Full");

            // Facebook only in Prototype mode (required)
            if (currentMode == "Prototype")
            {
                DrawSDKStatus("Facebook SDK", IsFacebookInstalled(), true);
            }

            // Adjust only in Full mode
            if (currentMode == "Full")
            {
                DrawSDKStatus("Adjust SDK", IsAdjustInstalled(), true);
            }

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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Module Management", EditorStyles.boldLabel);
            GUILayout.Space(5);

            string currentMode = EditorPrefs.GetString(MODE_KEY, "Not Selected");

            // Facebook Module - Only show in Prototype Mode (Required)
            if (currentMode == "Prototype")
            {
                DrawModuleToggle("Facebook Module", FACEBOOK_DEFINE, IsFacebookInstalled(),
                    "(Required for Prototype)", Color.red);
            }

            // MAX Module - Optional in both modes, but required for Full Mode
            DrawModuleToggle("AppLovin MAX Module", MAX_DEFINE, IsMaxInstalled(),
                currentMode == "Full" ? "(Required for Full Mode)" : "(Optional)", Color.red,
                SorollaPaletteSetup.InstallAppLovinMAX, "AppLovin MAX");

            // Adjust Module - Only show in Full Mode
            if (currentMode == "Full")
            {
                DrawModuleToggle("Adjust Module", ADJUST_DEFINE, IsAdjustInstalled(),
                    "(Required for Full Mode)", Color.red);
            }

            EditorGUILayout.EndVertical();
        }
        
        private void DrawModuleToggle(string moduleName, string defineSymbol, bool isInstalled, 
            string statusLabel, Color statusColor, System.Action installAction = null, string sdkName = null)
        {
            bool isEnabled = IsDefineEnabled(defineSymbol);
            bool hasInstallAction = installAction != null;
            
            EditorGUI.BeginDisabledGroup(!isInstalled && !hasInstallAction);
            EditorGUILayout.BeginHorizontal();
            
            bool newState = EditorGUILayout.Toggle(moduleName, isEnabled);
            if (newState != isEnabled)
            {
                if (newState && !isInstalled && hasInstallAction)
                {
                    if (EditorUtility.DisplayDialog($"Install {sdkName}", 
                        $"{sdkName} SDK is not installed. Do you want to install it now?", 
                        "Install", "Cancel"))
                    {
                        installAction();
                        EditorUtility.DisplayDialog("Installing...", 
                            $"{sdkName} is being installed. Please wait for Package Manager to resolve, then enable the module again.", 
                            "OK");
                    }
                }
                else
                {
                    SetDefineEnabled(defineSymbol, newState);
                }
            }
            
            if (!isInstalled)
            {
                GUILayout.Label("(SDK not installed)", EditorStyles.miniLabel);
                if (hasInstallAction && GUILayout.Button("Install", GUILayout.Width(60)))
                {
                    installAction();
                }
            }
            else if (!string.IsNullOrEmpty(statusLabel))
            {
                var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = statusColor } };
                GUILayout.Label(statusLabel, style);
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();
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
            
            SerializedObject serializedConfig = new SerializedObject(_config);
            
            DrawConfigGroup(serializedConfig, "GameAnalytics", true, "gaGameKey", "gaSecretKey");
            DrawConfigGroup(serializedConfig, "AppLovin MAX", IsDefineEnabled(MAX_DEFINE), "maxSdkKey", "maxRewardedAdUnitId", "maxInterstitialAdUnitId");
            DrawConfigGroup(serializedConfig, "Facebook", IsDefineEnabled(FACEBOOK_DEFINE), "facebookAppId");
            DrawConfigGroup(serializedConfig, "Adjust", IsDefineEnabled(ADJUST_DEFINE), "adjustAppToken", "adjustEnvironment");
            
            serializedConfig.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }
        
        private void DrawConfigGroup(SerializedObject serializedConfig, string label, bool enabled, params string[] propertyNames)
        {
            if (!enabled) return;
            
            GUILayout.Label(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            foreach (var propertyName in propertyNames)
            {
                EditorGUILayout.PropertyField(serializedConfig.FindProperty(propertyName));
            }
            
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
                if (GUILayout.Button("Create Configuration Asset", GUILayout.Height(30)))
                {
                    CreateConfig();
                }
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
            string[] guids = AssetDatabase.FindAssets("t:SorollaPaletteConfig");
            
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _config = AssetDatabase.LoadAssetAtPath<SorollaPaletteConfig>(path);
            }
        }
        
        private void CreateConfig()
        {
            _config = CreateInstance<SorollaPaletteConfig>();
            
            string path = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/SorollaPaletteConfig.asset");
            AssetDatabase.CreateAsset(_config, assetPath);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[Sorolla Palette] Configuration asset created at: {assetPath}");
            
            EditorGUIUtility.PingObject(_config);
            Selection.activeObject = _config;
        }
        
        private bool IsSDKInstalled(params string[] typeNames)
        {
            return typeNames.Any(typeName => Type.GetType(typeName) != null);
        }
        
        private bool IsGameAnalyticsInstalled() => IsSDKInstalled(
            "GameAnalytics, GameAnalyticsSDK",
            "GameAnalyticsSDK.GameAnalytics, GameAnalyticsSDK"
        );
        
        private bool IsFacebookInstalled() => IsSDKInstalled(
            "Facebook.Unity.FB, Facebook.Unity"
        );
        
        private bool IsMaxInstalled() => IsSDKInstalled(
            "MaxSdk, MaxSdk.Scripts",
            "MaxSdkBase, MaxSdk.Scripts"
        );
        
        private bool IsAdjustInstalled() => IsSDKInstalled(
            "com.adjust.sdk.Adjust, Assembly-CSharp",
            "com.adjust.sdk.Adjust, Adjust"
        );
        
        private bool IsDefineEnabled(string define)
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            return defines.Contains(define);
        }
        
        private void SetDefineEnabled(string define, bool enabled)
        {
            NamedBuildTarget buildTarget = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            
            var defineList = defines.Split(';').ToList();
            
            if (enabled && !defineList.Contains(define))
            {
                defineList.Add(define);
                Debug.Log($"[Sorolla Palette] Enabled module: {define}");
            }
            else if (!enabled && defineList.Contains(define))
            {
                defineList.Remove(define);
                Debug.Log($"[Sorolla Palette] Disabled module: {define}");
            }
            else
            {
                return; // No change needed
            }
            
            string newDefines = string.Join(";", defineList.Where(d => !string.IsNullOrEmpty(d)));
            PlayerSettings.SetScriptingDefineSymbols(buildTarget, newDefines);
            
            Debug.Log("[Sorolla Palette] Scripting defines updated. Unity will recompile...");
        }
    }
}

