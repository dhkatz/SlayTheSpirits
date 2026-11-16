---
title: Building from Source
description: Build the mod, run the tests, launch the game and work on these docs.
---

## Requirements

- .NET SDK 9 or later
- Slay the Spire 2 (v0.113.0 or later)
- Godot 4.5.1 mono, only for packing `.pck` files
- Node.js 22 or later, only for these docs

## Build

```powershell
dotnet test                            # rules engine and SDK tests (no game needed)
dotnet build mod/src/SlayTheSpirits    # builds, merges, packs and installs to <game>/mods/SlayTheSpirits
```

The build skips installing (with a warning) while the game is running. Close it and build again.

The build finds the game itself (`build/Sts2.GameDir.props`). First match wins:

1. `-p:Sts2GameDir=...` on the command line
2. the `STS2_GAME_DIR` environment variable
3. `Sts2GameDir` in `local.props` (copy `local.props.example`; it's git-ignored)
4. your Steam libraries

`local.props` is only needed for overrides, such as `DeployMods` or `GodotExe`.

## Run the Game

```powershell
./tools/launch.ps1 -Build                  # rebuild (closes the game first), then launch on test profile 1
./tools/launch.ps1 -OpenSettings Spirits   # straight to the mod's Settings tab
./tools/launch.ps1 -Gallery                # SpireKit controls gallery (F8 toggles)
./tools/launch.ps1 -Steam                  # your real Steam profile
```

The scripts ask the build where the game is; `-GameDir` overrides it.

For multiplayer on one machine:

```powershell
./tools/multiplayer.ps1 -Build       # host and 1 client (LAN, Steam off, separate test profiles)
./tools/multiplayer.ps1 -Clients 3   # a 4-player table
```

## Release to the Steam Workshop

1. Bump `<Version>` in `mod/src/SlayTheSpirits/SlayTheSpirits.csproj` and merge it. Every player needs the same
   version.
2. Publish a GitHub release tagged `v<Version>` (for example `gh release create v0.2.0 --generate-notes`). Its notes
   become the Workshop change note.

That runs the [Release workflow](https://github.com/dhkatz/SlayTheSpirits/blob/main/.github/workflows/release.yml) on a
Windows runner:

1. Downloads the game's `public-beta` branch with [dhkatz/steam-download](https://github.com/dhkatz/steam-download)
   (the build compiles against it, and packs the `.pck` with its executable). SpireKit uses APIs that are only on
   the beta for now, so players need it too; when they reach the default branch, drop `beta:` from the workflow.
2. Runs the tests and builds the mod in Release.
3. Checks the mod's version matches the tag.
4. Attaches `SlayTheSpirits-<Version>.zip` (a `SlayTheSpirits/` folder, ready for `<game>/mods`) to the release.
5. Uploads to the Workshop with [gmod-workshop/workshop-upload](https://github.com/gmod-workshop/workshop-upload),
   using the description and cover image in `workshop/`.

**Actions → Release → Run workflow** on a `v*` tag runs it again; untick *upload* to only download, test and build.

The workflow runs in the `steam-workshop` environment, which only allows `v*` tags. It holds the secrets
`STEAM_USERNAME` and `STEAM_VDF` (base64 of SteamCMD's `config/config.vdf` after logging in once) and the variable
`WORKSHOP_ID`. The first release creates a private item; set `WORKSHOP_ID` to its ID afterwards so later releases
update it. When the Steam login expires, the workflow fails at login: log in with SteamCMD again and update
`STEAM_VDF`.

The game is never cached or kept as an artifact: this repository is public, and pull requests from forks can restore
its caches.

Steam only records a change note when the files changed, so re-running a release adds no new entry.

## Repository Layout

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
tools/                         launch and multiplayer scripts, placeholder icon generator
workshop/                      Steam Workshop description and cover image
```

## Work on the Docs

```powershell
cd docs
npm install
npm run dev      # http://localhost:4321/
```

The [cards](../../wiki/cards/), [relics](../../wiki/relics/), [potions](../../wiki/potions/),
[keywords](../../wiki/keywords/), [drink ladder](../../play/how-it-works/) and [rule set table](../../play/rule-sets/) are generated from the mod's
source each time the site builds (`npm run generate`):

- `mod/tools/SlayTheSpirits.DocsExport` runs the real rules engine and writes the presets, ladder and trigger
  names to `src/generated/rules.json`.
- `scripts/extract-content.mjs` reads the card, relic and potion classes and their localization, writes
  `src/generated/content.json`, and copies their icons into `public/generated/`. If it can't read something (a new
  kind of upgrade, a missing translation, a pool it doesn't know), the build fails and says which model to fix.

### The Wiki

Every registered card, relic and potion, and each of the mod's keywords (the drinks, Tipsy, its powers), gets a page
at `/wiki/<kind>/<slug>/`, where the slug comes from its English title. Nothing about a single entry is written by
hand:

| File | What it decides |
|---|---|
| `src/lib/taxonomy.json` | Names and colors of pools, where each pool's content is found, rarity and type order, target names, where keywords go on a card, how each pool and rarity is drawn. Add a new pool here. |
| `src/lib/wiki.ts` | Each kind's default order, opening sentence, infobox, navigation groups, and compendium filters and sorts. Every rarity, type, pool and cost it names links to the compendium filtered to it. |
| `src/lib/compendium.ts` | Turns those filters and sorts into controls and data attributes, for any list. |
| `src/lib/refs.mjs` | Inline references: an entry's icon and name, linked, with a hover box. Everything that mentions an entry goes through it. |
| `src/components/wiki/` | `CardFace` draws a card; `WikiCompendium` and `Compendium` are the filterable lists; `WikiEntry` is an entry's page (text, `Infobox`, `Navbox`); `WikiHub` is the wiki's front page; `Tooltip` is a hover box. |
| `src/pages/wiki/[kind]/[slug].astro` | Builds a page per entry. |
| `src/pages/tips/[kind]/[slug].astro` | Builds each hover box as a piece of HTML, fetched when a reference is hovered (`src/scripts/tooltips.ts`). |
| `src/routeData.ts` | Marks the sidebar link an entry's page belongs under (Cards, on a card's page) as the current one. |
| `src/styles/wiki.css` | All of the wiki's styles. Anything inside `.sts-wiki-page` makes its page full width. |
| `game-assets/` | What the docs need from the game, extracted and committed (below). |
| `scripts/card-art.mjs` | Tints the game's card pieces for each pool and rarity, with the same color maths as the game's shader. |

#### Mentioning Cards, Relics, Potions and Keywords

In any page or note, write a directive and it becomes the entry's icon and name, linked to its page, with a hover box:

```md
:card[Down the Hatch] gets stronger with :kw[Tipsy], like the :relic[Hip Flask].
The :char[Ironclad] has it. :potion[Tallboy] makes you :kw[Sip] twice.
:kw[Finish Your Drink]{label="finishing your drink"} shows other words; {noicon} drops the icon.
```

A name that doesn't exist fails the build. In components, use `<Ref kind="card" id="down-the-hatch" />`. The
sentences in `taxonomy.json` that say where things are found take the same shorthand, plus `[links](play/rest-sites/)`
and `**bold**`. The mod's
keywords link to their page here; the game's (Strength, Block, Exhaust...) and the characters link to the community
wiki, when it has a page for them.

Gold words in a card, relic or potion's text are matched to keywords automatically, however they're worded ("Sips",
"finishes their drink"), and each entry's keyword list is read from its tooltips in the code, in the game's order.

#### What Comes from the Game

Cards are drawn the way the game draws them: its frame, portrait border, title banner, type plaque and energy orb,
stacked at the positions in its card scene, with the text on top in Kreon. The game's keywords use the game's own
text and icons. The build can't read the game, so all of this is extracted once into `game-assets/` and committed:

```powershell
npm run extract-game-art   # game folder from the build; Godot from GODOT_EXE or GodotExe in local.props
```

Run it after a game update, after adding a pool or rarity to `taxonomy.json`, or when the build says a keyword is
missing (a model started using a game keyword it didn't before). It also asks the community wiki which pages exist,
so links never point at a missing page; `-- --offline` skips that and keeps the links from last time.

#### Notes

To add notes to an entry (tips, combos, trivia), create `src/content/notes/<kind>/<slug>.md`, for example
`src/content/notes/cards/down-the-hatch.md`. Each entry page's "Edit page" link opens that file on GitHub, or creates
it if it doesn't exist yet. A note whose entry was renamed or removed fails the build.

Pushing to `main` publishes the site to GitHub Pages (`.github/workflows/docs.yml`). Pages serves it from
`/SlayTheSpirits/`, so the workflow builds with `DOCS_BASE=/SlayTheSpirits`; locally it's served from `/`. Keep links
between pages relative (`../rule-sets/`) and build image paths with `asset()` from `src/lib/data.ts`, so they work
under both.
