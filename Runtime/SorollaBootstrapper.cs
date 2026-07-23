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
    ///     MAX SDK handles consent flow (CMP → ATT) automatically.
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
            // a second time and double-subscribe MAX callbacks during the CMP window.
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
#if UNITY_IOS && !UNITY_EDITOR
    #if !(SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED)
            // No MAX → handle ATT manually (native dialog only, no soft prompt)
            // Wait for the app to be fully visible before requesting ATT.
            // Calling too early (before window scene is active) causes iOS to
            // silently drop the request and return NOT_DETERMINED without showing the dialog.
            yield return null; // let first frame render
            yield return new WaitForSeconds(1f); // ensure app has focus

            var currentStatus = ATTBridge.GetStatus();
            if (currentStatus == ATTBridge.AuthorizationStatus.NotDetermined)
            {
                bool attResponseReceived = false;
                ATTBridge.RequestAuthorization(_ =>
                {
                    attResponseReceived = true;
                });

                // Wait for the user to respond to the ATT dialog
                while (!attResponseReceived)
                    yield return null;
            }

            var finalStatus = ATTBridge.GetStatus();
            PaletteLog.Vital($"[Palette] Standalone ATT resolved: {finalStatus}");
            // Resolve ATT BEFORE Initialize so Palette.ResolveBootSignals reads the final status.
            // No-MAX boot keeps analytics ON regardless of ATT (no ads to gate).
            Palette.Initialize();
            // Ship ATT decision to analytics. Palette.Initialize set IsInitialized=true
            // on the non-MAX path so this fires immediately (not queued).
            Palette.TrackEvent("att_decision", new Dictionary<string, object>
            {
                { "att_status", Palette.AttString(finalStatus) },
                { "source", "standalone" },
            });
            yield break;
    #endif
#endif
            // MAX installed: CMP → ATT handled by MAX during its async init. Palette resolves boot
            // signals to analytics-ON / ads-denied; ad consent is elevated after MAX resolves CMP
            // (OnMaxConsentChanged). Non-iOS / Editor without MAX: no ATT, ads absent, analytics ON.
            Palette.Initialize();
            yield break;
        }
    }
}
