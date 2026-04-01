# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Entry Point:** Read `.claude/INDEX.md` FIRST for context, then drill into specific docs as needed.

## SDK Overview

**Sorolla SDK** is a plug-and-play mobile publisher SDK for Unity. It wraps GameAnalytics, Facebook, AppLovin MAX, Adjust, Firebase, and TikTok behind a unified `Palette` API.

This is a **standalone Git repository** (not part of the parent Unity project):
- Repo: https://github.com/sorolla-studio/sorolla-palette
- Commits run from this directory (see Git Policy below)

## Editor Commands

| Menu Path | Purpose |
|-----------|---------|
| Palette > Configuration | Mode setup, SDK installation, build health |
| Palette > Run Setup (Force) | Re-run initial setup (troubleshooting) |

## Validation & Build

`BuildValidator.cs` runs automatically before builds via `IPreprocessBuildWithReport`:
- Checks SDK version mismatches
- Validates mode consistency
- Ensures scoped registries configured
- Sanitizes AndroidManifest.xml (auto-fix for orphaned entries)
- Validates MAX SDK key in AppLovinSettings

## Architecture

### Stub + Implementation Pattern (UPM-based SDKs)

Optional SDK adapters use separate assemblies to avoid "assembly not found" errors:

```
Adapters/
├── Sorolla.Adapters.asmdef       # Stubs (no external refs, always compiles)
├── MaxAdapter.cs                  # Stub: static class with IMaxAdapter field
├── MAX/
│   ├── Sorolla.Adapters.MAX.asmdef   # defineConstraints + versionDefines
│   ├── AssemblyInfo.cs               # [AlwaysLinkAssembly]
│   └── MaxAdapterImpl.cs             # [RuntimeInitializeOnLoadMethod] registers impl
```

**Key insight**: `versionDefines` are **per-assembly only** (not project-wide). Each implementation asmdef must define its own symbols.

### Native Bridge Pattern (non-UPM SDKs)

For SDKs without UPM packages (e.g., TikTok), the impl lives alongside the stub — no separate asmdef, no `defineConstraints`. The impl uses platform-specific `#if UNITY_ANDROID`/`#if UNITY_IOS` blocks with JNI (Android) or `[DllImport]` (iOS). Native .aar/.framework files go in `Runtime/Plugins/`. The adapter is always compiled but no-ops if config fields are empty.

### Two Define Symbol Systems

The SDK uses **two complementary systems** for conditional compilation:

1. **Per-assembly `versionDefines`** (in `.asmdef` files) — detects package presence and sets symbols scoped to that assembly only. Used with `defineConstraints` to skip entire assembly compilation.

2. **Global scripting defines** (`DefineSymbols.cs`) — detects packages via `Client.List()` on domain reload and sets `PlayerSettings` defines (`SOROLLA_MAX_ENABLED`, `APPLOVIN_MAX_INSTALLED`, `ADJUST_SDK_INSTALLED`, `FIREBASE_*_INSTALLED`). These are project-wide and used by `Palette.cs` with `#if` blocks.

Both are needed: per-assembly prevents compilation of impl assemblies when SDKs are missing; global defines gate code in the main Palette assembly.

### Auto-Sync Systems (Editor)

- **`SdkVersionSync.cs`** — `[InitializeOnLoad]`, runs on every domain reload. Compares installed manifest.json versions against `SdkRegistry` constants. Auto-updates stale entries (catches SDK upgrades that bump `SdkRegistry` but leave old manifest entries).
- **`MaxVersionChecker.cs`** — `[InitializeOnLoadMethod]`, runs once per editor session. Queries AppLovin registry for latest MAX version, prompts Update/Skip/Later dialog.

### Adding a New SDK Adapter

**UPM-based SDK (has a Unity package):**
1. Add to `SdkRegistry.cs` (ID, package name, version, scope, requirement)
2. Create stub in `Adapters/XxxAdapter.cs` with `IXxxAdapter` interface
3. Create impl folder `Adapters/Xxx/` with:
   - `Sorolla.Adapters.Xxx.asmdef` (with defineConstraints + versionDefines)
   - `AssemblyInfo.cs` with `[assembly: AlwaysLinkAssembly]`
   - `XxxAdapterImpl.cs` with `[Preserve]` and `[RuntimeInitializeOnLoadMethod]`
4. Add define mapping in `DefineSymbols.cs` if `Palette.cs` needs `#if` access
5. Add initialization call in `Palette.Initialize()`
6. Add UI section in `SorollaWindow.cs`

**Native-bridge SDK (no Unity package, e.g., TikTok):**
1. Create stub `Adapters/XxxAdapter.cs` with `IXxxAdapter` interface
2. Create impl `Adapters/XxxAdapterImpl.cs` using `#if UNITY_ANDROID`/`#if UNITY_IOS` platform guards
3. Add native libs to `Runtime/Plugins/Android/` and `Runtime/Plugins/iOS/`
4. Add config fields to `SorollaConfig.cs`
5. Add conditional init in `Palette.Initialize()` (guarded by config field presence, not defines)
6. If Android needs dependencies, add `TikTokDependencies.xml` for EDM4U

### IL2CPP Stripping Protection

Three layers required for `[RuntimeInitializeOnLoadMethod]` to work in IL2CPP builds:
- `[assembly: AlwaysLinkAssembly]` - Forces linker to process assembly
- `[Preserve]` on class and Register method - Marks as roots
- `link.xml` in Assets/ - Fallback (NOT auto-included from packages)

## Critical Learnings (from DEVLOG.md)

**Unity asmdef**:
- `versionDefines` + `defineConstraints` BOTH needed for optional assemblies
- `defineConstraints` prevents compilation when symbol not set
- `defineConstraints` does NOT support `||` (boolean OR) in a single string - Unity silently treats unrecognized expressions as satisfied. Use a single symbol per constraint entry.

**MAX SDK**:
- SDK key is in AppLovinSettings (Integration Manager), NOT SorollaConfig
- `MaxSdk.SetSdkKey()` is deprecated - SDK reads from settings automatically
- Quality Service causes 401 build failures - auto-disabled by sanitizer

**Consent Flow (CMP-First)**:
- MAX handles consent flow automatically: CMP (UMP) → ATT (iOS)
- **Requires Google Ad Manager (or AdMob) mediated network installed in MAX Integration Manager** - without it, MAX has no Google Mobile Ads SDK bridge and the UMP consent form silently won't render
- Enable in Integration Manager: Terms & Privacy Policy Flow + iOS ATT
- SorollaBootstrapper just calls `Palette.Initialize()` - no manual ATT handling

**Firebase Android Build Pipeline**:
- `Firebase.Editor.dll` auto-generates `Assets/Plugins/Android/FirebaseApp.androidlib/` from `google-services.json` on domain reload
- This folder contains `res/values/google-services.xml` with processed Firebase config (API keys, project ID, etc.)
- `FirebaseCrashlytics.androidlib/` is also generated (build ID, Unity version)
- Also generates `Assets/GeneratedLocalRepo/` (local Maven repo for Gradle)
- **Without these folders, Android builds crash at launch**: "Default FirebaseApp failed to initialize because no default options were found"
- The `**APPLY_PLUGINS**` placeholder in `mainTemplate.gradle` is populated by Firebase Editor to inject `com.google.gms.google-services` plugin
- Common triggers for missing androidlib: switching from symlink to UPM git URL, clean import, deleting Library folder
- Fix: domain reload in Unity, then `Assets > External Dependency Manager > Android Resolver > Force Resolve` if still missing

**EDM4U + Unity 6**:
- Bundles Gradle 5.1.1, incompatible with Java 17+ (Unity 6 default)
- First resolution may fail, works after restart (EDM4U auto-recovers via Gradle template mode)

## Key Files

| File | Purpose |
|------|---------|
| `Runtime/Palette.cs` | Main public API (static class) |
| `Runtime/SorollaBootstrapper.cs` | Auto-init via [RuntimeInitializeOnLoadMethod] |
| `Runtime/SorollaConfig.cs` | ScriptableObject in Resources/ |
| `Editor/Sdk/SdkRegistry.cs` | Single source of truth for SDK metadata + versions |
| `Editor/Sdk/SdkVersionSync.cs` | Auto-updates manifest versions on domain reload |
| `Editor/Sdk/DefineSymbols.cs` | Global scripting defines based on installed packages |
| `Editor/BuildValidator.cs` | Pre-build validation |
| `Editor/MaxVersionChecker.cs` | Auto-check for MAX SDK updates per session |
| `Editor/SorollaWindow.cs` | Configuration UI |
| `DEVLOG.md` | Historical learnings - consult when debugging |

## Namespaces

- `Sorolla.Palette` - Public API (`Palette` static class)
- `Sorolla.Palette.Adapters` - SDK wrappers (stubs + impls)
- `Sorolla.Palette.ATT` - iOS privacy utils (legacy, kept for soft prompts)
- `Sorolla.Palette.Editor` - Editor tools

## Mode System

| Mode | Required SDKs | Optional | Use Case |
|------|---------------|----------|----------|
| Prototype | GameAnalytics, Facebook | MAX, Firebase | CPI tests, soft launch |
| Full | GameAnalytics, Facebook, MAX, Adjust, Firebase | — | Production |

TikTok is mode-independent — enabled when config fields are populated, disabled when empty.

Mode stored in EditorPrefs, runtime config in `Resources/SorollaConfig.asset`.

## Release Checklist

**Files to update:**
| File | What to change |
|------|----------------|
| `package.json` | `"version": "X.Y.Z"` |
| `CHANGELOG.md` | Add `## [X.Y.Z] - YYYY-MM-DD` section |
| `README.md` | Install URL `...git#vX.Y.Z` |
| `Documentation~/quick-start.md` | Install URL `...git#vX.Y.Z` |

**Commands:**
```bash
git add package.json CHANGELOG.md README.md Documentation~/quick-start.md
git commit -m "chore: bump version to X.Y.Z"
git tag -a vX.Y.Z -m "vX.Y.Z - Short description"
git push origin master --tags
```

## Git Policy

**This is a Unity project. Never use `git add -A`, `git add .`, or stage files without reviewing each one by name.**

Unity auto-modifies files just by opening the editor: `.asset` files (Addressables, URP settings, editor prefs), `.dll.meta` (serialization UID changes), scene files (reflection probes, lighting), Library state. These must never be included in a feature commit unless they are explicitly part of the change.

**Required workflow for every commit:**
1. `git status` - review the full list of changed files
2. Separate signal from noise: identify which files are part of the change vs Unity editor auto-touches
3. `git add file1 file2 file3` - stage by explicit name only
4. `git diff --cached --stat` - verify staged files before committing
5. If unsure whether a file belongs, leave it out

A missing file is trivial to add. A dirty commit requires a reset and force push.
