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
            window.minSize = new Vector2(420, 380);
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
            DrawSetupChecklist();
            EditorGUILayout.Space(10);
            DrawSdkStatusSection();
            EditorGUILayout.Space(10);
            DrawConfigSection();
            EditorGUILayout.Space(10);
            DrawInfoSection();
            EditorGUILayout.Space(8);
            DrawLinksSection();
        }

        private void DrawSetupChecklist()
        {
            var isPrototype = SorollaSettings.IsPrototype;
            
            // Calculate overall status
            var gaStatus = SdkConfigDetector.GetGameAnalyticsStatus();
            var fbStatus = SdkConfigDetector.GetFacebookStatus();
            var maxStatus = SdkConfigDetector.GetMaxStatus(_config);
            var adjustStatus = SdkConfigDetector.GetAdjustStatus(_config);
            
            // Determine if fully configured
            var isFullyConfigured = gaStatus == SdkConfigDetector.ConfigStatus.Configured &&
                (isPrototype 
                    ? fbStatus == SdkConfigDetector.ConfigStatus.Configured
                    : maxStatus == SdkConfigDetector.ConfigStatus.Configured && 
                      adjustStatus == SdkConfigDetector.ConfigStatus.Configured);

            // Header with overall status
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Setup Checklist", EditorStyles.boldLabel);
            
            GUILayout.FlexibleSpace();
            var statusStyle = new GUIStyle(EditorStyles.boldLabel);
            if (isFullyConfigured)
            {
                statusStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                GUILayout.Label("✓ Ready", statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = new Color(1f, 0.7f, 0.2f);
                GUILayout.Label("⚠ Setup Required", statusStyle);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
                
                // GameAnalytics (always required)
                DrawChecklistItem("GameAnalytics", gaStatus, 
                    "Configure your game keys", SdkConfigDetector.OpenGameAnalyticsSettings);
                
                if (isPrototype)
                {
                    // Facebook (prototype mode)
                    DrawChecklistItem("Facebook SDK", fbStatus, 
                        "Set your App ID", SdkConfigDetector.OpenFacebookSettings);
                    
                    // MAX optional in prototype
                    if (SdkDetector.IsInstalled(SdkId.AppLovinMAX))
                    {
                        EditorGUILayout.Space(3);
                        GUILayout.Label("Optional:", EditorStyles.miniLabel);
                    DrawChecklistItem("AppLovin MAX", maxStatus, 
                        "Enter SDK key below", SdkConfigDetector.OpenMaxSettings, isOptional: true);
                }
            }
            else
            {
                // Full mode: MAX + Adjust
                DrawChecklistItem("AppLovin MAX", maxStatus, 
                    "Enter SDK key below", SdkConfigDetector.OpenMaxSettings);
                DrawChecklistItem("Adjust", adjustStatus, 
                    "Enter app token below", null);
            }
            
            EditorGUILayout.EndVertical();
        }        private void DrawChecklistItem(string name, SdkConfigDetector.ConfigStatus status, 
            string hint, System.Action openSettings, bool isOptional = false)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Status icon
            var iconStyle = new GUIStyle(EditorStyles.label) { fixedWidth = 20 };
            switch (status)
            {
                case SdkConfigDetector.ConfigStatus.NotInstalled:
                    iconStyle.normal.textColor = Color.gray;
                    GUILayout.Label("○", iconStyle);
                    break;
                case SdkConfigDetector.ConfigStatus.NotConfigured:
                    iconStyle.normal.textColor = isOptional ? Color.yellow : new Color(1f, 0.5f, 0.2f);
                    GUILayout.Label("○", iconStyle);
                    break;
                case SdkConfigDetector.ConfigStatus.Configured:
                    iconStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                    GUILayout.Label("✓", iconStyle);
                    break;
            }
            
            // Name
            GUILayout.Label(name, GUILayout.Width(120));
            
            // Status text
            var statusText = status switch
            {
                SdkConfigDetector.ConfigStatus.NotInstalled => "Not installed",
                SdkConfigDetector.ConfigStatus.NotConfigured => hint,
                SdkConfigDetector.ConfigStatus.Configured => "Configured",
                _ => ""
            };
            
            var textStyle = new GUIStyle(EditorStyles.miniLabel);
            if (status == SdkConfigDetector.ConfigStatus.Configured)
                textStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
            GUILayout.Label(statusText, textStyle, GUILayout.Width(140));
            
            GUILayout.FlexibleSpace();
            
            // Open Settings button
            if (status != SdkConfigDetector.ConfigStatus.NotInstalled && 
                status != SdkConfigDetector.ConfigStatus.Configured && 
                openSettings != null)
            {
                if (GUILayout.Button("Open Settings", GUILayout.Width(100), GUILayout.Height(20)))
                    openSettings();
            }
            else if (status == SdkConfigDetector.ConfigStatus.Configured && openSettings != null)
            {
                if (GUILayout.Button("Edit", GUILayout.Width(60), GUILayout.Height(20)))
                    openSettings();
            }
            
            EditorGUILayout.EndHorizontal();
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

            // Show optional Firebase Analytics
            DrawFirebaseSdkStatus();

            EditorGUILayout.EndVertical();
        }

        private void DrawFirebaseSdkStatus()
        {
            var isInstalled = SdkDetector.IsInstalled(SdkId.FirebaseAnalytics);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Firebase (optional)", GUILayout.Width(180));

            var style = new GUIStyle(EditorStyles.label);
            if (isInstalled)
            {
                style.normal.textColor = Color.green;
                GUILayout.Label("✓ Installed", style);

                if (GUILayout.Button("Uninstall", GUILayout.Width(70)))
                    SdkInstaller.UninstallFirebase();
            }
            else
            {
                style.normal.textColor = Color.yellow;
                GUILayout.Label("○ Not installed", style);

                if (GUILayout.Button("Install", GUILayout.Width(60)))
                    SdkInstaller.InstallFirebase();
            }
            EditorGUILayout.EndHorizontal();

            // Show installed Firebase modules when installed
            if (isInstalled)
            {
                EditorGUI.indentLevel++;
                var miniStyle = new GUIStyle(EditorStyles.miniLabel) { fixedWidth = 170 };
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                miniStyle.normal.textColor = new Color(0.6f, 0.8f, 0.6f);
                GUILayout.Label("├ Analytics", miniStyle);
                GUILayout.Label(SdkDetector.IsInstalled(SdkId.FirebaseAnalytics) ? "✓" : "○", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label("├ Crashlytics", miniStyle);
                GUILayout.Label(SdkDetector.IsInstalled(SdkId.FirebaseCrashlytics) ? "✓" : "○", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.Label("└ Remote Config", miniStyle);
                GUILayout.Label(SdkDetector.IsInstalled(SdkId.FirebaseRemoteConfig) ? "✓" : "○", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel--;
            }
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

            var serializedConfig = new SerializedObject(_config);
            var isPrototype = SorollaSettings.IsPrototype;

            // Only show config section if MAX or Adjust needs keys
            var showMaxConfig = SdkDetector.IsInstalled(SdkId.AppLovinMAX);
            var showAdjustConfig = !isPrototype && SdkDetector.IsInstalled(SdkId.Adjust);

            if (!showMaxConfig && !showAdjustConfig)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("SDK Keys", EditorStyles.boldLabel);

            // MAX config (if installed)
            if (showMaxConfig)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("AppLovin MAX", EditorStyles.boldLabel, GUILayout.Width(120));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Integration Manager", EditorStyles.miniButton, GUILayout.Width(120)))
                    SdkConfigDetector.OpenMaxSettings();
                EditorGUILayout.EndHorizontal();
                
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxSdkKey"), new GUIContent("SDK Key"));
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxRewardedAdUnitId"), new GUIContent("Rewarded Ad Unit"));
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxInterstitialAdUnitId"), new GUIContent("Interstitial Ad Unit"));
                EditorGUI.indentLevel--;
            }

            // Adjust config (full mode only)
            if (showAdjustConfig)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label("Adjust", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedConfig.FindProperty("adjustAppToken"), new GUIContent("App Token"));
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

            // Firebase section (only show when installed)
            DrawFirebaseConfigSection(serializedConfig);
        }

        private void DrawFirebaseConfigSection(SerializedObject serializedConfig)
        {
            if (!SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                return;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Firebase", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            // Module toggles
            GUILayout.Label("Modules:", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("enableFirebaseAnalytics"), 
                new GUIContent("Analytics", "Track custom events to Firebase Analytics"));
            
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("enableCrashlytics"), 
                new GUIContent("Crashlytics", "Automatic crash and exception reporting"));
            
            EditorGUILayout.PropertyField(serializedConfig.FindProperty("enableRemoteConfig"), 
                new GUIContent("Remote Config", "A/B testing and feature flags"));
            
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);

            // Config file status
            var androidConfigured = SdkConfigDetector.IsFirebaseAndroidConfigured();
            var iosConfigured = SdkConfigDetector.IsFirebaseIOSConfigured();

            GUILayout.Label("Configuration Files:", EditorStyles.miniLabel);

            // Android config
            EditorGUILayout.BeginHorizontal();
            var androidStyle = new GUIStyle(EditorStyles.label) { fixedWidth = 20 };
            androidStyle.normal.textColor = androidConfigured ? new Color(0.2f, 0.8f, 0.2f) : new Color(1f, 0.5f, 0.2f);
            GUILayout.Label(androidConfigured ? "✓" : "○", androidStyle);
            GUILayout.Label("google-services.json", GUILayout.Width(150));
            
            var androidStatusStyle = new GUIStyle(EditorStyles.miniLabel);
            if (androidConfigured)
                androidStatusStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
            GUILayout.Label(androidConfigured ? "Found in Assets/" : "Missing - download from Firebase Console", androidStatusStyle);
            EditorGUILayout.EndHorizontal();

            // iOS config
            EditorGUILayout.BeginHorizontal();
            var iosStyle = new GUIStyle(EditorStyles.label) { fixedWidth = 20 };
            iosStyle.normal.textColor = iosConfigured ? new Color(0.2f, 0.8f, 0.2f) : new Color(1f, 0.5f, 0.2f);
            GUILayout.Label(iosConfigured ? "✓" : "○", iosStyle);
            GUILayout.Label("GoogleService-Info.plist", GUILayout.Width(150));
            
            var iosStatusStyle = new GUIStyle(EditorStyles.miniLabel);
            if (iosConfigured)
                iosStatusStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
            GUILayout.Label(iosConfigured ? "Found in Assets/" : "Missing - download from Firebase Console", iosStatusStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (!androidConfigured || !iosConfigured)
            {
                if (GUILayout.Button("Open Firebase Console", GUILayout.Height(25)))
                    Application.OpenURL("https://console.firebase.google.com/");
            }

            serializedConfig.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        private void DrawInfoSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("ℹ️ Auto-Initialization", EditorStyles.boldLabel);
            GUILayout.Label(
                "The SDK auto-initializes when your game starts.\n" +
                "• iOS: Shows ATT consent dialog automatically\n" +
                "• Use Sorolla.TrackDesign() to track events",
                new GUIStyle(EditorStyles.label) { wordWrap = true });
            EditorGUILayout.EndVertical();
        }

        private void DrawLinksSection()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            var linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                normal = { textColor = new Color(0.4f, 0.7f, 1f) },
                hover = { textColor = new Color(0.6f, 0.85f, 1f) },
                
            };

            if (GUILayout.Button("📖 Documentation", linkStyle))
                Application.OpenURL("https://github.com/LaCreArthur/sorolla-palette-upm#readme");
            
            GUILayout.Label("|", EditorStyles.linkLabel);
            
            if (GUILayout.Button("🐙 GitHub", linkStyle))
                Application.OpenURL("https://github.com/LaCreArthur/sorolla-palette-upm");
            
            GUILayout.Label("|", EditorStyles.linkLabel);
            
            if (GUILayout.Button("🐛 Report Issue", linkStyle))
                Application.OpenURL("https://github.com/LaCreArthur/sorolla-palette-upm/issues");
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
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
