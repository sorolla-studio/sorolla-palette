# Firebase Dashboard

What must be true in the Firebase console, and why the SDK cannot verify all of it from the client.

## What to configure

- A Firebase project exists with **Google Analytics enabled**.
- Android app registered with the exact package name; iOS app registered with the exact bundle ID.
- `google-services.json` (Android) and `GoogleService-Info.plist` (iOS) downloaded and placed under Unity `Assets/` with exact filenames (no `(2)` suffix from a re-download).

Full procedure: [Firebase docs](https://firebase.google.com/docs/unity/setup). Sorolla's internal dashboard-creation runbook: [dashboard-setup.md](../dashboard-setup.md).

## What the SDK can and cannot verify

Firebase either initializes or throws at startup — a missing/mismatched config file is a loud, catchable failure, visible as the `firebase` adapter state in a device snapshot (`/qa/snapshot` → `adapters.firebase`) not reaching initialized. Getting past init, however, does not prove the Firebase **project** is correctly configured dashboard-side (GA4 property linked, Crashlytics enabled, Remote Config parameters published) — those are console-side settings with no equivalent client-readable API the SDK calls.

**Purchase revenue is not receipt-verified.** The SDK emits the Firebase GA4 `purchase` event from client-side Unity IAP telemetry, not from a server-side receipt check. Every purchase event carries a `store_environment` custom parameter (`production`, `sandbox`, `xcode`, or `unknown`) — register it in Firebase for purchase-event filtering. Treat `unknown` as unverified, not as production: it covers Android, legacy purchase tracking, and undecodable iOS JWS values. For iOS production-only revenue views, filter `store_environment == "production"`; TestFlight builds report `sandbox`. Cross-platform canonical revenue still needs Adjust's server-side verification (see [adjust.md](adjust.md)), not this event.

## What the verdict shows when it's wrong

| Row | State | Meaning |
|---|---|---|
| Device Snapshot: (adapters.firebase, via bridge) | not `initialized` | Config file missing, wrong package name/bundle ID match, or malformed `google-services.json`/`GoogleService-Info.plist`. Re-download from the Firebase console and confirm the exact filename. |
| Launch Readiness (config-file presence checks) | **Fail/Warn** | Static check that the config file exists and is well-formed before ever reaching a device. |

Remote Config value staleness (`newValuesActivated:False` alongside `Success`) is expected, benign behavior on a cold fetch — do not read it as a failure by itself; check `remote_config.fetch_success` in the snapshot instead.

## Deep link

[console.firebase.google.com](https://console.firebase.google.com/)
