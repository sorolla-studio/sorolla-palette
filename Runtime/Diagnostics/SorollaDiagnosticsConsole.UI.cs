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
            if (_activeTab == ConsoleTab.Vitals)
                DrawHealthSummary();
            GUILayout.Space(6f * _uiScale);
        }

        void DrawTitleBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sorolla Vitals", _titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", _buttonStyle, GUILayout.Width(96f * _uiScale), GUILayout.Height(ButtonHeight())))
                SetVisible(false);
            GUILayout.EndHorizontal();
        }

        void DrawReportActions()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", _buttonStyle, GUILayout.Width(108f * _uiScale), GUILayout.Height(ButtonHeight())))
                SorollaDiagnostics.RefreshIdentifiers();
            if (GUILayout.Button("Copy Report", _buttonStyle, GUILayout.Width(124f * _uiScale), GUILayout.Height(ButtonHeight())))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildSummary();
            if (GUILayout.Button("Copy Problems", _buttonStyle, GUILayout.Width(148f * _uiScale), GUILayout.Height(ButtonHeight())))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildProblemsSummary();
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
            DrawCountBadge("FAIL", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Fail)], SorollaDiagnosticSeverity.Fail);
            DrawCountBadge("WARN", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Warning)], SorollaDiagnosticSeverity.Warning);
            DrawCountBadge("WAIT", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Waiting)], SorollaDiagnosticSeverity.Waiting);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            DrawCountBadge("PASS", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Pass)], SorollaDiagnosticSeverity.Pass);
            DrawCountBadge("INFO", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Info)], SorollaDiagnosticSeverity.Info);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(6f * _uiScale);
        }

        void DrawNeedsAttention()
        {
            if (_problemCount == 0)
                return;

            int shown = 0;
            GUILayout.BeginVertical(_summaryStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Needs attention", _sectionStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy Problems", _buttonStyle, GUILayout.Width(148f * _uiScale), GUILayout.Height(ButtonHeight())))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildProblemsSummary();
            GUILayout.EndHorizontal();

            DrawAttentionGroup("FAIL", SorollaDiagnosticSeverity.Fail, ref shown);
            DrawAttentionGroup("WARN", SorollaDiagnosticSeverity.Warning, ref shown);
            DrawAttentionGroup("WAIT", SorollaDiagnosticSeverity.Waiting, ref shown);

            if (shown == 0)
                GUILayout.Label("No FAIL / WARN / WAIT diagnostics observed.", _detailStyle);

            GUILayout.EndVertical();
            GUILayout.Space(8f * _uiScale);
        }

        void DrawAttentionGroup(string label, SorollaDiagnosticSeverity severity, ref int shown)
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

                DrawAttentionRow(row, shown);
                shown++;
            }
        }

        void DrawAttentionRow(SorollaDiagnosticRow row, int rowIndex)
        {
            DrawDiagnosticRow(row, rowIndex, true);
            if (!SorollaDiagnostics.IsRuntimeProblemRow(row)) return;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Open Console", _buttonStyle, GUILayout.Width(136f * _uiScale), GUILayout.Height(CompactButtonHeight())))
            {
                _activeTab = ConsoleTab.Console;
                _consoleFilter = ConsoleFilter.Problems;
                _scroll = Vector2.zero;
            }
            GUILayout.EndHorizontal();
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
            if (GUILayout.Button(ready ? "Show" : "Probe", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                onClick();
            GUILayout.EndHorizontal();
        }

        void DrawEventActions()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Event", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                TrackVitalsTestEvent();
            if (GUILayout.Button("Copy Console", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildConsoleSummary();
            if (GUILayout.Button("Clear Console", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
            {
                SorollaDiagnostics.ClearEventLog();
                SorollaDiagnostics.ClearRuntimeProblems();
                _expandedConsoleRows.Clear();
                _expandedRuntimeProblems.Clear();
            }
            GUILayout.EndHorizontal();
        }

        void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            DrawTabButton(ConsoleTab.Vitals, "Vitals");
            DrawTabButton(ConsoleTab.Console, "Console " + (_events.Count + _runtimeProblems.Count));
            DrawTabButton(ConsoleTab.Actions, "Actions");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawTabButton(ConsoleTab tab, string label)
        {
            GUIStyle style = _activeTab == tab ? _activeTabStyle : _tabStyle;
            if (GUILayout.Button(label, style, GUILayout.Width(126f * _uiScale), GUILayout.Height(TabHeight())))
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
                case ConsoleTab.Console:
                    DrawConsoleTab();
                    break;
                case ConsoleTab.Actions:
                    DrawActions();
                    break;
                default:
                    DrawVitalsTab();
                    break;
            }
        }

        void DrawVitalsTab()
        {
            DrawNeedsAttention();
            DrawFilters();
            DrawRows();
        }

        void DrawFilters()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Show", _detailStyle, GUILayout.Width(44f * _uiScale));
            DrawFilterButton(RowFilter.All, "All");
            DrawFilterButton(RowFilter.Problems, "Problems");
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
            DrawSummaryBadge(SorollaDiagnostics.KindLabel(row.Kind), SorollaDiagnosticSeverity.Info, 44f);
            GUILayout.Label(row.Name, _rowNameStyle, GUILayout.Width(compact ? 150f * _uiScale : 170f * _uiScale));
            GUILayout.Label(row.Detail, compact ? _miniDetailStyle : _detailStyle);
            GUILayout.EndHorizontal();
        }

        void DrawConsoleTab()
        {
            DrawConsoleToolbar();

            if (_events.Count == 0 && _runtimeProblems.Count == 0)
            {
                GUILayout.Label("No SDK events or runtime problems observed yet.", _detailStyle);
                return;
            }

            if (_consoleFilter != ConsoleFilter.Events)
                DrawRuntimeProblemList();

            if (_consoleFilter != ConsoleFilter.Problems)
                DrawEventList();
        }

        void DrawConsoleToolbar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Console   {_runtimeProblems.Count} problems / {_events.Count} events", _sectionStyle);
            GUILayout.FlexibleSpace();
            _showNewestEventsFirst = GUILayout.Toggle(_showNewestEventsFirst, "Newest first", _detailStyle, GUILayout.Width(112f * _uiScale));
            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(84f * _uiScale), GUILayout.Height(ButtonHeight())))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildConsoleSummary();
            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.Width(84f * _uiScale), GUILayout.Height(ButtonHeight())))
            {
                SorollaDiagnostics.ClearEventLog();
                SorollaDiagnostics.ClearRuntimeProblems();
                _expandedConsoleRows.Clear();
                _expandedRuntimeProblems.Clear();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawConsoleFilterButton(ConsoleFilter.All, "All");
            DrawConsoleFilterButton(ConsoleFilter.Problems, "Problems");
            DrawConsoleFilterButton(ConsoleFilter.Events, "Events");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        void DrawRuntimeProblemList()
        {
            if (_runtimeProblems.Count == 0)
            {
                if (_consoleFilter == ConsoleFilter.Problems)
                    GUILayout.Label("No runtime problems observed yet.", _detailStyle);
                return;
            }

            GUILayout.Space(6f * _uiScale);
            GUILayout.Label("Runtime problems", _sectionStyle);

            if (_showNewestEventsFirst)
            {
                for (int i = _runtimeProblems.Count - 1; i >= 0; i--)
                    DrawRuntimeProblemEntry(_runtimeProblems[i], _runtimeProblems.Count - 1 - i);
            }
            else
            {
                for (int i = 0; i < _runtimeProblems.Count; i++)
                    DrawRuntimeProblemEntry(_runtimeProblems[i], i);
            }
        }

        void DrawEventList()
        {
            if (_events.Count == 0)
            {
                if (_consoleFilter == ConsoleFilter.Events)
                    GUILayout.Label("No SDK events observed yet.", _detailStyle);
                return;
            }

            GUILayout.Space(6f * _uiScale);
            GUILayout.Label("SDK events", _sectionStyle);

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

        void DrawRuntimeProblemEntry(SorollaRuntimeProblem problem, int rowIndex)
        {
            GUIStyle rowStyle = problem.Severity == SorollaDiagnosticSeverity.Fail ? _rowProblemStyle :
                problem.Severity == SorollaDiagnosticSeverity.Warning ? _rowWarningStyle :
                rowIndex % 2 == 0 ? _rowStyle : _rowAltStyle;
            bool expanded = _expandedRuntimeProblems.Contains(problem.Id);

            GUILayout.BeginVertical(rowStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(expanded ? "[-]" : "[+]", _buttonStyle, GUILayout.Width(42f * _uiScale), GUILayout.Height(CompactButtonHeight())))
            {
                if (expanded)
                    _expandedRuntimeProblems.Remove(problem.Id);
                else
                    _expandedRuntimeProblems.Add(problem.Id);
            }
            GUILayout.Label(SorollaDiagnostics.FormatEventTime(problem.LastTimeSeconds), _badgeStyle, GUILayout.Width(70f * _uiScale));
            DrawSummaryBadge(SorollaDiagnostics.SeverityLabel(problem.Severity), problem.Severity, 66f);
            DrawSummaryBadge(problem.Source, SorollaDiagnosticSeverity.Info, 96f);
            GUILayout.Label($"{problem.Type} x{problem.Count}", _rowNameStyle, GUILayout.Width(180f * _uiScale));
            GUILayout.Label(problem.Message, _detailStyle);
            GUILayout.EndHorizontal();

            if (expanded)
                DrawRuntimeProblemDetails(problem);

            GUILayout.EndVertical();
            GUILayout.Space(3f * _uiScale);
        }

        void DrawRuntimeProblemDetails(SorollaRuntimeProblem problem)
        {
            DrawKeyValue("Top frame", problem.TopFrame);
            DrawKeyValue("First seen", SorollaDiagnostics.FormatEventTime(problem.FirstTimeSeconds));
            DrawKeyValue("Last seen", SorollaDiagnostics.FormatEventTime(problem.LastTimeSeconds));
            DrawKeyValue("Stack", problem.StackTrace);
        }

        void DrawEventEntry(SorollaDiagnosticEventLogEntry entry, int rowIndex)
        {
            GUIStyle rowStyle = rowIndex % 2 == 0 ? _rowStyle : _rowAltStyle;
            bool expanded = _expandedConsoleRows.Contains(entry.Id);

            GUILayout.BeginVertical(rowStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(expanded ? "[-]" : "[+]", _buttonStyle, GUILayout.Width(42f * _uiScale), GUILayout.Height(CompactButtonHeight())))
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

        void DrawKeyValue(string key, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, _miniDetailStyle, GUILayout.Width(130f * _uiScale));
            GUILayout.Label(string.IsNullOrEmpty(value) ? "None" : value, _miniDetailStyle);
            GUILayout.EndHorizontal();
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
            GUILayout.Label(label, _badgeStyle, GUILayout.Width(width * _uiScale), GUILayout.Height(BadgeHeight()));
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
            if (GUILayout.Button(label, style, GUILayout.Width((label.Length > 5 ? 104f : 82f) * _uiScale), GUILayout.Height(ButtonHeight())))
                _filter = filter;
        }

        void DrawConsoleFilterButton(ConsoleFilter filter, string label)
        {
            bool selected = _consoleFilter == filter;
            GUIStyle style = selected ? _selectedButtonStyle : _buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Width((label.Length > 6 ? 112f : 86f) * _uiScale), GUILayout.Height(ButtonHeight())))
                _consoleFilter = filter;
        }

        float ButtonHeight()
        {
            return 36f * _uiScale;
        }

        float CompactButtonHeight()
        {
            return 30f * _uiScale;
        }

        float TabHeight()
        {
            return 38f * _uiScale;
        }

        float BadgeHeight()
        {
            return 26f * _uiScale;
        }
    }
}
