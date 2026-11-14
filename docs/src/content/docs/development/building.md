---
title: Building from source
description: Build the mod, run the tests, launch the game and work on these docs.
---

## Requirements

- .NET SDK 9 or later
- Slay the Spire 2 (v0.113.0 or later)
- Godot 4.5.1 mono, only for packing `.pck` files
- Node.js 22 or later, only for these docs

## Build

```powershell
copy local.props.example local.props   # then edit the paths
dotnet test                            # rules engine and SDK tests (no game needed)
dotnet build mod/src/SlayTheSpirits    # builds, merges, packs and installs to <game>/mods/SlayTheSpirits
```

The build skips installing (with a warning) while the game is running. Close it and build again.

## Run the game

```powershell
./tools/launch.ps1 -Build                  # rebuild (closes the game first), then launch on test profile 1
./tools/launch.ps1 -OpenSettings Spirits   # straight to the mod's Settings tab
./tools/launch.ps1 -Gallery                # SpireKit controls gallery (F8 toggles)
./tools/launch.ps1 -Steam                  # your real Steam profile
```

For multiplayer on one machine:

```powershell
./tools/test.ps1 -Build        # host and 1 client (LAN, Steam off, separate test profiles)
./tools/test.ps1 -Clients 3    # a 4-player table
```

## Repository layout

```
sdk/                           SpireKit, the reusable SDK
  src/SpireKit.Core/           engine-agnostic: events, determinism, storage, host-authority sync
  src/SpireKit/                game-bound: bootstrap, patching, game events, networking, content, UI
mod/
  src/SlayTheSpirits.Domain/   the drinking-game engine: rules, triggers, targeting, policies, tally
  src/SlayTheSpirits/          the game-bound mod: wiring, cards, relics, potions, presentation, assets
  tools/SlayTheSpirits.DocsExport/  exports the rules engine's data for these docs
build/                         MSBuild targets (game references, merge, pack, deploy) and the PCK packer
docs/                          this site (Astro Starlight)
tools/                         launch and test scripts, placeholder icon generator
```

## Work on the docs

```powershell
cd docs
npm install
npm run dev      # http://localhost:4321/
```

The [cards](../../wiki/cards/), [relics](../../wiki/relics/), [potions](../../wiki/potions/),
[drink ladder](../../play/how-it-works/) and [rule set table](../../play/rule-sets/) are generated from the mod's
source each time the site builds (`npm run generate`):

- `mod/tools/SlayTheSpirits.DocsExport` runs the real rules engine and writes the presets, ladder and trigger
  names to `src/generated/rules.json`.
- `scripts/extract-content.mjs` reads the card, relic and potion classes and their localization, writes
  `src/generated/content.json`, and copies their icons into `public/generated/`. If it can't read something (a new
  kind of upgrade, a missing translation), the build fails and says which model to fix.

Pushing to `main` publishes the site to GitHub Pages (`.github/workflows/docs.yml`). Pages serves it from
`/SlayTheSpirits/`, so the workflow builds with `DOCS_BASE=/SlayTheSpirits`; locally it's served from `/`. Keep links
between pages relative (`../rule-sets/`) and build image paths with `asset()` from `src/lib/data.ts`, so they work
under both.
