using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole : MonoBehaviour
    {
        const int RequiredTapCount = 5;
        const float TapWindowSeconds = 3f;
        const float ScrollDragThresholdPixels = 10f;

        static SorollaDiagnosticsConsole s_instance;

        const int SeverityCount = 5;

        enum RowFilter
        {
            All,
            Issues,
            Fail,
            Warn,
            Wait,
            Pass,
        }

        enum ConsoleTab
        {
            Issues,
            Overview,
            Console,
            Actions,
        }

        sealed class SectionState
        {
            public bool Expanded;
            public bool Initialized;
            public bool UserToggled;
            public bool HadIssue;
        }

        sealed class SectionSummary
        {
            int _info;
            int _waiting;
            int _pass;
            int _warning;
            int _fail;

            public bool Active { get; private set; }
            public string CountsText { get; private set; } = string.Empty;

            public bool HasIssues => _fail > 0 || _warning > 0 || _waiting > 0;

            public void Reset()
            {
                _info = 0;
                _waiting = 0;
                _pass = 0;
                _warning = 0;
                _fail = 0;
                Active = false;
                CountsText = string.Empty;
            }

            public void Add(SorollaDiagnosticSeverity severity)
            {
                Active = true;
                switch (severity)
                {
                    case SorollaDiagnosticSeverity.Waiting:
                        _waiting++;
                        break;
                    case SorollaDiagnosticSeverity.Pass:
                        _pass++;
                        break;
                    case SorollaDiagnosticSeverity.Warning:
                        _warning++;
                        break;
                    case SorollaDiagnosticSeverity.Fail:
                        _fail++;
                        break;
                    default:
                        _info++;
                        break;
                }
            }

            public void RebuildCountsText()
            {
                string text = string.Empty;
                AppendCount(ref text, _pass, "pass");
                AppendCount(ref text, _waiting, "wait");
                AppendCount(ref text, _warning, "warn");
                AppendCount(ref text, _fail, "fail");
                AppendCount(ref text, _info, "info");
                CountsText = text;
            }

            public bool HasRowsFor(RowFilter filter)
            {
                if (!Active) return false;

                switch (filter)
                {
                    case RowFilter.Issues:
                        return HasIssues;
                    case RowFilter.Fail:
                        return _fail > 0;
                    case RowFilter.Warn:
                        return _warning > 0;
                    case RowFilter.Wait:
                        return _waiting > 0;
                    case RowFilter.Pass:
                        return _pass > 0;
                    default:
                        return true;
                }
            }
        }

        readonly List<SorollaDiagnosticRow> _rows = new List<SorollaDiagnosticRow>(80);
        readonly List<SorollaDiagnosticEventLogEntry> _events = new List<SorollaDiagnosticEventLogEntry>(40);
        readonly Dictionary<string, SectionState> _sectionStates = new Dictionary<string, SectionState>();
        readonly Dictionary<string, SectionSummary> _sectionSummaries = new Dictionary<string, SectionSummary>();
        readonly HashSet<int> _expandedConsoleRows = new HashSet<int>();
        readonly List<int> _staleExpandedConsoleRows = new List<int>(8);
        readonly int[] _severityCounts = new int[SeverityCount];
        int _issueCount;
        Vector2 _scroll;
        bool _visible;
        int _tapCount;
        float _firstTapTime;
        float _uiScale = 1f;
        int _scrollTouchId = -1;
        bool _scrollTouchDragging;
        bool _ignoreSectionToggleAfterDrag;
        Vector2 _scrollTouchStartPosition;
        Vector2 _lastScrollTouchPosition;
        RowFilter _filter = RowFilter.All;
        ConsoleTab _activeTab = ConsoleTab.Issues;
        bool _filterInitialized;
        bool _activeTabInitialized;
        bool _showNewestEventsFirst = true;
        GUIStyle _titleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _sectionButtonStyle;
        GUIStyle _rowNameStyle;
        GUIStyle _detailStyle;
        GUIStyle _miniDetailStyle;
        GUIStyle _badgeStyle;
        GUIStyle _buttonStyle;
        GUIStyle _selectedButtonStyle;
        GUIStyle _tabStyle;
        GUIStyle _activeTabStyle;
        GUIStyle _panelStyle;
        GUIStyle _summaryStyle;
        GUIStyle _rowStyle;
        GUIStyle _rowAltStyle;
        GUIStyle _rowProblemStyle;
        GUIStyle _rowWarningStyle;
        Texture2D _panelBackground;
        Texture2D _summaryBackground;
        Texture2D _rowBackground;
        Texture2D _rowAltBackground;
        Texture2D _rowProblemBackground;
        Texture2D _rowWarningBackground;
        Texture2D _sectionBackground;
        Texture2D _buttonBackground;
        Texture2D _buttonActiveBackground;
        Texture2D _buttonSelectedBackground;
        Texture2D _tabBackground;
        Texture2D _activeTabBackground;
        Texture2D _passBackground;
        Texture2D _warnBackground;
        Texture2D _failBackground;
        Texture2D _waitBackground;
        Texture2D _infoBackground;

        internal static void Ensure(GameObject host)
        {
            SorollaDiagnostics.EnsureLogBridge();
            if (s_instance != null) return;

            s_instance = host.GetComponent<SorollaDiagnosticsConsole>();
            if (s_instance == null)
                s_instance = host.AddComponent<SorollaDiagnosticsConsole>();
        }

        internal static void Show()
        {
            EnsureStandalone();
            s_instance.SetVisible(true);
        }

        internal static void Hide()
        {
            if (s_instance == null) return;
            s_instance.SetVisible(false);
        }

        internal static void Toggle()
        {
            EnsureStandalone();
            s_instance.ToggleVisible();
        }

        static void EnsureStandalone()
        {
            if (s_instance != null) return;

            var go = new GameObject("[Palette SDK Diagnostics]");
            try
            {
                DontDestroyOnLoad(go);
            }
            catch
            {
                // The bootstrapper will retry persistence if this is called very early.
            }

            Ensure(go);
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;
            SorollaDiagnostics.EnsureLogBridge();
            SorollaDiagnostics.InstallUnityLogSink();
        }

        void OnDestroy()
        {
            bool wasSingleton = s_instance == this;
            if (wasSingleton)
            {
                s_instance = null;
                SorollaDiagnostics.UninstallUnityLogSink();
            }

            DestroyStyleResources();
        }

        void Update()
        {
            SorollaDiagnostics.UpdatePolling();
            UpdateUiScale();
            CheckKeyboardToggle();

            if (_visible)
                CheckTouchScroll();
            else
                CheckTouchToggle();
        }

        void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();
            SorollaDiagnostics.BuildRows(_rows);
            SorollaDiagnostics.CopyEventLog(_events);
            RefreshDerivedState();

            GUI.depth = -1000;
            Rect safeArea = Screen.safeArea;
            float margin = 8f * _uiScale;
            Rect area = new Rect(safeArea.x + margin, Screen.height - safeArea.yMax + margin,
                safeArea.width - 2f * margin, safeArea.height - 2f * margin);

            GUI.DrawTexture(area, GetPanelBackground(), ScaleMode.StretchToFill, true);
            GUILayout.BeginArea(area, _panelStyle);
            DrawHeader();
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawActiveTab();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void ToggleVisible()
        {
            SetVisible(!_visible);
        }

        void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                if (visible)
                    SorollaDiagnostics.RefreshIdentifiers();
                return;
            }

            _visible = visible;
            _tapCount = 0;

            if (visible)
                SorollaDiagnostics.RefreshIdentifiers();
            else
                ResetTouchScroll();
        }

    }
}
