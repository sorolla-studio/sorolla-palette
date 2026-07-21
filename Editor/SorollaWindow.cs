using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Editor.UI;
using Sorolla.Palette.Health;
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
        VisualElement _configContainer;
        VisualElement _heroContainer;
        VisualElement _greenlightContainer;
        ScrollView _scrollView;
        // Captured so the Greenlight rows for these fields can scroll/focus straight to them instead of
        // leaving "enter it below" as a pointer with no action (F9, 2026-07-21 audit).
        VisualElement _adjustAppTokenField;
        VisualElement _tiktokAppIdField;
        // Per-group expand/collapse memory (vendor-grouping cycle, supervisor 2026-07-21 ~13:50),
        // replaces the old single _greenlightChecksExpanded bool for the one flat row list.
        readonly Dictionary<string, bool> _groupExpanded = new Dictionary<string, bool>();
        List<BuildValidator.ValidationResult> _validationResults = new List<BuildValidator.ValidationResult>();
        readonly GreenlightDeviceSnapshot.State _snapshotState = new GreenlightDeviceSnapshot.State();

        void OnEnable()
        {
            LoadOrCreateConfig();
            RunBuildValidation();
            Events.registeringPackages += OnPackagesRegistering;
            Events.registeredPackages += OnPackagesRegistered;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            FacebookPlatformValidator.OnProbeSettled += RunBuildValidation;
        }

        void OnDisable()
        {
            Events.registeringPackages -= OnPackagesRegistering;
            Events.registeredPackages -= OnPackagesRegistered;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            FacebookPlatformValidator.OnProbeSettled -= RunBuildValidation;
        }

        void OnPackagesRegistering(PackageRegistrationEventArgs args)
        {
            foreach (var package in args.added)
                _installingPackages.Add(package.name);
            Repaint();
            RefreshGreenlightUI(); // vendor group headers show Installing... (vendor-grouping cycle)
        }

        void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            foreach (var package in args.added)
                _installingPackages.Remove(package.name);

            // Re-run validation after packages are installed (no auto-install, just detect)
            EditorApplication.delayCall += () => RunBuildValidation();
            Repaint();
            RefreshGreenlightUI();
        }

        /// <summary>The ported rows' Install-button enabled state mirrors the old GUI.enabled =
        /// !EditorApplication.isPlaying gate, which used to re-evaluate every IMGUI frame for free.
        /// A cleared/rebuilt VisualElement doesn't repaint itself, so play-mode entry/exit is an
        /// explicit rebuild trigger here (same call-site-addition pattern as the package events).</summary>
        void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            RefreshGreenlightUI(); // vendor group headers' Install-button enabled state
            RefreshHeroHeaderUI(); // mode switch dims/undims with play mode (F13.8, 2026-07-21 audit)
        }

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

            // Extra bottom padding (Arthur's follow-up): scrolled content read as crowding the
            // header with no bottom padding at all - a few px more than the side padding gives the
            // fixed header its own clear breathing room above whatever scrolls beneath it.
            _heroContainer = new VisualElement();
            _heroContainer.style.paddingLeft = ContentPadding;
            _heroContainer.style.paddingRight = ContentPadding;
            _heroContainer.style.paddingBottom = ContentPadding;
            rootVisualElement.Add(_heroContainer);
            RefreshHeroHeaderUI();

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.contentContainer.style.paddingLeft = ContentPadding;
            scrollView.contentContainer.style.paddingRight = ContentPadding;
            rootVisualElement.Add(scrollView);
            _scrollView = scrollView;

            scrollView.Add(new IMGUIContainer(DrawUpperSections));

            // Greenlight sits above SDK Overview (studio-self-serve-greenlight-2026-07 plan,
            // §Editor window restructure): one mechanical verdict composing Build Health, editor
            // probes, mode-intent, device snapshot, and the manual/dashboard checklist - the hero's
            // verdict for "is this integration actually ready", above the section-by-section detail.
            _greenlightContainer = CreatePortedSectionContainer();
            _greenlightContainer.style.marginTop = 10;
            scrollView.Add(_greenlightContainer);
            RefreshGreenlightUI();

            // The separate SDK Overview section is gone (vendor-grouping cycle, supervisor 2026-07-21
            // ~13:50): its per-vendor status + Edit/Console/Install affordances now ARE the vendor
            // group headers inside the Greenlight section above - one owner per vendor, structurally,
            // instead of the same fact computed twice (SDK Overview's own status check AND the
            // Greenlight gate row) and kept in sync by hand. Build Health itself still runs (and still
            // auto-fixes) on every refresh and before every build; its row list is gone too (F12),
            // folded into the same Greenlight groups.

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
                modeIsFull: !isPrototype, onSwitchRequested: RequestModeSwitch,
                disabled: EditorApplication.isPlaying));

            // Preview toggle for the internal harness only (fix-cycle ruling 2): flips the same
            // instance flag CreateGUI already reads and rebuilds in place - no second window, no
            // second menu item. Never shown on a plain studio machine (_isHarnessWindow stays false
            // there since InternalDepthDefault is never set without the harness's InitializeOnLoad).
            if (_isHarnessWindow)
            {
                var studioViewToggle = new Toggle("Studio view") { value = !ShowInternalDepth };
                studioViewToggle.RegisterValueChangedCallback(evt =>
                {
                    ShowInternalDepth = !evt.newValue;
                    rootVisualElement.Clear();
                    CreateGUI();
                });
                container.Add(studioViewToggle);
            }

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

        /// <summary>Opens full-window with no internal tab strip (Arthur's design review: match
        /// the AppLovin Integration Manager's presentation) - a utility window, not a normal
        /// dockable one. ShowUtility() is the mechanism that drops the tab chrome; the accepted
        /// trade-off is the window is no longer dockable. Single open path for the whole SDK: focus
        /// the existing instance if one is already open (Resources.FindObjectsOfTypeAll, the same
        /// pattern this whole loop's capture harness already uses) instead of ever spawning a
        /// second one - AutoOpenOnLoad below is the only other caller and goes through this same
        /// method.</summary>
        [MenuItem("Tools/Sorolla Palette SDK")]
        public static void ShowWindow()
        {
            var existing = Resources.FindObjectsOfTypeAll<SorollaWindow>().FirstOrDefault();
            if (existing != null)
            {
                existing.Focus();
                return;
            }

            var window = CreateInstance<SorollaWindow>();
            window._isHarnessWindow = InternalDepthDefault;
            window.ShowInternalDepth = InternalDepthDefault;
            window.titleContent = new GUIContent(InternalDepthDefault ? "Sorolla Palette SDK (Internal)" : "Sorolla Palette SDK");
            window.minSize = new Vector2(420, 380);
            window.position = new Rect(100, 100, 560, 800);
            window.ShowUtility();
        }

        // ── Internal (Sorolla) depth ───────────────────────────────────
        //
        // ONE menu entry everywhere, including machines running the testbed-local internal harness
        // (fix-cycle ruling 2, 2026-07-21 11:30) - the harness must not declare its own MenuItem. The
        // harness instead sets <see cref="InternalDepthDefault"/> to true from its own
        // [InitializeOnLoad] initializer (via InternalsVisibleTo), so the package's single
        // Tools/Sorolla Palette SDK entry opens the full-depth window on a Sorolla machine and the
        // ordinary studio window everywhere else - same evaluator/report/export/validator model, same
        // frontend code, no second window, no second menu item.

        internal static bool InternalDepthDefault;
        internal bool ShowInternalDepth;
        bool _isHarnessWindow;

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

            // MAX Ad Units. Arthur's follow-up: the struct-level revert (plain PropertyField for
            // the whole PlatformAdUnitId) was the right call against double-labeling, but the
            // design intent still applies ONE LEVEL DOWN - each Android/iOS string is a real leaf
            // property, so it gets ValidatedField same as Adjust's fields, inside a manually-built
            // Foldout that keeps the exact same expand/collapse structure PropertyField used to
            // give us for free. Header rendered explicitly for the same reason as Adjust's.
            if (showMax)
            {
                var maxHeader = new Label("MAX Ad Units");
                maxHeader.AddToClassList("sorolla-config-group-header");
                _configContainer.Add(maxHeader);

                _configContainer.Add(BuildAdUnitFoldout("Rewarded", "rewardedAdUnit"));
                _configContainer.Add(BuildAdUnitFoldout("Interstitial", "interstitialAdUnit"));
                // "(optional)" casing matches every other optional-vendor label in this window (F13.3,
                // 2026-07-21 audit) - this was the one outlier capitalizing it.
                _configContainer.Add(BuildAdUnitFoldout("Banner (optional)", "bannerAdUnit"));
            }

            // Adjust (full mode only). adjustAppToken is the ONE documented hard build gate
            // (BuildValidationVendorSettings.cs / SdkConfigDetector.cs: empty or length<=5 fails a
            // Full-mode build) - Invalid state + subtext only while unresolved, no subtext once valid.
            // Group header is now rendered explicitly (not the [Header] attribute's auto-decorator):
            // ValidatedField.CreateBound uses a plain bound TextField, not PropertyField, so the
            // attribute-driven decorator no longer applies here (see ValidatedField.cs for why).
            if (showAdjust)
            {
                // "(Full Mode Only)" dropped (F13.2, 2026-07-21 audit): showAdjust is already gated on
                // !isPrototype, so this header only ever renders in Full mode - the qualifier was dead.
                var adjustHeader = new Label("Adjust");
                adjustHeader.AddToClassList("sorolla-config-group-header");
                _configContainer.Add(adjustHeader);

                var appToken = ValidatedField.CreateBound(_serializedConfig.FindProperty("adjustAppToken"), "App Token", value =>
                {
                    bool valid = !string.IsNullOrEmpty(value) && value.Length > 5;
                    return valid
                        ? (ValidatedField.State.Valid, (string)null)
                        : (ValidatedField.State.Invalid, "Required for Full-mode builds");
                });
                appToken.style.marginLeft = 15;
                _configContainer.Add(appToken);
                _adjustAppTokenField = appToken;

                _configContainer.Add(Indented(BoundField("adjustPurchaseEventToken", "Purchase Event Token")));
            }
            else
            {
                _adjustAppTokenField = null;
            }

            // TikTok (optional - shown only when enabled in SDK Overview). Also PlatformAdUnitId
            // (nested Android/iOS struct, same as the MAX ad units above), not a leaf string -
            // stays plain PropertyField for the same reason.
            if (_config.enableTikTok)
            {
                var tiktokAppIdField = new PropertyField(_serializedConfig.FindProperty("tiktokAppId"), "TikTok App ID");
                _configContainer.Add(Indented(tiktokAppIdField));
                _tiktokAppIdField = tiktokAppIdField;
                _configContainer.Add(Indented(new PropertyField(_serializedConfig.FindProperty("tiktokEmAppId"), "App ID (EM)")));
                _configContainer.Add(Indented(new PropertyField(_serializedConfig.FindProperty("tiktokAccessToken"), "Access Token")));
            }
            else
            {
                _tiktokAppIdField = null;
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

        /// <summary>ValidatedField-styled row for a plain optional string property (p4-config,
        /// item 7): no documented required/invalid rule for these fields, so state is just
        /// filled-vs-empty and there's no subtext to show.</summary>
        /// <summary>Scrolls the field into view and focuses it - the remedy for "enter it below" rows
        /// whose field already lives lower in this same window (F9, 2026-07-21 audit: same treatment as
        /// the Device Snapshot row's Connect Device embed, ruling 5). No-op if the field isn't currently
        /// rendered (e.g. TikTok disabled, or Adjust fields hidden in Prototype mode).</summary>
        void FocusConfigField(VisualElement field)
        {
            if (field == null || _scrollView == null) return;
            _scrollView.ScrollTo(field);
            field.Focus();
        }

        VisualElement BoundField(string propertyName, string label)
        {
            var property = _serializedConfig.FindProperty(propertyName);
            return ValidatedField.CreateBound(property, label, value =>
                string.IsNullOrEmpty(value)
                    ? (ValidatedField.State.None, (string)null)
                    : (ValidatedField.State.Valid, (string)null));
        }

        static VisualElement Indented(VisualElement element)
        {
            element.style.marginLeft = 15;
            return element;
        }

        static readonly Regex MaxAdUnitFormat = new Regex("^[0-9a-f]{16}$");

        /// <summary>Foldout containing ValidatedField rows for a PlatformAdUnitId's Android/iOS
        /// leaf properties (Arthur's follow-up: apply ValidatedField at the leaf level, not the
        /// struct level - avoids the double-label/[Header] problem the earlier struct-level attempt
        /// hit, since each row binds a real leaf SerializedProperty). Foldout keeps the exact same
        /// expand/collapse structure the old plain PropertyField gave us for free (default
        /// expanded, matching the prior behavior).</summary>
        VisualElement BuildAdUnitFoldout(string label, string propertyPath)
        {
            var foldout = new Foldout { text = label, value = true };
            foldout.Add(AdUnitField("Android", propertyPath + ".android"));
            foldout.Add(AdUnitField("iOS", propertyPath + ".ios"));
            return foldout;
        }

        /// <summary>Empty is fully neutral (Arthur, confirmed twice) - no icon, no color, reads as
        /// calm as a stock untouched field (ads are optional at prototype stage, Banner is
        /// explicitly optional). A non-empty value is validated against the MAX ad-unit ID format
        /// (16 lowercase hex chars): Valid (green check) if it matches, amber (State.Required -
        /// the warn-color state, not State.Invalid's red/fail color) with a short subtext if it
        /// doesn't - a malformed ID is a soft warning here, not a hard failure.</summary>
        VisualElement AdUnitField(string label, string propertyPath)
        {
            var property = _serializedConfig.FindProperty(propertyPath);
            return ValidatedField.CreateBound(property, label, value =>
            {
                if (string.IsNullOrEmpty(value))
                    return (ValidatedField.State.None, (string)null);

                bool formatValid = MaxAdUnitFormat.IsMatch(value);
                return formatValid
                    ? (ValidatedField.State.Valid, (string)null)
                    : (ValidatedField.State.Required, "Doesn't look like a MAX ad unit ID");
            });
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
                button.AddToClassList("sorolla-button-small");
                button.SetEnabled(data.ActionEnabled);
                row.Add(button);
            }

            return row;
        }

        VisualElement BuildSdkOverviewRow(SdkInfo sdk, SdkConfigDetector.ConfigStatus configStatus,
            string configHint, Action openSettings, bool isRequired, string configuredDetail = null)
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
                configText = configuredDetail != null ? $"✓ {configuredDetail}" : "✓ Configured";
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

        /// <summary>GameAnalytics is the one vendor Build Health runs TWO checks for (per-platform keys,
        /// and the Resource Whitelist that a game's economy tracking silently needs) - this row reflects
        /// whichever of the two is worse instead of only the key-config status, so it can't disagree with
        /// what Greenlight is warning about for the same vendor (product-audit fix cycle ruling 3,
        /// 2026-07-21 11:55). The Edit button always opens GA Settings regardless of which finding is
        /// worse - it is the one place both facts get fixed. <paramref name="whitelistWarn"/> is computed
        /// once by the caller (now the GameAnalytics group header builder in RefreshGreenlightUI) so this
        /// row and the group's own worst-of glyph can never disagree about it.</summary>
        VisualElement BuildGameAnalyticsOverviewRow(SdkConfigDetector.ConfigStatus keyStatus, bool whitelistWarn)
        {
            SdkInfo sdk = SdkRegistry.All[SdkId.GameAnalytics];
            bool isInstalled = SdkDetector.IsInstalled(sdk);
            bool isInstalling = _installingPackages.Contains(sdk.PackageId);
            string keyDetail = SdkConfigDetector.GetGameAnalyticsPlatformDetail();

            Color iconColor;
            string iconGlyph;
            Color configColor;
            string configText;
            if (isInstalling)
            {
                iconColor = ColorYellow;
                iconGlyph = "⏳";
                configColor = ColorYellow;
                configText = "Installing...";
            }
            else if (!isInstalled)
            {
                iconColor = ColorRed;
                iconGlyph = "✗";
                configColor = ColorGray;
                configText = "—";
            }
            else if (keyStatus != SdkConfigDetector.ConfigStatus.Configured)
            {
                iconColor = ColorRed;
                iconGlyph = "✗";
                configColor = ColorYellow;
                configText = keyDetail;
            }
            else if (whitelistWarn)
            {
                iconColor = ColorYellow;
                iconGlyph = "⚠";
                configColor = ColorYellow;
                configText = $"✓ {keyDetail} · Resource whitelist empty";
            }
            else
            {
                iconColor = ColorGreen;
                iconGlyph = "✓";
                configColor = ColorGreen;
                configText = $"✓ {keyDetail}";
            }

            var nameLabel = new Label(sdk.Name);
            nameLabel.AddToClassList("sorolla-sdk-row-name");

            return BuildVendorRow(new SdkOverviewRowData
            {
                NameElement = nameLabel,
                IconGlyph = iconGlyph,
                IconColor = iconColor,
                ConfigText = configText,
                ConfigColor = configColor,
                ActionLabel = isInstalled ? "Edit" : null,
                OnAction = isInstalled ? SdkConfigDetector.OpenGameAnalyticsSettings : null,
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
                RefreshGreenlightUI(); // TikTok's row lives in the Build & Project group now
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
            else if (enabled)
            {
                // "Set App ID below" used to be a pointer with no action (F9, 2026-07-21 audit) - now
                // scrolls/focuses the actual field, same treatment as the Adjust row above.
                actionLabel = "Set ID";
                onAction = () => FocusConfigField(_tiktokAppIdField);
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

        /// <summary>The vendor group header IS the overview row (vendor-grouping cycle, supervisor
        /// 2026-07-21 ~13:50) - the separate SDK Overview section is gone; RefreshGreenlightUI calls this
        /// once per vendor group to get the exact same row component that section used to render, now
        /// used as that group's collapsible header instead. One owner per vendor, structurally: this is
        /// the ONLY place these five vendors' status gets computed, instead of a second independent
        /// computation duplicating what the group's own gate rows already say.</summary>
        VisualElement BuildVendorGroupHeader(GreenlightAdapter.VendorGroup group)
        {
            switch (group)
            {
                case GreenlightAdapter.VendorGroup.GameAnalytics:
                {
                    // Reflects the WORST editor-side finding for this vendor (product-audit fix cycle
                    // ruling 3): per-platform key status AND the Resource Whitelist check.
                    SdkConfigDetector.ConfigStatus gaStatus = SdkConfigDetector.GetGameAnalyticsStatus();
                    bool gaWhitelistWarn = _validationResults.Any(r =>
                        r.Category == BuildValidator.CheckCategory.GameAnalyticsResourceWhitelist &&
                        r.Status == BuildValidator.ValidationStatus.Warning);
                    return BuildGameAnalyticsOverviewRow(gaStatus, gaWhitelistWarn);
                }
                case GreenlightAdapter.VendorGroup.Facebook:
                    return BuildSdkOverviewRow(
                        SdkRegistry.All[SdkId.Facebook], SdkConfigDetector.GetFacebookStatus(), "Set App ID",
                        SdkConfigDetector.OpenFacebookSettings, true);
                case GreenlightAdapter.VendorGroup.Firebase:
                    return BuildFirebaseOverviewRow();
                case GreenlightAdapter.VendorGroup.AppLovinMax:
                {
                    bool maxInstalled = SdkDetector.IsInstalled(SdkId.AppLovinMAX);
                    if (maxInstalled)
                        MaxSettingsSanitizer.SyncEmbeddedSdkKey();
                    bool maxSettingsSynced = !maxInstalled || MaxSettingsSanitizer.IsSdkKeyConfigured();
                    return BuildMaxOverviewRow(maxSettingsSynced, !SorollaSettings.IsPrototype);
                }
                case GreenlightAdapter.VendorGroup.Adjust:
                    // "Enter app token below" used to be a pointer with no action (F9) - now scrolls/
                    // focuses the actual field. Required only in Full mode; still shown (as optional) in
                    // Prototype since the Adjust category's own gate rows exist there too (harmless
                    // Skipped/"not required" entries) - a group with rows deserves a header.
                    return BuildSdkOverviewRow(
                        SdkRegistry.All[SdkId.Adjust], SdkConfigDetector.GetAdjustStatus(_config),
                        "Enter app token below", () => FocusConfigField(_adjustAppTokenField),
                        !SorollaSettings.IsPrototype);
                default:
                    return null; // Build & Project / Device & QA use the plain worst-of header instead.
            }
        }

        /// <summary>Worst-of header for the two non-vendor catch-all groups (Build & Project, Device &
        /// QA) - same BuildVendorRow visual shape as the vendor headers, just glyph/count-derived from
        /// the group's own rows instead of a vendor-specific status check, and no Edit/Console/Install
        /// action (there's no single settings asset to open for a heterogeneous group).</summary>
        static VisualElement BuildPlainGroupHeader(string title, List<GreenlightEvaluator.Row> rows)
        {
            bool anyFail = rows.Any(r => r.Status == CheckRow.Status.Fail);
            bool anyWarn = rows.Any(r => r.Status == CheckRow.Status.Warn);
            bool anyWait = rows.Any(r => r.Status == CheckRow.Status.Wait);

            Color color = anyFail ? ColorRed : anyWarn ? ColorYellow : anyWait ? ColorGray : ColorGreen;
            string glyph = anyFail ? "✗" : anyWarn ? "⚠" : anyWait ? "•" : "✓";
            int issues = rows.Count(r => r.Status == CheckRow.Status.Fail || r.Status == CheckRow.Status.Warn || r.Status == CheckRow.Status.Wait);
            string configText = issues > 0 ? $"{issues} need attention" : "All clear";

            var nameLabel = new Label(title);
            nameLabel.AddToClassList("sorolla-sdk-row-name");

            return BuildVendorRow(new SdkOverviewRowData
            {
                NameElement = nameLabel,
                IconGlyph = glyph,
                IconColor = color,
                ConfigText = configText,
                ConfigColor = color,
            });
        }

        #endregion

        #region Greenlight

        /// <summary>Rebuilds the Greenlight section: verdict badge + count strip, grouped rows, a
        /// Connect Device button (drives <see cref="GreenlightDeviceSnapshot"/>), and a Copy Report
        /// button. Clear-and-rebuild on data change only - same guarded pattern as the other ported
        /// sections (RunBuildValidation's completion, the manual checklist toggles, and the device
        /// snapshot's onSettled callback).</summary>
        void RefreshGreenlightUI()
        {
            if (_greenlightContainer == null) return;

            _greenlightContainer.Clear();

            GreenlightEvaluator.Report report = GreenlightEvaluator.Evaluate(_validationResults, _snapshotState);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 8;

            var headerLabel = new Label(ShowInternalDepth ? "Greenlight (internal)" : "Greenlight");
            headerLabel.AddToClassList("sorolla-type-section");
            headerRow.Add(headerLabel);

            var headerSpacer = new VisualElement();
            headerSpacer.style.flexGrow = 1;
            headerRow.Add(headerSpacer);

            StatusBadge.Severity badgeSeverity = GreenlightEvaluator.BadgeSeverity(report.Outcome);
            headerRow.Add(StatusBadge.Create(GreenlightEvaluator.VerdictLabel(report.Outcome, report.FailCount, report.WarnCount), badgeSeverity));

            _greenlightContainer.Add(headerRow);

            if (ShowInternalDepth)
            {
                string countStrip = $"{report.FailCount} fail · {report.WarnCount} warn · {report.WaitCount} wait · {report.PassCount} pass";
                var countLabel = new Label(countStrip);
                countLabel.AddToClassList("sorolla-type-small");
                countLabel.style.marginBottom = 8;
                _greenlightContainer.Add(countLabel);
            }

            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.flexWrap = Wrap.Wrap;
            actionsRow.style.marginBottom = 8;

            var refreshButton = new Button(RunBuildValidation) { text = "Refresh" };
            refreshButton.AddToClassList("sorolla-button-small");
            actionsRow.Add(refreshButton);

            var connectButton = new Button(() => GreenlightDeviceSnapshot.Run(_snapshotState, () =>
            {
                RefreshGreenlightUI();
                Repaint();
            }))
            {
                text = _snapshotState.Phase == GreenlightDeviceSnapshot.Phase.Running ? "Connecting..." : "Connect Device (Android/iOS USB)",
                tooltip = "Pulls the live QA snapshot from the connected device over USB. "
                    + "Android uses adb (USB debugging on); iOS uses iproxy from libimobiledevice (device unlocked + trusted). "
                    + "Keep the game foregrounded on the device while connecting.",
            };
            connectButton.AddToClassList("sorolla-button-small");
            connectButton.SetEnabled(_snapshotState.Phase != GreenlightDeviceSnapshot.Phase.Running);
            actionsRow.Add(connectButton);

            // Copy the AUDITABLE canonical report (review F4): the readable rendering carries every row's
            // disposition/requirement/proof + a build fingerprint, so a pasted result is unambiguous. The old
            // flattened summary is no longer the export - it dropped inert rows and provenance.
            // "(text)" only disambiguates against the JSON sibling that exists in the internal view - on
            // the studio surface there is no sibling to disambiguate against, so the suffix is meaningless
            // noise there (F13.4, 2026-07-21 audit).
            var copyButton = new Button(() =>
                    EditorGUIUtility.systemCopyBuffer = GreenlightReportExport.ToText(report.Health, report.Fingerprint, report.Context))
                { text = ShowInternalDepth ? "Copy Report (text)" : "Copy Report" };
            copyButton.AddToClassList("sorolla-button-small");
            actionsRow.Add(copyButton);

            if (ShowInternalDepth)
            {
                var copyJsonButton = new Button(() =>
                        EditorGUIUtility.systemCopyBuffer = GreenlightReportExport.ToJson(report.Health, report.Fingerprint, report.Context))
                    { text = "Copy Report (JSON)" };
                copyJsonButton.AddToClassList("sorolla-button-small");
                actionsRow.Add(copyJsonButton);

                var exportCatalogButton = new Button(GateCatalogExporter.ShowSavePanel)
                    { text = "Export Gate Catalog (JSON)" };
                exportCatalogButton.AddToClassList("sorolla-button-small");
                actionsRow.Add(exportCatalogButton);
            }

            _greenlightContainer.Add(actionsRow);

            // Privacy banner REMOVED entirely, both surfaces, no copy-time replacement (F13.5 ruling,
            // 2026-07-21 ~12:30) - it was internal QA vocabulary ("tester names", "evidence notes") on a
            // studio surface where attestations don't exist, and a permanent fixture above every
            // actionable row on both surfaces either way.

            // Rows regroup by their existing catalog/validator category (vendor-grouping cycle,
            // supervisor 2026-07-21 ~13:50) - never by label string-matching. One dictionary build feeds
            // both the internal and studio render below.
            var grouped = new Dictionary<GreenlightAdapter.VendorGroup, List<GreenlightEvaluator.Row>>();
            foreach (GreenlightEvaluator.Row row in report.Rows)
            {
                GreenlightAdapter.VendorGroup group = GreenlightAdapter.GroupFor(row.GateId);
                if (!grouped.TryGetValue(group, out List<GreenlightEvaluator.Row> list))
                    grouped[group] = list = new List<GreenlightEvaluator.Row>();
                list.Add(row);
            }

            if (ShowInternalDepth)
            {
                // Build Health's unique bits folded in here, its duplicate row list deleted (F12 ruling,
                // 2026-07-21 ~12:30): the same _validationResults used to render TWICE on this one surface
                // (Greenlight gate rows + a separate Build Health row list) with different vocabularies and
                // different totals. Only the profile selector, the AUTO-FIXED log, and the Firebase
                // sub-rows were unique to Build Health; its callout was a third, redundant verdict and is
                // simply gone now that Greenlight's badge above is the one verdict.
                var profileField = new EnumField("Validation Profile", BuildValidationProfileSettings.Current);
                profileField.AddToClassList("sorolla-type-small");
                profileField.style.marginBottom = 6;
                profileField.RegisterValueChangedCallback(evt =>
                {
                    BuildValidationProfileSettings.Current = (ValidationProfile)evt.newValue;
                    RunBuildValidation();
                });
                _greenlightContainer.Add(profileField);

                foreach (string fix in _autoFixLog)
                {
                    var fixLabel = new Label($"AUTO-FIXED: {fix}");
                    fixLabel.AddToClassList("sorolla-type-small");
                    _greenlightContainer.Add(fixLabel);
                }

                foreach (GreenlightAdapter.VendorGroup group in GroupOrder)
                {
                    if (!grouped.TryGetValue(group, out List<GreenlightEvaluator.Row> rows) || rows.Count == 0)
                        continue;

                    var rowElements = new List<VisualElement>();
                    foreach (GreenlightEvaluator.Row row in rows)
                    {
                        rowElements.Add(BuildGreenlightRow(row, includeAttestation: true));

                        // Firebase sub-rows, active target only (F7 fix, carried over from the deleted
                        // Build Health list): attached right under the Firebase Coherence gate row, now
                        // nested inside the Firebase group instead of a separate section.
                        if (row.GateId == GateIds.BuildFirebaseCoherence && SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
                        {
                            bool isIos = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
                            VisualElement platformRow = isIos
                                ? CheckRow.Create("GoogleService-Info.plist",
                                    SdkConfigDetector.IsFirebaseIOSConfigured() ? CheckRow.Status.Pass : CheckRow.Status.Warn,
                                    SdkConfigDetector.IsFirebaseIOSConfigured() ? "Found" : "Missing")
                                : CheckRow.Create("google-services.json",
                                    SdkConfigDetector.IsFirebaseAndroidConfigured() ? CheckRow.Status.Pass : CheckRow.Status.Warn,
                                    SdkConfigDetector.IsFirebaseAndroidConfigured() ? "Found" : "Missing");
                            platformRow.style.marginLeft = 24;
                            rowElements.Add(platformRow);
                        }
                    }

                    // TikTok has no gate/category at all (a pure config toggle, not a Build Health check) -
                    // it doesn't fit the grouping key ("the gate's existing category"), so it can't earn its
                    // own group without inventing a fourth one. Appended as an extra row in Build & Project,
                    // the catch-all for project-level config that isn't vendor-specific.
                    if (group == GreenlightAdapter.VendorGroup.BuildAndProject)
                        rowElements.Add(BuildTikTokOverviewRow());

                    VisualElement header = BuildVendorGroupHeader(group) ?? BuildPlainGroupHeader(GroupTitle(group), rows);
                    bool anyAttention = rows.Any(r => r.Status == CheckRow.Status.Fail || r.Status == CheckRow.Status.Warn || r.Status == CheckRow.Status.Wait);
                    _greenlightContainer.Add(BuildExpandableGroup($"internal:{group}", header, rowElements, anyAttention));
                }

                return;
            }

            // Studio view: same grouping, same filter as before regrouping - only studio-red actionable
            // rows render, per group (vendor-grouping cycle, supervisor 2026-07-21 ~13:50). A group with
            // nothing actionable renders NOT AT ALL (zero-leverage rule) - EXCEPT Build & Project, which
            // always carries the TikTok enable/configure control regardless of whether any gate row in
            // that group needs attention, since that's a studio-facing control, not a diagnostic.
            // Manual/dashboard attestation checklist rows are Sorolla QA process (studio pilot not yet
            // delegated), so they never render here either, per ruling 2 - EXCEPT the uncertified-pin case
            // (Disposition.Omitted), which is genuinely studio-actionable (F1 ruling, 2026-07-21 ~12:30).
            bool anyOpen = false;
            foreach (GreenlightAdapter.VendorGroup group in GroupOrder)
            {
                grouped.TryGetValue(group, out List<GreenlightEvaluator.Row> rows);
                rows ??= new List<GreenlightEvaluator.Row>();

                var actionableRows = new List<GreenlightEvaluator.Row>();
                foreach (GreenlightEvaluator.Row row in rows)
                {
                    // Info rows are a deliberate skip/absence, not a fix a studio can act on (F5 residual).
                    if (row.Status == CheckRow.Status.Pass || row.Status == CheckRow.Status.Info) continue;
                    bool isManualChecklistRow = GreenlightManualChecklist.DescriptorForLabel(row.Label) != null;
                    bool isStudioActionablePinIssue = row.Disposition == GateDisposition.Omitted;
                    if (isManualChecklistRow && !isStudioActionablePinIssue) continue;
                    actionableRows.Add(row);
                }

                bool isBuildAndProject = group == GreenlightAdapter.VendorGroup.BuildAndProject;
                if (actionableRows.Count == 0 && !isBuildAndProject) continue;

                if (actionableRows.Count > 0) anyOpen = true;

                var groupContainer = new VisualElement();
                groupContainer.style.marginBottom = 8;
                groupContainer.Add(BuildVendorGroupHeader(group) ?? BuildPlainGroupHeader(GroupTitle(group), rows));

                var rowsWrap = new VisualElement();
                rowsWrap.style.marginLeft = 20;
                foreach (GreenlightEvaluator.Row row in actionableRows)
                    rowsWrap.Add(BuildGreenlightRow(row, includeAttestation: false));
                if (isBuildAndProject)
                    rowsWrap.Add(BuildTikTokOverviewRow());
                groupContainer.Add(rowsWrap);

                _greenlightContainer.Add(groupContainer);
            }

            if (!anyOpen)
            {
                // F1 ruling (2026-07-21 ~12:30): NOT a semantics change - INCOMPLETE-while-clean is
                // correct order-of-operations (Sorolla QA precedes release), so the empty state now
                // explains that instead of asserting "nothing outstanding" right next to a badge that
                // still reads INCOMPLETE.
                var clear = new Label("Your setup is clean - remaining checks are Sorolla's QA before release.");
                clear.AddToClassList("sorolla-type-small");
                _greenlightContainer.Add(clear);
            }
        }

        static readonly GreenlightAdapter.VendorGroup[] GroupOrder =
        {
            GreenlightAdapter.VendorGroup.GameAnalytics,
            GreenlightAdapter.VendorGroup.Facebook,
            GreenlightAdapter.VendorGroup.Firebase,
            GreenlightAdapter.VendorGroup.AppLovinMax,
            GreenlightAdapter.VendorGroup.Adjust,
            GreenlightAdapter.VendorGroup.BuildAndProject,
            GreenlightAdapter.VendorGroup.DeviceAndQa,
        };

        static string GroupTitle(GreenlightAdapter.VendorGroup group) => group switch
        {
            GreenlightAdapter.VendorGroup.GameAnalytics => "GameAnalytics",
            GreenlightAdapter.VendorGroup.Facebook => "Facebook",
            GreenlightAdapter.VendorGroup.Firebase => "Firebase",
            GreenlightAdapter.VendorGroup.AppLovinMax => "AppLovin MAX",
            GreenlightAdapter.VendorGroup.Adjust => "Adjust",
            GreenlightAdapter.VendorGroup.BuildAndProject => "Build & Project",
            GreenlightAdapter.VendorGroup.DeviceAndQa => "Device & QA",
            _ => group.ToString(),
        };

        /// <summary>A vendor/plain header (the overview row) plus a collapsible rows container, toggled
        /// by a small arrow so clicking the header's own Edit/Console/Install button doesn't also
        /// fold/unfold the group. <paramref name="persistKey"/> remembers the user's manual toggle across
        /// refreshes within this window session (replaces the old single _greenlightChecksExpanded bool,
        /// now one per group); <paramref name="defaultExpanded"/> only applies the first time a key is
        /// seen.</summary>
        VisualElement BuildExpandableGroup(string persistKey, VisualElement header, List<VisualElement> rows, bool defaultExpanded)
        {
            bool expanded = _groupExpanded.TryGetValue(persistKey, out bool remembered) ? remembered : defaultExpanded;

            var container = new VisualElement();
            container.style.marginBottom = 8;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;

            var arrow = new Label(expanded ? "▾" : "▸");
            arrow.style.width = 16;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.pickingMode = PickingMode.Position;
            headerRow.Add(arrow);

            header.style.flexGrow = 1;
            headerRow.Add(header);
            container.Add(headerRow);

            var rowsWrap = new VisualElement();
            rowsWrap.style.marginLeft = 20;
            rowsWrap.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (VisualElement row in rows)
                rowsWrap.Add(row);
            container.Add(rowsWrap);

            arrow.RegisterCallback<ClickEvent>(_ =>
            {
                bool nowExpanded = rowsWrap.style.display == DisplayStyle.None;
                rowsWrap.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.text = nowExpanded ? "▾" : "▸";
                _groupExpanded[persistKey] = nowExpanded;
            });

            return container;
        }

        /// <summary>A CheckRow plus its Fix/deep-link line and, for manual checklist rows in the
        /// internal harness, a Verified toggle - manual/dashboard rows never render as a bare
        /// unchecked box (brief requirement: always carry fix text + deep link). Studio callers pass
        /// includeAttestation: false since attestation checklist rows never reach the studio render
        /// (ruling 2 - they're Sorolla QA process, not a studio row at all).</summary>
        VisualElement BuildGreenlightRow(GreenlightEvaluator.Row row, bool includeAttestation)
        {
            var container = new VisualElement();

            // Looked up once, up front: used below both to decide how this row's detail text lays out
            // (F10) and, further down, to render the Attest button (unchanged behavior, just no longer a
            // second lookup of the same descriptor).
            GreenlightManualChecklist.Descriptor manual = GreenlightManualChecklist.DescriptorForLabel(row.Label);

            // Manual gates carry a multi-sentence "why this matters" paragraph as Detail, which CheckRow
            // puts in its bold, right-aligned, single-line status slot - ragged 5-7 line blue columns
            // colliding with neighbor rows (F10, 2026-07-21 audit). The status slot gets a short state
            // word instead; the full paragraph renders full-width under the label, same as Fix text.
            bool longDetail = manual != null && row.Status != CheckRow.Status.Pass;
            string statusSlotText = longDetail ? row.Status.ToString().ToUpperInvariant() : row.Detail;
            container.Add(CheckRow.Create(row.Label, row.Status, statusSlotText));

            if (longDetail && !string.IsNullOrEmpty(row.Detail))
            {
                var whyLabel = new Label(row.Detail);
                whyLabel.AddToClassList("sorolla-type-small");
                whyLabel.style.marginLeft = 24;
                whyLabel.style.whiteSpace = WhiteSpace.Normal;
                container.Add(whyLabel);
            }

            // Pass rows suppress Fix text and remedy buttons entirely (product-audit finding F4,
            // 2026-07-21): a green row with mandatory "Fix:" homework and an action button pointing at
            // nothing-to-act-on is the glyph-vs-text contradiction family, one level deeper than the
            // ruled defects. The GA credential probe's platform-registration reminder is the ONE
            // deliberate exception (its own scope comment says never drop it, since the probe genuinely
            // cannot verify that fact) - it still renders on a Pass row, re-prefixed "Note:" instead of
            // "Fix:" so it doesn't read as an outstanding requirement.
            // Info rows (deliberate skip/absence, F5 residual) get the same treatment as Pass - no Fix
            // text and no action button, since a skip is not a caveat to resolve.
            bool isPass = row.Status == CheckRow.Status.Pass || row.Status == CheckRow.Status.Info;
            bool isGaCredentialNote = row.GateId == GateIds.BuildGameAnalyticsCredentials;

            if (!string.IsNullOrEmpty(row.Fix) && (!isPass || isGaCredentialNote))
            {
                string prefix = isPass ? "Note" : "Fix";
                var fixLabel = new Label($"{prefix}: {row.Fix}");
                fixLabel.AddToClassList("sorolla-type-small");
                fixLabel.style.marginLeft = 24;
                fixLabel.style.whiteSpace = WhiteSpace.Normal;
                container.Add(fixLabel);
            }

            if (!isPass && !string.IsNullOrEmpty(row.DeepLinkUrl))
            {
                var linkButton = new Button(() => Application.OpenURL(row.DeepLinkUrl)) { text = row.DeepLinkLabel ?? "Open" };
                linkButton.AddToClassList("sorolla-footer-link");
                linkButton.style.marginLeft = 24;
                container.Add(linkButton);
            }

            // Editor-performable fix -> a button on the row, same pattern as the Open Dashboard deep link
            // above, not just prose (product-audit fix cycle ruling 1, 2026-07-21 11:55). Pass rows get no
            // action button (F4) - nothing to act on.
            (string actionLabel, Action action) = isPass ? (null, null) : GreenlightAdapter.EditorActionFor(row.GateId);
            if (action != null)
            {
                var actionButton = new Button(action) { text = actionLabel };
                actionButton.AddToClassList("sorolla-footer-link");
                actionButton.style.marginLeft = 24;
                container.Add(actionButton);
            }

            // The device snapshot row's remedy IS the Connect Device action already in this section's
            // header - point straight at it instead of leaving a bare "evidence missing" line with no
            // button (product-audit fix cycle ruling 5, 2026-07-21 11:55).
            if (row.GateId == GateIds.DeviceNoSdkErrors && row.Status != CheckRow.Status.Pass)
            {
                // Connect Device failures were completely silent (F3, 2026-07-21): every failure path
                // writes State.DetailMessage but nothing ever rendered it, so pressing the button with no
                // device attached looked like a broken no-op. Surface the actual reason once the attempt
                // has settled without producing a parsed snapshot.
                bool settledWithoutSnapshot = _snapshotState.Phase == GreenlightDeviceSnapshot.Phase.Done &&
                                               _snapshotState.Outcome != GreenlightDeviceSnapshot.Outcome.Parsed;
                if (settledWithoutSnapshot && !string.IsNullOrEmpty(_snapshotState.DetailMessage))
                {
                    var detailLabel = new Label(_snapshotState.DetailMessage);
                    detailLabel.AddToClassList("sorolla-type-small");
                    detailLabel.style.marginLeft = 24;
                    detailLabel.style.whiteSpace = WhiteSpace.Normal;
                    container.Add(detailLabel);
                }

                bool connecting = _snapshotState.Phase == GreenlightDeviceSnapshot.Phase.Running;
                var connectRowButton = new Button(() => GreenlightDeviceSnapshot.Run(_snapshotState, () =>
                {
                    RefreshGreenlightUI();
                    Repaint();
                }))
                { text = connecting ? "Connecting..." : "Connect Device" };
                connectRowButton.AddToClassList("sorolla-button-small");
                connectRowButton.style.marginLeft = 24;
                connectRowButton.SetEnabled(!connecting);
                container.Add(connectRowButton);
            }

            // Mode Consistency's fix is literally this window's own hero-header mode switch (F6,
            // 2026-07-21 audit) - render it as a row action instead of prose alone. RequestModeSwitch()
            // always targets "the other mode" (only two exist), so it's correct whichever direction this
            // row's issue actually points.
            if (row.GateId == GateIds.BuildModeConsistency && row.Status != CheckRow.Status.Pass)
            {
                var switchModeButton = new Button(RequestModeSwitch) { text = "Switch Mode" };
                switchModeButton.AddToClassList("sorolla-button-small");
                switchModeButton.style.marginLeft = 24;
                switchModeButton.SetEnabled(!EditorApplication.isPlaying);
                container.Add(switchModeButton);
            }

            // The Report Integrity row (a schema/contract error, GateId null) had "report it to Sorolla"
            // with no channel to do so (F13.9, 2026-07-21 audit) - the footer already links to the GitHub
            // issues page, so point the row straight at it instead of leaving the instruction unactionable.
            if (row.Label == "Report Integrity" && row.Status != CheckRow.Status.Pass)
            {
                var reportButton = new Button(() => Application.OpenURL("https://github.com/sorolla-studio/sorolla-palette/issues"))
                    { text = "Report Issue" };
                reportButton.AddToClassList("sorolla-footer-link");
                reportButton.style.marginLeft = 24;
                container.Add(reportButton);
            }

            if (!includeAttestation) return container;

            // Manual gates are satisfied by a SCOPED attestation recorded against the current build identity
            // (Cycle 4b), not a legacy tick. The Attest button records who/when/which-build/what-proof; the
            // gate only reads PASS while that attestation matches the current build and is fresh.
            if (manual != null)
            {
                var attestButton = new Button(() =>
                {
                    // Opens a confirmation + evidence-note prompt; device gates bind to the connected build GUID
                    // (review C45-06). No one-click PASS.
                    string deviceBuildGuid = GreenlightDeviceSnapshot.BuildGuidOf(_snapshotState);
                    QaAttestPromptWindow.Show(manual, deviceBuildGuid, RefreshGreenlightUI);
                })
                { text = "Attest for this build…" };
                attestButton.AddToClassList("sorolla-button-small");
                attestButton.style.marginLeft = 24;
                container.Add(attestButton);
            }

            return container;
        }

        #endregion

        #region Build Validation

        /// <summary>
        ///     Run build validation checks and auto-fixes. Build Health no longer has its own rendered
        ///     section (F12 ruling, 2026-07-21 ~12:30 - its duplicate row list is deleted, its unique bits
        ///     folded into the internal Greenlight render) - this method still runs every check and
        ///     auto-fix exactly as before; only the now-deleted RefreshBuildHealthUI call is gone.
        /// </summary>
        void RunBuildValidation()
        {
            _autoFixLog.Clear();

            // Auto-fix: Sync config and install missing required SDKs
            if (BuildValidator.FixConfigSync())
                _autoFixLog.Add("Synced config / required SDKs / registries");

            // Run all sanitizers (single source of truth)
            _autoFixLog.AddRange(BuildValidator.RunAutoFixes());

            // Run validation checks
            _validationResults = BuildValidator.RunAllChecks();
            Repaint();
            RefreshConfigUI();
            RefreshGreenlightUI();
        }

        #endregion

    }
}
