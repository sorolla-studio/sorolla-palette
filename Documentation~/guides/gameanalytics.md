# GameAnalytics Setup

Analytics and event tracking for your game.

---

## 1. Create Account

1. Go to [gameanalytics.com](https://gameanalytics.com)
2. Sign up and create a new game
3. Select **iOS** and/or **Android**, Engine: **Unity**

## 2. Get API Keys

1. Open your game in GameAnalytics dashboard
2. Go to **Settings** → **Game Settings** (gear icon)
3. Copy:
   - **Game Key** (hexadecimal string)
   - **Secret Key** (hexadecimal string)

## 3. Configure in Unity

1. Open **Window** → **GameAnalytics** → **Select Settings**
2. Log in or paste keys manually
3. Save

## 4. Grant Admin Access

Required for Sorolla team support:

1. Go to **Settings** → **Users**
2. Click **Invite User**
3. Enter: `studio@sorolla.io`
4. Set Role: **Admin**
5. Send Invite

---

## Verify

Events appear in the dashboard within 5-10 minutes.

Use the [Debug UI](../quick-start.md#optional-debug-ui) to verify initialization on device.

---

## Tracking APIs

The following `Palette` APIs dispatch to GameAnalytics (and also to Firebase when enabled — see the fan-out table in [architecture.md](../architecture.md#data-flow)):

```csharp
using Sorolla.Palette;

// Custom structured event (GA4-compatible name, Firebase gets full params)
Palette.TrackEvent("booster_used", new Dictionary<string, object>
{
    { "booster_id", "speed_2x" },
    { "level", 12 },
});

// Level progression — Firebase mapping: Start -> level_start, Complete -> level_end, Fail -> level_fail
Palette.TrackProgression(ProgressionStatus.Complete, "World1", "Chapter2", "Level3",
    score: 1500,
    extraParams: new Dictionary<string, object> { { "duration_sec", 45 } });

// Economy events — Firebase mapping: Source -> earn_virtual_currency, Sink -> spend_virtual_currency
Palette.TrackResource(ResourceFlowType.Source, "coins", 100, "Reward", "DailyLogin");
Palette.TrackResource(ResourceFlowType.Sink,   "gems",    5, "Booster", "speed_2x");

// In-app purchase — fans out to Adjust + TikTok + Firebase
Palette.TrackPurchase(4.99, "USD",
    productId:     "com.mygame.coins_100",
    transactionId: storeReceipt.transactionId);
```

For full method signatures see [api-reference.md](../api-reference.md).

---

## Tracking Conventions

Keep event data clean and queryable:

1. **Event names** — lowercase `snake_case`, max 40 characters (`tutorial_complete`, `booster_used`). No reserved prefixes (`firebase_`, `google_`, `ga_`).
2. **Level names** — zero-pad numbers (`Level_001` not `Level_1`) so lexicographic sorts match numeric order in the dashboard.
3. **Resource tracking** — track every currency flow, both Source and Sink, for complete economy analysis.
4. **Remote Config** — always call `Palette.SetRemoteConfigDefaults()` at startup so games work offline and during the first-fetch window.
5. **Ad timing** — check `Palette.IsRewardedAdReady` before enabling the Watch Ad button to avoid dead clicks.
