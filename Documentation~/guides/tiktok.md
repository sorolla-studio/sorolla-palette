# TikTok Setup

> **Parked vendor.** TikTok is not part of the active supported vendor set; compatibility remains for
> existing integrations. The adapter still ships and the `SorollaConfig` TikTok fields remain, so an
> existing TikTok configuration keeps working. This guide stays for studios that already have a TikTok
> setup; new integrations should not configure TikTok.

Event tracking for TikTok ad campaigns.

> TikTok is **mode-independent**. It activates when config fields are populated, regardless of Prototype/Full mode.

---

## 1. Create App

1. Go to [TikTok Events Manager](https://ads.tiktok.com) (requires a TikTok ad account)
2. Create a new App, or attach SDK setup to an existing MMP-connected app
3. Collect three values per platform:

| TikTok Dashboard field | SorollaConfig field | Notes |
|------------------------|---------------------|-------|
| App ID (Events Manager) | `tiktokEmAppId` | Passed to SDK as `setAppId` |
| TikTok App ID (long numeric) | `tiktokAppId` | Passed to SDK as `setTTAppId` |
| Access Token (App Secret) | `tiktokAccessToken` | Used in SDK constructor |

## 2. Configure in Unity

1. Open **Tools > Sorolla Palette SDK**
2. Under **TikTok**, enter all three values for Android and/or iOS
3. Leave **Verbose Logging** (`verboseLogging`) **off** for distributed builds (auto-forced off in release builds regardless)

TikTok initializes automatically when all three fields are populated.

---

## What Gets Tracked

When configured, TikTok receives:
- **Purchase events** from the SDK-owned Unity IAP tracking path (`Palette.AttachPurchaseTracking(store)`)
- **Ad revenue** from MAX ILRD callbacks (revenue, currency, network, ad format)
- **Custom events** via the TikTok adapter directly

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Events not appearing | Check TikTok Events Manager > Test Events; verify all three config fields |
| Debug logging left on | `verboseLogging` is auto-forced off in release builds; no action needed |
| Field name confusion | `tiktokEmAppId` is the Events Manager App ID; `tiktokAppId` is the long numeric TikTok App ID |
| Android init failure | TikTok requires UI thread init - check logcat for `[Palette:TikTok]` errors |
