# Quick Start: Prototype Mode

Use Prototype mode to add core analytics to your Unity build quickly, without taking on the full soft-launch ad, attribution, and consent stack.

**Best for:** first playable builds, CPI tests, gameplay iteration, publisher review builds.

Prototype mode includes:

| Area | Included | What you do |
|------|----------|-------------|
| Analytics | GameAnalytics progression events | Add three level calls |
| Acquisition signal | Facebook SDK configuration | Paste App ID + Client Token |
| Firebase | Analytics, Crashlytics, Remote Config | Add config files |
| Diagnostics | Sorolla Vitals runtime console | Open on device and verify green checks |

Prototype mode does **not** require MAX ads, Adjust, GDPR/ATT consent setup, `app-ads.txt`, or purchase attribution. Add those when you migrate to [Full Mode for soft launch](switching-to-full.md).

---

## Before You Start

Have these ready:

- Unity 2022.3 or newer.
- The Android package name and iOS bundle ID you plan to test.
- GameAnalytics Game Key and Secret Key for each platform.
- Facebook App ID and Client Token.
- Firebase config files: `google-services.json` and `GoogleService-Info.plist`.
- A real Android or iOS device for validation.

If Sorolla is creating the dashboards for you, ask for the pre-filled values and skip straight to [Configure Prototype Mode](#2-configure-prototype-mode).

---

## 1. Install

1. In Unity, open **Window > Package Manager**.
2. Click `+` and choose **Add package from git URL**.
3. Paste:

```text
https://github.com/sorolla-studio/sorolla-palette.git
```

4. Wait for Unity to import the package and resolve dependencies.
5. Open **Palette > Configuration** if the configuration window does not open automatically.

You do not need to add a bootstrap prefab or any manual initialization call. Palette auto-initializes at runtime through `SorollaBootstrapper`.

---

## 2. Configure Prototype Mode

In **Palette > Configuration**:

1. Set the mode to **Prototype**.
2. Enter the required keys.
3. Use **Build Health** to confirm the project is coherent.

| SDK | Required for Prototype | What you do | Pass condition |
|-----|------------------------|---------------|----------------|
| GameAnalytics | Yes | Add Game Key + Secret Key | SDK Overview is green |
| Facebook | Yes | Add App ID + Client Token | SDK Overview is green |
| Firebase | Yes | Add `google-services.json` and `GoogleService-Info.plist` to `Assets/` | Firebase checks are green |

Keep Prototype mode lean. Do not configure MAX, Adjust, TikTok, IAP, consent, or store privacy yet.

---

## 3. Add Level Analytics

Add the level calls where your game already knows a level started, completed, or failed:

```csharp
using Sorolla.Palette;

public void OnLevelStarted(int level)
{
    Palette.Level.Start(level);
}

public void OnLevelWon(int level, int score)
{
    Palette.Level.Complete(level, score: score);
}

public void OnLevelLost(int level)
{
    Palette.Level.Fail(level);
}
```

If your game has worlds, pass both values:

```csharp
Palette.Level.Start(level: 4, world: 2);
Palette.Level.Complete(level: 4, world: 2, score: 1500);
```

Implementation rules:

- Call `Start` when gameplay begins, not when a menu opens.
- Call either `Complete` or `Fail` once per run.
- Keep your existing game analytics if you need them; Palette only needs these calls for Sorolla reporting.
- Do not manually initialize the SDK.

---

## 4. Build and Verify on Device

1. Build to a real Android or iOS device.
2. Launch the app and play one complete level.
3. Open Sorolla Vitals with five taps in the top-left safe area.

You can also open Vitals from a debug button:

```csharp
Palette.ShowDebugger();
```

In Vitals, check:

- SDK readiness is green for the SDKs configured in Prototype mode.
- Level events appear after you play a level.
- Event payloads contain the expected level and score values.
- No startup errors are logged.

---

## Prototype Definition of Done

- [ ] Project is in **Prototype** mode.
- [ ] GameAnalytics is green in SDK Overview.
- [ ] Facebook is green in SDK Overview.
- [ ] Firebase is green in SDK Overview.
- [ ] `Palette.Level.Start`, `Complete`, and `Fail` are wired in game code.
- [ ] Device build launches without SDK errors.
- [ ] Sorolla Vitals shows expected SDK readiness and level events.
- [ ] Prototype validation passes in the [Validation Checklist](validation.md#prototype-validation).

---

## Next Steps

| Goal | Link |
|------|------|
| Validate this build | [Validation Checklist](validation.md#prototype-validation) |
| Prepare for soft launch | [Switch to Full Mode](switching-to-full.md) |
| Add Firebase features | [Firebase Guide](guides/firebase.md) |
| Fix an integration issue | [Troubleshooting](troubleshooting.md) |
