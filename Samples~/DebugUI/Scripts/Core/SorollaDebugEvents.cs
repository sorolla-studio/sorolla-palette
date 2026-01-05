using System;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Static event hub for decoupled communication between UI components.
    ///     Components subscribe to events they care about without direct references.
    /// </summary>
    public static class SorollaDebugEvents
    {
        // Tab Navigation
        public static event Action<int> OnTabChanged;
        public static void RaiseTabChanged(int tabIndex) => OnTabChanged?.Invoke(tabIndex);

        // Toast Notifications
        public static event Action<string, ToastType> OnShowToast;
        public static void RaiseShowToast(string message, ToastType type) => OnShowToast?.Invoke(message, type);

        // Ad Events
        public static event Action<AdType, AdStatus> OnAdStatusChanged;
        public static void RaiseAdStatusChanged(AdType adType, AdStatus status) => OnAdStatusChanged?.Invoke(adType, status);

        // SDK Health
        public static event Action<string, bool> OnSDKHealthChanged;
        public static void RaiseSDKHealthChanged(string sdkName, bool isHealthy) => OnSDKHealthChanged?.Invoke(sdkName, isHealthy);

        // Mode Changed
        public static event Action<SorollaMode> OnModeChanged;
        public static void RaiseModeChanged(SorollaMode mode) => OnModeChanged?.Invoke(mode);

        // Toggle Events
        public static event Action<ToggleType, bool> OnToggleChanged;
        public static void RaiseToggleChanged(ToggleType toggle, bool value) => OnToggleChanged?.Invoke(toggle, value);

        // Log Events
        public static event Action<LogEntryData> OnLogAdded;
        public static void RaiseLogAdded(LogEntryData data) => OnLogAdded?.Invoke(data);

        public static event Action OnLogsClear;
        public static void RaiseLogsClear() => OnLogsClear?.Invoke();

        // Log Filter
        public static event Action<LogLevel> OnLogFilterChanged;
        public static void RaiseLogFilterChanged(LogLevel level) => OnLogFilterChanged?.Invoke(level);

        // Remote Config
        public static event Action<bool> OnRemoteConfigFetched;
        public static void RaiseRemoteConfigFetched(bool success) => OnRemoteConfigFetched?.Invoke(success);
    }
}
