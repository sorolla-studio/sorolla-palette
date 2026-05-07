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
            if (GUILayout.Button("Refresh", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                SorollaDiagnostics.RefreshIdentifiers();
            if (GUILayout.Button("Copy Report", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildSummary();
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
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            float countWidth = HealthCountBadgeWidth();
            DrawCountBadge("FAIL", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Fail)], SorollaDiagnosticSeverity.Fail, countWidth);
            DrawCountBadge("WARN", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Warning)], SorollaDiagnosticSeverity.Warning, countWidth);
            DrawCountBadge("WAIT", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Waiting)], SorollaDiagnosticSeverity.Waiting, countWidth);
            DrawCountBadge("PASS", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Pass)], SorollaDiagnosticSeverity.Pass, countWidth);
            DrawCountBadge("INFO", _healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Info)], SorollaDiagnosticSeverity.Info, countWidth);
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
            GUILayout.Label("Needs attention", _sectionStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Problems", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
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
            GUILayout.BeginVertical(_rowStyle);
            GUILayout.BeginHorizontal();
            DrawSummaryBadge(label, ready ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting, 104f);
            GUILayout.Label(ready ? "Ready to show" : "Not ready - probe", _detailStyle);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(ready ? "Show" : "Probe", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                onClick();
            GUILayout.EndVertical();
            GUILayout.Space(4f * _uiScale);
        }

        void DrawEventActions()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Event", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                TrackVitalsTestEvent();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
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
            GUILayout.EndHorizontal();
        }

        void DrawTabButton(ConsoleTab tab, string label)
        {
            GUIStyle style = _activeTab == tab ? _activeTabStyle : _tabStyle;
            if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true), GUILayout.Height(TabHeight())))
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
            DrawFilterButton(RowFilter.All, "All", HalfButtonWidth());
            DrawFilterButton(RowFilter.Problems, "Problems", HalfButtonWidth());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            float width = QuarterButtonWidth();
            DrawFilterButton(RowFilter.Fail, "Fail", width);
            DrawFilterButton(RowFilter.Warn, "Warn", width);
            DrawFilterButton(RowFilter.Wait, "Wait", width);
            DrawFilterButton(RowFilter.Pass, "Pass", width);
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

            GUILayout.BeginVertical(rowStyle);
            GUILayout.BeginHorizontal();
            DrawSummaryBadge(SorollaDiagnostics.SeverityLabel(row.Severity), row.Severity, 66f);
            DrawSummaryBadge(SorollaDiagnostics.KindLabel(row.Kind), SorollaDiagnosticSeverity.Info, 44f);
            GUILayout.Label(row.Name, _rowNameStyle);
            GUILayout.EndHorizontal();
            GUILayout.Label(row.Detail, compact ? _miniDetailStyle : _detailStyle);
            GUILayout.EndVertical();
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
            GUILayout.Label($"Console   {_runtimeProblems.Count} problems / {_events.Count} events", _sectionStyle);

            GUILayout.BeginHorizontal();
            _showNewestEventsFirst = GUILayout.Toggle(_showNewestEventsFirst, "Newest first", _detailStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildConsoleSummary();
            if (GUILayout.Button("Clear", _buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(ButtonHeight())))
            {
                SorollaDiagnostics.ClearEventLog();
                SorollaDiagnostics.ClearRuntimeProblems();
                _expandedConsoleRows.Clear();
                _expandedRuntimeProblems.Clear();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            float width = ThirdButtonWidth();
            DrawConsoleFilterButton(ConsoleFilter.All, "All", width);
            DrawConsoleFilterButton(ConsoleFilter.Problems, "Problems", width);
            DrawConsoleFilterButton(ConsoleFilter.Events, "Events", width);
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
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawSummaryBadge(problem.Source, SorollaDiagnosticSeverity.Info, 96f);
            GUILayout.Label($"{problem.Type} x{problem.Count}", _rowNameStyle);
            GUILayout.EndHorizontal();
            if (!expanded && !string.IsNullOrEmpty(problem.Message))
                GUILayout.Label(problem.Message, _miniDetailStyle);

            if (expanded)
                DrawRuntimeProblemDetails(problem);

            GUILayout.EndVertical();
            GUILayout.Space(3f * _uiScale);
        }

        void DrawRuntimeProblemDetails(SorollaRuntimeProblem problem)
        {
            DrawKeyValue("Message", problem.Message);
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
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawSummaryBadge(entry.Source, SorollaDiagnosticSeverity.Info, 96f);
            GUILayout.Label(entry.Name, _rowNameStyle);
            GUILayout.EndHorizontal();
            if (!expanded && !string.IsNullOrEmpty(entry.Payload) && entry.Payload != "{}")
                GUILayout.Label(entry.Payload, _miniDetailStyle);

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
                GUILayout.BeginVertical();
                GUILayout.Label(line.Key, _miniDetailStyle);
                GUILayout.Label(line.Value, _miniDetailStyle);
                GUILayout.EndVertical();
            }
        }

        void DrawKeyValue(string key, string value)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(key, _miniDetailStyle);
            GUILayout.Label(string.IsNullOrEmpty(value) ? "None" : value, _miniDetailStyle);
            GUILayout.EndVertical();
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

        void DrawCountBadge(string label, int count, SorollaDiagnosticSeverity severity, float width)
        {
            DrawSummaryBadge($"{count} {label}", severity, width / _uiScale);
        }

        void DrawFilterButton(RowFilter filter, string label, float width)
        {
            bool selected = _filter == filter;
            GUIStyle style = selected ? _selectedButtonStyle : _buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Width(width), GUILayout.Height(ButtonHeight())))
                _filter = filter;
        }

        void DrawConsoleFilterButton(ConsoleFilter filter, string label, float width)
        {
            bool selected = _consoleFilter == filter;
            GUIStyle style = selected ? _selectedButtonStyle : _buttonStyle;
            if (GUILayout.Button(label, style, GUILayout.Width(width), GUILayout.Height(ButtonHeight())))
                _consoleFilter = filter;
        }

        float HalfButtonWidth()
        {
            return Mathf.Max(72f * _uiScale, (_contentWidth - 6f * _uiScale) * 0.5f);
        }

        float HealthCountBadgeWidth()
        {
            return Mathf.Max(56f * _uiScale, (_contentWidth - 24f * _uiScale) * 0.2f);
        }

        float ThirdButtonWidth()
        {
            return Mathf.Max(66f * _uiScale, (_contentWidth - 12f * _uiScale) / 3f);
        }

        float QuarterButtonWidth()
        {
            return Mathf.Max(58f * _uiScale, (_contentWidth - 18f * _uiScale) * 0.25f);
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
