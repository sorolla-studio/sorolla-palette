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
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCrashlytics, AdapterDiagnosticStatus.Ready,
                        "initialized", "Initialized");

                    if (_captureExceptions)
                        Application.logMessageReceived += OnLogMessageReceived;

                    FlushPendingActions();
                }
                else
                {
                    PaletteLog.Error($"{Tag} Firebase not available");
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCrashlytics, AdapterDiagnosticStatus.Failed,
                        "firebase_unavailable", "Firebase not available");
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
                catch (Exception e)
                {
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCrashlytics,
                        AdapterDiagnosticStatus.Warning, "queued_action_threw",
                        "Queued Crashlytics action threw during flush");
                    PaletteLog.Warning($"{Tag} Queued action threw during flush: {e.Message}");
                }
            }
        }

        private void QueueOrExecute(Action action)
        {
            if (_ready)
            {
                action();
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCrashlytics,
                    AdapterDiagnosticStatus.DispatchAccepted, "action_sent", "Crashlytics action sent");
            }
            else if (_initRequested && !_initFailed)
                _pendingActions.Enqueue(action);
            else if (_initFailed)
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCrashlytics,
                    AdapterDiagnosticStatus.DispatchDropped, "init_failed_drop",
                    "Crashlytics init failed - dropping action");
            else
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCrashlytics,
                    AdapterDiagnosticStatus.DispatchDropped, "before_init_drop",
                    "Crashlytics action dropped before init");
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
