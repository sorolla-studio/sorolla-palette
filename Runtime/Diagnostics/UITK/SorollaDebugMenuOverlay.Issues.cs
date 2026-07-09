using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Issues tab (mockups 01/02, spec 2.2 + section 3). Display-only: rows are the same
    // SorollaDiagnosticRow list the header counts and the IMGUI Vitals tab already use.
    //
    // Judgment call (escalate-worthy but resolved locally, stated for the report): the flat list
    // includes BOTH Required and Observed rows at FAIL/WARN/WAIT, not just Required. Spec 2.2 says
    // "every FAIL/WARN/WAIT row" without qualifying Kind, and Observed rows (e.g. ad load failures
    // surfaced via Activity/Ads groups) are exactly the kind of thing a studio dev needs to see on
    // their to-do list. The header VERDICT still uses Required-only counts (ComputeMenuHealthCounts),
    // matching the existing IMGUI convention - only the Issues list widens.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        internal VisualElement BuildIssuesTab(List<SorollaDiagnosticRow> rows)
        {
            var pane = new VisualElement();
            pane.AddToClassList("sorolla-debugmenu-issues-pane");

            List<SorollaDiagnosticRow> issues = rows
                .Where(row => SorollaDiagnostics.NeedsAttention(row.Severity))
                .OrderByDescending(row => SeverityRank(row.Severity))
                .ToList();

            if (issues.Count == 0)
            {
                pane.Add(BuildEmptyState());
                return pane;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sorolla-debugmenu-issues-scroll");
            foreach (SorollaDiagnosticRow row in issues)
                scroll.Add(BuildIssueRow(row));
            pane.Add(scroll);

            var copyAll = new Button(() => GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildProblemsSummary())
            {
                text = "Copy problems",
            };
            copyAll.AddToClassList("sorolla-debugmenu-action-button");
            copyAll.AddToClassList("sorolla-debugmenu-action-button-primary");
            pane.Add(copyAll);

            return pane;
        }

        static VisualElement BuildIssueRow(SorollaDiagnosticRow row)
        {
            SorollaDebugMenuDiagnosis diagnosis = SorollaDebugMenuDiagnosisMapper.Map(row);

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

        static VisualElement BuildExpandedDiagnosis(SorollaDebugMenuDiagnosis diagnosis)
        {
            var block = new VisualElement();
            block.AddToClassList("sorolla-debugmenu-diagnosis");

            block.Add(BuildDiagnosisLine("WHY", diagnosis.Why, "sorolla-debugmenu-diagnosis-why"));
            block.Add(BuildDiagnosisLine("SIGNAL", diagnosis.Signal, "sorolla-debugmenu-diagnosis-signal"));
            block.Add(BuildDiagnosisLine("FIX", diagnosis.Fix, "sorolla-debugmenu-diagnosis-fix"));

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

        static VisualElement BuildEmptyState()
        {
            var card = new VisualElement();
            card.AddToClassList("sorolla-debugmenu-empty-card");

            var checkCircle = new Label("✓");
            checkCircle.AddToClassList("sorolla-debugmenu-empty-check");
            card.Add(checkCircle);

            var title = new Label("No issues detected");
            title.AddToClassList("sorolla-debugmenu-empty-title");
            card.Add(title);

            SorollaDiagnostics.BuildMenuCoverageLine(out bool thin);
            if (thin)
            {
                var warnNote = new Label("Coverage is thin: consent never resolved and 0 events were seen. "
                    + "HEALTHY only covers what this session exercised — play a level, trigger an ad, then re-check.");
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

        static string BuildDiagnosisCopyText(SorollaDebugMenuDiagnosis diagnosis)
        {
            var sb = new StringBuilder(256);
            sb.Append("WHY: ").AppendLine(diagnosis.Why);
            sb.Append("SIGNAL: ").AppendLine(diagnosis.Signal);
            sb.Append("FIX: ").Append(diagnosis.Fix);
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
