# Slay the Spirits

A configurable multiplayer drinking game for **Slay the Spire 2**, built on **SpireKit**, a small,
test-first modding SDK that lives in this repo (and is designed to be split out later).

**Docs and wiki: https://dhkatz.github.io/SlayTheSpirits/** (how to play, rule sets, every card, relic and potion).

- Penalties for game moments: boss/elite fights, specific enemies, deaths, big hits, curses, potions, wasted energy, rest-site water breaks, winning and losing.
- **Tavern cards** (colorless, plus one per character) that make your friends drink: *Cheers!*, *Round on Me*, *Bottoms Up*, *Last Call*. Cards only ever hand out Sips; stronger ones cost more.
- Drinks are **status cards** (Sip, Shot, Finish Your Drink…) that play automatically, with speech-bubble banter.
- **Rest site tabs**: open a tab to pick 2 of 5 Tavern cards, or run one up to choose one of the **Tavern relics** (Hip Flask, Beer Belly, Tip Jar, Drinking Buddy, Pint Glass). You pay with a **Tavern curse** in your deck (Hiccups, Bar Fly, Hangover, Beer Goggles…) that makes you drink later. Settle up at the Tavern. Off, once per act, or at every rest site.
- **Tavern potions** that you drink for real: *Light Beer* (1 Sip), *Tallboy* (2 Sips), *Firewater* (3 Sips).
- **Tipsy**: a run-long buff that some cards scale with. A per-combat **bar tab** under each player shows what they owe.
- **Host-authoritative rules**: the host's profile is sent to everyone and locked for the run. Clients never use local settings.
- Presets (Casual / Standard / Degenerate / Sober), per-act caps that downgrade penalties, custom labels, share codes.

## Quick Start

Requirements: .NET SDK 9+, Slay the Spire 2 (v0.113.0+), Godot 4.5.1 mono (only for packing `.pck` files).

The build finds the game in your Steam libraries. If it's somewhere else, set `STS2_GAME_DIR`, or copy
`local.props.example` to `local.props` and set `Sts2GameDir`.

```powershell
dotnet test                            # engine + SDK core tests (no game needed)
dotnet build mod/src/SlayTheSpirits    # builds, merges, packs and deploys to <game>/mods/SlayTheSpirits
```

The game log (`%AppData%\SlayTheSpire2\logs`) shows a health line after startup, e.g.
`[SlayTheSpirits] SlayTheSpirits: 12/12 healthy`. Anything not healthy is listed with the reason.

Every player needs the same mod version (the game enforces this for gameplay mods).

### Running the game

```powershell
./tools/launch.ps1 -Build                  # rebuild (closes the game first), then launch on test profile 1
./tools/launch.ps1 -OpenSettings Spirits   # straight to the mod's Settings tab
./tools/launch.ps1 -Gallery                # SpireKit controls gallery (F8 toggles)
./tools/launch.ps1 -Steam                  # your real Steam profile
```

The scripts find the game the same way the build does; `-GameDir` overrides it.

Rules are edited in game under **Settings → Spirits** (main menu; read-only during a run or as a client).

### Local Multiplayer Testing

```powershell
./tools/multiplayer.ps1 -Build       # host + 1 client on this machine (LAN, Steam off, separate test profiles)
./tools/multiplayer.ps1 -Clients 3   # 4-player table
```

### Releasing

Bump `<Version>` in `mod/src/SlayTheSpirits/SlayTheSpirits.csproj`, merge, and publish a GitHub release tagged
`v<Version>`. CI downloads the game, builds the mod, attaches it to the release and uploads it to the Steam Workshop
([details](docs/src/content/docs/development/building.md#release-to-the-steam-workshop)).

## Layout

```
sdk/                         SpireKit, the reusable SDK (future standalone repo)
  src/SpireKit.Core/         engine-agnostic: events, determinism, storage, host-authority sync. Unit-tested.
  src/SpireKit/              game-bound: bootstrap, fail-soft patching, game events, net channels, content, UI
  tests/SpireKit.Core.Tests/
mod/
  src/SlayTheSpirits.Domain/ the drinking-game engine: rules, triggers, targeting, policies, tally. Unit-tested.
  src/SlayTheSpirits/        game-bound mod: wiring, cards, presentation, assets, manifest
  tests/SlayTheSpirits.Domain.Tests/
  tools/SlayTheSpirits.DocsExport/ exports the rules engine's data (presets, drinks) for the docs site
build/                       MSBuild targets (game refs, merge/pack/deploy) and the Godot PCK packer
docs/                        docs site and wiki (Astro Starlight), published to GitHub Pages
workshop/                    Steam Workshop description and cover image (uploaded by the Release workflow)
```

See the [architecture guide](docs/src/content/docs/development/architecture.md) before adding features
([on the site](https://dhkatz.github.io/SlayTheSpirits/development/architecture/)).

## Credits

The Tavern's music is a remix ("haunted" with slowdown, tape wobble and reverb, layered with wind, rumble and a ghost
moan) of CC0 (public domain) work from [OpenGameArt](https://opengameart.org):

- [Medieval: King's Feast](https://opengameart.org/content/medieval-kings-feast) by RandomMind
- [A Cold Wind](https://opengameart.org/content/a-cold-wind) by Umplix
- [Loopable Dungeon Ambience](https://opengameart.org/content/loopable-dungeon-ambience) by JaggedStone
- [Ghost](https://opengameart.org/content/ghost) by Ogrebane

The bartender's voice is the game's own merchant, pitched down at runtime; no game audio ships with the mod.
