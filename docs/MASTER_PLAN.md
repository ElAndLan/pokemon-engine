# Creature Game Maker — Master Project Plan & Architecture

Version 1.0 — 2026-07-06
Status: Authoritative planning document. Supersedes PROJECT_BIBLE.md where they conflict.

---

## 1. Executive Summary

Creature Game Maker (CGM) is two products in one repository:

1. **The Creator** — a Windows desktop application where a user visually builds a monster-catching RPG: imports sprite sheets, slices them, authors tilesets/maps/creatures/moves/items/trainers/encounters, validates the project, playtests it, and exports it.
2. **The Runtime** — a custom, lightweight 2D game engine that ships as a prebuilt template executable. Export = template exe + the project's compiled data/asset pack. The runtime contains all genre rules; the project pack contains all content.

The gameplay target is a faithful recreation of the **Generation 3–4 Pokemon feel**, which concretely means:

- **Grid-based movement**: the player occupies exactly one tile; movement is tile-to-tile with a smooth interpolated walk animation (~0.25s/tile walking, faster running/biking). Input is buffered; turning in place before stepping (tap = face, hold = step). NPCs move on the same grid with wander/patrol/look-around behaviors.
- **Tile semantics**: passable/blocked per tile plus directional ledges (one-way hops), tall grass (encounter tiles), water (surf-gated), warps (doors, cave mouths, stairs), counters (talk-across), and scripted event tiles.
- **Battles**: menu-driven turn-based battles (Fight/Bag/Creature/Run) with the authentic Gen 3/4 damage pipeline: level-scaled base damage, physical/special split (Gen 4 style, per-move damage class), STAB ×1.5, full 18-type (data-driven, user-editable) effectiveness chart with ×0/¼/½/1/2/4 multipliers, stat stages −6..+6, accuracy/evasion stages, critical hits (stage-based 1/16 baseline, ×2), random 85–100% damage roll, priority brackets, speed ties resolved randomly, burn halving physical attack, paralysis speed cut and 25% full-para, sleep turn counters, freeze thaw chance, poison/toxic ramp, weather (rain/sun/sand/hail) effects on damage and residuals.
- **Stats**: species base stats + IVs (0–31) + EVs (0–252, 510 cap) + nature (±10%) using the Gen 3+ stat formulas; experience via growth-rate curves (fast/medium-fast/medium-slow/slow/erratic/fluctuating); EXP formula with trainer ×1.5 bonus.
- **Moves**: the full PokeAPI move dataset is available in `docs/pokeapi-results/move` and the engine's effect system must be expressive enough to represent every effect archetype in it (damage, multi-hit, ailment %, stat changes, drain, recoil, flinch, charge/semi-invulnerable two-turn moves, protection, hazards, weather, healing, fixed damage, OHKO, counter-style, delayed effects, forced switching, etc.). Effects are data-driven compositions of primitive effect operations, not hardcoded per-move.
- **Capture**: Gen 3/4 formula — `a = ((3·maxHP − 2·curHP) · rate · ballBonus / (3·maxHP)) · statusBonus`, then four shake checks against `b = 65536 / (255/a)^0.25` (approximation acceptable). Caught creatures go to party, or automatically to the active storage box when the party is full.
- **Evolution**: level-up, item use, trade (simulated via an in-editor "link" flag or NPC trade), happiness, time-of-day, held-item, known-move, location, and party-condition triggers — all data-driven from the PokeAPI evolution-chain structure.
- **Forms / Mega Evolution / Gigantamax**: modeled as a general **forms system**. A form is an alternate stat/type/ability/sprite block on a species with an activation rule: `permanent`, `battle_temporary` (Mega: requires held key item + trainer key item, once per battle, reverts after), `battle_timed` (Gmax-style: activates for N turns, altered move mapping, boosted HP multiplier), or `condition` (weather/held-item forms).
- **World loop**: heal at creature centers (full restore + reusable respawn point), shop at marts, fight gym-style trainer gauntlets with badge flags gating progression, story flags driving NPC dialogue/visibility, item pickups, PCs for storage access.

Non-negotiable framing: this is a **Pokemon-inspired maker**. It never ships copyrighted assets. PokeAPI JSON in `docs/pokeapi-results` is a development-time data source (mechanics, structures, move data) and an optional user-side import; official art/audio/names are never bundled in releases.

---

## 2. Scope Definition

### MVP (prove the loop end-to-end)
Create project → import & slice one tileset + one character sheet + two creature battle sprites → build one map with collision, grass, and a warp to a second map → place player start + one NPC with dialogue → define 2 types, 4 moves, 2 species, 1 encounter table, potion + capture ball → playtest in-editor → export .exe → walk, encounter, battle (damage/type chart/status:none), capture, faint/blackout, save/load.

### Vertical slice (the "is this real" milestone)
Everything in MVP plus: full battle mechanics (stat stages, statuses, crits, priority, accuracy), trainer battles with pre-battle line-of-sight approach + dialogue, party of 6 + storage boxes UI, inventory pockets, marts/money, evolution (level + item + happiness + time), experience/EV/IV/natures, day/night clock, gym flag gating one door, audio (BGM + SFX), 10 species / 30 moves / 3 maps authored as a demo game shipped with the app.

### Long-term
Full move-effect coverage of the PokeAPI dataset; Mega/Gmax forms; abilities; held items in battle; weather; breeding/egg groups (optional); surf/bike/field moves; double battles; a scripting layer (visual event graph) for cutscenes; animated tiles and battle move animations; controller support; project templates; localization; macOS/Linux export (stretch).

### Explicit non-goals
No multiplayer/netcode/real trading, no 3D, no general-purpose engine ambitions, no in-app asset marketplace, no mobile/console export, no user-facing programming language (event graph only), no shipping official Pokemon content, no plugin API before 1.0.

---

## 3. Recommended Tech Stack

| Stack | Pros | Cons | Verdict |
|---|---|---|---|
| **C++ engine + Qt/ImGui creator** | Maximum control/perf; SDL/ImGui battle-tested | Slowest iteration; memory-safety bug tax; Qt licensing friction; CMake toolchain drag; AI-generated C++ needs the most review | Rejected — perf headroom is wasted on a tile RPG |
| **Rust engine + Tauri/egui creator** | Memory safety catches AI code drift at compile time; great packaging (single static exe); wgpu/winit mature | egui immature for a heavy docking editor (or Tauri = webview + IPC schema drift); borrow checker slows exploratory gameplay code; smaller talent/example pool for *editor* apps | Strong runner-up |
| **C#/.NET 10 engine + Avalonia creator** | One language for creator, runtime, and shared data library — schemas defined once; Avalonia is a mature MVVM desktop UI (docking, tree views, canvases); Silk.NET gives raw SDL/OpenGL/OpenAL bindings without an engine; `dotnet publish` self-contained single-file exe solves export cleanly; best-in-class AI-assisted ergonomics (huge training corpus, strong typing, fast compile); NativeAOT available if startup matters | GC (irrelevant at this scale with pooling); runtime exe ~60–90 MB self-contained (acceptable); need discipline to not drift toward MonoGame patterns | **Chosen** |
| **TypeScript creator (Electron) + native runtime** | Fastest editor UI development | Two languages ⇒ duplicated schemas, IPC drift, Electron weight; validation logic written twice | Rejected |

**Final recommendation: C#/.NET 10 (LTS), three-project solution:**
- `Cgm.Core` — shared class library: all schemas, IDs, validation, battle math, save format. Zero UI/graphics dependencies. This is the single source of truth.
- `Cgm.Creator` — Avalonia 11 MVVM desktop app.
- `Cgm.Runtime` — the game engine: **Silk.NET.Windowing + Silk.NET.OpenGL** (or Silk.NET.SDL) for window/input/context, custom sprite-batch renderer, **Silk.NET.OpenAL** (or a thin miniaudio binding) for audio, **StbImageSharp** for PNG decode, **System.Text.Json** (+ source generators) for data, custom binary `.cgmpack` for shipped assets.

The battle engine and all game rules live in `Cgm.Core`, so the Creator's playtest, the Runtime, and the test suite all execute the *same* code.

---

## 4. System Architecture

```
┌────────────────────────── Cgm.Creator (Avalonia) ──────────────────────────┐
│ Dashboard │ Asset Browser │ Slicer │ Tileset │ Map │ Data Editors │ Export │
│           ViewModels ──► ProjectService (load/save/undo/dirty)             │
└──────────────────────────────┬─────────────────────────────────────────────┘
                               │ reads/writes
                    ┌──────────▼──────────┐
                    │   Project on disk    │  project.cgmproj + /data/*.json
                    │  (JSON, git-friendly)│  + /assets/**  (source PNG/OGG)
                    └──────────┬──────────┘
                               │ Cgm.Core: schemas • IDs • validation • battle math
                               │
              ┌────────────────▼────────────────┐
              │        Export Compiler          │  validate → compile → pack
              └────────────────┬────────────────┘
                               ▼
        Runtime.exe (prebuilt template) + game.cgmpack + config.json
                               │
┌──────────────────────────────▼────────────────────────────────────────────┐
│ Cgm.Runtime: Window/GL │ AssetDb │ SceneStack │ Overworld │ Battle │ Menus │
│ Fixed-step sim ◄─ Input map      Renderer (sprite batch)   Audio    Saves  │
└────────────────────────────────────────────────────────────────────────────┘
```

- **Project format**: a folder. `project.cgmproj` (settings + manifest), `/data/<category>/<id>.json` one file per entity (git-diffable), `/assets/` raw sources, `/derived/` slicer metadata. Every file carries `schemaVersion` for migration.
- **Validation system**: rule-based validator in Core (`IValidationRule` over a loaded `Project`), producing `ValidationIssue {severity, entityId, message, fixHint}`. Runs live in editors (debounced), in the validation dashboard, and as a hard gate before export. Categories: broken ID refs, unreachable maps, encounter tables summing ≠100, species without moves at obtainable levels, warps without targets, missing sprites, party-size violations, etc.
- **Runtime scenes**: a scene stack (`Boot → Title → Overworld`, push `Battle`, push `Menu/Bag/Party/Storage/Dialogue`). Overworld and Battle never mutate each other directly; they communicate via a `BattleRequest`/`BattleResult` message pair.

---

## 5. Creator Application Architecture

Shell: left nav (project tree by category), central document tabs, right inspector panel, bottom validation strip. MVVM; every edit goes through an undoable command stack per document.

| Screen | Purpose / data | Key controls | Validation | MVP → Later |
|---|---|---|---|---|
| **Project dashboard** | settings, recent projects, stats | new/open, name, tile size (16/32), start map/pos, starter party | start map exists | MVP: settings form → Later: templates, git status |
| **Asset browser** | all imported assets | grid/thumbnail view, import PNG/OGG, tags, usage search | orphaned/missing files | MVP: list+import → Later: bulk ops, hot-reload watch |
| **Sprite sheet slicer** | sheet → named sprites | auto-suggest grid, zoom canvas, draggable grid lines, cell select, name/tag cells | overlapping/empty slices | MVP: uniform grid + manual offset → Later: irregular rects, auto transparent-region detection UI |
| **Tileset editor** | tiles from slices | per-tile flags: solid, grass, water, ledge-dir, counter, animated frames, terrain tag | untextured tiles | MVP: passability+grass → Later: autotiles, animation |
| **Object editor** | multi-tile props (trees, PCs, signs) | footprint, collision mask, interaction script hook | missing sprite | MVP: static+solid → Later: animated, state-driven (cut tree) |
| **Map editor** | see §10 | — | see §10 | — |
| **Encounter editor** | encounter tables | see §11 | weights, level ranges, species refs | — |
| **Creature editor** | species | base stats sliders, types, growth rate, learnset grid (level→move), evolutions, forms, catch rate, EV yield, sprites (front/back/icon), cry | stat bounds, learnset refs, evo cycles | MVP: stats/types/learnset/1 evo → Later: forms/abilities/egg data |
| **Move editor** | moves | power/accuracy/PP/priority/type/class, effect composer (list of effect ops + params), targeting | effect param ranges | MVP: damage+ailment% → Later: full effect-op palette |
| **Type chart editor** | type matrix | editable N×N grid with 0/½/1/2 cells, add/remove types | orphan types | MVP as-is |
| **Item editor** | items | pocket, price, battle/field usability, effect composer, key-item flag | effect refs | MVP: heal/ball/key → Later: held items, TMs, evolution items |
| **Trainer editor** | trainers | class, sprite, AI level, party builder (species/level/moves/items), reward money, pre/post dialogue, sight range | party 1–6, refs | MVP: party+dialogue → Later: AI tuning, rematch flags |
| **Inventory editor** | pocket definitions, starting bag | pocket list/order/icons, start items | pocket refs | MVP: fixed 4 pockets → Later: custom pockets |
| **Storage editor** | box count/size/names/wallpaper | numeric + names | ≥1 box | MVP: defaults only |
| **Validation dashboard** | all issues | filter by severity/category, click-to-navigate | — | MVP: list → Later: quick-fix actions |
| **Playtest/Export** | run & ship | Play (spawn runtime in dev mode on current project), Play-from-map, export config (name, icon, debug/release), progress log | export gate = zero errors | MVP: playtest full project → Later: play-from-cursor, icon/branding |

---

## 6. Custom Engine Architecture (Cgm.Runtime)

- **App loop**: fixed-timestep simulation at 60 Hz with accumulator; render at vsync with interpolation of visual positions only. Deterministic sim (single seeded RNG stream per battle, one for overworld) — this is what makes battle golden-tests possible.
- **Object model**: **no ECS.** Plain typed objects. Overworld = `MapRuntime { Tilemap, List<MapEntity> }`; `MapEntity` is a small class hierarchy (Player, Npc, Pickup, Warp, Trigger) sharing `GridPosition`, `Facing`, `SpriteAnimator`, `Mover`. A creature RPG has <200 entities per map; ECS is over-engineering.
- **Movement/collision**: grid-based. `Mover` owns tile→tile interpolation; collision is a query against tile flags + entity occupancy map + ledge direction rules. No physics engine, no floating-point collision.
- **Tilemap rendering**: per-layer chunked vertex buffers (rebuilt on load, static thereafter), one texture-atlas draw per layer per frame. Layers: ground, decoration-below, entities (Y-sorted), decoration-above.
- **Renderer**: OpenGL 3.3 core. One sprite-batch class (texture-atlas, 2D ortho, integer-scaled virtual resolution — default 240×160 logical ×N, letterboxed). Nearest-neighbor sampling. That is the entire rendering feature set; no lighting, no shader graph.
- **Camera**: follows player with map-edge clamping; pixel-snapped.
- **Sprite animation**: `AnimationClip { frames[], durations[], loop }` + `SpriteAnimator` state machine (idle/walk × 4 facings for characters).
- **Input**: action-mapped (`Move{U,D,L,R}, Confirm, Cancel, Menu, Run`) with rebindable keys, default WASD/arrows + Z/X/Enter/Esc; input buffered one action for grid movement and menu feel.
- **UI**: immediate-mode-ish internal UI kit rendered through the same sprite batch: 9-slice panels, bitmap font text with typewriter reveal, menu cursor, HP bars with animated drain. All game menus (dialogue, bag, party, storage, battle) build on these five primitives.
- **Audio**: streaming OGG BGM with crossfade + one-shot SFX/cries via OpenAL; music defined per map and per battle type.
- **Data loading**: reads `game.cgmpack` (binary: header, string table, zstd-compressed asset blobs, JSON data section) into an in-memory `GameDb` of immutable definition objects keyed by namespaced ID. Dev mode reads the raw project folder instead (enables editor playtest + hot data reload).
- **Save/load**: versioned JSON (optionally gzip) in `%APPDATA%/<GameName>/saves/`: player pos/facing/map, party (full creature instances), boxes, bag, money, flags, clock, RNG seeds, playtime. Save via pause menu; single slot + auto-backup of previous save (MVP), 3 slots later.
- **Debug overlays** (dev builds, F-keys): collision/encounter tile tint, entity IDs, flag inspector, battle log console, free-warp map list, "give item/creature" console.
- **Battle integration**: overworld pushes `BattleScene(BattleRequest)`; battle scene owns a `BattleController` from Core, renders its state, submits player actions, animates emitted `BattleEvent`s; on end, pops with `BattleResult` (win/loss/capture/run + party mutations already applied to the shared `PlayerState`). Loss ⇒ blackout: heal, warp to last center, halve money.

---

## 7. Data Model

All references are **namespaced string IDs**: `species:leafcub`, `move:ember`, `type:fire`, `item:potion`, `map:route_001`, `object:small_tree`, `encounter:route_001_grass`, `trainer:gym_leader_flora`, `flag:story.badge_1`, `anim:player_walk_down`, `sheet:overworld_tiles`, `tileset:exterior`. IDs are immutable after creation (rename = display-name change only). Every JSON file: `{ "schemaVersion": 1, "id": "...", ... }`.

Key schemas (abridged; full field lists to be frozen in DATA_SCHEMA.md during Phase 2):

- **project**: name, engineVersion, tileSize, startMap/pos, starterConfig, playerSprites, typeChartRef, pockets[], boxConfig, clockConfig.
- **spritesheet**: source path, sliceMode (grid|rects), cellW/H, offset, spacing, cells[] `{index|rect, spriteId}`.
- **sprite**: sheetRef, rect, pivot, tags[].
- **animation**: frames[] `{spriteRef, ms}`, loop.
- **tile**: spriteRef|animRef, flags `{solid, grass, water, ledge:dir?, counter, terrainTag, encounterZoneRef?}`.
- **map**: width/height, tilesetRefs[], layers[] (ground/decoBelow/decoAbove: tile index arrays; collision override layer; encounter-zone paint layer), entities[] (npc/warp/pickup/trigger/sign placements with per-instance params), bgm, environment (indoor/outdoor for time tint).
- **encounter table**: method (`grass|surf|fishing|cave|tile|interact`), slots[] `{speciesRef, weight, minLvl, maxLvl, timeOfDay?, requiredFlag?}`, baseRate (steps-based probability).
- **species**: name, types[1–2], baseStats{hp,atk,def,spa,spd,spe}, catchRate, baseExp, growthRate, evYield, genderRatio, baseHappiness, learnset[] `{level, moveRef}` + machine/tutor lists, evolutions[] `{targetRef, trigger, params}`, forms[] `{formId, activation, statOverrides, typeOverrides, spriteSet, moveRemap?}`, sprites{front,back,icon}, cryRef.
- **move**: type, damageClass (physical|special|status), power?, accuracy? (null = always hits), pp, priority, critStage, targeting, effects[] `{op, chance?, params}` where `op ∈ {damage, ailment, statStage, drain, recoil, flinch, heal, weather, hazard, protect, multiHit, chargeTurn, fixedDamage, ohko, forceSwitch, ...}` — a closed, versioned palette of ~30 effect ops in Core.
- **item**: pocket, price, effects[] (same op system + `capture{ballBonus}`, `evolve`, `repel`), usableIn (field/battle/held), consumable, keyItem.
- **trainer**: class, sprite, sightRange, party[] `{speciesRef, level, moves?, ivs?, item?}`, aiProfile (`random|basic|smart`), money, dialogue{intro,defeat,postFlag}.
- **type chart**: types[], matrix of multipliers.
- **save**: version, mapRef/pos/facing, party[] (creature instances: speciesRef, formId, level, exp, ivs, evs, nature, statusPersistent, curHp, moves+pp, happiness, heldItem, nickname, otName, ball), boxes[][], bag{pocket→[{itemRef,count}]}, money, flags{string→bool|int}, respawn point, clock offset, rngState, playtime.
- **story flags**: declared in project with id + description; referenced by triggers, NPC visibility conditions, dialogue branches, door locks.

Creature *instances* (runtime/save) are strictly separate from species *definitions* (project data).

---

## 8. Battle System Plan

Lives entirely in `Cgm.Core.Battle`. Headless, deterministic (injected RNG), fully unit-testable.

- **BattleState**: sides (player/enemy), per-side party + active slot(s), field state (weather+turns, hazards per side, screens), per-active volatile state (stat stages, confusion, flinch, protect, charge state, substitute later), turn number, seeded RNG, event log.
- **Flow per turn**: `AwaitingActions → Validate → Order → Resolve → EndOfTurn → (check end) → AwaitingActions`.
- **Action selection**: UI (or AI) submits `BattleAction` (`UseMove(slot)`, `UseItem(itemRef, target)`, `Switch(partyIndex)`, `Run`, `Mega(slot)+UseMove`). **UI never mutates state** — it renders `BattleState` snapshots and consumes the `BattleEvent` stream (`MoveUsed, DamageDealt{amount, effectiveness, crit}, StatusApplied, StatStageChanged, Fainted, SwitchedIn, CaptureShake{n, success}, ExpGained, LevelUp, ...`) to drive animation/text.
- **Validation**: legal PP, not choice-locked, target alive, item usable in battle, can't run from trainer battle, switch target healthy — invalid actions are rejected back to the submitter.
- **Turn order**: pursuit-free simplification: switches → item uses → moves by priority bracket desc, then effective Speed desc (paralysis ¼, tailwind later), ties by RNG.
- **Move resolution pipeline**: PP deduct → charge/protect checks → accuracy check (`moveAcc × accStage(user)/evaStage(target)`, null acc = skip) → for each effect op in order: crit roll (stage table 1/16, 1/8, ¼, ⅓, ½; crit ignores negative offensive / positive defensive stages, ×2) → **damage** = `floor(floor(floor(2·L/5 + 2) · Power · A/D / 50) + 2) · weather · crit · rand(0.85..1.00) · STAB · typeEff · burn` with Gen-4 rounding order documented in BATTLE_SYSTEM_SPEC.md → secondary chances → contact/recoil/drain.
- **Statuses**: persistent (burn, poison, toxic, paralysis, sleep 1–3 turns, freeze 20% thaw) — one at a time, stored on the creature instance so they persist post-battle; volatile (confusion, flinch, seeded, etc.) cleared on switch/end.
- **Capture**: wild-only; Gen 3/4 formula (§1), 0–4 shake events emitted, success adds to party or auto-deposits to first non-full box.
- **Trainer AI** (`smart` profile — the default; the game should be challenging): score every legal action: expected damage % (using true damage calc with damage-roll midpoint), KO detection (prefer guaranteed KO with highest-accuracy move), type-matchup awareness including immunities, status value when target healthy, setup value on first turns vs passive opponents, switch consideration when active is hard-countered (bounded: max 1 voluntary switch per 3 turns), healing item use below 25% HP (limited stock), small ε-random noise (~10%) so it's tough but not perfectly optimal or predictable. `basic` = damage-only greedy; `random` for early-route trainers. AI uses only information a player could know (no reading player's chosen action this turn).
- **Battle log**: every event appended with turn/seed context; dev overlay shows raw numbers (damage rolls, accuracy rolls, AI scores). Log is serializable → golden tests replay a seed + action script and diff the event stream.
- **Win/loss**: side with no non-fainted creatures loses; run success uses Gen speed-based escape formula; loss ⇒ blackout, win vs trainer ⇒ money + defeat dialogue + persistent defeated-flag.
- **Pre-battle sequence** (overworld, not battle module): trainer sight-line check each step (ray of N tiles in facing direction, blocked by solids) → `!` emote → trainer walks to player (grid pathing along the ray) → author-defined intro dialogue → screen transition → battle. All dialogue per-trainer, author-set. Movement/emote animation polish is deferred to final polish; a functional instant version (teleport + dialogue) is fine for all testing phases.
- **Exp/EVs on faint**: participants split exp (`baseExp·level/7`, trainer ×1.5), EV yield applied, level-ups mid-battle with move-learn prompts.

---

## 9. Asset Pipeline Plan

1. **Import**: PNG (via StbImageSharp) copied into `/assets/`, hashed; re-import detects changes.
2. **Auto-slice suggestion**, in order: (a) if dimensions divide cleanly by common sizes {16, 32, 48, 64} pick the largest that divides both axes; (b) transparent-gutter scan — detect fully-transparent row/column bands to infer cell size + spacing + margin; (c) connected-component scan of opaque regions for irregular sheets → suggested rects snapped to a grid. Present top suggestion with confidence; one click to accept.
3. **Manual override**: numeric cell W/H/offset/spacing fields + draggable grid overlay; per-cell include/exclude; rect mode for irregular sheets. Live preview always.
4. **Slice preview & naming**: select cells → assign sprite IDs (batch naming with `{n}` pattern); mark empty cells auto-skipped (fully transparent).
5. **Animation grouping**: multi-select cells in order → create `anim:` with per-frame ms; character-sheet template (4-direction × 3-frame walk) auto-generates the 8 standard clips.
6. **Classification**: user tags slices as tile / object / character / creature-front / creature-back / icon / UI — determines which editors list them.
7. **Metadata**: all slicer output stored as JSON in `/derived/` (source PNG never modified).
8. **Validation**: unreferenced sprites (warning), references to deleted cells (error), oversized sheets (>4096 warn).
9. **Runtime packaging**: export compiler builds texture atlases per category (max 2048²), rewrites sprite rects to atlas coords, zstd-compresses into `game.cgmpack` alongside data JSON and OGG audio.

---

## 10. Map Editor Plan

- **Canvas**: zoomable/pannable tile canvas, tileset palette panel, tool bar: brush, rect fill, bucket, eyedropper, eraser, entity select/move.
- **Layers**: ground / deco-below / deco-above tile layers + non-visual overlay layers: **collision** (auto from tile flags, paintable per-cell override: force-solid/force-open/ledge), **encounter zones** (paint cells with an encounter-table ref, color-coded), **triggers**.
- **Object & entity placement**: drag objects (multi-tile, footprint-snapped), NPCs (sprite, facing, movement type: static/wander-radius/patrol-path drawn on map, dialogue or trainer ref), warps (place → pick target map+tile via mini map picker; validator enforces bidirectional pairing warning), item pickups (item+qty+flag), signs, PCs, shop clerks (shop inventory ref), story triggers (tile enter → flag ops/dialogue/battle).
- **Validation overlays**: toggle tints for solid, grass/encounter, warps-without-target (red), unreachable regions (flood-fill from player start, later).
- **Playtest from map**: button spawns the runtime in dev mode at the cursor tile with a configurable debug party.
- MVP: single-map editing, brush/fill, 3 layers, collision paint, entity placement, warps, encounter paint. Later: multi-select, copy/paste stamps, autotiles, animated tiles, connected-map edge scrolling (Gen-style seamless routes; MVP uses warp-edges instead).

## 11. Encounter Creation System

Encounter tables are first-class entities (§7) reusable across maps. Trigger methods, each author-selectable per placement:

1. **Zone (grass/cave/water)**: painted cells reference a table; per-step roll `rate/255`-style probability (author-tunable, default ~10%/step in grass); repel-style items suppress below-level encounters.
2. **Tile trigger**: a specific tile placement fires an encounter (once or repeating, optional flag gate) on step — for scripted ambushes.
3. **Interaction trigger**: interacting with an object (e.g., `object:small_tree` "smash-able rock") rolls a table or spawns a fixed encounter.
4. **Fixed/static encounter**: an NPC-like overworld creature entity → interaction starts a specific battle (species/level/ivs/form pinned, e.g., legendaries); despawns on capture/faint via flag.
5. **Fishing/surf** (later): same tables, method-filtered.

Table editor UI: slot list with species picker, weight (shown as computed %), level min–max, optional time-of-day and required-flag conditions; live "simulate 100 encounters" preview button. Validation: weights > 0, species exist, level ≤ project max, table referenced somewhere.

## 12. Export Pipeline Plan

1. **Gate**: full validation run; any error blocks export (warnings listed, overridable).
2. **Compile**: data JSON → checked, ID-interned, references verified → packed data section; textures → atlases; audio → copied/normalized.
3. **Pack**: single `game.cgmpack` (versioned header, manifest, zstd blobs).
4. **Template**: the Creator ships with prebuilt `Cgm.Runtime` executables (debug + release, produced by CI, self-contained .NET publish). Export = copy template → rename to `<GameName>.exe` → apply icon + version metadata (via a resource editor step) → write `config.json` (game name, window title, virtual resolution, save-dir name, debug flags) → place `game.cgmpack` beside it. No compiler needed on the user's machine — this is the key export-simplicity decision.
5. **Save folder**: runtime creates `%APPDATA%/<SaveDirName>/` on first run.
6. **Debug vs release**: debug template enables overlays/console/free-warp; release strips them.
7. **Smoke test**: after export, Creator launches the exe with `--smoke` (headless-ish: boot → load pack → load start map → init battle system → exit 0) and reports pass/fail; optional "launch game now" button.
8. Output: `/exports/<name>-<date>/` folder, optionally zipped.

## 13. Testing Strategy

- **Schema tests**: round-trip serialize/deserialize every schema; migration tests (v(n)→v(n+1) fixtures); reject-invalid tests.
- **Slicer tests**: fixture PNGs (clean grid, gutters, irregular) → expected slice suggestions.
- **Map/collision tests**: headless load of fixture maps; movement simulation asserting blocked/allowed/ledge/warp outcomes (Core logic, no rendering).
- **Battle tests (the crown jewels)**: pure-function damage-calc table tests cross-checked against known Gen 3/4 calculator values; per-effect-op unit tests; **golden battle replays** — (seed + team defs + action script) → full event log compared to committed golden file; any mechanics change that alters a golden log must be intentional.
- **Item/capture tests**: heal clamping, ball formula distributions (statistical test over 10k seeded rolls), full-party→box routing.
- **Save/load**: round-trip full `PlayerState`; forward-compat load of older save fixtures.
- **Export**: CI job exports the demo project and runs the smoke test.
- **Regression**: demo project ("golden game") lives in-repo; CI validates it, exports it, smoke-tests it on every merge.
- Framework: xUnit; Core has zero graphics deps so ~90% of game logic is CI-testable on any agent's machine.

## 14. Documentation Set

All in `/docs`. Each spec is the contract an implementing agent must follow; code that contradicts a spec is a bug in one of them and must be reconciled explicitly.

- **AGENTS.md** — how AI agents work in this repo: build/test commands, doc reading order, "consult DATA_SCHEMA.md before touching schemas," no new dependencies without TECH_STACK.md update, scope-guard rules, definition of done (tests+validation pass).
- **PROJECT_OVERVIEW.md** — product vision, the core loop, legal boundary, glossary.
- **TECH_STACK.md** — chosen stack, every dependency with justification and version pin, forbidden-dependency list (no engines).
- **ARCHITECTURE.md** — §4 expanded: solution layout, module boundaries, Core/Creator/Runtime dependency rules, scene stack, message contracts.
- **DATA_SCHEMA.md** — the frozen field-by-field schemas, ID rules, versioning/migration policy. Single source of truth.
- **CREATOR_APP_SPEC.md** — §5 per-screen specs with wireframe sketches and undo/validation behavior.
- **ENGINE_RUNTIME_SPEC.md** — §6: loop timing, renderer contract, input map, UI kit primitives, debug tooling.
- **ASSET_PIPELINE_SPEC.md** — §9 algorithms (slice detection heuristics spelled out), pack format spec.
- **MAP_EDITOR_SPEC.md** — §10 tools, layer semantics, entity params.
- **BATTLE_SYSTEM_SPEC.md** — the exact formulas with rounding order, effect-op catalog with params, AI scoring weights, event catalog. Longest doc in the set.
- **EXPORT_PIPELINE_SPEC.md** — §12: pack binary layout, template process, smoke-test contract.
- **IMPLEMENTATION_PLAN.md** — §15 roadmap, kept updated with phase status.
- **CODING_STANDARDS.md** — C# style, MVVM conventions, no-singletons-except-listed, RNG injection rule, event-over-mutation rule, test requirements per PR.
- **TESTING_STRATEGY.md** — §13 + golden-file workflow.
- **SCOPE_GUARD.md** — non-goals, "later" list, and the rule: nothing enters a phase that isn't in that phase's deliverables; new ideas get appended here, not implemented.

## 15. Implementation Roadmap

Realistic with heavy AI assistance: **~8–12 months part-time to vertical slice.** Each phase ends with green tests and updated docs.

| Phase | Goal & deliverables | Tests / done criteria | Do NOT build yet | Key risk |
|---|---|---|---|---|
| **0 Research & architecture** (done ≈ this doc) | Freeze mechanics references from pokeapi-results; write BATTLE formulas appendix | Peer-review of formulas vs known calculators | Anything executable | Wrong formula understanding |
| **1 Repo/docs/scaffolding** | Solution with Core/Creator/Runtime/Tests; all 15 docs stubbed→drafted; CI (build+test); Avalonia window opens; Silk.NET window clears a color | CI green on Windows; both apps launch | Any game/editor features | Toolchain friction |
| **2 Project format & validation** | Full DATA_SCHEMA.md; Core schema types + JSON serialization + ID registry + validator framework + 10 first rules; hand-authored fixture project loads | Round-trip + validation unit tests | Editors, rendering | Schema churn (mitigate: version fields from day 1) |
| **3 Creator shell** | Nav/tabs/inspector shell, project new/open/save, undo stack, validation strip, type-chart + item + move (basic) editors as the pathfinder data editors | Create/edit/save/reopen a project by hand in UI | Map/slicer | MVVM plumbing sprawl |
| **4 Sprite importer** | Import, auto-slice heuristics, manual grid override, naming, animation grouping, asset browser | Slicer fixture tests; slice a real free asset pack | Atlas packing | Heuristic rabbit-holes — cap at the 3 listed strategies |
| **5 Map editor prototype** | Tileset editor + map canvas, brush/fill, 3 layers, collision paint, save/load maps | Author a real 2-map area | Entities beyond warps | Canvas perf (chunk the render) |
| **6 Runtime window/render loop** | Fixed-step loop, sprite batch, atlas load from raw project (dev mode), tilemap render of Phase-5 map, input mapping, camera | Map renders at 60fps; timing test | Battles, UI kit polish | GL plumbing time-sink |
| **7 Playable walking prototype** | Grid movement, collision, ledges, warps, NPC placement+dialogue box (UI kit v1: panel/font/typewriter), Creator "Playtest" button spawning runtime | Walk fixture map; scripted movement sim tests | Encounters | Feel-tuning loop (timebox: match 0.25s/tile) |
| **8 Creature data & battle prototype** | Creature/move editors complete for MVP fields; Core battle engine v1 (damage, accuracy, crits, order, faint) headless + battle scene UI (menus, HP bars, text) for 1v1 wild-style fight launched from debug key | Damage table tests vs calculator; first golden replay | AI beyond `basic`, statuses | The big one — see risk register |
| **9 Wild encounters & capture** | Encounter tables + editor + zone painting, per-step rolls, capture formula + ball item, exp/level-up/learnset, party (fixed party menu v1) | Capture distribution test; encounter simulation test; play loop: walk→encounter→catch | Trainer AI | RNG determinism discipline |
| **10 Inventory & storage** | Pockets/bag UI, field item use, marts+money, PC storage boxes UI, auto-deposit, save/load full state | Save round-trip tests; box routing tests | Held items | Menu UI volume — reuse UI kit strictly |
| **11 Trainer battles** | Trainer editor, sight-line trigger (instant version), dialogue, trainer battle rules (no run/catch), `smart` AI, statuses + stat stages + priority complete, switch flow, defeat flags, money | AI scoring tests; golden replays ×10; full status test matrix | Doubles, abilities | AI tuning subjectivity — define win-rate targets vs scripted parties |
| **12 Export pipeline** | Pack format, atlas compiler, template publish in CI, export UI, icon/config, smoke test | CI exports demo game; exe runs on a clean Windows VM | Installer/signing | Pack format churn — spec first |
| **13 Vertical slice** | Demo game (10 species, 30 moves, 3 maps, gym, evolutions incl. happiness/time, day/night, audio), polish pass (trainer approach animation, transitions, emotes), bug bash | A stranger plays the exported demo start-to-badge without instructions | Everything in SCOPE_GUARD | Polish black hole — fixed 4-week timebox |

Post-slice: forms/Mega/Gmax, full move-effect coverage (batch through PokeAPI effect archetypes with golden tests per batch), abilities, weather, event graph, surf/bike, doubles.

## 16. Risk Register

| Risk | Mitigation |
|---|---|
| Custom engine scope explosion | Engine feature list is closed (§6); anything not listed goes to SCOPE_GUARD.md. Renderer = sprite batch, full stop. |
| Editor (Avalonia) complexity | Build the three hardest surfaces early (Phase 3–5: data grid, slicer canvas, map canvas) to de-risk; reuse one canvas-control base. |
| Battle rules complexity | Formulas frozen in BATTLE_SYSTEM_SPEC.md *before* coding; effect-op palette closed and versioned; golden replays make regressions loud; implement effects in batches, not per-move. |
| Export fragility | Template-copy approach (no on-machine compilation); smoke test mandatory; test on clean VM in CI (or a no-dev-tools machine) each release. |
| Asset import edge cases | Cap heuristics at 3 strategies + always-available manual override; fixture-driven tests. |
| Scope creep | SCOPE_GUARD.md + per-phase "do not build yet" column is binding; new ideas are appended, never implemented mid-phase. |
| IP risk | No official assets in repo/releases; PokeAPI JSON is dev-only and gitignored from release artifacts; neutral naming in UI/branding; demo game uses original art/names. |
| AI-generated code drift | Specs are the contract; AGENTS.md reading order; strong typing + tests as guardrails; PRs must cite the spec section they implement; periodic /code-review passes; no new deps without doc update. |
| Performance | Targets are trivial (few hundred sprites); chunked tilemaps + atlases + fixed step; profile only if a frame budget test fails — no speculative optimization. |
| Save compatibility | `schemaVersion` on saves from v1; migration functions + old-save fixtures in CI; never remove fields, only deprecate. |
| Determinism erosion (breaks golden tests) | Single injected RNG rule in CODING_STANDARDS.md; CI replays goldens on every merge. |

## 17. Recommended First Build Prompt (Phase 1)

> You are starting Phase 1 of the Creature Game Maker project. Read `docs/MASTER_PLAN.md` in full before doing anything — it is the authoritative plan. Your job is ONLY Phase 1 (repo, docs, scaffolding); do not implement any Phase 2+ features.
>
> Tasks:
> 1. Create a .NET 10 solution `CreatureGameMaker.slnx` with projects: `src/Cgm.Core` (class library, zero UI/graphics dependencies), `src/Cgm.Creator` (Avalonia MVVM app), `src/Cgm.Runtime` (console-launched app using Silk.NET.Windowing + Silk.NET.OpenGL), `tests/Cgm.Core.Tests` (xUnit). Pin all package versions.
> 2. Cgm.Creator must launch to an empty shell window titled "Creature Game Maker" with a left nav placeholder, tabbed center area, and status bar.
> 3. Cgm.Runtime must open a 960×640 window, run a fixed-timestep 60 Hz loop with a frame accumulator, clear the screen to a solid color, log tick/frame counts with `--debug`, and exit cleanly on Esc.
> 4. Add one meaningful test proving the fixed-timestep accumulator produces exactly N update ticks for a simulated elapsed time (loop logic must live in a testable class in Cgm.Core).
> 5. Create `/docs` files per MASTER_PLAN.md §14: fully write AGENTS.md, CODING_STANDARDS.md, and SCOPE_GUARD.md (derive content from MASTER_PLAN.md); create the remaining specs as structured stubs with section headings and "TBD — Phase N" markers.
> 6. Add a GitHub Actions workflow (Windows runner) that builds the solution and runs tests.
> 7. Add `.gitignore`, `README.md` (build/run instructions), and `git init` with an initial commit.
>
> Constraints: no game features, no editors, no rendering beyond clear-color, no dependencies beyond Avalonia, Silk.NET, StbImageSharp, xUnit. When done, report: how to run both apps, test results, and anything in MASTER_PLAN.md §Phase 1 you could not complete.

---
*End of plan.*
