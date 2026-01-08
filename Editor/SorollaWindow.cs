using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Configuration window for Palette SDK.
    /// </summary>
    public class SorollaWindow : EditorWindow
    {
        SorollaConfig _config;
        Vector2 _scrollPos;

        // Build health state
        List<BuildValidator.ValidationResult> _validationResults = new();
        List<string> _autoFixLog = new();
        bool _validationRan;

        void OnEnable()
        {
            LoadOrCreateConfig();
            RunBuildValidation();
        }

        void OnGUI()
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

        [MenuItem("Palette/Configuration")]
        public static void ShowWindow()
        {
            var window = GetWindow<SorollaWindow>("Palette");
            window.minSize = new Vector2(420, 380);
            window.Show();
        }

        [InitializeOnLoadMethod]
        static void AutoOpenOnLoad() => EditorApplication.delayCall += () =>
        {
            if (!SorollaSettings.IsConfigured && !Application.isPlaying)
                ShowWindow();
        };

        void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Palette SDK",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label("v1.0.0 - Plug & Play Publisher Stack",
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.EndVertical();
        }

        void DrawWelcomeScreen()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(10);

            GUILayout.Label("Welcome! Select a mode to get started:",
                new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.Space(15);

            if (GUILayout.Button("🧪  Prototype Mode\n(Facebook SDK for UA)", GUILayout.Height(45)))
                SorollaSettings.SetPrototypeMode();

            EditorGUILayout.Space(8);

            if (GUILayout.Button("🚀  Full Mode\n(Adjust + MAX for Production)", GUILayout.Height(45)))
                SorollaSettings.SetFullMode();

            EditorGUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        void DrawMainUI()
        {
            DrawModeSection();
            EditorGUILayout.Space(10);
            DrawSdkOverviewSection();
            EditorGUILayout.Space(10);
            DrawBuildHealthSection();
            EditorGUILayout.Space(10);
            DrawConfigSection();
            EditorGUILayout.Space(10);
            DrawInfoSection();
            EditorGUILayout.Space(8);
            DrawLinksSection();
        }

        #region SDK Overview

        void DrawSdkOverviewSection()
        {
            bool isPrototype = SorollaSettings.IsPrototype;

            // Calculate overall readiness
            var gaStatus = SdkConfigDetector.GetGameAnalyticsStatus();
            var fbStatus = SdkConfigDetector.GetFacebookStatus();
            var maxStatus = SdkConfigDetector.GetMaxStatus(_config);
            var adjustStatus = SdkConfigDetector.GetAdjustStatus(_config);

            bool isReady = gaStatus == SdkConfigDetector.ConfigStatus.Configured &&
                           (isPrototype
                               ? fbStatus == SdkConfigDetector.ConfigStatus.Configured
                               : maxStatus == SdkConfigDetector.ConfigStatus.Configured &&
                                 adjustStatus == SdkConfigDetector.ConfigStatus.Configured);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("SDK Overview", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            var statusStyle = new GUIStyle(EditorStyles.boldLabel);
            statusStyle.normal.textColor = isReady ? new Color(0.2f, 0.8f, 0.2f) : new Color(1f, 0.7f, 0.2f);
            GUILayout.Label(isReady ? "✓ Ready" : "⚠ Setup Required", statusStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // GameAnalytics (always required)
            DrawSdkOverviewItem(
                SdkRegistry.All[SdkId.GameAnalytics],
                gaStatus,
                "Configure game keys",
                SdkConfigDetector.OpenGameAnalyticsSettings,
                isRequired: true
            );

            if (isPrototype)
            {
                // Facebook (required in Prototype)
                DrawSdkOverviewItem(
                    SdkRegistry.All[SdkId.Facebook],
                    fbStatus,
                    "Set App ID",
                    SdkConfigDetector.OpenFacebookSettings,
                    isRequired: true
                );

                // MAX (optional in Prototype)
                DrawSdkOverviewItem(
                    SdkRegistry.All[SdkId.AppLovinMAX],
                    maxStatus,
                    "Enter SDK key below",
                    SdkConfigDetector.OpenMaxSettings,
                    isRequired: false
                );
            }
            else
            {
                // Full mode: MAX + Adjust required
                DrawSdkOverviewItem(
                    SdkRegistry.All[SdkId.AppLovinMAX],
                    maxStatus,
                    "Enter SDK key below",
                    SdkConfigDetector.OpenMaxSettings,
                    isRequired: true
                );

                DrawSdkOverviewItem(
                    SdkRegistry.All[SdkId.Adjust],
                    adjustStatus,
                    "Enter app token below",
                    null,
                    isRequired: true
                );
            }

            // Firebase (always optional)
            DrawFirebaseOverviewItem();

            EditorGUILayout.EndVertical();
        }

        void DrawSdkOverviewItem(SdkInfo sdk, SdkConfigDetector.ConfigStatus configStatus,
            string configHint, Action openSettings, bool isRequired)
        {
            bool isInstalled = SdkDetector.IsInstalled(sdk);

            EditorGUILayout.BeginHorizontal();

            // Install status icon
            var iconStyle = new GUIStyle(EditorStyles.label) { fixedWidth = 20 };
            if (isInstalled)
            {
                iconStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                GUILayout.Label("✓", iconStyle);
            }
            else
            {
                iconStyle.normal.textColor = isRequired ? new Color(1f, 0.4f, 0.4f) : Color.gray;
                GUILayout.Label(isRequired ? "✗" : "○", iconStyle);
            }

            // SDK name
            var nameLabel = isRequired ? sdk.Name : $"{sdk.Name} (optional)";
            GUILayout.Label(nameLabel, GUILayout.Width(140));

            // Config status
            var configStyle = new GUIStyle(EditorStyles.miniLabel);
            string configText;

            if (!isInstalled)
            {
                configStyle.normal.textColor = Color.gray;
                configText = "—";
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
            {
                configStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
                configText = "✓ Configured";
            }
            else
            {
                configStyle.normal.textColor = new Color(1f, 0.7f, 0.2f);
                configText = configHint;
            }

            GUILayout.Label(configText, configStyle, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            // Action button
            if (!isInstalled)
            {
                if (GUILayout.Button("Install", GUILayout.Width(70)))
                    SdkInstaller.Install(sdk.Id);
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.NotConfigured && openSettings != null)
            {
                if (GUILayout.Button("Configure", GUILayout.Width(70)))
                    openSettings();
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.Configured && openSettings != null)
            {
                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                    openSettings();
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawFirebaseOverviewItem()
        {
            bool isInstalled = SdkDetector.IsInstalled(SdkId.FirebaseAnalytics);
            var configStatus = SdkConfigDetector.GetFirebaseStatus(_config);

            EditorGUILayout.BeginHorizontal();

            // Install status icon
            var iconStyle = new GUIStyle(EditorStyles.label) { fixedWidth = 20 };
            iconStyle.normal.textColor = isInstalled ? new Color(0.2f, 0.8f, 0.2f) : Color.gray;
            GUILayout.Label(isInstalled ? "✓" : "○", iconStyle);

            // Name
            GUILayout.Label("Firebase (optional)", GUILayout.Width(140));

            // Config status
            var configStyle = new GUIStyle(EditorStyles.miniLabel);
            string configText;

            if (!isInstalled)
            {
                configStyle.normal.textColor = Color.gray;
                configText = "—";
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
            {
                configStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
                configText = "✓ Configured";
            }
            else
            {
                configStyle.normal.textColor = new Color(1f, 0.7f, 0.2f);
                configText = "Add config files";
            }

            GUILayout.Label(configText, configStyle, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            // Action button
            if (!isInstalled)
            {
                if (GUILayout.Button("Install", GUILayout.Width(70)))
                    SdkInstaller.InstallFirebase();
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.NotConfigured)
            {
                if (GUILayout.Button("Console", GUILayout.Width(70)))
                    OpenFirebaseConsole();
            }
            else
            {
                if (GUILayout.Button("Uninstall", GUILayout.Width(70)))
                    SdkInstaller.UninstallFirebase();
            }

            EditorGUILayout.EndHorizontal();

            // Show Firebase modules when installed
            if (isInstalled)
            {
                var moduleStyle = new GUIStyle(EditorStyles.miniLabel);
                moduleStyle.normal.textColor = new Color(0.6f, 0.8f, 0.6f);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.Label("├ Analytics", moduleStyle, GUILayout.Width(100));
                GUILayout.Label(SdkDetector.IsInstalled(SdkId.FirebaseAnalytics) ? "✓" : "○",
                    moduleStyle, GUILayout.Width(20));
                GUILayout.Label("├ Crashlytics", moduleStyle, GUILayout.Width(100));
                GUILayout.Label(SdkDetector.IsInstalled(SdkId.FirebaseCrashlytics) ? "✓" : "○",
                    moduleStyle, GUILayout.Width(20));
                GUILayout.Label("└ Remote Config", moduleStyle, GUILayout.Width(100));
                GUILayout.Label(SdkDetector.IsInstalled(SdkId.FirebaseRemoteConfig) ? "✓" : "○",
                    moduleStyle, GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();
            }
        }

        static void OpenFirebaseConsole() => Application.OpenURL("https://console.firebase.google.com/");

        #endregion

        void DrawModeSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Mode", EditorStyles.boldLabel);

            SorollaMode mode = SorollaSettings.Mode;
            bool isPrototype = mode == SorollaMode.Prototype;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Current:", GUILayout.Width(60));

            var modeStyle = new GUIStyle(EditorStyles.boldLabel);
            modeStyle.normal.textColor = isPrototype ? new Color(0.3f, 0.7f, 1f) : new Color(0.3f, 1f, 0.3f);
            GUILayout.Label(isPrototype ? "🧪 Prototype" : "🚀 Full", modeStyle);

            GUILayout.FlexibleSpace();

            SorollaMode otherMode = isPrototype ? SorollaMode.Full : SorollaMode.Prototype;
            if (GUILayout.Button($"Switch to {otherMode}", GUILayout.Width(130)))
                if (EditorUtility.DisplayDialog($"Switch to {otherMode} Mode?",
                    "This will install/uninstall SDKs as needed.", "Switch", "Cancel"))
                {
                    SorollaSettings.SetMode(otherMode);
                    // Re-run validation after mode switch
                    EditorApplication.delayCall += RunBuildValidation;
                }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        void DrawConfigSection()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("No config found. Click below to create one.", MessageType.Warning);
                if (GUILayout.Button("Create Configuration Asset"))
                    CreateConfig();
                return;
            }

            var serializedConfig = new SerializedObject(_config);
            bool isPrototype = SorollaSettings.IsPrototype;

            // Only show SDK Keys section if MAX or Adjust needs keys
            bool showMaxConfig = SdkDetector.IsInstalled(SdkId.AppLovinMAX);
            bool showAdjustConfig = !isPrototype && SdkDetector.IsInstalled(SdkId.Adjust);

            if (showMaxConfig || showAdjustConfig)
            {
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
                    EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxRewardedAdUnitId"),
                        new GUIContent("Rewarded Ad Unit"));
                    EditorGUILayout.PropertyField(serializedConfig.FindProperty("maxInterstitialAdUnitId"),
                        new GUIContent("Interstitial Ad Unit"));
                    EditorGUI.indentLevel--;
                }

                // Adjust config (full mode only)
                if (showAdjustConfig)
                {
                    EditorGUILayout.Space(5);
                    GUILayout.Label("Adjust", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(serializedConfig.FindProperty("adjustAppToken"),
                        new GUIContent("App Token"));
                    EditorGUI.indentLevel--;
                }

                serializedConfig.ApplyModifiedProperties();

                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save"))
                {
                    EditorUtility.SetDirty(_config);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Palette] Configuration saved.");
                }

                if (GUILayout.Button("Select Asset"))
                {
                    Selection.activeObject = _config;
                    EditorGUIUtility.PingObject(_config);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }

            // Firebase section (always check, shown when installed)
            DrawFirebaseConfigSection(serializedConfig);
        }

        void DrawFirebaseConfigSection(SerializedObject serializedConfig)
        {
            if (!SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                return;

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Firebase", EditorStyles.boldLabel);

            EditorGUILayout.Space(5);

            // Module toggles with helper text
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Modules (auto-enabled on install):", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            // Quick enable/disable all
            if (GUILayout.Button("All On", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                serializedConfig.FindProperty("enableFirebaseAnalytics").boolValue = true;
                serializedConfig.FindProperty("enableCrashlytics").boolValue = true;
                serializedConfig.FindProperty("enableRemoteConfig").boolValue = true;
            }
            if (GUILayout.Button("All Off", EditorStyles.miniButton, GUILayout.Width(50)))
            {
                serializedConfig.FindProperty("enableFirebaseAnalytics").boolValue = false;
                serializedConfig.FindProperty("enableCrashlytics").boolValue = false;
                serializedConfig.FindProperty("enableRemoteConfig").boolValue = false;
            }
            EditorGUILayout.EndHorizontal();

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
            bool androidConfigured = SdkConfigDetector.IsFirebaseAndroidConfigured();
            bool iosConfigured = SdkConfigDetector.IsFirebaseIOSConfigured();

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
            GUILayout.Label(androidConfigured ? "Found in Assets/" : "Missing - download from Firebase Console",
                androidStatusStyle);
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
            GUILayout.Label(iosConfigured ? "Found in Assets/" : "Missing - download from Firebase Console",
                iosStatusStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (!androidConfigured || !iosConfigured)
                if (GUILayout.Button("Open Firebase Console", GUILayout.Height(25)))
                    Application.OpenURL("https://console.firebase.google.com/");

            serializedConfig.ApplyModifiedProperties();
            EditorGUILayout.EndVertical();
        }

        void DrawInfoSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("ℹ️ Auto-Initialization", EditorStyles.boldLabel);
            GUILayout.Label(
                "The SDK auto-initializes when your game starts.\n" +
                "• iOS: Shows ATT consent dialog automatically\n" +
                "• Use Palette.TrackDesign() to track events",
                new GUIStyle(EditorStyles.label) { wordWrap = true });
            EditorGUILayout.EndVertical();
        }

        void DrawLinksSection()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                normal = { textColor = new Color(0.4f, 0.7f, 1f) },
                hover = { textColor = new Color(0.6f, 0.85f, 1f) },
            };

            if (GUILayout.Button("Documentation", linkStyle))
                Application.OpenURL("https://github.com/sorolla-studio/sorolla-palette#readme");

            GUILayout.Label("|", EditorStyles.linkLabel);

            if (GUILayout.Button("GitHub", linkStyle))
                Application.OpenURL("https://github.com/sorolla-studio/sorolla-palette");

            GUILayout.Label("|", EditorStyles.linkLabel);

            if (GUILayout.Button("Report Issue", linkStyle))
                Application.OpenURL("https://github.com/sorolla-studio/sorolla-palette/issues");

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        #region Build Health

        void RunBuildValidation()
        {
            _autoFixLog.Clear();
            _validationRan = true;

            // Auto-fix: Sync config with installed SDKs before validation
            if (BuildValidator.FixConfigSync())
                _autoFixLog.Add("Synced SorollaConfig with installed SDKs");

            // Run all sanitizers (single source of truth)
            _autoFixLog.AddRange(BuildValidator.RunAutoFixes());

            // Run validation checks
            _validationResults = BuildValidator.RunAllChecks();
            Repaint();
        }

        void DrawBuildHealthSection()
        {
            var errors = _validationResults.Count(r => r.Status == BuildValidator.ValidationStatus.Error);
            var warnings = _validationResults.Count(r => r.Status == BuildValidator.ValidationStatus.Warning);
            var isHealthy = errors == 0;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header with status
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Build Health", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            var statusStyle = new GUIStyle(EditorStyles.boldLabel);
            if (isHealthy)
            {
                statusStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                GUILayout.Label("Ready to Build", statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = new Color(0.9f, 0.4f, 0.4f);
                GUILayout.Label($"{errors} Issue(s)", statusStyle);
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                RunBuildValidation();

            EditorGUILayout.EndHorizontal();

            // Auto-fixed items
            if (_autoFixLog.Count > 0)
            {
                EditorGUILayout.Space(5);
                foreach (var fix in _autoFixLog)
                {
                    EditorGUILayout.BeginHorizontal();
                    var fixStyle = new GUIStyle(EditorStyles.miniLabel);
                    fixStyle.normal.textColor = new Color(0.4f, 0.8f, 0.4f);
                    GUILayout.Label("AUTO-FIXED", fixStyle, GUILayout.Width(70));
                    GUILayout.Label(fix, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(5);

            // Show all 7 checks with their status
            foreach (BuildValidator.CheckCategory category in Enum.GetValues(typeof(BuildValidator.CheckCategory)))
            {
                var checkName = BuildValidator.CheckNames[category];
                var categoryResults = _validationResults.Where(r => r.Category == category).ToList();

                // Determine status for this category
                var hasError = categoryResults.Any(r => r.Status == BuildValidator.ValidationStatus.Error);
                var hasWarning = categoryResults.Any(r => r.Status == BuildValidator.ValidationStatus.Warning);
                var validResult = categoryResults.FirstOrDefault(r => r.Status == BuildValidator.ValidationStatus.Valid);

                EditorGUILayout.BeginHorizontal();

                // Status icon
                var iconStyle = new GUIStyle(EditorStyles.label) { fixedWidth = 20 };
                if (hasError)
                {
                    iconStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
                    GUILayout.Label("✗", iconStyle);
                }
                else if (hasWarning)
                {
                    iconStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
                    GUILayout.Label("⚠", iconStyle);
                }
                else
                {
                    iconStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                    GUILayout.Label("✓", iconStyle);
                }

                // Check name
                GUILayout.Label(checkName, GUILayout.Width(120));

                // Status text
                var textStyle = new GUIStyle(EditorStyles.miniLabel);
                string statusText;
                if (hasError)
                {
                    textStyle.normal.textColor = new Color(1f, 0.5f, 0.5f);
                    var firstError = categoryResults.First(r => r.Status == BuildValidator.ValidationStatus.Error);
                    statusText = firstError.Message.Split('\n')[0];
                }
                else if (hasWarning)
                {
                    textStyle.normal.textColor = new Color(1f, 0.85f, 0.5f);
                    var firstWarning = categoryResults.First(r => r.Status == BuildValidator.ValidationStatus.Warning);
                    statusText = firstWarning.Message.Split('\n')[0];
                }
                else if (validResult != null)
                {
                    textStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
                    statusText = validResult.Message;
                }
                else
                {
                    textStyle.normal.textColor = Color.gray;
                    statusText = "Not checked";
                }

                GUILayout.Label(statusText, textStyle);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        void LoadOrCreateConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:SorollaConfig");
            if (guids.Length > 0)
                _config = AssetDatabase.LoadAssetAtPath<SorollaConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            else
                CreateConfig();
        }

        void CreateConfig()
        {
            _config = CreateInstance<SorollaConfig>();

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/SorollaConfig.asset");
            AssetDatabase.CreateAsset(_config, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Palette] Config created at: {path}");
            Selection.activeObject = _config;
        }
    }
}
