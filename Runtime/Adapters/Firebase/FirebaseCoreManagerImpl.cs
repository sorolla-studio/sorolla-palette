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
        const string Tag = "[Palette:FirebaseCore]";

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
            PaletteLog.Vital($"{Tag} Initializing...");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCore, AdapterDiagnosticStatus.Initializing,
                "init_requested", "Initializing");

            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    _initialized = true;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        PaletteLog.Error($"{Tag} Failed dependency check. Rebuild with verbose logging to inspect Firebase details.");
                        PaletteLog.Verbose($"{Tag} Failed: {task.Exception?.Message ?? "Cancelled"}");
                        _available = false;
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCore, AdapterDiagnosticStatus.Failed,
                            "dependency_check_failed", "Failed dependency check");
                        InvokePendingCallbacks(false);
                        return;
                    }

                    var status = task.Result;
                    _available = status == DependencyStatus.Available;

                    if (_available)
                    {
                        PaletteLog.Vital($"{Tag} Ready");
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCore, AdapterDiagnosticStatus.Ready,
                            "ready", "Ready");
                    }
                    else
                    {
                        PaletteLog.Error($"{Tag} Dependencies not available: {status}");
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCore, AdapterDiagnosticStatus.Failed,
                            "dependencies_unavailable", $"Dependencies not available: {status}");
                    }

                    InvokePendingCallbacks(_available);
                });
            }
            catch (Exception ex)
            {
                if (ex is TypeInitializationException || ex.InnerException is TypeInitializationException)
                {
                    PaletteLog.Error($"{Tag} Firebase native library not available in Editor. " +
                        "This does not affect Android/iOS device builds.");
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCore, AdapterDiagnosticStatus.Unavailable,
                        "native_library_unavailable", "Firebase native library not available");
                }
                else
                {
                    PaletteLog.Error($"{Tag} Exception during Firebase initialization. Rebuild with verbose logging to inspect details.");
                    PaletteLog.Verbose($"{Tag} Exception: {ex.Message}");
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseCore, AdapterDiagnosticStatus.Failed,
                        "init_exception", "Exception during Firebase initialization");
                }

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
                catch (Exception ex)
                {
                    PaletteLog.Error($"{Tag} Callback error during Firebase initialization.");
                    PaletteLog.Verbose($"{Tag} Callback error: {ex.Message}");
                }
            }
            _pendingCallbacks.Clear();
        }
    }
}
