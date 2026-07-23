# Agent Pack: Autonomous Integration QA

Operational instructions for a studio's AI agent (Claude Code, Cursor, or similar) to run Sorolla Palette SDK integration QA without a human driving each step. This is a public port of Sorolla's internal QA flow, minus the private per-game roster. It does not replace human judgment on the vendor-dashboard work — it replaces re-deriving "what to check" from scratch every time.

Read this once per game repo before running QA. It assumes the SDK is already integrated (see [Prototype Mode Quick Start](quick-start.md) / [Full Mode Soft Launch Migration](switching-to-full.md) if not).

---

## 1. Read the Launch Readiness verdict first

**Editor menu: Tools > Sorolla Palette SDK**, then the **Launch Readiness** section. It states the platform it judged ("Judging the iOS build target."); that is the active build target, and it is the only platform the verdict covers.

It composes four evidence classes into one mechanical status — read the live status in the window, not this doc's paraphrase of what it checks (the checks evolve; this list can lag). Every row is evaluated as a gate against the mode requirement table (§3 below), and one shared aggregation produces the status. Every gate is something the SDK observes for itself, from your project files or from a live device: there is nothing to tick off by hand. Remember it is an interim self-check, not a final release verdict:

1. **Static project checks** — one canonical catalog of 25 gates (mode, sandbox/dev flags, vendor config files, keystore, manifest, SDK pin), rendered inside the vendor group each belongs to plus a Build & Project group. Each check is its own gate: an error surfaces as a failing row, and a check that does not apply to the current mode/platform/installed-modules is excluded rather than forced (e.g. Adjust settings in Prototype, the Android keystore on an iOS build).
2. **Editor probes** — live network calls that verify credentials against the vendor's own API, not just "a config file exists." Currently: Facebook Graph API platform probe, GameAnalytics credential probe. Each probe row states what it proved and what it didn't (see [dashboards/](dashboards/) for the scope of each).
3. **Device Vitals** — required on any device platform you ship (Android over `adb`, iOS over `iproxy`); the Connect Device button pulls live `/qa/snapshot` from the connected device (§2 below). The editor consumes the same Vitals verdict shown on device, not a reduced SDK-error proxy. A build never confirmed on device reads `INCOMPLETE`, not green.
4. **Source-level integration review** — not rendered in the editor window. Run the `sorolla-sdk-integration-review` audit (source-only: event wiring, Palette API usage, config alignment) before the on-device pass, not instead of it. Ask your Sorolla contact if you don't have this skill/prompt.

The Launch Readiness status reads `HEALTHY`, `N ISSUES`, `INCOMPLETE`, or `FAILING` (§3 of `troubleshooting.md` covers the general debug flow; this doc covers the QA-specific surfaces only). Treat this as an interim self-check status, not a release verdict — it is the current safety floor, not the final QA gate. A single `FAILING` row anywhere fails the whole status. `INCOMPLETE` (a non-green badge) means required evidence is still missing, pending, or unverifiable — a probe that has not run, a required gate with no observation, or a device snapshot that never connected — so the report cannot honestly claim `HEALTHY`; it outranks `N ISSUES` but not `FAILING`. Every `INCOMPLETE` names a specific thing you can go do. A gate that does not apply to this build (wrong mode, wrong platform, absent module) is excluded from the verdict rather than counted as a pass, so it can neither fail nor prop up a green status. Work the `INCOMPLETE` rows down (run the checks, connect a device) before treating the status as final; the collapsed rows list will not claim "rows checked" while the report is incomplete. The on-device Vitals overlay renders four states of its own: `FAILING`, `N ISSUES`, `NOT PROVEN` (every check passes but the build has not been played through a level and an ad watched to the end), and `HEALTHY`. Use **Copy Report** for a readable, auditable export to attach to a PR or ticket — it carries every row (including passing/inert ones) plus a build/context fingerprint evaluated against the SDK commit recorded in the matching post-build receipt, even though the window itself only renders the rows still needing attention.

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
| `snapshot_schema`, `verdict` | Snapshot contract version and the authoritative Vitals verdict (`failing`, `action_needed`, `not_proven`, `pass`) |
| `sdk`, `mode`, `development_build`, `armed`, `ready`, `device_wall_clock` | SDK version, active mode, build flavor, bridge listening state, init-complete flag, device clock |
| `build` | Application id, platform, app version, and Unity build GUID used to match the installed binary to the editor's post-build receipt |
| `consent` | `status`, `geography`, `att` (iOS only; `null` on every other platform), `can_request_ads`, `form_shown_this_session`, `signals` (`analytics_storage`/`ad_storage`/`ad_personalization`/`ad_user_data`, each `"granted"`/`"denied"`/`"unknown"`), `iabtcf` (`tc_string_present`, `purpose_consents`) |
| `remote_config` | `status`, `fetch_seen`, `fetch_success`, `values` (per-key `value` + `source`) |
| `adapters` | Per-vendor init state string for `max`, `adjust`, `firebase`, `gameanalytics`, `facebook`, plus `crashlytics_ready`/`crashlytics_outcome` |
| `identity` | `att` (iOS only; `null` elsewhere), `advertising_id_present`, `advertising_id_zeroed`, `adjust_adid_present`, `attribution_network`, `adjust_environment`, `fb_att_enabled`, `fb_att_applied` |
| `events` | Array of `{name, count, last_params}` for every event type seen this session |
| `ads` | `interstitial`/`rewarded` (`loaded`, `completed` each), `revenue_seen` |
| `iap` | `tracking_attached`, `purchase_count`, `duplicate_count`, `verification`, `last_issue` |
| `problems` | SDK-owned diagnostics only: `sdk_warnings`, `sdk_errors`, `last_sdk_error` |

A snapshot proves what happened in the current session on this device. It does not prove vendor-side delivery (an event can be `count: 1` locally and never arrive server-side if credentials are wrong — cross-check against the relevant [vendor dashboard page](dashboards/)).

### `POST /qa/exec`

Body: `{"action": "<name>"}`. Fire-and-ack: the response only confirms dispatch (`{"ok":true}` / `HTTP 200`); read the outcome back from the next `/qa/snapshot` (e.g. `events` count incrementing, `ads.rewarded.completed` flipping). This is the same action set the on-device debug console exposes as buttons — one core, two frontends.

Action names: `show_rewarded`, `show_interstitial`, `open_privacy_options`, `reset_consent`, `refresh_consent`, `track_test_event`, `level_start`, `level_complete`, `economy_earn`, `economy_spend`.

Error responses: `400` + `{"detail":"unknown_action"}` (typo'd action name) or `{"detail":"bad_request"}` (malformed body), `404` + `{"detail":"unknown_endpoint"}` (wrong path), `411`/`413` (missing/oversized request body), or `503` when the bounded command queue is busy.

Test-generating actions (`track_test_event`, `level_*`, `economy_*`) run inside a tagged test-action scope: they are excluded from integration health counters and filterable out of Firebase, so running them repeatedly during QA does not pollute the game's real analytics.

## 3. What the verdict applies to (the mode requirement table)

Which gates are required, optional, or excluded is derived entirely from the trusted build context — the SDK mode (Prototype / Full, from `SorollaConfig`), the active build target, the installed modules (read from the package manifest), and the facts the checks observe. There is nothing to declare: the platform you are building for is the platform the report judges, so a studio can neither forget to configure this nor self-exempt a specific check. A successful Unity build writes an editor-owned receipt containing its build GUID and SDK source commit. A connected snapshot is accepted only when its schema, application id, platform, mode, app version, and build GUID match that receipt. The copied report then identifies the SDK source recorded for that binary, rather than combining an old device build with the current checkout.

What that means in practice:

- **Mode drives applicability.** GameAnalytics and Facebook are required in both modes. Firebase and AppLovin MAX are required in Full and optional in Prototype (installed if you want them, never force-flagged if absent — matching the installer). Adjust is Full-only. A gate that does not apply to the current mode is excluded, not failed.
- **The active build target drives platform applicability.** The report judges one platform: the one Unity is set to build. Checks about the other platform are excluded — out of the verdict, the counts and the rendered rows — and reappear at full severity when you switch the build target. So a game shipping a single platform reaches `HEALTHY` on that platform, and the copied report still lists the other platform's gates as `NotApplicable` with the reason. The device snapshot gates are required on either mobile target (Android over `adb`, iOS over `iproxy`) and not applicable off mobile; the Android keystore is an Android release-ship check. The window prints the platform it judged, e.g. "Judging the iOS build target."
- **Every gate is machine-observed.** A gate passes on a static project fact or a live device snapshot, never on someone asserting it. Store-console setup, vendor dashboard registration, and consent-persistence-across-relaunch are still real requirements — they are verified in Sorolla's release QA against an actual device run, and are not rows in this window.
- **Prototype is a first-class release path**, never a "pre-release" state. Being in Prototype is never itself a failure — Prototype ships for Facebook UA tests and publisher review builds.

## 4. The Vitals overlay (on-device, no bridge needed)

Every build ships an on-screen diagnostics overlay reachable without USB or a password: **tap the top-right corner of the screen 5 times within 2 seconds, then hold the final tap for 0.8 seconds.** It opens on one report screen: the hero verdict, a `FIX THESE` list whose rows expand to WHY/SIGNAL/FIX, and a `TEST YOUR GAME` coverage list. There are no area cards and no tabs; the only actions a studio sees are the inline buttons on the coverage rows (show an ad, reset consent) plus `Copy report` at the bottom. The wider action set the bridge's `/qa/exec` drives lives behind the internal view, which is not part of the studio surface.

Use it when you don't have adb/usbmux set up, or as the human-facing companion to a bridge-driven agent run — anything the bridge reports, Vitals explains in the same terms, in person, on the device.

## 5. Escalation boundary

Not every red row is something an agent (or a studio engineer) can fix. Classify before acting:

- **Studio-fixable, with the fix in the row itself** — static check errors/warnings, most probe failures (wrong app ID, mismatched client token). The row's `Fix` text names the concrete change (usually: edit `SorollaConfig` or `FacebookSettings`).
- **Dashboard work, with a doc link** — the procedures in [dashboards/](dashboards/). These are things the SDK cannot verify by design (see each vendor page for why) — go configure the vendor's dashboard, then re-run the verdict.
- **Contact Sorolla, with the copied SDK state** — cross-vendor drift that spans two dashboards Sorolla owns (see `dashboards/applovin-max.md`), anything that looks like a credential the studio doesn't hold, or a `FAILING` verdict that persists after working through every row above. Use **Copy Report** and attach the raw export — do not paraphrase the verdict when escalating, the exact row text is the evidence.

See also: [dashboards/](dashboards/) for the vendor-specific dashboard procedures, [troubleshooting.md](troubleshooting.md) for build/runtime issues outside the QA surfaces, [known-issues.md](known-issues.md) for previously-seen field incidents.
