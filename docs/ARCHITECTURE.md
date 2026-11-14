# Architecture

## Goals

1. **Survive game updates.** Early Access patches rename and reshape internals. Breakage should cost one
   feature, be reported precisely, and be fixable in one place.
2. **Test without the game.** Anything that isn't a direct call into the game is plain C# with unit tests.
3. **Reuse across mods.** SpireKit is a library other mods can embed. It has no knowledge of drinking.

## Layers

```
            ┌──────────────────────────────┐
            │ SlayTheSpirits (game-bound)   │  wiring, cards, toasts, assets
            └──────┬───────────────┬────────┘
                   │               │
   ┌───────────────▼──────┐  ┌─────▼─────────────────┐
   │ SlayTheSpirits.Domain │  │ SpireKit (game-bound)  │  bootstrap, patches, game events,
   │  rules engine (pure)  │  │                        │  net channels, content, UI
   └───────────────┬──────┘  └─────┬─────────────────┘
                   │               │
            ┌──────▼───────────────▼──────┐
            │ SpireKit.Core (pure)         │  events, determinism, storage, host-authority sync
            └──────────────────────────────┘
```

Rules:

- `*.Core` / `*.Domain` **must not** reference `sts2.dll`, Godot or Harmony. If you need a game concept
  there, define an abstraction and adapt it in the game-bound layer.
- Game-bound projects stay thin: translate game → abstraction → game. Logic goes down a layer, where it can be tested.

## Shipping: one DLL per mod

The game only loads `<id>.dll`, and it reflects over that DLL's types before any mod code runs. A separate
`SpireKit.dll` next to it would fail to resolve. So `build/Sts2.Mod.targets` **merges** every project
reference into the mod DLL with ILRepack. Consequences:

- Each mod pins its own SDK version. One mod's SDK update never breaks another mod.
- SDK statics are per-mod (`SpireRuntime.Current` is "this assembly's mod").
- Anything the game discovers by type **name** must be declared by the mod, not the SDK, or two mods
  embedding SpireKit would collide. Today that's just the hook listener (one line, see below).
  `SpireNetEnvelope` is safe: the game breaks wire-id ties by mod id, and handlers are registered per type.

## Game facts this design relies on (v0.113.0)

| Fact | Consequence |
|---|---|
| Mod initializers run **before** `ModelDb`/`LocManager` exist | `OnInitialize` registers things; `OnGameReady` (postfix on `OneTimeInitialization.ExecuteEssential`) uses them |
| Model ids = class name only (`SpiritsCheers` → `CARD.SPIRITS_CHEERS`) | Every model class starts with `ModelPrefix`; validated at boot |
| Every concrete `AbstractModel` in a mod DLL is auto-registered | SDK model base classes are `abstract` |
| Pools freeze at first use; `ModHelper.AddModelToPool` must run in the initializer | `ContentRegistry.AddCard<TPool,TCard>()` in `OnInitialize` |
| Every card source goes through `CardPoolModel.GetUnlockedCards` | One postfix implements per-run content toggles (`SetCardFilter`) |
| Hooks run on every peer in lockstep; `ModHelper.Subscribe*Hooks` is the official listener API | Game events need no networking; the engine is deterministic |
| Run-state listeners also receive combat hooks in combat (double dispatch) | Two listener clones with scopes; each hook is handled by exactly one |
| `AfterDamageReceived` skips lethal hits; winning/abandoning force-kills everyone | Normalized in `GameEventBridge` (`PlayerDamaged`, `PlayerDied`) |
| Net message types found by reflection, ≤256 total, 1-byte ids | One envelope type per mod and named channels on top (`NetHub`) |
| Host broadcasts only reach fully joined peers | Clients request + retry; the host replies directly to the requester |
| Run saves drop unknown JSON fields | Mod run data lives in sidecar files in the mod's storage folder |
| Only the host (or solo player) saves; the save is snapshotted (`RunManager.ToSave`) before entering each room and after each victory | `RunData` captures mod state at that same moment |
| A loaded multiplayer save reaches clients as a `SerializableRun` packet; mid-run rejoin isn't supported | `RunData` appends its bundle to that packet, so clients have it before the run starts |
| Peers must match gameplay mods exactly (`id-version`) | Rule sync can assume both sides run the same code |
| Settings tabs are hardcoded in `NSettingsTabManager._Ready` (`_tabs` dictionary); the same screen serves the main menu and the pause menu | A postfix adds mod tabs to `_tabs`, so tab switching, bumpers and fades work unchanged |
| Game widgets like `NSettingsTickbox`/`NPaginator` refuse to run as-is (`_Ready` throws unless subclassed) and find their parts by `%UniqueName` | `GameScenes.Graft` moves a scene's parts onto a mod subclass and re-owns them |
| Mod assemblies aren't registered with Godot's script system (only the game's is) | Mod node classes get a path-less script on first `new` and Godot learns their methods/signals from source-generated code; they can't be referenced from `.tscn` files |

## SpireKit (SDK)

| Area | Type | Notes |
|---|---|---|
| Boot | `SpireMod`, `SpireBootstrap.Boot<T>()`, `ModContext` | per-mod services; lifecycle `OnInitialize` → `OnGameReady` |
| Patching | `PatchRunner`, `[SpireFeature("id", Critical = …)]` | applies each `[HarmonyPatch]` class separately and records health; a critical failure unpatches the mod |
| Health | `FeatureHealthRegistry` | one log line at startup; query `IsEnabled(owner, feature)` |
| Events | `IEventBus` / `EventBus` | synchronous, handler failures isolated, subscribe to base types/interfaces |
| Game events | `RunStarted`, `CombatStarted`, `PlayerDied`, `PlayerDamaged`, … | from `SpireHookListener` via `GameEventBridge` |
| Session | `SessionTracker`, `SessionChanged`, `PeerConnected`, `ConnectedToHost` | Offline / Solo / Host / Client |
| Networking | `NetHub.Channel(name)` → `IMessageChannel` | bytes over `SpireNetEnvelope` |
| Shared settings | `HostAuthoritativeState<T>` + `AuthorityChannel<T>` | host owns, clients mirror, run lock, retries; pure and tested |
| Storage | `IFileStore`, `JsonDocumentFile<T>`, `VersionedJsonDocument<T>` | schema-versioned envelopes, migrations, corrupt-file backups; `defaults:` fills properties missing from older files (source-generated JSON would otherwise make them null/false) |
| Determinism | `DeterministicRandom`, `StableHash` | never draw from the game's RNG; derive a stream from the run seed |
| Content | `ContentRegistry`, `SpireCard`, `SpirePower`, `ModArt` | pool registration, run filter, name validation, raw-PNG card art and power icons (`res://<ModId>/images/{cards,powers}/<entry>.png`) |
| Async hooks | `IEventBus.SubscribeAsync` / `PublishAsync` | hook-driven game events are awaited by the game, so handlers can issue game commands (apply power, gain block) in lockstep |
| Localization | `LocInjector`, `SpireTips.Text(key, title, body, icon)` | runtime entries that survive language changes; keyword tooltips with computed text |
| UI | `GameVfx.SpeechBubble`, `GameVfx.CombatBanner`, `ToastOverlay`, `GameFonts`, `GameStyle` | native speech bubbles and turn-style banners (queued, never overlapping); out-of-combat toasts; the game's palette, font and outlined text for code-built UI |
| Custom controls | `SpireKit.UI.Controls` (`NumberStepper`, `ChoiceStepper`, `ControlsGallery`) | real Godot node classes in SDK/mod code (see "Write a custom control"); a startup probe reports feature `ui.controls` |
| Settings tabs | `ModContext.SettingsTabs.Add(title, page => …)`, `SettingsPage`, `SettingsToggle`, `SettingsChoice` | a tab on the game's Settings screen (main menu and pause menu) built from the game's own tab, tickbox, picker, row and divider; page rebuilt on each show; `handle.Refresh()` for outside changes |
| Game scenes | `GameScenes.Graft`, `GameScenes.CopyProperties`, `SceneDump` | subclass a game widget whose parts live in a `.tscn`; dump any scene's node tree (`launch.ps1 -DumpScenes`) |
| Sharing | `ShareCode` | `PREFIX:base64url(deflate(json))` |
| Run data | `ModContext.RunData.Register(slot, capture, restore)` | per-run state that survives save/quit: captured when the game snapshots the run, stored beside its save (`rundata/profile<N>/current_run[_mp].json`, stamped with seed + start time), sent to clients inside the loaded save, restored before `RunStarted` |

## The drinking game

```
game hook ─▶ SpireKit event ─▶ GameTriggers ─▶ TriggerOccurrence ─▶ PenaltyEngine ─▶ Announcer ─▶ toast
                                   (pure)                              │
card OnPlay ─────────────────────────────────▶ PenaltyRequest ─────────┘
```

`PenaltyEngine.Handle`: rule lookup (`RuleResolver`) → targets (`TargetResolver`, seeded RNG) → policies
(`RedirectPolicy` → `ImmunityPolicy` → `CapPolicy`) → Tipsy (`TipsyWeights`, configurable per kind) →
record in `RunPenaltyState` and `RunTally`.

**Tipsy** is the run-long drunkenness counter, shown as `SpiritsTipsyPower` under each player. Powers are
wiped at combat end, so `TipsyBuff` re-applies each player's total on `CombatStarted` and adds deltas as
penalties land. It's real combat state (covered by the game's desync checksum), which is fine because the
engine is deterministic; cards read it via `SpiritsCard.OwnerTipsy` (always after `await PenalizeAsync`).

**Drink cards.** Cards never hand out drinks directly; they create drink *status cards* (`SpiritsSip`,
`SpiritsShot`, `SpiritsFinishYourDrink`…, in the game's status pool) owned by the drinker:
- `GiveDrinkAsync(ctx, player, kind)`: the giver says a line, then the card auto-plays. With
  `RuleSet.ConfirmDrinks` (off by default) another player first confirms on the game's choose-a-card screen. The engine runs when the drink
  card plays, so caps, immunity and redirects apply then. Call it last in `OnPlay`: after another player's
  choice the rest of the play runs on that player's queue (same as the base game's Tutor).
- `GiveEveryoneAsync(ctx, kind)`: all other players confirm simultaneously; drinks then play in seat order,
  so the engine sees the same sequence on every peer regardless of who answers first.
- `ShuffleDrinksAsync(player, kind, n, pile)`: drink cards in a draw/discard pile auto-play when drawn.

**Bar tab.** During combat each player shows one counter per drink kind they've been handed this
combat (`SpiritsTabPower`: "Shots 1", "Sips 2"), counted after caps, immunity and redirects (`BarTab`).
It replaces confirmation popups as the reminder: the table can see at a glance who still owes a drink.
Titles follow the host's labels (`TabText` injects them whenever the rules change).

**Presentation** (`Announcer`, `Speech`, `Banter`): the giver says a line ("This one's on me!"); the drinker
reacts ("Here goes nothing!") when their drink plays. In combat, a speech bubble over each drinker plus a turn-style banner when
everyone drinks (delayed past the game's own combat-start banners); elsewhere, a banner-styled toast.
**Keyword tooltips** (`SpiritsTips`, text from `Glossary`): cards declare `PenaltyKeywords` and get one tip
per penalty plus a Tipsy tip; the game doesn't cascade tooltips, so cards list them all.

**Saving.** The engine's whole state (`RunPenaltyState`: Tipsy and tally, act caps, RNG position, pending
redirects) is a `RunData` slot (`RunPenaltyStateCodec`, schema-versioned). A resumed run continues exactly
where the save left off, on every peer.

Rules (`RuleSet`) live in the host's profile (`profile.json` in the mod storage folder), sync through
`RulesService` (host-authoritative), lock at run start, and are kept in `runs/<seed>.json` so a resumed
run uses the rules it started with.

## How to…

**Add a trigger.** Add a `TriggerDefinition` to `BuiltInTriggers`, give it defaults in `Presets`, map the
game event in `GameTriggers.From`, and add a display name in `Announcer`. If the game event doesn't exist
yet, add a record to `SpireKit/GameEvents/GameEvents.cs`, an override to `SpireHookListener` (check which
listener list the game dispatches the hook to), and a handler to `GameEventBridge`. Test the mapping in
`RuleResolverTests`.

**Add a card.** Create a `SpiritsCard` subclass whose class name starts with `Spirits`. Register it in
`SlayTheSpiritsMod.OnInitialize` (`AddCard<ColorlessCardPool, …>()`, or a character pool plus
`ISpiritsCharacterCard`). Add `<ENTRY>.title`/`.description` to `Assets/localization/eng/cards.json`, and
optionally art at `Assets/images/cards/<entry>.png`. Hand out penalties with `await PenalizeAsync(...)`
and list them in `PenaltyKeywords` for tooltips; set `ScalesWithTipsy` if the effect reads `OwnerTipsy`.

**Add a power.** Subclass `SpirePower` (prefixed name), implement `Type`/`StackType`, add
`<ENTRY>.title`/`.description` (and optional `.smartDescription`) to `localization/eng/powers.json`, and an
icon at `Assets/images/powers/<entry>.png` (`tools/icons.py` makes placeholders).

**Add a policy** (e.g. a "Bartender" relic doubling sips). Implement `IPenaltyPolicy`, keep any state in
`RunPenaltyState`, insert it into the pipeline, and test it in `PenaltyEngineTests`.

**Add synced state.** `new AuthorityChannel<T>(context.Net.Channel("name"), encode, decode)` +
`HostAuthoritativeState<T>`, mirroring `RulesService`.

**Keep state across save/quit.** `context.RunData.Register("name", capture, restore)` in `OnInitialize`.
`capture` returns text (or null) whenever the game snapshots the run; `restore` gets it back (or null) when a
saved run is set up, before `RunStarted`. Keep it deterministic: every peer restores the same text.

**Write a custom control.** Any project importing `Sts2.Game.targets` runs Godot's C# source generators. Declare a
`partial` class deriving from a Godot node (`public partial class MyPicker : HBoxContainer`), override `_Ready`,
`_GuiInput`, `_Draw` etc., and declare signals with `[Signal] public delegate void ChangedEventHandler(int v);`.
Create it with `new` (never from a `.tscn`), build its parts in the constructor, and add them as internal
children (`AddChild(part, @internal: InternalMode.Front)`) so callers' `GetChildren()` and `Duplicate()` ignore
them. Style text with `GameStyle`. Handle `MegaInput.left`/`right`/`confirm` in `_GuiInput` so controllers work.

**Add a Settings tab.** `context.SettingsTabs.Add("Title", page => { page.Header(...); page.Toggle(...); ... })`.
The callback runs every time the tab is shown, so read current state and write changes straight back. Show
read-only state with `editable: false`. Call `page.Refresh()` when an edit changes which rows exist, and keep the
returned handle to `Refresh()` when state changes elsewhere. Check it with `tools/launch.ps1 -OpenSettings Title
-Scroll 20`.

**Add a setting to the RuleSet.** Add the property with its default, then a row in `RulesSettingsTab`. Files and share codes
without it read as the default (`defaults:` on `RuleSetCodec.Format`).

**Change the RuleSet shape** (rename, change meaning). Bump `RuleSetCodec.SchemaVersion` and register a
`JsonMigration` from the previous version. Old profiles and share codes keep working.

## Known gaps (next milestones)

- Locked rules still use their own `runs/<seed>.json` sidecar and reach clients over the rules channel; they
  could move to `RunData` too, so clients get them in the same packet as the save.
- **Settings tab covers the basics** (rule set, switches, every trigger, share codes). Still `profile.json` only:
  drink caps, Tipsy weights, drink names, per-enemy rules, disabling single cards. No lobby "Rules" panel yet
  (clients see the host's rules under Settings → Spirits).
- Relic (Beer Stein) and potion (Mystery Brew), real card art, sounds.
- Combat banners reuse the game's "Player Turn" banner scene, so they also play its turn sound.
