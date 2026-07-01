using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole : MonoBehaviour
    {
        const int RequiredTapCount = 5;
        const float TapWindowSeconds = 2f;
        const float FinalTapHoldSeconds = 0.8f;
        const float DiagnosticsRefreshIntervalSeconds = 0.2f;

        static SorollaDiagnosticsConsole s_instance;

        const int SeverityCount = 5;

        enum RowFilter
        {
            All,
            Problems,
            Fail,
            Warn,
            Wait,
            Pass,
        }

        enum ConsoleTab
        {
            Vitals,
            Console,
            Actions,
        }

        enum ConsoleFilter
        {
            All,
            Events,
            Problems,
        }

        sealed class SectionState
        {
            public bool Expanded;
            public bool Initialized;
            public bool UserToggled;
            public bool HadProblem;
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

            public bool HasProblems => _fail > 0 || _warning > 0 || _waiting > 0;

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
                    case RowFilter.Problems:
                        return HasProblems;
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
        readonly List<SorollaRuntimeProblem> _runtimeProblems = new List<SorollaRuntimeProblem>(20);
        readonly Dictionary<string, SectionState> _sectionStates = new Dictionary<string, SectionState>();
        readonly Dictionary<string, SectionSummary> _sectionSummaries = new Dictionary<string, SectionSummary>();
        readonly HashSet<int> _expandedConsoleRows = new HashSet<int>();
        readonly HashSet<int> _expandedRuntimeProblems = new HashSet<int>();
        readonly List<int> _staleExpandedConsoleRows = new List<int>(8);
        readonly int[] _severityCounts = new int[SeverityCount];
        readonly int[] _healthCounts = new int[SeverityCount];
        readonly SorollaConsoleScrollDrag _scrollDrag = new SorollaConsoleScrollDrag();
        int _problemCount;
        Vector2 _scroll;
        bool _visible;
        int _tapCount;
        float _firstTapTime;
        float _uiScale = 1f;
        float _contentWidth = 320f;
        float _nextDiagnosticsRefreshTime;
        bool _diagnosticsCacheDirty = true;
        bool _unlockHoldPointer;
        int _unlockHoldTouchId = -1;
        float _unlockHoldStartTime;
        RowFilter _filter = RowFilter.All;
        ConsoleTab _activeTab = ConsoleTab.Vitals;
        ConsoleFilter _consoleFilter = ConsoleFilter.All;
        bool _filterInitialized;
        bool _activeTabInitialized;
        bool _showNewestEventsFirst = true;
        string _headerContextLine = string.Empty;
        GUIStyle _titleStyle;
        GUIStyle _sectionStyle;
        GUIStyle _sectionButtonStyle;
        GUIStyle _rowNameStyle;
        GUIStyle _rowNameInlineStyle;
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
            CheckKeyboardToggle();

            if (_visible)
            {
                UpdateUiScale();
                CheckTouchScroll();
                RefreshDiagnosticsCacheIfNeeded();
            }
            else
            {
                CheckTouchToggle();
            }
        }

        void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            GUI.depth = -1000;
            Rect screenArea = new Rect(0f, 0f, Screen.width, Screen.height);
            Rect area = SafeAreaForGui();
            UpdateLayoutMetrics(area);

            GUI.DrawTexture(screenArea, GetPanelBackground(), ScaleMode.StretchToFill, true);
            GUILayout.BeginArea(area, _panelStyle);
            DrawHeader();
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawActiveTab();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void UpdateLayoutMetrics(Rect area)
        {
            int horizontalPadding = _panelStyle?.padding.horizontal ?? 0;
            _contentWidth = Mathf.Max(1f, area.width - horizontalPadding);
        }

        static Rect SafeAreaForGui()
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
                return new Rect(0f, 0f, Screen.width, Screen.height);

            float y = Screen.height - safeArea.yMax;
            return new Rect(safeArea.x, y, safeArea.width, safeArea.height);
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
                {
                    SorollaDiagnostics.RefreshIdentifiers();
                    RefreshDiagnosticsCache();
                }
                return;
            }

            _visible = visible;
            ResetUnlockGesture();

            if (visible)
            {
                SorollaDiagnostics.RefreshIdentifiers();
                RefreshDiagnosticsCache();
            }
            else
            {
                _diagnosticsCacheDirty = true;
                ResetTouchScroll();
            }
        }

        void RefreshDiagnosticsCacheIfNeeded()
        {
            if (!_diagnosticsCacheDirty && Time.unscaledTime < _nextDiagnosticsRefreshTime)
                return;

            RefreshDiagnosticsCache();
        }

        void RefreshDiagnosticsCache()
        {
            SorollaDiagnostics.BuildRows(_rows);
            SorollaDiagnostics.CopyEventLog(_events);
            SorollaDiagnostics.CopyRuntimeProblems(_runtimeProblems);
            _headerContextLine = SorollaDiagnostics.BuildHeaderContext();
            RefreshDerivedState();
            _diagnosticsCacheDirty = false;
            _nextDiagnosticsRefreshTime = Time.unscaledTime + DiagnosticsRefreshIntervalSeconds;
        }

        void RequestDiagnosticsRefresh()
        {
            _diagnosticsCacheDirty = true;
            if (_visible)
                RefreshDiagnosticsCache();
        }

        void ResetUnlockGesture()
        {
            _tapCount = 0;
            _firstTapTime = 0f;
            _unlockHoldPointer = false;
            _unlockHoldTouchId = -1;
            _unlockHoldStartTime = 0f;
        }

    }
}
