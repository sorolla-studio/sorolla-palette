using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Overview tab (spec section 11, Arthur-approved 2026-07-09 post-implementation redesign).
    // Replaces the phase-3 "all rows grouped + filter chips" layout, which duplicated Issues with no
    // dedicated role. New role contract: Overview is the 5-second one-pager map, DEFAULT landing tab,
    // NO diagnostic rows of its own - verdict hero, then one card per diagnostic area (tap -> Issues
    // pre-filtered), then the session coverage matrix. Display-only, same BuildRows()/CaptureQaState
    // fact pipeline as every other tab.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        internal VisualElement BuildOverviewTab(List<SorollaDiagnosticRow> rows)
        {
            var pane = new VisualElement();
            pane.AddToClassList("sorolla-debugmenu-overview-pane");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sorolla-debugmenu-issues-scroll"); // shares the restyled-scrollbar rule
            var host = new VisualElement();
            scroll.Add(host);
            pane.Add(scroll);

            (int fail, int warn, int wait, int pass) = SorollaDiagnostics.ComputeMenuHealthCounts(rows);
            host.Add(BuildVerdictHero(fail, warn, wait, pass));

            host.Add(BuildAreaCardsSection(rows));
            host.Add(BuildCoverageMatrixCard());

            return pane;
        }

        VisualElement BuildVerdictHero(int fail, int warn, int wait, int pass)
        {
            var hero = new VisualElement();
            hero.AddToClassList("sorolla-debugmenu-hero");
            hero.Add(BuildVerdictBadge(fail, warn, wait));
            hero.Add(BuildCountStrip(fail, warn, wait, pass));
            return hero;
        }

        // One card per Group actually present in the snapshot, in encounter order - NOT a fixed
        // enumeration of the spec's 7 named areas (SDKs/Consent/Firebase/Ads/Identity/Activity/Red
        // flags). Judgment call (stated for the report): the row pipeline also emits "Boot" and
        // "Config" groups the spec prose didn't name; hardcoding the 7-item list would silently drop
        // those rows off Overview entirely, which contradicts "derived only from snapshot facts."
        VisualElement BuildAreaCardsSection(List<SorollaDiagnosticRow> rows)
        {
            var section = new VisualElement();
            section.AddToClassList("sorolla-debugmenu-area-cards");

            for (int i = 0; i < rows.Count;)
            {
                string group = rows[i].Group;
                int start = i;
                while (i < rows.Count && rows[i].Group == group)
                    i++;
                section.Add(BuildAreaCard(group, rows, start, i));
            }

            return section;
        }

        VisualElement BuildAreaCard(string group, List<SorollaDiagnosticRow> rows, int start, int end)
        {
            int fail = 0, warn = 0, wait = 0, pass = 0;
            SorollaDiagnosticRow topRow = default;
            int topRank = -1;
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

                int rank = SeverityRank(row.Severity);
                if (rank > topRank)
                {
                    topRank = rank;
                    topRow = row;
                }
            }

            bool hasProblems = fail > 0 || warn > 0 || wait > 0;

            var card = new VisualElement();
            card.AddToClassList("sorolla-debugmenu-area-card");
            card.AddToClassList(hasProblems ? AreaCardSeverityClass(fail, warn) : "sorolla-debugmenu-area-card-clean");

            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-area-card-header");

            var dot = new VisualElement();
            dot.AddToClassList("sorolla-debugmenu-area-card-dot");
            dot.AddToClassList(hasProblems ? AreaDotSeverityClass(fail, warn) : "sorolla-debugmenu-area-card-dot-clean");
            header.Add(dot);

            var name = new Label(group);
            name.AddToClassList("sorolla-debugmenu-area-card-name");
            header.Add(name);

            var counts = new Label(AreaCardCountsText(fail, warn, wait, pass));
            counts.AddToClassList("sorolla-debugmenu-area-card-counts");
            header.Add(counts);

            var chevron = new Label("›");
            chevron.AddToClassList("sorolla-debugmenu-area-card-chevron");
            header.Add(chevron);

            card.Add(header);

            // Single most-important-fact line (spec: "Consent - 1 waiting · CMP not resolved").
            var fact = new Label($"{group} · {SafeFirstLine(topRow.Detail)}");
            fact.AddToClassList("sorolla-debugmenu-area-card-fact");
            card.Add(fact);

            // All-green cards expand IN PLACE to show their PASS rows (spec item 2); a card with
            // problems instead jumps to Issues pre-filtered - two different taps, same header.
            if (hasProblems)
            {
                header.RegisterCallback<ClickEvent>(_ => JumpToIssuesFilteredBy(group));
                return card;
            }

            var body = new VisualElement();
            body.AddToClassList("sorolla-debugmenu-section-body");
            body.style.display = DisplayStyle.None;
            for (int i = start; i < end; i++)
                body.Add(BuildOverviewRow(rows[i]));
            card.Add(body);

            bool expanded = false;
            header.RegisterCallback<ClickEvent>(_ =>
            {
                expanded = !expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = expanded ? "⌄" : "›";
            });

            return card;
        }

        static string AreaCardCountsText(int fail, int warn, int wait, int pass)
        {
            if (fail > 0) return $"{fail} FAIL";
            if (warn > 0) return $"{warn} WARN";
            if (wait > 0) return $"{wait} WAIT";
            return $"{pass} PASS";
        }

        static string AreaCardSeverityClass(int fail, int warn) =>
            fail > 0 ? "sorolla-debugmenu-area-card-fail" : warn > 0 ? "sorolla-debugmenu-area-card-warn" : "sorolla-debugmenu-area-card-wait";

        static string AreaDotSeverityClass(int fail, int warn) =>
            fail > 0 ? "sorolla-debugmenu-area-card-dot-fail" : warn > 0 ? "sorolla-debugmenu-area-card-dot-warn" : "sorolla-debugmenu-area-card-dot-wait";

        // Child rows share the Issues row anatomy (badge+name+detail+expand to WHY/SIGNAL/FIX),
        // indented, always-collapsed - an all-green card's expansion is "the complete picture", not a
        // to-do list.
        static VisualElement BuildOverviewRow(SorollaDiagnosticRow row)
        {
            VisualElement issueRow = BuildIssueRow(row);
            issueRow.AddToClassList("sorolla-debugmenu-section-child-row");
            return issueRow;
        }
    }
}
