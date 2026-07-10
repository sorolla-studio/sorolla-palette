# Agent Pack: Autonomous Integration QA

Operational instructions for a studio's AI agent (Claude Code, Cursor, or similar) to run Sorolla Palette SDK integration QA without a human driving each step. This is a public port of Sorolla's internal QA flow, minus the private per-game roster. It does not replace human judgment on the manual/dashboard rows below — it replaces re-deriving "what to check" from scratch every time.

Read this once per game repo before running QA. It assumes the SDK is already integrated (see [Prototype Mode Quick Start](quick-start.md) / [Full Mode Soft Launch Migration](switching-to-full.md) if not).

---

## 1. Read the Greenlight verdict first

**Editor menu: Palette > Configuration**, then the **Greenlight** section.

It composes six evidence classes into one mechanical verdict — the verdict is the source of truth, not this doc's description of it:

1. **Build Health** — ~24 static checks (mode, sandbox/dev flags, vendor config files present, keystore, manifest, SDK pin). Errors and warnings roll up into one summary row; expand the section for the individual rows.
2. **Editor probes** — live network calls that verify credentials against the vendor's own API, not just "a config file exists." Currently: Facebook Graph API platform probe, GameAnalytics credential probe. Each probe row states what it proved and what it didn't (see [dashboards/](dashboards/) for the scope of each).
3. **Mode Intent** — compares the build's actual SDK mode against the `intendedMode` declared on the QA Expectations asset (§3 below). Only a genuine mismatch fails; being in Prototype mode is never itself a failure — Prototype is a first-class release path for Facebook UA tests and publisher review builds, not a "pre-release" state.
4. **Device Snapshot** — optional; pulls live `/qa/snapshot` from a connected device (§2 below).
5. **Manual / dashboard checklist** — rows a machine cannot check (§4 below). Every row carries fix text and a deep link where one exists; none render as a bare unchecked box.
6. **Source-level integration review** — not rendered in the editor window. Run the `sorolla-sdk-integration-review` audit (source-only: event wiring, Palette API usage, config alignment) before the on-device pass, not instead of it. Ask your Sorolla contact if you don't have this skill/prompt.

The verdict reads `HEALTHY`, `N ISSUES`, or `FAILING` — same three-state semantics as the on-device Vitals overlay (§3 of `troubleshooting.md` covers the general debug flow; this doc covers the QA-specific surfaces only). A single `FAILING` row anywhere fails the whole verdict. Use **Copy Greenlight Report** for a pasteable plain-text version to attach to a PR or ticket.

## 2. The QA bridge (device-level ground truth)

A loopback-only HTTP server compiled into every build (Editor, development, and release), bound to `127.0.0.1:8765`. It never binds `0.0.0.0` and is not reachable off-device — reach it over USB.

**Android:**
```
adb forward tcp:18765 tcp:8765
curl -H "X-Sorolla-QA-Password: <password>" http://127.0.0.1:18765/qa/snapshot
```

**iOS:** no `adb` equivalent ships with the SDK. Forward the port yourself over usbmux (e.g. `iproxy`) before curling the same loopback URL.

**Password**: `X-Sorolla-QA-Password` header (also accepted as `Authorization: Bearer <password>` or a `?qa_password=` query param). Resolution order: `SorollaConfig.qaBridgePassword` if set on the game's config asset, else the SDK's built-in default. **Do not hardcode the default into your own scripts or commit it anywhere** — read it from the config asset at runtime, or ask Sorolla for the current value. Treat anything returned by the bridge as potentially externally readable; it is a QA convenience surface, not a hardened one (see `known-issues.md` / the SDK's internal audit notes if you have access).

An empty reply is **not** an auth failure — it means the app has not booted or is not foregrounded yet. Wait ~20-30s after launch and retry before concluding the bridge is down.

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

Error responses: `403` + `{"detail":"qa_password_required"}` (bad/missing password), `400` + `{"detail":"unknown_action"}` (typo'd action name) or `{"detail":"bad_request"}` (malformed body), `404` + `{"detail":"unknown_endpoint"}` (wrong path), `411`/`413` (missing/oversized request body).

Test-generating actions (`track_test_event`, `level_*`, `economy_*`) run inside a tagged test-action scope: they are excluded from integration health counters and filterable out of Firebase, so running them repeatedly during QA does not pollute the game's real analytics.

## 3. QA Expectations asset

**Assets/Resources/SorollaQaExpectations.asset** (create via **Assets > Create > Palette > QA Expectations**). Optional — a missing asset means "no expectations configured," never a hard failure, and nothing at SDK init time depends on it.

This is what turns the mechanical verdict from generic to game-specific. Fill it once per game repo:

| Field | Why it changes the verdict |
|---|---|
| `intendedMode` | Enables the Mode Intent check (§1.3) — set it once you know which mode this game ships in. Leave `Unspecified` if undecided; the row degrades to informational, never fails. |
| `usesRewarded` / `usesInterstitial` / `hasEconomy` / `tracksEconomy` / `usesIap` / `usesAddressables` | Feature flags a source-level integration review checks against actual code usage. |
| `iapPlatforms` + `expectedSkus[]` | Drives the "Store SKUs / Testing Track Configured" manual row — only appears if `usesIap` is set, and states the expected SKU count so a mismatch is visible at a glance. |
| `knownExpectedFailures[]` (area, platform, note) | Prevents a known, accepted platform gap (e.g. IAP store-init failing on Android for an iOS-only game) from reading as a regression. Keep this list current or the verdict cries wolf. |
| `firstInterstitialAtLevel` | Reference value for manual ad-cadence QA; not machine-checked today. |
| `notes` | Free text for anything else a human reviewing the verdict should know. |

Version this asset in the game repo. Changes should land via a normal PR your publisher can review — it is the reviewable substitute for the old "tribal knowledge in someone's head" model.

## 4. The Vitals overlay (on-device, no bridge needed)

Every build ships an on-screen diagnostics overlay reachable without USB or a password: **tap the top-right corner of the screen 5 times within 2 seconds, then hold the final tap for 0.8 seconds.** It reveals the same verdict model as the Greenlight window (hero verdict + area cards + an Issues tab with WHY/SIGNAL/FIX per problem) and exposes the same action buttons the bridge's `/qa/exec` drives.

Use it when you don't have adb/usbmux set up, or as the human-facing companion to a bridge-driven agent run — anything the bridge reports, Vitals explains in the same terms, in person, on the device.

## 5. Escalation boundary

Not every red row is something an agent (or a studio engineer) can fix. Classify before acting:

- **Studio-fixable, with the fix in the row itself** — Build Health errors/warnings, most probe failures (wrong app ID, mismatched client token), Mode Intent mismatches. The row's `Fix` text names the concrete change (usually: edit `SorollaConfig`, `FacebookSettings`, or the QA Expectations asset).
- **Dashboard work, with a doc link** — every row in the manual checklist (§1.5) and the deeper procedures in [dashboards/](dashboards/). These are things the SDK cannot verify by design (see each vendor page for why) — go configure the vendor's dashboard, then re-run the verdict.
- **Contact Sorolla, with the copied SDK state** — cross-vendor drift that spans two dashboards Sorolla owns (see `dashboards/applovin-max.md`), anything that looks like a credential the studio doesn't hold, or a `FAILING` verdict that persists after working through every row above. Use **Copy Greenlight Report** and attach the raw text — do not paraphrase the verdict when escalating, the exact row text is the evidence.

See also: [dashboards/](dashboards/) for the vendor-specific detail behind each manual row, [troubleshooting.md](troubleshooting.md) for build/runtime issues outside the QA surfaces, [known-issues.md](known-issues.md) for previously-seen field incidents.
