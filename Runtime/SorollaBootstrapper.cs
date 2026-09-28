using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sorolla.Palette.Adapters;
using Sorolla.Palette.ATT;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Entry point for Palette SDK.
    ///     Auto-initializes at startup - NO MANUAL SETUP REQUIRED.
    ///     Runs the consent flow (Google UMP, then ATT on iOS) before ads start.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SorollaBootstrapper : MonoBehaviour
    {
        static SorollaBootstrapper s_instance;

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        // Returning to the foreground can mean the user changed ATT in iOS Settings while
        // we were backgrounded. Re-resolve consent so the new status reaches every vendor. Guarded on
        // IsInitialized so the pre-consent window is skipped (nothing to re-fan yet), and on the
        // singleton so a duplicate bootstrapper about to be destroyed never triggers it.
        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && s_instance == this && Palette.IsInitialized)
                Palette.OnAppFocusRegained();
        }

        void Start()
        {
            // Only the AutoInit-created instance drives initialization. A second bootstrapper
            // (e.g. one manually dropped into a scene) would otherwise call Palette.Initialize
            // and run the consent flow a second time.
            if (s_instance != this)
            {
                PaletteLog.Warning("[Palette] Extra SorollaBootstrapper found - the SDK auto-creates its own. Destroying this duplicate.");
                Destroy(this);
                return;
            }
            EnsurePersistent();
            StartCoroutine(Initialize());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoInit()
        {
            if (s_instance != null) return;

            SorollaDiagnostics.EnsureLogBridge();
            PaletteLog.Vital("[Palette] Auto-initializing...");
            SorollaDiagnostics.RecordAutoInitStarted();

            var go = new GameObject("[Palette SDK]");
            MakePersistent(go);
            SorollaDebugMenuLauncher.Ensure(go);
            QaBridgeServer.Ensure(go);
            s_instance = go.AddComponent<SorollaBootstrapper>();

            // Lend our coroutine host to adapters that need delayed callbacks
            // (e.g. MAX exponential-backoff retries). Adapter assemblies have no
            // MonoBehaviour of their own and would otherwise spawn parallel GOs.
            MaxAdapter.ScheduleDelegate = Schedule;
        }

        internal static void Schedule(float delaySeconds, Action callback)
        {
            if (s_instance == null) { callback?.Invoke(); return; }
            s_instance.StartCoroutine(DelayedInvoke(delaySeconds, callback));
        }

        static IEnumerator DelayedInvoke(float delaySeconds, Action callback)
        {
            // Realtime so app-pause naturally pauses the timer (Update doesn't tick
            // when suspended), and Time.timeScale=0 doesn't stall ad retries.
            yield return new WaitForSecondsRealtime(delaySeconds);
            callback?.Invoke();
        }

        static void MakePersistent(GameObject go)
        {
            try
            {
                DontDestroyOnLoad(go);
                PaletteLog.Verbose("[Palette] Successfully marked GameObject as persistent");
            }
            catch (Exception e)
            {
                // At BeforeSceneLoad, scene context may not be ready on some platforms.
                // EnsurePersistent() in Start() will retry when the scene is valid.
                PaletteLog.Verbose($"[Palette] DontDestroyOnLoad deferred to Start(): {e.Message}");
            }
        }

        /// <summary>
        ///     Fallback: if MakePersistent failed at BeforeSceneLoad (scene not ready),
        ///     retry now that we're in Start() with a valid scene context.
        /// </summary>
        void EnsurePersistent()
        {
            if (gameObject.scene.name == "DontDestroyOnLoad") return;

            try
            {
                DontDestroyOnLoad(gameObject);
                PaletteLog.Verbose("[Palette] Successfully marked GameObject as persistent (deferred)");
            }
            catch (Exception e)
            {
                PaletteLog.Error($"[Palette] Failed to persist SDK GameObject: {e.Message}");
            }
        }

        IEnumerator Initialize()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            // Full: analytics start now with ads denied. Then Google UMP (consent update, then its form
            // where required), then ATT on iOS, and only then MAX (Palette.OnConsentGathered): AppLovin's
            // own consent flow stays off, so the player never sees its terms alert.
            Palette.Initialize();
    #if UNITY_IOS && !UNITY_EDITOR
            yield return WaitForVisibleApp();
    #endif
            bool? umpAnswered = null;
            UmpBridge.Gather(answered => umpAnswered = answered);
            while (umpAnswered == null)
                yield return null;
    #if UNITY_IOS && !UNITY_EDITOR
            yield return RequestAttIfNotDetermined();
    #endif
            Palette.OnConsentGathered(umpAnswered.Value);
#elif UNITY_IOS && !UNITY_EDITOR
            // Prototype on iOS: no GDPR form and no ads. Resolve ATT BEFORE Initialize so the boot
            // fan-out reads the final status; analytics stay ON regardless of ATT (no ads to gate).
            yield return WaitForVisibleApp();
            yield return RequestAttIfNotDetermined();

            var finalStatus = ATTBridge.GetStatus();
            PaletteLog.Vital($"[Palette] Standalone ATT resolved: {finalStatus}");
            Palette.Initialize();
            // Ship ATT decision to analytics. Palette.Initialize set IsInitialized=true
            // on the non-MAX path so this fires immediately (not queued).
            Palette.TrackEvent("att_decision", new Dictionary<string, object>
            {
                { "att_status", Palette.AttString(finalStatus) },
                { "source", "standalone" },
            });
#else
            // Prototype elsewhere: no ATT, ads absent, analytics ON.
            Palette.Initialize();
            yield break;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        // Wait for the app to be fully visible before a consent form or the ATT request. Calling too
        // early (before the window scene is active) makes iOS silently drop the ATT request and
        // return NOT_DETERMINED without showing the dialog.
        static IEnumerator WaitForVisibleApp()
        {
            yield return null; // let first frame render
            yield return new WaitForSecondsRealtime(1f); // ensure app has focus, even in a game paused at timeScale 0
        }

        // Native ATT dialog only (no soft prompt). Skipped once the status is determined, including
        // when Google UMP's IDFA explainer message already asked during the consent flow.
        static IEnumerator RequestAttIfNotDetermined()
        {
            if (ATTBridge.GetStatus() != ATTBridge.AuthorizationStatus.NotDetermined) yield break;

            bool attResponseReceived = false;
            ATTBridge.RequestAuthorization(_ => attResponseReceived = true);

            // Wait for the user to respond to the ATT dialog
            while (!attResponseReceived)
                yield return null;
        }
#endif
    }
}
