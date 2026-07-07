# Creature Game Maker Project Bible

Working title: Creature Game Maker

Version: 0.1

Purpose: Define the product, scope, architecture, data model, legal boundaries, and implementation roadmap for a standalone app that lets users create Pokemon-style creature-battler RPGs from scratch and export them as playable Windows games.

This project is separate from any existing Godot creature-battler project.

---

## 1. Product Thesis

Creature Game Maker is a Windows-first desktop creation tool for building custom 2D monster-catching RPGs.

The app should let a user import art, define tiles and objects, build maps, configure encounters, create player characters, author creature data, build teams, define items, and export a standalone playable `.exe`.

The goal is not to build a generic game engine first. The goal is to build a focused creator for one genre:

> A top-down 2D creature-collecting RPG with exploration, battles, inventory, creature storage, encounters, trainers, and data-driven rules.

The creator app is the authoring tool.

The exported game is the player-facing runtime.

The first milestone must prove this loop:

```text
Create project
-> import a tileset
-> build one small map
-> place a player start
-> define one wild encounter
-> define one starter creature
-> export Windows build
-> launch playable game
-> walk around
-> trigger battle
-> win or lose battle
```

---

## 2. Legal And IP Boundary

This is a passion project and will not be monetized. That helps with intent, but it does not remove intellectual property risk.

The project must be designed as a Pokemon-style creature RPG maker, not as a tool that ships Nintendo, Game Freak, or The Pokemon Company assets.

Rules:

- Do not ship official Pokemon sprites, cries, music, logos, names, UI art, maps, or copyrighted assets.
- Do not ship a built-in Pokedex containing official Pokemon unless the legal risk is knowingly accepted for private use only.
- Prefer original creatures, original moves, original items, original types, and original art.
- If optional PokeAPI import exists, treat it as a user-controlled data import for private experimentation, not as bundled content.
- The app should support user-owned or licensed assets.
- The app should call the default rules "creature-battler rules" rather than "Pokemon rules" in UI.
- Avoid Pokemon branding in the project name, logo, splash screen, export metadata, and docs intended for release.

Why this matters:

- Pokemon fan tools and games have historically received takedowns even when free.
- Pokemon Essentials, a popular RPG Maker XP base for Pokemon-style fan games, has been associated with DMCA takedown history.
- Pokemon Uranium was removed after legal pressure despite being non-commercial and fan-made.

Safe positioning:

> A flexible 2D creature-battler RPG maker inspired by classic monster-catching games.

Risky positioning:

> A Pokemon game maker.

Research references:

- [PokeAPI documentation](https://pokeapi.co/docs/v2)
- [Pokemon Essentials history](https://pokemon-fan-game.fandom.com/wiki/Pok%C3%A9mon_Essentials)
- [Pokemon Uranium takedown reporting](https://www.wired.com/story/nintendo-takedown-pokemon-uranium)

---

## 3. Target User

Primary user:

- A hobbyist who wants to make a custom monster-catching RPG.
- May not know programming.
- Wants visual map editing, form-based data editing, and export buttons.
- Wants enough power to make a real game, not only a toy demo.

Secondary user:

- A technical creator who wants to customize rules, import datasets, add custom scripts later, and validate data.

User experience goal:

> The user should feel like they are building a game, not wrestling with engine internals.

---

## 4. Product Shape

The project has two major parts.

### 4.1 Creator App

The Creator App is the desktop tool used to author games.

It should provide:

- Project creation and project settings.
- Asset import.
- Sprite sheet slicing.
- Tileset and object definition.
- Map editor.
- Collision editor.
- Encounter zone editor.
- Character editor.
- Creature/species editor.
- Move editor.
- Type chart editor.
- Item editor.
- Inventory configuration.
- Trainer editor.
- Battle rule configuration.
- Storage/PC configuration.
- Validation dashboard.
- Playtest button.
- Export button.

### 4.2 Game Runtime Template

The Game Runtime Template is the actual game engine project that loads exported creator data.

It should provide:

- Top-down player movement.
- Map loading.
- Collision.
- Interactions.
- Encounter triggers.
- Inventory.
- Party.
- Storage.
- Battle system.
- Save/load.
- UI screens.
- Audio playback.
- Input handling.
- Debug logs.

### 4.3 Export Pipeline

The export pipeline packages:

- Runtime template.
- User-authored data.
- Imported assets.
- Project settings.
- Windows executable build.

The exporter should eventually produce:

```text
MyGame/
  MyGame.exe
  MyGame_Data/
  README.txt
  saves/
```

Single-file `.exe` is not required for the first version. A normal Windows game folder containing an `.exe` and data files is acceptable.

---

## 5. Recommended Technical Direction

### 5.1 Recommended Architecture

Use a separate Creator App plus a Unity-based runtime/export template.

Recommended:

| Area | Choice | Reason |
|---|---|---|
| Creator app | Tauri 2 + React + TypeScript | Fast desktop UI, small app footprint, good file-system integration. |
| Creator backend | Rust commands plus optional sidecar tools | Tauri can bundle sidecars for heavier asset processing. |
| Runtime/export engine | Unity 6 LTS or current Unity LTS | Strong 2D tooling, Windows standalone export, mature C# ecosystem. |
| Runtime language | C# | Best fit for Unity runtime and data-driven battle logic. |
| Runtime data | JSON first, binary bundles later | Easy to inspect, diff, validate, and generate. |
| Image processing | Rust image crate or bundled Python sidecar | Sprite slicing and transparent-region detection need robust image tooling. |
| Validation | TypeScript schemas plus runtime C# validation | Catch errors before export and again before play. |
| Tests | Vitest for creator logic, NUnit/EditMode/PlayMode for Unity | Separate creator tests from game runtime tests. |

Why Unity for the exported game:

- Unity supports Windows standalone player builds through its build settings.
- Unity ScriptableObjects are useful for static data containers, though generated JSON should remain the source of truth early.
- Unity has mature 2D Tilemap tooling and 2D Tilemap Extras such as Rule Tiles, Animated Tiles, and custom brushes.

Research references:

- [Unity Windows build settings](https://docs.unity.cn/2020.3/Documentation/Manual/WindowsStandaloneBinaries.html)
- [Unity standalone build settings](https://docs.unity.cn/2019.2/Documentation/Manual/BuildSettingsStandalone.html)
- [Unity ScriptableObject manual](https://docs.unity.cn/6000.1/Documentation/Manual/class-ScriptableObject.html)
- [Unity 2D Tilemap Extras](https://docs.unity.cn/Manual/com.unity.2d.tilemap.extras.html)
- [Tauri sidecar documentation](https://v2.tauri.app/learn/sidecar-nodejs/)

### 5.2 Alternative Architecture

If Fable 5 struggles with Tauri + Unity integration, use this simpler path:

| Area | Choice |
|---|---|
| Creator app | Unity Editor tool or Unity runtime editor |
| Runtime | Same Unity project |
| Export | Unity build pipeline |

This is less clean as a product, but faster to prototype because the editor and game runtime share the same engine.

Use this fallback only if the separate Creator App becomes too slow to build.

---

## 6. Core Design Principles

1. Data first.
2. No hardcoded Pokemon-specific names in core systems.
3. Every authored object has a stable ID.
4. UI edits data; runtime consumes data.
5. Validation catches broken games before export.
6. Battles are deterministic when seeded.
7. Debug logs explain battle outcomes.
8. Build the smallest playable creator loop first.
9. Official-style complexity should be opt-in.
10. Do not build a general-purpose engine before the first exported game works.

---

## 7. Naming And ID Rules

Use stable namespaced IDs.

Examples:

```text
species:leafcub
form:leafcub_default
creature:starter_leafcub
move:ember
type:fire
item:potion
tile:grass_dark
object:small_tree
map:route_001
encounter:route_001_grass_day
trainer:rival_001
inventory:pocket_medicine
storage:default_pc
status:burn
ability:overgrow_like
```

Rules:

- Display names are not IDs.
- IDs must be stable after release.
- Renaming a display name must not break saves.
- Save files store IDs, not display names.
- Data references must validate before export.

---

## 8. Project File Structure

Recommended user project layout:

```text
MyCreatureGame/
  project.json
  assets/
    spritesheets/
    sliced/
    portraits/
    audio/
    ui/
  data/
    types.json
    moves.json
    species.json
    forms.json
    items.json
    trainers.json
    encounters.json
    maps.json
    scripts.json
  maps/
    route_001.map.json
    home_town.map.json
  exports/
  validation/
    latest_report.json
```

Creator app internal structure:

```text
creature-game-maker/
  apps/
    creator/
  runtime/
    unity-template/
  packages/
    schema/
    validation/
    sprite-importer/
    exporter/
  docs/
```

---

## 9. Asset Import And Sprite Sheet System

### 9.1 Goals

The app must import sprite sheets and help the user slice them into usable assets.

Supported asset categories:

- Tilesets.
- Object sprites.
- Player character sprites.
- NPC sprites.
- Creature battle sprites.
- Creature icons.
- UI sprites.
- Item icons.
- Animations.

### 9.2 Minimum Import Features

The first import pipeline should support:

- PNG files.
- Manual grid size entry, such as 16x16, 32x32, 48x48, 64x64.
- Automatic grid suggestion based on image dimensions.
- Transparent-cell trimming.
- Preview grid overlay.
- Slice selection.
- Naming selected slices.
- Assigning slice category.
- Exporting slices as individual PNGs.
- Generating metadata JSON.

### 9.3 Auto Detection

Auto detection should be helpful, but never magical-only.

The importer may suggest:

- Cell width and height.
- Rows and columns.
- Empty cells.
- Duplicate cells.
- Animation strips.
- Collision defaults.
- Tile categories.

User must be able to override:

- Cell size.
- Spacing.
- Margin.
- Pivot.
- Category.
- Slice names.
- Animation frame order.

### 9.4 Sprite Metadata

Example:

```json
{
  "id": "sprite:grass_dark",
  "sourceSheet": "assets/spritesheets/world_tiles.png",
  "rect": { "x": 0, "y": 0, "w": 32, "h": 32 },
  "pivot": { "x": 0.5, "y": 0.5 },
  "category": "tile",
  "tags": ["grass", "outdoor"],
  "transparent": false
}
```

---

## 10. Tiles, Objects, And Map Authoring

### 10.1 Tile Definition

Tiles are grid-aligned visual pieces.

Each tile can define:

- Sprite reference.
- Walkable flag.
- Collision shape.
- Encounter zone tag.
- Terrain tag.
- Footstep sound.
- Surf/swim permission.
- Underwater permission.
- Ledge behavior.
- Layer.
- Sorting priority.
- Custom metadata.

Example:

```json
{
  "id": "tile:tall_grass",
  "displayName": "Tall Grass",
  "sprite": "sprite:tall_grass_01",
  "walkable": true,
  "collision": null,
  "terrainTags": ["grass", "encounter_surface"],
  "encounterZone": "encounter:route_001_grass"
}
```

### 10.2 Object Definition

Objects are placeable entities that may be grid-aligned or free-positioned.

Object examples:

- Trees.
- Rocks.
- Signs.
- Doors.
- NPCs.
- Item balls.
- PCs.
- Healing machines.
- Trainers.
- Ledges.
- Warp points.
- Story triggers.

Each object can define:

- Sprite or animation.
- Position.
- Collision.
- Interaction script.
- Encounter data.
- Conditional visibility.
- Facing direction.
- Trigger type.
- Custom properties.

### 10.3 Map Editor

Required editor tools:

- Paint tile.
- Erase tile.
- Rectangle fill.
- Bucket fill.
- Eyedropper.
- Layer selection.
- Object placement.
- Collision overlay.
- Encounter overlay.
- Start-position marker.
- Warp-link editor.
- Validation overlay.
- Playtest from current map.

### 10.4 Map Layers

Recommended layers:

| Layer | Purpose |
|---|---|
| Ground | Base terrain. |
| Detail | Flowers, small stones, decals. |
| Collision | Invisible collision and blocked cells. |
| Encounter | Encounter regions. |
| Objects | Trees, signs, NPCs, doors. |
| Overhead | Roofs, treetops, bridges. |
| Triggers | Warps, cutscenes, story gates. |

---

## 11. Player Character System

Minimum player character requirements:

- WASD movement.
- Optional arrow-key movement.
- Grid-feel movement even if internally smooth.
- Four-direction facing.
- Walking animations.
- Run toggle or hold key.
- Interaction button.
- Menu button.
- Collision response.
- Encounter checks when stepping through encounter zones.

Player data:

```json
{
  "id": "player:default",
  "displayName": "Player",
  "spriteSet": "spriteset:player_default",
  "startMap": "map:home_town",
  "startPosition": { "x": 8, "y": 12 },
  "startFacing": "down",
  "startingInventory": ["item:potion"],
  "startingTeam": ["creature:starter_leafcub"]
}
```

---

## 12. Inventory System

The inventory should feel like a classic creature RPG inventory.

Required pockets:

- Items.
- Medicine.
- Capture devices.
- Key items.
- Battle items.
- TMs or move manuals, if supported.

Item behavior categories:

- Heal HP.
- Restore PP/energy.
- Cure status.
- Revive.
- Capture creature.
- Escape from battle.
- Boost stat in battle.
- Key item.
- Evolution item.
- Held item, later.

Minimum item data:

```json
{
  "id": "item:potion",
  "displayName": "Potion",
  "pocket": "medicine",
  "usableInField": true,
  "usableInBattle": true,
  "target": "party_member",
  "effect": {
    "type": "heal_hp_flat",
    "amount": 20
  },
  "price": 300
}
```

Rules:

- UI never applies item effects directly.
- UI submits item-use action.
- Runtime validates target and context.
- Item resolver applies effect.
- Battle log records item use and result.

---

## 13. Creature Party And Storage

### 13.1 Party Rules

Required:

- Minimum party size: 1 living creature for normal play.
- Maximum party size: 6 creatures.
- Party order matters.
- First able creature is sent into battle by default.
- Fainted creatures cannot be sent out unless revived.

### 13.2 Creature Instance

Species data defines what a creature can be.

Creature instance data defines a specific caught creature.

Example:

```json
{
  "id": "creature_instance:abc123",
  "speciesId": "species:leafcub",
  "formId": "form:leafcub_default",
  "nickname": "Sprig",
  "level": 5,
  "experience": 135,
  "currentHp": 20,
  "moves": ["move:tackle", "move:growl"],
  "ability": "ability:leaf_guard_like",
  "status": null,
  "capturedAt": "map:route_001"
}
```

### 13.3 Storage Rules

Required:

- Caught creatures go to party if party has fewer than 6.
- If party is full, caught creatures go to storage automatically.
- PC object allows manual deposit, withdraw, and move.
- Storage boxes have names and capacity.
- Save data stores storage state.

Minimum PC features:

- View boxes.
- Select creature.
- Move to party.
- Move to box.
- Release creature, later.
- Rename box, later.

---

## 14. Creature Data Model

Species:

```json
{
  "id": "species:leafcub",
  "displayName": "Leafcub",
  "types": ["type:grass"],
  "baseStats": {
    "hp": 45,
    "attack": 49,
    "defense": 49,
    "specialAttack": 65,
    "specialDefense": 65,
    "speed": 45
  },
  "catchRate": 45,
  "baseExperience": 64,
  "forms": ["form:leafcub_default"],
  "learnset": [
    { "level": 1, "move": "move:tackle" },
    { "level": 3, "move": "move:growl" }
  ],
  "evolutions": []
}
```

Form:

```json
{
  "id": "form:leafcub_default",
  "speciesId": "species:leafcub",
  "displayName": "Leafcub",
  "battleSpriteFront": "sprite:leafcub_front",
  "battleSpriteBack": "sprite:leafcub_back",
  "overworldSprite": "spriteset:leafcub_overworld",
  "statModifiers": {},
  "isDefault": true
}
```

Future form mechanics:

- Evolution.
- Regional forms.
- Temporary battle transformations.
- Cosmetic variants.
- Boss-only forms.

---

## 15. Type System

The type chart must be data-driven.

Example:

```json
{
  "id": "type:fire",
  "displayName": "Fire",
  "damageTo": {
    "type:grass": 2.0,
    "type:water": 0.5,
    "type:fire": 0.5
  }
}
```

Rules:

- Do not hardcode type names in damage logic.
- Type multipliers come from type chart data.
- Missing relationship defaults to 1.0.
- Multiple defender types multiply together.
- Immunity is represented as 0.0.

---

## 16. Move System

Moves must be data-driven, but not every move effect must exist at launch.

Move data:

```json
{
  "id": "move:ember",
  "displayName": "Ember",
  "type": "type:fire",
  "category": "special",
  "power": 40,
  "accuracy": 100,
  "priority": 0,
  "pp": 25,
  "target": "opponent_active",
  "effects": [
    {
      "type": "damage",
      "formula": "standard"
    },
    {
      "type": "apply_status_chance",
      "status": "status:burn",
      "chance": 0.1
    }
  ]
}
```

Minimum move effect support:

- Standard physical damage.
- Standard special damage.
- Status move with no damage.
- Accuracy check.
- Critical hit.
- Same-type attack bonus, if enabled.
- Type effectiveness.
- Stat stage changes.
- Burn.
- Poison.
- Sleep, later.
- Paralysis, later.
- Multi-hit, later.
- Recoil, later.
- Drain, later.
- Weather and terrain, later.

Rule:

> A move should not be selectable in creator data unless its effects are implemented and validated.

---

## 17. Battle System

### 17.1 Battle Scope

MVP battle type:

- One player creature vs one wild creature.
- Turn-based.
- Four moves.
- Items.
- Run attempt.
- Capture attempt.
- Win/lose result.

Later:

- Trainer battles.
- Switching.
- Multi-creature parties.
- Experience.
- Level-up.
- Evolution.
- Smarter AI.
- Temporary transformations.
- Double battles only after single battles are stable.

### 17.2 Battle Architecture

Required flow:

```text
Battle UI
-> submits action
-> BattleController validates action
-> TurnOrderResolver orders actions
-> ActionResolver applies actions
-> DamageCalculator calculates damage
-> EffectResolver applies secondary effects
-> BattleState updates runtime state
-> BattleLog records result
-> BattleResult reports outcome
```

Rules:

- UI never directly subtracts HP.
- AI cannot bypass validation.
- Moves are resolved by effect IDs, not display names.
- Random rolls are recorded in battle log.
- Battle state can be serialized for debugging.

### 17.3 Damage Formula

Start with a simplified formula:

```text
baseDamage = (((2 * level / 5 + 2) * power * attack / defense) / 50) + 2
modifier = critical * random * stab * typeEffectiveness * burn * other
damage = floor(baseDamage * modifier)
```

Make formula settings configurable later.

### 17.4 Battle Log Requirements

Each battle log entry should answer:

- What action was chosen?
- Was it valid?
- What was the turn order?
- Did the move hit?
- Was it critical?
- What type multiplier applied?
- What random roll occurred?
- How much damage was dealt?
- What status/effect was applied?
- Why did the battle end?

Example:

```json
{
  "turn": 3,
  "actor": "player.active",
  "action": "move:ember",
  "hitRoll": 42,
  "accuracy": 100,
  "critical": false,
  "typeMultiplier": 2.0,
  "randomMultiplier": 0.93,
  "damage": 18,
  "message": "Sprig used Ember. It was super effective."
}
```

---

## 18. Encounter System

Encounter zones can be attached to:

- Tiles.
- Map regions.
- Objects.
- Scripts.

Encounter data:

```json
{
  "id": "encounter:route_001_grass",
  "displayName": "Route 001 Grass",
  "trigger": "step",
  "chancePerStep": 0.08,
  "table": [
    {
      "species": "species:leafcub",
      "minLevel": 2,
      "maxLevel": 4,
      "weight": 60
    },
    {
      "species": "species:flameling",
      "minLevel": 3,
      "maxLevel": 5,
      "weight": 40
    }
  ]
}
```

Rules:

- Encounter tables validate species references.
- Weights must be positive.
- Disabled species cannot appear in enabled encounters.
- Encounter rolls should be logged in debug mode.

---

## 19. Trainer System

Trainer data:

```json
{
  "id": "trainer:rival_001",
  "displayName": "Rival",
  "sprite": "spriteset:rival",
  "team": [
    {
      "species": "species:flameling",
      "level": 5,
      "moves": ["move:tackle", "move:ember"]
    }
  ],
  "aiProfile": "ai:basic_trainer",
  "onDefeat": "script:rival_001_defeated"
}
```

MVP:

- Trainer interaction starts battle.
- Trainer has fixed team.
- Trainer uses basic AI.
- Defeated trainers remain defeated in save data.

Later:

- Vision cones.
- Rematch flags.
- Dialogue branches.
- AI tiers.

---

## 20. AI System

Start simple.

AI tiers:

| Tier | Behavior |
|---|---|
| 0 | Random legal move. |
| 1 | Prefer highest expected damage. |
| 2 | Consider type effectiveness and accuracy. |
| 3 | Consider KO, status, healing, and switching. |
| 4 | Predict player likely action. |
| 5 | Boss/competitive style, only after core systems are stable. |

Rules:

- AI must use the same action validation as the player.
- AI decisions should be logged.
- Trainer data chooses AI profile.

---

## 21. Save System

Save data must store:

- Player position.
- Current map.
- Facing direction.
- Inventory.
- Party.
- Storage.
- Story flags.
- Defeated trainers.
- Collected items.
- Current settings.
- Play time.

Save data should not store:

- Raw sprite image data.
- Full species definitions.
- Full move definitions.
- Display-name-only references.

Save files reference stable IDs.

---

## 22. Creator Validation System

The validation dashboard is essential.

It should catch:

- Missing sprites.
- Broken IDs.
- Duplicate IDs.
- Missing start map.
- Missing player start.
- Maps with no walkable area.
- Warps with no destination.
- Encounter table with invalid species.
- Species with no valid form.
- Form with missing battle sprite.
- Move with unsupported effect.
- Item with unsupported effect.
- Trainer with invalid team.
- Player starting team with fewer than 1 creature.
- Party size greater than 6.
- Type chart references missing type.
- Export target missing.

Validation levels:

| Level | Meaning |
|---|---|
| Error | Blocks export. |
| Warning | Allows export, but likely broken or incomplete. |
| Info | Helpful suggestion. |

---

## 23. Creator App Screens

Minimum screens:

- Home/project list.
- Project settings.
- Asset importer.
- Sprite sheet slicer.
- Tileset editor.
- Object editor.
- Map editor.
- Creature editor.
- Move editor.
- Type chart editor.
- Item editor.
- Trainer editor.
- Encounter editor.
- Inventory settings.
- Storage settings.
- Validation dashboard.
- Playtest/export screen.

Do not build every screen fully before the first playable loop. Stub screens are acceptable if the MVP data can be edited.

---

## 24. Export Pipeline

MVP export can be simple:

1. Validate project.
2. Copy runtime template.
3. Copy project data into runtime data folder.
4. Copy assets into runtime assets folder.
5. Generate build settings.
6. Run Unity build command or open Unity template for manual build.
7. Place output in project `exports/`.

Preferred eventual export:

```text
Creator App
-> validates project
-> generates runtime data
-> invokes Unity batchmode build
-> outputs Windows folder
-> launches exported game for smoke test
```

First version may use:

```text
Creator App
-> Generate Unity project
-> User opens Unity
-> User clicks Build
```

That is acceptable for proving the authoring pipeline if full automated export is too much for the first pass.

---

## 25. PokeAPI Import Strategy

PokeAPI can be optional. It should not define the entire product.

Potential PokeAPI uses:

- Import type chart.
- Import species stat templates.
- Import move templates.
- Import ability templates.
- Import item templates.

Rules:

- PokeAPI data must be normalized into this project schema.
- Raw PokeAPI data is never used directly by the runtime.
- Imported content is disabled unless supported by the runtime.
- Unsupported move effects remain disabled.
- Imported official names/assets should be treated as private-use data with legal risk.

Reference:

- PokeAPI exposes move fields such as accuracy, power, PP, priority, and effect data, and type endpoints include damage relations. See [PokeAPI documentation](https://pokeapi.co/docs/v2).

---

## 26. MVP Roadmap

### Phase 0: Project Foundation

Goal: Set up repo, docs, schemas, and a minimal desktop shell.

Deliverables:

- Creator app skeleton.
- Runtime template skeleton.
- Shared JSON schemas.
- Project open/save.
- Validation framework.
- One sample project.

### Phase 1: Sprite Import And Map Prototype

Goal: Import a tileset and build one small map.

Deliverables:

- PNG import.
- Manual sprite slicing.
- Tile definitions.
- Map grid.
- Paint/erase tools.
- Collision flags.
- Player start marker.
- Save/load map JSON.

### Phase 2: Runtime Walking Prototype

Goal: Export or run a map in the runtime.

Deliverables:

- Runtime loads map JSON.
- Runtime loads sprites.
- Player moves with WASD.
- Collision works.
- Camera follows player.
- Start position works.

### Phase 3: Minimal Creature Data

Goal: Author enough data for a battle.

Deliverables:

- Type editor.
- Species editor.
- Form editor.
- Move editor.
- Starting party.
- Validation for battle-required data.

### Phase 4: Minimal Battle

Goal: One-on-one battle works.

Deliverables:

- Battle state.
- Move action.
- Damage calculator.
- Type effectiveness.
- HP/fainting.
- Battle result.
- Battle log.
- Basic battle UI.

### Phase 5: Wild Encounters And Capture

Goal: Walking on grass can start battles and capture creatures.

Deliverables:

- Encounter zones.
- Encounter tables.
- Step-based encounter rolls.
- Capture item.
- Capture formula.
- Add to party/storage.

### Phase 6: Inventory And Storage

Goal: Creature RPG loop becomes recognizable.

Deliverables:

- Inventory menu.
- Medicine items.
- Capture items.
- Party screen.
- PC/storage screen.
- Save/load party and storage.

### Phase 7: Trainer Battles

Goal: Basic authored trainers work.

Deliverables:

- Trainer objects.
- Trainer team data.
- Battle start interaction.
- Defeated flags.
- Basic AI.

### Phase 8: Exported Game Build

Goal: Creator produces a playable Windows build.

Deliverables:

- Export validation.
- Runtime packaging.
- Windows build output.
- Launch smoke test.
- Export report.

---

## 27. Explicit Non-Goals For Early Development

Do not build early:

- Online multiplayer.
- Trading creatures.
- Cloud saves.
- Mod marketplace.
- Mobile export.
- Console export.
- Full scripting language.
- Visual programming system.
- Every official Pokemon move.
- Every official Pokemon ability.
- Every official Pokemon item.
- Animated battle cut-ins.
- Complex weather/terrain systems.
- Double battles.
- Procedural world generation.
- AI-generated maps.
- Plugin marketplace.

These can be revisited after the creator can export a small playable game.

---

## 28. Fable 5 Working Instructions

When using Fable 5 on this project:

1. Read this document first.
2. Build the smallest playable creator-to-runtime loop.
3. Do not start by implementing all Pokemon mechanics.
4. Do not use copyrighted Pokemon assets.
5. Keep systems data-driven.
6. Add validation whenever data can break runtime behavior.
7. Keep battle logic out of UI.
8. Keep creator data separate from runtime save data.
9. Prefer one working vertical slice over many half-built screens.
10. At the end of each pass, report what works, what was tested, and what remains broken.

Suggested first Fable prompt:

```text
You are working on Creature Game Maker, a Windows-first desktop app that authors and exports custom 2D creature-battler RPGs.

Read docs/PROJECT_BIBLE.md completely before coding.

Do not use the existing Godot creature-battler project. This is separate.

Build Phase 0 only:
- create the repository structure
- add a minimal creator app shell
- add shared JSON schema placeholders
- add a sample project folder
- add a runtime-template placeholder
- add validation scaffolding for duplicate/missing IDs
- add tests for validation scaffolding

Do not implement battles, PokeAPI import, full map editing, or export automation yet.

Keep the code small, readable, and data-driven.

At the end, show the exact tests that pass.
```

---

## 29. Success Definition

The project is succeeding when a user can:

1. Create a new creature RPG project.
2. Import their own sprites.
3. Slice sprites into tiles/objects/characters/creatures.
4. Build a small map.
5. Define collision and encounters.
6. Create at least two creatures and two moves.
7. Create a player with a starting creature.
8. Walk around with WASD.
9. Trigger a battle.
10. Use moves with type effectiveness.
11. Use items.
12. Catch a creature.
13. Store extra creatures.
14. Save and reload.
15. Export a Windows playable game.

That is the first real target.

Everything else comes after.

