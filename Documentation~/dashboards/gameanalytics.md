# GameAnalytics Dashboard

What must be true in the GameAnalytics dashboard, and why the SDK cannot verify all of it from the client.

## What to configure

- The game exists in your GameAnalytics organization, with a **Game Key** and **Secret Key** per platform.
- **The active platform (Android and/or iOS) is added under the game's Settings.**
- Keys are entered into `Tools > Sorolla Palette SDK` (or `Window > GameAnalytics > Select Settings` for the underlying GA SDK settings).

Full setup procedure: [gameanalytics.com docs](https://gameanalytics.com/docs/). Sorolla's internal dashboard-creation runbook: [dashboard-setup.md](../dashboard-setup.md).

## What the SDK can and cannot verify

The **GameAnalytics Credentials probe** in Launch Readiness HMAC-signs a real `init` call against the GA Collection API using the configured game key + secret key. This proves the key pair is **live and matched** — a `401` means the keys are wrong or mismatched, and events will silently fail to submit.

**It cannot prove the active platform is registered in the GA dashboard.** The GA collector's `init` and `events` endpoints accept any syntactically valid platform string as long as the credentials are valid — a schema-valid event for a platform that was never added to the dashboard still returns `200`. This was confirmed live against the collector (2026-07), not inferred: a complete, correctly-signed request for a never-registered platform succeeds at the HTTP layer and is silently dropped dashboard-side. There is no credential the SDK holds that exposes platform-level dashboard state (that lives behind GA's Organization API, which needs a separate org-admin key the SDK does not carry).

This is a real, previously-seen failure mode: a build reported `ready: true` with the credential probe passing while GA silently dropped 100% of events for one platform because it was never added to the dashboard game settings. The credential probe closes the "wrong keys" case; it structurally cannot close the "right keys, wrong platform" case.

## What the verdict shows when it's wrong

| Row | State | Meaning |
|---|---|---|
| GameAnalytics Credentials | **Fail** (HTTP 401 from collector) | Game key / secret key pair is invalid or mismatched. Re-copy from the GA dashboard. |
| GameAnalytics Credentials | **Pass** | Keys are a live, matched pair. Does **not** mean the platform is registered. |
| _(no window row)_ | The credential probe's passing row says "platform registration not verified" | No probe can close this, and the SDK no longer shows a row that only a person could tick. Open the GA dashboard and confirm the active platform is listed under the game's Settings. Valid keys do **not** prove it: GameAnalytics accepts events for a platform that is not registered, and they land nowhere useful. |

## Deep link

[go.gameanalytics.com/login](https://go.gameanalytics.com/login)
