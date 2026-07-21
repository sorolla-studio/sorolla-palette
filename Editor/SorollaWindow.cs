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

        // Version from package.json (cached)
        static string s_version;
        static string Version => s_version ??= UnityEditor.PackageManager.PackageInfo
            .FindForAssembly(typeof(SorollaWindow).Assembly)?.version ?? "?.?.?";

        // Instance state
        readonly List<string> _autoFixLog = new List<string>();
        readonly HashSet<string> _installingPackages = new HashSet<string>();
        SorollaConfig _config;
        SerializedObject _serializedConfig;
        VisualElement _heroContainer;
        VisualElement _headerActionsHost;
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

            // Global actions (Refresh / Connect Device / Copy Report + internal-only exports) live
            // here, fixed below the hero and above the scrolled content (Arthur ruling 2026-07-21
            // ~17:50) - one home for window-wide actions, no in-content duplicates.
            _headerActionsHost = new VisualElement();
            _headerActionsHost.style.paddingLeft = ContentPadding;
            _headerActionsHost.style.paddingRight = ContentPadding;
            _headerActionsHost.style.paddingBottom = ContentPadding;
            rootVisualElement.Add(_headerActionsHost);

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

            // The standalone SDK Keys section is gone too (vendor-consolidation cycle, Arthur ruling
            // 2026-07-21 15:35): MAX ad units, Adjust tokens, and TikTok's toggle+fields render as config
            // inputs inside their own Group in the Greenlight section above (BuildConfigInputsForGroup),
            // so a vendor's status, check rows, and config all live under one foldout. Verbose Logging
            // moved into Build & Project's group. TikTok itself is one of the Groups (rewrite cycle,
            // 2026-07-21 ~16:45), not a bespoke section.

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

            // No Studio-view toggle: the internal depth is a separate dedicated window (ShowInternal),
            // so the shipped window carries zero internal chrome (Arthur ruling 2026-07-21 ~17:55).
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
        public static void ShowWindow() => Open(internalDepth: false);

        /// <summary>Entry point for the Sorolla-internal harness (via InternalsVisibleTo): a SEPARATE
        /// full-depth window, so the shipped window is always exactly what studios see - no Studio-view
        /// toggle, no internal chrome (Arthur ruling 2026-07-21 ~17:55, superseding the earlier
        /// one-window/one-menu-entry design). Same evaluator/report/export/validator model and the same
        /// frontend code behind both; only the per-instance depth flag differs.</summary>
        internal static void ShowInternal() => Open(internalDepth: true);

        static void Open(bool internalDepth)
        {
            SorollaWindow existing = Resources.FindObjectsOfTypeAll<SorollaWindow>()
                .FirstOrDefault(w => w.ShowInternalDepth == internalDepth);
            if (existing != null)
            {
                existing.Focus();
                return;
            }

            var window = CreateInstance<SorollaWindow>();
            window.ShowInternalDepth = internalDepth;
            window.titleContent = new GUIContent(internalDepth ? "Sorolla Palette SDK (Internal)" : "Sorolla Palette SDK");
            window.minSize = new Vector2(420, 380);
            window.position = new Rect(100, 100, 560, 800);
            window.ShowUtility();
        }

        // Serialized so the internal window stays internal across domain reloads instead of silently
        // reverting to the studio view.
        [SerializeField] internal bool ShowInternalDepth;

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

        /// <summary>Rewrite cycle (Arthur ruling 2026-07-21 ~16:45): every group's config inputs, built
        /// once per group as part of the single Group model - one owner per vendor for status, check
        /// rows, AND config, not two places. Returns an empty list for groups with nothing to configure
        /// (GameAnalytics/Facebook/Firebase - GA keys stay on GA Settings, no duplicated
        /// inputs here). Inputs render in BOTH views unconditionally (the rendering contract: "Studio
        /// view: all inputs") - the view filter never touches this list, only check rows.</summary>
        List<VisualElement> BuildConfigInputsForGroup(GreenlightAdapter.VendorGroup group)
        {
            var inputs = new List<VisualElement>();

            if (_config == null || _serializedConfig == null)
            {
                if (group == GreenlightAdapter.VendorGroup.BuildAndProject)
                {
                    inputs.Add(new HelpBox("No config found. Click below to create one.", HelpBoxMessageType.Warning));
                    inputs.Add(new Button(() =>
                    {
                        CreateConfig();
                        _serializedConfig = new SerializedObject(_config);
                        RefreshGreenlightUI();
                    })
                    { text = "Create Configuration Asset" });
                }
                return inputs;
            }

            switch (group)
            {
                // MAX Ad Units. Arthur's follow-up: the struct-level revert (plain PropertyField for
                // the whole PlatformAdUnitId) was the right call against double-labeling, but the
                // design intent still applies ONE LEVEL DOWN - each Android/iOS string is a real leaf
                // property, so it gets ValidatedField same as Adjust's fields, inside a manually-built
                // Foldout that keeps the exact same expand/collapse structure PropertyField used to
                // give us for free.
                case GreenlightAdapter.VendorGroup.AppLovinMax:
                    if (SdkDetector.IsInstalled(SdkId.AppLovinMAX))
                    {
                        inputs.Add(BuildAdUnitFoldout("Rewarded", "rewardedAdUnit"));
                        inputs.Add(BuildAdUnitFoldout("Interstitial", "interstitialAdUnit"));
                        // "(optional)" casing matches every other optional-vendor label in this window
                        // (F13.3, 2026-07-21 audit) - this was the one outlier capitalizing it.
                        inputs.Add(BuildAdUnitFoldout("Banner (optional)", "bannerAdUnit"));
                    }
                    break;

                // Adjust (full mode only). adjustAppToken is the ONE documented hard build gate
                // (BuildValidationVendorSettings.cs / SdkConfigDetector.cs: empty or length<=5 fails a
                // Full-mode build) - Invalid state + subtext only while unresolved, no subtext once
                // valid. ValidatedField.CreateBound uses a plain bound TextField, not PropertyField, so
                // no [Header]-driven decorator applies here (see ValidatedField.cs for why); the group's
                // own vendor header already names the vendor, so no redundant sub-header text.
                case GreenlightAdapter.VendorGroup.Adjust:
                    if (!SorollaSettings.IsPrototype && SdkDetector.IsInstalled(SdkId.Adjust))
                    {
                        var appToken = ValidatedField.CreateBound(_serializedConfig.FindProperty("adjustAppToken"), "App Token", value =>
                        {
                            bool valid = !string.IsNullOrEmpty(value) && value.Length > 5;
                            return valid
                                ? (ValidatedField.State.Valid, (string)null)
                                : (ValidatedField.State.Invalid, "Required for Full-mode builds");
                        });
                        inputs.Add(appToken);
                        _adjustAppTokenField = appToken;
                        inputs.Add(BoundField("adjustPurchaseEventToken", "Purchase Event Token"));
                    }
                    else
                    {
                        _adjustAppTokenField = null;
                    }
                    break;

                // Verbose Logging is a QA/debug knob, so it lives under Device & QA, not Build & Project
                // (Arthur ruling 2026-07-21 ~17:40 - a "Build & Project" group whose only studio-visible
                // content was this toggle read as noise; now the group only renders when a build check
                // actually needs attention). Plain bound Toggle, not PropertyField (round-4 refuter
                // follow-up, 2026-07-21): PropertyField would auto-render verboseLogging's own
                // [Header("Logging")] attribute as a standalone-looking "Logging" header line above the
                // checkbox - the same redundant-header problem already avoided for MAX/Adjust's inputs.
                case GreenlightAdapter.VendorGroup.DeviceAndQa:
                {
                    SerializedProperty verboseLoggingProp = _serializedConfig.FindProperty("verboseLogging");
                    var verboseLoggingToggle = new Toggle("Verbose Logging") { value = verboseLoggingProp.boolValue };
                    verboseLoggingToggle.tooltip = "Enable verbose debug output for all vendor SDKs (MAX, Adjust, TikTok). Forced OFF in release builds.";
                    verboseLoggingToggle.RegisterValueChangedCallback(evt =>
                    {
                        verboseLoggingProp.boolValue = evt.newValue;
                        _serializedConfig.ApplyModifiedProperties();
                        EditorUtility.SetDirty(_config);
                    });
                    inputs.Add(verboseLoggingToggle);
                    break;
                }

                // TikTok is a parked vendor (roadmap "Parking decisions" - no QA/diagnostics investment),
                // so it has no check rows/BuildValidator category - its Group carries only these inputs.
                // Plain unbound Toggle, not a bound PropertyField: a PropertyField's ValueChangeCallback
                // also fires once during Bind()'s own initial sync, and refreshing the whole container
                // from inside that sync recurses (a fresh PropertyField created by the refresh gets bound
                // again, fires again...) - Unity's own recursion guard caught this at ~490 deep during
                // verification.
                case GreenlightAdapter.VendorGroup.TikTok:
                    var tiktokToggle = new Toggle("Enabled") { value = _config.enableTikTok };
                    tiktokToggle.RegisterValueChangedCallback(evt =>
                    {
                        _config.enableTikTok = evt.newValue;
                        EditorUtility.SetDirty(_config);
                        RefreshGreenlightUI(); // fields show/hide with this same flag
                    });
                    inputs.Add(tiktokToggle);

                    if (_config.enableTikTok)
                    {
                        var tiktokAppIdField = new PropertyField(_serializedConfig.FindProperty("tiktokAppId"), "TikTok App ID");
                        inputs.Add(tiktokAppIdField);
                        _tiktokAppIdField = tiktokAppIdField;
                        inputs.Add(new PropertyField(_serializedConfig.FindProperty("tiktokEmAppId"), "App ID (EM)"));
                        inputs.Add(new PropertyField(_serializedConfig.FindProperty("tiktokAccessToken"), "Access Token"));
                    }
                    else
                    {
                        _tiktokAppIdField = null;
                    }
                    break;
            }

            return inputs;
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

        #region Group model (rewrite cycle, Arthur ruling 2026-07-21 ~16:45)

        // ── Rendering contract this region implements ──
        // One Group list (vendors + Build & Project + Device & QA + TikTok). One pure view filter, no
        // per-group exceptions. Header glyphs/counts derived AFTER filtering, from the same list the
        // view renders. One shared row element. One shared button style. See the consolidated contract
        // in sorolla-docs/platform/sdk/research/editor-window-simplification-2026-07-21.md, "Rewrite
        // cycle" section - it is the only licensed behavior; nothing here should reintroduce a
        // per-group special case in the VISIBILITY decision (RowVisible / group-has-children below).

        /// <summary>A vendor's independent config-validity signal (installed? configured?), separate
        /// from its check rows. <see cref="Phase"/> covers the whole lifecycle explicitly - the two
        /// install phases plus the Fail/Warn/Pass config scale - instead of the old nullable-severity +
        /// special-glyph split. Build & Project / Device & QA / TikTok have no such signal (null
        /// OwnState on their GroupModel) - their header is derived purely from their visible rows.</summary>
        sealed class OwnVendorState
        {
            public enum Phase
            {
                Installing,
                NotInstalled,
                Fail,
                Warn,
                Pass,
            }

            public Phase State;
            public string Text;
            public bool Optional;
            public string ActionLabel;
            public Action Action;
            public bool ActionEnabled = true;
        }

        /// <summary>One Group, per the rendering contract: title, own vendor-validity state (null for
        /// the three non-vendor groups), every check row the catalog ever routed here (view filter
        /// applies later, at render time), and every config input (rendered unconditionally in both
        /// views).</summary>
        sealed class GroupModel
        {
            public GreenlightAdapter.VendorGroup Id;
            public string Title;
            public List<GreenlightEvaluator.Row> Rows = new List<GreenlightEvaluator.Row>();
            public List<VisualElement> Inputs = new List<VisualElement>();
            public OwnVendorState OwnState;
        }

        /// <summary>Header tag for a group's effective status - the greenlight's own vocabulary
        /// (GREEN / INCOMPLETE / WARN / ERROR), rendered as a right-aligned StatusBadge pill.</summary>
        static (string text, StatusBadge.Severity severity) BadgeFor(CheckRow.Status status) => status switch
        {
            CheckRow.Status.Fail => ("ERROR", StatusBadge.Severity.Fail),
            CheckRow.Status.Warn => ("WARN", StatusBadge.Severity.Advisory),
            CheckRow.Status.Wait => ("INCOMPLETE", StatusBadge.Severity.Wait),
            _ => ("GREEN", StatusBadge.Severity.Pass), // Pass and Info both read as clean at the group level
        };

        /// <summary>Worst status among a set of rows (Fail &gt; Warn &gt; Wait &gt; Pass/Info) - the ONE
        /// place this is computed, fed by whatever the caller already filtered to be visible. Computing
        /// this from a pre-filtered list (not a separate side-channel query) is what keeps a header from
        /// ever contradicting what's actually rendered below it, in either view.</summary>
        static CheckRow.Status WorstOfRows(IEnumerable<GreenlightEvaluator.Row> rows)
        {
            CheckRow.Status worst = CheckRow.Status.Pass;
            foreach (GreenlightEvaluator.Row r in rows)
            {
                if (r.Status == CheckRow.Status.Fail) return CheckRow.Status.Fail; // can't get worse
                if (r.Status == CheckRow.Status.Warn) worst = CheckRow.Status.Warn;
                else if (r.Status == CheckRow.Status.Wait && worst != CheckRow.Status.Warn) worst = CheckRow.Status.Wait;
            }
            return worst;
        }

        /// <summary>Own-validity state for the three vendors sharing the install/configure/edit shape
        /// (Facebook, Adjust, and any future vendor whose only signal is "installed? configured?").
        /// GameAnalytics/MAX/Firebase have their own richer methods below because their "configured"
        /// check isn't a single ConfigStatus (MAX: settings-sync; Firebase: multi-file; GA: per-platform
        /// keys) - same shape, different validity predicate.</summary>
        OwnVendorState GenericVendorOwnState(SdkInfo sdk, SdkConfigDetector.ConfigStatus configStatus,
            string configHint, Action openSettings, bool isRequired, string configuredDetail = null)
        {
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isAutoInstalled = sdk.IsRequiredFor(isPrototype);
            bool isInstalled = SdkDetector.IsInstalled(sdk);
            bool isInstalling = _installingPackages.Contains(sdk.PackageId);

            if (isInstalling)
                return new OwnVendorState { State = OwnVendorState.Phase.Installing, Text = "Installing..." };

            if (!isInstalled)
            {
                var state = new OwnVendorState
                {
                    State = isRequired ? OwnVendorState.Phase.Fail : OwnVendorState.Phase.NotInstalled,
                    Text = isAutoInstalled ? "Auto-installs on mode switch" : null,
                    Optional = !isRequired,
                };
                if (!isAutoInstalled)
                {
                    state.ActionLabel = "Install";
                    state.Action = () => SdkInstaller.Install(sdk.Id);
                    state.ActionEnabled = !EditorApplication.isPlaying;
                }
                return state;
            }

            if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
                return new OwnVendorState
                {
                    State = OwnVendorState.Phase.Pass,
                    Text = configuredDetail ?? "Configured",
                    Optional = !isRequired,
                    ActionLabel = openSettings != null ? "Edit" : null,
                    Action = openSettings,
                };

            return new OwnVendorState
            {
                State = OwnVendorState.Phase.Warn,
                Text = configHint,
                Optional = !isRequired,
                ActionLabel = openSettings != null ? "Configure" : null,
                Action = openSettings,
            };
        }

        /// <summary>GameAnalytics' own signal is per-platform key status only - the Resource Whitelist
        /// check (a second thing Build Health validates for this vendor) is a normal check ROW routed
        /// into this same group by the catalog, so it escalates the header via the shared worst-of merge
        /// in <see cref="BuildGroupSection"/> exactly like any other vendor's rows - no bespoke
        /// "whitelistWarn" side-channel needed once rows drive the merge generically.</summary>
        OwnVendorState GameAnalyticsOwnState()
        {
            SdkInfo sdk = SdkRegistry.All[SdkId.GameAnalytics];
            bool isInstalled = SdkDetector.IsInstalled(sdk);
            bool isInstalling = _installingPackages.Contains(sdk.PackageId);
            string keyDetail = SdkConfigDetector.GetGameAnalyticsPlatformDetail();

            if (isInstalling)
                return new OwnVendorState { State = OwnVendorState.Phase.Installing, Text = "Installing..." };
            if (!isInstalled)
                return new OwnVendorState { State = OwnVendorState.Phase.Fail };

            // Active platform missing = Fail (the build in front of you won't report). Only the sibling
            // platform missing = Warn: still shippable today, but games ship both platforms, so it
            // surfaces instead of hiding behind the active target (Arthur ruling 2026-07-21 ~17:40).
            SdkConfigDetector.ConfigStatus keyStatus = SdkConfigDetector.GetGameAnalyticsStatus();
            OwnVendorState.Phase phase = keyStatus != SdkConfigDetector.ConfigStatus.Configured
                ? OwnVendorState.Phase.Fail
                : SdkConfigDetector.HasGameAnalyticsKeysForOtherPlatform() ? OwnVendorState.Phase.Pass : OwnVendorState.Phase.Warn;

            return new OwnVendorState
            {
                State = phase, Text = keyDetail,
                ActionLabel = "Edit", Action = SdkConfigDetector.OpenGameAnalyticsSettings,
            };
        }

        OwnVendorState MaxOwnState()
        {
            SdkInfo sdk = SdkRegistry.All[SdkId.AppLovinMAX];
            bool isRequired = !SorollaSettings.IsPrototype;
            bool isAutoInstalled = sdk.IsRequiredFor(SorollaSettings.IsPrototype);
            bool isInstalled = SdkDetector.IsInstalled(sdk);
            bool isInstalling = _installingPackages.Contains(sdk.PackageId);

            if (isInstalling)
                return new OwnVendorState { State = OwnVendorState.Phase.Installing, Text = "Installing..." };

            if (!isInstalled)
            {
                var state = new OwnVendorState
                {
                    State = isRequired ? OwnVendorState.Phase.Fail : OwnVendorState.Phase.NotInstalled,
                    Text = isAutoInstalled ? "Auto-installs on mode switch" : null,
                    Optional = !isRequired,
                };
                if (!isAutoInstalled)
                {
                    state.ActionLabel = "Install";
                    state.Action = () => SdkInstaller.Install(sdk.Id);
                    state.ActionEnabled = !EditorApplication.isPlaying;
                }
                return state;
            }

            MaxSettingsSanitizer.SyncEmbeddedSdkKey();
            if (MaxSettingsSanitizer.IsSdkKeyConfigured())
                return new OwnVendorState { State = OwnVendorState.Phase.Pass, Text = "Auto-synced" };
            return new OwnVendorState
            {
                State = OwnVendorState.Phase.Fail, Text = "Auto-sync failed",
                ActionLabel = "Refresh", Action = RunBuildValidation,
            };
        }

        OwnVendorState FirebaseOwnState()
        {
            bool isInstalled = SdkDetector.IsInstalled(SdkId.FirebaseAnalytics);
            bool isPrototype = SorollaSettings.IsPrototype;
            bool isRequired = !isPrototype;
            SdkConfigDetector.ConfigStatus configStatus = SdkConfigDetector.GetFirebaseStatus(_config);
            bool isInstalling = _installingPackages.Contains("com.google.firebase.app") ||
                                _installingPackages.Contains("com.google.firebase.analytics") ||
                                _installingPackages.Contains("com.google.firebase.crashlytics") ||
                                _installingPackages.Contains("com.google.firebase.remote-config");

            if (isInstalling)
                return new OwnVendorState { State = OwnVendorState.Phase.Installing, Text = "Installing..." };

            if (!isInstalled)
            {
                var state = isRequired
                    ? new OwnVendorState { State = OwnVendorState.Phase.Fail, Text = "Auto-installs on mode switch" }
                    : new OwnVendorState { State = OwnVendorState.Phase.NotInstalled, Optional = true };
                if (!isRequired) // isPrototype - Full mode auto-installs Firebase, no manual Install button
                {
                    state.ActionLabel = "Install";
                    state.ActionEnabled = !EditorApplication.isPlaying;
                    state.Action = () =>
                    {
                        SdkInstaller.Install(SdkId.FirebaseApp);
                        SdkInstaller.Install(SdkId.FirebaseAnalytics);
                        SdkInstaller.Install(SdkId.FirebaseCrashlytics);
                        SdkInstaller.Install(SdkId.FirebaseRemoteConfig);
                    };
                }
                return state;
            }

            if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
                return new OwnVendorState
                {
                    State = OwnVendorState.Phase.Pass, Text = "Configured", Optional = !isRequired,
                    ActionLabel = "Console", Action = () => Application.OpenURL("https://console.firebase.google.com/"),
                };
            return new OwnVendorState
            {
                State = OwnVendorState.Phase.Warn, Text = "Add config files", Optional = !isRequired,
                ActionLabel = "Console", Action = () => Application.OpenURL("https://console.firebase.google.com/"),
            };
        }

        /// <summary>Dispatches to each vendor's own-validity computation; null for the three groups with
        /// no such signal (Build & Project, Device & QA, TikTok) - their header derives purely from
        /// their visible rows in <see cref="BuildGroupSection"/>.</summary>
        OwnVendorState OwnStateFor(GreenlightAdapter.VendorGroup id) => id switch
        {
            GreenlightAdapter.VendorGroup.GameAnalytics => GameAnalyticsOwnState(),
            GreenlightAdapter.VendorGroup.Facebook => GenericVendorOwnState(
                SdkRegistry.All[SdkId.Facebook], SdkConfigDetector.GetFacebookStatus(), "Set App ID",
                SdkConfigDetector.OpenFacebookSettings, isRequired: true),
            GreenlightAdapter.VendorGroup.Firebase => FirebaseOwnState(),
            GreenlightAdapter.VendorGroup.AppLovinMax => MaxOwnState(),
            GreenlightAdapter.VendorGroup.Adjust => GenericVendorOwnState(
                SdkRegistry.All[SdkId.Adjust], SdkConfigDetector.GetAdjustStatus(_config), "Enter app token below",
                () => FocusConfigField(_adjustAppTokenField), isRequired: !SorollaSettings.IsPrototype),
            _ => null,
        };

        /// <summary>Builds the one Group list from evaluator rows + config state - the single data model
        /// every render pass (both views) reads from. Grouping key is the gate's existing catalog
        /// category (GreenlightAdapter.GroupFor), never label string-matching.</summary>
        List<GroupModel> BuildGroups(GreenlightEvaluator.Report report)
        {
            var grouped = new Dictionary<GreenlightAdapter.VendorGroup, List<GreenlightEvaluator.Row>>();
            foreach (GreenlightEvaluator.Row row in report.Rows)
            {
                GreenlightAdapter.VendorGroup id = GreenlightAdapter.GroupFor(row.GateId);
                if (!grouped.TryGetValue(id, out List<GreenlightEvaluator.Row> list))
                    grouped[id] = list = new List<GreenlightEvaluator.Row>();
                list.Add(row);
            }

            var groups = new List<GroupModel>();
            foreach (GreenlightAdapter.VendorGroup id in GroupOrder)
            {
                grouped.TryGetValue(id, out List<GreenlightEvaluator.Row> rows);
                groups.Add(new GroupModel
                {
                    Id = id,
                    Title = GroupTitle(id),
                    Rows = rows ?? new List<GreenlightEvaluator.Row>(),
                    Inputs = BuildConfigInputsForGroup(id),
                    OwnState = OwnStateFor(id),
                });
            }
            return groups;
        }

        /// <summary>Build checks a studio should see even when green (Arthur ruling 2026-07-21 ~17:55:
        /// "keep the important ones") - the facts a studio actually acts on or asks about: is every SDK
        /// present at the right version in the right mode, and is the Android build config sane. The
        /// internal-hygiene rest (registries, config sync, EDM4U, gradle java home, addressables, pin)
        /// stays internal-only when passing.</summary>
        static readonly HashSet<string> StudioVisibleWhenPass = new HashSet<string>
        {
            GateIds.BuildRequiredSdks,
            GateIds.BuildSdkVersions,
            GateIds.BuildModeConsistency,
            GateIds.BuildAndroidManifest,
            GateIds.BuildGradleConfig,
            GateIds.BuildDevelopmentBuild,
        };

        /// <summary>The ONE pure view filter (rendering contract): internal view shows every row;
        /// studio shows every non-pass/non-info row that isn't Sorolla-QA-only attestation content
        /// (except the one genuinely studio-actionable pin issue, Disposition.Omitted), plus the
        /// <see cref="StudioVisibleWhenPass"/> whitelist of important build facts shown even when
        /// green. A group's visibility is a pure function of "does it have >= 1 row this returns
        /// true for, or >= 1 input" (inputs always visible, checked separately by the caller).</summary>
        static bool RowVisible(GreenlightEvaluator.Row row, bool internalView)
        {
            if (internalView) return true;
            if (row.Status == CheckRow.Status.Pass || row.Status == CheckRow.Status.Info)
                return StudioVisibleWhenPass.Contains(row.GateId);
            bool isManual = GreenlightManualChecklist.DescriptorForLabel(row.Label) != null;
            bool isStudioActionablePinIssue = row.Disposition == GateDisposition.Omitted;
            return !isManual || isStudioActionablePinIssue;
        }

        /// <summary>One group section in the shared section-header style (caps title + rule, like Quick
        /// Start): foldout arrow + title + rule + status detail + optional action button + status tag,
        /// above a collapsible rows container. Effective status = own vendor state, escalated (never
        /// downgraded) by the worst VISIBLE row - computed from the same list rendered below, so the tag
        /// can never contradict its children. Only the arrow toggles folding, so the header's
        /// Edit/Console/Install button stays a plain click; the manual fold state persists per
        /// <paramref name="persistKey"/> for the window session, and the attention-based default only
        /// applies the first time a key is seen.</summary>
        VisualElement BuildGroupSection(string persistKey, GroupModel g, List<GreenlightEvaluator.Row> visibleRows, List<VisualElement> rowElements)
        {
            string title = g.Title;
            string detail;
            string badgeText;
            StatusBadge.Severity badgeSeverity;
            string actionLabel = null;
            Action action = null;
            bool actionEnabled = true;

            CheckRow.Status rowsWorst = WorstOfRows(visibleRows);
            if (g.OwnState != null)
            {
                OwnVendorState own = g.OwnState;
                detail = own.Text;
                actionLabel = own.ActionLabel;
                action = own.Action;
                actionEnabled = own.ActionEnabled;
                if (own.Optional) title = $"{title} (optional)";

                (badgeText, badgeSeverity) = own.State switch
                {
                    OwnVendorState.Phase.Installing => ("INSTALLING", StatusBadge.Severity.Wait),
                    OwnVendorState.Phase.NotInstalled => ("NOT INSTALLED", StatusBadge.Severity.Gated),
                    OwnVendorState.Phase.Fail => BadgeFor(CheckRow.Status.Fail),
                    OwnVendorState.Phase.Warn => BadgeFor(CheckRow.Status.Warn),
                    _ => BadgeFor(rowsWorst), // Pass: visible rows may escalate, never downgrade
                };
            }
            else
            {
                (badgeText, badgeSeverity) = BadgeFor(rowsWorst);
                int issues = visibleRows.Count(r => r.Status != CheckRow.Status.Pass && r.Status != CheckRow.Status.Info);
                // A lone item doesn't need a number (round-3 ruling) - "1 need attention" reads oddly.
                detail = issues == 0 ? null : issues == 1 ? "Needs attention" : $"{issues} need attention";
            }

            bool attention = badgeSeverity != StatusBadge.Severity.Pass && badgeSeverity != StatusBadge.Severity.Gated;
            bool expanded = _groupExpanded.TryGetValue(persistKey, out bool remembered) ? remembered : attention;

            var container = new VisualElement();
            container.style.marginBottom = 8;

            var headerRow = new VisualElement();
            headerRow.AddToClassList("sorolla-section-header");

            var arrow = new Label(expanded ? "▾" : "▸");
            arrow.style.width = 16;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.pickingMode = PickingMode.Position;
            headerRow.Add(arrow);

            var titleLabel = new Label(title.ToUpperInvariant());
            titleLabel.AddToClassList("sorolla-section-header-label");
            headerRow.Add(titleLabel);

            var rule = new VisualElement();
            rule.AddToClassList("sorolla-section-header-rule");
            headerRow.Add(rule);

            if (!string.IsNullOrEmpty(detail))
            {
                var detailLabel = new Label(detail);
                detailLabel.AddToClassList("sorolla-type-small");
                detailLabel.style.marginRight = 6;
                headerRow.Add(detailLabel);
            }

            if (!string.IsNullOrEmpty(actionLabel))
            {
                var button = new Button(() => action?.Invoke()) { text = actionLabel };
                button.AddToClassList("sorolla-button-small");
                button.style.marginRight = 6;
                button.SetEnabled(actionEnabled);
                headerRow.Add(button);
            }

            headerRow.Add(StatusBadge.Create(badgeText, badgeSeverity));
            container.Add(headerRow);

            var rowsWrap = new VisualElement();
            rowsWrap.style.marginLeft = 20;
            rowsWrap.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (VisualElement row in rowElements)
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

        #endregion

        #region Greenlight

        /// <summary>The window-wide actions (Refresh / Connect Device / Copy Report, plus the
        /// internal-only JSON + gate-catalog exports), fixed in the header below the hero (Arthur
        /// ruling 2026-07-21 ~17:50) - one home for global actions, no in-content duplicates.
        /// Repopulated on every greenlight refresh so the Connect button's connecting/enabled state
        /// tracks the snapshot phase.</summary>
        void RefreshHeaderActions(GreenlightEvaluator.Report report)
        {
            if (_headerActionsHost == null) return;
            _headerActionsHost.Clear();

            var container = CreatePortedSectionContainer();
            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.flexWrap = Wrap.Wrap;

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
            // disposition/requirement/proof + a build fingerprint, so a pasted result is unambiguous.
            // "(text)" only disambiguates against the JSON sibling that exists in the internal view.
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

            container.Add(actionsRow);
            _headerActionsHost.Add(container);
        }

        /// <summary>Rebuilds the Greenlight section: verdict badge + count strip, grouped rows, and
        /// the fixed header actions (see <see cref="RefreshHeaderActions"/>). Clear-and-rebuild on
        /// data change only - same guarded pattern as the other ported sections (RunBuildValidation's
        /// completion, the manual checklist toggles, and the device snapshot's onSettled
        /// callback).</summary>
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

            RefreshHeaderActions(report);

            // Privacy banner REMOVED entirely, both surfaces, no copy-time replacement (F13.5 ruling,
            // 2026-07-21 ~12:30) - it was internal QA vocabulary ("tester names", "evidence notes") on a
            // studio surface where attestations don't exist, and a permanent fixture above every
            // actionable row on both surfaces either way.

            if (ShowInternalDepth)
            {
                // Build Health's unique bits folded in here (F12 ruling, 2026-07-21 ~12:30): only the
                // profile selector and the AUTO-FIXED log are unique to Build Health now; its row list and
                // callout are gone, folded into the Greenlight groups below.
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
            }

            // One Group list, one pure filter, one render loop for both views (rendering contract). A
            // group renders iff it has >= 1 visible row or >= 1 input - nothing else renders a group, no
            // "All clear"-only lines, no count-only headers with nothing beneath them.
            List<GroupModel> groups = BuildGroups(report);
            bool anyOpen = false;
            foreach (GroupModel g in groups)
            {
                List<GreenlightEvaluator.Row> visibleRows = g.Rows.Where(r => RowVisible(r, ShowInternalDepth)).ToList();
                if (visibleRows.Count == 0 && g.Inputs.Count == 0) continue;
                anyOpen = true;

                var rowElements = new List<VisualElement>();
                foreach (GreenlightEvaluator.Row row in visibleRows)
                {
                    rowElements.Add(BuildGreenlightRow(row, includeAttestation: ShowInternalDepth, g.Id));

                    // Firebase sub-rows, active target only, internal-only (F7 fix, carried over from the
                    // deleted Build Health list): attached right under the Firebase Coherence gate row.
                    if (ShowInternalDepth && row.GateId == GateIds.BuildFirebaseCoherence && SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
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
                rowElements.AddRange(g.Inputs);

                // Fold-state key is just the group id: internal and studio are separate window
                // instances now, each with its own _groupExpanded dictionary.
                _greenlightContainer.Add(BuildGroupSection(g.Id.ToString(), g, visibleRows, rowElements));
            }

            if (!ShowInternalDepth && !anyOpen)
            {
                // F1 ruling (2026-07-21 ~12:30): NOT a semantics change - INCOMPLETE-while-clean is
                // correct order-of-operations (Sorolla QA precedes release), so the empty state now
                // explains that instead of asserting "nothing outstanding" right next to a badge that
                // still reads INCOMPLETE.
                var clear = new Label("Your setup is clean - remaining checks are Sorolla's QA before release.");
                clear.AddToClassList("sorolla-type-small");
                _greenlightContainer.Add(clear);
            }

            BindConfigInputs();
        }

        /// <summary>Binds every config input added to _greenlightContainer this refresh (MAX ad units,
        /// Adjust tokens, TikTok fields, Verbose Logging) and re-wires the MAX auto-sync side effect.
        /// Called once per RefreshGreenlightUI (both views), after every group has been added.</summary>
        void BindConfigInputs()
        {
            if (_serializedConfig == null) return;

            _greenlightContainer.Bind(_serializedConfig);

            if (SdkDetector.IsInstalled(SdkId.AppLovinMAX))
                _greenlightContainer.TrackSerializedObjectValue(_serializedConfig, _ => MaxSettingsSanitizer.SyncEmbeddedSdkKey());
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
            GreenlightAdapter.VendorGroup.TikTok,
        };

        /// <summary>Child rows inside a vendor group drop the redundant vendor name from their own label
        /// - "GameAnalytics Platform Keys" reads as "Platform Keys" once it's already indented under a
        /// "GameAnalytics" header (grouping-review ruling, 2026-07-21). Display-only: the gate id, the
        /// LabelFor lookup, and the copied/exported report all keep the full name - this only changes
        /// the text a CheckRow shows.</summary>
        static string TrimGroupPrefix(string label, GreenlightAdapter.VendorGroup? group)
        {
            string prefix = group switch
            {
                GreenlightAdapter.VendorGroup.GameAnalytics => "GameAnalytics ",
                GreenlightAdapter.VendorGroup.Facebook => "Facebook ",
                GreenlightAdapter.VendorGroup.Firebase => "Firebase ",
                GreenlightAdapter.VendorGroup.AppLovinMax => "MAX ",
                GreenlightAdapter.VendorGroup.Adjust => "Adjust ",
                _ => null,
            };
            return !string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(label) && label.StartsWith(prefix)
                ? label.Substring(prefix.Length)
                : label;
        }

        static string GroupTitle(GreenlightAdapter.VendorGroup group) => group switch
        {
            GreenlightAdapter.VendorGroup.GameAnalytics => "GameAnalytics",
            GreenlightAdapter.VendorGroup.Facebook => "Facebook",
            GreenlightAdapter.VendorGroup.Firebase => "Firebase",
            GreenlightAdapter.VendorGroup.AppLovinMax => "AppLovin MAX",
            GreenlightAdapter.VendorGroup.Adjust => "Adjust",
            GreenlightAdapter.VendorGroup.BuildAndProject => "Build & Project",
            GreenlightAdapter.VendorGroup.DeviceAndQa => "Device & QA",
            GreenlightAdapter.VendorGroup.TikTok => "TikTok (optional)",
            _ => group.ToString(),
        };

        /// <summary>A CheckRow plus its Fix/deep-link line and, for manual checklist rows in the
        /// internal harness, a Verified toggle - manual/dashboard rows never render as a bare
        /// unchecked box (brief requirement: always carry fix text + deep link). Studio callers pass
        /// includeAttestation: false since attestation checklist rows never reach the studio render
        /// (ruling 2 - they're Sorolla QA process, not a studio row at all).</summary>
        VisualElement BuildGreenlightRow(GreenlightEvaluator.Row row, bool includeAttestation, GreenlightAdapter.VendorGroup? group = null)
        {
            var container = new VisualElement();

            // Looked up once, up front: used below both to decide how this row's detail text lays out
            // (F10) and, further down, to render the Attest button (unchanged behavior, just no longer a
            // second lookup of the same descriptor). Keyed on the FULL label, never the trimmed display
            // text below - manual descriptor lookup, gate ids, and the exported report are unaffected by
            // the vendor-prefix trim (grouping-review ruling, 2026-07-21: display-only).
            GreenlightManualChecklist.Descriptor manual = GreenlightManualChecklist.DescriptorForLabel(row.Label);

            // Manual gates carry a multi-sentence "why this matters" paragraph as Detail, which CheckRow
            // puts in its bold, right-aligned, single-line status slot - ragged 5-7 line blue columns
            // colliding with neighbor rows (F10, 2026-07-21 audit). The status slot gets a short state
            // word instead; the full paragraph renders full-width under the label, same as Fix text.
            // Device Snapshot gets the identical treatment (round-3 refuter follow-up, 2026-07-21): it
            // isn't a manual-checklist descriptor, but it carries the same kind of descriptive-sentence
            // Detail and was the only Wait row in Device & QA rendering that sentence in place of the
            // WAIT tag instead of alongside it - inconsistent with its four sibling rows.
            bool longDetail = (manual != null || row.GateId == GateIds.DeviceNoSdkErrors) && row.Status != CheckRow.Status.Pass;
            // Studio humanizes the short state word (acceptance pass, 2026-07-21): internal view is a QA
            // tool and can show the raw enum name (WAIT), but studio's every other surface already speaks
            // plain English ("Needs attention", "Awaiting attestation") - includeAttestation doubles as
            // "this is the internal view" at this method's one call site.
            string statusWord = row.Status == CheckRow.Status.Wait && !includeAttestation
                ? "Pending"
                : row.Status.ToString().ToUpperInvariant();
            string statusSlotText = longDetail ? statusWord : row.Detail;
            container.Add(CheckRow.Create(TrimGroupPrefix(row.Label, group), row.Status, statusSlotText));

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
            // ruled defects. The GA credential probe's platform-registration reminder used to be the one
            // deliberate exception here - it moved off this row (refuter follow-up, 2026-07-21) since it
            // now duplicates the GameAnalytics Platform Registered attestation row in the same group.
            // Info rows (deliberate skip/absence, F5 residual) get the same treatment as Pass - no Fix
            // text and no action button, since a skip is not a caveat to resolve.
            bool isPass = row.Status == CheckRow.Status.Pass || row.Status == CheckRow.Status.Info;

            if (!string.IsNullOrEmpty(row.Fix) && !isPass)
            {
                var fixLabel = new Label($"Fix: {row.Fix}");
                fixLabel.AddToClassList("sorolla-type-small");
                fixLabel.style.marginLeft = 24;
                fixLabel.style.whiteSpace = WhiteSpace.Normal;
                container.Add(fixLabel);
            }

            if (!isPass && !string.IsNullOrEmpty(row.DeepLinkUrl))
            {
                // Bordered button, not link-style text (refuter follow-up, 2026-07-21): same defect class
                // as the Open GA Settings fix - a row action reads as an action, not prose, regardless of
                // whether it opens an in-editor asset or an external dashboard.
                var linkButton = new Button(() => Application.OpenURL(row.DeepLinkUrl)) { text = row.DeepLinkLabel ?? "Open" };
                linkButton.AddToClassList("sorolla-button-small");
                linkButton.style.marginLeft = 24;
                container.Add(linkButton);
            }

            // No per-row "Open GA/FB Settings" buttons: since the vendor foldouts, every row that had
            // one sits under a group header whose Edit button performs the identical action (Arthur
            // ruling 2026-07-21 ~17:40 - the duplicate affordance was noise).

            // The device snapshot row's remedy is the Connect Device button fixed in the window header
            // (Arthur ruling 2026-07-21 ~17:50: one home, no per-row duplicate button). The row still
            // surfaces the failure reason - Connect attempts used to fail completely silently (F3,
            // 2026-07-21): every failure path writes State.DetailMessage but nothing rendered it.
            if (row.GateId == GateIds.DeviceNoSdkErrors && row.Status != CheckRow.Status.Pass)
            {
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
                reportButton.AddToClassList("sorolla-button-small");
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
            RefreshGreenlightUI();
        }

        #endregion

    }
}
