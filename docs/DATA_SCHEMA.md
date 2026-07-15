# DATA_SCHEMA

Status: **Schema v5 (2026-07-14)** — the single source of truth for all serialized shapes.
Derived from the local PokeAPI corpus per [ADR-010](adr/ADR-010-pokeapi-derived-schema.md).
Changes require a `schemaVersion` bump + migration + this doc edited in the same change
(CLAUDE.md §2). v3 adds the move contact marker used by Phase 15 contact hooks; v4 expands the
move target vocabulary required by Phase 15B topology; v5 adds positive species weight/height
metrics required by Phase 15C-3 formulas. Save-file
ability/form progression remains under `saveFormatVersion`.

Scope of v5: the MVP + vertical-slice entities plus Phase 15 Core authoring data. Numbers in
`(PokeAPI: x)` note the source field.

---

## 1. Conventions
- One JSON file per entity: `data/<category>/<id-slug>.json`. UTF-8, `\n`, 2-space indent,
  **stable property order** (byte-stable output so git diffs and fixtures stay honest).
- Every file starts: `{ "schemaVersion": 5, "id": "<category:slug>", "name": "<display>", ... }`.
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
`onContactReceived`, `onWeatherChange`. `onModifyStat` and `onFaint` are reserved/deferred enum
values and are rejected by Phase 15 validation until BATTLE_SYSTEM_SPEC.md assigns closed ops to
those timings. `effects[]` uses the shared `Effect` shape with the closed ability-op palette in
BATTLE_SYSTEM_SPEC.md.

### 4.6 spritesheet (`sheet:overworld`) & 4.7 sprite (`sprite:*`) & 4.8 animation (`anim:*`)
- **spritesheet**: `{ asset: "assets/x.png", contentHash, mode: grid|rects, cellW, cellH,
  offsetX, offsetY, spacingX, spacingY, cells: [{index|rect, spriteId, tags[]}] }`.
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
   sprite|anim: id, interaction: null|sign|... }` (interaction vocabulary grows Phase 7/16).

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

**4.11a Entity placement** (tagged union by `kind`):
`player-start {pos,facing}` · `npc {pos,facing,sprite,move:static|wander|patrol, radius?/path?,
dialogue?/trainer:id?}` · `warp {pos,target:map, targetPos, transition:door|edge|stairs}` ·
`pickup {pos,item,qty,flag}` · `sign {pos,text}` · `trigger {pos,condition,actions[]}` (Phase 7+).

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

## 6. Versioning & migration
- `schemaVersion` (project data) and `saveFormatVersion` (saves) version independently.
- A shape change: bump the version, add `Migrator` step `v(n)→v(n+1)`, commit an old-shape
  fixture under `tests/fixtures/`, never delete a field (deprecate). Old saves/exports keep loading.
- v1→v2: no-op data migration. New Phase 15 fields default to empty/null and old v1 files load as
  v2 records.
- v2→v3: no-op data migration. `move.makesContact` defaults to false for old moves.
- v3→v4: no-op data migration. This is an additive target-enum expansion; every prior target
  value remains valid and older files require no rewritten fields.
- v4→v5: additive species metric migration. Species rows missing `weightHectograms` or
  `heightDecimeters` receive 1; non-species rows are unchanged.
