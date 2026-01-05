using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Manages log entries with object pooling.  Handles filtering and clearing.
    /// </summary>
    public class LogController : UIComponentBase
    {
        [SerializeField] GameObject logEntryPrefab;
        [SerializeField] Transform logContainer;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] int maxLogEntries = 100;
        [SerializeField] int poolSize = 20;
        [SerializeField] bool captureUnityLogs = true;

        readonly Queue<LogEntryView> _pool = new Queue<LogEntryView>();
        readonly List<LogEntryView> _activeEntries = new List<LogEntryView>();
        readonly List<LogEntryData> _allLogs = new List<LogEntryData>();

        LogLevel _currentFilter = LogLevel.All;
        bool _captureUnityLogs;
        bool _isLogging; // Prevents duplicate logs when we call Debug.Log

        void Awake()
        {
            InitializePool();
            // Subscribe here, not OnEnable - must capture logs even when tab is inactive
            SorollaDebugEvents.OnLogAdded += HandleLogAdded;
            SorollaDebugEvents.OnLogsClear += HandleLogsClear;
            SorollaDebugEvents.OnLogFilterChanged += HandleFilterChanged;
            SorollaDebugEvents.OnToggleChanged += HandleToggleChanged;

            SetCaptureUnityLogs(captureUnityLogs);
        }

        void OnDestroy()
        {
            SorollaDebugEvents.OnLogAdded -= HandleLogAdded;
            SorollaDebugEvents.OnLogsClear -= HandleLogsClear;
            SorollaDebugEvents.OnLogFilterChanged -= HandleFilterChanged;
            SorollaDebugEvents.OnToggleChanged -= HandleToggleChanged;
            Application.logMessageReceived -= HandleUnityLog;
        }

        void HandleToggleChanged(ToggleType toggle, bool value)
        {
            if (toggle == ToggleType.CaptureUnityLogs)
                SetCaptureUnityLogs(value);
        }

        protected override void SubscribeToEvents() { }
        protected override void UnsubscribeFromEvents() { }

        void InitializePool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                CreatePooledEntry();
            }
        }

        LogEntryView CreatePooledEntry()
        {
            GameObject entryGO = Instantiate(logEntryPrefab, logContainer);
            entryGO.SetActive(false);
            var entry = entryGO.GetComponent<LogEntryView>();
            _pool.Enqueue(entry);
            return entry;
        }

        LogEntryView GetFromPool()
        {
            if (_pool.Count == 0)
                CreatePooledEntry();
            return _pool.Dequeue();
        }

        void ReturnToPool(LogEntryView entry)
        {
            entry.gameObject.SetActive(false);
            entry.transform.SetAsLastSibling();
            _pool.Enqueue(entry);
        }

        void HandleLogAdded(LogEntryData data)
        {
            _allLogs.Add(data);

            // Trim old logs
            while (_allLogs.Count > maxLogEntries)
            {
                _allLogs.RemoveAt(0);
            }

            // Check if passes current filter
            if (PassesFilter(data, _currentFilter))
            {
                DisplayLogEntry(data);
            }

            // Auto-scroll to bottom
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        void HandleLogsClear()
        {
            _allLogs.Clear();

            foreach (LogEntryView entry in _activeEntries)
            {
                ReturnToPool(entry);
            }
            _activeEntries.Clear();
        }

        void HandleFilterChanged(LogLevel level)
        {
            _currentFilter = level;
            RefreshDisplay();
        }

        void DisplayLogEntry(LogEntryData data)
        {
            LogEntryView entry = GetFromPool();
            entry.SetData(data);
            entry.gameObject.SetActive(true);
            entry.transform.SetAsLastSibling();
            _activeEntries.Add(entry);

            // Trim displayed entries
            while (_activeEntries.Count > poolSize)
            {
                LogEntryView oldest = _activeEntries[0];
                _activeEntries.RemoveAt(0);
                ReturnToPool(oldest);
            }
        }

        public void SetFilter(LogLevel level)
        {
            _currentFilter = level;
            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            // Return all to pool
            foreach (LogEntryView entry in _activeEntries)
            {
                ReturnToPool(entry);
            }
            _activeEntries.Clear();

            // Re-display filtered logs
            int startIndex = Mathf.Max(0, _allLogs.Count - poolSize);
            for (int i = startIndex; i < _allLogs.Count; i++)
            {
                if (PassesFilter(_allLogs[i], _currentFilter))
                {
                    DisplayLogEntry(_allLogs[i]);
                }
            }
        }

        bool PassesFilter(LogEntryData data, LogLevel filter)
        {
            if (filter == LogLevel.All) return true;
            return data.level == filter;
        }

        public void ClearLogs()
        {
            _allLogs.Clear();

            foreach (LogEntryView entry in _activeEntries)
            {
                ReturnToPool(entry);
            }
            _activeEntries.Clear();
        }

        // Public method to add log from external code
        public void Log(string message, LogSource source, LogLevel level = LogLevel.Info)
        {
            Color accentColor = source switch
            {
                LogSource.GA => Theme.accentYellow,
                LogSource.Game => Theme.accentGreen,
                LogSource.Firebase => Theme.accentOrange,
                LogSource.Sorolla => Theme.textSecondary,
                _ => Theme.textPrimary,
            };

            var data = new LogEntryData
            {
                timestamp = DateTime.Now.ToString("HH:mm:ss. ff"),
                source = source,
                level = level,
                message = message,
                accentColor = accentColor,
            };

            _isLogging = true;
            string formattedMessage = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>[{source}]</color> {message}";
            switch (level)
            {
                case LogLevel.Warning: Debug.LogWarning(formattedMessage); break;
                case LogLevel.Error: Debug.LogError(formattedMessage); break;
                default: Debug.Log(formattedMessage); break;
            }
            _isLogging = false;

            SorollaDebugEvents.RaiseLogAdded(data);
        }

        // === Unity Log Capture ===

        /// <summary>Toggle Unity log capture - wire to ToggleSwitch in inspector</summary>
        public void SetCaptureUnityLogs(bool enabled)
        {
            if (enabled == _captureUnityLogs) return;
            _captureUnityLogs = enabled;

            if (enabled)
                Application.logMessageReceived += HandleUnityLog;
            else
                Application.logMessageReceived -= HandleUnityLog;
        }

        void HandleUnityLog(string message, string stackTrace, LogType type)
        {
            // Skip if we're currently logging from our own Log method
            if (_isLogging) return;

            // Only warnings and errors from external sources
            if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception)
                return;

            LogLevel level = type == LogType.Warning ? LogLevel.Warning : LogLevel.Error;
            Color accentColor = level == LogLevel.Warning ? Theme.accentYellow : Theme.accentRed;

            var data = new LogEntryData
            {
                timestamp = DateTime.Now.ToString("HH:mm:ss.ff"),
                source = LogSource.Game,
                level = level,
                message = message,
                accentColor = accentColor,
            };

            SorollaDebugEvents.RaiseLogAdded(data);
        }
    }
}
