# Sorolla SDK Simplification Plan - Detailed Handoff

## Overview
Simplify the Sorolla SDK codebase by fixing bugs, removing dead code, consolidating duplications, and merging small files.

**Scope:** 8 changes across 11 files
**Files deleted:** 3
**Approach:** Delete dead code immediately, no deprecation

---

## Phase 1: Bug Fix

### [x] 1.1 Fix undefined `s_config` in Palette.cs

**File:** `Runtime/Palette.cs`
**Line:** 202

**Current (broken):**
```csharp
#if SOROLLA_FACEBOOK_ENABLED
            var isPrototype = s_config == null || s_config.isPrototypeMode;
            if (isPrototype)
                FacebookAdapter.TrackEvent(eventName, value);
#endif
```

**Fix:** Change `s_config` to `Config` (the public property defined on line 50)
```csharp
#if SOROLLA_FACEBOOK_ENABLED
            var isPrototype = Config == null || Config.isPrototypeMode;
            if (isPrototype)
                FacebookAdapter.TrackEvent(eventName, value);
#endif
```

---

## Phase 2: Dead Code Removal

### [x] 2.1 Delete `IsValid()` from SorollaConfig.cs

**File:** `Runtime/SorollaConfig.cs`
**Lines to delete:** 46-62 (including comment above method)

Delete this entire block:
```csharp
        /// <summary>
        ///     Validate configuration for current mode
        /// </summary>
        public bool IsValid()
        {
            if (isPrototypeMode)
                return true; // Prototype is lenient

            // Full mode requires Adjust token (MAX SDK key is in AppLovin settings)
            if (string.IsNullOrEmpty(adjustAppToken))
            {
                Debug.LogError("[Palette] Adjust App Token required in Full Mode");
                return false;
            }

            return true;
        }
```

**Verification:** Grep codebase for `IsValid` - no callers exist.

### [x] 2.2 Delete unused methods from SdkConfigDetector.cs

**File:** `Editor/Sdk/SdkConfigDetector.cs`
**Lines to delete:** 192-226

Delete these two methods (they are never called):
```csharp
        /// <summary>
        ///     Checks if Firebase Crashlytics is configured.
        /// </summary>
        public static ConfigStatus GetCrashlyticsStatus(SorollaConfig config)
        {
            // ... entire method
        }

        /// <summary>
        ///     Checks if Firebase Remote Config is configured.
        /// </summary>
        public static ConfigStatus GetRemoteConfigStatus(SorollaConfig config)
        {
            // ... entire method
        }
```

**Verification:** Grep for `GetCrashlyticsStatus` and `GetRemoteConfigStatus` - no callers.

### [x] 2.3 Fix stale version key in SorollaTestingTools.cs

**File:** `Editor/SorollaTestingTools.cs`
**Line:** 18

**Current (stale):**
```csharp
EditorPrefs.DeleteKey($"Sorolla_Setup_v3_{hash}");
```

**Fix:** Match current format from SorollaSetup.cs (SetupVersion = "v6", line 18):
```csharp
EditorPrefs.DeleteKey($"Sorolla_Setup_v6_{hash}");
```

**Note:** The key format is `Sorolla_Setup_{version}_{hash}`. SorollaSetup.cs uses `SetupVersion = "v6"`.

---

## Phase 3: Deduplication

### [x] 3.1 Extract shared UI rendering in SorollaWindow.cs

**File:** `Editor/SorollaWindow.cs`
**Problem:** Three methods share identical patterns (lines 179-409):
- `DrawSdkOverviewItem()` (179-245)
- `DrawMaxOverviewItem()` (247-331)
- `DrawFirebaseOverviewItem()` (333-409)

**Shared patterns:**
1. Install status icon (green ✓, red ✗, gray ○)
2. SDK name label (with "(optional)" suffix)
3. Config status text (color + message)
4. Action button (Install/Configure/Edit)

**Solution:** Create a helper struct and method:

```csharp
private struct SdkRowData
{
    public string Name;
    public bool IsInstalled;
    public bool IsRequired;
    public SdkConfigDetector.ConfigStatus ConfigStatus;
    public string ConfigHint;
    public Action OnConfigure;
    public Action OnInstall;
}

private void DrawSdkRow(SdkRowData data)
{
    EditorGUILayout.BeginHorizontal();

    // Icon
    var iconStyle = new GUIStyle(EditorStyles.label) { fixedWidth = 20 };
    if (data.IsInstalled)
    {
        iconStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
        GUILayout.Label("✓", iconStyle);
    }
    else
    {
        iconStyle.normal.textColor = data.IsRequired ? new Color(1f, 0.4f, 0.4f) : Color.gray;
        GUILayout.Label(data.IsRequired ? "✗" : "○", iconStyle);
    }

    // Name
    var nameLabel = data.IsRequired ? data.Name : $"{data.Name} (optional)";
    GUILayout.Label(nameLabel, GUILayout.Width(140));

    // Config status
    var configStyle = new GUIStyle(EditorStyles.miniLabel);
    string configText;
    if (!data.IsInstalled)
    {
        configStyle.normal.textColor = Color.gray;
        configText = "—";
    }
    else if (data.ConfigStatus == SdkConfigDetector.ConfigStatus.Configured)
    {
        configStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
        configText = "✓ Configured";
    }
    else
    {
        configStyle.normal.textColor = new Color(1f, 0.7f, 0.2f);
        configText = data.ConfigHint;
    }
    GUILayout.Label(configText, configStyle, GUILayout.Width(120));

    GUILayout.FlexibleSpace();

    // Action button
    if (!data.IsInstalled && data.OnInstall != null)
    {
        if (GUILayout.Button("Install", GUILayout.Width(70)))
            data.OnInstall();
    }
    else if (data.IsInstalled && data.ConfigStatus == SdkConfigDetector.ConfigStatus.NotConfigured && data.OnConfigure != null)
    {
        if (GUILayout.Button("Configure", GUILayout.Width(70)))
            data.OnConfigure();
    }
    else if (data.IsInstalled && data.ConfigStatus == SdkConfigDetector.ConfigStatus.Configured && data.OnConfigure != null)
    {
        if (GUILayout.Button("Edit", GUILayout.Width(50)))
            data.OnConfigure();
    }

    EditorGUILayout.EndHorizontal();
}
```

Then refactor `DrawSdkOverviewItem()` to use it. Keep `DrawMaxOverviewItem()` and `DrawFirebaseOverviewItem()` separate since they have extra UI (ad unit fields, Firebase modules).

### [x] 3.2 Extract shared registry setup in SdkInstaller.cs

**File:** `Editor/Sdk/SdkInstaller.cs`
**Problem:** MAX registry setup duplicated at lines 29-38 and 99-108

**Solution:** Extract to private method:

```csharp
private static void EnsureMaxRegistry()
{
    // Remove com.applovin from OpenUPM if it exists (fixes duplicate scope error)
    ManifestManager.RemoveScopeFromRegistry("https://package.openupm.com", "com.applovin");

    ManifestManager.AddOrUpdateRegistry(
        "AppLovin MAX",
        "https://unity.packages.applovin.com/",
        new[] { "com.applovin" }
    );
}
```

Call from both `Install()` (line 29) and `InstallRequiredSdks()` (line 99) when `id == SdkId.AppLovinMAX`.

### [x] 3.3 Cache reflection Type in MaxSettingsSanitizer.cs

**File:** `Editor/MaxSettingsSanitizer.cs`
**Problem:** `FindAppLovinSettingsType()` called separately in each method (lines 22, 82, 125)

**Solution:** Cache the Type at class level:

```csharp
private static System.Type s_appLovinSettingsType;
private static bool s_typeSearched;

private static System.Type GetAppLovinSettingsType()
{
    if (!s_typeSearched)
    {
        s_typeSearched = true;
        s_appLovinSettingsType = FindAppLovinSettingsType();
    }
    return s_appLovinSettingsType;
}
```

Replace all calls to `FindAppLovinSettingsType()` with `GetAppLovinSettingsType()`.

---

## Phase 4: Cleanup

### [x] 4.1 Merge SorollaMode.cs into SorollaSettings.cs

**Delete:** `Editor/SorollaMode.cs`
**Modify:** `Editor/SorollaSettings.cs`

Move the enum to the top of SorollaSettings.cs (before the class):

```csharp
namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     SDK operating mode
    /// </summary>
    public enum SorollaMode
    {
        /// <summary>No mode selected yet (first run)</summary>
        None,
        /// <summary>Prototype: GA + Facebook for rapid UA testing</summary>
        Prototype,
        /// <summary>Full: GA + MAX + Adjust for production</summary>
        Full
    }

    /// <summary>
    ///     Central settings for Palette SDK.
    /// </summary>
    public static class SorollaSettings
    {
        // ... existing code
    }
}
```

### [x] 4.2 Merge BuildValidationWindow.cs into SorollaWindow.cs

**Delete:** `Editor/BuildValidationWindow.cs`
**Modify:** `Editor/SorollaWindow.cs`

Add the menu item inside SorollaWindow class (near line 43-49 where other menu items are):

```csharp
[MenuItem("Palette/Tools/Validate Build")]
public static void ValidateBuild() => ShowWindow();
```

### [x] 4.3 Merge FirebaseCoreManager.cs into FirebaseAdapter.cs

**Delete:** `Runtime/Adapters/FirebaseCoreManager.cs`
**Modify:** `Runtime/Adapters/FirebaseAdapter.cs`

Move `IFirebaseCoreManager` interface and `FirebaseCoreManager` class into FirebaseAdapter.cs:

```csharp
namespace Sorolla.Palette.Adapters
{
    // Existing IFirebaseAdapter interface...

    /// <summary>
    ///     Interface for Firebase Core Manager implementation.
    /// </summary>
    internal interface IFirebaseCoreManager
    {
        bool IsInitializing { get; }
        bool IsInitialized { get; }
        bool IsAvailable { get; }
        void Initialize(System.Action<bool> onReady);
    }

    // Existing FirebaseAdapter class...

    /// <summary>
    ///     Centralized Firebase initialization manager.
    /// </summary>
    public static class FirebaseCoreManager
    {
        private static IFirebaseCoreManager s_impl;

        internal static void RegisterImpl(IFirebaseCoreManager impl)
        {
            s_impl = impl;
            UnityEngine.Debug.Log("[Sorolla:FirebaseCore] Implementation registered");
        }

        public static bool IsInitializing => s_impl?.IsInitializing ?? false;
        public static bool IsInitialized => s_impl?.IsInitialized ?? false;
        public static bool IsAvailable => s_impl?.IsAvailable ?? false;

        public static void Initialize(System.Action<bool> onReady)
        {
            if (s_impl != null)
                s_impl.Initialize(onReady);
            else
                onReady?.Invoke(false);
        }
    }
}
```

### [x] 4.4 Trim AssemblyInfo comments

**Files:**
- `Runtime/Adapters/MAX/AssemblyInfo.cs`
- `Runtime/Adapters/Adjust/AssemblyInfo.cs` (if exists)
- `Runtime/Adapters/Firebase/AssemblyInfo.cs` (if exists)

**Current:**
```csharp
using UnityEngine.Scripting;

// Force the Unity linker to process this assembly even if no types are directly referenced in scenes.
// Required because this assembly uses [RuntimeInitializeOnLoadMethod] for auto-registration.
// See: https://docs.unity3d.com/ScriptReference/Scripting.AlwaysLinkAssemblyAttribute.html
[assembly: AlwaysLinkAssembly]
```

**After:**
```csharp
using UnityEngine.Scripting;

// Required for [RuntimeInitializeOnLoadMethod] to work with IL2CPP stripping
[assembly: AlwaysLinkAssembly]
```

---

## Verification Plan

After all changes:

1. **Compile check**: Open Unity, verify no compilation errors
2. **Window test**: Open `Palette > Configuration`, verify UI renders correctly
3. **Mode switch**: Switch between Prototype/Full modes, verify SDK installs work
4. **Validation**: Run `Palette > Tools > Validate Build`, verify checks pass
5. **Grep check**: Search codebase for deleted symbols to confirm no remaining references:
   - `IsValid` (SorollaConfig)
   - `GetCrashlyticsStatus`
   - `GetRemoteConfigStatus`
   - `s_config` (should only find `_config` or `Config`)

---

## Files Summary

| File | Action | Details |
|------|--------|---------|
| `Runtime/Palette.cs:202` | Edit | `s_config` → `Config` |
| `Runtime/SorollaConfig.cs:46-62` | Delete lines | Remove `IsValid()` |
| `Editor/Sdk/SdkConfigDetector.cs:192-226` | Delete lines | Remove 2 methods |
| `Editor/SorollaTestingTools.cs:18` | Edit | `v3` → `v6` |
| `Editor/SorollaWindow.cs` | Refactor | Add helper struct + method |
| `Editor/Sdk/SdkInstaller.cs` | Refactor | Extract `EnsureMaxRegistry()` |
| `Editor/MaxSettingsSanitizer.cs` | Refactor | Cache Type result |
| `Editor/SorollaMode.cs` | **DELETE** | Merge into SorollaSettings.cs |
| `Editor/SorollaSettings.cs` | Edit | Add `SorollaMode` enum |
| `Editor/BuildValidationWindow.cs` | **DELETE** | Merge into SorollaWindow.cs |
| `Runtime/Adapters/FirebaseCoreManager.cs` | **DELETE** | Merge into FirebaseAdapter.cs |
| `Runtime/Adapters/FirebaseAdapter.cs` | Edit | Add FirebaseCoreManager |
| `Runtime/Adapters/*/AssemblyInfo.cs` | Edit | Trim comments |

---

## Gotchas

1. **Order matters for merges**: Delete files AFTER merging their content into targets
2. **Namespace consistency**: `SorollaMode` enum uses `Sorolla.Palette.Editor` namespace
3. **Assembly references**: FirebaseCoreManager is in `Sorolla.Adapters.asmdef`, same as FirebaseAdapter - safe to merge
4. **SorollaSettings dependency**: Other files import `SorollaMode` - after merge, imports stay the same since namespace unchanged
