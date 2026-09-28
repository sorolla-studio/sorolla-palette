using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Report the SDK verdict, fixes and coverage from the shared diagnostics facts.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        VisualElement _reportHost;
        readonly HashSet<(string Group, string Name)> _expandedReportRows = new HashSet<(string, string)>();
        bool _showHealthyChecks;

        internal VisualElement BuildReportTab(List<SorollaDiagnosticRow> rows)
        {
            var pane = new VisualElement();
            pane.AddToClassList("sorolla-debugmenu-issues-pane");

            var scroll = BuildScrollView();
            _reportHost = new VisualElement();
            scroll.Add(_reportHost);
            pane.Add(scroll);
            RefreshReportContent(rows);
            return pane;
        }

        void RefreshReportContent(List<SorollaDiagnosticRow> rows)
        {
            _reportHost.Clear();
            SorollaVitalsVerdictReport verdict = SorollaDiagnostics.ComputeVerdict(rows);

            _reportHost.Add(BuildVerdictHero(verdict));
            _reportHost.Add(BuildContextLine());
            _reportHost.Add(BuildFixTheseSection(rows));
            VisualElement sorollaSection = BuildSendToSorollaSection(rows);
            if (sorollaSection != null)
                _reportHost.Add(sorollaSection);
            _reportHost.Add(BuildTestYourGameSection());
            _reportHost.Add(BuildHealthyChecksSection(rows));
            _reportHost.Add(BuildReportFooter());
        }

        // ── Verdict hero ──────────────────────────────────────────────────

        VisualElement BuildVerdictHero(in SorollaVitalsVerdictReport verdict)
        {
            var hero = new VisualElement();
            hero.AddToClassList("sorolla-debugmenu-hero");

            var topRow = new VisualElement();
            topRow.AddToClassList("sorolla-debugmenu-hero-top-row");
            topRow.Add(BuildVerdictBadge(verdict));
            hero.Add(topRow);

            var meaning = new Label(SorollaDiagnostics.VerdictMeaning(verdict));
            meaning.AddToClassList("sorolla-debugmenu-verdict-meaning");
            hero.Add(meaning);

            hero.Add(BuildCountStrip(verdict.Fail, verdict.Warn, verdict.Wait, verdict.Pass));
            return hero;
        }

        internal static VisualElement BuildVerdictBadge(in SorollaVitalsVerdictReport report)
        {
            var badge = new VisualElement();
            badge.AddToClassList("sorolla-debugmenu-badge");
            badge.AddToClassList(VerdictBadgeClass(report.Verdict));

            var badgeDot = new VisualElement();
            badgeDot.AddToClassList("sorolla-debugmenu-badge-dot");
            badge.Add(badgeDot);

            var badgeLabel = new Label(SorollaDiagnostics.VerdictWord(report));
            badgeLabel.AddToClassList("sorolla-debugmenu-badge-label");
            badge.Add(badgeLabel);
            return badge;
        }

        // NOT PROVEN shares the amber "issues" treatment on purpose: it is a not-yet, and the one thing
        // it must never look like is the green pill.
        static string VerdictBadgeClass(SorollaVitalsVerdict verdict) => verdict switch
        {
            SorollaVitalsVerdict.Failing => "sorolla-debugmenu-badge-failing",
            SorollaVitalsVerdict.ActionNeeded => "sorolla-debugmenu-badge-issues",
            SorollaVitalsVerdict.NotProven => "sorolla-debugmenu-badge-issues",
            _ => "sorolla-debugmenu-badge-healthy",
        };

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

        // ── Healthy check details ─────────────────────────────────────────

        VisualElement BuildHealthyChecksSection(List<SorollaDiagnosticRow> rows)
        {
            var section = new VisualElement();
            var body = new VisualElement();
            body.AddToClassList("sorolla-debugmenu-matrix-card");

            int count = 0;
            foreach (SorollaDiagnosticRow row in rows)
            {
                if (!ShowsInHealthyDetails(row)) continue;
                body.Add(BuildHealthyCheckRow(row));
                count++;
            }

            var toggle = new Button();
            toggle.AddToClassList("sorolla-debugmenu-healthy-toggle");

            void SetExpanded(bool expanded)
            {
                _showHealthyChecks = expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                toggle.text = expanded
                    ? $"⌄ Hide healthy checks ({count})"
                    : $"› Show healthy checks ({count})";
            }

            toggle.clicked += () => SetExpanded(!_showHealthyChecks);
            section.Add(toggle);
            section.Add(body);
            SetExpanded(_showHealthyChecks);
            return section;
        }

        internal static bool ShowsInHealthyDetails(in SorollaDiagnosticRow row) =>
            SorollaDiagnostics.DrivesHealth(row) && !SorollaDiagnostics.NeedsAttention(row.Severity);

        static VisualElement BuildHealthyCheckRow(in SorollaDiagnosticRow row)
        {
            var line = new VisualElement();
            line.AddToClassList("sorolla-debugmenu-matrix-row");

            var badge = new Label(SorollaDiagnostics.SeverityLabel(row.Severity));
            badge.AddToClassList("sorolla-debugmenu-severity-badge");
            badge.AddToClassList(BadgeSeverityClass(row.Severity));
            line.Add(badge);

            var textColumn = new VisualElement();
            textColumn.AddToClassList("sorolla-debugmenu-matrix-row-text");

            var name = new Label($"{row.Group} · {row.Name}");
            name.AddToClassList("sorolla-debugmenu-matrix-row-name");
            textColumn.Add(name);

            var detail = new Label(row.Detail) { enableRichText = false };
            detail.AddToClassList("sorolla-debugmenu-matrix-row-detail");
            textColumn.Add(detail);

            line.Add(textColumn);
            return line;
        }

        // ── SDK context + responsibility division ─────────────────────────

        static VisualElement BuildContextLine()
        {
            var contextLine = new Label(SorollaDiagnostics.BuildMenuContextLine());
            contextLine.AddToClassList("sorolla-debugmenu-context-line");
            return contextLine;
        }

        // ── FIX THESE (studio-owned) ──────────────────────────────────────

        VisualElement BuildFixTheseSection(List<SorollaDiagnosticRow> rows)
        {
            var section = new VisualElement();
            section.Add(BuildActionGroupTitle("FIX THESE"));

            int shown = 0;
            foreach (SorollaDiagnosticRow row in SortedForAttention(rows, SorollaRowOwner.Studio))
            {
                section.Add(BuildIssueRow(row));
                shown++;
            }

            if (shown == 0)
                section.Add(BuildNothingToFixCard());

            return section;
        }

        // ── SEND TO SOROLLA (SDK-owned) ───────────────────────────────────

        // Returns null when there is nothing for Sorolla: a studio should never see an SDK section that
        // only ever says "all good" - it is noise about someone else's work.
        VisualElement BuildSendToSorollaSection(List<SorollaDiagnosticRow> rows)
        {
            List<SorollaDiagnosticRow> sorollaRows = SortedForAttention(rows, SorollaRowOwner.Sorolla);
            if (sorollaRows.Count == 0)
                return null;

            var section = new VisualElement();
            section.Add(BuildActionGroupTitle("SEND TO SOROLLA"));

            var note = new Label("These are SDK-side, not your game. Use Copy report at the bottom and send it to Sorolla.");
            note.AddToClassList("sorolla-debugmenu-note");
            note.AddToClassList("sorolla-debugmenu-note-info");
            section.Add(note);

            foreach (SorollaDiagnosticRow row in sorollaRows)
                section.Add(BuildIssueRow(row));

            return section;
        }

        // One rule decides BOTH the hero count and what is listed: a row that does not drive the verdict is
        // never rendered as an issue (a red row nobody counts reads as FAILING while the hero says otherwise -
        // DR-C4-FIXTHESE), and coverage/TO DO facts live in TEST YOUR GAME, never in FIX THESE.
        static List<SorollaDiagnosticRow> SortedForAttention(
            List<SorollaDiagnosticRow> rows, SorollaRowOwner owner)
        {
            var picked = new List<SorollaDiagnosticRow>();
            foreach (SorollaDiagnosticRow row in rows)
                if (SorollaDiagnostics.DrivesHealth(row) && SorollaDiagnostics.NeedsAttention(row.Severity) &&
                    SorollaDiagnostics.OwnerOf(row) == owner)
                    picked.Add(row);
            picked.Sort((a, b) => SeverityRank(b.Severity).CompareTo(SeverityRank(a.Severity)));
            return picked;
        }

        // ── TEST YOUR GAME (session coverage as to-dos) ───────────────────

        VisualElement BuildTestYourGameSection()
        {
            var section = new VisualElement();
            section.Add(BuildActionGroupTitle("TEST YOUR GAME"));

            var card = new VisualElement();
            card.AddToClassList("sorolla-debugmenu-matrix-card");
            foreach (SorollaMenuMatrixRow row in SorollaDiagnostics.BuildCoverageMatrixRows())
                card.Add(BuildCoverageRow(row));
            section.Add(card);

            return section;
        }

        VisualElement BuildCoverageRow(SorollaMenuMatrixRow row)
        {
            var line = new VisualElement();
            line.AddToClassList("sorolla-debugmenu-matrix-row");

            var badge = new Label(row.Exercised ? "DONE" : "TO DO");
            badge.AddToClassList("sorolla-debugmenu-severity-badge");
            badge.AddToClassList(row.Exercised
                ? "sorolla-debugmenu-badge-pass"
                : "sorolla-debugmenu-badge-wait");
            line.Add(badge);

            var textColumn = new VisualElement();
            textColumn.AddToClassList("sorolla-debugmenu-matrix-row-text");

            var name = new Label(row.Name);
            name.AddToClassList("sorolla-debugmenu-matrix-row-name");
            textColumn.Add(name);

            // Exercised: show the cell fact. Not exercised: show the how-to-trigger hint, so a to-do row
            // states the tester's next action instead of a bare status word.
            var detail = new Label(row.Exercised ? row.Cell : row.Hint);
            detail.AddToClassList("sorolla-debugmenu-matrix-row-detail");
            textColumn.Add(detail);

            // The diagnostics model owns action applicability. This UI only renders the supplied action.
            if ((!row.Exercised || row.Action == QaActionRegistry.ResetConsent) && row.Action != null)
            {
                var button = new Button(() => RunActionAndRefresh(row.Action)) { text = row.ActionLabel };
                button.AddToClassList("sorolla-debugmenu-action-button");
                button.AddToClassList("sorolla-debugmenu-action-button-ghost");
                textColumn.Add(button);
            }

            line.Add(textColumn);
            return line;
        }

        // ── Footer ────────────────────────────────────────────────────────

        VisualElement BuildReportFooter()
        {
            var footer = new VisualElement();

            var copyReport = new Button(() => GUIUtility.systemCopyBuffer = BuildCopyReportText())
            {
                text = "Copy report",
            };
            copyReport.AddToClassList("sorolla-debugmenu-action-button");
            copyReport.AddToClassList("sorolla-debugmenu-action-button-primary");
            footer.Add(copyReport);

            var bridge = new Label(QaBridgeServer.IsArmed
                ? $"QA bridge: 127.0.0.1:{QaBridgeServer.Port}"
                : "QA bridge: not running");
            bridge.AddToClassList("sorolla-debugmenu-context-line");
            footer.Add(bridge);

            return footer;
        }

        /// <summary>The one support payload this screen produces: the verdict, what needs attention, and
        /// the full SDK state behind it. A studio sends this, not a choice between two overlapping copies.</summary>
        static string BuildCopyReportText()
        {
            var rows = new List<SorollaDiagnosticRow>(64);
            SorollaDiagnostics.BuildRows(rows);
            SorollaVitalsVerdictReport verdict = SorollaDiagnostics.ComputeVerdict(rows);
            var sb = new StringBuilder(4096);
            sb.Append(SorollaDiagnostics.VerdictWord(verdict)).Append(" — FAIL ").Append(verdict.Fail)
                .Append(" · WARN ").Append(verdict.Warn).Append(" · WAIT ").Append(verdict.Wait)
                .Append(" · PASS ").Append(verdict.Pass).AppendLine();
            sb.AppendLine(SorollaDiagnostics.BuildMenuContextLine());
            sb.AppendLine(SorollaDiagnostics.BuildMenuCoverageLine(out _));
            sb.AppendLine();
            sb.AppendLine(SorollaDiagnostics.BuildProblemsSummary());
            sb.Append(SorollaDiagnostics.BuildQaStateSummary());
            return sb.ToString();
        }

        static VisualElement BuildNothingToFixCard()
        {
            var card = new VisualElement();
            card.AddToClassList("sorolla-debugmenu-empty-card");

            var checkCircle = new Label("✓");
            checkCircle.AddToClassList("sorolla-debugmenu-empty-check");
            card.Add(checkCircle);

            var title = new Label("No setup issues observed");
            title.AddToClassList("sorolla-debugmenu-empty-title");
            card.Add(title);

            SorollaDiagnostics.BuildMenuCoverageLine(out bool thin);
            if (thin)
            {
                var warnNote = new Label("This build still has untested paths. Complete the remaining "
                    + "TEST YOUR GAME checks, then re-check.");
                warnNote.AddToClassList("sorolla-debugmenu-note");
                warnNote.AddToClassList("sorolla-debugmenu-note-warn");
                card.Add(warnNote);
            }

            var infoNote = new Label("Some vendor failures are only visible in native device logs; this menu "
                + "shows what the SDK can verify from inside the app.");
            infoNote.AddToClassList("sorolla-debugmenu-note");
            infoNote.AddToClassList("sorolla-debugmenu-note-info");
            card.Add(infoNote);

            return card;
        }

        // ── Row anatomy (migrated from the deleted Issues pane) ───────────

        // The generic shape for a row whose producer supplied no diagnosis. Producers are expected to supply
        // WHY/SIGNAL/FIX (see SorollaDiagnostics.Diagnoses.cs); this is the last resort, and it routes through
        // an affordance a studio can ALWAYS see: the footer's own Copy report button. The SEND TO SOROLLA
        // section is absent whenever no Sorolla-owned row exists.
        const string UnknownSignal = "—";
        const string UnknownFix = "Not diagnosable from inside the app. Use \"Copy report\" at the bottom "
            + "of this screen and send it to Sorolla.";

        VisualElement BuildIssueRow(SorollaDiagnosticRow row)
        {
            (string why, string signal, string fix) diagnosis = row.HasStructuredDiagnosis
                ? (row.Why, row.Signal, row.Fix)
                : (string.IsNullOrEmpty(row.Detail) ? "No detail recorded." : row.Detail, UnknownSignal, UnknownFix);

            var container = new VisualElement();
            container.AddToClassList("sorolla-debugmenu-issue-row");
            container.AddToClassList(RowSeverityClass(row.Severity));

            var collapsed = new Button();
            collapsed.AddToClassList("sorolla-debugmenu-row-button");
            collapsed.AddToClassList("sorolla-debugmenu-issue-row-collapsed");

            var badge = new Label(SorollaDiagnostics.SeverityLabel(row.Severity));
            badge.AddToClassList("sorolla-debugmenu-severity-badge");
            badge.AddToClassList(BadgeSeverityClass(row.Severity));
            collapsed.Add(badge);

            var textColumn = new VisualElement();
            textColumn.AddToClassList("sorolla-debugmenu-matrix-row-text");
            var name = new Label(row.Name) { enableRichText = false };
            name.AddToClassList("sorolla-debugmenu-issue-name");
            textColumn.Add(name);

            var detail = new Label(SafeFirstLine(row.Detail)) { enableRichText = false };
            detail.AddToClassList("sorolla-debugmenu-issue-detail");
            textColumn.Add(detail);
            collapsed.Add(textColumn);

            var key = (row.Group, row.Name);
            bool isExpanded = _expandedReportRows.Contains(key);
            var chevron = new Label(isExpanded ? "⌄" : "›");
            chevron.AddToClassList("sorolla-debugmenu-issue-chevron");
            collapsed.Add(chevron);

            container.Add(collapsed);

            VisualElement expanded = BuildExpandedDiagnosis(diagnosis);
            expanded.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(expanded);

            collapsed.clicked += () =>
            {
                bool nowExpanded = expanded.style.display == DisplayStyle.None;
                expanded.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = nowExpanded ? "⌄" : "›";
                if (nowExpanded) _expandedReportRows.Add(key);
                else _expandedReportRows.Remove(key);
            };

            return container;
        }

        static VisualElement BuildExpandedDiagnosis((string why, string signal, string fix) diagnosis)
        {
            var block = new VisualElement();
            block.AddToClassList("sorolla-debugmenu-diagnosis");

            block.Add(BuildDiagnosisLine("WHY", diagnosis.why, "sorolla-debugmenu-diagnosis-why"));
            block.Add(BuildDiagnosisLine("SIGNAL", diagnosis.signal, "sorolla-debugmenu-diagnosis-signal"));
            block.Add(BuildDiagnosisLine("FIX", diagnosis.fix, "sorolla-debugmenu-diagnosis-fix"));

            var copyOne = new Button(() => GUIUtility.systemCopyBuffer = BuildDiagnosisCopyText(diagnosis))
            {
                text = "Copy diagnosis",
            };
            copyOne.AddToClassList("sorolla-debugmenu-action-button");
            copyOne.AddToClassList("sorolla-debugmenu-action-button-ghost");
            block.Add(copyOne);

            return block;
        }

        static VisualElement BuildDiagnosisLine(string key, string value, string keyClass)
        {
            var line = new VisualElement();
            line.AddToClassList("sorolla-debugmenu-diagnosis-line");

            var keyLabel = new Label(key);
            keyLabel.AddToClassList("sorolla-debugmenu-diagnosis-key");
            keyLabel.AddToClassList(keyClass);
            line.Add(keyLabel);

            var valueLabel = new Label(value) { enableRichText = false };
            valueLabel.AddToClassList("sorolla-debugmenu-diagnosis-value");
            line.Add(valueLabel);

            return line;
        }

        static string BuildDiagnosisCopyText((string why, string signal, string fix) diagnosis)
        {
            var sb = new StringBuilder(256);
            sb.Append("WHY: ").AppendLine(diagnosis.why);
            sb.Append("SIGNAL: ").AppendLine(diagnosis.signal);
            sb.Append("FIX: ").Append(diagnosis.fix);
            return sb.ToString();
        }

        static string SafeFirstLine(string detail)
        {
            if (string.IsNullOrEmpty(detail)) return "";
            int newline = detail.IndexOf('\n');
            return newline >= 0 ? detail.Substring(0, newline) : detail;
        }

        static int SeverityRank(SorollaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case SorollaDiagnosticSeverity.Fail: return 3;
                case SorollaDiagnosticSeverity.Warning: return 2;
                case SorollaDiagnosticSeverity.Waiting: return 1;
                default: return 0;
            }
        }

        static string RowSeverityClass(SorollaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case SorollaDiagnosticSeverity.Fail: return "sorolla-debugmenu-row-fail";
                case SorollaDiagnosticSeverity.Warning: return "sorolla-debugmenu-row-warn";
                default: return "sorolla-debugmenu-row-neutral";
            }
        }

        static string BadgeSeverityClass(SorollaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case SorollaDiagnosticSeverity.Fail: return "sorolla-debugmenu-badge-fail";
                case SorollaDiagnosticSeverity.Warning: return "sorolla-debugmenu-badge-warn";
                case SorollaDiagnosticSeverity.Waiting: return "sorolla-debugmenu-badge-wait";
                case SorollaDiagnosticSeverity.Pass: return "sorolla-debugmenu-badge-pass";
                default: return "sorolla-debugmenu-badge-info";
            }
        }
    }
}
