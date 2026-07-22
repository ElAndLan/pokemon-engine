# DATA_SCHEMA

Status: **Schema v11 (2026-07-21)** — the single source of truth for all serialized shapes.
Derived from the local PokeAPI corpus per [ADR-010](adr/ADR-010-pokeapi-derived-schema.md).
Changes require a `schemaVersion` bump + migration + this doc edited in the same change
(CLAUDE.md §2). v3 adds the move contact marker used by Phase 15 contact hooks; v4 expands the
move target vocabulary required by Phase 15B topology; v5 adds positive species weight/height
metrics required by Phase 15C-3 formulas; v6 adds the `onTerrainChange` ability-hook enum value
required by the Phase 15E-3 terrain lifecycle; v7 adds the `onGroundedQuery` ability-hook enum value
required by the Phase 15E-3 shared grounded-state query; v8 adds stable map-entity keys, v9 closes
the world-action vocabulary, v10 records source-image dimensions, and v11 adds the
`onEscapeAttempt` ability-hook enum value. Save-file
ability/form progression remains under `saveFormatVersion`.

Scope of v11: the MVP + vertical-slice entities plus current Core and Runtime authoring data. Numbers in
`(PokeAPI: x)` note the source field.

---

## 1. Conventions
- One JSON file per entity: `data/<category>/<id-slug>.json`. UTF-8, `\n`, 2-space indent,
  **stable property order** (byte-stable output so git diffs and fixtures stay honest).
- Every file starts: `{ "schemaVersion": 11, "id": "<category:slug>", "name": "<display>", ... }`.
- Unknown fields tolerated on read (forward-compat); never written back.
- All numbers are integers unless the field says otherwise. `null` = "not applicable" (e.g. a
  status move has `power: null`), distinct from `0`.
- No network. `{name,url}` refs in the PokeAPI corpus are resolved to IDs from **local** sibling
  files at import time (ADR-010). Runtime/Creator never fetch anything.

## 2. EntityId
- Format `category:slug`. `slug` matches `^[a-z0-9_]+$`. Immutable after creation (rename edits
  `name`, never `id`). Value-equal, sortable, case-sensitive (always lower).
- **Closed category registry:** `project, type, species, move, item, ability, tileset, tile, object,
  sheet, sprite, anim, map, encounter, trainer, flag, box`. Adding a category = a schema change.
- Cross-entity references are always the string ID (`"type:fire"`), never a nested object.

## 3. Project folder layout
```
project.cgmproj              # the project root record (§4.1)
data/<category>/<slug>.json  # one file per entity
assets/                      # imported source PNG/OGG (never modified in place)
derived/                     # slicer metadata, import staging (regenerable)
.saves/                      # dev-mode saves (gitignored)
```

## 4. Entity schemas

### 4.1 project  (`project:main`, stored as `project.cgmproj`)
| field | type | notes |
|---|---|---|
| schemaVersion, id, name | | `engineVersion` string too (min runtime) |
| tileSize | int | 16 or 32 |
| startMap | id `map:*` | · `startPos` `{x,y}` · `startFacing` enum down/up/left/right |
| starterParty | id[] `species:*` | 1–6; instances rolled at new-game |
| playerSprites | `{front,back,walkClips}` | sprite/anim IDs |

- **`walkClips`** is four `anim:*` clips ordered by `Facing` (Down, Up, Left, Right). Each clip's
  **frame 0 is the standing pose**, shown by the runtime whenever the character is idle; the runtime
  advances the clip only while moving, one fixed tick at a time (deterministic, replay-safe).
| typeChart | id `... ` | implicit: the set of `type:*` entities + their relations (§4.2) |
| pockets | string[] | ordered pocket keys (default: `items,medicine,balls,key`) |
| boxes | `{count,capacity,names[]}` | default count 8, capacity 30 |
| clock | `{mode,cycleMinutes}` | mode `realtime`\|`ingame`; time-of-day support |
| encounterDefaults | `{grassRatePerStep}` | tunable, default ~0.08 |

### 4.2 type  (`type:fire`)  ← PokeAPI `type/*` `damage_relations`
| field | type | notes |
|---|---|---|
| id, name | | |
| doubleDamageTo | id[] `type:*` | ×2 (PokeAPI: `double_damage_to`) |
| halfDamageTo | id[] `type:*` | ×0.5 (`half_damage_to`) |
| noDamageTo | id[] `type:*` | ×0 immunity (`no_damage_to`) |

The full effectiveness matrix is **derived** from these lists (see BATTLE_DAMAGE_CALC.md §Type).
`*_from` lists are redundant (they're the inverse) and not stored. Type chart editor edits these.

### 4.3 species  (`species:bulbasaur`)  ← merge of PokeAPI `pokemon/*` + `pokemon-species/*`
| field | type | PokeAPI source |
|---|---|---|
| id, name | | |
| types | id[1..2] `type:*` | `pokemon.types[].type` (slot order) |
| baseStats | `{hp,atk,def,spa,spd,spe}` | `pokemon.stats[].base_stat` |
| weightHectograms | int > 0 | `pokemon.weight`; defaults to 1 for migrated v1-v4 projects |
| heightDecimeters | int > 0 | `pokemon.height`; defaults to 1 for migrated v1-v4 projects |
| evYield | `{hp,atk,def,spa,spd,spe}` | `pokemon.stats[].effort` (mostly 0/1/2/3) |
| baseExp | int | `pokemon.base_experience` |
| growthRate | id `growthrate:*`? | `pokemon-species.growth_rate` → stored as a curve key (§4.11) |
| catchRate | int 0–255 | `pokemon-species.capture_rate` |
| baseHappiness | int 0–255 | `pokemon-species.base_happiness` |
| genderFemaleEighths | int -1..8 | `pokemon-species.gender_rate` (-1 = genderless) |
| eggCycles | int | `pokemon-species.hatch_counter` (breeding is long-term; stored, unused now) |
| eggGroups | string[] | `pokemon-species.egg_groups[].name` (stored, unused now) |
| learnset | `{level:int, move:id}[]` | from `pokemon.moves` level-up entries, dedup/sorted |
| evolutions | Evolution[] | denormalized from `evolution-chain` (§4.3a) |
| abilities | id[0..2] `ability:*` | normal ability slots; empty means no authored abilities |
| hiddenAbility | id `ability:*`? | optional hidden ability slot |
| forms | Form[] | active in v2 (Phase 15) |
| sprites | `{front,back,icon}` | our `sprite:*` IDs (filled when art imported) |
| spriteUrls | `{frontDefault,backDefault,frontShiny,backShiny,officialArtwork}`? | **import-staging** PokeAPI URLs kept for later download (ADR-010); stripped once art lands |
| cry | id `sprite:*`?/path? | audio ref (optional) |

**4.3a Evolution** ← PokeAPI `evolution-chain[].evolution_details`
| field | type | PokeAPI source / notes |
|---|---|---|
| target | id `species:*` | the evolved species |
| trigger | enum | `level-up`, `use-item`, `trade`, `other` (`evolution_details.trigger`) |
| minLevel | int? | `min_level` |
| item | id `item:*`? | `item` (evolution stone) |
| heldItem | id `item:*`? | `held_item` |
| knownMove | id `move:*`? | `known_move` |
| minHappiness | int? | `min_happiness` |
| timeOfDay | enum? | `day`\|`night` (`time_of_day`) |
| location | string? | `location` (used as a flag/zone key) |
| gender | enum? | `male`\|`female` |
Unused-in-v1 conditions may be stored but the engine only executes: level, item, trade-flag,
happiness, time-of-day, known-move, held-item (per Phase 13 scope).

**4.3b Form**
| field | type | notes |
|---|---|---|
| formId | string | stable within the species; saved creatures reference this string |
| activation | enum | `permanent`\|`battleTemporary`\|`battleTimed`\|`condition` |
| statOverrides | `{hp,atk,def,spa,spd,spe}`? | full replacement stat block when present |
| typeOverrides | id[1..2] `type:*`? | full replacement type list when present |
| abilityOverride | id `ability:*`? | replaces the selected ability while in form |
| sprites | `{front,back,icon}` | required by validation when the form is battle-visible |
| requiredHeldItem | id `item:*`? | battle-temporary/condition held-item gate |
| requiredTrainerItem | id `item:*`? | battle-temporary side key-item gate |
| turns | int? | battle-timed duration |
| hpMultiplierPercent | int? | battle-timed max-HP multiplier, e.g. 200 |
| moveRemap | `{move:*: move:*}`? | optional move substitution while transformed; battle PP remains on the original move slot |
| condition | `{weather?: string, heldItem?: id}`? | condition-form trigger data |

### 4.4 move  (`move:ember`)  ← PokeAPI `move/*`
| field | type | PokeAPI source |
|---|---|---|
| id, name | | |
| type | id `type:*` | `type` |
| damageClass | enum | `physical`\|`special`\|`status` (`damage_class`) |
| power | int? | `power` (null for status) |
| accuracy | int? | `accuracy` (null = never misses) |
| pp | int | `pp` |
| priority | int -7..7 | `priority` |
| critStage | int | `meta.crit_rate` (0 baseline) |
| makesContact | bool | contact marker for `onContactReceived` hooks; default false |
| target | enum | `selected`\|`user`\|`all-opponents`\|`all-other-pokemon`\|`users-field`\|`entire-field`\|`all-allies`\|`all-pokemon`\|`ally`\|`opponents-field`\|`random-opponent`\|`selected-pokemon-me-first`\|`specific-move`\|`user-and-allies`\|`user-or-ally`\|`fainting-pokemon`; target resolution is defined by BATTLE_SYSTEM_SPEC Phase 15B |
| effects | Effect[] | composed from `meta` + `stat_changes` + `effect_chance` (§4.4a) |

**4.4a Effect** — closed op palette (full catalog in BATTLE_SYSTEM_SPEC.md; v1 subset here):
`{ op, chance?, params }`. Seeded from PokeAPI as: `damage` (always, for damaging classes);
`ailment` from `meta.ailment`+`ailment_chance`; `statStage` from `stat_changes`+`meta.stat_chance`;
`drain` from `meta.drain`; `flinch` from `meta.flinch_chance`; `heal` from `meta.healing`;
`multiHit` from `meta.min_hits/max_hits`. Ops beyond these are hand-authored (Battle v5).
`positionSwap` is a parameterless Phase 15B op and requires `target: ally`. `redirect` is a
turn-scoped Phase 15B op with required non-empty `classes` and optional `priority`,
`bypassClasses`, `tags`, and `bypassTags`; tag lists contain only `damaging`, `status`, and
`contact` and may not be empty or duplicated when supplied.
Prose `effect_entries` are discarded (ADR-010).

### 4.5 item  (`item:potion`)  ← PokeAPI `item/*`
| field | type | PokeAPI source |
|---|---|---|
| id, name | | |
| pocket | string | mapped from `category`→pocket (`healing`→medicine, balls→balls, …) |
| price | int | `cost` |
| flingPower | int? | `fling_power` (battle throw; optional) |
| consumable | bool | `attributes` contains `consumable` |
| usableInField | bool | `attributes` contains `usable-overworld` |
| usableInBattle | bool | `attributes` contains `usable-in-battle` |
| holdable | bool | `attributes` contains `holdable` |
| keyItem | bool | pocket == key / not `countable` |
| effects | Effect[] | from `short_effect`→ops (heal N, capture ballBonus, cure-status, evolve, repel) |
| battleEffects | Effect[] | held-item battle ops from BATTLE_SYSTEM_SPEC v6 (`thresholdHeal`, `statusCure`, etc.) |
| spriteUrl | string? | import-staging item sprite URL (ADR-010) |
| icon | id `sprite:*`? | our sprite once imported |

### 4.5b ability  (`ability:sturdy_root`)
| field | type | notes |
|---|---|---|
| id, name | | |
| hooks | AbilityHook[] | data-driven hooks; no bespoke ability code |

**AbilityHook**: `{ hook, effects[] }`, where `hook` is one of `onSwitchIn`,
`onModifyOutgoingDamage`, `onModifyIncomingDamage`, `onStatusAttempt`, `onEndOfTurn`,
`onContactReceived`, `onWeatherChange`, `onTerrainChange`, `onGroundedQuery`, `onEscapeAttempt`.
`onEscapeAttempt` admits only the parameterless `escapeBlock` op. `onModifyStat` and `onFaint` are reserved/deferred enum
values and are rejected by Phase 15 validation until BATTLE_SYSTEM_SPEC.md assigns closed ops to
those timings. `effects[]` uses the shared `Effect` shape with the closed ability-op palette in
BATTLE_SYSTEM_SPEC.md.

### 4.6 spritesheet (`sheet:overworld`) & 4.7 sprite (`sprite:*`) & 4.8 animation (`anim:*`)
- **spritesheet**: `{ asset: "assets/x.png", contentHash, imageW, imageH, mode: grid|rects,
  cellW, cellH, offsetX, offsetY, spacingX, spacingY, cells: [{index|rect, spriteId, tags[]}] }`.
- `asset` is **project-relative** and canonicalized by `AssetPath`: forward slashes, no absolute or
  rooted paths, no `..` segment. Authored data becomes a filesystem read and a pack section name, so
  an unsafe path is a validation error rather than something the loader resolves.
- `imageW`/`imageH` are the source image's pixel size, recorded at import (**schema v10**). Grid
  slicing needs a column count and validation needs to know a cell lies inside the image; deriving
  either would force Core to decode PNGs, which it must not do. Columns are
  `(imageW - offsetX + spacingX) / (cellW + spacingX)` — the trailing cell needs no spacing after it.
- Cell geometry is resolved by `SpriteResolver` (Core). A cell that slices outside the image is a
  `sheet-slice` validation error: it would otherwise draw neighbouring art with no diagnostic.
- **sprite / tile are not standalone files.** `sprite:*` ids are defined inside sheet `cells`
  (each cell: `{index|rect, spriteId, class, tags[]}`) and `tile` data lives inside tilesets
  (§4.9). They are projections, resolved by indexing sheets/tilesets — the loader does not read
  `data/sprite/` or `data/tile/` folders. (The categories still exist for referencing.)
- **animation**: `{ frames: [{sprite: id, ms: int}], loop: bool }`.

### 4.9 tileset (`tileset:exterior`) & tile
- **tileset**: `{ tiles: Tile[] }`. **Tile**: `{ sprite|anim: id, solid: bool, grass: bool,
  water: bool, ledge: null|up|down|left|right, counter: bool, terrainTag: string }`.

### 4.10 object (`object:small_tree`)
`{ footprint: {w,h}, collision: bool[w*h], anchor: {x,y}, layer: below|above,
   sprite|anim: id, interaction: Action[] }` — `interaction` is the closed world-action list from
§4.11b (schema v9; previously a single open string).

### 4.11 map (`map:route_001`)
| field | type | notes |
|---|---|---|
| id, name, width, height | | tiles measured in cells |
| tilesets | id[] `tileset:*` | palettes used |
| layers | `{ground:int[], decoBelow:int[], decoAbove:int[]}` | row-major tile indices, -1 = empty |
| collisionOverrides | `{index:int, value:enum}[]` | force-solid/open/ledge (sparse) |
| encounterZones | `{index:int, table:id}[]` | painted cells → `encounter:*` (sparse) |
| entities | Entity[] | §4.11a |
| bgm | string? | audio ref · `indoor` bool (time-tint exempt) |

**4.11a Entity placement** (tagged union by `kind`). Every entity carries `key`, added in
**schema v8** (2026-07-21, ENGINE_RUNTIME_SPEC 16D prerequisite):
`player-start {key,pos,facing}` · `npc {key,pos,facing,sprite,move:static|wander|patrol,
radius?/path?, dialogue?/trainer:id?}` · `warp {key,pos,target:map, targetPos,
transition:door|edge|stairs}` · `pickup {key,pos,item,qty,flag}` · `sign {key,pos,text}` ·
`trigger {key,pos,condition,actions[]}` (Phase 7+) ·
`object {key,pos,object:id}` (**schema v11**, 2026-07-22): places a multi-tile `object:*`
definition (§4.10) at a map position. Footprint, collision, anchor, and sprite live on the
definition; the placement only says which object and where. `object` is a reference and is checked
by broken-reference validation.

- **`key`** is the entity's stable identity *within its map*: non-empty, unique per map, and
  immutable once authored. Runtime, saves, and diagnostics address entities by key and never by list
  position or object identity, so reordering entities in a map file cannot silently repoint a save's
  defeated-trainer or collected-pickup flags at a different entity.
- Enforced by validation rule `map-entity-key` (error on empty or duplicate).
- **Migration v7 → v8:** entities without a key receive `{kind}_{index}` from their original list
  position, with a `_2`, `_3`… suffix only if that collides with an authored key in the same map.
  Derivation is positional and therefore identical on every machine and every run; it never depends
  on enumeration or filesystem order. Authored keys are always preserved.
**4.11b World action vocabulary** (schema **v9**, 2026-07-21, ENGINE_RUNTIME_SPEC 16D prerequisite).
`trigger.actions[]` and object `interaction[]` share one **closed** set. Runtime dispatches on `op`
and never on an arbitrary string, so authored data cannot become a script the engine interprets.

`{ op, text?, flag?, value: int = 1, entity?: id }` where `op` is exactly one of:

| op | Required | Meaning |
|---|---|---|
| `dialogue` | `text` | Show a dialogue page. Display strings are data, never IDs. |
| `setFlag` | `flag` | Set a save flag to `value`. |
| `clearFlag` | `flag` | Reset a save flag to zero. |
| `giveItem` | `entity` (`item:`), `value` > 0 | Give items through Core inventory rules. |
| `heal` | — | Restore the party, as a healing service does. |
| `startBattle` | `entity` (`trainer:`) | Request a trainer battle through the Core battle boundary. |

- Enforced by validation rule `trigger-action`: unknown op, missing required field, wrong entity
  category, missing referenced entity, or non-positive quantity are all errors. An action reaching
  Runtime is therefore already complete; Runtime never interprets or repairs one.
- **Adding a capability means adding an op and its validation**, never a new string convention and
  never a script interpreter. This is the same closed-palette rule the battle effect ops follow.
- **Migration v8 → v9:** pre-v9 trigger actions and object interactions were free strings with no
  defined meaning. Each converts to an explicit `dialogue` action carrying the original text —
  lossless and hand-correctable, and never guessing that a string meant something executable. Object
  `interaction` also changes from a single nullable string to a list.

### 4.12 encounter (`encounter:route_001_grass`)
`{ method: grass|cave|water|tile|interact, baseRate: float, slots: Slot[] }`.
**Slot**: `{ species: id, weight: int, minLevel: int, maxLevel: int, timeOfDay?: day|night,
requiredFlag?: string }`. Weights shown as % in the editor; sum need not be 100.

### 4.13 trainer (`trainer:gym_leader_flora`)
`{ class: string, battleSprite: id, overworldSprite: id, sightRange: int, aiProfile:
random|basic|smart, money: int, party: PartyMember[], dialogue: {sight,intro,defeat,postDefeat} }`.
**PartyMember**: `{ species: id, level: int, moves?: id[], ivs?: {...}, nature?: string,
heldItem?: id }` (unspecified → generated). `defeatedFlag` auto = `flag:trainer.<id>_defeated`.

### 4.14 story flags
Declared in project or inline: `{ id: "flag:story.badge_1", type: bool|int, description }`.
Referenced by triggers, NPC visibility, dialogue branches, door locks, evolution `location`.

### 4.15 growth-rate curves — **built-in reference data, not a project entity**
The six standard curves (`fast, medium-fast, medium-slow, slow, erratic, fluctuating`) are fixed
and identical across projects, so they are built into Core (`GrowthRates`), not authored per
project. A species references one by string key (`species.growthRate`), validated against the
key set. The level→exp tables (`int[101]`, exact from PokeAPI `growth-rate.levels`) are populated
when leveling is built (Phase 9); v1 stores only the valid keys. There is no `growthrate:` entity
category.

## 5. Save file (`.saves/<slot>.json`)  — instance data, NOT definitions
`{ saveFormatVersion, gameContentHash, map, pos, facing, party: CreatureInstance[],
   boxes: CreatureInstance[][], bag: {pocket: {item,count}[]}, money, flags: {id:value},
   respawn: {map,pos}, clockOffset, rngStates: {...}, playtimeSeconds, dex: {seen[],caught[]} }`.

**CreatureInstance** (runtime creature, distinct from species definition):
`{ species: id, form?: string, level, exp, ivs:{6}, evs:{6}, nature, ability?, curHp,
   status: null|burn|poison|toxic|paralysis|sleep|freeze, statusCounter, moves: {move,pp}[],
   happiness, heldItem?: id, nickname?, otName, ball?: id }`.
Volatile battle state (stat stages, confusion, flinch) is NOT saved — it lives only in BattleState.

## 5a. Runtime options (`options.json` beside the executable) — Runtime-local, NOT save data

Player input and volume preferences. Deliberately separate from saves and from project data: it is
per-installation, survives deleting a save, and **never bumps `schemaVersion`**. Versioned on its own
with `optionsVersion` (current: **1**, added 2026-07-21 for ENGINE_RUNTIME_SPEC 16C).

`{ optionsVersion: 1, keyboardBindings: {action: string[]}, gamepadBindings: {action: string[]},
   musicVolume: 0-100, sfxVolume: 0-100 }`

- Action keys are exact `GameAction` names: `Up`, `Down`, `Left`, `Right`, `Confirm`, `Cancel`,
  `Menu`, `Run`, `DebugToggle`. Input values are stable platform-adapter names (`Enter`, `ShiftLeft`,
  `FaceSouth`, `LeftStickUp`), never numeric device codes, so a profile survives driver reordering.
- Bindings are per device and ordered; the first entry is the one a rebinding UI displays.
- **Degradation is total, never partial.** Missing actions keep their defaults. Unknown action names
  and empty input lists are ignored with one warning. A duplicate binding, an `optionsVersion` newer
  than the runtime supports, unreadable JSON, or an empty document falls back to **all** defaults
  rather than guessing which half of an ambiguous file was intended. An older `optionsVersion` loads
  on a best-effort basis with a warning, missing fields taking defaults.
- Volumes clamp to 0–100 on load and on save.
- Bad options never prevent boot: `Confirm` and `Cancel` always retain at least one default input,
  so a player cannot bind themselves out of the menu that would repair the file.

Migration: no migrator runs for this file. A future `optionsVersion` 2 must keep version 1 readable
or accept the documented fallback-to-defaults path.

> Overlap note (2026-07-21): Core's `GameOptions` (§Save data, per save directory) also carries
> volume fields. The two are not yet reconciled — `GameOptions` is per-save player preference under
> Core, while this file is Runtime-local installation configuration. If both ship, the authority for
> audio volume must be stated explicitly before 16E wires the audio buses.

## 6. Versioning & migration
- `schemaVersion` (project data) and `saveFormatVersion` (saves) version independently.
- `optionsVersion` (Runtime options, §5a) versions independently of both and has no migrator.
- A shape change: bump the version, add `Migrator` step `v(n)→v(n+1)`, commit an old-shape
  fixture under `tests/fixtures/`, never delete a field (deprecate). Old saves/exports keep loading.
- v1→v2: no-op data migration. New Phase 15 fields default to empty/null and old v1 files load as
  v2 records.
- v2→v3: no-op data migration. `move.makesContact` defaults to false for old moves.
- v3→v4: no-op data migration. This is an additive target-enum expansion; every prior target
  value remains valid and older files require no rewritten fields.
- v4→v5: additive species metric migration. Species rows missing `weightHectograms` or
  `heightDecimeters` receive 1; non-species rows are unchanged.
- v5→v6: no-op data migration. `onTerrainChange` is an additive ability-hook enum value; every
  existing ability hook remains valid and older files require no rewritten fields.
- v6→v7: no-op data migration. `onGroundedQuery` is an additive ability-hook enum value; every
  existing ability hook remains valid and older files require no rewritten fields.
- v7→v8: **data-writing** migration (the first non-additive one). Placed map entities gain the
  required `key` field (§4.11a); entities without one receive `{kind}_{index}` from their original
  list position, suffixed only to avoid colliding with an authored key in the same map. Derivation
  is positional, so it is reproducible on every machine and independent of enumeration order.
  Authored keys are preserved. Non-map documents are untouched.
- v8→v9: **data-writing** migration closing the world-action vocabulary (§4.11b). Free-string
  `trigger.actions[]` entries and object `interaction` become explicit `dialogue` actions carrying
  the original text; object `interaction` also becomes a list. Lossless and hand-correctable — no
  string is ever assumed to mean something executable.
- v9→v10: no-op data migration adding `sheet.imageW`/`imageH` (§4.6). The size cannot be derived
  from the document and Core must not decode images, so migrated sheets keep `0` and the
  `sheet-slice` rule reports them as needing re-import. Inventing a size would produce cells that
  slice the wrong pixels **and pass validation**, which is strictly worse than a loud error.
- v10→v11: no-op data migration bundling two additive changes shipped together. (1) `onEscapeAttempt`
  is an additive ability-hook enum value; every existing ability hook remains valid. (2) The `object`
  map-entity placement kind (§4.11a) becomes available; a map without placed objects is unchanged.
  Both are additive, so older files require no rewritten fields.
