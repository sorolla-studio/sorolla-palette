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
        readonly VisualElement[] _tabPanes = new VisualElement[4];
        readonly Button[] _tabButtons = new Button[4];
        int _activeTabIndex;
        float _nextLiveRefreshTime;

        // Computed once per Build() (menu open), never per frame - rebuilt tree, not rebuilt rows.
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

            SorollaDiagnostics.BuildRows(_rows);
            int issueCount = CountIssueRows(_rows);

            _header = BuildHeader();
            _root.Add(_header);
            _root.Add(BuildTabBar(issueCount, CountConsoleEntries()));

            _content = new VisualElement();
            _content.AddToClassList("sorolla-debugmenu-content");
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                switch (i)
                {
                    case 0: _tabPanes[i] = BuildOverviewTab(_rows); break;
                    case 1: _tabPanes[i] = BuildIssuesTab(_rows); break;
                    case 2: _tabPanes[i] = BuildConsoleTab(); break;
                    case 3: _tabPanes[i] = BuildActionsTab(); break;
                }
                _tabPanes[i].style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _content.Add(_tabPanes[i]);
            }
            _root.Add(_content);

            SetActiveTab(0);

            _createdEventSystem = SorollaDebugMenuEventSystemFactory.CreateIfMissing();
        }

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("sorolla-debugmenu-header-row");

            // The header stays compact; full verdict counts live on the default Overview tab.
            (int fail, int warn, int wait, int pass) = SorollaDiagnostics.ComputeMenuHealthCounts(_rows);
            titleRow.Add(BuildCompactVerdictChip(fail, warn, wait));

            var title = new Label("Sorolla Vitals");
            title.AddToClassList("sorolla-debugmenu-title");
            titleRow.Add(title);

            var close = new Button(Close) { text = "Close" };
            close.AddToClassList("sorolla-debugmenu-close");
            titleRow.Add(close);

            header.Add(titleRow);

            var contextLine = new Label(SorollaDiagnostics.BuildMenuContextLine());
            contextLine.AddToClassList("sorolla-debugmenu-context-line");
            header.Add(contextLine);

            // Rich text keeps the caption and sentence in one line box and on one baseline.
            string coverageText = SorollaDiagnostics.BuildMenuCoverageLine(out bool thin);
            string captionColor = thin ? "d9a636" : "7d8694";
            string sentenceColor = thin ? "d9a636" : "7d8694";
            var coverageLine = new Label(
                $"<size=9.5px><color=#{captionColor}><b>COVERAGE</b></color></size>  <color=#{sentenceColor}>{coverageText}</color>");
            coverageLine.enableRichText = true;
            coverageLine.AddToClassList("sorolla-debugmenu-coverage-line");
            header.Add(coverageLine);

            return header;
        }

        // Uses the same thresholds as the Overview verdict badge.
        static VisualElement BuildCompactVerdictChip(int fail, int warn, int wait)
        {
            string verdictClass;
            string verdictWord;
            if (fail > 0) { verdictClass = "sorolla-debugmenu-badge-failing"; verdictWord = "FAILING"; }
            else if (warn + wait > 0) { verdictClass = "sorolla-debugmenu-badge-issues"; verdictWord = $"{warn + wait} ISSUES"; }
            else { verdictClass = "sorolla-debugmenu-badge-healthy"; verdictWord = "HEALTHY"; }

            var chip = new Label(verdictWord);
            chip.AddToClassList("sorolla-debugmenu-compact-chip");
            chip.AddToClassList(verdictClass);
            return chip;
        }

        internal static VisualElement BuildVerdictBadge(int fail, int warn, int wait)
        {
            string verdictClass;
            string verdictWord;
            if (fail > 0)
            {
                verdictClass = "sorolla-debugmenu-badge-failing";
                verdictWord = "FAILING";
            }
            else if (warn + wait > 0)
            {
                verdictClass = "sorolla-debugmenu-badge-issues";
                verdictWord = $"{warn + wait} ISSUES";
            }
            else
            {
                verdictClass = "sorolla-debugmenu-badge-healthy";
                verdictWord = "HEALTHY";
            }

            var badge = new VisualElement();
            badge.AddToClassList("sorolla-debugmenu-badge");
            badge.AddToClassList(verdictClass);
            var badgeDot = new VisualElement();
            badgeDot.AddToClassList("sorolla-debugmenu-badge-dot");
            badge.Add(badgeDot);
            var badgeLabel = new Label(verdictWord);
            badgeLabel.AddToClassList("sorolla-debugmenu-badge-label");
            badge.Add(badgeLabel);
            return badge;
        }

        internal static VisualElement BuildCountStrip(int fail, int warn, int wait, int pass)
        {
            var strip = new VisualElement();
            strip.AddToClassList("sorolla-debugmenu-countstrip");
            strip.Add(BuildCountItem("FAIL", fail, "sorolla-debugmenu-count-fail"));
            strip.Add(BuildCountItem("WARN", warn, "sorolla-debugmenu-count-warn"));
            strip.Add(BuildCountItem("WAIT", wait, "sorolla-debugmenu-count-wait"));
            strip.Add(BuildCountItem("PASS", pass, "sorolla-debugmenu-count-pass", alwaysColored: true));
            return strip;
        }

        static Label BuildCountItem(string label, int count, string colorClass, bool alwaysColored = false)
        {
            var item = new Label($"{label} {count}");
            item.AddToClassList("sorolla-debugmenu-countstrip-item");
            item.AddToClassList(count > 0 || alwaysColored ? colorClass : "sorolla-debugmenu-count-zero");
            return item;
        }

        VisualElement BuildTabBar(int issueCount, int consoleCount)
        {
            var bar = new VisualElement();
            bar.AddToClassList("sorolla-debugmenu-tabs");

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                var button = new Button(() => SetActiveTab(index)) { text = TabBarLabel(i, issueCount, consoleCount) };
                button.AddToClassList("sorolla-debugmenu-tab");
                _tabButtons[i] = button;
                bar.Add(button);
            }

            return bar;
        }

        static string TabBarLabel(int index, int issueCount, int consoleCount)
        {
            switch (index)
            {
                case 1: return $"Issues {issueCount}";
                case 2: return consoleCount > 0 ? $"Console {consoleCount}" : "Console";
                default: return TabLabel(index);
            }
        }

        int CountConsoleEntries()
        {
            var events = new List<SorollaDiagnosticEventLogEntry>(40);
            var problems = new List<SorollaRuntimeProblem>(20);
            SorollaDiagnostics.CopyEventLog(events);
            SorollaDiagnostics.CopyRuntimeProblems(problems);
            return events.Count + problems.Count;
        }

        void RefreshTabBadgeCounts()
        {
            int issueCount = CountIssueRows(_rows);
            int consoleCount = CountConsoleEntries();
            _tabButtons[1].text = TabBarLabel(1, issueCount, consoleCount);
            _tabButtons[2].text = TabBarLabel(2, issueCount, consoleCount);
        }

        void RefreshDiagnosticViews()
        {
            SorollaDiagnostics.BuildRows(_rows);

            VisualElement nextHeader = BuildHeader();
            _root.Remove(_header);
            _root.Insert(0, nextHeader);
            _header = nextHeader;

            _content.Remove(_tabPanes[0]);
            _content.Remove(_tabPanes[1]);
            _tabPanes[0] = BuildOverviewTab(_rows);
            _tabPanes[1] = BuildIssuesTab(_rows);
            _tabPanes[0].style.display = _activeTabIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _tabPanes[1].style.display = _activeTabIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            _content.Insert(0, _tabPanes[0]);
            _content.Insert(1, _tabPanes[1]);
            RefreshTabBadgeCounts();
        }

        /// <summary>Issues tab count: every row (Required or Observed) at FAIL/WARN/WAIT, matching
        /// what BuildIssuesTab actually lists - see SorollaDebugMenuOverlay.Issues.cs for the ruling
        /// that Observed rows belong in the studio's flat to-do list too.</summary>
        static int CountIssueRows(List<SorollaDiagnosticRow> rows)
        {
            int count = 0;
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (SorollaDiagnostics.NeedsAttention(row.Severity))
                    count++;
            }

            return count;
        }

        void SetActiveTab(int index)
        {
            _activeTabIndex = index;
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                _tabPanes[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
                _tabButtons[i].RemoveFromClassList("sorolla-debugmenu-tab-active");
                if (i == index)
                    _tabButtons[i].AddToClassList("sorolla-debugmenu-tab-active");
            }

            if (index == 2)
                RefreshConsoleList(true);
            else if (index == 3)
                RefreshActionState();
        }

        void Update()
        {
            if (_panelSettings != null)
                _panelSettings.scale = ComputePanelScale();

            if (Time.unscaledTime < _nextLiveRefreshTime) return;
            _nextLiveRefreshTime = Time.unscaledTime + LiveRefreshIntervalSeconds;

            if (_activeTabIndex == 2)
                RefreshConsoleList();
            else if (_activeTabIndex == 3)
                RefreshActionState();
        }

        static string TabLabel(int index)
        {
            switch (index)
            {
                case 0: return "Overview";
                case 1: return "Issues";
                case 2: return "Console";
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
