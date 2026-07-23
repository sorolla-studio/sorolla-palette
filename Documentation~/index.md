# Sorolla Palette SDK

<div class="srl-hero">
  <p class="srl-hero-eyebrow">Sorolla Studio · Unity SDK</p>
  <p class="srl-hero-lead">One package for everything a Sorolla game ships with: analytics, ads, attribution, consent, and remote config behind a single <code>Palette</code> API. Install it, paste your keys, wire three level events — Palette handles the rest.</p>
  <div class="srl-cards">
    <a class="srl-card" href="quick-start.html">
      <span class="srl-card-step">Step 1</span>
      <strong>Prototype Mode</strong>
      <span>First integration. GameAnalytics and Facebook, with optional Firebase for publisher review builds and CPI tests. About an hour.</span>
    </a>
    <a class="srl-card" href="switching-to-full.html">
      <span class="srl-card-step">Step 2</span>
      <strong>Full Mode</strong>
      <span>When the game is ready for soft launch: ads, attribution, consent, and revenue validation on top of Prototype.</span>
    </a>
  </div>
</div>

## The integration journey

<div class="srl-journey">
  <span class="srl-journey-step"><a href="quick-start.html">Prototype</a></span>
  <span class="srl-journey-step"><a href="switching-to-full.html">Full migration</a></span>
  <span class="srl-journey-step"><a href="validation.html">Validation</a></span>
  <span class="srl-journey-step">Soft launch</span>
</div>

Every game follows the same path. Fresh installs start in Prototype mode automatically; do not switch to Full mode until Sorolla tells you to.

| Mode | Included SDKs | Best for |
|------|---------------|----------|
| Prototype | GameAnalytics, Facebook; Firebase optional | Publisher review builds, CPI tests, gameplay iteration |
| Full | Prototype SDKs plus AppLovin MAX and Adjust | Soft launch, monetization tests, paid UA |

Palette auto-initializes at runtime. Studios only wire game events and game-specific placements.

## Common paths

| Need | Go to |
|------|-------|
| First integration | [Prototype Mode Quick Start](quick-start.md) |
| Soft-launch migration | [Full Mode Soft Launch Migration](switching-to-full.md) |
| Full-mode validation | [Full Mode Validation](validation.md) |
| API signatures | [API Reference](api-reference.md) |
| Build or dashboard issue | [Troubleshooting](troubleshooting.md) |
| Upload/store rejections seen before | [Known Issues](known-issues.md) |
| App Store privacy answers | [App Store Privacy](app-store-privacy.md) |

## SDK setup guides

Use these only when you need dashboard-level detail:

- [GameAnalytics](guides/gameanalytics.md)
- [Facebook](guides/facebook.md)
- [Firebase](guides/firebase.md)
- [AppLovin MAX Ads](guides/ads.md)
- [Adjust Attribution](guides/adjust.md)
- [GDPR / ATT Consent](guides/gdpr.md)

## Self-serve integration QA

For running QA (human or agent-driven) once the SDK is integrated - the Launch Readiness verdict, the QA bridge, and what the SDK can and cannot verify per vendor:

- [Agent Pack](agents.md) - how a studio's own AI agent runs integration QA: reading Launch Readiness, the `/qa/snapshot` bridge contract, on-device Vitals, and the escalation boundary.
- Vendor dashboard pages (what a probe can't check, and why): [GameAnalytics](dashboards/gameanalytics.md), [Facebook](dashboards/facebook.md), [Adjust](dashboards/adjust.md), [AppLovin MAX](dashboards/applovin-max.md), [Firebase](dashboards/firebase.md).
