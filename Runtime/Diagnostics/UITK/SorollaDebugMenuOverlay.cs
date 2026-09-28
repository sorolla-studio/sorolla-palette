using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Code-created runtime debug menu: no prefab or scene setup. All facts come from the existing
    // diagnostics/snapshot pipeline, and all runtime UI objects are torn down when the menu closes.
    internal sealed partial class SorollaDebugMenuOverlay : MonoBehaviour
    {
        const string HostName = "[Palette SDK Debug Menu]";
        const string ThemeResourcePath = "SorollaDebugMenuTheme";
        const string UssResourcePath = "SorollaDebugMenuRuntime";
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
        Label _headerVerdict;
        Label _headerCoverage;
        readonly VisualElement[] _tabPanes = new VisualElement[3];
        readonly Button[] _tabButtons = new Button[3];
        Rect _safeArea;
        Vector2Int _screenSize;
        int _activeTabIndex;
        float _nextLiveRefreshTime;
        float _nextReportFactsCheckTime;
        int _reportFingerprint;

        readonly List<SorollaDiagnosticRow> _rows = new List<SorollaDiagnosticRow>(64);

        internal static bool IsOpen => s_instance != null;

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

            RefreshPanelGeometry();
            SorollaDiagnostics.BuildRows(_rows);
            _reportFingerprint = SorollaDiagnostics.ComputeFactsFingerprint(_rows);

            _root.Add(BuildHeader());
            RefreshHeader();
            _root.Add(BuildTabBar());

            var content = new VisualElement();
            content.AddToClassList("sorolla-debugmenu-content");
            _tabPanes[0] = BuildReportTab(_rows);
            _tabPanes[1] = BuildConsoleTab();
            _tabPanes[2] = BuildActionsTab();
            foreach (VisualElement pane in _tabPanes)
                content.Add(pane);
            _root.Add(content);

            SetActiveTab(0);
            _nextReportFactsCheckTime = Time.unscaledTime + ReportFactsCheckIntervalSeconds;
            _createdEventSystem = SorollaDebugMenuEventSystemFactory.CreateIfMissing();
        }

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("sorolla-debugmenu-header-row");

            _headerVerdict = new Label();
            _headerVerdict.AddToClassList("sorolla-debugmenu-compact-chip");
            titleRow.Add(_headerVerdict);

            var title = new Label("Sorolla Vitals");
            title.AddToClassList("sorolla-debugmenu-title");
            titleRow.Add(title);

            var close = new Button(Close) { text = "Close" };
            close.AddToClassList("sorolla-debugmenu-close");
            titleRow.Add(close);

            header.Add(titleRow);

            _headerCoverage = new Label();
            _headerCoverage.AddToClassList("sorolla-debugmenu-coverage-line");
            header.Add(_headerCoverage);

            return header;
        }

        void RefreshHeader()
        {
            SorollaVitalsVerdictReport verdict = SorollaDiagnostics.ComputeVerdict(_rows);
            _headerVerdict.text = SorollaDiagnostics.VerdictWord(verdict);
            _headerVerdict.EnableInClassList("sorolla-debugmenu-badge-failing", verdict.Verdict == SorollaVitalsVerdict.Failing);
            _headerVerdict.EnableInClassList("sorolla-debugmenu-badge-issues",
                verdict.Verdict == SorollaVitalsVerdict.ActionNeeded || verdict.Verdict == SorollaVitalsVerdict.NotProven);
            _headerVerdict.EnableInClassList("sorolla-debugmenu-badge-healthy", verdict.Verdict == SorollaVitalsVerdict.Pass);
            _headerCoverage.text = "COVERAGE  " + SorollaDiagnostics.BuildMenuCoverageLine(out bool thin);
            _headerCoverage.EnableInClassList("sorolla-debugmenu-coverage-thin", thin);
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
            RefreshHeader();
            if (_activeTabIndex != 0) return;

            int fingerprint = SorollaDiagnostics.ComputeFactsFingerprint(_rows);
            if (fingerprint == _reportFingerprint) return;

            _reportFingerprint = fingerprint;
            RefreshReportContent(_rows);
        }

        void SetActiveTab(int index)
        {
            _activeTabIndex = index;
            for (int i = 0; i < _tabPanes.Length; i++)
                _tabPanes[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < _tabButtons.Length; i++)
                _tabButtons[i].EnableInClassList("sorolla-debugmenu-tab-active", i == index);

            if (index == 0)
                RefreshDiagnosticViews();
            else if (index == 1)
                RefreshConsoleList();
            else if (index == 2)
                RefreshActionState();
        }

        void Update()
        {
            RefreshPanelGeometry();

            if (Time.unscaledTime >= _nextReportFactsCheckTime)
            {
                _nextReportFactsCheckTime = Time.unscaledTime + ReportFactsCheckIntervalSeconds;
                RefreshDiagnosticViews();
            }

            if (Time.unscaledTime < _nextLiveRefreshTime) return;
            _nextLiveRefreshTime = Time.unscaledTime + LiveRefreshIntervalSeconds;

            if (_activeTabIndex == 1)
                RefreshConsoleList();
            else if (_activeTabIndex == 2)
                RefreshActionState();
        }

        void RefreshPanelGeometry()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;
            if (size == _screenSize && safeArea == _safeArea) return;

            _screenSize = size;
            _safeArea = safeArea;
            float scale = ComputePanelScale();
            _panelSettings.scale = scale;
            _root.style.paddingLeft = safeArea.xMin / scale;
            _root.style.paddingRight = (size.x - safeArea.xMax) / scale;
            _root.style.paddingTop = (size.y - safeArea.yMax) / scale;
            _root.style.paddingBottom = safeArea.yMin / scale;
        }

        static ScrollView BuildScrollView()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            };
            scroll.AddToClassList("sorolla-debugmenu-issues-scroll");
            return scroll;
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
