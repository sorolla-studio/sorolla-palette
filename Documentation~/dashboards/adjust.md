# Adjust Dashboard

Adjust is required in **Full mode** only (not used in Prototype). What must be true in the Adjust dashboard, and why the SDK cannot verify it from the client.

## Create ONE multi-platform app, not per-platform apps

**Create a single Adjust app that covers both Android and iOS**, and use its one App Token everywhere. Do not create a separate Adjust app per platform.

This isn't a preference — the SDK's config schema holds exactly one Adjust App Token (`SorollaConfig`), applied to whichever platform the build targets. There is no per-platform token field. A studio that creates two apps (one Android, one iOS) will end up with two App Tokens and only one field to put either of them in — whichever platform's token isn't in `SorollaConfig` silently reports under the wrong app, or fails init entirely. If a studio already has separate per-platform apps from before integrating with Sorolla, consolidate to one multi-platform app before going to Full mode.

## What to configure

- One Adjust app (multi-platform), App Token copied into `Tools > Sorolla Palette SDK` → **SDK Keys** → **Adjust**.
- A **Purchase** event created in the dashboard, with its Event Token — needed if this game sells IAP.
- Environment set to **Sandbox** while testing, **Production** before release.
- Server-side **purchase verification** enabled under the app's Event settings, if selling IAP (this is the one dashboard toggle the SDK cannot read back — see below).

Full procedure: [Adjust help center](https://help.adjust.com/en/article/set-up-adjust). Sorolla's internal dashboard-creation runbook: [dashboard-setup.md](../dashboard-setup.md).

## What the SDK cannot verify

Nothing about Adjust dashboard configuration is probeable from the SDK today — there is no credential-only API call analogous to the Facebook Graph probe or the GameAnalytics HMAC probe that would prove the App Token is live and correctly scoped. Two things specifically need a manual dashboard check:

- **Purchase verification toggle.** Whether server-side receipt verification is turned on for the Purchase event is an Adjust dashboard setting, not something the SDK can read back through any API it calls. If it's off, purchase events still submit but are not verified — revenue can be spoofed client-side.
- **Cross-vendor Facebook app-id drift.** If this game also uses AppLovin MAX with Meta Audience Network mediation, Adjust's own Facebook integration setting and MAX's FAN setup both reference a Facebook App ID server-side, independently of each other and of the SDK's Facebook config. See [applovin-max.md](applovin-max.md) for why this specific drift is not probe-coverable and stays a permanent manual check.

## What the verdict shows when it's wrong

| Row | State | Meaning |
|---|---|---|
| Adjust Purchase Verification (Full mode) | **Wait**, until manually ticked | Only shown in Full mode. Permanent manual row — no probe can close this. Adjust dashboard → the app → Event settings → confirm purchase verification is ON, then tick the row. |

A wrong or stale App Token doesn't fail loud in the verdict today — it shows up as the `adjust` adapter state in a device snapshot (`/qa/snapshot` → `adapters.adjust`) not reaching an initialized state, or as `identity.adjust_adid_present` staying false. Check those if Adjust events aren't appearing in the dashboard despite the config looking right.

## Deep link

[suite.adjust.com](https://suite.adjust.com/)
