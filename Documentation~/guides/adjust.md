# Adjust Setup

Full attribution tracking for production.

> Adjust is **required in Full mode** only. Not needed for Prototype.

---

## 1. Create Account

1. Sign up at [adjust.com](https://www.adjust.com)
2. Create an app for iOS and/or Android
3. Copy your **App Token** (12-character string, e.g., `abc123def456`)

## 2. Configure in Unity

1. Open **Palette > Configuration**
2. Under **SDK Keys** → **Adjust**, enter your App Token
3. Save

---

## Campaign Tracking

Create tracking links for marketing campaigns:

1. In Adjust dashboard, go to **Campaign Lab** → **Trackers**
2. Create tracking links for each campaign/source
3. Share links with marketing team

Attribution data appears in Adjust dashboard within 24 hours.

---

## Ad Revenue Attribution

Ad revenue from MAX is automatically forwarded to Adjust when both are configured.

View revenue attribution in Adjust dashboard → **Reports** → **Ad Revenue**.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No attribution data | Wait 24 hours, verify App Token |
| Test installs not showing | Use Adjust testing mode in dashboard |
| Ad revenue not tracking | Verify both MAX and Adjust are initialized (check Debug UI) |
