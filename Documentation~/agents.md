# Agent Pack: Autonomous Integration QA

Operational instructions for a studio's AI agent (Claude Code, Cursor, or similar) to run Sorolla Palette SDK integration QA without a human driving each step. This is a public port of Sorolla's internal QA flow, minus the private per-game roster. It does not replace human judgment on the manual/dashboard rows below — it replaces re-deriving "what to check" from scratch every time.

Read this once per game repo before running QA. It assumes the SDK is already integrated (see [Prototype Mode Quick Start](quick-start.md) / [Full Mode Soft Launch Migration](switching-to-full.md) if not).

---

## 1. Read the Greenlight verdict first

**Editor menu: Tools > Sorolla Palette SDK**, then the **Greenlight** section.

It composes five evidence classes into one mechanical status — read the live status in the window, not this doc's paraphrase of what it checks (the checks evolve; this list can lag). Every row is evaluated as a gate against the mode requirement table (§3 below), and one shared aggregation produces the status. Remember it is an interim self-check, not a final release verdict:

1. **Build Health** — ~24 static checks (mode, sandbox/dev flags, vendor config files present, keystore, manifest, SDK pin). Each check is its own gate: an error surfaces as a failing row, and a check that does not apply to the current mode/platform/installed-modules is excluded rather than forced (e.g. Adjust settings in Prototype, the Android keystore on an iOS build).
2. **Editor probes** — live network calls that verify credentials against the vendor's own API, not just "a config file exists." Currently: Facebook Graph API platform probe, GameAnalytics credential probe. Each probe row states what it proved and what it didn't (see [dashboards/](dashboards/) for the scope of each).
3. **Device Snapshot** — required on any device platform you ship (Android over `adb`, iOS over `iproxy`); the Connect Device button pulls live `/qa/snapshot` from the connected device (§2 below). A build never confirmed on device reads `INCOMPLETE`, not green.
4. **Manual / dashboard checklist** — rows a machine cannot check. Every row carries fix text and a deep link where one exists; none render as a bare unchecked box. A ticked box is not scoped evidence, so it maps to `INCOMPLETE` until scoped attestation exists (§3 below).
5. **Source-level integration review** — not rendered in the editor window. Run the `sorolla-sdk-integration-review` audit (source-only: event wiring, Palette API usage, config alignment) before the on-device pass, not instead of it. Ask your Sorolla contact if you don't have this skill/prompt.

The Greenlight status reads `HEALTHY`, `N ISSUES`, `INCOMPLETE`, or `FAILING` (§3 of `troubleshooting.md` covers the general debug flow; this doc covers the QA-specific surfaces only). Treat this as an interim self-check status, not a release verdict — it is the current safety floor, not the final QA gate. A single `FAILING` row anywhere fails the whole status. `INCOMPLETE` (a non-green badge) means required evidence is still missing, pending, or unverifiable — a probe that has not run, a required gate with no observation, a device snapshot that never connected, or a manual gate lacking scoped attestation — so the report cannot honestly claim `HEALTHY`; it outranks `N ISSUES` but not `FAILING`. A gate that does not apply to this build (wrong mode, wrong platform, absent module) is excluded from the verdict rather than counted as a pass, so it can neither fail nor prop up a green status. Work the `INCOMPLETE` rows down (run the checks, connect a device, gather scoped attestation) before treating the status as final; the collapsed rows list will not claim "rows checked" while the report is incomplete. The on-device Vitals overlay renders four states of its own: `FAILING`, `N ISSUES`, `NOT PROVEN` (every check passes but the build has not been played through a level and an ad watched to the end), and `HEALTHY`. Use **Copy Report (text)** for a readable version or **Copy Report (JSON)** for the machine-readable canonical export to attach to a PR or ticket — both carry every row (including inert ones) plus a build/context fingerprint. Note the copied report embeds the tester name and evidence notes you entered when attesting: do not put secrets, tokens, or personal data in those fields.

## 2. The QA bridge (device-level ground truth)

A loopback-only HTTP server compiled into every build (Editor, development, and release), bound to `127.0.0.1:8765`. It never binds `0.0.0.0` and is not reachable off-device — reach it over USB.

**Android:**
```
adb forward tcp:18765 tcp:8765
curl http://127.0.0.1:18765/qa/snapshot
```

**iOS:** no `adb` equivalent ships with the SDK. Forward the port yourself over usbmux (e.g. `iproxy 18765 8765`) before curling the same loopback URL.

**No password.** Both endpoints require no auth. The loopback-only bind blocks direct LAN access, but device loopback is device-global: another app/process on the device can connect. A host computer needs USB forwarding. Treat anything returned by the bridge as potentially readable and don't send secrets to it — it is a QA convenience surface, not a hardened one (see `known-issues.md` / the SDK's internal audit notes if you have access). A shared PIN for the mutating `/qa/exec` endpoint may be added later; there is none today.

An empty reply means the app has not booted or is not foregrounded yet. Wait ~20-30s after launch and retry before concluding the bridge is down.

### `GET /qa/snapshot`

Returns the full runtime state as one JSON object:

| Field | Contains |
|---|---|
| `sdk`, `mode`, `development_build`, `armed`, `ready`, `device_wall_clock` | SDK version, active mode, build flavor, bridge listening state, init-complete flag, device clock |
| `consent` | `status`, `geography`, `att`, `can_request_ads`, `form_shown_this_session`, `signals` (`analytics_storage`/`ad_storage`/`ad_personalization`/`ad_user_data`, each `"granted"`/`"denied"`/`"unknown"`), `iabtcf` (`tc_string_present`, `purpose_consents`) |
| `remote_config` | `status`, `fetch_seen`, `fetch_success`, `values` (per-key `value` + `source`) |
| `adapters` | Per-vendor init state string for `max`, `adjust`, `firebase`, `gameanalytics`, `facebook`, plus `crashlytics_ready`/`crashlytics_outcome` |
| `identity` | `att`, `advertising_id_present`, `advertising_id_zeroed`, `adjust_adid_present`, `attribution_network`, `adjust_environment`, `fb_att_enabled`, `fb_att_applied` |
| `events` | Array of `{name, count, last_params}` for every event type seen this session |
| `ads` | `interstitial`/`rewarded` (`loaded`, `completed` each), `revenue_seen` |
| `iap` | `tracking_attached`, `purchase_count`, `duplicate_count`, `verification`, `last_issue` |
| `problems` | `sdk_warnings`, `sdk_errors`, `last_sdk_error`, `runtime_unique`, `runtime_total`, `runtime_top` |

A snapshot proves what happened in the current session on this device. It does not prove vendor-side delivery (an event can be `count: 1` locally and never arrive server-side if credentials are wrong — cross-check against the relevant [vendor dashboard page](dashboards/)).

### `POST /qa/exec`

Body: `{"action": "<name>"}`. Fire-and-ack: the response only confirms dispatch (`{"ok":true}` / `HTTP 200`); read the outcome back from the next `/qa/snapshot` (e.g. `events` count incrementing, `ads.rewarded.completed` flipping). This is the same action set the on-device debug console exposes as buttons — one core, two frontends.

Action names: `show_rewarded`, `show_interstitial`, `open_privacy_options`, `reset_consent`, `refresh_consent`, `track_test_event`, `level_start`, `level_complete`, `economy_earn`, `economy_spend`.

Error responses: `400` + `{"detail":"unknown_action"}` (typo'd action name) or `{"detail":"bad_request"}` (malformed body), `404` + `{"detail":"unknown_endpoint"}` (wrong path), `411`/`413` (missing/oversized request body).

Test-generating actions (`track_test_event`, `level_*`, `economy_*`) run inside a tagged test-action scope: they are excluded from integration health counters and filterable out of Firebase, so running them repeatedly during QA does not pollute the game's real analytics.

## 3. What the verdict applies to (the mode requirement table)

Which gates are required, optional, or excluded is derived from the trusted build context — the SDK mode (Prototype / Full, from `SorollaConfig`), the active build platform, the installed modules (read from the package manifest), and the facts the checks observe — together with two studio-declared target sets on `SorollaConfig`: the **distribution platforms** (which stores the game ships its app on) and the **commerce platforms** (which stores it sells in-app purchases on). These target sets are functional SDK config, NOT the deleted per-game QA-expectations asset: they express product intent (where you ship / where you sell), and the SDK derives which gates apply from them — a studio still cannot self-exempt a specific check. Both default to undeclared, which fails the dependent gates closed to `INCOMPLETE` rather than guessing from installed packages. (A connected device snapshot is bound to the game/build by a minimum identity: schema version plus application id, platform, mode, app version, and a Unity build GUID, and the greenlight rejects a wrong-game or wrong-build snapshot. The copied report additionally resolves the exact SDK source commit at export time — the version string `4.0.0` alone cannot identify a development build — and carries the device build GUID when a device is connected; the runtime-baked platform build number comes later.)

What that means in practice:

- **Mode drives applicability.** GameAnalytics and Facebook are required in both modes. Firebase and AppLovin MAX are required in Full and optional in Prototype (installed if you want them, never force-flagged if absent — matching the installer). Adjust is Full-only. A gate that does not apply to the current mode is excluded, not failed.
- **Distribution platforms drive device applicability.** The on-device snapshot gates apply on the platforms the game declares it ships on (distribution platforms). On a declared platform that has no on-device collector yet (iOS today), the required device gates are `INCOMPLETE` — a capability gap, so an iOS-only game cannot read `HEALTHY` without ever running on device — NOT excluded. On a platform the game does not ship on, they are excluded. The Android keystore is an Android release-ship check.
- **Commerce platforms drive the store gate.** `iap.store_configured` applies only where Unity IAP is installed AND the active platform is a declared commerce target; a game that ships its app on Android but sells IAP only on iOS has a NotApplicable Android store gate. Purchase-tracking wiring (`iap.tracking_attached`) is a separate device-observed gate — a store attestation cannot satisfy it.
- **Manual gates need scoped attestation.** The dashboard/on-device rows (§1.4) require vendor-side or on-device proof. A legacy editor check-off carries no build/device/vendor scope, so it maps to `INCOMPLETE` — re-attest with scoped evidence rather than ticking a box. This is deliberate: the verdict never grandfathers an unscoped tick into a pass.
- **Prototype is a first-class release path**, never a "pre-release" state. Being in Prototype is never itself a failure — Prototype ships for Facebook UA tests and publisher review builds.

## 4. The Vitals overlay (on-device, no bridge needed)

Every build ships an on-screen diagnostics overlay reachable without USB or a password: **tap the top-right corner of the screen 5 times within 2 seconds, then hold the final tap for 0.8 seconds.** It opens on one report screen: the hero verdict, a `FIX THESE` list whose rows expand to WHY/SIGNAL/FIX, and a `TEST YOUR GAME` coverage list. There are no area cards and no tabs; the only actions a studio sees are the inline buttons on the coverage rows (show an ad, reset consent) plus `Copy report` at the bottom. The wider action set the bridge's `/qa/exec` drives lives behind the internal view, which is not part of the studio surface.

Use it when you don't have adb/usbmux set up, or as the human-facing companion to a bridge-driven agent run — anything the bridge reports, Vitals explains in the same terms, in person, on the device.

## 5. Escalation boundary

Not every red row is something an agent (or a studio engineer) can fix. Classify before acting:

- **Studio-fixable, with the fix in the row itself** — Build Health errors/warnings, most probe failures (wrong app ID, mismatched client token). The row's `Fix` text names the concrete change (usually: edit `SorollaConfig` or `FacebookSettings`).
- **Dashboard work, with a doc link** — every row in the manual checklist (§1.4) and the deeper procedures in [dashboards/](dashboards/). These are things the SDK cannot verify by design (see each vendor page for why) — go configure the vendor's dashboard, then re-run the verdict.
- **Contact Sorolla, with the copied SDK state** — cross-vendor drift that spans two dashboards Sorolla owns (see `dashboards/applovin-max.md`), anything that looks like a credential the studio doesn't hold, or a `FAILING` verdict that persists after working through every row above. Use **Copy Report (text)** or **Copy Report (JSON)** and attach the raw export — do not paraphrase the verdict when escalating, the exact row text is the evidence.

See also: [dashboards/](dashboards/) for the vendor-specific detail behind each manual row, [troubleshooting.md](troubleshooting.md) for build/runtime issues outside the QA surfaces, [known-issues.md](known-issues.md) for previously-seen field incidents.
