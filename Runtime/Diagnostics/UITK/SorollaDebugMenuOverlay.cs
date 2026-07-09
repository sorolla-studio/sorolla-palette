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
            root.Add(BuildTabBar(issueCount, CountConsoleEntries()));

            VisualElement content = new VisualElement();
            content.AddToClassList("sorolla-debugmenu-content");
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                switch (i)
                {
                    // Phase 6 (spec section 11): Overview is now the DEFAULT landing tab - moved
                    // to index 0 (leftmost + initially active) instead of Issues.
                    case 0: _tabPanes[i] = BuildOverviewTab(_rows); break;
                    case 1: _tabPanes[i] = BuildIssuesTab(_rows); break;
                    case 2: _tabPanes[i] = BuildConsoleTab(); break;
                    case 3: _tabPanes[i] = BuildActionsTab(); break;
                    default: _tabPanes[i] = BuildPlaceholderPane(TabLabel(i)); break;
                }
                _tabPanes[i].style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                content.Add(_tabPanes[i]);
            }
            root.Add(content);

            SetActiveTab(0); // Overview, now index 0 - the default landing tab (spec section 11)

            _createdEventSystem = SorollaDebugMenuEventSystemFactory.CreateIfMissing();
        }

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("sorolla-debugmenu-header-row");

            // Phase 6 (Overview redesign, spec section 11 item 1): the big verdict badge + severity
            // count strip MOVE to the Overview hero. The always-visible header keeps only a COMPACT
            // verdict chip (word only, no dot, no count strip) so every tab's header stays slim -
            // this was the "FAILING too big" note. Full counts are one tap away (Overview, the
            // default landing tab) or in the coverage line below.
            (int fail, int warn, int wait, int pass) = SorollaDiagnostics.ComputeMenuHealthCounts(_rows);
            titleRow.Add(BuildCompactVerdictChip(fail, warn, wait));

            var title = new Label("Sorolla Vitals");
            title.AddToClassList("sorolla-debugmenu-title");
            titleRow.Add(title);

            // Phase 4 (Actions tab parity ruling): "Legacy console" removed now that every
            // QaActionRegistry action the IMGUI console exposed has a home here (ads, consent incl.
            // refresh, QA bridge, report, and all 5 event triggers incl. the phase-4 chip row).
            // Restores the mockups' one-line header as a side effect. The IMGUI console CODE stays
            // (SorollaDiagnosticsConsole*.cs untouched) - only this button is gone; deletion is
            // gated on Arthur's later device confirmation, not this phase.
            var close = new Button(Close) { text = "Close" };
            close.AddToClassList("sorolla-debugmenu-close");
            titleRow.Add(close);

            header.Add(titleRow);

            var contextLine = new Label(SorollaDiagnostics.BuildMenuContextLine());
            contextLine.AddToClassList("sorolla-debugmenu-context-line");
            header.Add(contextLine);

            // Tier-3 fix (Arthur, phase 6 fix round): margin-nudging two SEPARATE Labels never
            // actually put them on one baseline (two different font sizes each lay out from their
            // own line box top, so any fixed nudge is a guess, not a fix - confirmed by Arthur still
            // seeing misalignment after the 1px->2.5px nudge tier-2 had accepted). Structural fix:
            // ONE Label with UITK rich text, so the caption and sentence share the SAME line box and
            // therefore the same baseline by construction - no manual metric-matching required.
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

        // Compact header chip (phase 6): word-only, no dot, no count strip - roughly a third the
        // footprint of the Overview hero badge below. Same verdict thresholds as BuildVerdictBadge
        // so the header and the hero never disagree.
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

        // Goal C (tab badge live-update): called whenever Console/Issues-affecting actions run inside
        // an already-open menu (currently: Console tab's Clear). Re-derives both counts from the same
        // sources BuildTabBar used and rewrites just the button text - no full Build() rebuild needed.
        void RefreshTabBadgeCounts()
        {
            int issueCount = CountIssueRows(_rows);
            int consoleCount = CountConsoleEntries();
            _tabButtons[1].text = TabBarLabel(1, issueCount, consoleCount);
            _tabButtons[2].text = TabBarLabel(2, issueCount, consoleCount);
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
