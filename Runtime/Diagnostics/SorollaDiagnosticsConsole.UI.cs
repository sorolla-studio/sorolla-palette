using System.Text;
using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        const float DefaultHeaderRefreshButtonWidth = 83.4f;
        const float DefaultHeaderCopyButtonWidth = 62.5f;
        const float DefaultHeaderCloseButtonWidth = 66.8f;
        const float DefaultDiagnosticSeverityBadgeWidth = 82.9f;
        const float DefaultDiagnosticKindBadgeWidth = 52.1f;
        const float DefaultDiagnosticDetailGapWidth = 8f;
        const float DefaultMinHealthCountBadgeWidth = 57.5f;
        const float DefaultMinThirdButtonWidth = 68.8f;
        const float DefaultMinQuarterButtonWidth = 55f;
        const float DefaultButtonHeight = 30.8f;
        const float DefaultCompactButtonHeight = 30.1f;
        const float DefaultSectionHeaderHeight = 30f;
        const float DefaultTabHeight = 30.1f;
        const float DefaultBadgeHeight = 25.2f;
        const float DefaultOverallBadgeWidth = 120.2f;
        const float DefaultAdActionBadgeWidth = 103.6f;
        const float DefaultExpandButtonWidth = 32f;
        const float DefaultTimeBadgeWidth = 68f;
        const float DefaultConsoleSeverityBadgeWidth = 86f;
        const float DefaultSourceBadgeWidth = 117.3f;
        const float DefaultActionButtonWidth = 108f;
        const float DefaultHeaderTabsGap = 5f;
        const float DefaultHeaderBodyGap = 5f;
        const float DefaultHealthSummaryBottomGap = 5f;
        const float DefaultActionsBottomGap = 5f;
        const float DefaultAdActionBottomGap = 5f;
        const float DefaultSectionTopGap = 5f;
        const float DefaultConsoleSectionTopGap = 5f;
        const float DefaultConsoleRowGap = 3f;
        const float DefaultHealthCountGapBudget = 25.1f;
        const float DefaultThirdButtonGapBudget = 18f;
        const float DefaultQuarterButtonGapBudget = 20f;

        float _headerRefreshButtonWidth = DefaultHeaderRefreshButtonWidth;
        float _headerCopyButtonWidth = DefaultHeaderCopyButtonWidth;
        float _headerCloseButtonWidth = DefaultHeaderCloseButtonWidth;
        float _diagnosticSeverityBadgeWidth = DefaultDiagnosticSeverityBadgeWidth;
        float _diagnosticKindBadgeWidth = DefaultDiagnosticKindBadgeWidth;
        float _diagnosticDetailGapWidth = DefaultDiagnosticDetailGapWidth;
        float _minHealthCountBadgeWidth = DefaultMinHealthCountBadgeWidth;
        float _minThirdButtonWidth = DefaultMinThirdButtonWidth;
        float _minQuarterButtonWidth = DefaultMinQuarterButtonWidth;
        float _buttonHeight = DefaultButtonHeight;
        float _compactButtonHeight = DefaultCompactButtonHeight;
        float _sectionHeaderHeight = DefaultSectionHeaderHeight;
        float _tabHeight = DefaultTabHeight;
        float _badgeHeight = DefaultBadgeHeight;
        float _overallBadgeWidth = DefaultOverallBadgeWidth;
        float _adActionBadgeWidth = DefaultAdActionBadgeWidth;
        float _expandButtonWidth = DefaultExpandButtonWidth;
        float _timeBadgeWidth = DefaultTimeBadgeWidth;
        float _consoleSeverityBadgeWidth = DefaultConsoleSeverityBadgeWidth;
        float _sourceBadgeWidth = DefaultSourceBadgeWidth;
        float _actionButtonWidth = DefaultActionButtonWidth;
        float _headerTabsGap = DefaultHeaderTabsGap;
        float _headerBodyGap = DefaultHeaderBodyGap;
        float _healthSummaryBottomGap = DefaultHealthSummaryBottomGap;
        float _actionsBottomGap = DefaultActionsBottomGap;
        float _adActionBottomGap = DefaultAdActionBottomGap;
        float _sectionTopGap = DefaultSectionTopGap;
        float _consoleSectionTopGap = DefaultConsoleSectionTopGap;
        float _consoleRowGap = DefaultConsoleRowGap;
        float _healthCountGapBudget = DefaultHealthCountGapBudget;
        float _thirdButtonGapBudget = DefaultThirdButtonGapBudget;
        float _quarterButtonGapBudget = DefaultQuarterButtonGapBudget;

        void DrawHeader()
        {
            DrawTitleBar();
            DrawBuildContext();

            GUILayout.Space(_headerTabsGap * _uiScale);
            DrawTabs();
            if (_activeTab == ConsoleTab.Vitals)
                DrawHealthSummary();
            GUILayout.Space(_headerBodyGap * _uiScale);
        }

        void DrawTitleBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sorolla Vitals", _titleStyle);
            GUILayout.FlexibleSpace();
            if (DrawCompactButton("Refresh", _headerRefreshButtonWidth * _uiScale))
            {
                SorollaDiagnostics.RefreshIdentifiers();
                RequestDiagnosticsRefresh();
            }
            if (DrawCompactButton("Copy", _headerCopyButtonWidth * _uiScale))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildSummary();
            if (DrawCompactButton("Close", _headerCloseButtonWidth * _uiScale))
                SetVisible(false);
            GUILayout.EndHorizontal();
        }

        void DrawBuildContext()
        {
            string context = string.IsNullOrEmpty(_headerContextLine)
                ? SorollaDiagnostics.BuildHeaderContext()
                : _headerContextLine;
            GUILayout.Label($"{Application.identifier}  |  {Application.platform}  |  {(Debug.isDebugBuild ? "Dev" : "Release")} build", _miniDetailStyle);
            GUILayout.Label(context, _miniDetailStyle);
        }

        void DrawHealthSummary()
        {
            GUILayout.BeginVertical(_summaryStyle);
            GUILayout.BeginHorizontal();
            DrawSummaryBadge(OverallLabel(), OverallSeverity(), _overallBadgeWidth);
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
            GUILayout.Space(_healthSummaryBottomGap * _uiScale);
        }

        void DrawActions()
        {
            GUILayout.BeginVertical(_summaryStyle);
            GUILayout.Label("Actions", _sectionStyle);

            GUILayout.Label("Ads", _sectionStyle);
            DrawAdAction("Rewarded", Palette.IsRewardedAdReady, TestRewardedAd);
            DrawAdAction("Interstitial", Palette.IsInterstitialAdReady, TestInterstitialAd);
            GUILayout.Space(_sectionTopGap * _uiScale);

            GUILayout.Label("Consent", _sectionStyle);
            DrawActionRow("Privacy", Palette.PrivacyOptionsRequired ? "Options required" : "Options unavailable",
                Palette.PrivacyOptionsRequired ? SorollaDiagnosticSeverity.Warning : SorollaDiagnosticSeverity.Info,
                "Open", ShowPrivacyOptionsProbe);
            DrawActionRow("Consent", Palette.ConsentStatus.ToString(), Palette.CanRequestAds ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning,
                "Refresh", RefreshConsentProbe);
            DrawActionRow("Reset consent", "Re-run CMP (resets consent)", SorollaDiagnosticSeverity.Info,
                "Reset", ResetConsentProbe);
            GUILayout.Space(_sectionTopGap * _uiScale);

            GUILayout.Label("QA Bridge", _sectionStyle);
            bool bridgeArmed = QaBridgeServer.IsArmed;
            DrawActionRow("Bridge", bridgeArmed ? "Auto on 127.0.0.1:" + QaBridgeServer.Port : "Bind failed or unavailable",
                bridgeArmed ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Warning,
                bridgeArmed ? "Restart" : "Retry", RestartQaBridge);
            GUILayout.Space(_sectionTopGap * _uiScale);

            GUILayout.Label("Events", _sectionStyle);
            DrawActionButtons("Custom", TrackVitalsTestEvent, "Level Start", TrackVitalsLevelStart);
            DrawActionButtons("Level End", TrackVitalsLevelComplete, "Earn", TrackVitalsEconomyEarn);
            DrawActionButtons("Spend", TrackVitalsEconomySpend, null, null);

            GUILayout.EndVertical();
            GUILayout.Space(_actionsBottomGap * _uiScale);
        }

        void DrawAdAction(string label, bool ready, System.Action onClick)
        {
            DrawActionRow(label, ready ? "Ready to show" : "Not ready - probe",
                ready ? SorollaDiagnosticSeverity.Pass : SorollaDiagnosticSeverity.Waiting,
                ready ? "Show" : "Probe", onClick, _adActionBottomGap);
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
            if (DrawButton(label, style, TabHeight()))
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
            DrawFilters();
            DrawRows();
        }

        void DrawFilters()
        {
            GUILayout.BeginHorizontal();
            float width = ThirdButtonWidth();
            DrawFilterButton(RowFilter.All, "All", width);
            DrawFilterButton(RowFilter.Problems, "Problems", width);
            if (_problemCount > 0)
            {
                if (DrawCompactButton("Copy Problems", width))
                    GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildProblemsSummary();
            }
            else
            {
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            float severityWidth = QuarterButtonWidth();
            DrawFilterButton(RowFilter.Fail, "Fail", severityWidth);
            DrawFilterButton(RowFilter.Warn, "Warn", severityWidth);
            DrawFilterButton(RowFilter.Wait, "Wait", severityWidth);
            DrawFilterButton(RowFilter.Pass, "Pass", severityWidth);
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

                GUILayout.Space(_sectionTopGap * _uiScale);
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
            string label = string.IsNullOrEmpty(counts) ? $"{prefix} {group}" : $"{prefix} {group}  ·  {counts}";
            if (DrawButton(label, _sectionButtonStyle, SectionHeaderHeight()))
            {
                if (!_scrollDrag.IgnoreSectionToggleAfterDrag)
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
            GUIStyle rowStyle = RowStyle(row, rowIndex);

            BeginRowCard(rowStyle);
            DrawDiagnosticNameLine(row);
            DrawRowDetail(row.Detail, compact ? _miniDetailStyle : _detailStyle);
            EndRowCard();
        }

        void DrawDiagnosticNameLine(SorollaDiagnosticRow row)
        {
            GUILayout.BeginHorizontal();
            DrawSummaryBadge(SorollaDiagnostics.SeverityLabel(row.Severity), row.Severity, _diagnosticSeverityBadgeWidth);
            DrawSummaryBadge(SorollaDiagnostics.KindLabel(row.Kind), SorollaDiagnosticSeverity.Info, _diagnosticKindBadgeWidth);
            DrawInlineRowName(row.Name);
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
            GUILayout.Label($"Console   {_runtimeProblems.Count} problems / {_events.Count} events", _sectionStyle);

            GUILayout.BeginHorizontal();
            _showNewestEventsFirst = GUILayout.Toggle(_showNewestEventsFirst, "Newest first", _detailStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (DrawPrimaryButton("Copy"))
                GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildConsoleSummary();
            if (DrawPrimaryButton("Clear"))
            {
                SorollaDiagnostics.ClearEventLog();
                SorollaDiagnostics.ClearRuntimeProblems();
                _expandedConsoleRows.Clear();
                _expandedRuntimeProblems.Clear();
                RequestDiagnosticsRefresh();
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

            GUILayout.Space(_consoleSectionTopGap * _uiScale);
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

            GUILayout.Space(_consoleSectionTopGap * _uiScale);
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

            BeginRowCard(rowStyle);
            if (DrawConsoleTitleLine(expanded, problem.Source, $"{problem.Type} x{problem.Count}",
                    SorollaDiagnostics.FormatEventTime(problem.LastTimeSeconds), true,
                    SorollaDiagnostics.SeverityLabel(problem.Severity), problem.Severity,
                    BuildRuntimeProblemCopyText(problem)))
            {
                if (expanded)
                    _expandedRuntimeProblems.Remove(problem.Id);
                else
                    _expandedRuntimeProblems.Add(problem.Id);
            }

            if (!expanded)
                DrawRowDetail(problem.Message, _miniDetailStyle);

            if (expanded)
                DrawRuntimeProblemDetails(problem);

            EndRowCard(_consoleRowGap);
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

            BeginRowCard(rowStyle);
            if (DrawConsoleTitleLine(expanded, entry.Source, entry.Name,
                    SorollaDiagnostics.FormatEventTime(entry.TimeSeconds), false, string.Empty, SorollaDiagnosticSeverity.Info,
                    BuildEventCopyText(entry)))
            {
                if (expanded)
                    _expandedConsoleRows.Remove(entry.Id);
                else
                    _expandedConsoleRows.Add(entry.Id);
            }

            if (!expanded && entry.Payload != "{}")
                DrawRowDetail(entry.Payload, _miniDetailStyle);

            if (expanded)
                DrawConsoleDetails(entry);
            EndRowCard(_consoleRowGap);
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
                DrawKeyValue(line.Key, line.Value);
            }
        }

        void DrawKeyValue(string key, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, _miniDetailStyle, GUILayout.Width(ConsoleDetailKeyWidth()));
            GUILayout.Space(BadgeTextGap());
            GUILayout.Label(string.IsNullOrEmpty(value) ? "None" : value, _miniDetailStyle);
            GUILayout.EndHorizontal();
        }

        GUIStyle RowStyle(SorollaDiagnosticRow row, int rowIndex)
        {
            if (row.Severity == SorollaDiagnosticSeverity.Warning)
                return _rowWarningStyle;
            if (row.Severity == SorollaDiagnosticSeverity.Fail)
                return _rowProblemStyle;

            return rowIndex % 2 == 0 ? _rowStyle : _rowAltStyle;
        }

        void DrawSummaryBadge(string label, SorollaDiagnosticSeverity severity, float width = DefaultOverallBadgeWidth)
        {
            DrawSummaryBadgePixels(label, severity, width * _uiScale);
        }

        bool DrawSummaryBadgeButton(string label, SorollaDiagnosticSeverity severity, float width = DefaultOverallBadgeWidth)
        {
            return DrawSummaryBadgeButtonPixels(label, severity, width * _uiScale);
        }

        void DrawSummaryBadgePixels(string label, SorollaDiagnosticSeverity severity, float width, float height = -1f)
        {
            Texture2D oldBackground = _badgeStyle.normal.background;
            Color oldTextColor = _badgeStyle.normal.textColor;
            _badgeStyle.normal.background = SeverityBackground(severity);
            _badgeStyle.normal.textColor = BadgeTextColor(severity);
            GUILayout.Label(label, _badgeStyle, GUILayout.Width(width), GUILayout.Height(height > 0f ? height : BadgeHeight()));
            _badgeStyle.normal.background = oldBackground;
            _badgeStyle.normal.textColor = oldTextColor;
        }

        bool DrawSummaryBadgeButtonPixels(string label, SorollaDiagnosticSeverity severity, float width, float height = -1f)
        {
            Texture2D oldNormalBackground = _badgeStyle.normal.background;
            Texture2D oldHoverBackground = _badgeStyle.hover.background;
            Texture2D oldActiveBackground = _badgeStyle.active.background;
            Color oldNormalTextColor = _badgeStyle.normal.textColor;
            Color oldHoverTextColor = _badgeStyle.hover.textColor;
            Color oldActiveTextColor = _badgeStyle.active.textColor;

            Texture2D background = SeverityBackground(severity);
            Color textColor = BadgeTextColor(severity);
            _badgeStyle.normal.background = background;
            _badgeStyle.hover.background = background;
            _badgeStyle.active.background = background;
            _badgeStyle.normal.textColor = textColor;
            _badgeStyle.hover.textColor = textColor;
            _badgeStyle.active.textColor = textColor;
            bool clicked = GUILayout.Button(label, _badgeStyle, GUILayout.Width(width), GUILayout.Height(height > 0f ? height : BadgeHeight()));
            _badgeStyle.normal.background = oldNormalBackground;
            _badgeStyle.hover.background = oldHoverBackground;
            _badgeStyle.active.background = oldActiveBackground;
            _badgeStyle.normal.textColor = oldNormalTextColor;
            _badgeStyle.hover.textColor = oldHoverTextColor;
            _badgeStyle.active.textColor = oldActiveTextColor;
            return clicked;
        }

        void DrawCountBadge(string label, int count, SorollaDiagnosticSeverity severity, float width)
        {
            DrawSummaryBadgePixels($"{count} {label}", severity, width);
        }

        bool DrawButton(string label, GUIStyle style, float height, float width = -1f)
        {
            if (width > 0f)
                return GUILayout.Button(label, style, GUILayout.Width(width), GUILayout.Height(height));

            return GUILayout.Button(label, style, GUILayout.ExpandWidth(true), GUILayout.Height(height));
        }

        bool DrawPrimaryButton(string label, float width = -1f)
        {
            return DrawButton(label, _buttonStyle, ButtonHeight(), width);
        }

        bool DrawCompactButton(string label, float width = -1f)
        {
            return DrawButton(label, _buttonStyle, CompactButtonHeight(), width);
        }

        bool DrawExpandButton(bool expanded)
        {
            return DrawButton(expanded ? "[-]" : "[+]", _buttonStyle, BadgeHeight(), _expandButtonWidth * _uiScale);
        }

        void DrawActionRow(string label, string detail, SorollaDiagnosticSeverity severity, string buttonLabel,
            System.Action onClick, float bottomGap = 0f)
        {
            BeginRowCard(_rowStyle);
            GUILayout.BeginHorizontal();
            DrawSummaryBadge(label, severity, _adActionBadgeWidth);
            DrawInlineActionDetail(detail);
            if (!string.IsNullOrEmpty(buttonLabel))
            {
                GUILayout.FlexibleSpace();
                if (DrawCompactButton(buttonLabel, ActionButtonWidth()))
                    onClick?.Invoke();
            }
            GUILayout.EndHorizontal();
            EndRowCard(bottomGap);
        }

        void DrawActionButtons(string leftLabel, System.Action leftClick, string rightLabel, System.Action rightClick)
        {
            GUILayout.BeginHorizontal();
            if (DrawPrimaryButton(leftLabel))
                leftClick?.Invoke();
            if (!string.IsNullOrEmpty(rightLabel))
            {
                if (DrawPrimaryButton(rightLabel))
                    rightClick?.Invoke();
            }
            else
            {
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }

        void DrawInlineActionDetail(string detail)
        {
            GUILayout.Space(BadgeTextGap());
            GUILayout.Label(detail, _detailStyle, GUILayout.Height(BadgeHeight()));
        }

        bool DrawConsoleTitleLine(bool expanded, string source, string name, string time,
            bool hasSeverity, string severityLabel, SorollaDiagnosticSeverity severity, string copyText = null)
        {
            GUILayout.BeginHorizontal();
            if (DrawSummaryBadgeButton(source, SorollaDiagnosticSeverity.Info, _sourceBadgeWidth))
                GUIUtility.systemCopyBuffer = string.IsNullOrEmpty(copyText) ? name : copyText;
            DrawInlineRowName(name);
            if (hasSeverity)
                DrawSummaryBadge(severityLabel, severity, _consoleSeverityBadgeWidth);
            DrawTimeBadge(time);
            bool toggled = DrawExpandButton(expanded);
            GUILayout.EndHorizontal();
            return toggled;
        }

        void DrawTimeBadge(string time)
        {
            GUILayout.Label(time, _badgeStyle, GUILayout.Width(_timeBadgeWidth * _uiScale), GUILayout.Height(BadgeHeight()));
        }

        string BuildRuntimeProblemCopyText(SorollaRuntimeProblem problem)
        {
            var sb = new StringBuilder(512);
            AppendCopyLine(sb, "Time", SorollaDiagnostics.FormatEventTime(problem.LastTimeSeconds));
            AppendCopyLine(sb, "Source", problem.Source);
            AppendCopyLine(sb, "Severity", SorollaDiagnostics.SeverityLabel(problem.Severity));
            AppendCopyLine(sb, "Problem", $"{problem.Type} x{problem.Count}");
            AppendCopyLine(sb, "Message", problem.Message);
            AppendCopyLine(sb, "Top frame", problem.TopFrame);
            AppendCopyLine(sb, "First seen", SorollaDiagnostics.FormatEventTime(problem.FirstTimeSeconds));
            AppendCopyLine(sb, "Last seen", SorollaDiagnostics.FormatEventTime(problem.LastTimeSeconds));
            AppendCopyLine(sb, "Stack", problem.StackTrace);
            return sb.ToString();
        }

        string BuildEventCopyText(SorollaDiagnosticEventLogEntry entry)
        {
            var sb = new StringBuilder(256);
            AppendCopyLine(sb, "Time", SorollaDiagnostics.FormatEventTime(entry.TimeSeconds));
            AppendCopyLine(sb, "Source", entry.Source);
            AppendCopyLine(sb, "Event", entry.Name);

            if (entry.PayloadLines.Length > 0)
            {
                for (int i = 0; i < entry.PayloadLines.Length; i++)
                {
                    SorollaDiagnosticPayloadLine line = entry.PayloadLines[i];
                    AppendCopyLine(sb, line.Key, line.Value);
                }
            }
            else if (!string.IsNullOrEmpty(entry.Payload) && entry.Payload != "{}")
            {
                AppendCopyLine(sb, "Payload", entry.Payload);
            }

            return sb.ToString();
        }

        static void AppendCopyLine(StringBuilder sb, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            sb.Append(key);
            sb.Append(": ");
            sb.AppendLine(value);
        }

        void BeginRowCard(GUIStyle rowStyle)
        {
            GUILayout.BeginVertical(rowStyle);
        }

        void EndRowCard(float bottomGap = 0f)
        {
            GUILayout.EndVertical();
            if (bottomGap > 0f)
                GUILayout.Space(bottomGap * _uiScale);
        }

        void DrawRowDetail(string detail, GUIStyle style)
        {
            if (!string.IsNullOrEmpty(detail))
                GUILayout.Label(detail, style);
        }

        void DrawInlineRowName(string name)
        {
            GUILayout.Space(BadgeTextGap());
            GUILayout.Label(name, _rowNameInlineStyle, GUILayout.ExpandWidth(true), GUILayout.Height(BadgeHeight()));
        }

        float BadgeTextGap()
        {
            return _diagnosticDetailGapWidth * _uiScale;
        }

        void DrawFilterButton(RowFilter filter, string label, float width)
        {
            bool selected = _filter == filter;
            GUIStyle style = selected ? _selectedButtonStyle : _buttonStyle;
            if (DrawButton(label, style, CompactButtonHeight(), width))
                _filter = filter;
        }

        void DrawConsoleFilterButton(ConsoleFilter filter, string label, float width)
        {
            bool selected = _consoleFilter == filter;
            GUIStyle style = selected ? _selectedButtonStyle : _buttonStyle;
            if (DrawButton(label, style, CompactButtonHeight(), width))
                _consoleFilter = filter;
        }

        float HealthCountBadgeWidth()
        {
            return Mathf.Max(_minHealthCountBadgeWidth * _uiScale, (_contentWidth - _healthCountGapBudget * _uiScale) * 0.2f);
        }

        float ThirdButtonWidth()
        {
            return Mathf.Max(_minThirdButtonWidth * _uiScale, (_contentWidth - _thirdButtonGapBudget * _uiScale) / 3f);
        }

        float QuarterButtonWidth()
        {
            return Mathf.Max(_minQuarterButtonWidth * _uiScale, (_contentWidth - _quarterButtonGapBudget * _uiScale) * 0.25f);
        }

        float ConsoleDetailKeyWidth()
        {
            return Mathf.Min(_sourceBadgeWidth * _uiScale, _contentWidth * 0.32f);
        }

        float ActionButtonWidth()
        {
            return _actionButtonWidth * _uiScale;
        }

        float ButtonHeight()
        {
            return _buttonHeight * _uiScale;
        }

        float CompactButtonHeight()
        {
            return _compactButtonHeight * _uiScale;
        }

        float SectionHeaderHeight()
        {
            return _sectionHeaderHeight * _uiScale;
        }

        float TabHeight()
        {
            return _tabHeight * _uiScale;
        }

        float BadgeHeight()
        {
            return _badgeHeight * _uiScale;
        }
    }
}
