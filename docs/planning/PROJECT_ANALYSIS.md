# Pokemon Engine & World Editor — Project Analysis

**Date:** January 25, 2026  
**Scope:** Deep dive across all `.md` files, source code, and data to assess current status, gaps, and missing content.

---

## 1. Project Overview

- **Goal:** A desktop **Pokemon game engine** and **world editor** from scratch, Gen 3/4–style, with custom regions loaded from the local filesystem.
- **Tech stack:** Electron + Vite + TypeScript; HTML5 Canvas for rendering; IPC for file I/O.
- **Layout:** Dual-mode app — **Play** (game runtime) vs **Editor** (world builder). Toggle via toolbar; sidebar visible in editor, hidden in play.

**Key directories:**
- `src/main` — Electron main process (window, IPC, fs)
- `src/renderer/src` — Game + Editor
- `src/renderer/src/core` — Game loop, overworld, battle, data, UI, events
- `src/renderer/src/editor` — Map editor, layers, tilesets, undo
- `data/db` — `pokedex.json`, `moves.json`, `encounters.json`, `scripts.json`
- `maps/` — Tiled-format JSON maps (`overworld_main.json`, `mom_house_start.json`, etc.)
- `scripts/` — Node scripts for pokedex/moves/sprites (import, validate, download)

---

## 2. Documentation Summary (All .md Files)

| File | Purpose |
|------|---------|
| **task.md** | Master checklist: setup, core engine, world editor, overworld, data, battle, packaging, Phase 1/2. Mix of done [/], partial [/], and todo [ ]. |
| **implementation_plan.md** | Original plan: Electron stack, IPC, data structures. Latter half: **Encounter redesign** — zone painting (ID → zone key), editor workflow, migration from `EncounterZone` objects. |
| **project_roadmap.md** | Phased roadmap: **Phase 1** (playable loop: menu, save/load, transitions), **Phase 2** (battle polish: bag, switching, catching), **Phase 3** (audio, day/night), **Phase 4** (DB editors). |
| **world_builder_plan.md** | Editor spec: map editor (tiles, layers, tools, metadata painting), object editor (items, NPCs, trainers, triggers), encounter editor, brainstorm (flags, warp editor, weather, shops). Data structures for maps, encounters, objects. |
| **battle_ai_spec.md** | **Battle AI:** StateReader, Simulator, Scorer, ActionPicker. Damage formula, stat stages, scoring (Damage, Status, Stat, Tactical, Risk). Implementation roadmap and validation scenarios. |
| **TILESET_QUICKSTART.md** | How to import a sample tileset and paint. Path: `data/tilesets/images/sample_tileset.png`. |
| **data/tilesets/README.md** | Tileset layout, add/import flow, config in `tilesets.json`. |
| **docs/ENCOUNTER_SYSTEM.md** | **Node** `EncounterManager` usage: `generateEncounter(route, type)`, IVs, moves, shiny, natures. API and integration example. Refers to `data/encounters/` and separate encounter files; actual engine uses `data/db/encounters.json` (single file) and **renderer** `EncounterManager`. |

---

## 3. Current Implementation Status

### 3.1 Project Setup & Core Architecture

| Item | Status | Notes |
|------|--------|-------|
| Electron + Vite + TypeScript | ✅ | `package.json`, `electron.vite.config.ts` |
| IPC for file I/O | ✅ | `fs.readFile`, `fs.readImage` used across renderer |
| Project structure (main / renderer / core) | ✅ | Matches plan |
| ESLint / Prettier | ❌ | task.md unchecked; not in `package.json` |
| Game loop (rAF) | ✅ | `Game.loop` |
| InputManager | ✅ | Keyboard handling |
| Event Bus / Signal System | ⚠️ | `EventBus` exists; task.md marks “partial” [/] |
| Save system | ✅ | `SaveManager`; JSON read/write; Game save/load, flags |

### 3.2 World Editor

| Item | Status | Notes |
|------|--------|-------|
| Editor mode toggle / route | ✅ | Toolbar “Play Game” / “Switch to Editor” |
| Editor layout (sidebar + canvas) | ✅ | Sidebar, viewport, tabs |
| Tile placement, layers | ✅ | Ground, Collision, Decoration, Encounters |
| Layer visibility, multi-tile, palette zoom | ✅ | Per spec |
| Object selection, NPC/trainer props | ✅ | Facing, sprite, trigger, collision |
| **Pokemon Team Editor** | ❌ | task.md unchecked |
| **Async Event Manager (wait)** | ❌ | Basic `waitForResume` exists; “async” wait support incomplete |
| **NPC pathfinding/walking** | ⚠️ | `npcWalk` in scripts; promise-based flow partial |
| **Battle trigger command** | ✅ | `battle` in scripts; `handleBattleTrigger` |
| **Step triggers** | ✅ | Game loop checks triggers; repeatable flag |
| **Editor script builder** (Move, Battle, Wait) | ❌ | UI exists; full Move/Battle/Wait flow not complete |
| Warp & trigger zones | ✅ | Warp picker, bi-directional warps, Trigger type, EventManager |
| **Metadata painting** (collision, encounter zones) | ⚠️ | Collision/Encounters *layers* exist; **Zone Painting** (zone list, paint-by-zone, colored overlays) **not** done per implementation_plan |
| Save/load maps to JSON | ✅ | IPC write; project tab, create/delete/rename, search |
| Smart tools | ✅ | Flood fill, rectangle, picker |
| **Auto-tiling** | ❌ | Unchecked |
| Resize map | ✅ | GUI |
| Items, NPCs, Trainers, Triggers | ✅ | Place, edit, save |
| **Encounter zone painting** | ❌ | Zone list, paint-by-zone, overlays — all unchecked |

### 3.3 Overworld (World System)

| Item | Status | Notes |
|------|--------|-------|
| Tilemap renderer | ✅ | Canvas; `Tilemap.render` |
| **Tiled JSON support** | ✅ | **Implemented** — `MapLoader.loadMap`, `Tilemap.loadFromTiled`. task.md “Support Tiled” unchecked; should be checked. |
| Camera | ✅ | `Camera` follows player; clamped to map |
| Grid-based movement | ✅ | `Player` grid + pixel interpolation |
| Collision | ✅ | `Tilemap.isWalkable` (Collision layer); Player + NPCs |
| Map transitions (warps) | ✅ | `checkForWarp` → `loadLevel` with coords; warp cooldown |
| Spawn points | ✅ | `Spawn` / `SpawnPoint` in Objects layer |

### 3.4 Data Management

| Item | Status | Notes |
|------|--------|-------|
| Data structures (Pokemon, Move, etc.) | ✅ | `DataTypes`, `DataManager`; interfaces used |
| Registry (pokedex, moves) | ✅ | Load at startup; `getPokemonSpecies`, `getMove` |
| Asset loader | ✅ | Via IPC `fs.readFile` / `fs.readImage` for sprites, BGs |
| **loadEncounterTable** | ⚠️ | `DataManager` loads `data/encounters/{id}.json`; **no such folder**. Encounters live in `data/db/encounters.json`. Likely dead code. |

### 3.5 Battle System

| Item | Status | Notes |
|------|--------|-------|
| Battle state machine | ✅ | `BattleScene` states (INTRO → SELECT_ACTION → … → BATTLE_END_WAIT) |
| Stat calculation (IVs, EVs, level) | ✅ | `DataManager.createPokemonInstance`; `EncounterManager` for wild |
| Battle UI | ✅ | Menus, HP bars, text, sprites, BGs |
| Move execution | ✅ | `MoveEngine`, `DamageCalculator`, `ExperienceCalculator` |
| Damage formula | ✅ | Per battle_ai_spec |
| **Battle AI** | ✅ | `BattleAI`, `StateReader`, `Simulator`, `Scorer`; `getBestAction` used in enemy turn |
| **Bag integration** | ❌ | task.md unchecked |
| **Party switching** | ❌ | Unchecked |
| **Catching** | ❌ | Unchecked |

### 3.6 Phase 1 (Playable Loop)

| Item | Status | Notes |
|------|--------|-------|
| MenuSystem, StartMenu | ✅ | UI stack; Start menu (Pokedex, Pokemon, Bag, Save, etc.) |
| Save/Load in menu | ✅ | Save option → `SaveManager`; Title “Continue” loads |
| Title screen | ✅ | New Game / Continue; `mom_house_start.json` for new |
| **Map transition (fade to black)** | ⚠️ | Wild encounter uses **strobe + fade**; generic warp “fade” not explicitly called out — transition overlay exists but is encounter-specific |

### 3.7 Phase 2 (Battle Polish & AI)

| Item | Status | Notes |
|------|--------|-------|
| Battle AI (heuristic scoring) | ✅ | As above |
| Bag, switching, catching | ❌ | Not implemented |

### 3.8 Encounter System

- **Renderer** `EncounterManager` (`core/EncounterManager.ts`): loads `data/db/encounters.json`, `generateEncounter(zoneId)`. Used for wild battles.
- **Node** `EncounterManager` (`managers/EncounterManager.js`): `require`-based; documented in `ENCOUNTER_SYSTEM.md`. Different API and data path.
- **Encounter flow:** Map `Encounters` layer + `zoneMapping` (tile ID → zone key) → `getEncounterZoneAt` → zone key → `generateEncounter`. Per-step chance (e.g. 15%) in `checkForEncounter`.

### 3.9 Event System

- **EventManager:** Loads `data/db/scripts.json`; `runScript(id)`. Commands: `dialog`, `heal`, `giveItem`, `npcAction`, `npcWalk`, `battle`, `wait`.
- **DialogBox:** Shows text; `resume()` on keypress to continue scripts.
- **Triggers:** Step + interact; repeatable flag; `triggeredIds` for one-shots.

---

## 4. Gaps, Inconsistencies & Bugs

### 4.1 task.md vs Reality

- **Tiled support:** Implemented but unchecked. **Recommendation:** Check “Support Tiled Map Editor (JSON parsing).”
- **Camera, grid movement, collision, warps:** All implemented; task.md has them unchecked. **Recommendation:** Update checklist to match.

### 4.2 Duplicate / Unused Code

- **Duplicate `updateTransition` check in `Game.ts`:** The same `if (this.isTransitioning) { updateTransition; return; }` block appears twice (around lines 283–291). Remove the duplicate.
- **`DataManager.loadEncounterTable`:** Points at `data/encounters/{id}.json`. That layout is unused; encounters are in `data/db/encounters.json`. Either remove `loadEncounterTable` or repoint it at the shared encounter DB and use it, if desired.

### 4.3 Two Encounter Systems

- **Renderer** `EncounterManager`: Used by game; single `encounters.json`.
- **Node** `EncounterManager` + `ENCOUNTER_SYSTEM.md`: Different API, `data/encounters/` Per-route files.
- **Recommendation:** Clarify in docs which system is canonical. If only the renderer one is used, note that the Node version is legacy or for tooling only.

### 4.4 Data Paths

- Battle background: `data/battle_bg_grass.png` — file present.
- Sprites: `data/pokemon/...`, `data/player/...` — used consistently.
- Tilesets: `data/tilesets/...` — matches README / quickstart.

### 4.5 Other Code-Level Gaps

- **Battle:** No Bag, no party switch, no catch flow.
- **Editor:** No Pokemon Team Editor; Zone Painting (encounter overlay/paint-by-zone) not done; Auto-tiling missing.
- **Events:** No generic “fade to black” for warps (only encounter transition); async wait support is minimal.

---

## 5. What Needs to Be Added or Fixed

### 5.1 Quick Fixes

1. **Game.ts:** Remove the duplicate `isTransitioning` / `updateTransition` block.
2. **task.md:** Mark Tiled, Camera, grid movement, collision, warps as done (or partial) where appropriate.
3. **DataManager:** Either remove `loadEncounterTable` or align it with `data/db/encounters.json` and document.

### 5.2 High Priority (Playable Loop & Polish)

1. **Map transition (fade) for warps:** Reuse or generalize the transition overlay for warp-based map changes (not only wild encounters).
2. **Bag integration in battle:** Use items (e.g. Potions, status healers) from a Bag structure.
3. **Party switching in battle:** “Pokemon” menu in battle; switch after KO or when choosing to switch.
4. **Catching mechanics:** Pokeballs, catch rate formula, Pokedex “owned” updates.

### 5.3 Editor & Content

1. **Encounter Zone Painting:**  
   - Zone list with colors.  
   - Paint tool assigns zone ID to Encounters layer.  
   - Colored overlays in editor.  
   Matches `implementation_plan` and `world_builder_plan`.
2. **Pokemon Team Editor:** Edit trainer parties in the object editor.
3. **Metadata painting:** Unify collision + encounter zone painting workflow (including zone key ↔ ID) as in the plans.
4. **Auto-tiling:** For terrain layers.

### 5.4 Event System

1. **Async event manager:** Proper wait support (e.g. timed waits, “wait for fade”), not only “wait for dialog confirm.”
2. **NPC pathfinding/walking:** Robust `npcWalk` with pathfinding if required by design.
3. **Editor script builder:** Full support for Move, Battle, Wait and any other script commands.

### 5.5 Infrastructure & Ops

1. **ESLint + Prettier:** Per task.md; add config and scripts.
2. **Testing:** `package.json` has no real test runner (e.g. Vitest). battle_ai_spec and roadmap assume tests.
3. **Packaging:** No Electron Builder / Forge; add when you want distributable builds.

---

## 6. What Content Is Missing (Or Under-Documented)

### 6.1 Documentation

- **Single “engine” README:** Overview, run instructions (`npm run dev`), map format, where to put assets. `TILESET_QUICKSTART` and `data/tilesets/README` are focused on tilesets only.
- **ENCOUNTER_SYSTEM.md:** Update to reflect `data/db/encounters.json` and renderer `EncounterManager`; clarify role of Node `EncounterManager` and `data/encounters/`.
- **Map format doc:** Brief spec of your Tiled JSON usage: layers (Ground, Collision, Encounters, Objects), object types (Spawn, Warp, Trigger, NPC, etc.), `zoneMapping` and encounter zones.
- **Script format doc:** `scripts.json` command types (`dialog`, `npcWalk`, `battle`, etc.) and parameters. Helpful for both runtime and editor.

### 6.2 Data & Content

- **Encounters:** `encounters.json` has `route_1_grass` and `route_1_water`; ensure maps that use grass/water reference these zone keys (or add more zones as needed).
- **Scripts:** `scripts.json` has `mom_start`, `event_*`, `rival_ambush`, etc. Link these to NPCs/triggers in maps and document.
- **Trainer data:** Scripts reference `enemyId` (e.g. `rival01`). Define trainer definitions (teams, AI) and hook them to `battle` commands.
- **Items:** Give-item scripts use `itemId`; no Item DB or Bag structure yet. Need at least a minimal items registry and Bag.

### 6.3 Features Mentioned in Plans But Not Implemented

- **Global flags/variables:** For story progression, conditional dialogue, blocking paths. EventManager has `flags` in save; need design for how flags drive logic.
- **Warp/connection editor:** You have warps and bi-directional warp tool; a higher-level “connection” view (map graph, etc.) is still optional.
- **Weather / time-of-day:** world_builder_plan and ENCOUNTER_SYSTEM mention weather and time; not implemented.
- **Audio:** No `AudioManager`; BGM/SFX per roadmap.
- **Database editors (Phase 4):** Move / Pokemon / Item editors in-app; not started.

---

## 7. Suggested Order of Work

1. **Immediate:** Fix duplicate `updateTransition`; update task.md; (optionally) fix or remove `loadEncounterTable`.
2. **Short-term:** Fade transition for warps; then Bag + party switch + catching.
3. **Editor:** Encounter zone painting → Pokemon Team Editor → metadata painting → auto-tiling.
4. **Docs:** README, encounter + map + script specs.
5. **Later:** Audio, day/night, DB editors, packaging.

---

## 8. Summary

You have a **working prototype**: overworld with Tiled maps, camera, collision, warps, triggers, NPCs, dialog, scripts, wild encounters, and a full **battle system with AI**. The **editor** supports tiles, layers, objects (NPCs, items, trainers, triggers, warps), project/map management, and encounter editing (non–zone-painting).  

**Largest gaps:** Zone-based encounter painting in the editor; Bag, switching, and catching in battle; consistent warp transition; and clearer docs for encounters, maps, and scripts. Addressing those next will bring the project much closer to the “playable loop” and “world builder” goals described in your `.md` files.
