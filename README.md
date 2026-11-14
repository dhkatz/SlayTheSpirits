# Slay the Spirits

A configurable multiplayer drinking game for **Slay the Spire 2**, built on **SpireKit**, a small,
test-first modding SDK that lives in this repo (and is designed to be split out later).

- Penalties for game moments: boss/elite fights, specific enemies, deaths, big hits, curses, potions, wasted energy, rest-site water breaks, winning and losing.
- **Tavern cards** (colorless, plus one per character) that make your friends drink: *Cheers!*, *Round on Me*, *Bottoms Up*, *Last Call* (6 cost).
- Drinks are **status cards** (Sip, Shot, Finish Your Drink…) that play automatically, with speech-bubble banter.
- **Tipsy**: a run-long buff that some cards scale with. A per-combat **bar tab** under each player shows what they owe.
- **Host-authoritative rules**: the host's profile is sent to everyone and locked for the run. Clients never use local settings.
- Presets (Casual / Standard / Degenerate / Sober), per-act caps that downgrade penalties, custom labels, share codes.

## Quick Start

Requirements: .NET SDK 9+, Slay the Spire 2 (v0.113.0+), Godot 4.5.1 mono (only for packing `.pck` files).

```powershell
copy local.props.example local.props   # then edit paths
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

Rules are edited in game under **Settings → Spirits** (main menu; read-only during a run or as a client).

### Local Multiplayer Testing

```powershell
./tools/test.ps1 -Build        # host + 1 client on this machine (LAN, Steam off, separate test profiles)
./tools/test.ps1 -Clients 3    # 4-player table
```

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
build/                       MSBuild targets (game refs, merge/pack/deploy) and the Godot PCK packer
docs/ARCHITECTURE.md         design, conventions, and how to extend
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) before adding features.
