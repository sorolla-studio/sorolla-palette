# Debug UI Installer - Implementation Plan

**Date**: 2026-01-19
**Status**: Ready for implementation

## Problem Statement

The Debug UI currently lives in `Samples~/DebugUI/`. Users can import it via Package Manager's Samples UI, but:
- Copied to `Assets/Samples/...` with nested path
- Manual deletion required on updates
- No version tracking
- Friction for users

## Solution

Editor script that copies Debug UI from `Samples~/` to `Assets/SorollaDebugUI/` with:
- One-click install via SorollaWindow button
- Version tracking (`.installed_version` file)
- Update detection and confirmation dialog
- Menu item to add prefab to scene

## Architecture

### File Structure

```
Packages/com.sorolla.sdk/
├── Samples~/DebugUI/                    # Source (unchanged)
│   ├── Scripts/
│   ├── Prefabs/
│   ├── Sprites/
│   ├── Resources/
│   ├── Scenes/
│   ├── Sorolla.DebugUI.asmdef
│   └── *.meta files                     # GUIDs preserved on copy
│
├── Editor/DebugUI/
│   └── DebugUIInstaller.cs              # NEW - install/update logic
│
└── Editor/SorollaWindow.cs              # MODIFY - add UI button
```

### User's Project (after install)

```
Assets/SorollaDebugUI/                   # Copied destination
├── Scripts/
├── Prefabs/
│   └── SorollaDebugUI.prefab            # User drags to scene
├── Sprites/
├── Resources/                           # Only FakeATT/CMP dialogs
├── Scenes/
├── Sorolla.DebugUI.asmdef
└── .installed_version                   # e.g., "3.2.0"
```

## Implementation Details

### 1. DebugUIInstaller.cs

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    /// Handles Debug UI installation and updates.
    /// </summary>
    public static class DebugUIInstaller
    {
        const string SourceFolder = "Samples~/DebugUI";
        const string DestFolder = "Assets/SorollaDebugUI";
        const string VersionFileName = ".installed_version";

        static string SourcePath => Path.GetFullPath($"Packages/com.sorolla.sdk/{SourceFolder}");
        static string DestPath => Path.Combine(Application.dataPath, "SorollaDebugUI");
        static string VersionFilePath => Path.Combine(DestPath, VersionFileName);
        static string PrefabPath => $"{DestFolder}/Prefabs/SorollaDebugUI.prefab";

        static string SdkVersion => UnityEditor.PackageManager.PackageInfo
            .FindForAssembly(typeof(DebugUIInstaller).Assembly)?.version ?? "0.0.0";

        /// <summary>
        /// Whether Debug UI is installed in the project.
        /// </summary>
        public static bool IsInstalled => Directory.Exists(DestPath);

        /// <summary>
        /// Installed version, or null if not installed.
        /// </summary>
        public static string InstalledVersion
        {
            get
            {
                if (!IsInstalled || !File.Exists(VersionFilePath))
                    return null;
                return File.ReadAllText(VersionFilePath).Trim();
            }
        }

        /// <summary>
        /// Whether an update is available.
        /// </summary>
        public static bool UpdateAvailable =>
            IsInstalled && InstalledVersion != SdkVersion;

        /// <summary>
        /// Install Debug UI to project.
        /// </summary>
        public static void Install()
        {
            if (IsInstalled)
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Update Debug UI?",
                    "This will overwrite any local modifications to the Debug UI.\n\n" +
                    $"Installed: {InstalledVersion ?? "unknown"}\n" +
                    $"Available: {SdkVersion}",
                    "Update",
                    "Cancel");

                if (!confirm) return;

                // Delete existing
                Directory.Delete(DestPath, recursive: true);
                // Delete .meta file too
                var metaPath = DestPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
            }

            try
            {
                CopyDirectory(SourcePath, DestPath);
                File.WriteAllText(VersionFilePath, SdkVersion);
                AssetDatabase.Refresh();

                Debug.Log($"[Palette] Debug UI installed to {DestFolder}");

                EditorUtility.DisplayDialog(
                    "Debug UI Installed",
                    "Debug UI has been installed.\n\n" +
                    "To use it:\n" +
                    "1. Open your scene\n" +
                    "2. Menu: Palette > Debug UI > Add to Scene\n" +
                    "   (or drag the prefab manually)\n\n" +
                    "Toggle: Triple-tap (mobile) or Backtick key (desktop)",
                    "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Palette] Failed to install Debug UI: {e.Message}");
                EditorUtility.DisplayDialog(
                    "Installation Failed",
                    $"Failed to install Debug UI:\n{e.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// Uninstall Debug UI from project.
        /// </summary>
        public static void Uninstall()
        {
            if (!IsInstalled)
            {
                Debug.LogWarning("[Palette] Debug UI is not installed.");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog(
                "Uninstall Debug UI?",
                "This will remove the Debug UI from your project.\n\n" +
                "Any local modifications will be lost.",
                "Uninstall",
                "Cancel");

            if (!confirm) return;

            Directory.Delete(DestPath, recursive: true);
            var metaPath = DestPath + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);

            AssetDatabase.Refresh();
            Debug.Log("[Palette] Debug UI uninstalled.");
        }

        /// <summary>
        /// Add Debug UI prefab to current scene.
        /// </summary>
        [MenuItem("Palette/Debug UI/Add to Scene")]
        public static void AddToScene()
        {
            if (!IsInstalled)
            {
                EditorUtility.DisplayDialog(
                    "Debug UI Not Installed",
                    "Please install Debug UI first via Palette > Configuration.",
                    "OK");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Palette] Debug UI prefab not found at {PrefabPath}");
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Add Debug UI");
                Selection.activeGameObject = instance;
                Debug.Log("[Palette] Debug UI added to scene. Toggle: Triple-tap or Backtick key.");
            }
        }

        [MenuItem("Palette/Debug UI/Add to Scene", validate = true)]
        static bool AddToSceneValidate() => IsInstalled;

        [MenuItem("Palette/Debug UI/Uninstall")]
        static void UninstallMenuItem() => Uninstall();

        [MenuItem("Palette/Debug UI/Uninstall", validate = true)]
        static bool UninstallValidate() => IsInstalled;

        /// <summary>
        /// Recursively copy directory.
        /// </summary>
        static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(source))
            {
                var fileName = Path.GetFileName(file);
                // Skip .DS_Store and other hidden files (except .meta and .installed_version)
                if (fileName.StartsWith(".") && !fileName.EndsWith(".meta"))
                    continue;

                File.Copy(file, Path.Combine(dest, fileName));
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var dirName = Path.GetFileName(dir);
                // Skip hidden directories
                if (dirName.StartsWith("."))
                    continue;

                CopyDirectory(dir, Path.Combine(dest, dirName));
            }
        }
    }
}
```

### 2. SorollaWindow.cs Modifications

Add Debug UI section to the main UI:

```csharp
void DrawDebugUISection()
{
    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

    EditorGUILayout.BeginHorizontal();
    GUILayout.Label("Debug UI", EditorStyles.boldLabel);
    GUILayout.FlexibleSpace();

    if (!DebugUIInstaller.IsInstalled)
    {
        if (GUILayout.Button("Install", GUILayout.Width(70)))
            DebugUIInstaller.Install();
    }
    else if (DebugUIInstaller.UpdateAvailable)
    {
        GUILayout.Label($"v{DebugUIInstaller.InstalledVersion}", s_configStyleYellow);
        if (GUILayout.Button("Update", GUILayout.Width(70)))
            DebugUIInstaller.Install();
    }
    else
    {
        GUILayout.Label($"v{DebugUIInstaller.InstalledVersion}", s_configStyleGreen);
        if (GUILayout.Button("Add to Scene", GUILayout.Width(90)))
            DebugUIInstaller.AddToScene();
    }

    EditorGUILayout.EndHorizontal();

    // Help text
    if (!DebugUIInstaller.IsInstalled)
    {
        EditorGUILayout.LabelField(
            "Optional debug panel for testing SDK features.",
            EditorStyles.miniLabel);
    }
    else
    {
        EditorGUILayout.LabelField(
            "Toggle: Triple-tap (mobile) or ` key (desktop)",
            EditorStyles.miniLabel);
    }

    EditorGUILayout.EndVertical();
}
```

Call `DrawDebugUISection()` in `DrawMainUI()` after other sections.

## User Flow

### Fresh Install
```
1. User opens Palette > Configuration
2. Sees "Debug UI" section with [Install] button
3. Clicks Install
4. Files copied to Assets/SorollaDebugUI/
5. Success dialog with instructions
6. User clicks Palette > Debug UI > Add to Scene
7. Prefab added to scene, ready to use
```

### SDK Update
```
1. User updates SDK (new version)
2. Opens Palette > Configuration
3. Sees "Debug UI" section showing "v3.1.0" in yellow with [Update] button
4. Clicks Update
5. Confirmation dialog warns about overwriting local changes
6. Confirms → files replaced
7. Done
```

### Uninstall
```
1. User clicks Palette > Debug UI > Uninstall
2. Confirmation dialog
3. Confirms → folder deleted
4. Done
```

## Testing Checklist

- [ ] Install on fresh project (no existing Debug UI)
- [ ] Install when folder already exists (should prompt)
- [ ] Update detection (change SDK version, verify button changes)
- [ ] Update preserves functionality
- [ ] Uninstall removes all files
- [ ] "Add to Scene" works after install
- [ ] "Add to Scene" disabled when not installed
- [ ] Prefab works in Play mode (triple-tap toggle)
- [ ] asmdef references resolve (no compile errors)
- [ ] Works when SDK installed from git URL (PackageCache)

## Build Impact

| Scenario | Build Size Impact |
|----------|------------------|
| Debug UI not installed | Zero |
| Debug UI installed, dev build | ~500KB-1MB |
| Debug UI installed, release build | ~500KB-1MB (prefab included but never instantiated) |

Note: The prefab is in `Assets/SorollaDebugUI/` (not Resources), but Unity still includes referenced assets. To achieve zero release build impact, user should:
1. Remove Debug UI from scenes before release build, OR
2. Use build script to exclude the folder, OR
3. Accept the minimal size impact

## Future Improvements (out of scope)

- Build preprocessor to auto-exclude Debug UI from release builds
- In-prefab version display
- Auto-update prompt on SDK import
- Selective update (preserve user modifications to specific files)

## References

- [Unity Manual: Creating samples for packages](https://docs.unity3d.com/Manual/cus-samples.html)
- [Unity Manual: Access package assets using scripts](https://docs.unity3d.com/Manual/upm-assets.html)
- [Unity Scripting API: FileUtil.CopyFileOrDirectory](https://docs.unity3d.com/ScriptReference/FileUtil.CopyFileOrDirectory.html)

## Decision Log

| Decision | Rationale |
|----------|-----------|
| Copy instead of .unitypackage | Simpler, no package generation needed, same UX |
| No Resources folder for main prefab | Zero build bloat when not in scene |
| Manual "Add to Scene" | Trade-off: slight friction vs zero release build impact |
| Version file instead of scanning | Simpler, reliable, no edge cases |
| Overwrite on update | Users who customize should backup; simplest approach |

---

## Gotchas & Decisions Explained

This section documents the pitfalls we discovered during design and why we made certain choices.

### Gotcha 1: Resources.Load() doesn't work from UPM packages

**Discovery**: Initially proposed putting Debug UI in `Runtime/Resources/` for auto-loading.

**Problem**: UPM packages live outside `Assets/` (in `Library/PackageCache/` or `Packages/`). Unity's `Resources.Load()` only scans `Assets/**/Resources/` folders. This is a fundamental Unity limitation.

**Source**: [Unity Discussions - How to load assets at runtime from within a UPM package?](https://discussions.unity.com/t/how-to-load-assets-at-runtime-from-within-a-upm-package/757513)

**Solution**: Copy files to `Assets/` where `Resources.Load()` works, OR don't use Resources at all (our choice - manual scene setup).

### Gotcha 2: Resources folders cannot be excluded from builds

**Discovery**: Proposed using `IPreprocessBuildWithReport` to strip Resources from release builds.

**Problem**: Unity cannot exclude Resources automatically because `Resources.Load()` can be called with dynamically constructed strings at runtime. Unity must include everything.

**Workaround exists but is fragile**: Temporarily rename `Resources/` to `Resources~/` during build, restore after. Risky if build fails mid-process.

**Source**: [Unity Support - Excluding Scripts and Assets from builds](https://support.unity.com/hc/en-us/articles/208456906-Excluding-Scripts-and-Assets-from-builds)

**Solution**: Don't use Resources for the main prefab. User adds to scene manually → only included if actually in a scene.

### Gotcha 3: .meta files exist in Samples~ folders

**Discovery**: Worried that copying from `Samples~/` would lose GUIDs and break prefab references.

**Reality**: Unity DOES generate .meta files for Samples~ contents (even though the folder is hidden from AssetDatabase). When we copy, .meta files come along, preserving all GUIDs.

**Verification**: `ls -la Packages/com.sorolla.sdk/Samples~/DebugUI/` shows .meta files present.

**Implication**: Prefab → script references work correctly after copy. No GUID regeneration issues.

### Gotcha 4: Packages/ is a virtual path

**Concern**: When SDK is installed from git URL or registry, it lives in `Library/PackageCache/com.sorolla.sdk@x.y.z/`. Can we still access `Samples~/`?

**Answer**: Yes. `Path.GetFullPath("Packages/com.sorolla.sdk/Samples~/DebugUI")` resolves to the correct filesystem path regardless of where the package actually lives.

**Source**: [Unity Manual - Access package assets using scripts](https://docs.unity3d.com/Manual/upm-assets.html)

### Gotcha 5: FileUtil.CopyFileOrDirectory fails if destination exists

**Problem**: Unity's `FileUtil.CopyFileOrDirectory()` throws an error if the destination path already exists.

**Solution**: Always delete the destination folder before copying:
```csharp
if (Directory.Exists(destPath))
    Directory.Delete(destPath, recursive: true);
```

### Gotcha 6: Must delete .meta file when deleting folder

**Problem**: When deleting `Assets/SorollaDebugUI/`, the `.meta` file at `Assets/SorollaDebugUI.meta` remains, causing warnings.

**Solution**: Explicitly delete the .meta file too:
```csharp
var metaPath = destPath + ".meta";
if (File.Exists(metaPath))
    File.Delete(metaPath);
```

### Gotcha 7: .DS_Store files on macOS

**Problem**: macOS creates `.DS_Store` files in folders. Copying these to the user's project is messy.

**Solution**: Filter out hidden files (starting with `.`) except `.meta` files:
```csharp
if (fileName.StartsWith(".") && !fileName.EndsWith(".meta"))
    continue;
```

### Gotcha 8: asmdef references by name, not path

**Verification needed**: Debug UI's `Sorolla.DebugUI.asmdef` references `Sorolla.Runtime` by assembly name.

**Why it works**: Unity resolves assembly references by name globally, not by path. After copying to `Assets/SorollaDebugUI/`, the reference to `Sorolla.Runtime` still resolves to the package's assembly.

**Verified in**: `Samples~/DebugUI/Sorolla.DebugUI.asmdef` shows `"references": ["Sorolla.Runtime", ...]`

---

## Alternatives Considered & Rejected

### Alternative A: Keep as Unity Sample (status quo)

**How it works**: User imports via Package Manager > Samples tab.

**Rejected because**:
- Copies to `Assets/Samples/Sorolla SDK/x.y.z/DebugUI/` (ugly nested path)
- No version tracking - user must manually delete and re-import on updates
- Package Manager UI doesn't show "update available" for samples

### Alternative B: Embedded .unitypackage

**How it works**: Ship `.unitypackage` inside the UPM package, import via `AssetDatabase.ImportPackage()`.

**Advantages**:
- Unity's import dialog shows changed files
- Native Unity workflow

**Rejected because**:
- Must regenerate .unitypackage on every release (maintenance burden)
- Same end result as simple copy
- More moving parts for no real benefit

### Alternative C: Runtime auto-load from Resources

**How it works**: Put prefab in `Runtime/Resources/`, auto-instantiate in dev builds.

**Rejected because**:
- Resources.Load() doesn't work from UPM packages (Gotcha 1)
- Even if it did, Resources are always included in builds (Gotcha 2)

### Alternative D: Addressables

**How it works**: Mark Debug UI as Addressable, load on demand.

**Rejected because**:
- Adds heavyweight dependency (com.unity.addressables)
- Overkill for a debug tool
- Most users don't use Addressables

### Alternative E: Procedural UI (no prefab)

**How it works**: Build entire Debug UI in code using IMGUI or UI Toolkit.

**Rejected because**:
- Massive refactor of existing UI
- Harder to maintain and customize
- Loses visual editing in Unity

### Alternative F: Resilient path scanning

**How it works**: Instead of checking fixed path, scan project for `DebugPanelManager.cs` or similar.

**Rejected because**:
- Overkill - if user moves/renames folder, they're a power user
- Adds complexity for edge case
- Simple path check is sufficient
