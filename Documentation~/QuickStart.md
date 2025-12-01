# Sorolla SDK - Quick Start Guide

## **1. Initialization**

The SDK initializes automatically. No setup required.

---

## **2. Level Tracking (Critical)**

The **Level Funnel** is your most important metric. Track when users start, win, or lose levels.

### **✅ Start a Level**

```csharp
Sorolla.TrackProgression(ProgressionStatus.Start, "Level_005");
```

### **🏆 Complete a Level**

```csharp
Sorolla.TrackProgression(ProgressionStatus.Complete, "Level_005", score: 100);
```

### **💀 Fail a Level**

```csharp
Sorolla.TrackProgression(ProgressionStatus.Fail, "Level_005");
```

> ⚠️ Important: Zero-pad level numbers (Level_005 not Level_5) for correct dashboard sorting.

---

## **3. Custom Events**

Track specific behaviors outside level flow.

```csharp
Sorolla.TrackDesign("UI:Shop:RemoveAds_Click");
Sorolla.TrackDesign("Gameplay:BoosterUsed", 1);
Sorolla.TrackDesign("Tutorial:Step_01");
```

---

## **4. Remote Config (A/B Testing)**

Tune values without app updates.

```csharp
Sorolla.FetchRemoteConfig(success =>
{
    if (success)
    {
        float speed = Sorolla.GetRemoteConfigFloat("player_speed", 5.5f);
        bool xmasEvent = Sorolla.GetRemoteConfigBool("event_xmas", false);
    }
});
```

---

## **5. Economy Tracking (Optional)**

Only if your prototype uses currency.

```csharp
// Player earned coins
Sorolla.TrackResource(ResourceFlowType.Source, "Coins", 50, "Chest", "Level_001");

// Player spent coins
Sorolla.TrackResource(ResourceFlowType.Sink, "Coins", 100, "Shop", "Blue_Hat");
```

---

## **✅ Pre-Build Checklist**

- [ ] Every `Start` has a matching `Complete` or `Fail`
- [ ] Level names are zero-padded (`Level_001`)
- [ ] Tutorial steps are tracked (`Tutorial:Step_01`, `Step_02`...)
