---
title: SpireKit
description: The modding SDK Slay the Spirits is built on.
---

SpireKit is a small, test-first SDK for Slay the Spire 2 mods. It lives in this repo under `sdk/` and will be split
into its own package once its API settles. It knows nothing about drinking: Slay the Spirits is just its first user.

:::note
SpireKit isn't published yet. Until it is, the best reference is the source and the
[architecture guide](../../development/architecture/), which has a how-to for each feature.
:::

## Goals

- **Survive game updates.** Early Access patches rename and reshape internals. A breaking change should cost one
  feature, be reported precisely in the game log, and be fixable in one place.
- **Test without the game.** Anything that isn't a direct call into the game is plain C# with unit tests.
- **One DLL per mod.** SpireKit is merged into each mod's DLL, so every mod pins its own version.

## What it gives a mod

| Area | Highlights |
|---|---|
| Content | Cards, relics, potions and powers from raw PNG art; per-run filters for cards and potions; model name checks |
| Rest sites | Extra rest site options with their own icons |
| Game events | Typed events (`RunStarted`, `CombatStarted`, `PlayerDied`, …) that run in lockstep on every peer |
| Multiplayer | Named network channels, host-authoritative settings with run locking |
| Storage | Versioned JSON documents with migrations, defaults and corrupt-file backups; per-run data that survives save and quit |
| UI | Toasts, speech bubbles, combat banners, the game's fonts and colors |
| Settings | A tab on the game's Settings screen built from the game's own controls, plus custom controls |
| Determinism | Seeded random streams derived from the run seed |
| Diagnostics | Fail-soft Harmony patches with a per-feature health report in the game log |
