# Plan: Add `Palette.ShowDebugger()` Public API

## Goal

Expose the debug UI through a simple static call: `Palette.ShowDebugger()`, with one-click setup via checkbox in Palette Configuration.

## Architecture

```
com.sorolla.sdk
├── Runtime/
│   ├── Palette.cs                      # ShowDebugger() + internal events
│   └── link.xml                        # Add Sorolla.DebugUI entry
├── Editor/
│   ├── SorollaWindow.cs                # "Enable Debug UI" checkbox + update prompt
│   └── DebugUIInstaller.cs             # Copy logic + version checker
└── DebugUI~/                           # Source files (hidden folder, copied to Assets/)
    ├── Resources/
    │   └── SorollaDebugPanel.prefab
    ├── Scripts/
    │   ├── AssemblyInfo.cs             # [assembly: AlwaysLinkAssembly]
    │   ├── Core/
    │   │   ├── DebugUIBootstrapper.cs  # [RuntimeInitializeOnLoadMethod] + [Preserve]
    │   │   ├── DebugPanelManager.cs
    │   │   └── ...
    │   └── ...
    ├── Sorolla.DebugUI.asmdef
    └── .version                        # SDK version for drift detection
```

**Why `DebugUI~/` instead of `Samples~/`?** The `~` suffix hides the folder from Unity, preventing accidental import via Package Manager (which would cause GUID collisions with the copied files).

**Why copy to Assets/?** Unity's `Resources.Load` only works from `Assets/Resources/`, not from packages. This is a Unity limitation.

## Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  User checks "Enable Debug UI" in Palette > Configuration       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  DebugUIInstaller.Install()                                     │
│  1. Copy DebugUI~/ → Assets/Sorolla/DebugUI                     │
│  2. Write .version file with current SDK version                │
│  3. AssetDatabase.Refresh()                                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Domain reload triggers DebugUIBootstrapper                     │
│  [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]                │
│  1. Load prefab from Resources                                  │
│  2. Instantiate, DontDestroyOnLoad                              │
│  3. Start hidden                                                │
│  4. Subscribe to Palette events                                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  User calls Palette.ShowDebugger()                              │
│  Event fires → DebugPanelManager.SetVisible(true)               │
└─────────────────────────────────────────────────────────────────┘
```

## Version Drift Prevention (Prompt-Based)

```
┌─────────────────────────────────────────────────────────────────┐
│  [InitializeOnLoad] DebugUIInstaller                            │
│                                                                 │
│  On every domain reload:                                        │
│  1. Check if Assets/Sorolla/DebugUI exists                      │
│  2. Read Assets/Sorolla/DebugUI/.version                        │
│  3. Compare with current SDK version (package.json)             │
│  4. If mismatch → set UpdateAvailable flag (NO auto-update)     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  SorollaWindow shows warning + "Update Debug UI" button         │
│  User clicks → DebugUIInstaller.Install() (explicit consent)    │
└─────────────────────────────────────────────────────────────────┘
```

**Why prompt instead of auto-update?** Users may customize the Debug UI. Silent overwrites destroy their modifications without consent.

## Files to Create/Modify

### 1. Runtime/link.xml — Add Debug UI Entry

Add to existing link.xml:

```xml
<!-- Debug UI assembly - only exists when enabled -->
<assembly fullname="Sorolla.DebugUI" preserve="all" ignoreIfMissing="1"/>
```

### 2. Runtime/Palette.cs — Add Debug UI Region

After line ~163 (after `OnInitialized` event):

```csharp
#region Debug UI

internal static event Action OnShowDebuggerRequested;
internal static event Action OnHideDebuggerRequested;
internal static event Action OnToggleDebuggerRequested;

/// <summary>
/// Shows the Sorolla debug panel.
/// Enable Debug UI in Palette > Configuration first.
/// </summary>
public static void ShowDebugger()
{
    if (OnShowDebuggerRequested == null)
    {
        Debug.LogWarning($"{Tag} Debug UI not enabled. Enable it in Palette > Configuration.");
        return;
    }
    OnShowDebuggerRequested.Invoke();
}

/// <summary>
/// Hides the Sorolla debug panel.
/// </summary>
public static void HideDebugger() => OnHideDebuggerRequested?.Invoke();

/// <summary>
/// Toggles the Sorolla debug panel visibility.
/// </summary>
public static void ToggleDebugger()
{
    if (OnToggleDebuggerRequested == null)
    {
        Debug.LogWarning($"{Tag} Debug UI not enabled. Enable it in Palette > Configuration.");
        return;
    }
    OnToggleDebuggerRequested.Invoke();
}

#endregion
```

### 3. Editor/DebugUIInstaller.cs — New File

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    [InitializeOnLoad]
    public static class DebugUIInstaller
    {
        const string SourcePath = "Packages/com.sorolla.sdk/DebugUI~";
        const string DestPath = "Assets/Sorolla/DebugUI";
        const string VersionFile = ".version";

        public static bool UpdateAvailable { get; private set; }

        static DebugUIInstaller()
        {
            // Check for updates on domain reload (don't auto-update)
            if (IsInstalled() && IsVersionMismatch())
            {
                UpdateAvailable = true;
                Debug.LogWarning("[Sorolla] Debug UI update available. Update via Palette > Configuration.");
            }
        }

        public static bool IsInstalled()
        {
            return Directory.Exists(DestPath);
        }

        public static void Install()
        {
            // Ensure parent directory exists
            var parentDir = Path.GetDirectoryName(DestPath);
            if (!Directory.Exists(parentDir))
                Directory.CreateDirectory(parentDir);

            // Remove old version if exists
            if (Directory.Exists(DestPath))
                FileUtil.DeleteFileOrDirectory(DestPath);

            // Copy fresh
            FileUtil.CopyFileOrDirectory(SourcePath, DestPath);

            // Write version file
            var version = GetPackageVersion();
            File.WriteAllText(Path.Combine(DestPath, VersionFile), version);

            // Clear update flag
            UpdateAvailable = false;

            AssetDatabase.Refresh();
            Debug.Log("[Sorolla] Debug UI installed.");
        }

        public static void Uninstall()
        {
            if (Directory.Exists(DestPath))
            {
                FileUtil.DeleteFileOrDirectory(DestPath);
                FileUtil.DeleteFileOrDirectory(DestPath + ".meta");
                UpdateAvailable = false;
                AssetDatabase.Refresh();
                Debug.Log("[Sorolla] Debug UI removed.");
            }
        }

        static bool IsVersionMismatch()
        {
            var installedVersion = GetInstalledVersion();
            var packageVersion = GetPackageVersion();
            return installedVersion != packageVersion;
        }

        static string GetInstalledVersion()
        {
            var versionPath = Path.Combine(DestPath, VersionFile);
            if (!File.Exists(versionPath)) return null;
            return File.ReadAllText(versionPath).Trim();
        }

        static string GetPackageVersion()
        {
            var packageJsonPath = "Packages/com.sorolla.sdk/package.json";
            if (!File.Exists(packageJsonPath)) return "unknown";
            var json = File.ReadAllText(packageJsonPath);
            var match = System.Text.RegularExpressions.Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : "unknown";
        }
    }
}
```

### 4. Editor/SorollaWindow.cs — Add Checkbox + Update Prompt

In the configuration UI section:

```csharp
void DrawDebugUISection()
{
    EditorGUILayout.Space(10);
    EditorGUILayout.LabelField("Debug UI", EditorStyles.boldLabel);

    bool isInstalled = DebugUIInstaller.IsInstalled();

    // Show update prompt if available
    if (DebugUIInstaller.UpdateAvailable)
    {
        EditorGUILayout.HelpBox(
            "A new version of Debug UI is available.\n" +
            "Update to get the latest fixes and features.",
            MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Update Debug UI"))
        {
            DebugUIInstaller.Install();
        }
        if (GUILayout.Button("Skip"))
        {
            // TODO: Could persist "skip this version" preference
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    EditorGUI.BeginChangeCheck();
    bool enabled = EditorGUILayout.Toggle("Enable Debug UI", isInstalled);
    if (EditorGUI.EndChangeCheck() && enabled != isInstalled)
    {
        if (enabled)
            DebugUIInstaller.Install();
        else
            DebugUIInstaller.Uninstall();
    }

    if (isInstalled && !DebugUIInstaller.UpdateAvailable)
    {
        EditorGUILayout.HelpBox(
            "Debug UI is enabled. Call Palette.ShowDebugger() in your code.",
            MessageType.Info);
    }
}
```

### 5. DebugUI~/Scripts/AssemblyInfo.cs — New File (IL2CPP Protection)

```csharp
using System.Runtime.CompilerServices;

// Force IL2CPP linker to process this assembly even if no direct references
[assembly: AlwaysLinkAssembly]
```

### 6. DebugUI~/Scripts/Core/DebugUIBootstrapper.cs — New File

```csharp
using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.DebugUI
{
    [Preserve] // IL2CPP stripping protection
    internal static class DebugUIBootstrapper
    {
        static GameObject _instance;
        static Action _showHandler;
        static Action _hideHandler;
        static Action _toggleHandler;

        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (_instance != null) return;

            var prefab = Resources.Load<GameObject>("SorollaDebugPanel");
            if (prefab == null)
            {
                Debug.LogError("[Sorolla] DebugPanel prefab not found. Reinstall Debug UI from Palette > Configuration.");
                return;
            }

            _instance = UnityEngine.Object.Instantiate(prefab);
            _instance.name = "SorollaDebugPanel";
            UnityEngine.Object.DontDestroyOnLoad(_instance);

            var manager = _instance.GetComponent<DebugPanelManager>();
            manager.SetVisible(false); // Always start hidden

            // Subscribe to Palette events
            _showHandler = () => manager.SetVisible(true);
            _hideHandler = () => manager.SetVisible(false);
            _toggleHandler = manager.TogglePanel;

            Palette.OnShowDebuggerRequested += _showHandler;
            Palette.OnHideDebuggerRequested += _hideHandler;
            Palette.OnToggleDebuggerRequested += _toggleHandler;

            // Cleanup on application quit
            Application.quitting += Cleanup;
        }

        static void Cleanup()
        {
            Palette.OnShowDebuggerRequested -= _showHandler;
            Palette.OnHideDebuggerRequested -= _hideHandler;
            Palette.OnToggleDebuggerRequested -= _toggleHandler;
        }
    }
}
```

### 7. DebugUI~/Scripts/Core/DebugPanelManager.cs — Simplify

Remove singleton Awake logic (bootstrapper handles instantiation):

```csharp
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.DebugUI
{
    [Preserve]
    public class DebugPanelManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] TabController tabController;
        [SerializeField] ToastController toastController;
        [SerializeField] LogController logController;

        [Header("Settings")]
        [SerializeField] KeyCode toggleKey = KeyCode.BackQuote;

        public bool IsVisible { get; private set; }

        // Remove: static Instance, Awake singleton logic, DontDestroyOnLoad
        // Bootstrapper handles all of that

        void Start()
        {
            EnsureEventSystem();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                TogglePanel();

#if UNITY_IOS || UNITY_ANDROID
            if (Input.touchCount == 3)
            {
                bool allBegan = true;
                foreach (Touch touch in Input.touches)
                {
                    if (touch.phase != TouchPhase.Began)
                    {
                        allBegan = false;
                        break;
                    }
                }
                if (allBegan) TogglePanel();
            }
#endif
        }

        void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null &&
                FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var go = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                DontDestroyOnLoad(go);
            }
        }

        public void TogglePanel() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            panelRoot.SetActive(visible);
        }

        // ... rest of public API unchanged
    }
}
```

### 8. DebugUI~/.version — New File

```
0.0.0
```

(Placeholder, overwritten by installer with actual SDK version)

### 9. DebugUI~/Resources/ — Move Prefab

Move `SorollaDebugPanel.prefab` to `DebugUI~/Resources/SorollaDebugPanel.prefab` so `Resources.Load("SorollaDebugPanel")` works after copy.

## Usage

```csharp
// After enabling Debug UI in Palette > Configuration:
Palette.ShowDebugger();
Palette.HideDebugger();
Palette.ToggleDebugger();

// Or use built-in gestures:
// - Triple-tap on mobile
// - Press ` (backtick) key
```

## Verification

1. Fresh project: `Palette.ShowDebugger()` logs warning about enabling Debug UI
2. Check "Enable Debug UI" in Palette > Configuration
3. Files appear in `Assets/Sorolla/DebugUI/`
4. Enter Play mode, call `Palette.ShowDebugger()` → panel appears
5. Update SDK version in package.json
6. Reopen Unity → Warning appears in SorollaWindow, "Update Debug UI" button shown
7. Click "Update Debug UI" → files updated, warning disappears
8. Uncheck "Enable Debug UI" → files removed from Assets/

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Source location | `DebugUI~/` (not `Samples~/`) | Prevents GUID collision from Package Manager import |
| Update strategy | Prompt (not auto) | Respects user modifications |
| IL2CPP protection | `[Preserve]` + `[AlwaysLinkAssembly]` + `link.xml` | Belt & suspenders, matches existing adapter pattern |
| Activation gesture | Defer | Not priority, can customize later |
