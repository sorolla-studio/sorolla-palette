using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Overview tab (mockup 03, spec 2.3): all diagnostic rows grouped in collapsible sections using
    // the SAME consecutive-Group convention the IMGUI Vitals tab uses (SorollaDiagnosticsConsole.
    // UI.cs DrawRows) - rows for one group are always contiguous in BuildRows() output, so grouping
    // is a single linear pass, not a dictionary/GroupBy. Display-only: no new fact pipeline.
    //
    // Judgment call (stated for the report): the filter chips operate on ALL rows (Required +
    // Observed + Context/Pass), unlike the Issues tab's Required/Observed-only "needs attention"
    // list - Overview's job is "the complete picture" (spec 2.3 heading), so a PASS/Context row must
    // still be visible under the "All"/"Pass" filters even though it never appears in Issues.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        enum OverviewFilter
        {
            All,
            Problems,
            Fail,
            Warn,
            Wait,
            Pass,
        }

        OverviewFilter _overviewFilter;
        readonly Dictionary<string, bool> _overviewSectionExpanded = new Dictionary<string, bool>(16);
        VisualElement _overviewSectionsHost;
        List<SorollaDiagnosticRow> _overviewRows;
        readonly List<Button> _overviewFilterChips = new List<Button>(6);

        internal VisualElement BuildOverviewTab(List<SorollaDiagnosticRow> rows)
        {
            _overviewRows = rows;

            bool anyIssues = CountIssueRows(rows) > 0;
            _overviewFilter = anyIssues ? OverviewFilter.Problems : OverviewFilter.All;

            var pane = new VisualElement();
            pane.AddToClassList("sorolla-debugmenu-overview-pane");

            pane.Add(BuildOverviewFilterRow());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sorolla-debugmenu-issues-scroll"); // shares the restyled-scrollbar rule
            _overviewSectionsHost = new VisualElement();
            scroll.Add(_overviewSectionsHost);
            pane.Add(scroll);

            RefreshOverviewSections();

            return pane;
        }

        VisualElement BuildOverviewFilterRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-debugmenu-filter-row");
            _overviewFilterChips.Clear();

            foreach (OverviewFilter filter in new[]
                     {
                         OverviewFilter.All, OverviewFilter.Problems, OverviewFilter.Fail,
                         OverviewFilter.Warn, OverviewFilter.Wait, OverviewFilter.Pass,
                     })
            {
                var chip = new Button(() =>
                {
                    _overviewFilter = filter;
                    RefreshOverviewSections();
                })
                {
                    text = filter.ToString(),
                };
                chip.AddToClassList("sorolla-debugmenu-filter-chip");
                chip.userData = filter;
                _overviewFilterChips.Add(chip);
                row.Add(chip);
            }

            return row;
        }

        void RefreshOverviewSections()
        {
            _overviewSectionsHost.Clear();

            // Chip active-state sync via the tracked chip list (team-lead tier-2 fix: the previous
            // parent-traversal query never found the filter row - a ScrollView reparents its children
            // into its own contentContainer, so _overviewSectionsHost.parent was the ScrollView's
            // content container, not a sibling of the filter row - every chip silently stayed
            // inactive-styled regardless of _overviewFilter).
            foreach (Button chip in _overviewFilterChips)
            {
                bool active = chip.userData is OverviewFilter cf && cf == _overviewFilter;
                chip.EnableInClassList("sorolla-debugmenu-filter-chip-active", active);
            }

            List<SorollaDiagnosticRow> rows = _overviewRows;
            for (int i = 0; i < rows.Count;)
            {
                string group = rows[i].Group;
                int start = i;
                while (i < rows.Count && rows[i].Group == group)
                    i++;
                int end = i;

                VisualElement section = BuildOverviewSection(group, rows, start, end);
                if (section != null)
                    _overviewSectionsHost.Add(section);
            }
        }

        VisualElement BuildOverviewSection(string group, List<SorollaDiagnosticRow> rows, int start, int end)
        {
            int fail = 0, warn = 0, wait = 0, pass = 0;
            var visibleRows = new List<SorollaDiagnosticRow>(end - start);
            for (int i = start; i < end; i++)
            {
                SorollaDiagnosticRow row = rows[i];
                switch (row.Severity)
                {
                    case SorollaDiagnosticSeverity.Fail: fail++; break;
                    case SorollaDiagnosticSeverity.Warning: warn++; break;
                    case SorollaDiagnosticSeverity.Waiting: wait++; break;
                    case SorollaDiagnosticSeverity.Pass: pass++; break;
                }

                if (MatchesOverviewFilter(row))
                    visibleRows.Add(row);
            }

            if (visibleRows.Count == 0) return null; // empty sections hide under the active filter

            // Team-lead tier-2 fix: WAIT counts as a problem for BOTH count color and default
            // expansion (mockup 03's CONSENT section - "1 WAIT · 2 PASS" - renders amber and
            // default-expanded). WAIT rows are on the Issues to-do list, so a section hiding one
            // behind a "clean" green/collapsed treatment would misrepresent it.
            bool hasProblems = fail > 0 || warn > 0 || wait > 0;

            var section = new VisualElement();
            section.AddToClassList("sorolla-debugmenu-section");

            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-section-header");

            var chevron = new Label(hasProblems ? "⌄" : "›");
            chevron.AddToClassList("sorolla-debugmenu-section-chevron");
            header.Add(chevron);

            var title = new Label(group.ToUpperInvariant());
            title.AddToClassList("sorolla-debugmenu-section-title");
            header.Add(title);

            var count = new Label(SectionCountsText(fail, warn, wait, pass));
            count.AddToClassList("sorolla-debugmenu-section-count");
            count.AddToClassList(hasProblems ? "sorolla-debugmenu-section-count-warn" : "sorolla-debugmenu-section-count-clean");
            header.Add(count);

            section.Add(header);

            var body = new VisualElement();
            body.AddToClassList("sorolla-debugmenu-section-body");
            foreach (SorollaDiagnosticRow row in visibleRows)
                body.Add(BuildOverviewRow(row));
            section.Add(body);

            // Sections with problems default-expanded, all-green default-collapsed (spec 2.3); tap
            // toggles, remembered per-group for the life of this menu instance.
            bool expanded = _overviewSectionExpanded.TryGetValue(group, out bool stored) ? stored : hasProblems;
            body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            chevron.text = expanded ? "⌄" : "›";

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool nowExpanded = body.style.display == DisplayStyle.None;
                body.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = nowExpanded ? "⌄" : "›";
                _overviewSectionExpanded[group] = nowExpanded;
            });

            return section;
        }

        bool MatchesOverviewFilter(SorollaDiagnosticRow row)
        {
            switch (_overviewFilter)
            {
                case OverviewFilter.All: return true;
                case OverviewFilter.Problems: return SorollaDiagnostics.NeedsAttention(row.Severity);
                case OverviewFilter.Fail: return row.Severity == SorollaDiagnosticSeverity.Fail;
                case OverviewFilter.Warn: return row.Severity == SorollaDiagnosticSeverity.Warning;
                case OverviewFilter.Wait: return row.Severity == SorollaDiagnosticSeverity.Waiting;
                case OverviewFilter.Pass: return row.Severity == SorollaDiagnosticSeverity.Pass;
                default: return true;
            }
        }

        static string SectionCountsText(int fail, int warn, int wait, int pass)
        {
            if (fail == 0 && warn == 0 && wait == 0 && pass == 0) return "none";

            var parts = new List<string>(4);
            if (fail > 0) parts.Add($"{fail} FAIL");
            if (warn > 0) parts.Add($"{warn} WARN");
            if (wait > 0) parts.Add($"{wait} WAIT");
            if (pass > 0) parts.Add($"{pass} PASS");
            return string.Join(" · ", parts);
        }

        // Child rows share the Issues row anatomy (badge+name+detail+expand to WHY/SIGNAL/FIX), just
        // indented and always-collapsed-by-default regardless of severity (Overview's job is the
        // complete picture, not a to-do list - the Issues tab already surfaces problems pre-expanded
        // nowhere; consistent "tap to expand" behavior here too).
        static VisualElement BuildOverviewRow(SorollaDiagnosticRow row)
        {
            VisualElement issueRow = BuildIssueRow(row);
            issueRow.AddToClassList("sorolla-debugmenu-section-child-row");
            return issueRow;
        }
    }
}
