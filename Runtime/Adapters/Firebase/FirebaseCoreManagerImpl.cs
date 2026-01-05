using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Firebase Core Manager implementation. Registered at runtime.
    /// </summary>
    internal class FirebaseCoreManagerImpl : IFirebaseCoreManager
    {
        private const string Tag = "[Sorolla:FirebaseCore]";

        private bool _initRequested;
        private bool _initialized;
        private bool _available;
        private readonly List<Action<bool>> _pendingCallbacks = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            FirebaseCoreManager.RegisterImpl(new FirebaseCoreManagerImpl());
        }

        public bool IsInitializing => _initRequested && !_initialized;
        public bool IsInitialized => _initialized;
        public bool IsAvailable => _available;

        public void Initialize(Action<bool> onReady)
        {
            if (_initialized)
            {
                onReady?.Invoke(_available);
                return;
            }

            if (onReady != null)
                _pendingCallbacks.Add(onReady);

            if (_initRequested)
                return;

            _initRequested = true;
            Debug.Log($"{Tag} Initializing...");

            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    _initialized = true;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError($"{Tag} Failed: {task.Exception?.Message ?? "Cancelled"}");
                        _available = false;
                        InvokePendingCallbacks(false);
                        return;
                    }

                    var status = task.Result;
                    _available = status == DependencyStatus.Available;

                    if (_available)
                        Debug.Log($"{Tag} Ready");
                    else
                        Debug.LogError($"{Tag} Dependencies not available: {status}");

                    InvokePendingCallbacks(_available);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Exception: {ex.Message}");
                _initialized = true;
                _available = false;
                InvokePendingCallbacks(false);
            }
        }

        private void InvokePendingCallbacks(bool success)
        {
            foreach (var callback in _pendingCallbacks)
            {
                try { callback?.Invoke(success); }
                catch (Exception ex) { Debug.LogError($"{Tag} Callback error: {ex.Message}"); }
            }
            _pendingCallbacks.Clear();
        }
    }
}
