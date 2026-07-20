# AppLovin MAX Dashboard

AppLovin MAX is required in **Full mode** only (not used in Prototype). What must be true in the MAX dashboard, and why the SDK cannot verify most of it from the client.

## What to configure

- Ad units created per format (Rewarded, Interstitial, optionally Banner) for each platform, entered into `Tools > Sorolla Palette SDK`.
- Mediation networks configured under Monetize → Manage (AdMob, Meta Audience Network, Unity Ads, etc.).
- GDPR/consent flow: Google Ad Manager or AdMob installed under Mediated Networks (required for the UMP consent form to render), Terms and Privacy Policy Flow enabled with a Privacy Policy URL and ATT usage description.
- `app-ads.txt` published on the studio's developer website domain.

Full procedure: [AppLovin MAX docs](https://developers.applovin.com/en/max/unity/overview/integration). Sorolla's internal dashboard-creation runbook: [dashboard-setup.md](../dashboard-setup.md).

## What the SDK cannot verify: cross-vendor Facebook app-id drift

If this game mediates Meta Audience Network through MAX, **the FAN setup inside MAX and Adjust's own Facebook integration (see [adjust.md](adjust.md)) both reference a Facebook App ID server-side, independently of each other and of the SDK's own Facebook config.** A previously-seen real incident: a Facebook app was deleted from the developer console, but its now-dead App ID was still referenced inside both MAX's FAN mediation setup and Adjust's Facebook integration setting — neither vendor's dashboard flagged it, and the SDK's own Facebook Graph probe (see [facebook.md](facebook.md)) couldn't see it either, because that probe is scoped to the FB app object itself and has no visibility into what another vendor's server-side config references.

This is **ruled not probe-coverable**: no API call the SDK can make from the client reaches into MAX's or Adjust's server-side FAN/attribution config to check what Facebook App ID they have on file. It stays a permanent manual verdict row.

## What the verdict shows when it's wrong

| Row | State | Meaning |
|---|---|---|
| Cross-Vendor Dashboard Drift (manual checklist) | **Wait**, until manually ticked | Permanent manual row — no probe can close this. Confirm the Facebook App ID referenced in MAX's FAN mediation setup and in Adjust's Facebook integration setting both match the live, current Facebook app (see [facebook.md](facebook.md) for confirming the FB app itself is live). |

There is no automated signal for this drift today; a future runtime MAX adapter-error listener (not yet built) is the only planned path to catching it mechanically. Until then, this is a genuinely manual dashboard-to-dashboard comparison.

## Deep link

[dash.applovin.com](https://dash.applovin.com/)
