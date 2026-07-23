using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // The ONE studio-visible pane (2026-07-20 studio-UX simplification). It replaces the Overview +
    // Issues + coverage-matrix trio: a studio launches the game, opens Vitals, and reads one report -
    // green or not, with a row per thing to fix. Sorolla-owned rows are not hidden, they are routed to
    // their own "send to Sorolla" section, because a studio cannot fix SDK internals and should not be
    // asked to. Display-only, same BuildRows()/CaptureQaState fact pipeline as every other pane.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        // 5 taps on the SDK context line unlock the internal (Sorolla) view. Deliberately obscure and
        // deliberately not a build-time flag: the full depth ships in every build, it is just not the
        // studio's default surface.
        const int InternalUnlockTapCount = 5;
        const float InternalUnlockWindowSeconds = 2f;

        int _internalUnlockTaps;
        float _internalUnlockFirstTapTime;

        internal VisualElement BuildReportTab(List<SorollaDiagnosticRow> rows)
        {
            var pane = new VisualElement();
            pane.AddToClassList("sorolla-debugmenu-issues-pane");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            // Vertical MODE alone does not stop Unity from laying out the horizontal scroller (verified
            // live 2026-07-20: display=Flex, no horizontal overflow), and it renders as a light desktop
            // scrollbar band across the pane's bottom edge. USS cannot fix it - ScrollView writes the
            // scroller's display as an INLINE style, which beats any stylesheet rule.
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("sorolla-debugmenu-issues-scroll");
            var host = new VisualElement();
            scroll.Add(host);
            pane.Add(scroll);

            SorollaVitalsVerdictReport verdict = SorollaDiagnostics.ComputeVerdict(rows);

            host.Add(BuildVerdictHero(verdict));
            host.Add(BuildContextLine());
            host.Add(BuildFixTheseSection(rows));
            VisualElement sorollaSection = BuildSendToSorollaSection(rows);
            if (sorollaSection != null)
                host.Add(sorollaSection);
            host.Add(BuildTestYourGameSection());
            host.Add(BuildReportFooter(verdict));

            return pane;
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

        // ── SDK context + responsibility division ─────────────────────────

        // The SDK context line, and the 5-tap target that unlocks the internal view. The
        // responsibility/certification sentence that used to sit under it is deleted: a studio cannot act on
        // who certifies SDK internals, and zero-leverage info does not render on a studio surface (scope
        // lens, 2026-07-20). That framing lives in the report export / agent payload.
        VisualElement BuildContextLine()
        {
            var contextLine = new Label(SorollaDiagnostics.BuildMenuContextLine());
            contextLine.AddToClassList("sorolla-debugmenu-context-line");
            contextLine.RegisterCallback<ClickEvent>(_ => RegisterInternalUnlockTap());
            return contextLine;
        }

        void RegisterInternalUnlockTap()
        {
            float now = Time.unscaledTime;
            if (now - _internalUnlockFirstTapTime > InternalUnlockWindowSeconds)
            {
                _internalUnlockFirstTapTime = now;
                _internalUnlockTaps = 0;
            }

            _internalUnlockTaps++;
            if (_internalUnlockTaps < InternalUnlockTapCount) return;

            _internalUnlockTaps = 0;
            ToggleInternalMode();
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
                var button = new Button(() => RunActionAndRefreshReport(row.Action)) { text = row.ActionLabel };
                button.AddToClassList("sorolla-debugmenu-action-button");
                button.AddToClassList("sorolla-debugmenu-action-button-ghost");
                textColumn.Add(button);
            }

            line.Add(textColumn);
            return line;
        }

        void RunActionAndRefreshReport(string registryAction)
        {
            QaActionRegistry.TryInvoke(registryAction, null, out _);
            RefreshDiagnosticViews();
        }

        // ── Footer ────────────────────────────────────────────────────────

        VisualElement BuildReportFooter(in SorollaVitalsVerdictReport verdict)
        {
            var footer = new VisualElement();

            string word = SorollaDiagnostics.VerdictWord(verdict);
            int fail = verdict.Fail, warn = verdict.Warn, wait = verdict.Wait, pass = verdict.Pass;
            var copyReport = new Button(() =>
                GUIUtility.systemCopyBuffer = BuildCopyReportText(word, fail, warn, wait, pass))
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
        static string BuildCopyReportText(string verdictWord, int fail, int warn, int wait, int pass)
        {
            var sb = new StringBuilder(4096);
            sb.Append(verdictWord).Append(" — FAIL ").Append(fail).Append(" · WARN ").Append(warn)
                .Append(" · WAIT ").Append(wait).Append(" · PASS ").Append(pass).AppendLine();
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

            var title = new Label("Nothing to fix in your game's setup");
            title.AddToClassList("sorolla-debugmenu-empty-title");
            card.Add(title);

            SorollaDiagnostics.BuildMenuCoverageLine(out bool thin);
            if (thin)
            {
                var warnNote = new Label("Coverage is thin: this build has not yet been played through a level "
                    + "and an ad watched to the end. A clean report only covers what was actually exercised — "
                    + "work the TEST YOUR GAME list, then re-check.");
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
        // section is absent whenever no Sorolla-owned row exists, and the 5-tap console is internal.
        const string UnknownSignal = "—";
        const string UnknownFix = "Not diagnosable from inside the app. Use \"Copy report\" at the bottom "
            + "of this screen and send it to Sorolla.";

        static VisualElement BuildIssueRow(SorollaDiagnosticRow row)
        {
            (string why, string signal, string fix) diagnosis = row.HasStructuredDiagnosis
                ? (row.Why, row.Signal, row.Fix)
                : (string.IsNullOrEmpty(row.Detail) ? "No detail recorded." : row.Detail, UnknownSignal, UnknownFix);

            var container = new VisualElement();
            container.AddToClassList("sorolla-debugmenu-issue-row");
            container.AddToClassList(RowSeverityClass(row.Severity));

            var collapsed = new VisualElement();
            collapsed.AddToClassList("sorolla-debugmenu-issue-row-collapsed");

            var badge = new Label(SorollaDiagnostics.SeverityLabel(row.Severity));
            badge.AddToClassList("sorolla-debugmenu-severity-badge");
            badge.AddToClassList(BadgeSeverityClass(row.Severity));
            collapsed.Add(badge);

            var name = new Label(row.Name);
            name.AddToClassList("sorolla-debugmenu-issue-name");
            collapsed.Add(name);

            var detail = new Label(SafeFirstLine(row.Detail));
            detail.AddToClassList("sorolla-debugmenu-issue-detail");
            collapsed.Add(detail);

            var chevron = new Label("›");
            chevron.AddToClassList("sorolla-debugmenu-issue-chevron");
            collapsed.Add(chevron);

            container.Add(collapsed);

            VisualElement expanded = BuildExpandedDiagnosis(diagnosis);
            expanded.style.display = DisplayStyle.None;
            container.Add(expanded);

            collapsed.RegisterCallback<ClickEvent>(_ =>
            {
                bool nowExpanded = expanded.style.display == DisplayStyle.None;
                expanded.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = nowExpanded ? "⌄" : "›";
            });

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

            var valueLabel = new Label(value);
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
            string firstLine = newline >= 0 ? detail.Substring(0, newline) : detail;
            const int maxLength = 60;
            return firstLine.Length <= maxLength ? firstLine : firstLine.Substring(0, maxLength - 1) + "…";
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
