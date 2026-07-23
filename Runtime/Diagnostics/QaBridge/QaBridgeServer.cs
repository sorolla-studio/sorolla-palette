using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
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
    ///     Trusted-studio support surface: compiled into ALL builds and auto-started in Editor,
    ///     development, and release builds. Both endpoints are passwordless. The loopback bind blocks direct
    ///     LAN access, but device loopback is device-global: another app/process on the device can connect; a
    ///     host computer needs USB forwarding. Treat snapshot output as potentially readable and do not widen
    ///     it. A shared PIN for mutating <c>/qa/exec</c> may be added later (see <see cref="HandleRequest"/>);
    ///     none exists today.
    ///
    ///     Threading: HttpListener callbacks run on worker threads. They validate and read the bounded
    ///     transport request, then enqueue a parsed request. Unity/SDK reads and action dispatch happen on
    ///     the main thread in <see cref="Update"/>.
    /// </summary>
    internal sealed class QaBridgeServer : MonoBehaviour
    {
        internal const int Port = 8765;
        // Both loopback prefixes are registered so the agent/studios can curl either host: HttpListener
        // rejects a request whose Host header does not match a prefix ("127.0.0.1" alone returns 400 to
        // `curl localhost:8765`, the documented command). "localhost" resolves to the loopback adapter,
        // so this stays loopback-only - it never binds 0.0.0.0 and never trips the iOS Local Network prompt.
        const string LoopbackPrefix = "http://127.0.0.1:8765/";
        const string LocalhostPrefix = "http://localhost:8765/";
        const int MaxRequestsPerFrame = 4;
        const int MaxPendingRequests = 16;
        const int MaxRequestBodyBytes = 4096;

        static QaBridgeServer s_instance;

        readonly ConcurrentQueue<PendingRequest> _pending = new ConcurrentQueue<PendingRequest>();
        int _pendingCount;
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

        /// <summary>Binds the port and starts accepting. Idempotent recovery path for the diagnostics console.</summary>
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

            StartServer();
        }

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
                _listener.Prefixes.Add(LoopbackPrefix);
                _listener.Prefixes.Add(LocalhostPrefix);
                _listener.Start();
                _running = true;
                _listener.BeginGetContext(OnContext, null);
                PaletteLog.Vital($"[Palette:QaBridge] Listening on {LoopbackPrefix} (and localhost)");
            }
            catch (Exception e)
            {
                // Bind failure (port in use, platform restriction) must never crash the game over QA
                // plumbing. Log one line and let the console retry/restart path recover if possible.
                _running = false;
                SafeCloseListener();
                PaletteLog.Vital($"[Palette:QaBridge] Could not bind {LoopbackPrefix}: {e.Message}");
            }
        }

        void StopServer()
        {
            if (!_running && _listener == null) return;

            _running = false;
            SafeCloseListener();

            while (_pending.TryDequeue(out PendingRequest request))
            {
                Interlocked.Decrement(ref _pendingCount);
                TryAbort(request.Context);
            }

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

            if (Interlocked.Increment(ref _pendingCount) > MaxPendingRequests)
            {
                Interlocked.Decrement(ref _pendingCount);
                WriteJson(ctx, 503, "{\"ok\":false,\"detail\":\"bridge_busy\"}");
                return;
            }

            PendingRequest request = PrepareRequest(ctx);
            if (request == null)
            {
                Interlocked.Decrement(ref _pendingCount);
                return;
            }
            if (!_running)
            {
                Interlocked.Decrement(ref _pendingCount);
                TryAbort(ctx);
                return;
            }
            _pending.Enqueue(request);
        }

        void Update()
        {
            if (!_running) return;

            // Drive the diagnostics identifier-refresh poll from the bridge's own runner so the
            // snapshot stays current even when the on-screen console is closed.
            SorollaDiagnostics.UpdatePolling();

            for (int i = 0; i < MaxRequestsPerFrame && _pending.TryDequeue(out PendingRequest request); i++)
            {
                Interlocked.Decrement(ref _pendingCount);
                HandleRequest(request);
            }
        }

        // Main thread: safe to read Unity/SDK state and write the response.
        void HandleRequest(PendingRequest request)
        {
            HttpListenerContext ctx = request.Context;
            try
            {
                string path = request.Path;
                string method = request.Method;

                if (!IsQaPath(path))
                {
                    WriteJson(ctx, 404, "{\"ok\":false,\"detail\":\"unknown_endpoint\"}");
                    return;
                }

                // No auth gate by design: reads are reachable from another
                // app/process on the device, or from a host after USB forwarding. Do not re-add a per-request
                // password here.
                if (path == "/qa/snapshot" && method == "GET")
                {
                    WriteJson(ctx, 200, QaSnapshot.Build());
                    return;
                }

                // Mutating surface. Reads above are deliberately open; if the accepted mutation risk of an
                // open /qa/exec ever needs closing, a single shared PIN check goes HERE (one rule, not a
                // per-game secret). No such PIN exists today - exec is open like the reads.
                if (path == "/qa/exec" && method == "POST")
                {
                    HandleExec(ctx, request.Body);
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

        static bool IsQaPath(string path) =>
            path == "/qa" || path.StartsWith("/qa/", StringComparison.Ordinal);

        // Fire-and-ack: parse the action, dispatch it on the main thread, reply immediately. The snapshot
        // is the single source of truth for the outcome (no blocking, no completion sources, no timeout).
        void HandleExec(HttpListenerContext ctx, string body)
        {
            string action;
            try
            {
                action = string.IsNullOrEmpty(body) ? null : JsonUtility.FromJson<ExecRequest>(body)?.action;
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"[Palette:QaBridge] exec body parse failed: {e.Message}");
                WriteJson(ctx, 400, "{\"ok\":false,\"detail\":\"bad_request\"}");
                return;
            }

            // Receipt for every exec, accepted or refused. Without it a bridge-triggered ad or consent reset
            // is only attributable from the NEXT snapshot's test_* counters, which makes an agent action and a
            // human action look identical in the console stream while both are in flight.
            bool ok = QaActionRegistry.TryInvoke(action, null, out string detail);
            PaletteLog.Vital($"[Palette:QaBridge] exec '{action ?? "(none)"}' -> {(ok ? "accepted" : $"refused ({detail})")}.");

            if (ok)
            {
                WriteJson(ctx, 200, "{\"ok\":true}");
                return;
            }

            WriteJson(ctx, 400, BuildError(detail));
        }

        static PendingRequest PrepareRequest(HttpListenerContext ctx)
        {
            HttpListenerRequest request = ctx.Request;
            string path = request.Url?.AbsolutePath ?? "";
            string method = request.HttpMethod;

            if (!IsQaPath(path))
            {
                WriteJson(ctx, 404, "{\"ok\":false,\"detail\":\"unknown_endpoint\"}");
                return null;
            }

            if (path != "/qa/exec" || method != "POST")
                return new PendingRequest(ctx, path, method, null);

            if (TryRejectBody(ctx))
                return null;

            if (!request.HasEntityBody || request.ContentLength64 == 0)
                return new PendingRequest(ctx, path, method, string.Empty);

            try
            {
                byte[] buffer = new byte[(int)request.ContentLength64];
                int read = 0;
                while (read < buffer.Length)
                {
                    int count = request.InputStream.Read(buffer, read, buffer.Length - read);
                    if (count == 0)
                        break;
                    read += count;
                }
                if (read != buffer.Length)
                {
                    WriteJson(ctx, 400, "{\"ok\":false,\"detail\":\"incomplete_request_body\"}");
                    return null;
                }
                return new PendingRequest(ctx, path, method,
                    (request.ContentEncoding ?? Encoding.UTF8).GetString(buffer));
            }
            catch
            {
                WriteJson(ctx, 400, "{\"ok\":false,\"detail\":\"bad_request\"}");
                return null;
            }
        }

        // The loopback bind already gates the bridge (passwordless by design); this only bounds what the exec parser will read.
        // An undeclared length (chunked transfer-encoding) is refused with 411 so it is not conflated with
        // an oversized body; a declared length over the cap is refused with 413. Returns true (and has
        // already written the response) when the request was rejected.
        static bool TryRejectBody(HttpListenerContext ctx)
        {
            HttpListenerRequest request = ctx.Request;
            if (request == null || !request.HasEntityBody) return false;

            if (request.ContentLength64 < 0)
            {
                WriteJson(ctx, 411, "{\"ok\":false,\"detail\":\"length_required\"}");
                return true;
            }

            if (request.ContentLength64 > MaxRequestBodyBytes)
            {
                WriteJson(ctx, 413, "{\"ok\":false,\"detail\":\"request_body_too_large\"}");
                return true;
            }

            return false;
        }

        static string BuildError(string detail)
        {
            var sb = new StringBuilder(48);
            sb.Append("{\"ok\":false,\"detail\":");
            QaJson.AppendString(sb, detail);
            sb.Append('}');
            return sb.ToString();
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

        // POST /qa/exec body shape. args is intentionally not deserialized: the generic
        // actions are parameterless, and JsonUtility ignores the unparsed "args" object.
        [Serializable]
        sealed class ExecRequest
        {
            public string action;
        }

        sealed class PendingRequest
        {
            internal readonly HttpListenerContext Context;
            internal readonly string Path;
            internal readonly string Method;
            internal readonly string Body;

            internal PendingRequest(HttpListenerContext context, string path, string method, string body)
            {
                Context = context;
                Path = path;
                Method = method;
                Body = body;
            }
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
