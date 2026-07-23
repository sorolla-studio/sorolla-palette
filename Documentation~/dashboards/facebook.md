# Facebook Dashboard

What must be true in the Meta App Dashboard, and why the SDK can verify most, but not all, of it from the client.

## What to configure

- A Meta app exists (type: Business), with the exact Android package name / iOS bundle ID registered as a platform.
- **App ID** and **Client Token** are entered into Unity (`Facebook > Edit Settings`) and, for Android, mirrored into `AndroidManifest.xml` `<meta-data>` entries.
- Android also needs the correct key hash and class name registered against the platform entry; iOS needs the exact bundle ID.

Do not create a second Meta app if the studio already has one for this package name/bundle ID — Facebook deduplicates by identifier and a second app causes event routing conflicts. Full procedure: [developers.facebook.com](https://developers.facebook.com/docs/). Sorolla's internal dashboard-creation runbook: [dashboard-setup.md](../dashboard-setup.md).

## What the SDK can and cannot verify

The **Facebook Platform probe** in Launch Readiness makes a real Graph API call — `GET /{app-id}?fields=supported_platforms` — using the configured app ID and client token. Unlike the GameAnalytics collector, this call is genuinely platform-scoped: the Graph API's `supported_platforms` field reflects what is actually registered against the app object in the dashboard, so this probe **does verify platform registration**, not just credential validity.

Because of that, this page's focus is app creation and client-token correctness rather than "how do I check platform registration" (the probe already does).

### Reading the probe result

| Probe state | Meaning | Fix |
|---|---|---|
| `Verified` | App exists, credentials are valid, active platform is registered. | Nothing to do. |
| `PlatformMissing` | App and credentials are valid, but the active build target (Android/iOS) has no platform entry on this Meta app. | Meta App Dashboard → Settings → Basic → add the platform, enter package name/bundle ID, key hash (Android) or nothing extra (iOS). |
| `CredentialInvalid` | The Graph API rejected the request under error code 190 (`OAuthException`). | See below — three distinct causes collapse under this one code. |
| `Unreachable` | Network/offline, or the Graph API endpoint is blocked from this machine. | Re-run when online; not a credential problem. |

### The three causes behind error code 190

The Graph API returns the same `errorCode: 190` for three different underlying problems. The probe reads the error message text to tell them apart and reports the specific cause:

1. **App deleted** — the App ID no longer resolves to a live Meta app. Someone deleted it from the developer console (e.g. during an account cleanup). The app must be recreated; there is no undelete.
2. **Client token mismatch** — the App ID is real, but the client token in `FacebookSettings.asset` belongs to a different app (usually copy-paste from the wrong game, or a token rotated dashboard-side without updating Unity). Re-copy the client token from Settings → Advanced.
3. **Invalid App ID** — the App ID string itself doesn't match any Meta app (typo, or copied from the wrong field). Re-copy from Settings → Basic.

Whichever cause, the practical effect is the same: Facebook init reports `AuthError`, and analytics + attribution silently stop reaching Facebook — no crash, no loud error at runtime.

## What the verdict shows when it's wrong

| Row | State | Meaning |
|---|---|---|
| Facebook Platform (Graph API) | **Fail** | One of `PlatformMissing` / `CredentialInvalid` above — the row's `Fix` text names the specific cause. |
| Facebook Platform (Graph API) | **Wait** | `Unreachable` — probe couldn't complete this run, re-run when online. Never rendered as a pass. |

## Deep link

[developers.facebook.com/apps](https://developers.facebook.com/apps/)
