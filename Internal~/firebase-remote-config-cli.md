# Firebase Remote Config — CLI + Palette Integration Guide

Internal reference. Hidden from Unity (`~`-suffix folder), tracked in git.

Two halves:
1. **CLI side** — author/publish a template from your machine or CI.
2. **Runtime side** — how Palette consumes that template inside the Unity game.

---

## Part 1 — CLI Setup & Publishing

### Prerequisites

- `firebase-tools` installed: `npm install -g firebase-tools`
- Authenticated: `firebase login` (re-auth: `firebase login --reauth`)
- Verify project access: `firebase projects:list`
- Caller needs **Firebase Remote Config Admin** role on the project (CLI errors are opaque without it).

### Setup (once per project)

Three files at the project root. Commit all three to git — they're the tuning source-of-truth.

**`.firebaserc`** — pin the default project so `-P` is no longer needed on every command:

```json
{
  "projects": {
    "default": "<project-id>",
    "staging": "<project-id-staging>",
    "prod":    "<project-id-prod>"
  }
}
```

Switch active alias with `firebase use staging` / `firebase use prod`. After this, `firebase deploy --only remoteconfig` works flag-free against the active alias. `-P <project-id>` is then only for one-off overrides.

**`firebase.json`** — tells `firebase deploy` where to find the template:

```json
{
  "remoteconfig": {
    "template": "remote-config.json"
  }
}
```

**`remote-config.json`** — the actual template (filename must match `firebase.json`). Bootstrap it from live (see below) or start with `{ "parameters": {} }`.

### Pull live template

```bash
firebase remoteconfig:get -o remote-config.json
```

If output is `{}` or has no `parameters` key, the project has **no template yet** — that's normal for a fresh Firebase project. Skip the pull, write your `remote-config.json` from scratch, deploy directly. You haven't done anything wrong.

### Strip the `version` block before re-publishing

Server may reject a re-imported template that still has its `version` block:

```bash
jq 'del(.version)' remote-config.json > tmp && mv tmp remote-config.json
```

### Publish

```bash
firebase deploy --only remoteconfig
```

Success:

```
=== Deploying to '<project-id>'...
i  deploying remoteconfig
✔  Deploy complete!
```

### Verify

```bash
firebase remoteconfig:get
firebase remoteconfig:versions:list --limit 5
```

Confirm `version.versionNumber` incremented + `updateTime` matches.

### Per-publish loop (after setup is done)

```bash
firebase remoteconfig:get -o remote-config.json   # 1. pull live
# 2. edit remote-config.json
jq 'del(.version)' remote-config.json > tmp && mv tmp remote-config.json   # 3. strip version
firebase deploy --only remoteconfig               # 4. deploy
firebase remoteconfig:get                         # 5. verify
```

### Diff local against live

```bash
firebase remoteconfig:get -o /tmp/live.json
diff <(jq -S . remote-config.json) <(jq -S . /tmp/live.json)
```

### Rollback

```bash
firebase remoteconfig:versions:list --limit 10
firebase remoteconfig:rollback -v <previous_version_number>
```

Firebase retains the **last 300 versions**. Anything older is gone — snapshot critical templates into git history.

### Non-interactive deploy (CI / fastlane)

`firebase login` won't work in CI. Two options:

**Option A — service account** (preferred for GCP-native CI):

```bash
export GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account.json
firebase deploy --only remoteconfig --project <project-id>
```

Service account needs **Firebase Remote Config Admin** + **Firebase Admin SDK Administrator Service Agent**.

**Option B — CI token** (legacy, still works):

```bash
firebase login:ci   # one-time on a workstation, prints a token
# In CI:
firebase deploy --only remoteconfig --project <project-id> --token "$FIREBASE_TOKEN"
```

Set `FIREBASE_TOKEN` as a secret in fastlane / GitHub Actions. Tokens are long-lived and revocable via `firebase logout --token <token>`.

---

## Part 2 — Palette Runtime Integration

This is the half that makes the CLI deploys actually reach game code. The Sorolla SDK wraps Firebase Remote Config behind `Palette.*` so games never touch `FirebaseRemoteConfig.DefaultInstance` directly.

### Public API surface (`Sorolla.Palette.Palette`)

| Method / Property | Purpose |
|---|---|
| `Palette.SetRemoteConfigDefaults(Dictionary<string, object>)` | Register **in-app defaults**. Call once at boot before any `Get*`. |
| `Palette.IsRemoteConfigReady()` | True once Firebase has loaded cached values (or GA fallback ready). |
| `Palette.FetchRemoteConfig(Action<bool> onComplete = null)` | Force a fetch + activate. Manual override of auto-fetch. |
| `Palette.GetRemoteConfig(key, defaultValue = "")` | String. Falls back to GameAnalytics when Firebase isn't installed. |
| `Palette.GetRemoteConfigInt(key, defaultValue = 0)` | Int. |
| `Palette.GetRemoteConfigFloat(key, defaultValue = 0f)` | Float. |
| `Palette.GetRemoteConfigBool(key, defaultValue = false)` | Bool. |
| `Palette.AutoActivateRemoteConfigUpdates` (bool) | When true (default), real-time pushed values activate immediately. |
| `Palette.ActivateRemoteConfigAsync()` | Manual activate when auto-activate is off. |
| `Palette.OnRemoteConfigUpdated` (event) | Fires when a real-time update arrives. |

### In-app defaults — the part everyone forgets

Every parameter you publish in the dashboard **must** have a matching in-app default registered before the first `Get*` call. Otherwise `Get*` returns the C# default until Firebase finishes its first fetch — and on a fresh install, that gap is the entire first session.

```csharp
using Sorolla.Palette;
using System.Collections.Generic;

void Awake()
{
    Palette.SetRemoteConfigDefaults(new Dictionary<string, object>
    {
        { "ads_interstitial_cooldown_sec", 45 },
        { "economy_starting_coins",        100 },
        { "core_tutorial_enabled",         true },
        { "core_shop_offer_json",          "{\"price\":4.99,\"sku\":\"starter\"}" },
    });
}
```

The in-app default keys + types **must** match exactly what the dashboard publishes. Mismatches = silent fallback to defaults forever.

### Reading a value

```csharp
int cooldown = Palette.GetRemoteConfigInt("ads_interstitial_cooldown_sec", 45);
bool tutorial = Palette.GetRemoteConfigBool("core_tutorial_enabled", true);
string offerJson = Palette.GetRemoteConfig("core_shop_offer_json", "{}");
var offer = JsonUtility.FromJson<ShopOffer>(offerJson);
```

### Reacting to live config updates

Palette wires Firebase's `OnConfigUpdateListener` automatically. New values arrive **without** waiting for the fetch interval — typically within seconds of a publish.

```csharp
void OnEnable()  => Palette.OnRemoteConfigUpdated += HandleConfigUpdate;
void OnDisable() => Palette.OnRemoteConfigUpdated -= HandleConfigUpdate;

void HandleConfigUpdate(IReadOnlyCollection<string> changedKeys)
{
    if (changedKeys.Contains("ads_interstitial_cooldown_sec"))
        RefreshAdScheduler();
}
```

### The 12-hour fetch interval — what to actually expect

Firebase Remote Config's default `MinimumFetchInterval` in production is **12 hours**. The Sorolla impl only sets it to 0 inside `#if UNITY_EDITOR` (`FirebaseRemoteConfigAdapterImpl.cs:70-74`). On a real device:

- **Manual `Palette.FetchRemoteConfig()` calls** are throttled to once per 12h. Calling more often returns cached values without hitting the network.
- **Real-time updates via `OnRemoteConfigUpdated`** are *not* throttled — Palette listens to Firebase's push stream and activates new values as soon as they arrive. This is how production devices typically pick up changes.
- **App relaunch** triggers a fresh fetch outside the 12h window on cold start.

If you deploy a value, watch your test device, and see nothing change: it's almost always one of (a) waiting on the listener, (b) the device cached an old value, (c) the in-app default key doesn't match the published key. Force a refresh by killing + relaunching the app.

### Where Firebase falls back to GameAnalytics

If the Firebase package isn't installed (Prototype mode without Firebase), every `Palette.GetRemoteConfig*` call falls through to `GameAnalyticsAdapter.GetRemoteConfigValue`. Same key namespace, different backend. Keep parameter names dashboard-agnostic so a single key works in both modes.

---

## Template JSON Schema (reference)

```json
{
  "parameters": {
    "<param_name>": {
      "defaultValue": { "value": "<string>" },
      "description": "<string>",
      "valueType": "STRING|NUMBER|BOOLEAN|JSON",
      "conditionalValues": {
        "<condition_name>": { "value": "<string>" }
      }
    }
  },
  "parameterGroups": {
    "<group_name>": {
      "description": "<string>",
      "parameters": {
        "<param_name>": { }
      }
    }
  },
  "conditions": [
    {
      "name": "<condition_name>",
      "expression": "app.id == '1:xxx:android:yyy' && device.country in ['us', 'ca']",
      "tagColor": "BLUE"
    }
  ],
  "version": { }
}
```

Strip `"version"` block before publishing a re-imported template — server may reject it.

### `valueType` rules

| Type | `value` field format | Example |
|---|---|---|
| `NUMBER` | string-quoted number | `"value": "45"` |
| `BOOLEAN` | string-quoted bool | `"value": "true"` |
| `STRING` | plain string | `"value": "hello"` |
| `JSON` | string-escaped JSON | `"value": "{\"price\":4.99,\"sku\":\"starter\"}"` |

All values stored as strings on the wire — `valueType` tells the SDK how to deserialize. JSON is the painful one: every quote must be escaped, and your IDE won't help.

### `defaultValue` special form

```json
"defaultValue": { "useInAppDefault": true }
```

Tells the SDK to use the in-app default registered via `Palette.SetRemoteConfigDefaults` rather than the dashboard value. Useful for kill-switches.

### Condition `tagColor` allowed values

`BLUE`, `BROWN`, `CYAN`, `DEEP_ORANGE`, `GREEN`, `INDIGO`, `LIME`, `ORANGE`, `PINK`, `PURPLE`, `TEAL`

### Server-side limits

- **2000 parameters** max
- **500 conditions** max
- **1 MB** total template size
- **300 versions** retained for rollback

Hitting any of these is opaque and painful — design key namespaces with the cap in mind on big games.

---

## Common Pitfalls

1. **Wrong dir:** `Error: Not in a Firebase app directory` → run from dir w/ `firebase.json`, or `firebase deploy -c /path/to/firebase.json`.
2. **Auth expired:** `Authentication Error: Your credentials are no longer valid` → `firebase login --reauth`.
3. **`version` field present on publish:** server may reject. Strip with `jq 'del(.version)'`.
4. **Numbers as JSON numbers:** `"value": 45` invalid. Always quote: `"value": "45"`.
5. **etag mismatch (REST API only):** CLI handles transparently. Direct REST needs `If-Match: <etag>` from prior GET.
6. **`jq` on `remoteconfig:get`:** default output is colored table, not JSON. Use `-o file.json` for parseable JSON.
7. **In-app default missing or mistyped key:** `Get*` silently returns the C# default forever. No warning, no log. Audit defaults dictionary against the published template.
8. **Device "not seeing" deploys:** usually the 12h fetch cap on `FetchRemoteConfig`, or a stale in-app cache. Relaunch the app, or wait for the real-time listener.
9. **CLI permissions:** opaque errors when caller lacks Firebase Remote Config Admin role. Check IAM before debugging the template.

---

## REST API Fallback

If CLI unavailable:

- Endpoint: `https://firebaseremoteconfig.googleapis.com/v1/projects/<project-number>/remoteConfig`
- Auth: `Authorization: Bearer $(gcloud auth print-access-token)` (needs Firebase Remote Config Admin role)
- GET returns template + `ETag` header
- PUT requires `If-Match: <etag>` header + template body (omit `version`)

---

## Minimal Working Example

```bash
mkdir my-project && cd my-project

cat > .firebaserc <<'EOF'
{ "projects": { "default": "my-firebase-project-id" } }
EOF

cat > firebase.json <<'EOF'
{ "remoteconfig": { "template": "remote-config.json" } }
EOF

cat > remote-config.json <<'EOF'
{
  "parameters": {
    "feature_x_enabled": {
      "defaultValue": { "value": "true" },
      "description": "Toggle for feature X",
      "valueType": "BOOLEAN"
    }
  }
}
EOF

firebase deploy --only remoteconfig
firebase remoteconfig:get
```

Then in Unity:

```csharp
Palette.SetRemoteConfigDefaults(new Dictionary<string, object>
{
    { "feature_x_enabled", true },
});

// Anywhere in game code:
if (Palette.GetRemoteConfigBool("feature_x_enabled", true))
    EnableFeatureX();
```
