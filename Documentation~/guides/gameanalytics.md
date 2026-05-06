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

Use [Sorolla Vitals](../quick-start.md#4-build-and-verify-on-device) to verify initialization on device.

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

// Level progression. Complete/Fail auto-include duration if Start was called.
Palette.Level.Start(level: 3, world: 1);
Palette.Level.Complete(level: 3, world: 1, score: 1500,
    extraParams: new Dictionary<string, object> { { "difficulty", "hard" } });

// Economy events. Source -> earn_virtual_currency, Sink -> spend_virtual_currency.
Palette.Economy.Earn(CurrencyId.Coins, 100, EconomySource.LevelReward, itemId: "level_3");
Palette.Economy.Spend(CurrencyId.Gems, 5, EconomySink.Booster, itemId: "speed_2x");

// In-app purchases are tracked through Unity IAP v5 wiring.
// See Full Mode Soft Launch Migration for Palette.AttachPurchaseTracking(store).
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
