using System;
using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Editor.UI;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Configuration window for Palette SDK.
    /// </summary>
    public class SorollaWindow : EditorWindow
    {
        const string TokensUssPath = "Packages/com.sorolla.sdk/Editor/UI/tokens.uss";

        // Cached GUIStyles - initialized once
        static GUIStyle s_statusGreenStyle;
        static GUIStyle s_statusYellowStyle;
        static GUIStyle s_statusRedStyle;
        static GUIStyle s_configStyleGreen;
        static GUIStyle s_configStyleYellow;
        static GUIStyle s_configStyleRed;
        static GUIStyle s_configStyleGray;
        static GUIStyle s_modePrototypeStyle;
        static GUIStyle s_modeFullStyle;
        static GUIStyle s_wordWrapStyle;
        static GUIStyle s_linkStyle;
        static GUIStyle s_autoFixStyle;
        static bool s_stylesInitialized;

        // Icon colors (avoid style mutation)
        static readonly Color ColorGreen = new Color(0.2f, 0.8f, 0.2f);
        static readonly Color ColorYellow = new Color(1f, 0.7f, 0.2f);
        static readonly Color ColorRed = new Color(0.9f, 0.4f, 0.4f);
        static readonly Color ColorGray = Color.gray;

        // Version from package.json (cached)
        static string s_version;
        static string Version => s_version ??= UnityEditor.PackageManager.PackageInfo
            .FindForAssembly(typeof(SorollaWindow).Assembly)?.version ?? "?.?.?";

        // Instance state
        readonly List<string> _autoFixLog = new List<string>();
        readonly HashSet<string> _installingPackages = new HashSet<string>();
        SorollaConfig _config;
        SerializedObject _serializedConfig;
        Vector2 _scrollPos;
        List<BuildValidator.ValidationResult> _validationResults = new List<BuildValidator.ValidationResult>();

        void OnEnable()
        {
            LoadOrCreateConfig();
            RunBuildValidation();
            Events.registeringPackages += OnPackagesRegistering;
            Events.registeredPackages += OnPackagesRegistered;
        }

        void OnDisable()
        {
            Events.registeringPackages -= OnPackagesRegistering;
            Events.registeredPackages -= OnPackagesRegistered;
        }

        void OnPackagesRegistering(PackageRegistrationEventArgs args)
        {
            foreach (var package in args.added)
                _installingPackages.Add(package.name);
            Repaint();
        }

        void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            foreach (var package in args.added)
                _installingPackages.Remove(package.name);

            // Re-run validation after packages are installed (no auto-install, just detect)
            EditorApplication.delayCall += () => RunBuildValidation();
            Repaint();
        }

        void CreateGUI()
        {
            // Supervisor-specified layout (2026-07-08): NO outer UI Toolkit ScrollView this cycle.
            // A plain fixed-header + flexGrow IMGUIContainer column avoids double/competing scroll
            // containers; the IMGUIContainer keeps its own IMGUI scroll (restored below, moved from
            // the old OnGUI) exactly as before. A UI Toolkit ScrollView only replaces it in the
            // LAST peel-out cycle, once the IMGUI region disappears entirely.
            rootVisualElement.Add(BuildHeroHeaderSection());
            var imguiContainer = new IMGUIContainer(DrawMainUIWithStyles);
            imguiContainer.style.flexGrow = 1;
            rootVisualElement.Add(imguiContainer);
        }

        /// <summary>Ported to UI Toolkit (p3-header) - real HeroHeader component, scoped to its
        /// own .sorolla-root container so the fixed-dark token theme applies only to the ported
        /// section, not the still-IMGUI content below it (mid-migration, per PLAN.md's per-section
        /// approach - the visual seam is expected and temporary, not a bug).</summary>
        static VisualElement BuildHeroHeaderSection()
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-root");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                container.styleSheets.Add(styleSheet);

            bool isPrototype = SorollaSettings.Mode == SorollaMode.Prototype;
            container.Add(HeroHeader.Create("Palette SDK", $"v{Version} - Plug & Play Publisher Stack",
                isPrototype ? "PROTOTYPE" : "FULL", modeIsFull: !isPrototype));
            return container;
        }

        /// <summary>DrawMainUI() itself is byte-for-byte unchanged from the pre-p3-header IMGUI
        /// version. Its outer scroll (_scrollPos/BeginScrollView, formerly wrapping DrawHeader()
        /// too in the old OnGUI) now wraps only this IMGUIContainer's content, since the header is
        /// a separate fixed VisualElement above it. EnsureStyles() MUST run in here, not in
        /// CreateGUI() - it builds GUIStyle instances (EditorStyles.boldLabel etc.) that need a
        /// live IMGUI/OnGUI context to resolve correctly, the same class of "looks fine but is
        /// silently wrong outside a real GUIClip/OnGUI context" bug as the GUIToScreenPoint-inside-
        /// GeometryChangedEvent regression from p0-capture-exact-origin - caught by design this
        /// time instead of by a second regression.</summary>
        void DrawMainUIWithStyles()
        {
            EnsureStyles();
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            DrawMainUI();
            EditorGUILayout.EndScrollView();
        }

        static void EnsureStyles()
        {
            if (s_stylesInitialized) return;

            s_statusGreenStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColorGreen } };
            s_statusYellowStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColorYellow } };
            s_statusRedStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColorRed } };

            s_configStyleGreen = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.8f, 0.5f) } };
            s_configStyleYellow = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColorYellow } };
            s_configStyleRed = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.5f, 0.5f) } };
            s_configStyleGray = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColorGray } };

            s_modePrototypeStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.3f, 0.7f, 1f) } };
            s_modeFullStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.3f, 1f, 0.3f) } };

            s_wordWrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            s_linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                normal = { textColor = new Color(0.4f, 0.7f, 1f) },
                hover = { textColor = new Color(0.6f, 0.85f, 1f) },
            };
            s_autoFixStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 0.8f, 0.4f) } };

            s_stylesInitialized = true;
        }

        /// <summary>Draw a colored icon without mutating shared styles.</summary>
        static void DrawIcon(string icon, Color color)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(icon, GUILayout.Width(20));
            GUI.contentColor = prev;
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
            if (SorollaSettings.HasRuntimeConfig)
            {
                SorollaSettings.SyncFromRuntimeConfig();
                return;
            }

            if (!SorollaSettings.IsConfigured && !Application.isPlaying)
            {
                // Auto-select Prototype mode on fresh installs for better UX
                SorollaSettings.SetPrototypeMode();
                ShowWindow();
            }
        };

        void DrawMainUI()
        {
            DrawPlayModeWarning();
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

        void DrawPlayModeWarning()
        {
            if (!EditorApplication.isPlaying)
                return;

            EditorGUILayout.HelpBox(
                "⚠️ Exit Play Mode to install or switch SDK modes. Package Manager does not resolve packages during Play Mode.",
                MessageType.Warning);
            EditorGUILayout.Space(5);
        }

        void DrawModeSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Mode", EditorStyles.boldLabel);

            SorollaMode mode = SorollaSettings.Mode;
            bool isPrototype = mode == SorollaMode.Prototype;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Current:", GUILayout.Width(60));

            GUILayout.Label(isPrototype ? "🧪 Prototype" : "🚀 Full", isPrototype ? s_modePrototypeStyle : s_modeFullStyle);

            GUILayout.FlexibleSpace();

            SorollaMode otherMode = isPrototype ? SorollaMode.Full : SorollaMode.Prototype;
            GUI.enabled = !EditorApplication.isPlaying;
            if (GUILayout.Button($"Switch to {otherMode}", GUILayout.Width(130)))
                if (EditorUtility.DisplayDialog($"Switch to {otherMode} Mode?",
                    "This will install/uninstall SDKs as needed.", "Switch", "Cancel"))
                {
                    SorollaSettings.SetMode(otherMode);
                    // Re-run validation after mode switch
                    EditorApplication.delayCall += () => RunBuildValidation();
                }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        void DrawConfigSection()
        {
            if (_config == null || _serializedConfig == null)
            {
                EditorGUILayout.HelpBox("No config found. Click below to create one.", MessageType.Warning);
                if (GUILayout.Button("Create Configuration Asset"))
                {
                    CreateConfig();
                    _serializedConfig = new SerializedObject(_config);
                }
                return;
            }

            _serializedConfig.Update();
            bool isPrototype = SorollaSettings.IsPrototype;
            bool showMax = SdkDetector.IsInstalled(SdkId.AppLovinMAX);
            bool showAdjust = !isPrototype && SdkDetector.IsInstalled(SdkId.Adjust);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("SDK Keys", EditorStyles.boldLabel);

            // MAX Ad Units (Header comes from [Header] attribute on first property)
            if (showMax)
            {
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("rewardedAdUnit"), new GUIContent("Rewarded"));
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("interstitialAdUnit"), new GUIContent("Interstitial"));
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("bannerAdUnit"), new GUIContent("Banner (Optional)"));
            }

            // Adjust (full mode only)
            if (showAdjust)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label("Adjust", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("adjustAppToken"), new GUIContent("App Token"));
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("adjustPurchaseEventToken"), new GUIContent("Purchase Event Token"));
                EditorGUI.indentLevel--;
            }

            // TikTok (optional - shown only when enabled in SDK Overview)
            if (_config.enableTikTok)
            {
                EditorGUILayout.Space(5);
                GUILayout.Label("TikTok", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("tiktokAppId"), new GUIContent("TikTok App ID"));
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("tiktokEmAppId"), new GUIContent("App ID (EM)"));
                EditorGUILayout.PropertyField(_serializedConfig.FindProperty("tiktokAccessToken"), new GUIContent("Access Token"));
                EditorGUI.indentLevel--;
            }

            // Verbose Logging (master toggle)
            EditorGUILayout.Space(5);
            GUILayout.Label("Logging", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_serializedConfig.FindProperty("verboseLogging"),
                new GUIContent("Verbose Logging", "Enable verbose debug output for all vendor SDKs (MAX, Adjust, TikTok). Forced OFF in release builds."));

            if (_serializedConfig.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_config);

                // Auto-sync MAX SDK key to AppLovinSettings when config changes
                if (showMax)
                    MaxSettingsSanitizer.SyncEmbeddedSdkKey();
            }

            EditorGUILayout.EndVertical();
        }

        void DrawInfoSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("ℹ️ Auto-Initialization", EditorStyles.boldLabel);
            GUILayout.Label(
                "The SDK auto-initializes when your game starts.\n" +
                "• iOS: Shows ATT consent dialog automatically\n" +
                "• Use Palette.TrackEvent(name, params) for custom events\n" +
                "• Use Palette.Level.Start/Complete/Fail for level progression\n" +
                "• Use Palette.Economy.Earn/Spend for in-game currency",
                s_wordWrapStyle);
            EditorGUILayout.EndVertical();
        }

        void DrawLinksSection()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Documentation", s_linkStyle))
                Application.OpenURL("https://github.com/sorolla-studio/sorolla-palette#readme");

            GUILayout.Label("|", EditorStyles.linkLabel);

            if (GUILayout.Button("GitHub", s_linkStyle))
                Application.OpenURL("https://github.com/sorolla-studio/sorolla-palette");

            GUILayout.Label("|", EditorStyles.linkLabel);

            if (GUILayout.Button("Report Issue", s_linkStyle))
                Application.OpenURL("https://github.com/sorolla-studio/sorolla-palette/issues");

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void LoadOrCreateConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:SorollaConfig");
            if (guids.Length > 0)
                _config = AssetDatabase.LoadAssetAtPath<SorollaConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            else
                CreateConfig();

            if (_config != null)
                _serializedConfig = new SerializedObject(_config);
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
        }

        /// <summary>
        ///     Data for rendering a single SDK row in the overview section.
        /// </summary>
        struct SdkRowData
        {
            public string Name;
            public string PackageId;
            public bool IsInstalled;
            public bool IsRequired;
            public bool IsAutoInstalled;
            public SdkConfigDetector.ConfigStatus ConfigStatus;
            public string ConfigHint;
            public Action OnConfigure;
            public Action OnInstall;
        }

        #region SDK Overview

        void DrawSdkOverviewSection()
        {
            bool isPrototype = SorollaSettings.IsPrototype;

            // Calculate overall readiness
            SdkConfigDetector.ConfigStatus gaStatus = SdkConfigDetector.GetGameAnalyticsStatus();
            SdkConfigDetector.ConfigStatus fbStatus = SdkConfigDetector.GetFacebookStatus();
            SdkConfigDetector.ConfigStatus adjustStatus = SdkConfigDetector.GetAdjustStatus(_config);
            bool maxInstalled = SdkDetector.IsInstalled(SdkId.AppLovinMAX);
            if (maxInstalled)
                MaxSettingsSanitizer.SyncEmbeddedSdkKey();
            bool maxSettingsSynced = !maxInstalled || MaxSettingsSanitizer.IsSdkKeyConfigured();

            bool isReady = gaStatus == SdkConfigDetector.ConfigStatus.Configured &&
                           (isPrototype
                               ? fbStatus == SdkConfigDetector.ConfigStatus.Configured
                               : maxInstalled &&
                                 maxSettingsSynced &&
                                 adjustStatus == SdkConfigDetector.ConfigStatus.Configured);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("SDK Overview", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            GUILayout.Label(isReady ? "✓ Ready" : "⚠ Setup Required", isReady ? s_statusGreenStyle : s_statusYellowStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // GameAnalytics (always required)
            DrawSdkOverviewItem(
                SdkRegistry.All[SdkId.GameAnalytics],
                gaStatus,
                "Configure game keys",
                SdkConfigDetector.OpenGameAnalyticsSettings,
                true
            );

            // Facebook (always required)
            DrawSdkOverviewItem(
                SdkRegistry.All[SdkId.Facebook],
                fbStatus,
                "Set App ID",
                SdkConfigDetector.OpenFacebookSettings,
                true
            );

            if (isPrototype)
            {
                // MAX (optional in Prototype) - expandable for ad unit IDs
                DrawMaxOverviewItem(maxSettingsSynced, false);
            }
            else
            {
                // Full mode: MAX + Adjust required
                DrawMaxOverviewItem(maxSettingsSynced, true);

                DrawSdkOverviewItem(
                    SdkRegistry.All[SdkId.Adjust],
                    adjustStatus,
                    "Enter app token below",
                    null,
                    true
                );
            }

            // Firebase (required in Full, optional in Prototype)
            DrawFirebaseOverviewItem();

            // TikTok (optional in all modes)
            DrawTikTokOverviewItem();

            EditorGUILayout.EndVertical();
        }

        void DrawSdkOverviewItem(SdkInfo sdk, SdkConfigDetector.ConfigStatus configStatus,
            string configHint, Action openSettings, bool isRequired)
        {
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isAutoInstalled = sdk.IsRequiredFor(isPrototype);

            DrawSdkRow(new SdkRowData
            {
                Name = sdk.Name,
                PackageId = sdk.PackageId,
                IsInstalled = SdkDetector.IsInstalled(sdk),
                IsRequired = isRequired,
                IsAutoInstalled = isAutoInstalled,
                ConfigStatus = configStatus,
                ConfigHint = configHint,
                OnConfigure = openSettings,
                OnInstall = () => SdkInstaller.Install(sdk.Id),
            });
        }

        void DrawSdkRow(SdkRowData data)
        {
            EditorGUILayout.BeginHorizontal();

            bool isInstalling = _installingPackages.Contains(data.PackageId);

            // Status icon
            Color iconColor = data.IsInstalled ? ColorGreen : isInstalling ? ColorYellow : data.IsRequired ? ColorRed : ColorGray;
            string iconText = data.IsInstalled ? "✓" : isInstalling ? "⏳" : data.IsRequired ? "✗" : "○";
            DrawIcon(iconText, iconColor);

            // Name
            string nameLabel = data.IsRequired ? data.Name : $"{data.Name} (optional)";
            GUILayout.Label(nameLabel, GUILayout.Width(140));

            // Settings sync status
            GUIStyle configStyle;
            string configText;
            if (isInstalling)
            {
                configStyle = s_configStyleYellow;
                configText = "Installing...";
            }
            else if (!data.IsInstalled)
            {
                configStyle = s_configStyleGray;
                configText = data.IsAutoInstalled ? "Auto-installs on mode switch" : "—";
            }
            else if (data.ConfigStatus == SdkConfigDetector.ConfigStatus.Configured)
            {
                configStyle = s_configStyleGreen;
                configText = "✓ Configured";
            }
            else
            {
                configStyle = s_configStyleYellow;
                configText = data.ConfigHint;
            }
            GUILayout.Label(configText, configStyle, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            // Action button
            if (!data.IsInstalled && !isInstalling && data.OnInstall != null && !data.IsAutoInstalled)
            {
                GUI.enabled = !EditorApplication.isPlaying;
                if (GUILayout.Button("Install", GUILayout.Width(70)))
                    data.OnInstall();
                GUI.enabled = true;
            }
            else if (data.IsInstalled && data.ConfigStatus == SdkConfigDetector.ConfigStatus.NotConfigured && data.OnConfigure != null)
            {
                if (GUILayout.Button("Configure", GUILayout.Width(70)))
                    data.OnConfigure();
            }
            else if (data.IsInstalled && data.ConfigStatus == SdkConfigDetector.ConfigStatus.Configured && data.OnConfigure != null)
            {
                if (GUILayout.Button("Edit", GUILayout.Width(50)))
                    data.OnConfigure();
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawMaxOverviewItem(bool settingsSynced, bool isRequired)
        {
            SdkInfo sdk = SdkRegistry.All[SdkId.AppLovinMAX];
            bool isInstalled = SdkDetector.IsInstalled(sdk);
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isAutoInstalled = sdk.IsRequiredFor(isPrototype);
            bool isInstalling = _installingPackages.Contains(sdk.PackageId);

            EditorGUILayout.BeginHorizontal();

            // Status icon
            Color iconColor = isInstalled ? ColorGreen : isInstalling ? ColorYellow : isRequired ? ColorRed : ColorGray;
            string iconText = isInstalled ? "✓" : isInstalling ? "⏳" : isRequired ? "✗" : "○";
            DrawIcon(iconText, iconColor);

            // SDK name
            string nameLabel = isRequired ? sdk.Name : $"{sdk.Name} (optional)";
            GUILayout.Label(nameLabel, GUILayout.Width(140));

            // Config status
            GUIStyle configStyle;
            string configText;
            if (isInstalling)
            {
                configStyle = s_configStyleYellow;
                configText = "Installing...";
            }
            else if (!isInstalled)
            {
                configStyle = s_configStyleGray;
                configText = isAutoInstalled ? "Auto-installs on mode switch" : "—";
            }
            else if (settingsSynced)
            {
                configStyle = s_configStyleGreen;
                configText = "✓ Auto-synced";
            }
            else
            {
                configStyle = s_configStyleRed;
                configText = "Auto-sync failed";
            }
            GUILayout.Label(configText, configStyle, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            // Action buttons
            if (!isInstalled && !isInstalling && !isAutoInstalled)
            {
                GUI.enabled = !EditorApplication.isPlaying;
                if (GUILayout.Button("Install", GUILayout.Width(70)))
                    SdkInstaller.Install(sdk.Id);
                GUI.enabled = true;
            }
            else if (isInstalled && !settingsSynced && GUILayout.Button("Refresh", GUILayout.Width(70)))
            {
                RunBuildValidation();
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawFirebaseOverviewItem()
        {
            bool isInstalled = SdkDetector.IsInstalled(SdkId.FirebaseAnalytics);
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isRequired = !isPrototype; // Required in Full, optional in Prototype
            var configStatus = SdkConfigDetector.GetFirebaseStatus(_config);

            // Check if any Firebase package is installing
            bool isInstalling = _installingPackages.Contains("com.google.firebase.app") ||
                                _installingPackages.Contains("com.google.firebase.analytics") ||
                                _installingPackages.Contains("com.google.firebase.crashlytics") ||
                                _installingPackages.Contains("com.google.firebase.remote-config");

            EditorGUILayout.BeginHorizontal();

            // Status icon (mode-aware)
            Color iconColor = isInstalled ? ColorGreen : isInstalling ? ColorYellow : isRequired ? ColorRed : ColorGray;
            string iconText = isInstalled ? "✓" : isInstalling ? "⏳" : isRequired ? "✗" : "○";
            DrawIcon(iconText, iconColor);

            string nameLabel = isRequired ? "Firebase" : "Firebase (optional)";
            GUILayout.Label(nameLabel, GUILayout.Width(140));

            // Config status
            GUIStyle configStyle;
            string configText;
            if (isInstalling)
            {
                configStyle = s_configStyleYellow;
                configText = "Installing...";
            }
            else if (!isInstalled)
            {
                configStyle = s_configStyleGray;
                configText = isRequired ? "Auto-installs on mode switch" : "—";
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
            {
                configStyle = s_configStyleGreen;
                configText = "✓ Configured";
            }
            else
            {
                configStyle = s_configStyleYellow;
                configText = "Add config files";
            }
            GUILayout.Label(configText, configStyle, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            // Install button in Prototype mode (optional, manual install)
            if (!isInstalled && !isInstalling && isPrototype)
            {
                GUI.enabled = !EditorApplication.isPlaying;
                if (GUILayout.Button("Install", GUILayout.Width(70)))
                {
                    SdkInstaller.Install(SdkId.FirebaseApp);
                    SdkInstaller.Install(SdkId.FirebaseAnalytics);
                    SdkInstaller.Install(SdkId.FirebaseCrashlytics);
                    SdkInstaller.Install(SdkId.FirebaseRemoteConfig);
                }
                GUI.enabled = true;
            }
            else if (isInstalled && GUILayout.Button("Console", GUILayout.Width(70)))
            {
                Application.OpenURL("https://console.firebase.google.com/");
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawTikTokOverviewItem()
        {
            bool enabled = _config.enableTikTok;
            bool hasAppId = enabled && _config?.tiktokAppId?.IsConfigured == true
                            && _config?.tiktokEmAppId?.IsConfigured == true;

            EditorGUILayout.BeginHorizontal();

            // Status icon
            Color iconColor = hasAppId ? ColorGreen : ColorGray;
            string iconText = hasAppId ? "✓" : "○";
            DrawIcon(iconText, iconColor);

            // Enable toggle
            bool newEnabled = EditorGUILayout.ToggleLeft("TikTok (optional)", enabled, GUILayout.Width(140));
            if (newEnabled != enabled)
            {
                _config.enableTikTok = newEnabled;
                EditorUtility.SetDirty(_config);
            }

            // Config status
            GUIStyle configStyle;
            string configText;
            if (!enabled)
            {
                configStyle = s_configStyleGray;
                configText = "Disabled";
            }
            else if (hasAppId)
            {
                configStyle = s_configStyleGreen;
                configText = "✓ Configured";
            }
            else
            {
                configStyle = s_configStyleGray;
                configText = "Set App ID below";
            }
            GUILayout.Label(configText, configStyle, GUILayout.Width(150));

            GUILayout.FlexibleSpace();

            if (hasAppId && GUILayout.Button("Dashboard", GUILayout.Width(70)))
            {
                Application.OpenURL("https://business.tiktok.com/");
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Build Health

        /// <summary>
        ///     Run build validation checks and auto-fixes.
        /// </summary>
        void RunBuildValidation()
        {
            _autoFixLog.Clear();

            // Auto-fix: Sync config and install missing required SDKs
            if (BuildValidator.FixConfigSync())
                _autoFixLog.Add("Synced config / installed missing SDKs");

            // Run all sanitizers (single source of truth)
            _autoFixLog.AddRange(BuildValidator.RunAutoFixes());

            // Run validation checks
            _validationResults = BuildValidator.RunAllChecks();
            Repaint();
        }

        void DrawBuildHealthSection()
        {
            int errors = _validationResults.Count(r => r.Status == BuildValidator.ValidationStatus.Error);
            bool isHealthy = errors == 0;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header with status
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Build Health", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (isHealthy)
                GUILayout.Label("Ready to Build", s_statusGreenStyle);
            else
                GUILayout.Label($"{errors} Issue(s)", s_statusRedStyle);

            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                RunBuildValidation();

            EditorGUILayout.EndHorizontal();

            // Auto-fixed items
            if (_autoFixLog.Count > 0)
            {
                EditorGUILayout.Space(5);
                foreach (string fix in _autoFixLog)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("AUTO-FIXED", s_autoFixStyle, GUILayout.Width(70));
                    GUILayout.Label(fix, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(5);

            // Show all checks with their status
            foreach (BuildValidator.CheckCategory category in (BuildValidator.CheckCategory[])Enum.GetValues(typeof(BuildValidator.CheckCategory)))
            {
                string checkName = BuildValidator.CheckNames[category];
                var categoryResults = _validationResults.Where(r => r.Category == category).ToList();

                bool hasError = categoryResults.Any(r => r.Status == BuildValidator.ValidationStatus.Error);
                bool hasWarning = categoryResults.Any(r => r.Status == BuildValidator.ValidationStatus.Warning);
                var validResult = categoryResults.Find(r => r.Status == BuildValidator.ValidationStatus.Valid);

                EditorGUILayout.BeginHorizontal();

                // Status icon
                if (hasError)
                    DrawIcon("✗", ColorRed);
                else if (hasWarning)
                    DrawIcon("⚠", ColorYellow);
                else
                    DrawIcon("✓", ColorGreen);

                GUILayout.Label(checkName, GUILayout.Width(120));

                // Status text
                GUIStyle textStyle;
                string statusText;
                if (hasError)
                {
                    textStyle = s_configStyleRed;
                    statusText = categoryResults.First(r => r.Status == BuildValidator.ValidationStatus.Error).Message.Split('\n')[0];
                }
                else if (hasWarning)
                {
                    textStyle = s_configStyleYellow;
                    statusText = categoryResults.First(r => r.Status == BuildValidator.ValidationStatus.Warning).Message.Split('\n')[0];
                }
                else if (validResult != null)
                {
                    textStyle = s_configStyleGreen;
                    statusText = validResult.Message;
                }
                else
                {
                    textStyle = s_configStyleGray;
                    statusText = "Not checked";
                }

                GUILayout.Label(statusText, textStyle);

                EditorGUILayout.EndHorizontal();

                // Firebase config file sub-rows
                if (category == BuildValidator.CheckCategory.FirebaseCoherence && SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                    DrawFirebaseConfigSubRows();
            }

            EditorGUILayout.EndVertical();
        }

        void DrawFirebaseConfigSubRows()
        {
            bool androidOk = SdkConfigDetector.IsFirebaseAndroidConfigured();
            bool iosOk = SdkConfigDetector.IsFirebaseIOSConfigured();

            // Android config
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24); // Indent under parent row
            DrawIcon(androidOk ? "✓" : "○", androidOk ? ColorGreen : ColorYellow);
            GUILayout.Label("google-services.json", GUILayout.Width(150));
            GUILayout.Label(androidOk ? "Found" : "Missing", androidOk ? s_configStyleGreen : s_configStyleYellow);
            EditorGUILayout.EndHorizontal();

            // iOS config
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24);
            DrawIcon(iosOk ? "✓" : "○", iosOk ? ColorGreen : ColorYellow);
            GUILayout.Label("GoogleService-Info.plist", GUILayout.Width(150));
            GUILayout.Label(iosOk ? "Found" : "Missing", iosOk ? s_configStyleGreen : s_configStyleYellow);
            EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}
