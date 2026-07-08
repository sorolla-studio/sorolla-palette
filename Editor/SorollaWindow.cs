using System;
using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Editor.UI;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.UIElements;
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
        static GUIStyle s_modePrototypeStyle;
        static GUIStyle s_modeFullStyle;
        static GUIStyle s_wordWrapStyle;
        static GUIStyle s_linkStyle;
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
        VisualElement _buildHealthContainer;
        VisualElement _sdkOverviewContainer;
        VisualElement _configContainer;
        List<BuildValidator.ValidationResult> _validationResults = new List<BuildValidator.ValidationResult>();

        void OnEnable()
        {
            LoadOrCreateConfig();
            RunBuildValidation();
            Events.registeringPackages += OnPackagesRegistering;
            Events.registeredPackages += OnPackagesRegistered;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable()
        {
            Events.registeringPackages -= OnPackagesRegistering;
            Events.registeredPackages -= OnPackagesRegistered;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        void OnPackagesRegistering(PackageRegistrationEventArgs args)
        {
            foreach (var package in args.added)
                _installingPackages.Add(package.name);
            Repaint();
            RefreshSdkOverviewUI();
        }

        void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            foreach (var package in args.added)
                _installingPackages.Remove(package.name);

            // Re-run validation after packages are installed (no auto-install, just detect)
            EditorApplication.delayCall += () => RunBuildValidation();
            Repaint();
            RefreshSdkOverviewUI();
        }

        /// <summary>The ported rows' Install-button enabled state mirrors the old GUI.enabled =
        /// !EditorApplication.isPlaying gate, which used to re-evaluate every IMGUI frame for free.
        /// A cleared/rebuilt VisualElement doesn't repaint itself, so play-mode entry/exit is an
        /// explicit rebuild trigger here (same call-site-addition pattern as the package events).</summary>
        void OnPlayModeStateChanged(PlayModeStateChange change) => RefreshSdkOverviewUI();

        void CreateGUI()
        {
            // Revised layout (p3-buildhealth, supervisor-approved): Build Health sits in the
            // MIDDLE of the old DrawMainUI() call order, so porting it splits the IMGUI region
            // into two disjoint pieces with a real ported VisualElement between them - an
            // IMGUI-internal scroll can no longer span that gap. The outer UI Toolkit ScrollView
            // therefore moves up to this cycle (originally planned for the last peel-out) and owns
            // ALL scrolling; neither IMGUIContainer keeps its own internal BeginScrollView or
            // flexGrow - each sizes to its natural GUILayout content height.
            rootVisualElement.Add(BuildHeroHeaderSection());

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            rootVisualElement.Add(scrollView);

            scrollView.Add(new IMGUIContainer(DrawUpperSectionsWithStyles));

            // p3-sdkoverview: DrawSdkOverviewSection() was the LAST call in DrawUpperSections, so
            // porting it is a clean peel from the end of the already-shrunk IMGUIContainerA region,
            // not a new middle-split - it just slots in right before Build Health.
            _sdkOverviewContainer = CreatePortedSectionContainer();
            _sdkOverviewContainer.style.marginTop = 10;
            scrollView.Add(_sdkOverviewContainer);
            RefreshSdkOverviewUI();

            _buildHealthContainer = CreatePortedSectionContainer();
            _buildHealthContainer.style.marginTop = 10;
            _buildHealthContainer.style.marginBottom = 10;
            scrollView.Add(_buildHealthContainer);
            RefreshBuildHealthUI(); // initial paint from whatever _validationResults already holds

            // p3-config: DrawConfigSection() was the FIRST call in DrawLowerSections, so porting it
            // is a clean peel from the front of the already-shrunk IMGUIContainerB region.
            _configContainer = CreatePortedSectionContainer();
            _configContainer.style.marginBottom = 10;
            scrollView.Add(_configContainer);
            RefreshConfigUI();

            scrollView.Add(new IMGUIContainer(DrawLowerSectionsWithStyles));
        }

        /// <summary>Shared boilerplate for every ported section container: fixed-dark theme scope
        /// + the tokens stylesheet. Factored once three call sites needed the same 6 lines
        /// (p3-config).</summary>
        static VisualElement CreatePortedSectionContainer()
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-root");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                container.styleSheets.Add(styleSheet);
            return container;
        }

        /// <summary>Ported to UI Toolkit (p3-header) - real HeroHeader component, scoped to its
        /// own .sorolla-root container so the fixed-dark token theme applies only to the ported
        /// section, not the still-IMGUI content below it (mid-migration, per PLAN.md's per-section
        /// approach - the visual seam is expected and temporary, not a bug).</summary>
        static VisualElement BuildHeroHeaderSection()
        {
            var container = CreatePortedSectionContainer();

            bool isPrototype = SorollaSettings.Mode == SorollaMode.Prototype;
            container.Add(HeroHeader.Create("Palette SDK", $"v{Version} - Plug & Play Publisher Stack",
                isPrototype ? "PROTOTYPE" : "FULL", modeIsFull: !isPrototype));
            return container;
        }

        /// <summary>DrawUpperSections()/DrawLowerSections() are byte-for-byte unchanged draw calls
        /// (only their grouping into two methods changed, see the split above). Neither IMGUI
        /// piece keeps its own scroll anymore - the outer UI Toolkit ScrollView in CreateGUI() owns
        /// all scrolling now, spanning both IMGUI pieces and the ported Build Health section
        /// between them. EnsureStyles() MUST run in EACH handler, not once in CreateGUI() - every
        /// IMGUIContainer is its own separate GUI context, and GUIStyle construction needs a live
        /// one to resolve correctly (same bug class as the GUIToScreenPoint-inside-
        /// GeometryChangedEvent regression from p0-capture-exact-origin - caught by design this
        /// time). EnsureStyles() is itself idempotent (s_stylesInitialized guard), so calling it
        /// from both handlers every frame is cheap and correct.</summary>
        void DrawUpperSectionsWithStyles()
        {
            EnsureStyles();
            DrawUpperSections();
        }

        void DrawLowerSectionsWithStyles()
        {
            EnsureStyles();
            DrawLowerSections();
        }

        static void EnsureStyles()
        {
            if (s_stylesInitialized) return;

            s_modePrototypeStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.3f, 0.7f, 1f) } };
            s_modeFullStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.3f, 1f, 0.3f) } };

            s_wordWrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            s_linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                normal = { textColor = new Color(0.4f, 0.7f, 1f) },
                hover = { textColor = new Color(0.6f, 0.85f, 1f) },
            };
            s_stylesInitialized = true;
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

        /// <summary>Draw-only split of the old DrawMainUI() (p3-buildhealth) - Build Health now
        /// sits between these two as a real ported VisualElement, so the single IMGUI region that
        /// used to be one contiguous blob is now two separate IMGUIContainers in the outer UI
        /// Toolkit ScrollView. No logic moved, only which method calls which draw call.</summary>
        void DrawUpperSections()
        {
            DrawPlayModeWarning();
            DrawModeSection();
        }

        void DrawLowerSections()
        {
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

        /// <summary>Ported to UI Toolkit (p3-sdkconfig): real UnityEditor.UIElements.PropertyField
        /// bound to _serializedConfig, not a phase-2 component - PropertyField is Unity's own
        /// SerializedObject-binding mechanism (undo, dirty-marking, multi-edit, [Header] decorator
        /// rendering all come for free) and is the behavior-preserving equivalent of the old
        /// EditorGUILayout.PropertyField calls, not a re-implementation. Indent levels become an
        /// explicit marginLeft (same approximation Build Health already used for its indented
        /// Firebase sub-rows). Rebuild-on-change same pattern as buildhealth/sdkoverview: called
        /// from CreateGUI, RunBuildValidation()'s completion (mode/install changes affect
        /// showMax/showAdjust), and the TikTok toggle callback (its own field visibility).</summary>
        void RefreshConfigUI()
        {
            if (_configContainer == null) return;

            _configContainer.Clear();

            if (_config == null || _serializedConfig == null)
            {
                _configContainer.Add(new HelpBox("No config found. Click below to create one.", HelpBoxMessageType.Warning));
                _configContainer.Add(new Button(() =>
                {
                    CreateConfig();
                    _serializedConfig = new SerializedObject(_config);
                    RefreshConfigUI();
                })
                { text = "Create Configuration Asset" });
                return;
            }

            bool isPrototype = SorollaSettings.IsPrototype;
            bool showMax = SdkDetector.IsInstalled(SdkId.AppLovinMAX);
            bool showAdjust = !isPrototype && SdkDetector.IsInstalled(SdkId.Adjust);

            _configContainer.Add(SectionHeader.Create("SDK Keys"));

            // MAX Ad Units (Header comes from [Header] attribute on first property - PropertyField
            // renders it automatically, same as the old EditorGUILayout.PropertyField did)
            if (showMax)
            {
                _configContainer.Add(new PropertyField(_serializedConfig.FindProperty("rewardedAdUnit"), "Rewarded"));
                _configContainer.Add(new PropertyField(_serializedConfig.FindProperty("interstitialAdUnit"), "Interstitial"));
                _configContainer.Add(new PropertyField(_serializedConfig.FindProperty("bannerAdUnit"), "Banner (Optional)"));
            }

            // Adjust (full mode only) - "Adjust (Full Mode Only)" header comes from the
            // [Header] attribute on adjustAppToken itself, same auto-render as MAX Ad Units above;
            // a manual label here would duplicate it (caught via screenshot, not assumed).
            if (showAdjust)
            {
                var appToken = new PropertyField(_serializedConfig.FindProperty("adjustAppToken"), "App Token");
                appToken.style.marginLeft = 15;
                _configContainer.Add(appToken);
                var purchaseToken = new PropertyField(_serializedConfig.FindProperty("adjustPurchaseEventToken"), "Purchase Event Token");
                purchaseToken.style.marginLeft = 15;
                _configContainer.Add(purchaseToken);
            }

            // TikTok (optional - shown only when enabled in SDK Overview) - "TikTok (Optional)"
            // header comes from tiktokAppId's own [Header] attribute, same as Adjust above.
            if (_config.enableTikTok)
            {
                var appId = new PropertyField(_serializedConfig.FindProperty("tiktokAppId"), "TikTok App ID");
                appId.style.marginLeft = 15;
                _configContainer.Add(appId);
                var emAppId = new PropertyField(_serializedConfig.FindProperty("tiktokEmAppId"), "App ID (EM)");
                emAppId.style.marginLeft = 15;
                _configContainer.Add(emAppId);
                var accessToken = new PropertyField(_serializedConfig.FindProperty("tiktokAccessToken"), "Access Token");
                accessToken.style.marginLeft = 15;
                _configContainer.Add(accessToken);
            }

            // Verbose Logging (master toggle) - "Logging" header comes from verboseLogging's own
            // [Header] attribute, same as Adjust/TikTok above.
            var verboseLogging = new PropertyField(_serializedConfig.FindProperty("verboseLogging"), "Verbose Logging");
            verboseLogging.tooltip = "Enable verbose debug output for all vendor SDKs (MAX, Adjust, TikTok). Forced OFF in release builds.";
            _configContainer.Add(verboseLogging);

            _configContainer.Bind(_serializedConfig);

            // Auto-sync MAX SDK key to AppLovinSettings whenever any field on this config changes,
            // matching the old "if (_serializedConfig.ApplyModifiedProperties()) { ... if (showMax)
            // sync }" behavior - TrackSerializedObjectValue fires once per change, same semantics.
            if (showMax)
                _configContainer.TrackSerializedObjectValue(_serializedConfig, _ => MaxSettingsSanitizer.SyncEmbeddedSdkKey());
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

        #region SDK Overview

        /// <summary>Row data for the generic vendor-row builder below. NameElement is a full
        /// VisualElement (not just a string) because TikTok's row replaces the plain name Label
        /// with a live Toggle - every other vendor passes a plain Label built from its name.</summary>
        sealed class SdkOverviewRowData
        {
            public VisualElement NameElement;
            public string IconGlyph;
            public Color IconColor;
            public string ConfigText;
            public Color ConfigColor;
            public string ActionLabel;
            public Action OnAction;
            public bool ActionEnabled = true;
        }

        /// <summary>Ported to UI Toolkit (p3-sdkoverview). One shared row builder for all six
        /// vendors (GameAnalytics/Facebook/Adjust share this directly; MAX/Firebase/TikTok wrap it
        /// with their own status logic below) rather than six copy-pasted row blocks - this row
        /// shape has no second consumer outside this section, so it stays inline instead of
        /// becoming a new Components/ entry (supervisor-approved, p3-sdkoverview scoping).</summary>
        static VisualElement BuildVendorRow(SdkOverviewRowData data)
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-sdk-row");

            var icon = new Label(data.IconGlyph);
            icon.AddToClassList("sorolla-sdk-row-icon");
            icon.style.color = data.IconColor;
            row.Add(icon);

            row.Add(data.NameElement);

            var configLabel = new Label(data.ConfigText);
            configLabel.AddToClassList("sorolla-sdk-row-config");
            configLabel.style.color = data.ConfigColor;
            row.Add(configLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            row.Add(spacer);

            if (!string.IsNullOrEmpty(data.ActionLabel))
            {
                var button = new Button(() => data.OnAction?.Invoke()) { text = data.ActionLabel };
                button.AddToClassList("sorolla-callout-button");
                button.SetEnabled(data.ActionEnabled);
                row.Add(button);
            }

            return row;
        }

        VisualElement BuildSdkOverviewRow(SdkInfo sdk, SdkConfigDetector.ConfigStatus configStatus,
            string configHint, Action openSettings, bool isRequired)
        {
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isAutoInstalled = sdk.IsRequiredFor(isPrototype);
            bool isInstalled = SdkDetector.IsInstalled(sdk);
            bool isInstalling = _installingPackages.Contains(sdk.PackageId);

            Color iconColor = isInstalled ? ColorGreen : isInstalling ? ColorYellow : isRequired ? ColorRed : ColorGray;
            string iconGlyph = isInstalled ? "✓" : isInstalling ? "⏳" : isRequired ? "✗" : "○";

            Color configColor;
            string configText;
            if (isInstalling)
            {
                configColor = ColorYellow;
                configText = "Installing...";
            }
            else if (!isInstalled)
            {
                configColor = ColorGray;
                configText = isAutoInstalled ? "Auto-installs on mode switch" : "—";
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
            {
                configColor = ColorGreen;
                configText = "✓ Configured";
            }
            else
            {
                configColor = ColorYellow;
                configText = configHint;
            }

            string actionLabel = null;
            Action onAction = null;
            bool actionEnabled = true;
            if (!isInstalled && !isInstalling && !isAutoInstalled)
            {
                actionLabel = "Install";
                onAction = () => SdkInstaller.Install(sdk.Id);
                actionEnabled = !EditorApplication.isPlaying;
            }
            else if (isInstalled && configStatus == SdkConfigDetector.ConfigStatus.NotConfigured && openSettings != null)
            {
                actionLabel = "Configure";
                onAction = openSettings;
            }
            else if (isInstalled && configStatus == SdkConfigDetector.ConfigStatus.Configured && openSettings != null)
            {
                actionLabel = "Edit";
                onAction = openSettings;
            }

            var nameLabel = new Label(isRequired ? sdk.Name : $"{sdk.Name} (optional)");
            nameLabel.AddToClassList("sorolla-sdk-row-name");

            return BuildVendorRow(new SdkOverviewRowData
            {
                NameElement = nameLabel,
                IconGlyph = iconGlyph,
                IconColor = iconColor,
                ConfigText = configText,
                ConfigColor = configColor,
                ActionLabel = actionLabel,
                OnAction = onAction,
                ActionEnabled = actionEnabled,
            });
        }

        VisualElement BuildMaxOverviewRow(bool settingsSynced, bool isRequired)
        {
            SdkInfo sdk = SdkRegistry.All[SdkId.AppLovinMAX];
            bool isInstalled = SdkDetector.IsInstalled(sdk);
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isAutoInstalled = sdk.IsRequiredFor(isPrototype);
            bool isInstalling = _installingPackages.Contains(sdk.PackageId);

            Color iconColor = isInstalled ? ColorGreen : isInstalling ? ColorYellow : isRequired ? ColorRed : ColorGray;
            string iconGlyph = isInstalled ? "✓" : isInstalling ? "⏳" : isRequired ? "✗" : "○";

            Color configColor;
            string configText;
            if (isInstalling)
            {
                configColor = ColorYellow;
                configText = "Installing...";
            }
            else if (!isInstalled)
            {
                configColor = ColorGray;
                configText = isAutoInstalled ? "Auto-installs on mode switch" : "—";
            }
            else if (settingsSynced)
            {
                configColor = ColorGreen;
                configText = "✓ Auto-synced";
            }
            else
            {
                configColor = ColorRed;
                configText = "Auto-sync failed";
            }

            string actionLabel = null;
            Action onAction = null;
            bool actionEnabled = true;
            if (!isInstalled && !isInstalling && !isAutoInstalled)
            {
                actionLabel = "Install";
                onAction = () => SdkInstaller.Install(sdk.Id);
                actionEnabled = !EditorApplication.isPlaying;
            }
            else if (isInstalled && !settingsSynced)
            {
                actionLabel = "Refresh";
                onAction = RunBuildValidation;
            }

            var nameLabel = new Label(isRequired ? sdk.Name : $"{sdk.Name} (optional)");
            nameLabel.AddToClassList("sorolla-sdk-row-name");

            return BuildVendorRow(new SdkOverviewRowData
            {
                NameElement = nameLabel,
                IconGlyph = iconGlyph,
                IconColor = iconColor,
                ConfigText = configText,
                ConfigColor = configColor,
                ActionLabel = actionLabel,
                OnAction = onAction,
                ActionEnabled = actionEnabled,
            });
        }

        VisualElement BuildFirebaseOverviewRow()
        {
            bool isInstalled = SdkDetector.IsInstalled(SdkId.FirebaseAnalytics);
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isRequired = !isPrototype; // Required in Full, optional in Prototype
            var configStatus = SdkConfigDetector.GetFirebaseStatus(_config);

            bool isInstalling = _installingPackages.Contains("com.google.firebase.app") ||
                                _installingPackages.Contains("com.google.firebase.analytics") ||
                                _installingPackages.Contains("com.google.firebase.crashlytics") ||
                                _installingPackages.Contains("com.google.firebase.remote-config");

            Color iconColor = isInstalled ? ColorGreen : isInstalling ? ColorYellow : isRequired ? ColorRed : ColorGray;
            string iconGlyph = isInstalled ? "✓" : isInstalling ? "⏳" : isRequired ? "✗" : "○";

            Color configColor;
            string configText;
            if (isInstalling)
            {
                configColor = ColorYellow;
                configText = "Installing...";
            }
            else if (!isInstalled)
            {
                configColor = ColorGray;
                configText = isRequired ? "Auto-installs on mode switch" : "—";
            }
            else if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
            {
                configColor = ColorGreen;
                configText = "✓ Configured";
            }
            else
            {
                configColor = ColorYellow;
                configText = "Add config files";
            }

            string actionLabel = null;
            Action onAction = null;
            bool actionEnabled = true;
            if (!isInstalled && !isInstalling && isPrototype)
            {
                actionLabel = "Install";
                actionEnabled = !EditorApplication.isPlaying;
                onAction = () =>
                {
                    SdkInstaller.Install(SdkId.FirebaseApp);
                    SdkInstaller.Install(SdkId.FirebaseAnalytics);
                    SdkInstaller.Install(SdkId.FirebaseCrashlytics);
                    SdkInstaller.Install(SdkId.FirebaseRemoteConfig);
                };
            }
            else if (isInstalled)
            {
                actionLabel = "Console";
                onAction = () => Application.OpenURL("https://console.firebase.google.com/");
            }

            var nameLabel = new Label(isRequired ? "Firebase" : "Firebase (optional)");
            nameLabel.AddToClassList("sorolla-sdk-row-name");

            return BuildVendorRow(new SdkOverviewRowData
            {
                NameElement = nameLabel,
                IconGlyph = iconGlyph,
                IconColor = iconColor,
                ConfigText = configText,
                ConfigColor = configColor,
                ActionLabel = actionLabel,
                OnAction = onAction,
                ActionEnabled = actionEnabled,
            });
        }

        /// <summary>The only row that mutates the config asset directly. The Toggle callback must
        /// stay byte-identical to the old ToggleLeft handler - same field, same SetDirty target, no
        /// debounce (supervisor's explicit condition for exercising this control live, unlike the
        /// Install buttons above).</summary>
        VisualElement BuildTikTokOverviewRow()
        {
            bool enabled = _config.enableTikTok;
            bool hasAppId = enabled && _config?.tiktokAppId?.IsConfigured == true
                            && _config?.tiktokEmAppId?.IsConfigured == true;

            Color iconColor = hasAppId ? ColorGreen : ColorGray;
            string iconGlyph = hasAppId ? "✓" : "○";

            var toggle = new Toggle("TikTok (optional)") { value = enabled };
            toggle.AddToClassList("sorolla-sdk-row-name");
            toggle.RegisterValueChangedCallback(evt =>
            {
                _config.enableTikTok = evt.newValue;
                EditorUtility.SetDirty(_config);
                RefreshSdkOverviewUI();
                RefreshConfigUI(); // TikTok's config fields show/hide with this same flag
            });

            Color configColor;
            string configText;
            if (!enabled)
            {
                configColor = ColorGray;
                configText = "Disabled";
            }
            else if (hasAppId)
            {
                configColor = ColorGreen;
                configText = "✓ Configured";
            }
            else
            {
                configColor = ColorGray;
                configText = "Set App ID below";
            }

            string actionLabel = null;
            Action onAction = null;
            if (hasAppId)
            {
                actionLabel = "Dashboard";
                onAction = () => Application.OpenURL("https://business.tiktok.com/");
            }

            return BuildVendorRow(new SdkOverviewRowData
            {
                NameElement = toggle,
                IconGlyph = iconGlyph,
                IconColor = iconColor,
                ConfigText = configText,
                ConfigColor = configColor,
                ActionLabel = actionLabel,
                OnAction = onAction,
            });
        }

        /// <summary>Ported to UI Toolkit (p3-sdkoverview): same readiness computation and row order
        /// as the old DrawSdkOverviewSection(), rendered as real VisualElements. Clear-and-rebuild
        /// on data change only, same guarded pattern as RefreshBuildHealthUI - triggers are
        /// RunBuildValidation()'s completion (config/mode changes), the package registration events,
        /// play-mode transitions (Install-button enabled state), and the TikTok toggle itself.</summary>
        void RefreshSdkOverviewUI()
        {
            if (_sdkOverviewContainer == null) return;

            _sdkOverviewContainer.Clear();

            bool isPrototype = SorollaSettings.IsPrototype;

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

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 8;

            var headerLabel = new Label("SDK Overview");
            headerLabel.AddToClassList("sorolla-type-section");
            headerRow.Add(headerLabel);

            var headerSpacer = new VisualElement();
            headerSpacer.style.flexGrow = 1;
            headerRow.Add(headerSpacer);

            headerRow.Add(isReady
                ? StatusBadge.Create("READY", StatusBadge.Severity.Pass)
                : StatusBadge.Create("SETUP REQUIRED", StatusBadge.Severity.Advisory));

            _sdkOverviewContainer.Add(headerRow);

            // GameAnalytics (always required)
            _sdkOverviewContainer.Add(BuildSdkOverviewRow(
                SdkRegistry.All[SdkId.GameAnalytics], gaStatus, "Configure game keys",
                SdkConfigDetector.OpenGameAnalyticsSettings, true));

            // Facebook (always required)
            _sdkOverviewContainer.Add(BuildSdkOverviewRow(
                SdkRegistry.All[SdkId.Facebook], fbStatus, "Set App ID",
                SdkConfigDetector.OpenFacebookSettings, true));

            if (isPrototype)
            {
                // MAX (optional in Prototype) - expandable for ad unit IDs
                _sdkOverviewContainer.Add(BuildMaxOverviewRow(maxSettingsSynced, false));
            }
            else
            {
                // Full mode: MAX + Adjust required
                _sdkOverviewContainer.Add(BuildMaxOverviewRow(maxSettingsSynced, true));
                _sdkOverviewContainer.Add(BuildSdkOverviewRow(
                    SdkRegistry.All[SdkId.Adjust], adjustStatus, "Enter app token below", null, true));
            }

            // Firebase (required in Full, optional in Prototype)
            _sdkOverviewContainer.Add(BuildFirebaseOverviewRow());

            // TikTok (optional in all modes)
            _sdkOverviewContainer.Add(BuildTikTokOverviewRow());
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
            RefreshBuildHealthUI();
            RefreshSdkOverviewUI();
            RefreshConfigUI();
        }

        /// <summary>Ported to UI Toolkit (p3-buildhealth): CalloutCard summary + SectionHeader +
        /// CheckRow per category, rebuilt from _validationResults/_autoFixLog. Clear-and-rebuild on
        /// data change only (RunBuildValidation's completion path + this initial call from
        /// CreateGUI) - never from a per-frame/per-event path, per the supervisor's guard against a
        /// UITK Clear()-in-a-hot-path anti-pattern. Content/logic is unchanged from the old
        /// DrawBuildHealthSection()/DrawFirebaseConfigSubRows() - only the rendering technology.</summary>
        void RefreshBuildHealthUI()
        {
            // Ordering/null-safety guard: RunBuildValidation() can run from OnEnable before
            // CreateGUI() has built _buildHealthContainer on some window lifecycles (and after a
            // domain reload). No-op safely; CreateGUI()'s own call renders whatever
            // _validationResults already holds once the container exists.
            if (_buildHealthContainer == null) return;

            _buildHealthContainer.Clear();

            int errors = _validationResults.Count(r => r.Status == BuildValidator.ValidationStatus.Error);
            bool isHealthy = errors == 0;

            _buildHealthContainer.Add(SectionHeader.Create("Build Health", "Refresh", RunBuildValidation));

            _buildHealthContainer.Add(isHealthy
                ? CalloutCard.Create(CalloutCard.Severity.Success, "Build Health checks passing", "Ready to build.")
                : CalloutCard.Create(CalloutCard.Severity.Blocker, $"{errors} Issue(s)", "Fix the failing checks below before building."));

            foreach (string fix in _autoFixLog)
            {
                var fixLabel = new Label($"AUTO-FIXED: {fix}");
                fixLabel.AddToClassList("sorolla-type-small");
                _buildHealthContainer.Add(fixLabel);
            }

            foreach (BuildValidator.CheckCategory category in (BuildValidator.CheckCategory[])Enum.GetValues(typeof(BuildValidator.CheckCategory)))
            {
                string checkName = BuildValidator.CheckNames[category];
                var categoryResults = _validationResults.Where(r => r.Category == category).ToList();

                bool hasError = categoryResults.Any(r => r.Status == BuildValidator.ValidationStatus.Error);
                bool hasWarning = categoryResults.Any(r => r.Status == BuildValidator.ValidationStatus.Warning);
                var validResult = categoryResults.Find(r => r.Status == BuildValidator.ValidationStatus.Valid);

                CheckRow.Status status;
                string statusText;
                if (hasError)
                {
                    status = CheckRow.Status.Fail;
                    statusText = categoryResults.First(r => r.Status == BuildValidator.ValidationStatus.Error).Message.Split('\n')[0];
                }
                else if (hasWarning)
                {
                    status = CheckRow.Status.Warn;
                    statusText = categoryResults.First(r => r.Status == BuildValidator.ValidationStatus.Warning).Message.Split('\n')[0];
                }
                else if (validResult != null)
                {
                    status = CheckRow.Status.Pass;
                    statusText = validResult.Message;
                }
                else
                {
                    status = CheckRow.Status.Wait; // "Not checked" - not a pass, closest honest state
                    statusText = "Not checked";
                }

                _buildHealthContainer.Add(CheckRow.Create(checkName, status, statusText));

                if (category == BuildValidator.CheckCategory.FirebaseCoherence && SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                {
                    bool androidOk = SdkConfigDetector.IsFirebaseAndroidConfigured();
                    bool iosOk = SdkConfigDetector.IsFirebaseIOSConfigured();

                    VisualElement androidRow = CheckRow.Create("google-services.json",
                        androidOk ? CheckRow.Status.Pass : CheckRow.Status.Warn, androidOk ? "Found" : "Missing");
                    androidRow.style.marginLeft = 24;
                    _buildHealthContainer.Add(androidRow);

                    VisualElement iosRow = CheckRow.Create("GoogleService-Info.plist",
                        iosOk ? CheckRow.Status.Pass : CheckRow.Status.Warn, iosOk ? "Found" : "Missing");
                    iosRow.style.marginLeft = 24;
                    _buildHealthContainer.Add(iosRow);
                }
            }
        }

        #endregion
    }
}
