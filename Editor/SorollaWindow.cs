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
        VisualElement _heroContainer;
        bool _buildHealthChecksExpanded;
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
            // Content padding (Arthur's design review, item 8): content was flush against the
            // window edges. Matches the gallery's per-section padding (space-4, 12px). Applied to
            // the header (unscrolled) and the ScrollView's contentContainer specifically, NOT
            // rootVisualElement/the ScrollView itself - contentContainer is the actual scrolled
            // element, separate from the scrollbar chrome, so the scrollbar stays flush to the true
            // window edge (matching the gallery's look) while the content gets inset.
            const float ContentPadding = 12f;

            _heroContainer = new VisualElement();
            _heroContainer.style.paddingLeft = ContentPadding;
            _heroContainer.style.paddingRight = ContentPadding;
            rootVisualElement.Add(_heroContainer);
            RefreshHeroHeaderUI();

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.contentContainer.style.paddingLeft = ContentPadding;
            scrollView.contentContainer.style.paddingRight = ContentPadding;
            rootVisualElement.Add(scrollView);

            scrollView.Add(new IMGUIContainer(DrawUpperSections));

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

            // p3-quickstart: DrawInfoSection() was the FIRST (now only non-Links) call in
            // DrawLowerSections, so porting it is another clean front-peel. Static content (no
            // live SDK/config state), so no stored field or refresh method - built once here.
            var quickstartContainer = CreatePortedSectionContainer();
            quickstartContainer.style.marginBottom = 10;
            quickstartContainer.Add(BuildQuickstartSection());
            scrollView.Add(quickstartContainer);

            // p3-footer: DrawLinksSection() was the LAST remaining call in DrawLowerSections, so
            // this is the final peel - IMGUIContainerB (and DrawLowerSections/
            // DrawLowerSectionsWithStyles entirely) is deleted below, the hybrid migration is
            // complete, and CreateGUI() is now pure VisualElement composition apart from
            // IMGUIContainerA (PlayModeWarning + Mode, unchanged - not in this cycle's scope).
            var footerContainer = CreatePortedSectionContainer();
            footerContainer.Add(BuildFooterLinksSection());
            scrollView.Add(footerContainer);
        }

        /// <summary>Shared boilerplate for every ported section container: fixed-dark theme scope
        /// + the tokens stylesheet. Factored once three call sites needed the same 6 lines
        /// (p3-config); now used by all six ported sections.</summary>
        static VisualElement CreatePortedSectionContainer()
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-root");
            container.AddToClassList(EditorGUIUtility.isProSkin ? "sorolla-skin-dark" : "sorolla-skin-light");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                container.styleSheets.Add(styleSheet);
            return container;
        }

        /// <summary>Ported to UI Toolkit (p3-header) - real HeroHeader component, scoped to its
        /// own .sorolla-root container so the fixed-dark token theme applies only to the ported
        /// section, not the still-IMGUI content below it (mid-migration, per PLAN.md's per-section
        /// approach - the visual seam is expected and temporary, not a bug).</summary>
        /// <summary>Rebuilds the hero header fresh so its segmented mode switch reflects the
        /// current mode after a switch - the switch fires SorollaSettings.SetMode synchronously,
        /// then this repaints it in place (same rebuild-on-change pattern as the other ported
        /// sections, not a live in-place mutation of the segments).</summary>
        void RefreshHeroHeaderUI()
        {
            if (_heroContainer == null) return;
            _heroContainer.Clear();

            var container = CreatePortedSectionContainer();
            bool isPrototype = SorollaSettings.Mode == SorollaMode.Prototype;
            container.Add(HeroHeader.Create("Palette SDK", $"v{Version} - Plug & Play Publisher Stack",
                modeIsFull: !isPrototype, onSwitchRequested: RequestModeSwitch));
            _heroContainer.Add(container);
        }

        /// <summary>The exact same switch flow the old IMGUI Mode box used (confirmation dialog +
        /// SorollaSettings.SetMode + re-validate) - item 10 moves the presentation into the hero
        /// header's segmented switch, it does not touch this behavior.</summary>
        void RequestModeSwitch()
        {
            if (EditorApplication.isPlaying) return;

            SorollaMode otherMode = SorollaSettings.Mode == SorollaMode.Prototype ? SorollaMode.Full : SorollaMode.Prototype;
            if (EditorUtility.DisplayDialog($"Switch to {otherMode} Mode?",
                "This will install/uninstall SDKs as needed.", "Switch", "Cancel"))
            {
                SorollaSettings.SetMode(otherMode);
                RefreshHeroHeaderUI();
                EditorApplication.delayCall += () => RunBuildValidation();
            }
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

        /// <summary>Ported to UI Toolkit (p3-quickstart). The old box mixed real integration facts
        /// (auto-init, ATT) with pseudo-code bullets ("Start/Complete/Fail" as one slash-joined
        /// non-compilable line) - CodeSnippetBlock ships a Copy button that implies real, pasteable
        /// syntax, so pseudo-code behind it would be a correctness regression, not a neutral
        /// re-style. Split instead: the auto-init/ATT facts stay prose (CalloutCard Info, its
        /// actual payload - the SDK self-bootstraps with no init call/GameObject - is the single
        /// most important integration fact in this window and must not be lost); the three API
        /// patterns become real, minimal, source-verified one-liners (checked against
        /// Runtime/Palette.Level.cs, Runtime/Palette.Economy.cs, Runtime/Palette.cs directly, not
        /// against docs or memory - simplest correct call per pattern, no kitchen-sink optional
        /// params). The Level snippet matches the gallery's existing CodeSnippetBlock demo verbatim
        /// (one source of truth for what "the" canonical snippet looks like).</summary>
        static VisualElement BuildQuickstartSection()
        {
            var container = new VisualElement();

            container.Add(CalloutCard.Create(CalloutCard.Severity.Info, "Auto-Initialization",
                "The SDK auto-initializes when your game starts - no init call, no GameObject required. " +
                "iOS shows the ATT consent dialog automatically."));

            container.Add(SectionHeader.Create("Quick Start"));

            container.Add(CodeSnippetBlock.Create("Level Progression",
                "Palette.Level.Start(1);\nPalette.Level.Complete(1, score: 100);"));
            container.Add(CodeSnippetBlock.Create("Economy",
                "Palette.Economy.Earn(CurrencyId.Coins, 100, EconomySource.LevelReward);"));
            container.Add(CodeSnippetBlock.Create("Custom Event",
                "Palette.TrackEvent(\"tutorial_done\");"));

            return container;
        }

        /// <summary>Ported to UI Toolkit (p3-footer) - the last IMGUI section. Three real UI
        /// Toolkit Buttons styled as text links (accent color, no chrome) instead of
        /// EditorStyles.linkLabel + GUILayout.Button, same URLs unchanged.</summary>
        static VisualElement BuildFooterLinksSection()
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-footer-links");

            row.Add(CreateFooterLink("Documentation", "https://github.com/sorolla-studio/sorolla-palette#readme"));
            row.Add(CreateFooterSeparator());
            row.Add(CreateFooterLink("GitHub", "https://github.com/sorolla-studio/sorolla-palette"));
            row.Add(CreateFooterSeparator());
            row.Add(CreateFooterLink("Report Issue", "https://github.com/sorolla-studio/sorolla-palette/issues"));

            return row;
        }

        static Button CreateFooterLink(string label, string url)
        {
            var button = new Button(() => Application.OpenURL(url)) { text = label };
            button.AddToClassList("sorolla-footer-link");
            return button;
        }

        static Label CreateFooterSeparator()
        {
            var label = new Label("|");
            label.AddToClassList("sorolla-footer-separator");
            return label;
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
        /// DrawBuildHealthSection()/DrawFirebaseConfigSubRows() - only the rendering technology.
        /// The check list itself is wrapped in a CollapsibleCheckGroup (Arthur's design review,
        /// 2026-07-08): default-collapsed so the always-visible CalloutCard summary above it is the
        /// primary signal, not a wall of rows; _buildHealthChecksExpanded remembers whatever the
        /// user last chose across refreshes (Refresh button, mode switch, etc.) within this window
        /// session, rather than re-collapsing every rebuild.</summary>
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

            var checkRows = new List<VisualElement>();
            int issueCount = 0;

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
                    issueCount++;
                }
                else if (hasWarning)
                {
                    status = CheckRow.Status.Warn;
                    statusText = categoryResults.First(r => r.Status == BuildValidator.ValidationStatus.Warning).Message.Split('\n')[0];
                    issueCount++;
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

                checkRows.Add(CheckRow.Create(checkName, status, statusText));

                if (category == BuildValidator.CheckCategory.FirebaseCoherence && SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                {
                    bool androidOk = SdkConfigDetector.IsFirebaseAndroidConfigured();
                    bool iosOk = SdkConfigDetector.IsFirebaseIOSConfigured();

                    VisualElement androidRow = CheckRow.Create("google-services.json",
                        androidOk ? CheckRow.Status.Pass : CheckRow.Status.Warn, androidOk ? "Found" : "Missing");
                    androidRow.style.marginLeft = 24;
                    checkRows.Add(androidRow);

                    VisualElement iosRow = CheckRow.Create("GoogleService-Info.plist",
                        iosOk ? CheckRow.Status.Pass : CheckRow.Status.Warn, iosOk ? "Found" : "Missing");
                    iosRow.style.marginLeft = 24;
                    checkRows.Add(iosRow);
                }
            }

            string summary = issueCount > 0 ? $"{issueCount} of {checkRows.Count} checks need attention" : $"{checkRows.Count} checks passing";
            Foldout checkGroup = CollapsibleCheckGroup.Create(summary, checkRows, _buildHealthChecksExpanded);
            checkGroup.RegisterValueChangedCallback(evt => _buildHealthChecksExpanded = evt.newValue);
            _buildHealthContainer.Add(checkGroup);
        }

        #endregion
    }
}
