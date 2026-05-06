using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void DrawHeader()
        {
            DrawTitleBar();
            DrawReportActions();
            DrawBuildContext();

            GUILayout.Space(8f * _uiScale);
            DrawTabs();
            if (_activeTab == ConsoleTab.Issues || _activeTab == ConsoleTab.Overview)
                DrawHealthSummary();
            GUILayout.Space(6f * _uiScale);
        }

        void DrawTitleBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sorolla Vitals", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", _buttonStyle, GUILayout.Width(76f * _uiScale)))
                SetVisible(false);
            GUILayout.EndHorizontal();
        }

        void DrawReportActions()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", _buttonStyle, GUILayout.Width(88f * _uiScale)))
                SorollaDiagnostics.RefreshIdentifiers();
            if (GUILayout.Button("Copy Report", _buttonStyle, GUILayout.Width(104f * _uiScale)))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildSummary();
            if (GUILayout.Button("Copy Issues", _buttonStyle, GUILayout.Width(104f * _uiScale)))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildIssuesSummary();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawBuildContext()
        {
            GUILayout.Label($"{Application.identifier}  |  {Application.platform}  |  {(Debug.isDebugBuild ? "Development" : "Release")} build", _detailStyle);
        }

        void DrawHealthSummary()
        {
            GUILayout.BeginVertical(_summaryStyle);
            GUILayout.BeginHorizontal();
            DrawSummaryBadge(OverallLabel(), OverallSeverity());
            DrawCountBadge("FAIL", _severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Fail)], SorollaDiagnosticSeverity.Fail);
            DrawCountBadge("WARN", _severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Warning)], SorollaDiagnosticSeverity.Warning);
            DrawCountBadge("WAIT", _severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Waiting)], SorollaDiagnosticSeverity.Waiting);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawCountBadge("PASS", _severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Pass)], SorollaDiagnosticSeverity.Pass);
            DrawCountBadge("INFO", _severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Info)], SorollaDiagnosticSeverity.Info);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(6f * _uiScale);
        }

        void DrawIssuesTab()
        {
            if (_issueCount == 0)
            {
                GUILayout.BeginVertical(_summaryStyle);
                GUILayout.Label("Issues", _sectionStyle);
                GUILayout.Label("No FAIL / WARN / WAIT diagnostics observed.", _detailStyle);
                GUILayout.EndVertical();
                return;
            }

            DrawIssues();
        }

        void DrawIssues()
        {
            int shown = 0;
            GUILayout.BeginVertical(_summaryStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Issues", _sectionStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy Issues", _buttonStyle, GUILayout.Width(104f * _uiScale)))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildIssuesSummary();
            GUILayout.EndHorizontal();
            DrawIssueGroup("FAIL", SorollaDiagnosticSeverity.Fail, ref shown);
            DrawIssueGroup("WARN", SorollaDiagnosticSeverity.Warning, ref shown);
            DrawIssueGroup("WAIT", SorollaDiagnosticSeverity.Waiting, ref shown);
            GUILayout.EndVertical();
            GUILayout.Space(8f * _uiScale);
        }

        void DrawIssueGroup(string label, SorollaDiagnosticSeverity severity, ref int shown)
        {
            bool wroteHeader = false;
            foreach (SorollaDiagnosticRow row in _rows)
            {
                if (row.Severity != severity) continue;
                if (!wroteHeader)
                {
                    GUILayout.Label(label, _detailStyle);
                    wroteHeader = true;
                }

                DrawDiagnosticRow(row, shown, true);
                shown++;
            }
        }

        void DrawActions()
        {
            GUILayout.BeginVertical(_summaryStyle);
            GUILayout.Label("Actions", _sectionStyle);

            DrawAdAction("Rewarded", Palette.IsRewardedAdReady, TestRewardedAd);
            DrawAdAction("Interstitial", Palette.IsInterstitialAdReady, TestInterstitialAd);
            DrawEventActions();

            GUILayout.EndVertical();
            GUILayout.Space(8f * _uiScale);
        }

        void DrawAdAction(string label, bool ready, System.Action onClick)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            DrawSummaryBadge(label, ready ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting, 104f);
            GUILayout.Label(ready ? "Ready to show" : "Not ready - probe", _detailStyle, GUILayout.Width(128f * _uiScale));
            if (GUILayout.Button(ready ? "Show" : "Probe", _buttonStyle, GUILayout.ExpandWidth(true)))
                onClick();
            GUILayout.EndHorizontal();
        }

        void DrawEventActions()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Event", _buttonStyle, GUILayout.ExpandWidth(true)))
                TrackVitalsTestEvent();
            if (GUILayout.Button("Copy Console", _buttonStyle, GUILayout.ExpandWidth(true)))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildEventLogSummary();
            if (GUILayout.Button("Clear Console", _buttonStyle, GUILayout.ExpandWidth(true)))
            {
                SorollaDiagnostics.ClearEventLog();
                _expandedConsoleRows.Clear();
            }
            GUILayout.EndHorizontal();
        }

        void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            DrawTabButton(ConsoleTab.Issues, "Issues " + _issueCount);
            DrawTabButton(ConsoleTab.Overview, "Overview");
            DrawTabButton(ConsoleTab.Console, "Console " + _events.Count);
            DrawTabButton(ConsoleTab.Actions, "Actions");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawTabButton(ConsoleTab tab, string label)
        {
            GUIStyle style = _activeTab == tab ? _activeTabStyle : _tabStyle;
            if (GUILayout.Button(label, style, GUILayout.Width(112f * _uiScale), GUILayout.Height(28f * _uiScale)))
            {
                if (_activeTab != tab)
                {
                    _activeTab = tab;
                    _scroll = Vector2.zero;
                }
            }
        }

        void DrawActiveTab()
        {
            switch (_activeTab)
            {
                case ConsoleTab.Issues:
                    DrawIssuesTab();
                    break;
                case ConsoleTab.Console:
                    DrawEventConsole();
                    break;
                case ConsoleTab.Actions:
                    DrawActions();
                    break;
                default:
                    DrawOverviewTab();
                    break;
            }
        }

        void DrawOverviewTab()
        {
            DrawFilters();
            DrawRows();
        }

        void DrawFilters()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Show", _detailStyle, GUILayout.Width(44f * _uiScale));
            DrawFilterButton(RowFilter.All, "All");
            DrawFilterButton(RowFilter.Issues, "Issues");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(44f * _uiScale);
            DrawFilterButton(RowFilter.Fail, "Fail");
            DrawFilterButton(RowFilter.Warn, "Warn");
            DrawFilterButton(RowFilter.Wait, "Wait");
            DrawFilterButton(RowFilter.Pass, "Pass");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawRows()
        {
            int rowIndex = 0;

            for (int i = 0; i < _rows.Count;)
            {
                string group = _rows[i].Group;
                int groupStart = i;
                while (i < _rows.Count && _rows[i].Group == group)
                    i++;
                int groupEnd = i;

                if (!SectionHasVisibleRows(group))
                    continue;

                GUILayout.Space(8f * _uiScale);
                if (!DrawSectionHeader(group))
                    continue;

                for (int rowIndexInGroup = groupStart; rowIndexInGroup < groupEnd; rowIndexInGroup++)
                {
                    SorollaDiagnosticRow row = _rows[rowIndexInGroup];
                    if (!MatchesFilter(row))
                        continue;

                    DrawDiagnosticRow(row, rowIndex, false);
                    rowIndex++;
                }
            }
        }

        bool DrawSectionHeader(string group)
        {
            SectionState state = GetSectionState(group);
            GUILayout.BeginHorizontal();
            string prefix = state.Expanded ? "[-]" : "[+]";
            string counts = SectionCountsText(group);
            if (GUILayout.Button($"{prefix} {group}   {counts}", _sectionButtonStyle))
            {
                if (!_ignoreSectionToggleAfterDrag)
                {
                    state.Expanded = !state.Expanded;
                    state.UserToggled = true;
                }
            }
            GUILayout.EndHorizontal();
            return state.Expanded;
        }

        void DrawDiagnosticRow(SorollaDiagnosticRow row, int rowIndex, bool compact)
        {
            GUIStyle rowStyle = RowStyle(row, rowIndex, compact);

            GUILayout.BeginHorizontal(rowStyle);
            DrawSummaryBadge(SorollaDiagnostics.SeverityLabel(row.Severity), row.Severity, 66f);
            GUILayout.Label(row.Name, _rowNameStyle, GUILayout.Width(compact ? 170f * _uiScale : 190f * _uiScale));
            GUILayout.Label(row.Detail, compact ? _miniDetailStyle : _detailStyle);
            GUILayout.EndHorizontal();
        }

        void DrawEventConsole()
        {
            DrawEventToolbar();

            if (_events.Count == 0)
            {
                GUILayout.Label("No SDK events observed yet.", _detailStyle);
                return;
            }

            DrawEventList();
        }

        void DrawEventToolbar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Console   {_events.Count} events", _sectionStyle);
            GUILayout.FlexibleSpace();
            _showNewestEventsFirst = GUILayout.Toggle(_showNewestEventsFirst, "Newest first", _detailStyle, GUILayout.Width(112f * _uiScale));
            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(72f * _uiScale)))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildEventLogSummary();
            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.Width(72f * _uiScale)))
            {
                SorollaDiagnostics.ClearEventLog();
                _expandedConsoleRows.Clear();
            }
            GUILayout.EndHorizontal();
        }

        void DrawEventList()
        {
            if (_showNewestEventsFirst)
            {
                for (int i = _events.Count - 1; i >= 0; i--)
                    DrawEventEntry(_events[i], _events.Count - 1 - i);
            }
            else
            {
                for (int i = 0; i < _events.Count; i++)
                    DrawEventEntry(_events[i], i);
            }
        }

        void DrawEventEntry(SorollaDiagnosticEventLogEntry entry, int rowIndex)
        {
            GUIStyle rowStyle = rowIndex % 2 == 0 ? _rowStyle : _rowAltStyle;
            bool expanded = _expandedConsoleRows.Contains(entry.Id);

            GUILayout.BeginVertical(rowStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(expanded ? "[-]" : "[+]", _buttonStyle, GUILayout.Width(34f * _uiScale), GUILayout.Height(22f * _uiScale)))
            {
                if (expanded)
                    _expandedConsoleRows.Remove(entry.Id);
                else
                    _expandedConsoleRows.Add(entry.Id);
            }
            GUILayout.Label(SorollaDiagnostics.FormatEventTime(entry.TimeSeconds), _badgeStyle, GUILayout.Width(70f * _uiScale));
            DrawSummaryBadge(entry.Source, SorollaDiagnosticSeverity.Info, 96f);
            GUILayout.Label(entry.Name, _rowNameStyle);
            GUILayout.EndHorizontal();
            if (expanded)
                DrawConsoleDetails(entry);
            GUILayout.EndVertical();
            GUILayout.Space(3f * _uiScale);
        }

        void DrawConsoleDetails(SorollaDiagnosticEventLogEntry entry)
        {
            if (entry.PayloadLines.Length == 0)
            {
                GUILayout.Label("No payload", _miniDetailStyle);
                return;
            }

            for (int i = 0; i < entry.PayloadLines.Length; i++)
            {
                SorollaDiagnosticPayloadLine line = entry.PayloadLines[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(line.Key, _miniDetailStyle, GUILayout.Width(130f * _uiScale));
                GUILayout.Label(line.Value, _miniDetailStyle);
                GUILayout.EndHorizontal();
            }
        }

        GUIStyle RowStyle(SorollaDiagnosticRow row, int rowIndex, bool compact)
        {
            if (row.Severity == SorollaDiagnosticSeverity.Warning)
                return _rowWarningStyle;
            if (row.Severity == SorollaDiagnosticSeverity.Fail)
                return _rowProblemStyle;

            return rowIndex % 2 == 0 ? _rowStyle : _rowAltStyle;
        }

        void DrawSummaryBadge(string label, SorollaDiagnosticSeverity severity, float width = 112f)
        {
            Texture2D oldBackground = _badgeStyle.normal.background;
            Color oldTextColor = _badgeStyle.normal.textColor;
            _badgeStyle.normal.background = SeverityBackground(severity);
            _badgeStyle.normal.textColor = BadgeTextColor(severity);
            GUILayout.Label(label, _badgeStyle, GUILayout.Width(width * _uiScale), GUILayout.Height(22f * _uiScale));
            _badgeStyle.normal.background = oldBackground;
            _badgeStyle.normal.textColor = oldTextColor;
        }

        void DrawCountBadge(string label, int count, SorollaDiagnosticSeverity severity)
        {
            DrawSummaryBadge($"{count} {label}", severity, 82f);
        }

        void DrawFilterButton(RowFilter filter, string label)
        {
            bool selected = _filter == filter;
            GUIStyle style = selected ? _selectedButtonStyle : _buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Width((label.Length > 5 ? 92f : 68f) * _uiScale)))
                _filter = filter;
        }
    }
}
