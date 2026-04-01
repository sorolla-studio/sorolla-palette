# Sorolla SDK Learnings

Format: `[YYYY-MM-DD] #tag: insight`
Greppable: `grep "#unity" LEARNINGS.md`

---

## Android Build System

[2026-03-31] #android #gradle #r8: R8 version pins in baseProjectTemplate.gradle are AGP-version-specific. The R8 8.1.56 pin needed for AGP 7.4.2 (Unity 2022) crashes AGP 8.10.0 (Unity 6) with `setBuildMetadataConsumer` NoSuchMethodError. AGP 8.10.0's bundled R8 handles Kotlin 2.0 metadata natively - remove the pin after upgrading.

[2026-03-31] #android #unity6: `androidApplicationEntry` in ProjectSettings.asset is a bitmask, not a boolean. `1` = Activity (legacy), `2` = GameActivity, `3` = both. Unity 2022 only has Activity. Unity 6 defaults to GameActivity for new projects but preserves Activity when upgrading.

[2026-03-31] #android #manifest #unity6: Unity 6 uses split Gradle modules. When `useCustomLauncherManifest: 1`, `AndroidManifest.xml` goes to unityLibrary module, `LauncherManifest.xml` goes to launcher module. Unity generates `android:enabled="false"` on the non-selected activity in the library manifest. The launcher manifest must override with `enabled="true"` + `tools:replace="android:enabled"`.

[2026-03-31] #android #manifest: Unity 6's deployment checker reads source manifests, not the Gradle-merged result. If the launcher activity isn't visible in the source manifest Unity checks, `DeploymentOperationFailedException` fires even if the APK is correct. Workaround: declare the launcher activity in BOTH AndroidManifest.xml and LauncherManifest.xml.

[2026-03-31] #android #manifest #facebook: Facebook SDK's manifest sanitizer (`FacebookManifestSanitizer`) is an AssetPostprocessor that fires on every AndroidManifest.xml import. It uses `#if UNITY_2023_1_OR_NEWER` to force `UnityPlayerGameActivity`, ignoring `androidApplicationEntry`. This fights any manual fix to the manifest. Must be updated to read PlayerSettings instead.

[2026-03-31] #android #manifest: Multiple manifest patchers (Palette sanitizer, Facebook sanitizer, Unity auto-gen) will fight each other. When a manifest edit keeps reverting, check for AssetPostprocessors: `grep OnPostprocessAllAssets` across all Editor scripts.

[2026-03-31] #unity #packages: Editing .cs files in a symlinked UPM package doesn't always trigger recompilation. Touch the .asmdef or reimport the package folder to force it.

## Unity Version Compatibility Matrix

[2026-03-31] #android #compatibility:

| Unity Version | AGP | R8 Pin Needed | Activity Class | androidApplicationEntry |
|--------------|-----|---------------|----------------|------------------------|
| 2022.3 LTS | 7.4.2 | Yes (8.1.56+) for Kotlin 2.0 libs | UnityPlayerActivity | 1 (only option) |
| 2023.1-2023.2 | 7.x-8.x | Depends on AGP version | Both available | 1=Activity, 2=GameActivity |
| 6000.x (Unity 6) | 8.10.0 | No - bundled R8 is sufficient | Both, GameActivity default | 1=Activity, 2=GameActivity |

