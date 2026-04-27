# Architecture Guide

Technical reference for contributors working on the Sorolla SDK.

---

## Overview

**Sorolla SDK** is a plug-and-play mobile publisher SDK for Unity. It wraps GameAnalytics, Facebook, AppLovin MAX, Adjust, Firebase, and TikTok behind a unified `Palette` API.

### Design Principles

1. **Zero-Configuration** - Auto-initializes at runtime
2. **Conditional Compilation** - Adapters only compile when SDK installed
3. **Mode-Based** - Prototype/Full modes determine required SDKs
4. **Single Source of Truth** - `SdkRegistry.cs` contains all SDK metadata
5. **DX-First API Design** - Studios are game makers, not SDK integrators. SDK absorbs complexity; studios express intent. See [DX-First API Design](#dx-first-api-design) below.

### DX-First API Design

**Context**: Sorolla is the publisher and the tracker. Studios focus on making games - they don't do custom analytics, they don't know MMP verification formats, they don't speak ISO 4217. Every primitive parameter (`double`, `string`, `bool`) on a public API is a future silent-data-corruption bug. The reference example is the `3.9.2` `TrackPurchase` hotfix: the method accepted tier-index as amount and `"Tier"` as currency and fired the event anyway, polluting Adjust revenue data for weeks - no compile error, no runtime error, silent.

**Rules - load-bearing for every new or modified public API:**

1. **Rich types over primitives.** If a richer type carries the data (`UnityEngine.Purchasing.Product`, `Exception`, `ConsentStatus` enum, a schema-generated key), take it. Derive primitives inside the SDK. Never make studios extract what we can extract.

2. **One-line integration per feature, no alternatives.** If the feature can be "wrap once, forget", build that — and then make it the *only* path so studios cannot get it wrong. Reference pattern for Unity IAP v5: studio calls `Palette.AttachPurchaseTracking(_storeController)` once immediately after `UnityIAPServices.StoreController()` — that's it. The SDK owns the `OnPurchasePending` subscription and runs TxID dedup internally, so Google Play's in-session double-fire and Unity's crash-replay warning on `OnPurchasePending` cannot produce duplicate analytics. `Palette.TrackPurchase` is `internal` as of 3.14.1 — studios have no code path that can fire a purchase event directly, by design. (The legacy `Palette.Purchasing.AutoTracker` wrapping `IDetailedStoreListener` is Obsolete in v5 per https://docs.unity.com/en-us/iap/upgrade-to-iap-v5 — retained only as a transition shim with `[Obsolete]` warnings.)

3. **Silent misuse is a critical bug.** If a call accepts wrong data and fires anyway, validate. Drop or warn loud, with a pointer to the recommended API. Catches bugs at integration time instead of after weeks of polluted dashboards.

4. **Automate anything studios would have to do manually.** Receipt parsing, platform splits, verification routing, consent resolution, key lookup - not studio problems. If Unity exposes a lifecycle event, listener, or callback, wrap it. Ship an auto-hook before requiring a manual call.

5. **No "flexible escape hatch" rationalization.** Sorolla designs the events, the schema, and the taxonomy. Studios call named methods. A feature that justifies itself with "games need flexibility here" is suspect - build the structured API or don't expose it. Custom analytics is Sorolla's job, not the studio's.

6. **Schema-driven, not stringly-typed.** If Sorolla owns a schema (Remote Config keys, user properties, ad placements, in-game currency names), generate typed accessors. No magic strings in studio code.

7. **Three questions before merging any public API.**
   - What's the minimum studio must pass? Can we derive the rest?
   - If they pass wrong values, does it fail loud or silent? Silent = fix it.
   - Can we offer a one-line "register once and forget" automation?

**Retrofit rule**: when touching an existing public API, apply the same three questions. Most pre-`3.9.2` APIs predate this principle and have landmines - fix them as they come up in the course of other work, or batch them by priority.

---

## Package Structure

```
Runtime/
├── Palette.cs                 ← Main public API (static class)
├── SorollaBootstrapper.cs     ← Auto-init via [RuntimeInitializeOnLoadMethod]
├── SorollaConfig.cs           ← ScriptableObject configuration
├── SorollaLoadingOverlay.cs   ← Ad loading UI helper
├── GameAnalyticsAdapter.cs    ← GA wrapper (always included)
├── Adapters/
│   ├── Sorolla.Adapters.asmdef     ← Core stubs (no external refs)
│   ├── MaxAdapter.cs               ← Stub (delegates to impl)
│   ├── AdjustAdapter.cs            ← Stub (delegates to impl)
│   ├── FacebookAdapter.cs          ← Facebook (#if SOROLLA_FACEBOOK_ENABLED)
│   ├── FirebaseAdapter.cs          ← Stub (delegates to impl)
│   ├── FirebaseCrashlyticsAdapter.cs  ← Stub
│   ├── FirebaseRemoteConfigAdapter.cs ← Stub
│   ├── FirebaseCoreManager.cs      ← Stub
│   ├── TikTokAdapter.cs           ← Stub (native bridge pattern)
│   ├── TikTokAdapterImpl.cs       ← Impl (JNI/DllImport, always compiled)
│   ├── Firebase/                   ← Separate assembly (defineConstraints)
│   │   ├── Sorolla.Adapters.Firebase.asmdef
│   │   ├── FirebaseAdapterImpl.cs
│   │   ├── FirebaseCoreManagerImpl.cs
│   │   ├── FirebaseCrashlyticsAdapterImpl.cs
│   │   └── FirebaseRemoteConfigAdapterImpl.cs
│   ├── MAX/                        ← Separate assembly (defineConstraints)
│   │   ├── Sorolla.Adapters.MAX.asmdef
│   │   └── MaxAdapterImpl.cs
│   └── Adjust/                     ← Separate assembly (defineConstraints)
│       ├── Sorolla.Adapters.Adjust.asmdef
│       └── AdjustAdapterImpl.cs
└── ATT/
    ├── ContextScreenView.cs   ← Pre-ATT explanation screen
    └── FakeATTDialog.cs       ← Editor testing

Editor/
├── SorollaWindow.cs           ← Main configuration window
├── SorollaSettings.cs         ← Mode persistence (EditorPrefs)
├── SorollaMode.cs             ← Enum: None, Prototype, Full
├── SorollaIOSPostProcessor.cs ← Xcode post-processing
├── ManifestManager.cs         ← manifest.json manipulation
└── Sdk/
    ├── SdkRegistry.cs         ← SDK metadata & versions
    ├── SdkDetector.cs         ← Check if SDK installed
    ├── SdkInstaller.cs        ← Install/uninstall SDKs
    └── DefineSymbols.cs       ← Scripting define management
```

---

## Initialization Flow

```
[RuntimeInitializeOnLoadMethod]
    ↓
SorollaBootstrapper.AutoInit()
    ↓
Creates "[Sorolla SDK]" GameObject (DontDestroyOnLoad)
    ↓
iOS: Check ATT status → Show ContextScreen if needed
    ↓
Palette.Initialize(consent)
    ├── GameAnalyticsAdapter.Initialize()      ← Always
    ├── FacebookAdapter.Initialize()           ← Always
    ├── MaxAdapter.Initialize()                ← If configured
    │   └── OnSdkInitialized → AdjustAdapter.Initialize()
    ├── FirebaseAdapter.Initialize()           ← If enabled
    ├── FirebaseCrashlyticsAdapter.Initialize()
    └── FirebaseRemoteConfigAdapter.Initialize()
    ↓
IsInitialized = true
```

---

## Adapter Pattern

Optional SDK adapters use a **Stub + Implementation** pattern with separate assemblies:

### Why This Pattern?
Unity resolves assembly references **before** evaluating `#if` preprocessor blocks. This means `#if SDK_DEFINE` guards don't prevent "assembly not found" errors when the SDK isn't installed.

**Solution**: Use separate assembly definitions with `versionDefines` + `defineConstraints`:

1. **versionDefines** detect if SDK package is installed → sets a scripting symbol
2. **defineConstraints** prevent compilation if symbol not set → assembly excluded entirely

> **IMPORTANT**: `versionDefines` symbols are **per-assembly only** (not project-wide). Each implementation asmdef must define its own version defines AND use them in defineConstraints.

### Structure

**Stub (always compiles)** - `Adapters/XxxAdapter.cs`:
```csharp
namespace Sorolla.Palette.Adapters
{
    internal interface IXxxAdapter
    {
        void Initialize();
        void DoSomething();
    }

    public static class XxxAdapter
    {
        private static IXxxAdapter s_impl;

        internal static void RegisterImpl(IXxxAdapter impl)
        {
            s_impl = impl;
        }

        public static void Initialize()
        {
            if (s_impl != null) s_impl.Initialize();
            else Debug.LogWarning("[Sorolla] SDK not installed");
        }

        public static void DoSomething() => s_impl?.DoSomething();
    }
}
```

**Implementation (separate assembly)** - `Adapters/Xxx/XxxAdapterImpl.cs`:
```csharp
using ThirdPartySDK;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    [Preserve]
    internal class XxxAdapterImpl : IXxxAdapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            XxxAdapter.RegisterImpl(new XxxAdapterImpl());
        }

        public void Initialize() { ThirdPartySDK.Init(); }
        public void DoSomething() { ThirdPartySDK.DoSomething(); }
    }
}
```

**Assembly Info** - `Adapters/Xxx/AssemblyInfo.cs`:
```csharp
using UnityEngine.Scripting;

// Force linker to process this assembly for RuntimeInitializeOnLoadMethod
[assembly: AlwaysLinkAssembly]
```

**Assembly Definition** - `Adapters/Xxx/Sorolla.Adapters.Xxx.asmdef`:
```json
{
    "name": "Sorolla.Adapters.Xxx",
    "references": ["ThirdPartySDK", "Sorolla.Adapters"],
    "defineConstraints": ["XXX_SDK_INSTALLED"],
    "versionDefines": [
        {
            "name": "com.xxx.sdk",
            "expression": "",
            "define": "XXX_SDK_INSTALLED"
        }
    ]
}
```

> **Note**: Both `versionDefines` AND `defineConstraints` are required. The versionDefines sets the symbol when the package is detected, and defineConstraints prevents compilation when the symbol is not set.

### How It Works
1. **SDK not installed**:
   - `versionDefines` condition not met → symbol NOT defined
   - `defineConstraints` not satisfied → Unity skips entire assembly (no compilation attempted)
2. **SDK installed**:
   - `versionDefines` detects package → defines symbol (e.g., `APPLOVIN_MAX_INSTALLED`)
   - `defineConstraints` satisfied → assembly compiles
   - `[AlwaysLinkAssembly]` forces IL2CPP linker to process it
   - `[RuntimeInitializeOnLoadMethod]` auto-registers impl at runtime
3. **At runtime**: Stub delegates to impl if registered, otherwise gracefully handles missing SDK

### IL2CPP Code Stripping Protection

For IL2CPP builds, we use a belt-and-suspenders approach:

| Mechanism | Purpose |
|-----------|---------|
| `[assembly: AlwaysLinkAssembly]` | Forces linker to **process** assembly (doesn't preserve by itself) |
| `[Preserve]` on class | Marks class as root, prevents stripping |
| `[Preserve]` on Register() | Marks method as root |
| `link.xml` (fallback) | Manual override - auto-copied to Assets/ on setup |

> **CRITICAL (Unity Documentation)**:
> - `link.xml` files are **NOT auto-included from UPM packages** - must be in `Assets/` folder
> - `[AlwaysLinkAssembly]` only forces linker to **process** the assembly, not preserve it
> - Both `[AlwaysLinkAssembly]` AND `[Preserve]` are needed for packages with `[RuntimeInitializeOnLoadMethod]`
>
> Source: [Unity Docs - Preserving code](https://docs.unity3d.com/Manual/managed-code-stripping-preserving.html)

### Scripting Defines

All defines are auto-managed - never set manually. Three categories with different scopes:

**Auto-managed by `DefineSymbols.cs` (global PlayerSettings):**

| Define | Set when package installed |
|---|---|
| `APPLOVIN_MAX_INSTALLED` | `com.applovin.mediation.ads` |
| `SOROLLA_MAX_ENABLED` | `com.applovin.mediation.ads` |
| `ADJUST_SDK_INSTALLED` | `com.adjust.sdk` |
| `SOROLLA_ADJUST_ENABLED` | `com.adjust.sdk` |
| `FIREBASE_ANALYTICS_INSTALLED` | `com.google.firebase.analytics` |
| `FIREBASE_CRASHLYTICS_INSTALLED` | `com.google.firebase.crashlytics` |
| `FIREBASE_REMOTE_CONFIG_INSTALLED` | `com.google.firebase.remote-config` |

**Per-assembly versionDefines (NOT in PlayerSettings):**

| Define | Assembly | Detects |
|---|---|---|
| `SOROLLA_FACEBOOK_ENABLED` | `Sorolla.Runtime`, `Sorolla.Adapters` | Facebook SDK package |
| `SOROLLA_*_ASMDEF_OK` | Implementation asmdefs | Used with `defineConstraints` to gate assembly compilation |

**Mode define (set by Palette > Configuration):**

- `SOROLLA_PROTOTYPE` or `SOROLLA_FULL` - mutually exclusive, set by `SorollaSettings.SetMode()`

---

## Mode System

### Prototype Mode
- **Required**: GameAnalytics, Facebook SDK
- **Optional**: AppLovin MAX, Firebase
- **Use case**: CPI tests, soft launches

### Full Mode
- **Required**: GameAnalytics, Facebook SDK, AppLovin MAX, Adjust, Firebase
- **Use case**: Production with monetization

### Switching Modes

```csharp
// Editor/SorollaSettings.cs
public static void SetMode(SorollaMode mode) {
    // 1. Save to EditorPrefs
    // 2. Install required SDKs
    // 3. Uninstall mode-specific SDKs
    // 4. Update SorollaConfig.isPrototypeMode
    // 5. Apply scripting defines
}
```

---

## SDK Registry

Single source of truth for all SDK information:

```csharp
// Editor/Sdk/SdkRegistry.cs
public enum SdkId {
    GameAnalytics, Facebook, AppLovinMAX, Adjust,
    FirebaseApp, FirebaseAnalytics, FirebaseCrashlytics, FirebaseRemoteConfig
}

public enum SdkRequirement { Core, PrototypeOnly, FullOnly, FullRequired, Optional }

// Version constants
public const string GA_VERSION = "7.10.6";
public const string MAX_VERSION = "8.5.0";
public const string FIREBASE_VERSION = "12.10.1";
```

---

## Data Flow

### Event Tracking (v3.7.0+)
```
Palette.TrackEvent("post_score", { score: 1200, level: "world1" })
    ├── FirebaseAdapter.TrackEvent()             ← Full structured params
    └── GameAnalyticsAdapter.TrackDesignEvent()  ← Best-effort (name + first numeric value)

Palette.Level.Complete(3, world: 1, score: 1500, extraParams: { duration_sec: 45 })
    ├── GameAnalyticsAdapter.TrackProgressionEvent()  ← Always (GA schema)
    └── FirebaseAdapter.TrackProgressionEvent()       ← If enabled (GA4 level_end + extraParams)

Palette.Economy.Earn(CurrencyId.Coins, 50, EconomySource.DailyReward, itemId: "daily_login")
    ├── GameAnalyticsAdapter.TrackResourceEvent()  ← Always (GA schema)
    └── FirebaseAdapter.TrackResourceEvent()       ← If enabled (earn_virtual_currency)
```

### Ad Revenue
```
Palette.ShowRewardedAd()
    └── MaxAdapter.ShowRewardedAd()
        └── OnAdRevenuePaidEvent
            ├── AdjustAdapter.TrackAdRevenue()
            ├── FirebaseAdapter.TrackAdImpression()
            └── GameAnalytics ILRD
```

### Remote Config
```
Palette.GetRemoteConfig(key, default)
    ├── Firebase ready? → return Firebase value
    ├── GA ready? → return GA value
    └── else → return default

Real-time (v3.7.0+):
    Firebase real-time listener → OnConfigUpdated event
    ├── AutoActivateUpdates = true  → values active immediately
    └── AutoActivateUpdates = false → call ActivateRemoteConfigAsync() manually
```

### Purchase Attribution
```
Palette.AttachPurchaseTracking(store)                    ← ONLY studio-facing entry point (3.14.1+)
    └── store.OnPurchasePending += [internal handler]    ← SDK-owned subscription, studios cannot attach
        └── internal Palette.TrackPurchase(PendingOrder) ← Reads Info.TransactionID / Info.Receipt
                                                           while order is still Pending
                                                           (consumables lose both on ConfirmPurchase)
            └── internal Palette.TrackPurchase(amount, currency, productId, transactionId, purchaseToken)
                    │   ── TxID dedup enforced here ──   ← Session-wide HashSet<string> on
                    │                                      non-empty transactionId. Blocks
                    │                                      Google Play in-session doubles and
                    │                                      Unity-documented crash-replay
                    │                                      before fan-out. Fails open on
                    │                                      empty TxID.
                    ├── AdjustAdapter.TrackPurchase()     ← Platform-routed verification
                    ├── TikTokAdapter.TrackPurchase()     ← If enabled
                    └── FirebaseAdapter.TrackPurchase()   ← If enabled (analytics only, no verification)

Legacy shims (Obsolete in v5, internal — unreachable from studio code):
- Palette.TrackPurchase(Product)           ← v4 Product.transactionID/.receipt are [Obsolete]
- Palette.Purchasing.AutoTracker           ← wraps IDetailedStoreListener, Obsolete in v5
```

---

## Common Tasks

### Adding a New SDK

1. Add entry to `SdkRegistry.cs`
2. Create adapter in `Runtime/Adapters/`
3. Add initialization call in `Palette.Initialize()`
4. Add detection define in `DefineSymbols.cs`
5. Add UI section in `SorollaWindow.cs`

### Updating SDK Versions

1. Update version in `SdkRegistry.cs`
2. Update `CHANGELOG.md`
3. Bump version in `package.json`

### Adding New API Method

1. Add public method to `Palette.cs`
2. Call relevant adapters
3. Add XML documentation

---

## Quick Reference

### Key Files
| File | Purpose |
|------|---------|
| `Runtime/Palette.cs` | Main public API |
| `Runtime/SorollaBootstrapper.cs` | Auto-initialization |
| `Runtime/Adapters/MaxAdapter.cs` | Ad mediation |
| `Editor/SorollaWindow.cs` | Configuration UI |
| `Editor/Sdk/SdkRegistry.cs` | SDK metadata & versions |
| `Editor/BuildValidator.cs` | Pre-build validation |

### Namespaces
| Namespace | Purpose |
|-----------|---------|
| `Sorolla.Palette` | Public API |
| `Sorolla.Palette.Adapters` | SDK wrappers |
| `Sorolla.Palette.ATT` | iOS privacy |
| `Sorolla.Palette.Editor` | Editor tools |

### Critical Paths
```
Init:      Bootstrapper → ATT → Palette.Initialize()
Events:    Palette → Adapters → Third-party SDKs
Ads:       ShowRewardedAd → MaxAdapter → AdjustAdapter.TrackAdRevenue
Config:    GetRemoteConfig → Firebase → GA → default
Purchase:  TrackPurchase → Adjust (verify) + TikTok + Firebase
```

## Build System

### Pre-build Validation

`BuildValidator` runs at `callbackOrder -100` (before Adjust at 0, MAX at int.MaxValue - 10) via `IPreprocessBuildWithReport`. Checks SDK versions, mode consistency, Firebase config files, Android manifest, R8/AGP config.

**Auto-fixes** (safe, reversible, with backup):
- R8 version pin removal on Unity 6 (always crashes with AGP 8.10.0)
- Android manifest orphan cleanup

**Warn only** (may be intentional):
- Kotlin stdlib version forcing

### Console Breadcrumb

`BuildHealthConsoleNotifier` (`[InitializeOnLoad]`) logs a single warning on domain reload if build health errors exist. No popup, no window - just a breadcrumb.
