# Android Build Compatibility Guide

How the Sorolla SDK interacts with Unity's Android build system across Unity versions.

---

## Unity Version Matrix

| | Unity 2022.3 LTS | Unity 2023.x | Unity 6 (6000.x) |
|---|---|---|---|
| **AGP** | 7.4.2 | 7.x - 8.x | 8.10.0 |
| **R8 pin needed** | Yes (8.1.56+) | Depends on AGP | No |
| **Activity class** | UnityPlayerActivity only | Both available | Both, GameActivity default |
| **`androidApplicationEntry`** | 1 (only option) | 1=Activity, 2=GameActivity | 1=Activity, 2=GameActivity |
| **Split manifest** | No | Partial | Yes (launcher + library modules) |
| **Minimum Gradle** | 7.x | 7.x - 8.x | 8.x |
| **JDK** | 11 or 17 | 17 | 17 |

---

## androidApplicationEntry

This PlayerSettings field controls which Activity class Unity compiles into the APK.

It is a **bitmask**, not a simple enum:
- `1` = Activity (legacy `com.unity3d.player.UnityPlayerActivity`)
- `2` = GameActivity (`com.unity3d.player.UnityPlayerGameActivity`)
- `3` = both
- `0` = none (error: "At least one application entry has to be selected")

Unity 2022 only supports Activity (value is always `1`). Unity 6 defaults to GameActivity (`2`) for new projects but preserves the existing value when upgrading, so upgraded projects keep `1`.

**SDK impact**: The `AndroidManifestSanitizer.GetExpectedMainActivity()` reads this field to determine the correct activity class. Any manifest patching code must use this value, not compile-time version checks like `#if UNITY_2023_1_OR_NEWER`.

---

## R8 / AGP Compatibility

### Unity 2022 (AGP 7.4.2)

AGP 7.4.2 bundles an old R8 that crashes on Kotlin 2.0 metadata (`mv=[2,0,0]`). Libraries compiled with Kotlin 2.0 include AppLovin SDK 13.x and Firebase 23.x.

**Required**: Pin R8 8.1.56+ in `baseProjectTemplate.gradle`:
```gradle
buildscript {
    repositories {
        google()
        mavenCentral()
    }
    dependencies {
        classpath "com.android.tools:r8:8.1.56"
    }
}
```

### Unity 6 (AGP 8.10.0)

AGP 8.10.0 bundles a modern R8 that handles Kotlin 2.0 natively. The R8 pin from Unity 2022 **must be removed** - it causes `NoSuchMethodError: setBuildMetadataConsumer` because the old R8 is incompatible with AGP 8.10.0's dex merger.

---

## Split Manifest Architecture (Unity 6)

Unity 6 builds Android projects with two Gradle modules:

```
launcher/          <- LauncherManifest.xml (if useCustomLauncherManifest=1)
  - Main activity, launcher intent filter
unityLibrary/      <- AndroidManifest.xml (if useCustomMainManifest=1)
  - SDK activities, receivers, permissions
```

### Key behaviors

1. Unity generates both `UnityPlayerActivity` and `UnityPlayerGameActivity` in the library manifest, with `android:enabled="false"` on the non-selected one.

2. The launcher manifest must override `enabled` to `true` using `tools:replace`:
```xml
<activity android:name="com.unity3d.player.UnityPlayerGameActivity"
          android:enabled="true"
          tools:replace="android:enabled">
```

3. The MAIN/LAUNCHER intent filter should be in **both** manifests - the library one for Unity's deployment checker, and the launcher one for the actual APK.

4. Unity's deployment checker reads source manifests, not the Gradle-merged result. This is a known Unity bug (related to the activity-alias fix in 6000.0.63f1).

### useCustomLauncherManifest

When this flag is `0`, Unity auto-generates the launcher manifest. When `1`, it uses `Assets/Plugins/Android/LauncherManifest.xml`. Studios sometimes enable this accidentally (along with all other custom template flags) without populating the file properly.

---

## Manifest Patching Conflicts

Multiple systems may modify AndroidManifest.xml:

| System | When | What it does |
|--------|------|-------------|
| Palette `AndroidManifestSanitizer` | Pre-build (`IPreprocessBuildWithReport`) | Fixes wrong activity class, removes orphaned entries |
| Facebook SDK `FacebookManifestSanitizer` | On asset import (`AssetPostprocessor`) | Forces activity class based on Unity version |
| EDM4U | On resolve | Adds repositories and dependencies |
| Unity | On build | Generates library manifest, merges with custom |

**Conflict risk**: If two patchers disagree on the expected activity class, they will revert each other's changes in a loop. The Facebook SDK's AssetPostprocessor fires on every import, so it always gets the last word before build.

**Resolution**: All patchers must read `androidApplicationEntry` from PlayerSettings, not use compile-time checks.

---

## Upgrading Unity Versions

When a studio upgrades Unity (e.g., 2022 -> 6):

### Checklist

1. **Remove R8 pin** from `baseProjectTemplate.gradle` if upgrading to Unity 6
2. **Check `androidApplicationEntry`** - upgrade preserves old value (`1`), decide whether to switch to GameActivity (`2`)
3. **Check `useCustomLauncherManifest`** - if enabled, ensure `LauncherManifest.xml` has correct activity declaration
4. **Check AGP version** - Unity auto-upgrades AGP, which changes Gradle DSL (deprecated attributes like `aaptOptions` -> `androidResources`)
5. **Run `Palette > Run Setup (Force)`** to reconfigure EDM4U for the new Gradle version
6. **Verify manifest activity class** matches `androidApplicationEntry` in both custom manifests
