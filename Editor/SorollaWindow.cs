using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Editor.UI;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     The Palette SDK window: ONE window, curated for studios.
    ///     This class is the shell only - lifecycle, layout composition, and the validation run. What the
    ///     window SHOWS lives in the view layer (<see cref="ReadinessSectionView"/>,
    ///     <see cref="ConfigInputsView"/>, <see cref="VendorStatusProbe"/>).
    ///     There is deliberately no second, deeper "internal" window. Sorolla's own SDK validation reads the
    ///     canonical Copy Report export, which renders every gate including the ones this window filters out
    ///     of view, so a second EditorWindow would only have been a second thing to keep in sync.
    /// </summary>
    public class SorollaWindow : EditorWindow
    {
        // Version from package.json (cached)
        static string s_version;
        static string Version => s_version ??= UnityEditor.PackageManager.PackageInfo
            .FindForAssembly(typeof(SorollaWindow).Assembly)?.version ?? "?.?.?";

        readonly List<string> _autoFixLog = new List<string>();
        readonly HashSet<string> _installingPackages = new HashSet<string>();
        SorollaConfig _config;
        SerializedObject _serializedConfig;
        ConfigInputsView _configInputs;
        ReadinessSectionView _readinessSection;
        VisualElement _heroContainer;
        ScrollView _scrollView;
        List<BuildValidator.ValidationResult> _validationResults = new List<BuildValidator.ValidationResult>();
        bool _revalidationQueued;
        int _validatorInputFingerprint;
        double _lastInputPoll;
        const double InputPollSeconds = 1.0;

        /// <summary>Opens full-window with no tab strip (matching the AppLovin Integration Manager's
        /// presentation) - a utility window, not a dockable one. ShowUtility() is what drops the tab chrome;
        /// the accepted trade-off is that the window is not dockable. Single open path for the whole SDK:
        /// focus the existing instance instead of ever spawning a second one.</summary>
        [MenuItem("Tools/Sorolla Palette SDK")]
        public static void ShowWindow()
        {
            SorollaWindow existing = Resources.FindObjectsOfTypeAll<SorollaWindow>().FirstOrDefault();
            if (existing != null)
            {
                existing.Focus();
                return;
            }

            var window = CreateInstance<SorollaWindow>();
            window.titleContent = new GUIContent("Sorolla Palette SDK");
            window.minSize = new Vector2(420, 380);
            window.position = new Rect(100, 100, 560, 800);
            window.ShowUtility();
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

        void OnEnable()
        {
            LoadOrCreateConfig();
            _validatorInputFingerprint = ValidatorInputs.Fingerprint();
            RunBuildValidation();
            Events.registeringPackages += OnPackagesRegistering;
            Events.registeredPackages += OnPackagesRegistered;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += PollValidatorInputs;
            FacebookPlatformValidator.OnProbeSettled += RunBuildValidation;
            GameAnalyticsCredentialValidator.OnProbeSettled += RunBuildValidation;
        }

        void OnDisable()
        {
            Events.registeringPackages -= OnPackagesRegistering;
            Events.registeredPackages -= OnPackagesRegistered;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= PollValidatorInputs;
            FacebookPlatformValidator.OnProbeSettled -= RunBuildValidation;
            GameAnalyticsCredentialValidator.OnProbeSettled -= RunBuildValidation;
        }

        /// <summary>
        ///     Re-validates when a validator input changes - build target, mode, or a file in the tracked
        ///     list. Polled rather than event-driven because Unity offers no reliable in-session
        ///     notification for the active build target. Throttled to once a second.
        /// </summary>
        void PollValidatorInputs()
        {
            if (EditorApplication.timeSinceStartup - _lastInputPoll < InputPollSeconds) return;
            _lastInputPoll = EditorApplication.timeSinceStartup;

            int fingerprint = ValidatorInputs.Fingerprint();
            if (fingerprint == _validatorInputFingerprint) return;

            _validatorInputFingerprint = fingerprint;
            RefreshHeroHeaderUI(); // the mode switch is one of the inputs
            ScheduleRevalidation();
        }

        void CreateGUI()
        {
            // Content padding is applied to the header (unscrolled) and to the ScrollView's
            // contentContainer specifically, NOT to rootVisualElement or the ScrollView itself:
            // contentContainer is the actual scrolled element, separate from the scrollbar chrome, so the
            // scrollbar stays flush to the window edge while the content gets inset.
            const float ContentPadding = 12f;

            _heroContainer = Padded(new VisualElement(), ContentPadding);
            rootVisualElement.Add(_heroContainer);
            RefreshHeroHeaderUI();

            // Global actions (Refresh / Copy Report) live here, fixed below the hero and
            // above the scrolled content - one home for window-wide actions, no in-content duplicates.
            VisualElement headerActionsHost = Padded(new VisualElement(), ContentPadding);
            rootVisualElement.Add(headerActionsHost);

            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            _scrollView.contentContainer.style.paddingLeft = ContentPadding;
            _scrollView.contentContainer.style.paddingRight = ContentPadding;
            rootVisualElement.Add(_scrollView);

            // The one surviving IMGUI island: the play-mode warning.
            _scrollView.Add(new IMGUIContainer(DrawPlayModeWarning));

            VisualElement readinessContainer = SorollaTheme.CreateSectionContainer();
            readinessContainer.style.marginTop = 10;
            _scrollView.Add(readinessContainer);

            _configInputs = new ConfigInputsView(_config, _serializedConfig);
            var vendorStatus = new VendorStatusProbe(_config, _installingPackages, _configInputs,
                FocusConfigField, RunBuildValidation);
            _readinessSection = new ReadinessSectionView(readinessContainer, headerActionsHost,
                _configInputs, vendorStatus,
                onRefresh: RunBuildValidation, onModeSwitch: RequestModeSwitch);
            RefreshReadinessUI();

            VisualElement quickstartContainer = SorollaTheme.CreateSectionContainer();
            quickstartContainer.style.marginBottom = 10;
            quickstartContainer.Add(QuickStartSection.Create());
            _scrollView.Add(quickstartContainer);

            VisualElement footerContainer = SorollaTheme.CreateSectionContainer();
            footerContainer.Add(FooterLinks.Create());
            _scrollView.Add(footerContainer);

            WatchConfigForRevalidation();
        }

        static VisualElement Padded(VisualElement element, float padding)
        {
            element.style.paddingLeft = padding;
            element.style.paddingRight = padding;
            element.style.paddingBottom = padding;
            return element;
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

        /// <summary>Rebuilds the hero header fresh so its segmented mode switch reflects the current mode
        /// after a switch - the switch fires SorollaSettings.SetMode synchronously, then this repaints it in
        /// place (the same rebuild-on-change pattern as every other section).</summary>
        void RefreshHeroHeaderUI()
        {
            if (_heroContainer == null) return;
            _heroContainer.Clear();

            VisualElement container = SorollaTheme.CreateSectionContainer();
            container.Add(HeroHeader.Create("Palette SDK", $"v{Version} - Plug & Play Publisher Stack",
                modeIsFull: !SorollaSettings.IsPrototype, onSwitchRequested: RequestModeSwitch,
                disabled: EditorApplication.isPlaying));
            _heroContainer.Add(container);
        }

        void RefreshReadinessUI() =>
            _readinessSection?.Refresh(GreenlightEvaluator.Evaluate(_validationResults), _autoFixLog);

        /// <summary>Confirmation dialog + SorollaSettings.SetMode + re-validate, unchanged from the original
        /// IMGUI Mode box - only its presentation moved into the hero header's segmented switch.</summary>
        void RequestModeSwitch()
        {
            if (EditorApplication.isPlaying) return;

            SorollaMode otherMode = SorollaSettings.IsPrototype ? SorollaMode.Full : SorollaMode.Prototype;
            if (EditorUtility.DisplayDialog($"Switch to {otherMode} Mode?",
                    "This will install/uninstall SDKs as needed.", "Switch", "Cancel"))
            {
                SorollaSettings.SetMode(otherMode);
                RefreshHeroHeaderUI();
                EditorApplication.delayCall += RunBuildValidation;
            }
        }

        /// <summary>Scrolls a config field into view and focuses it - the remedy for rows that say "enter it
        /// below" about a field further down this same window. No-op if the field is not currently rendered
        /// (for example, Adjust fields hidden in Prototype mode).</summary>
        void FocusConfigField(VisualElement field)
        {
            if (field == null || _scrollView == null) return;
            _scrollView.ScrollTo(field);
            field.Focus();
        }

        void LoadOrCreateConfig()
        {
            _config = SorollaSettings.GetOrCreateRuntimeConfig();
            if (_config != null)
                _serializedConfig = new SerializedObject(_config);
        }

        /// <summary>Re-runs validation whenever the config asset changes, so every check that reads the
        /// config (Adjust sandbox mode, verbose logging, tokens, ad units) updates as the studio edits it.
        /// Without this, editing a value left the row that grades it showing a snapshot taken when the window
        /// opened - stale in BOTH directions, including a green "sandbox off" row over a config with sandbox
        /// on. It replaces the per-control refresh callbacks those rows used to lack.
        /// Installed ONCE, on the never-cleared root: a tracker on the rebuilt readiness container would be
        /// re-installed by the very refresh it triggers, and any initial-sync fire would then loop forever.</summary>
        void WatchConfigForRevalidation()
        {
            if (_serializedConfig == null) return;
            rootVisualElement.TrackSerializedObjectValue(_serializedConfig, _ => ScheduleRevalidation());
        }

        /// <summary>Coalesces a burst of config edits into one validation pass on the next editor tick.
        /// RunBuildValidation also runs the auto-fixers, which are idempotent, so a pass triggered by a
        /// fixer's own write settles instead of ping-ponging.</summary>
        void ScheduleRevalidation()
        {
            if (_revalidationQueued) return;
            _revalidationQueued = true;
            EditorApplication.delayCall += () =>
            {
                _revalidationQueued = false;
                RunBuildValidation();
            };
        }

        /// <summary>Runs every auto-fix, then every check, then re-renders. The auto-fix pass is the ONLY
        /// place the SDK writes to the project; nothing in the view layer may mutate anything.
        /// Guards a destroyed window here rather than at each call site: three paths reach this through
        /// EditorApplication.delayCall (mode switch, package install, config edit) and the window can be
        /// closed before any of those ticks fire.</summary>
        void RunBuildValidation()
        {
            if (this == null) return;

            _autoFixLog.Clear();

            if (BuildValidator.SyncConfigState())
                _autoFixLog.Add("Synced editor mode from SorollaConfig");
            if (BuildValidator.ResolveRequiredPackages())
                _autoFixLog.Add("Resolving required SDK packages / registries");
            _autoFixLog.AddRange(BuildValidator.RunSafeAutoFixes());

            _validationResults = BuildValidator.RunAllChecks();
            Repaint();
            RefreshReadinessUI();
        }

        void OnPackagesRegistering(PackageRegistrationEventArgs args)
        {
            foreach (UnityEditor.PackageManager.PackageInfo package in args.added)
                _installingPackages.Add(package.name);
            Repaint();
            RefreshReadinessUI(); // vendor group headers show Installing...
        }

        void OnPackagesRegistered(PackageRegistrationEventArgs args)
        {
            foreach (UnityEditor.PackageManager.PackageInfo package in args.added)
                _installingPackages.Remove(package.name);

            // Re-run validation after packages are installed (no auto-install, just detect)
            EditorApplication.delayCall += RunBuildValidation;
            Repaint();
            RefreshReadinessUI();
        }

        /// <summary>A cleared/rebuilt VisualElement doesn't repaint itself, so play-mode entry/exit is an
        /// explicit rebuild trigger: it changes the enabled state of every Install button and of the hero's
        /// mode switch.</summary>
        void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            RefreshReadinessUI();
            RefreshHeroHeaderUI();
        }
    }
}
