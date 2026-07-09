using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Phase-1 spike proved the injected-UIDocument bootstrap gate (inert parallel code, opened only
    // via the existing IMGUI console's "Open new menu (preview)" action button; the IMGUI console
    // itself stays untouched and shipping). Phase 2 (debug-menu overhaul) fills in the design system,
    // the real header (verdict/counts/context/coverage), and the Issues tab - see
    // SorollaDebugMenuOverlay.Issues.cs. Zero scene setup - GameObject + UIDocument + PanelSettings
    // are created entirely from code on open, DontDestroyOnLoad, and fully torn down on close.
    // Display-only: all facts are read straight off the existing diagnostics/snapshot pipeline
    // (BuildRows, CaptureQaState, Application.*) - no new fact pipeline. Overview/Console/Actions
    // tabs stay empty placeholders until phase 3+.
    internal sealed partial class SorollaDebugMenuOverlay : MonoBehaviour
    {
        const string HostName = "[Palette SDK Debug Menu]";
        const string ThemeResourcePath = "SorollaDebugMenuTheme";
        const string UssResourcePath = "SorollaDebugMenuRuntime";
        const int PanelSortingOrder = 32000;

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
        readonly VisualElement[] _tabPanes = new VisualElement[4];
        readonly Button[] _tabButtons = new Button[4];
        int _activeTabIndex;

        // Computed once per Build() (menu open), never per frame - rebuilt tree, not rebuilt rows.
        readonly List<SorollaDiagnosticRow> _rows = new List<SorollaDiagnosticRow>(64);

        internal static bool IsOpen => s_instance != null;

        internal static void Open()
        {
            if (s_instance != null) return;

            var host = new GameObject(HostName);
            try
            {
                DontDestroyOnLoad(host);
            }
            catch
            {
                // Matches SorollaDiagnosticsConsole.EnsureStandalone: tolerate very-early calls
                // before the scene is ready for DontDestroyOnLoad.
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

        static void OpenLegacyConsole() => SorollaDiagnosticsConsole.Show();

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

            VisualElement root = document.rootVisualElement;
            root.AddToClassList("sorolla-debugmenu-root");
            if (runtimeUss != null)
                root.styleSheets.Add(runtimeUss);
            else
                Debug.LogWarning("[Palette] Debug menu spike: runtime USS not found at Resources/" + UssResourcePath);

            SorollaDiagnostics.BuildRows(_rows);
            int issueCount = CountIssueRows(_rows);

            root.Add(BuildHeader());
            root.Add(BuildTabBar(issueCount));

            VisualElement content = new VisualElement();
            content.AddToClassList("sorolla-debugmenu-content");
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                _tabPanes[i] = i == 0 ? BuildIssuesTab(_rows) : BuildPlaceholderPane(TabLabel(i));
                _tabPanes[i].style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                content.Add(_tabPanes[i]);
            }
            root.Add(content);

            SetActiveTab(0);

            _createdEventSystem = SorollaDebugMenuEventSystemFactory.CreateIfMissing();
        }

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("sorolla-debugmenu-header-row");

            (int fail, int warn, int wait, int pass) = SorollaDiagnostics.ComputeMenuHealthCounts(_rows);
            titleRow.Add(BuildVerdictBadge(fail, warn, wait));

            var title = new Label("Sorolla Vitals");
            title.AddToClassList("sorolla-debugmenu-title");
            titleRow.Add(title);

            // Temporary two-way switch for the phase 2-4 transition (Arthur, scope addition): the old
            // IMGUI console still owns Actions/Console until those tabs are ported, so the 5-tap
            // unlock opening this overlay directly must not strand anyone who needs it.
            var legacy = new Button(OpenLegacyConsole) { text = "Legacy console" };
            legacy.AddToClassList("sorolla-debugmenu-close");
            titleRow.Add(legacy);

            var close = new Button(Close) { text = "Close" };
            close.AddToClassList("sorolla-debugmenu-close");
            titleRow.Add(close);

            header.Add(titleRow);
            header.Add(BuildCountStrip(fail, warn, wait, pass));

            var contextLine = new Label(SorollaDiagnostics.BuildMenuContextLine());
            contextLine.AddToClassList("sorolla-debugmenu-context-line");
            header.Add(contextLine);

            var coverageCaption = new Label("COVERAGE");
            coverageCaption.AddToClassList("sorolla-debugmenu-coverage-caption");

            string coverageText = SorollaDiagnostics.BuildMenuCoverageLine(out bool thin);
            var coverageLine = new Label(coverageText);
            coverageLine.AddToClassList("sorolla-debugmenu-coverage-line");
            if (thin)
            {
                coverageCaption.AddToClassList("sorolla-debugmenu-coverage-line-thin");
                coverageLine.AddToClassList("sorolla-debugmenu-coverage-line-thin");
            }

            header.Add(coverageCaption);
            header.Add(coverageLine);

            return header;
        }

        static VisualElement BuildVerdictBadge(int fail, int warn, int wait)
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

        static VisualElement BuildCountStrip(int fail, int warn, int wait, int pass)
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

        VisualElement BuildTabBar(int issueCount)
        {
            var bar = new VisualElement();
            bar.AddToClassList("sorolla-debugmenu-tabs");

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                string label = i == 0 ? $"Issues {issueCount}" : TabLabel(i);
                var button = new Button(() => SetActiveTab(index)) { text = label };
                button.AddToClassList("sorolla-debugmenu-tab");
                _tabButtons[i] = button;
                bar.Add(button);
            }

            return bar;
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

        static VisualElement BuildPlaceholderPane(string tabLabel)
        {
            var pane = new VisualElement();
            var label = new Label($"{tabLabel} (placeholder - phase 2+ fills this in)");
            label.AddToClassList("sorolla-debugmenu-placeholder");
            pane.Add(label);
            return pane;
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
        }

        static string TabLabel(int index)
        {
            switch (index)
            {
                case 0: return "Issues";
                case 1: return "Overview";
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
