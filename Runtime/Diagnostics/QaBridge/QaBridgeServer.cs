using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     QA-only loopback HTTP bridge. Serves <c>GET /qa/snapshot</c> (structured SDK state) so the
    ///     qa-greenlight agent asserts on JSON instead of grepping device logs. Reached over a USB
    ///     forward (<c>adb forward</c> / usbmux). Binds 127.0.0.1 only, never 0.0.0.0, so it never
    ///     trips the iOS Local Network prompt and is not reachable off-device.
    ///
    ///     Access-gated, not build-gated (Arthur 2026-06-12): compiled into ALL builds. In the Editor
    ///     and development builds it auto-starts. In release builds it stays DORMANT (never binds the
    ///     port) until a human arms it from the debug UI; arming is not persisted, so a relaunch starts
    ///     dormant again. The debug UI itself still gates access (5-tap today).
    ///
    ///     Threading: HttpListener callbacks run on worker threads. They only enqueue the context;
    ///     every Unity/SDK read and the response write happen on the main thread in <see cref="Update"/>
    ///     (the bridge's own runner: it must not assume the on-screen console exists).
    /// </summary>
    internal sealed class QaBridgeServer : MonoBehaviour
    {
        internal const int Port = 8765;
        const string Prefix = "http://127.0.0.1:8765/";

        static QaBridgeServer s_instance;

        readonly ConcurrentQueue<HttpListenerContext> _pending = new ConcurrentQueue<HttpListenerContext>();
        HttpListener _listener;
        volatile bool _running;

        /// <summary>True while the listener is bound and accepting (the snapshot's <c>armed</c> field).</summary>
        internal static bool IsArmed => s_instance != null && s_instance._running;

        internal static void Ensure(GameObject host)
        {
            if (s_instance != null) return;

            s_instance = host.GetComponent<QaBridgeServer>();
            if (s_instance == null)
                s_instance = host.AddComponent<QaBridgeServer>();
        }

        /// <summary>Binds the port and starts accepting. Called automatically in Editor/dev builds, or by the debug UI in release.</summary>
        internal static void Arm()
        {
            if (s_instance == null) return;
            s_instance.StartServer();
        }

        /// <summary>Stops accepting and releases the port.</summary>
        internal static void Disarm()
        {
            if (s_instance == null) return;
            s_instance.StopServer();
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            s_instance = this;

            // The bridge owns its diagnostics plumbing so it works without the on-screen console:
            // capture Palette log markers and route C# exceptions into the runtime-problems snapshot.
            SorollaDiagnostics.EnsureLogBridge();
            SorollaDiagnostics.InstallUnityLogSink();

            if (ShouldAutoStart())
                StartServer();
        }

        // Editor + development builds auto-start so the agent dev loop needs no human. Release builds
        // stay dormant until armed from the debug UI (access-gated model).
        static bool ShouldAutoStart() => Application.isEditor || Debug.isDebugBuild;

        void StartServer()
        {
            if (_running) return;

            if (!HttpListener.IsSupported)
            {
                PaletteLog.Vital("[Palette:QaBridge] HttpListener unsupported on this platform; bridge disabled.");
                return;
            }

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Prefix);
                _listener.Start();
                _running = true;
                _listener.BeginGetContext(OnContext, null);
                PaletteLog.Vital($"[Palette:QaBridge] Listening on {Prefix}");
            }
            catch (Exception e)
            {
                // Bind failure (port in use, platform restriction) must never crash the game over QA
                // plumbing. Log one line and stay dormant; the agent remaps the local forward side.
                _running = false;
                SafeCloseListener();
                PaletteLog.Vital($"[Palette:QaBridge] Could not bind {Prefix}: {e.Message}");
            }
        }

        void StopServer()
        {
            if (!_running && _listener == null) return;

            _running = false;
            SafeCloseListener();

            while (_pending.TryDequeue(out HttpListenerContext ctx))
                TryAbort(ctx);

            PaletteLog.Vital("[Palette:QaBridge] Stopped.");
        }

        void SafeCloseListener()
        {
            if (_listener == null) return;
            try { _listener.Close(); }
            catch { /* already disposed */ }
            _listener = null;
        }

        // Worker thread: accept the next request, immediately queue the following accept, and hand the
        // context to the main thread. No Unity/SDK calls here.
        void OnContext(IAsyncResult ar)
        {
            HttpListener listener = _listener;
            if (listener == null || !_running) return;

            HttpListenerContext ctx;
            try
            {
                ctx = listener.EndGetContext(ar);
            }
            catch
            {
                return; // listener stopped/disposed mid-accept
            }

            try
            {
                if (_running)
                    listener.BeginGetContext(OnContext, null);
            }
            catch
            {
                // listener stopped between EndGetContext and the next accept
            }

            // Re-check after the StopServer drain window: if teardown already ran, this context would
            // otherwise be enqueued and never drained (Update early-returns once !_running). Abort it.
            if (!_running)
            {
                TryAbort(ctx);
                return;
            }

            _pending.Enqueue(ctx);
        }

        void Update()
        {
            if (!_running) return;

            // Drive the diagnostics identifier-refresh poll from the bridge's own runner so the
            // snapshot stays current even when the on-screen console is closed.
            SorollaDiagnostics.UpdatePolling();

            while (_pending.TryDequeue(out HttpListenerContext ctx))
                HandleRequest(ctx);
        }

        // Main thread: safe to read Unity/SDK state and write the response.
        void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url?.AbsolutePath ?? "";
                string method = ctx.Request.HttpMethod;

                if (path == "/qa/snapshot" && method == "GET")
                {
                    WriteJson(ctx, 200, QaSnapshot.Build());
                    return;
                }

                WriteJson(ctx, 404, "{\"ok\":false,\"detail\":\"unknown_endpoint\"}");
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"[Palette:QaBridge] Request handling failed: {e.Message}");
                TryAbort(ctx);
            }
        }

        static void WriteJson(HttpListenerContext ctx, int statusCode, string json)
        {
            try
            {
                byte[] payload = Encoding.UTF8.GetBytes(json);
                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentType = "application/json";
                ctx.Response.ContentLength64 = payload.Length;
                ctx.Response.OutputStream.Write(payload, 0, payload.Length);
                ctx.Response.OutputStream.Close();
            }
            catch
            {
                TryAbort(ctx);
            }
        }

        static void TryAbort(HttpListenerContext ctx)
        {
            try { ctx.Response.Abort(); }
            catch { /* connection already gone */ }
        }

        void OnApplicationQuit()
        {
            StopServer();
        }

        void OnDestroy()
        {
            if (s_instance != this) return;
            StopServer();
            s_instance = null;
        }
    }
}
