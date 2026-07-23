# Prototype Mode Quick Start

<div class="srl-journey">
  <span class="srl-journey-step srl-journey-current">Prototype</span>
  <span class="srl-journey-step"><a href="switching-to-full.html">Full migration</a></span>
  <span class="srl-journey-step"><a href="validation.html">Validation</a></span>
  <span class="srl-journey-step">Soft launch</span>
</div>

Fresh installs start in **Prototype** mode automatically. Do not switch to Full mode until Sorolla tells you to do so.

Follow these instructions to prepare the build in prototype mode:

| SDK | Studio task |
|-----|----|
| GameAnalytics | Create the game, paste Game Key + Secret Key, add level calls |
| Facebook SDK | Create the app, paste App ID + Client Token |
| Firebase (optional) | Install only when this Prototype needs Firebase analytics, Crashlytics, or Remote Config |

---

## 1. Install Palette

1. In Unity, open **Window > Package Manager**.
2. Click `+` and choose **Add package from git URL**.
3. Paste:

```text
https://github.com/sorolla-studio/sorolla-palette.git
```

4. Wait for Unity to import the package and resolve dependencies.
5. Open **Tools > Sorolla Palette SDK** if the Palette window does not open automatically.

**You do not need a bootstrap prefab or a manual init call.** Palette auto-initializes at runtime through `SorollaBootstrapper`.

---

## 2. Match App Identifiers

Use the same identifiers in Unity, GameAnalytics, Facebook, and Firebase.

In Unity, check:

- **Android package name:** **Project Settings > Player > Android > Other Settings > Package Name**
- **iOS bundle ID:** **Project Settings > Player > iOS > Other Settings > Bundle Identifier**

If a dashboard app uses a different package name or bundle ID, fix the dashboard before building. Identifier mismatches are the fastest way to lose install and event data.

---

## 3. Create GameAnalytics Keys

1. Go to [gameanalytics.com](https://gameanalytics.com).
2. Create a game for Android and/or iOS with **Engine = Unity**.
3. Open **Settings > Game Settings**.
4. Copy the **Game Key** and **Secret Key** for each platform.
5. In Unity, open **Window > GameAnalytics > Select Settings** and paste the keys.

Grant Sorolla dashboard access if Sorolla will review analytics:

1. In GameAnalytics, open **Settings > Users**.
2. Invite `studio@sorolla.io` as **Admin**.

Full details: [GameAnalytics Setup](guides/gameanalytics.md).

---

## 4. Create Facebook Keys

1. Go to [developers.facebook.com](https://developers.facebook.com).
2. Create an app.
3. Copy the **App ID** from the app dashboard header.
4. Copy the **Client Token** from **Settings > Advanced > Security**.
5. Add the iOS platform with the Unity bundle ID.
6. Add the Android platform with the Unity package name.
7. In Unity, open **Facebook > Edit Settings** and paste the App ID and Client Token.

Authorize Sorolla's ad account if Sorolla will run UA:

1. In Facebook, open **Settings > Advanced**.
2. In **Authorized Ad Account IDs**, add `2220619588747821`.

Full details: [Facebook SDK Setup](guides/facebook.md).

---

## 5. Optional: Create Firebase Configs

1. Go to [Firebase Console](https://console.firebase.google.com/).
2. Create a project and enable **Google Analytics**.
3. Add an Android app with the Unity package name, then download `google-services.json`.
4. Add an iOS app with the Unity bundle ID, then download `GoogleService-Info.plist`.
5. Place both files in `Assets/`:

```text
Assets/google-services.json
Assets/GoogleService-Info.plist
```

Skip this section when the Prototype does not need Firebase. In **Tools > Sorolla Palette SDK**,
install Firebase from the Firebase row if it is needed, then press **Refresh** and confirm the
Firebase group reports the config file for your active build target. Firebase becomes required in
Full mode.

Full details: [Firebase Setup](guides/firebase.md).

---

## 6. Check Palette Configuration

Open **Tools > Sorolla Palette SDK**.

Confirm:

- The current mode is **Prototype**.
- GameAnalytics is configured.
- Facebook SDK is configured.
- If this Prototype uses Firebase, it is installed and the active platform config file is present.
- The **Launch Readiness** verdict reads **HEALTHY**.

Keep Prototype mode lean. MAX and Firebase are optional: when absent, their checks and test steps do
not appear; when installed, Palette validates them fully. Do not install Adjust or Unity IAP for a
Prototype that does not use them. TikTok is a parked vendor: new integrations should not configure
it, though an existing compatibility config may remain (see the [TikTok guide](guides/tiktok.md)).

---

## 7. Add Level Analytics

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

Rules:

- Call `Start` when gameplay begins, not when a menu opens.
- Call either `Complete` or `Fail` once per run.
- Do not manually initialize Palette.
- Keep any existing studio analytics you still need.

---

## Prototype Checklist

- [ ] Unity package name and bundle ID match the dashboard apps.
- [ ] GameAnalytics Game Key and Secret Key are configured.
- [ ] Facebook App ID and Client Token are configured.
- [ ] If this Prototype uses Firebase, its active-platform config file is in `Assets/`.
- [ ] **Tools > Sorolla Palette SDK** shows Prototype mode.
- [ ] **Launch Readiness** has no blocking issues for your build target.
- [ ] `Palette.Level.Start`, `Palette.Level.Complete`, and `Palette.Level.Fail` are wired in game code.
- [ ] A real-device build launches and one complete level can be played.
- [ ] Sorolla has the build and dashboard access needed for review.
