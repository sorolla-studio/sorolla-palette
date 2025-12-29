# Getting Started

Get your game integrated with Sorolla SDK in 5 minutes.

---

## 1. Install the Package

**Unity Package Manager** → `+` → **Add package from git URL**:
```
https://github.com/LaCreArthur/sorolla-palette-upm.git
```

The Configuration window opens automatically.

---

## 2. Choose Your Mode

| | Prototype Mode | Full Mode |
|--|---------------|-----------|
| **Purpose** | Rapid UA testing | Production launch |
| **Analytics** | GameAnalytics | GameAnalytics |
| **Attribution** | Facebook SDK | Adjust |
| **Ads** | Optional | Required |
| **When to use** | CPI tests, soft launch | Live game |

**Click** `Prototype Mode` or `Full Mode` in the Configuration window, then follow the setup guide:

- **[Prototype Mode Setup](prototype-setup.md)** - GameAnalytics + Facebook
- **[Full Mode Setup](full-setup.md)** - GameAnalytics + Adjust + MAX

---

## 3. Verify Integration

### Debug UI (On-Device Testing)

1. Import the **Debug UI** sample from Package Manager
2. Build to device
3. **Triple-tap** screen (mobile) or press **BackQuote** key (desktop)

The debug panel shows:
- SDK initialization status
- Ad loading/showing
- Event tracking
- ATT/consent status

---

## Next Steps

- [Ads Setup](ads-setup.md) - AppLovin MAX for rewarded and interstitial ads
- [Firebase Setup](firebase.md) - Analytics, Crashlytics, Remote Config
- [API Reference](api-reference.md) - Full API documentation
- [Troubleshooting](troubleshooting.md) - Common issues and fixes
