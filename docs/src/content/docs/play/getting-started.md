---
title: Getting started
description: Install Slay the Spirits and start a drinking-game run with friends.
---

Slay the Spirits is a multiplayer mod. Tavern content only appears in multiplayer runs, and the drinking game is
meant for a table of friends, in the same room or on a call.

## Install

1. Get the mod. Until there are release downloads, build it from source (see
   [Building from source](../../development/building/)). The build produces a `SlayTheSpirits` folder containing
   `SlayTheSpirits.dll`, `SlayTheSpirits.pck` and `SlayTheSpirits.json`.
2. Put that folder in the game's `mods` folder, for example
   `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\SlayTheSpirits`.
3. **Everyone at the table needs the same version.** The game won't start a multiplayer run when gameplay mods
   don't match.

Shortly after the game starts, its log (`%AppData%\SlayTheSpire2\logs\godot.log`) shows a health line such as
`SlayTheSpirits: 23/23 healthy`. Anything that isn't healthy is listed with the reason.

## Pick the rules

The **host** chooses the rules, and everyone else plays by them.

1. From the main menu, open **Settings → Spirits**.
2. Pick a **Rule Set** ([compare them](../rule-sets/)), or change individual rules under **Who Drinks When**.
3. Start a multiplayer run. The rules are sent to every player and locked until the run ends.

Players who join see the host's rules on the same tab, read-only. Changes made during a run apply from the next one.

## Get a drink

Have your drink in hand before the first fight, and some water. In the Standard rule set, everyone will:

- **Sip** for small things: elite fights, curses, big hits, using a potion.
- **Finish your drink** for bigger things: dying, winning the run.
- **Chug** a whole beer (or seltzer, or any can or bottle) when a boss shows up.
- Take a **Shot** if the run is lost.

[How it works](../how-it-works/) explains the drinks, Tipsy and the bar tab.
