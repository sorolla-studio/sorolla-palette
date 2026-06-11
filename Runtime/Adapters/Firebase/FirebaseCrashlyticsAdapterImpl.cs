using System;
using System.Collections.Generic;
using Firebase.Crashlytics;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Firebase Crashlytics adapter implementation. Registered at runtime.
    /// </summary>
    [Preserve]
    internal class FirebaseCrashlyticsAdapterImpl : IFirebaseCrashlyticsAdapter
    {
        const string Tag = "[Palette:Crashlytics]";
        private bool _initRequested;
        private bool _ready;
        private bool _initFailed;
        private bool _captureExceptions;
        private readonly Queue<Action> _pendingActions = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            FirebaseCrashlyticsAdapter.RegisterImpl(new FirebaseCrashlyticsAdapterImpl());
        }

        public bool IsReady => _ready;
        public bool IsCrashlyticsCollectionEnabled => _ready && Crashlytics.IsCrashlyticsCollectionEnabled;

        public void Initialize(bool captureUncaughtExceptions)
        {
            if (_initRequested) return;
            _initRequested = true;
            _captureExceptions = captureUncaughtExceptions;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    // Route uncaught C# exceptions to Crashlytics as FATAL (v10.4.0+ recommended,
                    // per https://firebase.google.com/docs/crashlytics/unity/customize-crash-reports).
                    // Without this, uncaught exceptions are not auto-reported — only native crashes are.
                    Crashlytics.ReportUncaughtExceptionsAsFatal = true;

                    _ready = true;
                    PaletteLog.Vital($"{Tag} Initialized (ReportUncaughtExceptionsAsFatal=true)");

                    if (_captureExceptions)
                        Application.logMessageReceived += OnLogMessageReceived;

                    FlushPendingActions();
                }
                else
                {
                    PaletteLog.Error($"{Tag} Firebase not available");
                    _initFailed = true;
                    // Drop anything that queued between Initialize() and the failure callback.
                    _pendingActions.Clear();
                }
            });
        }

        private void FlushPendingActions()
        {
            // Catch-continue per action so one throw can't strand the rest of the queue (DR-38).
            while (_pendingActions.Count > 0)
            {
                var action = _pendingActions.Dequeue();
                try { action?.Invoke(); }
                catch (Exception e) { PaletteLog.Warning($"{Tag} Queued action threw during flush: {e.Message}"); }
            }
        }

        private void QueueOrExecute(Action action)
        {
            if (_ready)
                action();
            else if (_initRequested && !_initFailed)
                _pendingActions.Enqueue(action);
            // _initFailed: drop silently - Firebase permanently unavailable, queueing would leak.
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            // LogType.Exception is handled natively via ReportUncaughtExceptionsAsFatal — don't
            // double-report here. Only capture Error / Assert as breadcrumbs for context.
            if (type != LogType.Error && type != LogType.Assert)
                return;

            try
            {
                Crashlytics.Log($"[{type}] {condition}");
                if (!string.IsNullOrEmpty(stackTrace))
                    Crashlytics.Log(stackTrace);
            }
            catch { }
        }

        public void LogException(Exception exception)
        {
            QueueOrExecute(() => Crashlytics.LogException(exception));
        }

        public void Log(string message)
        {
            QueueOrExecute(() => Crashlytics.Log(message));
        }

        public void SetCustomKey(string key, string value)
        {
            QueueOrExecute(() => Crashlytics.SetCustomKey(key, value));
        }
    }
}
