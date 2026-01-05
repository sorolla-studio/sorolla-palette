using System;
using System.Collections.Generic;
using Firebase.Crashlytics;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Firebase Crashlytics adapter implementation. Registered at runtime.
    /// </summary>
    internal class FirebaseCrashlyticsAdapterImpl : IFirebaseCrashlyticsAdapter
    {
        private const string Tag = "[Sorolla:Crashlytics]";
        private bool _initRequested;
        private bool _ready;
        private bool _captureExceptions;
        private readonly Queue<Action> _pendingActions = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
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
                    _ready = true;
                    Debug.Log($"{Tag} Initialized");

                    if (_captureExceptions)
                        Application.logMessageReceived += OnLogMessageReceived;

                    FlushPendingActions();
                }
                else
                {
                    Debug.LogError($"{Tag} Firebase not available");
                }
            });
        }

        private void FlushPendingActions()
        {
            while (_pendingActions.Count > 0)
            {
                var action = _pendingActions.Dequeue();
                action?.Invoke();
            }
        }

        private void QueueOrExecute(Action action)
        {
            if (_ready)
                action();
            else if (_initRequested)
                _pendingActions.Enqueue(action);
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                return;

            try
            {
                Crashlytics.SetCustomKey("unity_log_type", type.ToString());

                if (type == LogType.Exception)
                    Crashlytics.LogException(new Exception($"{condition}\n{stackTrace}"));
                else
                {
                    Crashlytics.Log($"[{type}] {condition}");
                    if (!string.IsNullOrEmpty(stackTrace))
                        Crashlytics.Log(stackTrace);
                }
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

        public void SetUserId(string userId)
        {
            QueueOrExecute(() => Crashlytics.SetUserId(userId));
        }
    }
}
