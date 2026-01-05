using System;
using UnityEngine;

namespace Sorolla.Palette.DebugUI
{
    // Enums
    public enum AdType
    {
        Interstitial,
        Rewarded,
        Banner,
    }

    public enum AdStatus
    {
        Idle,
        Loading,
        Loaded,
        Showing,
        Failed,
    }

    public enum LogLevel
    {
        All,
        Verbose,
        Info,
        Warning,
        Error,
    }

    public enum LogSource
    {
        UI,
        Game,
        Sorolla,
        GA,
        Firebase,
        MAX,
        Adjust,
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
    }

    public enum SorollaMode
    {
        Prototype,
        Full,
    }

    public enum ToggleType
    {
        None,
        DebugMode,
        GodMode,
        CaptureUnityLogs,
        BannerAds,
    }

    // Data structures
    [Serializable]
    public struct LogEntryData
    {
        public string timestamp;
        public LogSource source;
        public LogLevel level;
        public string message;
        public Color accentColor;
    }
}
