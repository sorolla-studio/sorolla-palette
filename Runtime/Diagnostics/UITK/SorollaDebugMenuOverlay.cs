using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Code-created runtime debug menu: no prefab or scene setup. All facts come from the existing
    // diagnostics/snapshot pipeline, and all runtime UI objects are torn down when the menu closes.
    //
    // Studio surface (2026-07-20): ONE report pane, no tab bar. The full depth (Console, Actions) is
    // still in every build - it is unlocked by 5 taps on the SDK context line and persisted, because
    // hiding it behind a build flag would mean the thing we ship is not the thing we debug.
    internal sealed partial class SorollaDebugMenuOverlay : MonoBehaviour
    {
        const string HostName = "[Palette SDK Debug Menu]";
        const string ThemeResourcePath = "SorollaDebugMenuTheme";
        const string UssResourcePath = "SorollaDebugMenuRuntime";
        const string InternalModePrefKey = "sorolla.vitals.internal";
        const int PanelSortingOrder = 32000;
        const float LiveRefreshIntervalSeconds = 0.2f;

        // The report pane is a full tree rebuild, so it is checked once a second and rebuilt only when
        // the underlying facts moved (mobile perf baseline: never a per-tick rebuild).
        const float ReportFactsCheckIntervalSeconds = 1f;

        // The design source (Sorolla Vitals Mobile.dc.html) authors its 11-13px type scale as phone
        // POINTS on a 392-wide phone frame. ScaleWithScreenSize's width/height blend (`match`) breaks
        // down on a landscape desktop Game View (1920x1080): matching width scales 1920/392=4.9x but
        // matching height only scales 1080/852=1.27x, and any blend between them still starves the
        // type on the height-dominated axis - confirmed too-small live at 1920x1080 even after the
        // first ScaleWithScreenSize fix. ConstantPixelSize + a manually computed `scale`, driven by
        // the SHORTER screen dimension, sidesteps the axis-blend problem entirely: a phone reads off
        // its width, a landscape desktop window reads off its height, and either way the type scale
        // matches "point size relative to the narrow axis" the way a real phone would render it.
        const float ReferenceShortDimension = 392f;

        static float ComputePanelScale() =>
            Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / ReferenceShortDimension);

        static SorollaDebugMenuOverlay s_instance;

        PanelSettings _panelSettings;
        GameObject _createdEventSystem;
        VisualElement _root;
        VisualElement _header;
        VisualElement _content;
        VisualElement[] _tabPanes = System.Array.Empty<VisualElement>();
        Button[] _tabButtons = System.Array.Empty<Button>();
        int _activeTabIndex;
        float _nextLiveRefreshTime;
        float _nextReportFactsCheckTime;
        int _reportFingerprint;

        // Computed once per Build() (menu open), never per frame - rebuilt tree, not rebuilt rows.
        readonly List<SorollaDiagnosticRow> _rows = new List<SorollaDiagnosticRow>(64);

        internal static bool IsOpen => s_instance != null;

        /// <summary>Internal (Sorolla) view: Report + Console + Actions behind tabs. Persisted so an
        /// internal session survives a relaunch; a studio never trips over it.</summary>
        static bool InternalMode
        {
            get => PlayerPrefs.GetInt(InternalModePrefKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(InternalModePrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        internal static void Open()
        {
            if (s_instance != null) return;

            SorollaDiagnostics.EnsureLogBridge();
            SorollaDiagnostics.InstallUnityLogSink();

            var host = new GameObject(HostName);
            try
            {
                DontDestroyOnLoad(host);
            }
            catch
            {
                // Very-early calls can occur before the scene is ready for DontDestroyOnLoad.
            }

            s_instance = host.AddComponent<SorollaDebugMenuOverlay>();
            s_instance.Build();
        }

        internal static void Close()
        {
            if (s_instance == null) return;
            Destroy(s_instance.gameObject);
        }

        internal static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        void Build()
        {
            var document = gameObject.AddComponent<UIDocument>();
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "SorollaDebugMenuPanelSettings (runtime, code-created)";
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = ComputePanelScale();
            _panelSettings.sortingOrder = PanelSortingOrder;
            document.panelSettings = _panelSettings;

            StyleSheet runtimeUss = Resources.Load<StyleSheet>(UssResourcePath);

            _root = document.rootVisualElement;
            _root.AddToClassList("sorolla-debugmenu-root");
            if (runtimeUss != null)
                _root.styleSheets.Add(runtimeUss);
            else
                Debug.LogWarning("[Palette] Debug menu runtime USS not found at Resources/" + UssResourcePath);

            BuildTree();

            _createdEventSystem = SorollaDebugMenuEventSystemFactory.CreateIfMissing();
        }

        /// <summary>Builds (or rebuilds) everything below the root: header, optional tab bar, panes. One
        /// entry point so flipping the internal view is a rebuild, not a second layout path.</summary>
        void BuildTree()
        {
            _root.Clear();
            SorollaDiagnostics.BuildRows(_rows);
            _reportFingerprint = SorollaDiagnostics.ComputeFactsFingerprint(_rows);

            _header = BuildHeader();
            _root.Add(_header);

            bool internalMode = InternalMode;
            _tabPanes = new VisualElement[internalMode ? 3 : 1];
            _tabButtons = internalMode ? new Button[3] : System.Array.Empty<Button>();

            if (internalMode)
                _root.Add(BuildTabBar());

            _content = new VisualElement();
            _content.AddToClassList("sorolla-debugmenu-content");
            _tabPanes[0] = BuildReportTab(_rows);
            if (internalMode)
            {
                _tabPanes[1] = BuildConsoleTab();
                _tabPanes[2] = BuildActionsTab();
            }
            foreach (VisualElement pane in _tabPanes)
                _content.Add(pane);
            _root.Add(_content);

            SetActiveTab(0);
        }

        void ToggleInternalMode()
        {
            InternalMode = !InternalMode;
            BuildTree();
        }

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("sorolla-debugmenu-header-row");

            SorollaVitalsVerdictReport verdict = SorollaDiagnostics.ComputeVerdict(_rows);
            var chip = new Label(SorollaDiagnostics.VerdictWord(verdict));
            chip.AddToClassList("sorolla-debugmenu-compact-chip");
            chip.AddToClassList(VerdictBadgeClass(verdict.Verdict));
            titleRow.Add(chip);

            var title = new Label(InternalMode ? "Sorolla Vitals · internal" : "Sorolla Vitals");
            title.AddToClassList("sorolla-debugmenu-title");
            titleRow.Add(title);

            var close = new Button(Close) { text = "Close" };
            close.AddToClassList("sorolla-debugmenu-close");
            titleRow.Add(close);

            header.Add(titleRow);

            // Rich text keeps the caption and sentence in one line box and on one baseline.
            string coverageText = SorollaDiagnostics.BuildMenuCoverageLine(out bool thin);
            string lineColor = thin ? "d9a636" : "7d8694";
            var coverageLine = new Label(
                $"<size=9.5px><color=#{lineColor}><b>COVERAGE</b></color></size>  <color=#{lineColor}>{coverageText}</color>");
            coverageLine.enableRichText = true;
            coverageLine.AddToClassList("sorolla-debugmenu-coverage-line");
            header.Add(coverageLine);

            return header;
        }

        VisualElement BuildTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("sorolla-debugmenu-tabs");

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                var button = new Button(() => SetActiveTab(index)) { text = TabLabel(i) };
                button.AddToClassList("sorolla-debugmenu-tab");
                _tabButtons[i] = button;
                bar.Add(button);
            }

            return bar;
        }

        void RefreshDiagnosticViews()
        {
            SorollaDiagnostics.BuildRows(_rows);
            _reportFingerprint = SorollaDiagnostics.ComputeFactsFingerprint(_rows);
            RebuildHeaderAndReport();
        }

        /// <summary>Rebuilds the report pane only when the facts behind it actually changed, so a fact
        /// landing while the report is on screen (an ad completing) redraws without a per-tick rebuild.</summary>
        void RefreshReportIfFactsChanged()
        {
            SorollaDiagnostics.BuildRows(_rows);
            int fingerprint = SorollaDiagnostics.ComputeFactsFingerprint(_rows);
            if (fingerprint == _reportFingerprint) return;

            _reportFingerprint = fingerprint;
            RebuildHeaderAndReport();
        }

        void RebuildHeaderAndReport()
        {
            VisualElement nextHeader = BuildHeader();
            _root.Remove(_header);
            _root.Insert(0, nextHeader);
            _header = nextHeader;

            _content.Remove(_tabPanes[0]);
            _tabPanes[0] = BuildReportTab(_rows);
            _tabPanes[0].style.display = _activeTabIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _content.Insert(0, _tabPanes[0]);
        }

        void SetActiveTab(int index)
        {
            _activeTabIndex = index;
            for (int i = 0; i < _tabPanes.Length; i++)
                _tabPanes[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                _tabButtons[i].RemoveFromClassList("sorolla-debugmenu-tab-active");
                if (i == index)
                    _tabButtons[i].AddToClassList("sorolla-debugmenu-tab-active");
            }

            if (index == 1)
                RefreshConsoleList(true);
            else if (index == 2)
                RefreshActionState();
        }

        void Update()
        {
            if (_panelSettings != null)
                _panelSettings.scale = ComputePanelScale();

            if (_activeTabIndex == 0)
            {
                if (Time.unscaledTime < _nextReportFactsCheckTime) return;
                _nextReportFactsCheckTime = Time.unscaledTime + ReportFactsCheckIntervalSeconds;
                RefreshReportIfFactsChanged();
                return;
            }

            if (Time.unscaledTime < _nextLiveRefreshTime) return;
            _nextLiveRefreshTime = Time.unscaledTime + LiveRefreshIntervalSeconds;

            if (_activeTabIndex == 1)
                RefreshConsoleList();
            else if (_activeTabIndex == 2)
                RefreshActionState();
        }

        static string TabLabel(int index)
        {
            switch (index)
            {
                case 0: return "Report";
                case 1: return "Console";
                default: return "Actions";
            }
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;

            if (_panelSettings != null)
                Destroy(_panelSettings);

            if (_createdEventSystem != null)
                Destroy(_createdEventSystem);
        }
    }
}
