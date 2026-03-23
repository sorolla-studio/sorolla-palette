# Bug Report: Platform Detection + Double Init + ATT (Fabian / COG Interactive)

**Date:** 2026-03-10
**Reporter:** Fabian (COG Interactive) via Slack #technical-questions
**SDK Version:** 3.4.1
**Context:** Fabian's first scene "initializes and goes away instantly" (splash/loading screen pattern)

---

## Console Output (Unity Editor, Android build target)

```
[11:13:24] [Palette:TikTok] Implementation registered
[11:13:24] [Palette] Auto-initializing...
[11:13:24] [Palette] Successfully marked GameObject as persistent
[11:13:24] Current platform is: OSXEditor; Platform Type is: Desktop
[11:13:24] [Palette] Already initialized.
[11:13:24] InAppPurchasing: IStoreService.Connect called without a callback...
```

## Fabian's Messages

> "Do I need to call anything in code? Or does the popup attach to first scene? (first scene initializes and goes away instantly)"
>
> "I don't have iPhone, in Editor I see these: [...] I could debug on MacOS version. Dude look, it says Current platform: OSXEditor but I am on Android. Might be reversed on iOS, and never shows popup"

Arthur replied: "No need to call anything for the ATT to show up. Looking at the logs, do you see anything unusual?" and "V3.4.1 should have fixed it"

---

## Analysis: 3 Issues (1 non-bug, 2 real)

### Issue 1: "Current platform is: OSXEditor" — NOT A SOROLLA BUG

**Source:** GameAnalytics SDK internal logging (NOT from Sorolla code). Grep confirms zero matches for "Current platform" in any Sorolla source file. The log lacks a `[Palette]` tag.

**Why it shows OSXEditor:** `Application.platform` is a RUNTIME value — it always returns `RuntimePlatform.OSXEditor` when running in the Unity Editor, regardless of build target. This is standard Unity behavior.

**Our SDK is correct:** All platform-specific code uses COMPILE-TIME guards (`#if UNITY_IOS`, `#if UNITY_ANDROID`) which ARE correctly set by the build target. The `PlatformAdUnitId.Current` property, ATT flow, and all adapter code use compile-time guards.

**Action needed:** Explain to Fabian that this is expected GameAnalytics behavior. The runtime platform log is irrelevant — our SDK uses compile-time platform detection which is correct.

### Issue 2: "[Palette] Already initialized." — DOUBLE INITIALIZATION (REAL BUG)

**The warning at `Palette.cs:462`:**
```csharp
if (IsInitialized)
{
    Debug.LogWarning($"{Tag} Already initialized.");
    return;
}
```

**Root cause — most likely:** Fabian is calling `Palette.Initialize()` manually in his game code, in addition to the auto-init from `SorollaBootstrapper`. The SDK auto-initializes via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` — no manual call needed. Fabian's question "Do I need to call anything in code?" suggests he may have added a manual call.

**Alternative cause:** A second `SorollaBootstrapper` instance somehow bypassing the singleton guard. Less likely given the `if (s_instance != null) return;` check in `AutoInit()`.

**The init sequence from logs:**
1. `[Palette:TikTok] Implementation registered` — adapter static registration
2. `[Palette] Auto-initializing...` — `SorollaBootstrapper.AutoInit()` fires at `BeforeSceneLoad`
3. `[Palette] Successfully marked GameObject as persistent` — `DontDestroyOnLoad` succeeds
4. `Current platform is: OSXEditor...` — GameAnalytics logs during `Palette.Initialize()` (first call — succeeds)
5. `[Palette] Already initialized.` — second call to `Palette.Initialize()` is blocked

**The double-init is harmless** (the guard catches it), but it indicates a usage problem that should be addressed.

**Fix plan:**
1. Improve the warning message to help diagnose the source:
   ```csharp
   Debug.LogWarning($"{Tag} Already initialized. If you see this, you likely have a manual " +
       "Palette.Initialize() call — remove it. The SDK auto-initializes via SorollaBootstrapper.");
   ```
2. Optionally add a stack trace to the warning to pinpoint the caller:
   ```csharp
   Debug.LogWarning($"{Tag} Already initialized.\n{System.Environment.StackTrace}");
   ```
3. Consider making `Palette.Initialize()` internal instead of public to prevent external calls. **Breaking change** — evaluate if any games legitimately call it.

### Issue 3: ATT Not Showing on iOS — THEORETICAL RISK (NOT CONFIRMED)

**Status:** Fabian says "I don't have iPhone" — he's SPECULATING based on the OSXEditor log. He hasn't actually tested on iOS. Arthur says "V3.4.1 should have fixed it."

**However, there's a real architectural risk with Fabian's "first scene goes away instantly" pattern:**

The ATT flow in `SorollaBootstrapper.Initialize()` (lines 76-110):
```csharp
#if UNITY_IOS && !UNITY_EDITOR
    #if !(SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED)
        // Standalone ATT path (no MAX)
        yield return null;                    // wait one frame
        yield return new WaitForSeconds(1f);  // wait 1 second for app focus
        // ... ATT dialog request + wait for response ...
        Palette.Initialize(consent);
        yield break;
    #endif
#endif
    // MAX path OR non-iOS: immediate init
    Palette.Initialize(consent: true);
```

**The risk:** The ATT coroutine waits ~1+ seconds (one frame + 1s delay + user interaction time). If `DontDestroyOnLoad` works, the bootstrapper survives scene transitions and the coroutine completes normally. BUT:

1. **`MakePersistent` scene validity check** (`SorollaBootstrapper.cs:54`):
   ```csharp
   if (!go.scene.IsValid() || !go.scene.isLoaded)
   {
       Debug.LogWarning("[Palette] Invalid scene context, deferring DontDestroyOnLoad");
       return;  // ← DontDestroyOnLoad NEVER called!
   }
   ```
   At `BeforeSceneLoad` timing, the scene may not be valid on all Unity versions/platforms. If this check fails, the bootstrapper is NOT persistent — and when Fabian's first scene "goes away instantly," the bootstrapper is destroyed mid-ATT-coroutine. **The log shows "Successfully marked as persistent" in the Editor, but on-device behavior may differ.**

2. **No fallback if persistence fails.** The warning says "deferring DontDestroyOnLoad" but there's no actual deferral mechanism — it just returns and the GO is never made persistent.

**Fix plan:**
1. **Remove or simplify the scene validity check.** The `BeforeSceneLoad` timing with `new GameObject()` works in modern Unity (2022.3+). The triple-defense pattern is over-engineered and the "Layer 2" check can actually PREVENT the persistence it's trying to guarantee:
   ```csharp
   static void MakePersistent(GameObject go)
   {
       try
       {
           DontDestroyOnLoad(go);
       }
       catch (Exception e)
       {
           Debug.LogError($"[Palette] DontDestroyOnLoad failed: {e.Message}");
       }
   }
   ```
2. **Add a deferred persistence fallback.** If `DontDestroyOnLoad` can't be called at `BeforeSceneLoad`, queue it for `Start()`:
   ```csharp
   void Start()
   {
       // Ensure persistence even if MakePersistent failed at BeforeSceneLoad
       if (gameObject.scene.name != "DontDestroyOnLoad")
           DontDestroyOnLoad(gameObject);
       StartCoroutine(Initialize());
   }
   ```
3. **Add a diagnostic log** for the ATT coroutine lifecycle to help debug on-device:
   ```csharp
   Debug.Log("[Palette] ATT: Starting authorization flow...");
   // ... after completion:
   Debug.Log($"[Palette] ATT: Completed ({finalStatus})");
   ```

---

## File Map

| File | Lines | Role |
|------|-------|------|
| `Runtime/SorollaBootstrapper.cs` | 113 | Entry point, auto-init, ATT coroutine, MakePersistent |
| `Runtime/Palette.cs` | 754 | Main API, `Initialize()` at line 458, double-init guard at 460 |
| `Runtime/ATT/ATTBridge.cs` | 63 | Native iOS ATT bridge, compile-time guards correct |
| `Runtime/GameAnalyticsAdapter.cs` | 124 | GA wrapper, NOT source of platform log |
| `Runtime/SorollaConfig.cs` | 75 | Config with `PlatformAdUnitId.Current` using compile-time `#if` |

## Priority

1. **Fix `MakePersistent`** — remove/simplify the scene validity check + add Start() fallback. This is the only issue that could cause real user-facing breakage (ATT not showing).
2. **Improve double-init warning** — help devs identify the source. Low effort, high debugging value.
3. **Reply to Fabian** — explain the platform log is expected, ask if he's calling `Palette.Initialize()` manually, confirm ATT works on iOS device builds.

## Reply Draft for Fabian

> Hey Fabian! Three things:
>
> 1. **"Current platform: OSXEditor"** — That's normal. It's a GameAnalytics log, not ours. `Application.platform` always shows the editor platform in Editor. Our SDK uses compile-time platform detection which is correct for your build target.
>
> 2. **"Already initialized"** — Are you calling `Palette.Initialize()` manually anywhere in your code? The SDK auto-initializes — no manual call needed. If you have one, remove it.
>
> 3. **ATT on iOS** — The SDK handles ATT automatically. Since you mentioned your first scene goes away instantly, we're pushing a patch (v3.4.2) to make the bootstrapper more robust in that scenario. Can you test on an iOS device after the update?
