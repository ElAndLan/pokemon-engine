# IMPLEMENTATION_PLAN.md — Full Development Lifecycle

Version 2.0 — 2026-07-06
Status: Authoritative phase-by-phase guideline for the entire project, Phase 0 through
1.0 launch. Every feature the product ships appears in exactly one phase below. If a
feature isn't listed here, it isn't planned — propose it via SCOPE_GUARD.md's Idea
Ledger and amend this document by decision.

Companion docs: MASTER_PLAN.md (architecture), ARCHITECTURE_ADDENDUM.md (wins on
conflicts; battle layers v0–v6 in §8, import layers v0–v5 in §9), SCOPE_GUARD.md
(current-phase pointer), CLAUDE.md / AGENTS.md (working rules).

Conventions used below:
- **Exit gate** = ALL listed criteria true + build/tests green in CI + review pass
  (Addendum §12 cadence) logged in §Review Outcomes + SCOPE_GUARD current-phase line
  advanced. A phase is never "mostly done."
- **Testing suite** = tests that must exist and pass *by the end of that phase*; they
  remain in CI forever (regression accumulates).
- Durations assume part-time development with heavy AI assistance. They are estimates,
  not commitments; scope is fixed per phase, time flexes.

---

## Status Board

| Phase | Name | Duration est. | Status |
|---|---|---|---|
| 0 | Research & Architecture | — | **Done** (2026-07-06) |
| 1 | Scaffold & Governance | 1 wk | **Done** (2026-07-06) — GUI apps verified launching |
| 2 | Project Format, Schemas & Validation | 2 wk | **Done** (2026-07-06, 82 tests, review PASS) |
| 3 | Creator Shell & Pathfinder Editors | 2 wk | **Code complete** (120 tests) — pending manual UI run + review |
| 4 | Asset Import & Sprite Slicer | 2–3 wk | **Core complete** (151 tests) — slicing/decode/import/validation done; canvas UI deferred |
| 5 | Tileset & Map Editor | 3–4 wk | **Core complete** (172 tests) — layer ops, collision, map validation done; canvas UI deferred |
| 6 | Runtime Foundation | 2–3 wk | **In progress** — loop/camera/input/scene/viewport logic done (185 tests); GL renderer deferred |
| 7 | Playable Walking Prototype | 3 wk | **Core complete** (218 tests) — movement/mover/wander/flags/interaction done; render+dialogue UI deferred |
| 8 | Creature Data & Battle Core (v0–v1) | 3–4 wk | **Core complete** (290 tests) — deterministic 1v1 battle engine done; battle UI deferred |
| 9 | Encounters, Capture & Progression (v2) + Saves | 3 wk | **Core complete** (361 tests) — all MVP headless logic (battle+capture+exp+encounters+saves+progression) done; UI deferred |
| 10 | Inventory, Shops & Storage | 2–3 wk | **Core complete** (401 tests) — item effects, mart, box storage, repel done; bag/box UI deferred |
| 11 | Trainer Battles, Statuses & Stages (v3–v4) | 4 wk | **Core complete (510 tests)** — party switching, statuses, stat stages, confusion+flinch, sight-line trigger, trainer party validation, basic + random AI. Remaining: Creator trainer editor + battle/overworld UI (display-dependent, deferred) |
| 12 | Export Pipeline | 2–3 wk | **In progress** — atlas packer (v5) + `.cgmpack` binary + GameDb pack-vs-folder unity test done, EXPORT/ASSET specs written (531 tests). Codec = stdlib Deflate (no new dep). config.json gen + `Exporter` (validate-gate → pack + config) + `Cgm.Tools export` CLI done, verified end-to-end (544 tests). Remaining: runtime template build, icon patching, export UI, smoke test (build/CI/VM-dependent) |
| 13 | Vertical Slice & Demo Game | 4 wk (timeboxed) | **Core rules complete (620 tests)** — evolution trigger matrix, happiness (bracketed Gen rates), day/night clock, creature-center recovery, trigger/door-lock condition eval (badge gating), time-conditional encounters, animation-template clip helper, options settings + persistence. Remaining items are display/asset/integration-bound: audio playback (OpenAL), demo-game original assets, evolution/battle scenes, UI screens, input-replay integration test — need the user's machine + original art/audio. Audio crossfade helper deferred (would be a speculative fragment without the audio system). |
| 14 | Advanced Battle Effects & Smart AI (v5) | 4 wk | **Verified Core baseline (785 tests, 2026-07-09)** - v5 effect-op palette complete in Core; smart AI dispatch, score tables, memory, finite trainer healing items, bounded switching, hazard-aware/relative switch scoring, minimal repeated-move prediction, and AI-vs-AI deterministic smoke are implemented and tested. Smart-vs-Basic baseline is **69.0% @400** with Smart-vs-Smart side balance **49.2%**. Full tuning is deferred until Phase 15+ mechanics exist; display/debug-console score-table integration moves to presentation/debug tooling. |
| 15 | Abilities, Held Items, Weather & Forms (v6) | 4-6 wk | **Ready for exit review (865 tests)** - Core v6 hooks/forms, Creator authoring support, sample showcase data, export template-copy, Runtime `--smoke`, exported config/pack loading, and the playable/readable exported 3v3 showcase fight are green. Remaining gate: focused Phase 15 closeout audit/review. |
| 16 | World Depth & Eventing | 4–6 wk | Blocked on 13 |
| 17 | Creator at Scale & Data Import | 3–4 wk | Blocked on 13 |
| 18 | Presentation Polish | 3–4 wk | Blocked on 15+16 |
| 19 | Release Hardening & 1.0 Launch | 4 wk | Blocked on all |

Critical path: 1→2→3→4→5→7→8→9→11→13→…→19. Phases 6 and 12 can run parallel to editor
phases if desired; 14–17 can partially interleave after 13.

### Reconciliation note — 2026-07-08

`docs/SCOPE_GUARD.md` was stale and still pointed at Phase 4. The actual codebase is in
Phase 14: the v5 effect-op palette is implemented in Core, and the local Phase 14 smart-AI Core
slice now dispatches by trainer profile with legal move/switch actions, named score tables,
memory, and switch cooldown coverage. As of 2026-07-09, Phase 14 is accepted as a verified Core
baseline (785 tests); full AI tuning is deferred until Phase 15+ mechanics exist.

Do not treat display-dependent work as done: Runtime is a minimal Silk.NET host with headless
helpers and a readable exported showcase battle, not a full sprite-backed runtime; Creator
canvas/editor screens remain deferred where noted; export can copy a local runtime template and run
`--smoke`, but icon/metadata patching, Creator export UI, CI self-contained templates, clean-VM
testing, and presentation polish remain open.

---

## Phase 0 — Research & Architecture ✅

**Goal:** Freeze product vision, mechanics targets (Gen 3–4 fidelity), stack, and scope
law before any code. **Delivered:** MASTER_PLAN.md, ARCHITECTURE_ADDENDUM.md, CLAUDE.md,
SCOPE_GUARD.md, AGENTS.md, CODING_STANDARDS.md, TECH_STACK.md, this plan. PokeAPI
mechanics reference identified at `docs/pokeapi-results` (dev-only). **Exit gate met:**
docs internally consistent; .NET 10 decision recorded; battle layers and import layers
defined.

---

## Phase 1 — Scaffold & Governance

**Goal:** A repository where every later phase can start safely: solution compiles, CI
enforces tests, the one stack unknown (.NET 10 package compatibility) is retired, and
the governance docs constrain all future agents.

**Features/deliverables (complete list):**
1. Git repo initialized; `.gitignore` (bin/obj, exports/, templates/ blobs, .cgm-cache/).
2. `CreatureGameMaker.sln` on .NET 10 LTS: `src/Cgm.Core`, `src/Cgm.Creator`,
   `src/Cgm.Runtime`, `src/Cgm.Tools`, `tests/Cgm.Core.Tests`, `tests/Cgm.Runtime.Tests`
   — exact tree per Addendum §5. Nullable enabled + warnings-as-errors solution-wide.
3. `Cgm.Creator`: Avalonia 11 shell — window "Creature Game Maker", left nav placeholder
   panel, central tab control, bottom status bar. No functionality.
4. `Cgm.Runtime`: Silk.NET window 960×640, GL 3.3 core context, clear-color render,
   fixed 60 Hz loop driven by `FixedStepClock` (lives in Core), Esc exits, `--debug`
   logs tick/frame counts.
5. `Cgm.Core`: `FixedStepClock` (pure accumulator, 60 Hz, 5-tick clamp, interpolation
   alpha) — the only game code this phase.
6. `Cgm.Tools`: CLI stub with `--help`.
7. CI: GitHub Actions windows-latest — restore, build, test on every push/PR.
8. Docs: stubs created for all remaining specs (section headings + "TBD — Phase N");
   TECH_STACK.md version pins filled from the spike; ADRs 001–009 split into
   `docs/adr/ADR-00x.md`; README with clone-to-running instructions.

**Testing suite:**
- `FixedStepClock`: exact tick counts for synthetic elapsed sequences; clamp at 5;
  alpha in [0,1); no drift over simulated 10 minutes; zero/negative elapsed handled.
- Architecture test: `Cgm.Core` references no Avalonia/Silk/graphics assemblies.
- CI proves: fresh clone → build → test green on a runner (not just dev machine).

**Exit gate:** both apps launch and close cleanly on a machine other than the dev box
(CI artifacts or second machine); TECH_STACK.md has exact version pins; zero TODOs
referencing future phases; Prompt B review (Addendum §12) logged with go verdict.

**Do NOT build:** schemas, editors, rendering beyond clear, input mapping, any game logic.

---

## Phase 2 — Project Format, Schemas & Validation

**Goal:** The data layer the entire product stands on: every MVP entity type defined on
paper (DATA_SCHEMA.md frozen) then in code, loadable/savable as a git-friendly project
folder, with a validation framework that becomes the export gate later.

**Features/deliverables (complete list):**
1. **DATA_SCHEMA.md fully written and frozen before code** (2-day writing cap, then
   decision review): field-by-field schemas, EntityId grammar, schemaVersion/migration
   policy, project folder layout (`project.cgmproj`, `data/<category>/<id>.json`,
   `assets/`, `derived/`).
2. `EntityId` type: `category:slug` parse/format/validate, value equality, closed
   category registry (species, move, type, item, map, tileset, tile, object, sheet,
   sprite, anim, encounter, trainer, flag, box).
3. All MVP schema types as immutable records with JSON serialization (source-generated,
   byte-stable output, unknown-field tolerant): project settings, spritesheet, sprite,
   animation, tileset+tile flags, object, map (layers, entity placements), encounter
   table, species (incl. empty `forms[]` placeholder, learnset, evolutions), move
   (basic: power/accuracy/pp/priority/type/class + effect list shell), type chart, item
   (pocket/price/effects shell), trainer, story-flag declarations, save-file shell.
4. `ProjectLoader.Load/Save(folder)` round-tripping the full project; content hashing
   for asset manifest entries.
5. `SchemaVersion` + `Migrator` mechanism with a working v0→v1 walkthrough proving it.
6. Validation framework: `IValidationRule`, `ValidationIssue`, `Validator`, rule catalog.
   Initial rules (≥12): broken entity references (generic, reflection-or-registry
   driven), start map exists, start position in bounds, species stat bounds, learnset
   move refs, evolution target exists + cycle detection, encounter weights >0 and
   species refs valid, level ranges sane (min≤max≤cap), warp targets exist, trainer
   party 1–6, type chart square/complete, duplicate ID detection.
7. `Cgm.Tools validate <folder>` CLI with human-readable + `--json` output and exit codes.
8. Fixtures: `samples/fixture-min/` (minimal valid project, hand-written) and ≥6 broken
   variants under `tests/fixtures/projects/`.

**Testing suite:** round-trip byte-stability per schema type; EntityId property tests
(valid/invalid grammar, sorting, equality); one pass + one fail test per validation
rule; unknown-field tolerance; migration walkthrough; loader error cases (missing file,
malformed JSON, duplicate IDs) produce diagnostics not crashes; CLI exit-code tests.

**Exit gate:** `validate samples/fixture-min` → 0 errors; each broken fixture produces
exactly its expected issues; DATA_SCHEMA.md matches code field-for-field (review-verified,
divergence = highest-severity finding); Prompt D review logged.

**Do NOT build:** any UI, rendering, battle math beyond schema shapes, pack/binary
format, post-slice schema fields (abilities, weather, held-item battle data…).

---

## Phase 3 — Creator Shell & Pathfinder Editors

**Goal:** The Creator's skeleton and the reusable editor pattern, proven on the three
simplest editors so every subsequent editor is a copy of a known shape.

**Features/deliverables (complete list):**
1. Project lifecycle: New Project wizard (name, folder, tile size 16/32, creates
   fixture-shaped skeleton), Open (recent-projects list persisted per-user), Save,
   Save-indicator/dirty tracking, close-with-unsaved-changes prompt.
2. Shell: left nav tree populated from project categories; opening an entity opens a
   center tab (one document per entity, re-focus if already open); right inspector
   region (per-editor content); bottom validation strip.
3. **Undo/redo command stack** per document: whole-record snapshot commands; Ctrl+Z/Y;
   stack depth ≥100; dirty state derived from stack position.
4. **Live validation integration:** edits re-run relevant validators debounced (~500ms);
   strip shows issue count; clicking an issue navigates to the owning entity/tab.
5. Entity operations: create (ID prompt with grammar validation + uniqueness check),
   duplicate (new ID), delete (with reference-usage warning listing referencing
   entities), rename display-name (never ID).
6. **Pathfinder editors, fully functional:**
   - **Type chart editor:** add/remove types, N×N grid with cycle-through 0/½/1/2
     multiplier cells, row/column headers, color-coded cells.
   - **Item editor:** name, description, pocket selector, price, key-item flag,
     usability flags (field/battle), effect list using the two effect ops that exist
     (heal-hp amount, capture ballBonus) — full effect palette comes with battle layers.
   - **Move editor (basic):** name, description, type ref picker, damage class,
     power/accuracy/pp/priority numerics with schema-range enforcement, effect list
     shell (damage op only for now).
7. Reference picker control (searchable dropdown of EntityIds by category) — reused by
   every future editor.
8. CREATOR_APP_SPEC.md: shell, undo semantics, validation strip behavior, and the
   "editor template" pattern documented to match what was built.

**Testing suite:** ViewModel-level (headless) tests — undo/redo sequences restore exact
records; dirty tracking across save/undo; create/duplicate/delete with reference
warnings; debounced validation triggers; ID-grammar rejection in creation dialog;
reference picker filters correctly. Manual script: create project → add 3 types → fill
chart → make item → break a reference → see issue → fix → save → reopen → identical.

**Exit gate:** the manual script passes end-to-end; a second editor (item) was built by
copying the first (type chart) with no framework changes needed — proving the pattern;
review logged.

**Do NOT build:** slicer, map editor, creature/trainer editors, any Runtime work, docking/
multi-window, themes/settings UI.

---

## Phase 4 — Asset Import & Sprite Slicer

**Goal:** Art gets into projects: PNG import, slicing (manual + first two auto-detect
layers), naming, tagging, manual animation clips, and an asset browser. Import layers
v0–v2 + manual portion of v4 (Addendum §9).

**Features/deliverables (complete list):**
1. **PNG import:** file picker + drag-drop onto asset browser; copy into `assets/`;
   manifest entry with content hash; re-import detection (hash change ⇒ "source changed"
   badge and re-slice prompt); reject non-PNG and >4096px images with clear errors.
2. **Asset browser:** thumbnail grid of imported sheets and OGG audio; category filter;
   search by name/tag; usage lookup ("where is this used?" lists referencing entities);
   delete with usage warning; import audio (OGG) as plain assets (no editor).
3. **Slicer canvas** (the reusable zoom/pan canvas control is built HERE and must be
   designed for reuse by the map editor): checkerboard alpha background, pixel grid at
   high zoom, zoom 25%–1600%, pan, cell hover/selection highlight.
4. **Import v0 — manual grid:** numeric cellW/H, offsetX/Y, spacingX/Y; live grid
   overlay; per-cell include/exclude toggle; fully-transparent cells auto-excluded
   (overridable).
5. **Import v1 — common-size suggestion:** divisibility test {16,32,48,64}, prefer
   largest, tie-break to project tile size; suggestion chips with one-click apply;
   never auto-applies.
6. **Import v2 — transparent gutter detection:** alpha-projection histograms → band
   runs → periodicity fit for cell/spacing/margin; confidence shown; rejects fits
   explaining <90% of bands (falls back to v1).
7. **Naming & classification:** batch-name selected cells with `{n}` pattern; per-sprite
   tags; classification tag (tile / object / character / creature-front / creature-back /
   icon / ui) driving which editors list the sprite.
8. **Manual animation clips (v4-manual):** ordered multi-select → create `anim:` with
   per-frame duration (ms); clip list per sheet; preview player (play/pause/loop) —
   preview may run in the Avalonia UI (it renders authoring data, not gameplay; ADR-009
   is not violated). The 4-dir×3-frame character template helper is Phase 13 polish.
9. Slice metadata persisted to `derived/` per DATA_SCHEMA.md; sprites are projections
   of metadata (no image copies).
10. Validation rules added: sprite refs to deleted cells (error), unreferenced sprites
    (info), sheet file missing on disk (error), clip frames exist + nonzero durations.
11. ASSET_PIPELINE_SPEC.md: v0–v2 algorithms written to match implementation exactly.

**Testing suite:** fixture PNGs in `tests/fixtures/sheets/` — clean 16px grid, 32px
grid, guttered (1px and 2px, with/without margin), non-divisible dimensions (v1 must
yield "no suggestion"), gutterless (v2 must fall through), all-transparent, oversized.
Unit tests: v1 suggestion ranking table; v2 band detection + periodicity fit + <90%
rejection; auto-exclusion of transparent cells; hash-based change detection; metadata
round-trip. ViewModel tests: batch naming pattern, clip creation ordering, undo of
slice edits. Manual script: import a real free asset pack, slice, name, tag, clip a
4-frame walk, reopen project, everything intact.

**Exit gate:** manual script passes with a real-world asset pack (not just fixtures);
canvas control lives in a shared location consumable by Phase 5; review logged.

**Do NOT build:** connected-component slicing (v3 — Phase 17), atlas packing (v5 —
Phase 12), character template helper, audio editing, image editing of any kind.

---

## Phase 5 — Tileset & Map Editor

**Goal:** Authors can build the world: tilesets with gameplay flags and a full-featured
single-map editor. The most complex Creator surface — budget accordingly.

**Features/deliverables (complete list):**
1. **Tileset editor:** create tileset from tile-classified sprites; per-tile flags per
   DATA_SCHEMA.md — solid, grass (encounter terrain), water, ledge direction
   (N/S/E/W one-way), counter (talk-across); terrain tag string; multi-select flag
   editing; flag-tint preview toggle.
2. **Object editor:** multi-tile objects from object-classified sprites; footprint
   (W×H tiles); per-footprint-cell collision mask; anchor; display layer (below/above
   player); interaction hook shell (sign text now; other interactions Phase 7+).
3. **Map document:** create map (name, W×H up to 200×200, tileset refs, BGM ref
   placeholder, indoor/outdoor flag); resize with content preservation + anchor choice.
4. **Map canvas** (reuses Phase 4 canvas control): chunked tile rendering (16×16-tile
   chunks, redraw dirty chunks only — perf requirement below), grid toggle, zoom/pan.
5. **Tile tools:** palette panel (tileset tabs, multi-tile brush selection), brush,
   rectangle fill, bucket fill (contiguous, per layer), eyedropper (samples all layers /
   active layer), eraser; hold-Shift line painting.
6. **Layers:** ground, decoration-below, decoration-above; active-layer selector; dim
   inactive layers toggle.
7. **Collision overlay:** auto-derived from tile flags; per-cell paint overrides
   (force-solid / force-open / ledge-dir); distinct override visualization.
8. **Encounter-zone overlay:** paint cells with an encounter-table ref (tables authored
   Phase 9 — this phase paints refs and validates dangling ones); color per table.
9. **Entity placement (data-complete even where runtime lands later):** player start
   (exactly one per project), NPC (sprite, facing, movement type static/wander-radius/
   patrol-path drawn as waypoints, dialogue text OR trainer ref — trainer wiring
   Phase 11), warp (target map+tile via picker dialog, transition type door/edge/stairs),
   sign, item pickup (item ref, qty, one-time flag auto-generated), story trigger shell
   (tile enter → set-flag/dialogue; vocabulary grows Phase 7/16). Select/move/delete
   entities; inspector edits per-instance params.
10. **Map-level undo:** per-cell/per-stroke commands (one stroke = one undo step);
    entity ops as snapshot commands.
11. Validation rules added: warp targets exist and are in-bounds + landing tile not
    solid; player start exists exactly once and is walkable; encounter refs exist;
    NPC patrol paths in-bounds; object footprints in-bounds and non-overlapping with
    other objects' collision (warning).
12. Validation overlays in-canvas: toggle tints for solid/grass/water/ledge; red
    markers on invalid warps/refs.
13. MAP_EDITOR_SPEC.md written to match.

**Testing suite:** map model unit tests — resize preservation, layer indexing, chunk
dirty-marking; tool logic tests (headless): bucket fill contiguity/bounds, rect fill,
stroke-grouping undo; collision derivation (flags + overrides precedence); every new
validation rule pass+fail; entity placement serialization round-trip. Perf test:
200×200 map, full 3 layers — full repaint <16ms on dev machine, single-stroke edit
repaint <2ms (chunked). Manual script: author a real 2-map area (town + route) with
collision, grass zones, warps both directions, 3 NPCs, signs, pickups; reopen; identical.

**Exit gate:** the 2-map area exists in `samples/` and validates clean; perf numbers
met; undo survives a 50-operation fuzz (scripted) with model equality at each rewind;
review logged.

**Do NOT build:** autotiles, animated tiles, copy/paste stamps, multi-map edge
scrolling, minimap, event graph, in-canvas playtest.

---

## Phase 6 — Runtime Foundation

**Goal:** The engine exists: window, loop, renderer, asset loading from a raw project
folder (dev mode), and the Phase 5 map drawn at 60fps. Can start after Phase 2 in
parallel with 3–5.

**Features/deliverables (complete list):**
1. **App bootstrap:** `Cgm.Runtime --project <folder> [--spawn map_id:x,y] [--debug]`;
   friendly fatal-error dialog (missing project, GL failure) — never a bare crash.
2. **Loop:** FixedStepClock-driven 60 Hz sim / vsync render with interpolation; clamp;
   pause-on-minimize; clean shutdown.
3. **Renderer (custom, GL 3.3 core):** `IRenderer` boundary per Addendum §6; texture
   loading (StbImageSharp → GL, nearest-neighbor); **sprite batch** (single atlas-aware
   quad batcher, layer-sorted, one shader); **virtual resolution** 240×160 (project-
   configurable) with integer scaling + letterbox; camera (world offset, pixel-snapped).
4. **Tilemap renderer:** per-layer chunked static vertex buffers built at map load;
   draw order ground → deco-below → [entities Phase 7] → deco-above.
5. **Dev-mode asset/data loading:** Core `GameDb` built from raw project JSON via the
   same loader as the Creator (ADR/format unity); slice metadata → runtime texture
   regions (no atlas packing yet — one GL texture per sheet is fine now); hot-reload of
   *data* JSON on F5 (assets excluded).
6. **Input:** `GameAction` map (Up/Down/Left/Right/Confirm/Cancel/Menu/Run), keyboard
   bindings from config (WASD+arrows defaults), `IInputSource` seam, edge detection
   (pressed vs held).
7. **Scene stack:** push/pop/replace; Boot → DevSpawn scene rendering the map with a
   free-fly debug camera (no player yet).
8. **Debug overlays (dev builds):** F1 frame stats (fps, tick time, draw calls), F2
   collision tint, F3 grid.
9. ENGINE_RUNTIME_SPEC.md written to match (loop timing, renderer contract, virtual
   resolution rules, input map, scene stack).

**Testing suite:** headless-testable pieces in CI — GameDb construction from
fixture-min (all entities resolve); slice-metadata→region math; camera clamp math;
input edge-detection state machine; scene stack push/pop invariants; virtual-resolution
letterbox math table. On-machine (scripted, not CI): render the Phase 5 town map,
assert via frame-stats log — 60fps sustained 30s, draw calls ≤ layers+overlays budget
(≤16), zero GL errors (glGetError sweep per frame in debug).

**Exit gate:** Phase 5's 2-map area renders correctly (visually compared against the
Creator's canvas — same tiles same places); perf + zero-GL-error criteria met; runtime
still builds with zero references from Core to it (architecture test extended); review
logged.

**Do NOT build:** player/entities, UI kit, audio, battle scene, pack loading, save
system, D3D backend.

---

## Phase 7 — Playable Walking Prototype

**Goal:** It's a game you can walk around in: grid movement with Gen-authentic feel,
collision, warps, NPCs, dialogue, and the Creator's Playtest button. First
morale-critical playable milestone.

**Features/deliverables (complete list):**
1. **Player entity:** spawned at player start (or `--spawn`); 4-dir sprite animator
   (idle/walk × 4 facings from the project's character clips); grid `Mover` —
   tap-to-face (input <80ms turns without stepping), hold-to-walk, 0.25s/tile walk,
   0.125s/tile run (Run held), input buffering (one queued action), no mid-tile
   direction changes.
2. **Collision resolution (Core):** tile solidity + overrides, map bounds, entity
   occupancy (two entities never share a tile; moves reserve target tile), **ledges**
   (step onto ledge tile from its allowed direction ⇒ 2-tile hop, blocked otherwise),
   water blocks (surf is Phase 16), counter tiles allow interaction across but not
   movement.
3. **NPC entities:** static / wander-radius (random step every 1.5–3s within radius,
   never onto warps/triggers/occupied) / patrol-path (waypoint loop); NPCs face the
   player when talked to; movement pauses during dialogue.
4. **Interaction system:** Confirm facing an adjacent interactable (NPC, sign, pickup,
   counter-adjacent) triggers it; priority rules when stacked.
5. **UI kit v1 (runtime):** 9-slice panel, bitmap font renderer (font baked from a
   project sprite or built-in default), typewriter text reveal (per-char, Confirm to
   complete/advance), multi-page dialogue, cursor/arrow glyph. **Dialogue box** built
   on it.
6. **Item pickups:** interaction gives item (bag is Phase 10 — items accumulate in a
   minimal internal list now), sets one-time flag, despawns; flag respected on reload
   of map.
7. **Story flags v1 (Core):** flag store (bool/int), trigger shell executes set-flag +
   show-dialogue; NPC/entity visibility conditions on flags.
8. **Warps:** step-on warp ⇒ fade-out → load target map → fade-in; door/stairs/edge
   transition variants (visual polish later; fade is enough); target-map entity spawn.
9. **Map runtime lifecycle:** load/unload maps with entity instantiation; per-map state
   (picked-up flags) persists across map switches in-session.
10. **Playtest integration (Creator):** Play button (from project start) and
    Play-from-map (spawn at cursor tile) — spawns Runtime process with args; Stop kills
    it; Creator stays responsive; runtime exit reported in status bar.
11. Debug additions: F4 flag inspector, F6 free-warp menu (map list), noclip toggle.

**Testing suite (movement is Core ⇒ heavily unit-tested):** scripted movement
simulations on fixture maps — blocked by solid/entity/bounds; ledge hop from correct
side only, blocked from others; tap-vs-hold semantics (tick-level tests); buffering (one
queued, no double-steps); run speed exact tile durations in ticks; NPC wander never
violates constraints (1000-step fuzz per map, seeded); occupancy reservation (two
movers, one tile — exactly one wins deterministically); warp transitions land at exact
target facing; interaction targeting matrix (facing × adjacency × counter); flag
set/read/visibility; typewriter pagination logic (headless). Manual script: full walk
of the 2-map area — talk, read signs, pick up item once, warp both ways, run, hop ledge.

**Exit gate:** manual script passes; movement "feel" sign-off from the user against
reference footage (tap-turn, buffering, speed) — this is subjective and explicitly part
of the gate; all Core movement tests green; review logged.

**Do NOT build:** encounters, battles, menus beyond dialogue, saves, audio, bag UI,
followers, surf/bike.

---

## Phase 8 — Creature Data & Battle Core (Battle v0–v1)

**Goal:** The battle engine's foundation, correct to Gen 3–4 math, headless and
golden-tested — plus the creature/move editors and the battle scene UI to fight one
wild-style 1v1.

**Features/deliverables (complete list):**
1. **BATTLE_SYSTEM_SPEC.md formula appendix written FIRST** (before battle code):
   damage formula with exact rounding order, stat formulas (IV/EV/nature), stage
   multiplier tables, crit stages/multiplier, accuracy pipeline, exp curves (all 6),
   type-effectiveness composition — each cross-checked against two independent
   reference calculators and cited.
2. **Creature stat model (Core):** species base stats → instance stats via Gen 3+
   formulas; IVs 0–31 (generated via IRng on instance creation), EVs 0–252/510
   (storage now, gain Phase 9), 25 natures ±10%; level 1–100; exp-to-level via
   growth-rate curves.
3. **Creature instance model:** species ref, level, exp, IVs, EVs, nature, current HP,
   moves (up to 4) with PP/max-PP, happiness (base now), nickname, OT, met info, status
   slot (empty until v4), held-item slot (inert until v6, schema only).
4. **Battle v0 (per Addendum §8):** BattleState (1v1), action submit/validate,
   turn order by effective Speed (ties by IRng), damage core, faint, win/loss,
   `BattleEvent` stream, `BattleController` boundary exactly per Addendum §6.
5. **Battle v1:** type chart lookup incl. ×0 immunity and dual-type composition; STAB
   1.5; accuracy roll (null accuracy = sure hit); crit (stage table, ×2, baseline
   1/16); 85–100 roll; physical/special split per move damage class; struggle-equivalent
   when all PP exhausted (simplified: no-PP move disabled, if none usable → Struggle
   with recoil ¼ max HP).
6. **Run action:** wild-battle escape formula (Gen speed-based, attempt counter).
7. **Creature editor (Creator):** name/description; type refs (1–2); base-stat editors
   with total display; catch rate, base exp, growth rate, base happiness, EV yield,
   gender ratio; learnset grid (level→move rows, sorted, add/remove); evolution list
   (target + trigger enum + params — data-complete; execution Phase 13); sprite slots
   (front/back/icon via classified-sprite picker); cry ref slot.
8. **Move editor completed for current palette:** effect list with `damage` op params;
   crit-stage field; targeting (self/enemy — 1v1 only).
9. **Battle scene (Runtime):** transition flash from overworld (debug-key F9 triggers a
   test battle this phase; real encounters Phase 9); layout — enemy front sprite +
   name/level/HP bar, player back sprite + name/level/HP/exp bar shells; main menu
   Fight/Bag/Creature/Run (Bag/Creature stubbed with "no items/creatures" until
   9/10); move select with PP/type display; battle text from BattleEvents via UI kit;
   HP bar animated drain; faint animation (sprite drop); victory/defeat → return to
   overworld. Blackout on loss: warp to start map + full heal (center respawn Phase 13).
10. Validation rules added: species has ≥1 learnable move at ≤ starting-obtainable
    level; move refs valid; sprite slots filled for species used anywhere; type refs
    valid; base-stat bounds.

**Testing suite (the project's crown jewels start here):** stat-formula table tests
(≥30 hand-verified rows covering natures/IV/EV extremes); damage table tests (≥40
hand-verified cases: STAB on/off, dual-type ½×2 compositions, immunity, crit, min/max
rolls) cross-checked vs reference calculators; effectiveness matrix unit tests; exp
curve tables (all 6, boundary levels 1/50/100); accuracy/crit statistical tests (seeded
10k rolls within binomial tolerance); action validation matrix (illegal PP, dead
target…); Speed-tie determinism per seed; **first golden replays** (≥5: sweep, mutual
faint ordering, immunity fight, crit-heavy seed, run-away) via Verify; BattleController
API misuse tests (submit when not awaiting, advance without actions). ViewModel tests
for creature editor learnset/evolution editing + undo.

**Exit gate:** all formula tests cite their reference values in comments; goldens
committed; F9 test battle playable start-to-finish with correct visible numbers
(spot-checked vs calculator); spec-vs-code review finds zero formula divergence;
review logged.

**Do NOT build:** items in battle, capture, switching, statuses, stages, trainer AI,
multi-battle features, battle animations beyond flash/drop/HP drain.

---

## Phase 9 — Encounters, Capture & Progression (Battle v2) + Saves — MVP COMPLETE

**Goal:** The genre loop closes: walk in grass → wild battle → catch or beat → gain
exp → level up → learn moves → save. This phase ending = MVP.

**Features/deliverables (complete list):**
1. **Encounter tables (Creator):** editor per MASTER_PLAN §11 — method (grass/cave/
   tile/interact; surf/fishing deferred), slot list (species picker, weight with live
   % display, min–max level, optional time-of-day [inert until Phase 13 clock] and
   required-flag conditions), base rate; "simulate 100 encounters" preview button.
2. **Encounter runtime (Core):** per-step roll on encounter-flagged tiles using painted
   zone table; weighted slot pick → level roll → instance generation (IRng: IVs,
   nature, gender); tile-trigger and interact-trigger encounter variants; repeat
   suppression (no encounter on the same tile-entry that just resolved one).
3. **Battle v2 — items & capture:** Bag action path in battle (minimal in-battle item
   list until Phase 10 UI: potions + balls usable); heal op (clamped); **capture** —
   Gen 3/4 formula per MASTER_PLAN §8, ball bonus from item data, status bonus slot
   (1× until v4), 0–4 `CaptureShake` events, ball toss/shake/success presentation;
   caught → party if space else **auto-deposit** to first non-full box (silent, message
   states box name; box UI is Phase 10); catch adds to seen/caught record (dex counts
   only; full dex screen Phase 18).
4. **Party (Core + minimal UI):** party of 1–6; party screen v1 — list with name/level/
   HP, reorder, summary page (stats, moves, exp-to-next); Creature action in battle
   shows party (switching itself lands v3 — in wild battles Creature menu allows
   view-only this phase; document in UI).
5. **Progression:** exp award on faint (participation tracking, `baseExp·level/7`,
   trainer ×1.5 flag ready), EV gain from yields, level-up mid-battle with stat recalc,
   move-learn prompt at learnset levels (replace-move dialog when 4 known), evolution
   *detection* deferred to Phase 13 (data exists; no evolution execution yet — level-up
   only logs "wants to evolve" debug note).
6. **Starter flow:** project setting for starter party (1–6 creature specs); new game
   grants it.
7. **Save system v1 (Core):** SaveFile per Addendum §6 — versioned, player pos/map/
   facing, party instances, boxes, minimal item list, money (0 until Phase 10 economy),
   flags, encounter/dex record, RNG states, playtime; save via pause menu (Menu button:
   Save/Options-stub/Quit); single slot + automatic `save.bak`; load from title screen
   (title scene v1: New Game / Continue); `%APPDATA%/<SaveDirName>` in dev mode =
   project-local `.saves/`.
8. Validation rules added: encounter tables referenced by painted zones exist and
   non-empty; starter species valid; ball/potion items exist if referenced; save-dir
   name valid.

**Testing suite:** weighted-slot distribution (seeded 10k, χ² tolerance); level-range
inclusivity; per-step probability honored; instance generation determinism per seed;
capture formula table tests (hand-verified: full HP/1HP × ball bonuses × catch rates)
+ shake-count distribution statistical test; auto-deposit routing (party full, boxes
full ⇒ defined failure message); exp split/award tables; EV cap enforcement (510/252);
level-up stat recalc tables; move-learn prompt logic incl. 4-move replacement; save
round-trip full-state equality; save.bak preservation; load-newer-version safe failure;
corrupted-save rejection with backup intact; goldens: capture success seed, capture
break-out seed, exp/level-up battle. Manual script: new game → starter → grass →
encounter → weaken → catch → second catch with full party routes to box → level up →
learn move → save → quit → continue → state identical.

**Exit gate:** manual script passes; **MVP definition from Addendum §3 fully satisfied
in dev mode** (export comes Phase 12); statistical tests stable across 3 CI runs;
review logged.

**Do NOT build:** box UI, bag pockets UI, marts/money economy, trainer battles,
statuses, evolution execution, day/night.

---

## Phase 10 — Inventory, Shops & Storage

**Goal:** The full Pokemon-style item economy and creature storage: pockets, field item
use, marts, money, and the PC box interface.

**Features/deliverables (complete list):**
1. **Bag (Core + UI):** pocket definitions from project (default: Items / Medicine /
   Balls / Key Items; MVP fixed set per Addendum — custom pockets Phase 17); per-pocket
   item lists with counts (stack cap 999, key items uncounted); bag screen — pocket
   tabs, item list with icons/descriptions, contextual actions (Use/Give-stub/Toss with
   confirm; key items untossable).
2. **Field item use:** medicine on party member via party screen hookup (heal HP, revive
   [new op], PP restore [new op — ether-style]); "can't use that now" messaging;
   consumption rules.
3. **In-battle Bag completed:** real bag UI in battle (replaces Phase 9 minimal list),
   pocket filtering to battle-usable, use consumes turn per battle rules.
4. **Money & marts:** money on save file; mart NPC placement (Phase 5 entity gains shop
   inventory ref); shop editor (Creator): item list with prices (project price or
   override); buy/sell UI (sell = half price, key items unsellable); money display;
   insufficient-funds handling.
5. **Storage boxes (Core + UI):** box config (count, capacity 30, names) from project
   settings + storage editor screen (Creator); PC entity interaction → box UI — box
   grid with creature icons, cursor navigation, deposit/withdraw/move within+across
   boxes, party pane side-by-side, summary view, release with double-confirm; cannot
   withdraw to full party / deposit last healthy party member.
6. **Give/Take held item (schema-level):** creatures can hold an item (inert in battle
   until v6); bag Give + party Take actions.
7. Item editor completed: revive/PP-restore/repel effect ops; repel field effect
   (suppress encounters below lead's level for N steps).
8. Validation rules added: shop item refs valid + priced; box config sane (≥1 box);
   pocket assignment valid for every item.

**Testing suite:** stack/cap/toss/key-item rule matrix; pocket assignment routing; heal/
revive/PP-restore op tests (clamps, fainted-target rules — revive only on fainted);
buy/sell arithmetic incl. edge (exact funds, sell-at-999); repel counter + suppression
logic; box operations (deposit/withdraw/move/release invariants — total creature count
conserved except release; last-healthy-member protection; full-party/full-box edges);
held-item give/take round-trip; save round-trip extended (bag, money, boxes, repel
steps); goldens: in-battle potion turn, in-battle ball-from-real-bag. Manual script:
earn nothing→set debug money→buy potions/balls→catch→deposit→rearrange→withdraw→give
held item→save/load→identical.

**Exit gate:** manual script passes; box UI usable with keyboard-only navigation; no
regression in Phase 9 goldens; review logged.

**Do NOT build:** TMs/tutors, battle held-item effects, custom pockets, mart tiers,
bargaining/economy sinks.

---

## Phase 11 — Trainer Battles, Statuses & Stat Stages (Battle v3–v4)

**Goal:** Battles reach vertical-slice mechanical completeness: trainer fights with
switching and basic AI, plus the full Gen 3/4 status and stat-stage layer.

**Features/deliverables (complete list):**
1. **Trainer editor (Creator):** class name, battle sprite ref, overworld sprite ref,
   sight range (0 = interact-only), party builder (1–6: species, level, optional fixed
   moves/IVs/nature/held-item slot), AI profile (random/basic; smart is Phase 14),
   reward money (base × class multiplier or explicit), dialogue set (sight/intro/
   defeat/post-defeat-overworld), defeated-flag auto-generated.
2. **Overworld trainer trigger:** per-step sight-line check (N tiles, facing, blocked
   by solids/counters), `!` indicator (static sprite now; animated emote Phase 18),
   **instant approach** (trainer relocates adjacent + dialogue; walking approach is
   Phase 18 polish per MASTER_PLAN §8), intro dialogue → battle; defeated trainers
   don't re-trigger (flag); interact-initiated battles for 0-range trainers; post-defeat
   overworld dialogue.
3. **Battle v3:** trainer battle rules (no run — "can't escape!", no capture — block
   with message); **switching** — Creature menu now switches (uses turn), forced
   switch-select on faint (no turn loss), enemy sends next creature with preview text;
   `basic` AI (greedy max-expected-damage with immunity awareness); `random` AI;
   multi-creature enemy parties; victory → money + defeat dialogue + flag; exp
   participation across switches (already-built tracking now exercised).
4. **Battle v4:** persistent statuses — burn (1/8 residual [Gen3/4: 1/8], atk ½),
   poison (1/8), toxic (ramp n/16, counter resets on switch), paralysis (speed ¼,
   25% full-para), sleep (1–3 turns, counter on instance), freeze (20% thaw, fire
   moves thaw); one persistent status at a time; persist after battle; status cure
   items (new ops: cure-status, full-heal, full-restore) + center-heal clears (centers
   land Phase 13 — party heal via item/PC this phase); **stat stages** −6..+6 all 7
   (atk/def/spa/spd/spe/acc/eva), exact multiplier tables, message tiers ("rose
   sharply!" etc.), reset on switch; **volatile statuses:** confusion (1–4 turns, 50%
   self-hit at 40BP typeless physical), flinch (priority-dependent); **priority
   brackets** live; status/stage move effect ops (`ailment`, `statStage` with chance)
   in move editor; status icons + colored HP bar states in battle UI; capture status
   bonus now real (sleep/freeze 2×, brn/psn/par 1.5×).
5. Battle UI additions: trainer intro slide, party-preview pips, switch menu, status
   icons in party/battle/box screens.
6. Validation rules added: trainer party legality (species/moves learnable at level or
   explicitly overridden — override allowed with warning), sight range sane, dialogue
   non-empty for sighted trainers, ai profile valid.

**Testing suite:** sight-line matrix (range, blockers, facing, corner cases); defeated-
flag suppression; v3 goldens (≥8: full trainer fight with switches, faint-replace
ordering, basic-AI move choice determinism, no-run/no-catch enforcement); AI unit
tests (basic picks max damage incl. immunity avoidance; random is seed-stable);
**status matrix tests** (apply/immune-by-type [e.g. fire can't burn if typed so —
data-driven]/already-statused block/persist-post-battle/cure paths/center-heal); toxic
ramp + switch reset; sleep counter determinism; para full-stop distribution (seeded
statistical); freeze thaw incl. fire-thaw; confusion self-hit math + duration; stage
multiplier tables all 7 stats × 13 stages; stage messages; crit-ignores-stages
interaction; priority bracket ordering incl. ties; capture status-bonus table; v4
goldens (≥10 covering each status in a full battle). Manual script: route with 2
trainers + interact-only trainer → sight trigger → lose to one (blackout) → rematch
blocked check → win → money/dialogue/flag.

**Exit gate:** all v0–v4 goldens green; status behavior matches BATTLE_SYSTEM_SPEC
appendix exactly (review compares line-by-line); manual script passes; review logged.

**Do NOT build:** smart AI, advanced effect ops (v5), abilities/weather/held-items (v6),
doubles, walking approach animation.

---

## Phase 12 — Export Pipeline

**Goal:** A project becomes a standalone Windows game a stranger can run with nothing
installed. Can start after Phase 9, parallel to 10–11.

**Features/deliverables (complete list):**
1. **Pack format (`.cgmpack`):** binary per Addendum §6 — versioned header, manifest
   (PackFormatVersion, RequiredRuntimeVersion, GameName, timestamp, content hash),
   section index, zstd blobs; spec written in EXPORT_PIPELINE_SPEC.md **before**
   implementation.
2. **Import v5 — atlas packing:** skyline packer into ≤2048² RGBA atlases per category;
   sprite-region rewrite to atlas coords; overflow splits to multiple atlases.
3. **Data compilation:** validated project → ID-interned binary-friendly data section
   (same GameDb loader consumes pack or raw folder — ADR-006 unity requirement).
4. **Audio packaging:** OGG copied into pack (streaming from pack via offset reads).
5. **Runtime template build (CI):** `dotnet publish` self-contained win-x64 debug +
   release flavors → versioned artifacts in `templates/`; release template disables
   raw-folder mode + debug overlays.
6. **Icon/metadata patching:** Week-1-of-phase spike per Addendum §10 — single-file
   patching vs non-single-file apphost patching; implement the winner; game name,
   version, icon (.ico import with size validation) into the exe.
7. **Export UI (Creator):** export screen — output folder, game name/icon/version,
   debug-or-release, window title, save-dir name; progress log pane (validate →
   compile → pack → template → patch → smoke); zero-error validation hard gate
   (warnings listed, explicit override checkbox).
8. **config.json** generation per Addendum §6; runtime refuses version-mismatched pack
   with friendly dialog.
9. **Smoke test:** exported exe run with `--smoke` (boot → manifest+hash verify → load
   start map → construct battle system with first species → temp save write/read →
   exit 0); Creator reports pass/fail; failure marks export failed.
10. Output layout: `exports/<name>-<version>/` (exe, pack, config, `saves/` created on
    first run in `%APPDATA%/<SaveDirName>`); optional zip.
11. `Cgm.Tools export <project> <out>` CLI (CI exports fixture project every merge).

**Testing suite:** pack round-trip (write→read→GameDb equality vs raw-folder GameDb —
the critical unity test); manifest/hash tamper detection; version-mismatch refusal;
atlas packer property tests (no overlap, all sprites placed, overflow splitting,
determinism); region-rewrite correctness (sampled pixels identical pre/post atlas);
config generation; smoke-test contract tests (each failure mode exits nonzero with
distinct code); CI job: export fixture-min + run smoke on runner. Manual/VM ritual
(gate requirement): copy export to a clean Windows VM (no .NET, no dev tools) → runs →
plays 2 min → saves → relaunch → loads.

**Exit gate:** clean-VM ritual passes for both debug and release exports; CI export job
green; pack-vs-raw GameDb equality test green; review logged.

**Do NOT build:** installer, auto-update, code signing, single-exe embedded pack,
macOS/Linux, Steam-anything.

---

## Phase 13 — Vertical Slice & Demo Game (timeboxed 4 weeks)

**Goal:** A complete, polished-enough small game shipped via the export pipeline,
proving every system end-to-end — plus the remaining slice-scope systems (evolution,
day/night, centers, gyms, audio). **Content and integration phase; new-feature list is
closed and short.**

**Features/deliverables (complete list):**
1. **Evolution execution (Core + UI):** triggers per schema — level-up, item-use
   (evolution stones via new `evolve` item op), happiness threshold on level-up,
   time-of-day conditional, trade-flag mechanism (NPC trade / flag-based; no netcode);
   evolution scene (silhouette flash, cancellable with Cancel [B], "...huh?" on cancel);
   evolved species learnset continuation; dex records evolved form.
2. **Happiness system:** base happiness, gains (level-up, walking steps counter, item
   use) and losses (faint), Gen-approximate rates documented in spec.
3. **Day/night clock:** sim clock (real-time-anchored with save offset, project setting
   for cycle length or real-time), time-of-day encounter conditions and evolution
   conditions go live; simple palette tint outdoor evening/night (indoor flag exempts).
4. **Creature centers:** center entity/interaction → heal party (full HP/PP/status) +
   set respawn point; blackout now warps to last center (replaces start-map fallback).
5. **Gym/badge pattern:** no new engine feature — story flags + door-lock trigger
   conditions + trainer gauntlet + leader trainer awarding badge flag; badge display
   on a simple trainer-card screen (Menu addition).
6. **Audio system (Runtime):** OpenAL — streamed OGG BGM per map + battle BGM
   (wild/trainer project settings), crossfade on transition, SFX one-shots (menu
   cursor/confirm/cancel, hit, faint, ball shake/click, heal jingle, level-up jingle),
   creature cries on send-out/faint (per-species OGG ref), volume settings (Options
   menu v1: BGM/SFX sliders, text speed, persisted per-save-dir).
7. **Character animation template helper (Creator, from Phase 4 deferral):** 4-dir ×
   3-frame auto-clip from a standard sheet layout.
8. **Demo game (`samples/demo-game/`), 100% original assets:** 10 species (3-stage
   starter line ×1, 2-stage lines ×2, standalones ×3 incl. one happiness-evo and one
   stone-evo and one night-only), 30 moves exercising every implemented effect op,
   6 types with full chart, 3 maps (town + route + gym), 6 trainers + gym leader,
   2 encounter tables (day/night variance on one), mart, center, PC, ~15 min of play
   to the badge.
9. **Polish (remainder of timebox, strictly bounded):** screen transitions (battle
   swirl, warp fades), title screen with project logo slot, text-speed honored
   everywhere, bug-bash fixes. Explicitly NOT: battle move animations, walking trainer
   approach, emotes (all Phase 18).

**Testing suite:** evolution trigger matrix (each trigger type × cancel × learnset
continuation); happiness gain/loss table; clock math (offset persistence across save/
load, boundary times); time-conditional encounters/evolutions (seeded at forced clock);
respawn-point set/use; badge flag gating (door blocked→unblocked); audio unit-testable
pieces (crossfade state machine, cry lookup); options persistence; **full-game
integration test (scripted input replay):** deterministic input script plays the demo
from new-game to first badge in dev mode — asserting no crash + final state snapshot
(the ultimate regression test, maintained forever); demo project passes `validate`
with zero errors and zero warnings.

**Exit gate (the big one):** **a person who has never seen the project plays the
exported release-build demo on a clean machine from new game to badge without
instructions or crashes**; input-replay integration test green in CI; timebox respected
(cut polish, not systems, if needed); review logged. **Vertical slice complete.**

**Do NOT build:** anything from Phases 14–19; resist ALL "while we're polishing" ideas
— ledger them.

---

## Phase 14 — Advanced Battle Effects & Smart AI (Battle v5)

**Goal:** The effect-op palette covers all major move archetypes so a creator can build
a deep movepool from data alone; trainer AI becomes genuinely challenging.

**Features/deliverables (complete list):**
1. **Effect ops (each = op + resolver + editor params + tests; implemented in batches
   of ~5 with goldens per batch):** multiHit (2–5 Gen-distribution / fixed-N), drain
   (½ dealt), recoil (¼/⅓ dealt; crash-on-miss variant), flinch chance, chargeTurn
   (fly/dig-style semi-invulnerable + solar-beam-style visible charge),
   protect/detect (success-chain halving), hazards (spikes layers, rocks with
   type-scaled damage — per-side field state), fixedDamage (flat N / level-based),
   ohko (level-gated accuracy), forceSwitchOut (roar-style, trainer-random /
   wild-ends), selfDestructFaint, healFraction (½; weather-scaled slot ready),
   weatherSet (rain/sun/sand/hail — **field state + residual chip + damage modifiers
   only**; ability interactions Phase 15), multiTurnLock (thrash-style + confusion
   after), bind/trap residual, leechSeed, statusHeal-on-user, critBoost op
   (focus-energy), accuracyBypass, counterDamage (physical mirror), priority already
   live. Palette version bumped in BATTLE_SYSTEM_SPEC; palette remains **closed** —
   new ops require spec amendment.
2. **PP pressure rules:** PP-restore in battle, disable interaction (later if op absent).
3. **Smart AI (per MASTER_PLAN §8):** scoring model — true-damage-calc expected damage
   (midpoint roll), guaranteed-KO preference weighted by accuracy, immunity/resist
   avoidance, status value scaling with target HP, setup-move valuation vs passive
   opponents, hazard valuation by opponent's remaining party, voluntary switch when
   hard-countered (≤1 per 3 turns), healing-item use <25% HP from limited stock, ε≈10%
   score noise (seeded); AI sees only player-visible information; per-trainer profile
   selects random/basic/smart; scoring weights in a tunable data block (not code
   constants).
4. Debug: battle console shows AI score table per decision (dev builds).
5. Demo game gains a post-badge "expert" rematch trainer using smart AI.

**Testing suite:** per-op unit test file (documented edge cases per op: protect chain
math, hazard layer stacking + type scaling, charge-turn interruption, multi-hit crit
independence, recoil-crash on miss, counter vs special immunity…); batch goldens (≥3
full battles per op batch); AI decision tests — table-driven scenarios asserting chosen
action (KO available ⇒ takes it; immune move never chosen; switch triggers exactly at
threshold scenario; item use at HP boundary); AI determinism per seed; AI "difficulty
smoke": scripted competent-player policy beats smart-AI demo teams 40–70% of the time
across 200 seeded sims (tunable target — the "tough but fair" gate); no regression in
v0–v4 goldens; input-replay integration test still green.

**Exit gate:** every op in the amended spec implemented + tested; AI win-rate band met;
review confirms zero bespoke-move code (ops only); review logged.

**Do NOT build:** abilities, held items, weather-ability interactions, doubles.

---

## Phase 15 — Abilities, Held Items, Weather Integration & Forms (Battle v6)

**Goal:** The last mechanical tier: abilities as data-driven hooks, battle-active held
items, full weather interplay, and the forms system (Mega/Gmax-style).

**Features/deliverables (complete list):**
1. **Ability system (Core):** hook dispatcher with defined ordering — hook points:
   onSwitchIn, onModifyOutgoingDamage, onModifyIncomingDamage, onStatusAttempt
   (immunity), onEndOfTurn, onContactReceived, onWeatherChange; onModifyStat/onFaint
   remain reserved until a closed op needs those timings; ability defs = data (hook + params from a closed ability-op palette
   mirroring the move-op approach: statModify, typeDamageModify, statusImmunity,
   weatherSummon, contactChanceEffect, residualHeal…); ability editor (Creator);
   species gain ability slots (1–2 + hidden slot); instance rolls ability on creation.
2. **Held items in battle:** item effect ops for held context — endureBerry-style
   (HP-threshold trigger consumables), status-cure berries, damage-boost by type,
   choice-style lock (boost + move lock), leftovers-style residual, focus-sash-style
   survive; consume/restore rules; Give/Take already exists (Phase 10).
3. **Weather completion:** ability-summoned weather, held-item duration extension slot,
   weather-dependent op interactions (healFraction scaling, charge-skip in sun, etc. —
   the slots left ready in Phases 14 ops go live).
4. **Forms system (per MASTER_PLAN §7):** `forms[]` schema activated — stat/type/
   ability/sprite overrides + activation rule: `permanent` (variant), `battle_temporary`
   (Mega: requires creature held key item + trainer key item + once-per-battle side
   flag; reverts on battle end/faint), `battle_timed` (Gmax-style: N turns, HP
   multiplier, optional move remap, reverts), `condition` (weather/held-item auto-forms
   via hooks); transformation resolver + revert invariants; battle UI transformation
   presentation (flash + sprite swap + announcement); forms tab in creature editor;
   Mega action in battle menu when eligible.
5. Validation rules: ability refs, form override completeness (sprite required),
   held-item battle ops well-formed, once-per-battle key-item pairing.
6. Demo game showcase: 1 Mega-style form, 1 Gmax-style form, 4 abilities, 3 held items
   on the expert rematch trainer.

**Testing suite:** hook-ordering goldens (multiple abilities + items + weather firing
in one turn — order pinned in spec and asserted); per-ability-op and per-held-op unit
files; status-immunity abilities block exactly their statuses; weather-summon
precedence (later switch-in wins); form activation matrix (eligible/ineligible ×
already-used × faint-revert × battle-end-revert × timed expiry); form stat swap
correctness mid-battle (recalc vs stages interaction per spec); choice-lock legality
in action validation; berry consume-once; save round-trip with forms/abilities/held
items mid-progression; full golden refresh (intentional, documented); prior integration
replay updated.

**Exit gate:** showcase fight in exported demo works; hook ordering spec == code
(review line-check); no bespoke ability/item code (ops only); review logged.

**Do NOT build:** doubles-dependent abilities, breeding-linked abilities, item crafting.

---

## Phase 16 — World Depth & Eventing

**Goal:** Overworlds stop being MVP-simple: richer triggers/cutscene vocabulary, field
moves and traversal, animated world, and multi-map flow.

**Features/deliverables (complete list):**
1. **Event system (fixed vocabulary, NOT a scripting language — per SCOPE_GUARD):**
   trigger conditions (flag ==/≥, badge, party contains, time window, item possessed,
   facing/direction entered) + ordered action lists (show dialogue [branching
   choice→flag], set/clear/increment flag, move entity along path [player or NPC],
   face entity, warp, start trainer/wild battle, give/take item, give creature, heal
   party, play SFX/BGM override, screen shake/flash, wait N ticks, lock/unlock player
   input); event editor (Creator): condition builder + action list with reorder;
   one-shot vs repeatable; this covers cutscene needs — a visual node graph remains
   ledgered.
2. **NPC trade events:** trade action (give creature A receive creature B) satisfying
   trade-evolution triggers.
3. **Field traversal:** **surf** (badge/flag-gated field action on water edges; surf
   sprite state; water encounter method live), **fishing** (rod key items, encounter
   method live, bite minigame timing), bike/run-shoes toggle (speed states), cut-tree/
   smash-rock object states (object interaction + flag + field-move item/badge gate),
   waterfall/current tiles (auto-movement tiles incl. spin tiles).
4. **Animated tiles:** tileset frame animation (water, flowers) in editor + runtime
   chunk support.
5. **Connected maps:** edge-linked maps with seamless walk-across (load neighbor,
   camera continuity) — replaces edge-warps where authored; map connections editor UI.
6. **Repel/flag polish:** encounter-modifying field states consolidated.
7. Validation rules: event action refs, path validity, connection reciprocity +
   dimension compatibility, water-region reachability warnings.

**Testing suite:** event condition matrix; action-list execution order + input-lock
safety (player can never softlock — watchdog test: every event terminates and unlocks
in fuzzed runs); one-shot re-entry suppression; trade-evolution end-to-end; surf
mount/dismount legality matrix; fishing timing state machine; forced-movement tiles
incl. loops (spin-tile cycle detection validator); connection walk-across continuity
(position math), camera continuity; animated-tile frame timing determinism; extended
input-replay integration test (script now surfs/fishes/does a cutscene). Manual: author
a 5-map region using every feature in the demo-game expansion.

**Exit gate:** demo game expanded to a 5-map region with a scripted cutscene, a trade
NPC, surf area and fishing; no softlock found in 1-hour fuzzed-input soak (random
valid inputs, must never wedge); review logged.

**Do NOT build:** visual node-graph editor, camera cutscene keyframing, weather
overworld effects, day/night NPC schedules.

---

## Phase 17 — Creator at Scale & Data Import

**Goal:** The Creator handles real content volumes (hundreds of species/moves/maps) and
imports external data. This is where the "make a full game without writing code"
promise gets stress-tested.

**Features/deliverables (complete list):**
1. **Bulk editing:** multi-select in nav lists; bulk tag/retype/reprice operations;
   spreadsheet-style grid views for species stats and learnsets (sortable, copy/paste
   rows); find-and-replace over dialogue/descriptions; project-wide reference search.
2. **Import v3 — connected-component slicing** (per Addendum §9: flood-fill →
   bounding boxes → merge ≤2px → snap option; per-rect confirm).
3. **PokeAPI import wizard (private-use, user-initiated, clearly labeled):** maps
   PokeAPI JSON (species/moves/types/items/evolution-chains — structure per
   `docs/pokeapi-results`) onto project schemas; effect-text → effect-op suggested
   mapping table with manual confirm per move (unmappable ⇒ flagged list); imports
   data only, never art/audio/names by default (name import behind an explicit
   private-use acknowledgment); import report.
4. **Project templates:** "New from template" (blank / demo-game-derived starter kit
   with original assets); template export from any project (strips saves).
5. **Creator performance pass:** nav virtualization, lazy document loading, validation
   incremental re-run (only affected rules), project load <3s at 500 species / 800
   moves / 50 maps (synthetic big-project fixture).
6. **Creator quality-of-life:** keyboard shortcut map + palette, autosave (timed, to
   `.autosave/`, crash recovery prompt), project statistics dashboard (counts, unused
   assets, validation trend).
7. Copy/paste map stamps + map duplicate (from Phase 5 deferral).
8. **Custom pockets** (from Phase 10 deferral): pocket editor with icons/order.

**Testing suite:** big-project fixture generation tool + load/validate perf budgets in
CI; bulk-op undo (single undo step reverts whole bulk op); grid-view edit round-trips;
v3 slicer fixtures (prop sheet, noisy sheet, min-area threshold); PokeAPI mapping unit
tests against committed sample JSON (species/move/evolution-chain fixtures from
docs/pokeapi-results copied into tests/fixtures — data-only, acceptable), unmappable-
move flagging, no-asset-import guarantee test; autosave/recovery simulation; template
instantiation validates clean; incremental-validation correctness (equals full run).

**Exit gate:** import a 150-species PokeAPI subset (private test) → project validates
→ playable in dev mode; big-project perf budgets green in CI; review logged.

**Do NOT build:** cloud anything, collaboration, asset marketplace, mod support.

---

## Phase 18 — Presentation Polish

**Goal:** The exported games *feel* finished: battle presentation, overworld polish,
input breadth, and the deferred animation work from MASTER_PLAN §8.

**Features/deliverables (complete list):**
1. **Battle move animations (data-driven, not per-move code):** animation primitive
   palette — sprite projectile (A→B, arc/straight), overlay effect clip on
   target/user/field, screen shake/flash/tint, sprite lunge/recoil motion, particle
   burst (simple pooled quads), SFX cue points; per-move animation def (ordered
   primitive list) + animation editor tab in move editor + preview in a sandbox
   battle scene; default generic animation per damage class when unauthored.
2. **Trainer approach animation (from Phase 11 deferral):** animated `!` emote pop,
   trainer walks tile-by-tile to the player, camera micro-adjust; NPC emote primitive
   available to events.
3. **Overworld juice:** footstep SFX by terrain tag, grass rustle overlay on step,
   door open/close animations, reflection on water (simple flipped-sprite alpha),
   screen-edge map-name toast.
4. **Dex screen:** seen/caught list UI with sprite, dex text field (species editor
   gains dex text + height/weight flavor), sortable.
5. **Trainer card & badges screen** polish.
6. **Controller support:** gamepad via Silk.NET input (mapping UI in Options,
   hot-swap), rumble slot (optional).
7. **Battle speed option** (text+animation speed presets incl. "fast" for testing).
8. **Creator preview embedding decision executed:** playtest remains process-spawned
   (ADR-009); add "follow playtest" panel showing runtime log/screenshot stream —
   NOT an embedded viewport (re-affirm or amend ADR by decision here).
9. Accessibility pass v1: remappable everything, hold-vs-toggle run, text size option
   (virtual-res scale variants), colorblind-safe default UI palette check.

**Testing suite:** animation primitive unit tests (timing, pooling — no allocation
growth over 1000 animations [pool test]); animation def round-trip + editor undo;
default-animation fallback; approach pathing (trainer walk blocked-path fallback to
instant + warning validator); controller mapping persistence + hot-swap state machine;
dex record correctness vs capture/see events (goldens extended to assert dex events);
soak: 2-hour scripted auto-play (replay loop) with zero memory growth beyond pool
budgets (leak gate); input-replay integration tests re-recorded with animations on +
speed presets.

**Exit gate:** demo game plays with full presentation; leak/pool soak green; a
30-second gameplay capture is genuinely mistakable for a finished commercial-style
retro creature RPG (user sign-off); review logged.

**Do NOT build:** shader effects beyond tint/flash, cutscene camera keyframing, video
playback.

---

## Phase 19 — Release Hardening & 1.0 Launch

**Goal:** Theoretical launchability: the Creator itself is a product a stranger can
download, learn, build with, and export from — stable, documented, and legally clean.

**Features/deliverables (complete list):**
1. **Creator distribution:** Velopack (or MSIX — spike, decide, ADR) installer with
   updates channel; signed binaries if certificate obtained (decision + cost item);
   crash reporting (local crash-dump + "copy report" — no telemetry without opt-in).
2. **First-run experience:** welcome screen, "build your first game" interactive
   tutorial (guided: import→slice→map→creature→encounter→playtest→export, driven by
   the real UI with step highlights), sample project install.
3. **User documentation:** user manual (per-editor guides), effect-op reference,
   ability-op reference, event-action reference, export guide, FAQ; docs bundled
   in-app (Help menu) + generated static site from the same markdown.
4. **Save/version compatibility audit:** runtime save migration chain tested from
   every released save version; **project migration chain** from Phase 2's earliest
   schema to 1.0 (fixtures for each released schemaVersion); exported-game version
   independence statement (old exports keep working — templates are versioned).
5. **Legal/IP final sweep:** repo + all release artifacts scanned for official content
   (automated name/asset checklist + manual pass); PokeAPI wizard labeling reviewed;
   licenses file (all dependencies attributed); EULA/README for exported games
   (creator-owns-content statement); product name/branding final check.
6. **Stability program:** 4-week beta with external users; bug triage SLA (crash =
   fix-now); "50 exported games" stress corpus (generated variant projects) all pass
   smoke; fuzzed-project loading (malformed JSON corpus) never crashes the Creator;
   Windows version matrix pass (Win 10 19045+, Win 11).
7. **Performance final gates:** Creator big-project budgets (Phase 17) re-verified;
   runtime 60fps on a 2015-era iGPU reference machine; export of big-project <90s.
8. **1.0 release:** versioning policy doc (semver: pack format, save format, project
   schema each independently versioned), CHANGELOG, release CI pipeline (tag →
   build → sign → package → smoke → publish), announcement/readme site page.

**Testing suite:** full regression (every suite from Phases 1–18 green); migration-
chain matrix tests (projects + saves, every version → latest); installer
install/update/uninstall scripted test; tutorial completion scripted test (UI
automation happy-path); fuzz corpus (10k malformed project files — zero crashes, all
diagnostics); stress corpus export+smoke in CI (nightly); beta exit criteria — crash-
free session rate >99%, zero data-loss bugs open, all FIX-NOW findings closed.

**Exit gate — 1.0 launchable:** a stranger downloads the installer, completes the
tutorial, builds a 2-map game with a custom creature, and exports a working exe to
another clean machine — observed, without help; beta exit criteria met; legal sweep
signed off; release pipeline produced the shipping artifact end-to-end. **Launch.**

**Do NOT build (post-1.0 ledger):** macOS/Linux, localization framework, doubles,
breeding, visual event graph, plugin API, marketplace, mobile — 2.0 territory,
re-planned from a fresh document.

---

## Change Control

This plan changes by decision, not drift: propose the change, name the affected phases,
update this file + SCOPE_GUARD.md in the same change, and log it below.

## Deviations Log
- **2026-07-06 (Phase 1):** .NET 10 SDK installed to `D:\dotnet` (xcopy via dotnet-install
  script) instead of the standard MSI location, because C: has <1 GB free. Documented in
  TECH_STACK.md → "Local dev environment". CI still uses the standard setup-dotnet action.
- **2026-07-06 (Phase 1):** ADR-001–009 kept in `ARCHITECTURE_ADDENDUM.md` §2 with a single
  `docs/adr/README.md` pointer, rather than split into 9 duplicate files as the phase text said.
  Reason: Ponytail/DRY — same intent (adr/ home exists, new ADRs 010+ land there as files) with
  no duplicated source of truth.

## Phase 1 progress (2026-07-06)
Done: git init; `.gitignore` (replaced stale Godot one); `CreatureGameMaker.sln`;
`Directory.Build.props` (nullable + warnings-as-errors); `src/Cgm.Core` +
`tests/Cgm.Core.Tests` on net10.0; `FixedStepClock` (ADR-005) + 18 passing tests
(tick counts, 5-tick clamp, alpha range, 10-min no-drift, negative/NaN guards).
Update (2026-07-06, part 2): **Avalonia + Silk.NET spike passed.** Avalonia **12.0.5** and
Silk.NET **2.23.0** (Windowing/Input/OpenGL) both restore + build on net10.0. Added:
`Cgm.Creator` (Avalonia shell — nav/tab/status window), `Cgm.Runtime` (Silk.NET GL 3.3 window
+ FixedStepClock-driven 60Hz loop, clear-color, Esc-exit, `--debug` logging), `Cgm.Tools`
(CLI stub, runs). Core-purity architecture test added (now 19 tests green). Full solution
builds 0-warning under warnings-as-errors. Note: solution is `.slnx`; Avalonia moved 11→12
(TECH_STACK updated).

Update (2026-07-06, part 3): CI workflow (`.github/workflows/ci.yml`, setup-dotnet 10.0.301,
build+test the slnx); `README.md`; `run.bat` launch menu; `docs/adr/README.md` (pointer, see
deviation); 10 spec stubs created (PROJECT_OVERVIEW, ARCHITECTURE, DATA_SCHEMA, CREATOR_APP_SPEC,
ENGINE_RUNTIME_SPEC, ASSET_PIPELINE_SPEC, MAP_EDITOR_SPEC, BATTLE_SYSTEM_SPEC, EXPORT_PIPELINE_SPEC,
TESTING_STRATEGY). CLAUDE.md now mandates the Ponytail plugin on all code.

**Phase 1 remaining (only 2 items):** (1) **run-verify both GUI apps launch/close** — needs an
interactive desktop, do on the user's machine (`run.bat` → options 1 and 2); (2) the Phase 1
review pass (Addendum §12 Prompt B) → then advance SCOPE_GUARD current-phase to Phase 2.

## Phase 2 progress (2026-07-06)
Done: [ADR-010](adr/ADR-010-pokeapi-derived-schema.md) (data schema derived from the local
PokeAPI corpus — mirror useful fields, flatten `{name,url}`→local IDs, trim baggage, keep sprite
URLs as import-staging, replace prose effects with the op palette; zero network calls);
DATA_SCHEMA.md frozen v1 (all MVP entities field-by-field, EntityId grammar, folder layout,
versioning); BATTLE_DAMAGE_CALC.md written early (Gen III/IV damage pipeline, type/STAB/crit).
Code done (increment 1): `EntityId` + closed `EntityCategory` registry + JSON string converter
(value + dict-key); `CgmJson` byte-stable options (camelCase, string enums, null-omit, tolerant
reads); `Project` record + value types (§4.1); `ProjectFile` load/save; `samples/fixture-min/`;
`TestPaths` helper. **47 tests green.** Note: entity records use `IReadOnlyList`, so round-trip
tests assert serialized-string equality (record `Equals` compares lists by reference — no
EquatableArray wrapper built, YAGNI).

Code done (increment 2): renamed root record `Project`→`ProjectSettings`; added `IEntity` +
aggregate `Project` (single entity dict, `All<T>()`/`Find<T>()`/`Contains`); all MVP entity
records (TypeDef, Species+Evolution+Form, Move+Effect, Item, EncounterTable+Slot, Trainer+Party,
StoryFlag, SpriteSheet, Animation, Tileset+Tile, MapObject, Map + polymorphic MapEntity union via
STJ `[JsonPolymorphic]`); `GrowthRates` (built-in, not an entity); generic `ProjectLoader` with
folder/filename↔id integrity checks; fixture-min expanded to a full mini project (2 types, 2
moves, species, item, tileset, encounter, map w/ player-start+npc+warp). **55 tests green, full
solution builds 0-warning.** Schema clarified: growth-rate is built-in reference data; sprite/tile
are projections (no standalone files).

Code done (increment 3): validation framework (`ValidationIssue`/`ValidationSeverity`,
`IValidationRule`, `Validator`+`ValidationReport`+`DefaultRules`) and **12 rules** —
`BrokenReferenceRule` (one reflection walker resolving every `EntityId` in the graph incl.
settings, with sheet-projected sprite ids in the resolvable set), plus start-map-exists,
starter-party, growth-rate, species-types, species-stats, learnset, evolution, move,
encounter-table, trainer-party, warp-target. **71 tests green** incl. "fixture-min validates
with 0 errors" + pass/fail per rule. Softened CODING_STANDARDS "one type per file" to match
grouped practice.

Code done (increment 4 — closes Phase 2 code): `Migrator` + `IJsonMigration` (version gate wired
into the load path via `CgmJson.DeserializeVersioned`; no-op at v1, rejects too-new files; proven
by a synthetic v0→v1 walkthrough test); `cgm validate` CLI (`Cgm.Tools` → loader+validator, exit
0/1/2, `--json`, verified end-to-end); 6 committed broken fixtures under
`tests/fixtures/projects/` (4 load-but-fail-validation asserting the specific rule, 2
fail-to-load) with a data-driven integration test. **82 tests green, full solution 0-warning.**

Phase 2 exit gate: `validate samples/fixture-min` → 0 errors ✅; broken fixtures produce expected
issues ✅; DATA_SCHEMA matches code ✅. **Remaining: Phase 2 review (Addendum §12 Prompt D)** →
then advance SCOPE_GUARD to Phase 3.

## Phase 3 progress (2026-07-06)
Done: CREATOR_APP_SPEC written (shell, project lifecycle, editor pattern, undo model, validation
strip, the 3 pathfinder editors). Dependency decision: **CommunityToolkit.Mvvm 8.4.2** added
(source-gen MVVM; less boilerplate at scale than hand-rolled — recorded in TECH_STACK). Editor
infrastructure (headless, tested): `UndoStack`+`IEditCommand`+`SnapshotCommand<T>` (undo/redo,
dirty-vs-saved, max-depth trim, redo-tail clearing, change event); `ProjectSession` (editable
working copy — open, Put/Add/Remove with dirty tracking, byte-stable Save via `CgmJson.SerializeEntity`,
Snapshot→Validator). New `tests/Cgm.Creator.Tests` project (proves the Avalonia exe's non-UI logic
is testable). **94 tests green (82 Core + 12 Creator), full solution 0-warning.**

Done (increment 2): shell view-model (`MainWindowViewModel` — open/new/save, nav tree, doc tabs,
live validation strip, undo/redo commands; UI-free, 13 headless tests) via `IDialogService` seam;
the reusable single-entity editor pattern (`EditorDocument`/`EntityEditorDocument<T>` with undoable
record-snapshot edits) proven by **Move and Item editors** (VMs tested; item = copy of the move
pattern); Avalonia XAML — `MainWindow` (menu, nav TreeView, doc TabControl with VM→View
DataTemplates, status/validation strip), `MoveView`, `ItemView`, `AvaloniaDialogService`, App
wiring. Compiled bindings type-check all XAML at build. **107 tests green (82 Core + 25 Creator),
full solution 0-warning.**

Done (increment 3, closes Phase 3 code): entity **create/duplicate/delete** (slug-prompt via
`PromptWindow`; delete refuses when referenced, via reusable `EntityReferences.Collect` extracted
from the broken-ref rule); **clickable validation strip** (Expander + issue list → NavigateToIssue);
**type-chart matrix editor** (`TypeChartDocument` — cell cycle 1→2→½→0, undoable, writes the
attacker type's damage lists) + `TypeChartView`. **120 tests green (82 Core + 38 Creator), full
solution 0-warning.**

Deferred to later (per spec, not in Phase 3 done-criteria): shared reference-picker (move/item use
inline combos) and the effect-list control (no effect-op editing UI yet — lands with Battle v5/UI).
Outstanding for Phase 3 sign-off: **manual UI script** (user runs via run.bat — needs a display)
and the Phase 3 review.

## Phase 14 progress (2026-07-08)
Done: implemented the smart-AI Core slice. Files changed: `src/Cgm.Core/Battle/SmartAi.cs`,
`src/Cgm.Core/Battle/TrainerAi.cs`, `src/Cgm.Core/Battle/BattleEvents.cs`, `src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/SmartAiTests.cs`, `tests/Cgm.Core.Tests/Battle/BattleItemTests.cs`,
`docs/SCOPE_GUARD.md`, `docs/BATTLE_SYSTEM_SPEC.md`, `docs/BATTLE_AI_SPEC.md`, and this plan.
Behavior covered: `TrainerAi` profile dispatch, `SmartAiContext`, named score components for
score-table/debug visibility, seen/repeated player move memory, legal move/switch action output,
status/setup/hazard/protect/force-switch/recovery/recoil/self-KO scoring, and voluntary switch
cooldown.

Tests/checks run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green after rerun with
AppData access for Avalonia build telemetry), `D:\dotnet\dotnet.exe test tests/Cgm.Core.Tests/Cgm.Core.Tests.csproj --no-build`
(640 passed), and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (753 passed).

Remaining blockers/next task: Phase 14 review pass; demo/difficulty tuning and win-rate smoke once
demo teams are available; display/debug-console integration for score tables; trainer item use is
deferred until enemy item actions exist.

Update (2026-07-09): switch scoring fix landed. `SmartAi` now gates voluntary switches on the
relative gain over staying, then adds that gain to the best stay/move baseline for final candidate
ranking. Default `SwitchThreshold` is 100 after the corrected relative formula over-switched at 35.
Added a regression test proving the threshold is relative, not `bestMove + threshold`. Targeted
Smart AI + difficulty tests pass (31 tests).

Update (2026-07-09, part 2): minimal prediction hook landed. When `SmartAiMemory` shows the player
has repeated the same seen move, voluntary switch scoring uses that move's expected damage instead
of assuming the player's strongest possible damage every turn. This keeps Advanced fair: no selected
action read, no open-team-sheet logic, and no schema change. Added a regression test for repeated
move prediction in switch valuation.

Update (2026-07-09, part 3): tuning pass measured current Smart-vs-Basic at **69.0% @400** with
Smart-vs-Smart side balance **49.2%**. Tested switch thresholds 80/60, noise 0.03, hazard value 45,
memory-enabled benchmark wiring, and priority-KO scoring; none improved the 400-game win rate, so
none were kept. Phase 14 is roughly **90% complete for Core** and **80% complete for the whole phase**:
v5 effects and smart-AI Core are implemented and tested; remaining exit work is full Phase 14 review,
status/docs cleanup, and display/debug-console integration. Demo/asset/UI-dependent validation remains
outside the Core slice.

Update (2026-07-09, part 4): per user decision, Phase 14 is **verified for now**. We will revisit
AI tuning after Phase 15+ battle mechanics are available. Display/debug-console score-table
integration is no longer a Phase 14 gate; it belongs with later presentation/debug tooling.

## Phase 15 progress (2026-07-09)
Started the Battle v6 spec-lock slice in `docs/BATTLE_SYSTEM_SPEC.md`: hook dispatch order,
the initial closed ability-op and held-item-op palettes, weather-change precedence, form
activation/revert invariants, and the Phase 15 test contract are now pinned before schema/code
changes. Next slice: schema v2 for `ability:*`, species ability slots, active `forms[]`, and held
battle effects, with `DATA_SCHEMA.md`, migrator coverage, and serialization tests in the same
change.

Update (2026-07-09, part 2): completed the schema v2 project-data slice. Changed files:
`docs/DATA_SCHEMA.md`, `docs/AGENTS.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Model/SchemaVersions.cs`, `src/Cgm.Core/Model/EntityId.cs`,
`src/Cgm.Core/Model/Entities/Ability.cs`, `src/Cgm.Core/Model/Entities/Species.cs`,
`src/Cgm.Core/Model/Entities/Item.cs`, `src/Cgm.Core/Serialization/EntityRegistry.cs`,
`src/Cgm.Core/Serialization/Migrator.cs`, `tests/Cgm.Core.Tests/Model/EntityIdTests.cs`,
`tests/Cgm.Core.Tests/Serialization/MigratorTests.cs`,
`tests/Cgm.Core.Tests/Serialization/ProjectLoaderTests.cs`, and
`tests/Cgm.Core.Tests/Serialization/SchemaV2SerializationTests.cs`.
Behavior covered: `schemaVersion` current defaults to 2; v1 project files migrate to v2 as a
no-op; `ability:*` is a loadable entity category; ability hooks serialize; species normal/hidden
ability slots and active form records serialize; item held `battleEffects` serialize. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (791 passing tests).
Next slice: v2 validation rules for ability refs, ability hook/op shape, held-item battle-op
shape, and form completeness/activation invariants before battle resolver code.

Update (2026-07-09, part 3): completed the v2 validation slice. Changed files:
`src/Cgm.Core/Model/EntityReferences.cs`, `src/Cgm.Core/Validation/Validator.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, `docs/SCOPE_GUARD.md`, and this plan.
Behavior covered: ability hook ops are checked against the closed Phase 15 ability palette;
held-item battle effects require holdable items and closed held-op names; shared effect validation
checks chance and common numeric param ranges; form validation covers duplicate/empty form IDs,
required battle-visible sprites, type/stat override shape, activation-specific requirements, and
timed/condition incompatibilities. `EntityReferences` now walks dictionary keys and values, so
form `moveRemap` references are included in the existing broken-reference rule. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (first sandboxed run hit the known Avalonia
AppData telemetry permission issue; escalated rerun green) and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (795 passing tests).
Next slice: implement the minimal Battle v6 hook dispatcher skeleton in Core with ordering tests
for switch-in and damage hooks, without adding bespoke ability or item behavior.

Update (2026-07-09, part 4): completed the minimal Battle v6 hook dispatcher ordering slice.
Changed files: `src/Cgm.Core/Battle/BattleHookDispatcher.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookDispatcherTests.cs`, `docs/SCOPE_GUARD.md`, and this
plan. Behavior covered: a pure Core dispatcher now orders Phase 15 hook invocations for switch-in
(form, active ability, active held item, opposing ability, opposing held item, field/v5 hooks) and
damage modification (outgoing ability, outgoing held item, incoming ability, incoming held item,
field/weather), while filtering unrelated hook points. No ability/item behavior was implemented
yet, and no bespoke ability or item code was added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green) and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (798 passing tests).
Next slice: wire project ability/held-item data into battle setup minimally, then add the first
no-bespoke hook op execution tests for `weatherSummon` and `typeDamageModify`.

Update (2026-07-09, part 5): completed the first executable Battle v6 hook-op slice. Changed
files: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
`BattleCreature.FromInstance` now wires selected/default species ability hooks and held-item
`battleEffects` from `GameDb`; ability `weatherSummon` executes through the hook dispatcher on
switch-in; ability `typeDamageModify` multiplies matching damage through the shared damage-query
path; validation now requires the executable hook params pinned in `BATTLE_SYSTEM_SPEC.md`.
No named ability/item special-cases or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green) and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (801 passing tests).
Next slice: held-item hook execution for `typeDamageBoost`, then the first consumable held-item
state path for `thresholdHeal` with consume-once tests.

Update (2026-07-09, part 6): completed the first held-item execution and consumable-state slice.
Changed files: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`src/Cgm.Core/Battle/BattleEvents.cs`, `src/Cgm.Core/Battle/BattleHookDispatcher.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookDispatcherTests.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
held-item `typeDamageBoost` now has direct damage-query coverage; held-item `thresholdHeal`
routes through end-of-turn hooks, heals at or below its configured threshold, emits
`HeldItemConsumed`, and consumes once per battle after a successful heal. Validation now requires
`thresholdPercent` plus exactly one of `healAmount` or `healFractionPercent`. No named item
special-cases or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green) and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (804 passing tests);
coverage collector run via
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --collect:"XPlat Code Coverage"`
(Core line coverage 95.57%, branch coverage 88.89%).
Next slice: implement the next smallest executable Phase 15 op pair: `residualHeal` /
`residualDamage` on end-of-turn ability hooks, with hook-order and faint/full-HP edge tests.

Update (2026-07-09, part 7): completed the residual end-turn hook-op slice. Changed files:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Battle/BattleEvents.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
ability/held end-turn `residualHeal` heals a `{ num, den }` fraction and caps logged healing at
actual missing HP; `residualDamage` applies fractional chip, can faint, and later hooks for that
fainted creature are skipped by the shared hook path. Validation now requires `num` and `den` for
both residual ops. No named ability/item special-cases or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (807 passing tests), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --collect:"XPlat Code Coverage"`
(Core line coverage 95.61%, branch coverage 89.12%).
Next slice: implement the smallest status-attempt hook path for ability `statusImmunity`, with
hook-order coverage and tests for type/status immunity stacking without adding named abilities.

Update (2026-07-09, part 8): completed the status-attempt immunity slice. Changed files:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Battle/BattleHookDispatcher.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookDispatcherTests.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
`statusImmunity` runs through a status-attempt dispatcher, blocks only matching persistent
statuses before chance rolls, and leaves built-in type immunity/current-status blockers intact.
Validation now requires the `status` string param for `statusImmunity`. No named ability
special-cases or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (811 passing tests), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --collect:"XPlat Code Coverage"`
(Core line coverage 95.63%, branch coverage 89.12%).
Next slice: implement the smallest held-item status path for consumable `statusCure`, with
consume-once tests and validation for its `status` param.

Update (2026-07-09, part 9): completed the consumable held-item `statusCure` slice. Changed
files: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Battle/BattleEvents.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
held-item `statusCure` now runs through the existing end-turn held-item hook path, consumes only
after curing a matching active persistent status, emits `HeldItemConsumed` and `StatusCured`, and
does not consume on mismatched status. This slice also fixed the previous residual slice's held
`residualHeal` mapping with direct held-item coverage. Validation now requires a valid `status`
param for `statusCure` and rejects unknown status names. No named item special-cases or new
dependencies were added. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (814 passing tests), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --collect:"XPlat Code Coverage"`
(Core line coverage 95.66%, branch coverage 89.15%).
Next slice: implement consumable held-item `surviveFromFull` on the shared damage path, with
full-HP, not-full-HP, multi-hit/once-only, and validation tests.

Update (2026-07-09, part 10): completed the consumable held-item `surviveFromFull` slice.
Changed files: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
move damage now routes through a shared damage helper that applies `surviveFromFull` before HP
loss, consumes the held item once, leaves the target at 1 HP only when the hit starts from full HP,
and allows later multi-hit damage to faint after the item is consumed. Validation rejects params on
`surviveFromFull` because the op takes none. No named item special-cases or new dependencies were
added. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (817 passing tests), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build --collect:"XPlat Code Coverage"`
(Core line coverage 95.74%, branch coverage 89.35%).
Next slice: implement held-item `weatherDurationExtend` for holder-summoned weather, with default
weather duration unchanged without the item and validation for positive `turns`.

Update (2026-07-09, part 11): completed the held-item `weatherDurationExtend` slice. Changed
files: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
holder-summoned `weatherSummon` duration now adds the holder's held-item
`weatherDurationExtend.turns`; weather without the held item keeps the default duration.
Validation requires positive numeric `turns`. No named item special-cases or new dependencies
were added. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests|FullyQualifiedName~ValidationTests"` (46 passed),
and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (819 passing tests).
Next slice: implement the smallest remaining held-item op, `choiceLock`, with move-lock legality
and switch-out reset tests.

Update (2026-07-09, part 12): completed the held-item `choiceLock` slice. Changed files:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
`choiceLock` now multiplies outgoing damage for its configured damage class, records the first
chosen move, rejects later different move choices, and clears on switch-out through the existing
volatile reset path. Validation requires `damageClass` physical/special plus positive
`multiplierPercent`. No named item special-cases or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests|FullyQualifiedName~ValidationTests"` (48 passed),
and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (821 passing tests).
Next slice: implement the smallest ability stat-query op, `statModify`, with physical/special
stat query tests and validation for its params.

Update (2026-07-09, part 13): completed the ability `statModify` slice. Changed files:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
ability `statModify` now applies to outgoing and incoming damage stat queries for atk/def/spa/spd,
with multiplicative and additive modifiers composed through the existing hook dispatcher.
Validation requires a damage stat plus either `multiplierPercent` or `add`. No named ability
special-cases, schema changes, or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests|FullyQualifiedName~ValidationTests"` (50 passed),
and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (823 passing tests).
Next slice: spec-lock and implement the smallest `contactChanceEffect` execution path, including
how contact is represented on moves before resolver code changes.

Update (2026-07-09, part 14): completed the ability `contactChanceEffect` slice. Changed files:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/DATA_SCHEMA.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Model/SchemaVersions.cs`, `src/Cgm.Core/Model/Entities/Move.cs`,
`src/Cgm.Core/Serialization/Migrator.cs`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`src/Cgm.Core/Battle/MoveCompiler.cs`, `src/Cgm.Core/Battle/BattleHookDispatcher.cs`,
`src/Cgm.Core/Battle/BattleController.cs`,
`src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`,
`tests/Cgm.Core.Tests/Battle/MoveCompilerTests.cs`,
`tests/Cgm.Core.Tests/Serialization/SchemaV2SerializationTests.cs`,
`tests/Cgm.Core.Tests/Serialization/MigratorTests.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
move data now has `makesContact`, schemaVersion is v3 with a no-op v2→v3 migration,
`contactChanceEffect` runs from the defender's `onContactReceived` ability hook after a contact
move connects, and can status or stat-stage the contacting attacker. Validation requires either a
known status or a non-HP stat plus nonzero delta. No named ability special-cases or new dependencies
were added. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests|FullyQualifiedName~ValidationTests|FullyQualifiedName~MoveCompilerTests|FullyQualifiedName~SchemaV2SerializationTests|FullyQualifiedName~MigratorTests"`
(82 passed), and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (828 passing tests).
Next slice: implement `onWeatherChange` hook dispatch for weather changes, then cover weather-change
ordering with the smallest ability op that already exists.

Update (2026-07-09, part 15): completed the `onWeatherChange` hook-dispatch slice. Changed files:
`docs/SCOPE_GUARD.md`, `src/Cgm.Core/Battle/BattleHookDispatcher.cs`,
`src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookDispatcherTests.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
weather changes now dispatch `OnWeatherChange` hooks immediately after the first
`WeatherChanged` event, ordered from the side that changed weather through the existing v6
hook ordering. `weatherSummon` inside a weather-change hook can replace the weather once, with a
recursion guard to avoid same-turn weather-hook loops. No schema changes, named ability
special-cases, or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookDispatcherTests|FullyQualifiedName~BattleV6HookExecutionTests"`
(33 passed), and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (830 passing tests).
Next slice: start the form activation/revert matrix with the smallest Core-only permanent form
application path, or tighten the form battle spec first if the current runtime shape is insufficient.

Update (2026-07-09, part 16): completed the permanent-form setup slice. Changed files:
`docs/SCOPE_GUARD.md`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
`BattleCreature.FromInstance` now applies saved permanent form overrides for battle stats, types,
ability override hooks, and move remaps during battle-state construction. Non-permanent saved form
activation remains unsupported until its own resolver slice, instead of silently pretending it is
active. No schema changes, named form special-cases, or new dependencies were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (28 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (831 passing tests).
Next slice: implement the smallest condition-form re-evaluation path for weather or held-item
conditions, with HP/stat-stage invariants.

Update (2026-07-09, part 17): completed the weather-condition form re-evaluation slice. Changed
files: `docs/SCOPE_GUARD.md`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
weather-conditioned forms now re-evaluate on weather changes, weather expiry, and switch-in.
The active battle creature swaps form stats/types/ability hooks through the existing battle-state
object, preserves current HP by percentage, and leaves stat stages intact. This slice intentionally
keeps held-item condition changes and dynamic move remapping out until their own resolver path, so
no new item-state model or move-PP remap semantics were invented. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (30 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (833 passing tests).
Next slice: implement held-item condition form re-evaluation at the existing held-item consume
points, or add dynamic form move-remap semantics if that is judged the smaller remaining gap.

Update (2026-07-09, part 18): completed the held-item condition form re-evaluation slice. Changed
files: `docs/SCOPE_GUARD.md`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
condition forms can now require the active held-item ID, activate on switch-in through the same
condition-form path as weather forms, and revert when an existing consumable held-item effect fires.
HP percentage preservation and stat-stage retention continue to use the existing form application
helper. No schema changes, named item/form special-cases, new dependencies, or new held-item state
model were added. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (32 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (835 passing tests).
Next slice: add dynamic condition-form move remapping if the current battle move/PP model can support
it cleanly; otherwise start the smallest battle-temporary form activation path.

Update (2026-07-09, part 19): completed the battle-temporary form activation slice. Changed files:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`, `src/Cgm.Core/Battle/BattleEvents.cs`,
`src/Cgm.Core/Battle/BattleCreature.cs`, `src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
Core now has `ActivateForm(formId, moveIndex)` for battle-temporary forms; activation requires the
creature's required held item plus the side's required trainer key item, happens before the selected
move, sets the once-per-battle side flag, emits `FormChanged`, and reverts fainted temporary forms
through the existing form application helper. The trainer key item is required but not consumed.
No new schema fields, named form special-cases, dynamic move-remap semantics, or dependencies were
added. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (36 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (839 passing tests).
Next slice: implement battle-timed form activation/expiry using the same form application path, or
tighten dynamic form move-remap PP semantics before coding remaps.

Update (2026-07-09, part 20): completed the battle-timed form activation/expiry slice. Changed
files: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleCreature.cs`, `src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
`ActivateForm(formId, moveIndex)` now also activates battle-timed forms before the selected move,
stores their remaining turns in battle state, applies `hpMultiplierPercent` through the existing
form stat path, ticks expiry after weather end-of-turn processing, emits `FormChanged` on activation
and reversion, and reverts fainted timed forms through the same active battle-form cleanup as
temporary forms. Dynamic move-remap PP semantics remain intentionally deferred until they are
specified. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (39 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (842 passing tests).
Next slice: tighten dynamic form move-remap PP semantics in `BATTLE_SYSTEM_SPEC.md`/schema notes
before coding move remaps, or continue closing the remaining form activation matrix if a smaller
gap appears in review.

Update (2026-07-09, part 21): completed the dynamic form move-remap slice. Changed files:
`docs/BATTLE_SYSTEM_SPEC.md`, `docs/DATA_SCHEMA.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleCreature.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
form `moveRemap` now applies through the existing form application path for active condition,
battle-temporary, and battle-timed forms. The selected slot's PP remains attached to the original
move slot while the active move data is remapped, so transformed moves spend original-slot PP and
reversion preserves that spent PP. No new battle action, resolver branch, schema field, or
dependency was added. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (40 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (843 passing tests).
Next slice: review the remaining Phase 15 form activation matrix for missing revert/condition edge
cases, or start a focused Phase 15 review pass if the matrix is now complete enough to audit.

Update (2026-07-09, part 22): completed the battle-end form reversion slice. Changed files:
`docs/SCOPE_GUARD.md`, `src/Cgm.Core/Battle/BattleCreature.cs`,
`src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
temporary and timed battle forms now revert through the shared battle-end path, not only when the
transformed creature faints or a timed form expires. Party wipe, capture, and wild force-out now use
the same battle-end helper, emitting `FormChanged(..., null)` before `BattleEnded` when a side is
still transformed. No new schema fields, battle actions, events, or dependencies were added. Tests
run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (43 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (846 passing tests).
Next slice: run a focused Phase 15 review/audit of the remaining form and hook matrix before
declaring the Core v6 slice ready for broader review.

Update (2026-07-09, part 23): completed the Phase 15 hook validation reconciliation slice. Changed
files: `docs/AGENTS.md`, `docs/BATTLE_SYSTEM_SPEC.md`, `docs/DATA_SCHEMA.md`,
`docs/SCOPE_GUARD.md`, `src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, and this plan. Behavior covered:
`AbilityHookRule` now rejects deferred `OnModifyStat` and `OnFaint` hook points instead of letting
project data validate for hook timings the battle runtime does not dispatch. The battle spec,
schema notes, doc map, and Phase 15 deliverable list now match the implemented hook set:
switch-in, outgoing/incoming damage, status-attempt, end-turn, contact-received, and weather-change.
No schema field, resolver path, dependency, or bespoke ability code was added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~ValidationTests"` (28 passed), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (847 passing tests).
Next slice: continue the focused Phase 15 audit by checking hook-op placement rules and held-item
execution coverage before broader Core v6 review.

Update (2026-07-09, part 24): completed the ability assignment on generated instances slice.
Changed files: `src/Cgm.Core/Battle/InstanceGen.cs`,
`tests/Cgm.Core.Tests/Model/SaveTests.cs`, `docs/SCOPE_GUARD.md`, and this plan. Behavior covered:
`InstanceGen.Create(...)` now persists a chosen normal ability slot for newly generated creatures
using the injected RNG. It leaves ability empty when a species has no normal ability slots and does
not include hidden ability selection because no hidden-ability odds or eligibility policy is
specified. Tests run: `D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter
"FullyQualifiedName~InstanceGenTests"` (6 passed), `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx` (green), and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build`
(849 passing tests). Next slice: focused Core v6 review fixes.

Update (2026-07-09, part 25): completed the focused Core v6 review-fix slice. Changed files:
`src/Cgm.Core/Battle/BattleCreature.cs`, `src/Cgm.Core/Battle/BattleController.cs`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, `docs/SCOPE_GUARD.md`, and this plan.
Confirmed gap fixed: condition-form activation/reversion mutated Core battle state but did not emit
`FormChanged`, unlike temporary/timed form changes. `BattleCreature.ReevaluateConditionForm(...)`
now reports condition-form transitions and `BattleController` logs `FormChanged` through the shared
reevaluation path for weather changes, switch-in, held-item consumption, and timed-form expiry.
Audit found no confirmed bespoke ability/item behavior or unsupported hook dispatch in this slice.
Tests run: `D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (43 passed), `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx` (green), and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build`
(849 passing tests). Next slice: Phase 15 golden/integration coverage.

Update (2026-07-09, part 26): completed the Phase 15 golden/integration coverage slice. Changed
files: `docs/BATTLE_SYSTEM_SPEC.md`, `docs/SCOPE_GUARD.md`,
`tests/Cgm.Core.Tests/Battle/BattleV6HookExecutionTests.cs`, and this plan. Behavior covered:
added a durable event-stream integration assertion for one switch-in turn involving condition-form
checks, ability-summoned weather, weather-triggered form change, and held-item consumption. The
battle spec now documents the intentionally locked visible order:
`SwitchedIn` -> pre-hook condition-form `FormChanged` -> `WeatherChanged` -> weather-triggered
condition-form `FormChanged` -> end-turn held-item events. Tests run:
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter
"FullyQualifiedName~BattleV6HookExecutionTests"` (44 passed), `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx` (green), and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build`
(850 passing tests). Next slice: Phase 15 authoring validation hardening.

Update (2026-07-09, part 27): completed the Phase 15 authoring validation hardening slice.
Changed files: `src/Cgm.Core/Validation/Rules/BattleV6Rules.cs`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, `docs/SCOPE_GUARD.md`, and this plan.
Behavior covered: `BrokenReferenceRule` now has explicit Phase 15 coverage for species ability
slots, hidden ability, form ability override, form item gates, and condition held-item refs.
`FormRule` now rejects battle-temporary forms whose required held item exists but is not holdable,
whose required trainer item exists but is not a key item, and condition forms whose held-item
condition exists but is not holdable. Held-item battle-effect validation tests now cover the
remaining held op params and valid examples. No schema fields or runtime behavior changed. Tests
run: `D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter
"FullyQualifiedName~ValidationTests"` (29 passed), `D:\dotnet\dotnet.exe build
CreatureGameMaker.slnx` (green), and `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build`
(851 passing tests). Next slice: Creator authoring surfaces.

Update (2026-07-09, part 28): completed the Creator authoring surfaces slice. Changed files:
`src/Cgm.Creator/MainWindow.axaml`, `src/Cgm.Creator/ViewModels/MainWindowViewModel.cs`,
`src/Cgm.Creator/ViewModels/ItemDocument.cs`, `src/Cgm.Creator/ViewModels/AbilityDocument.cs`,
`src/Cgm.Creator/ViewModels/SpeciesDocument.cs`, `src/Cgm.Creator/Views/ItemView.axaml`,
`src/Cgm.Creator/Views/AbilityView.axaml`, `src/Cgm.Creator/Views/AbilityView.axaml.cs`,
`src/Cgm.Creator/Views/SpeciesView.axaml`, `src/Cgm.Creator/Views/SpeciesView.axaml.cs`,
`tests/Cgm.Creator.Tests/ViewModels/EditorDocumentTests.cs`,
`tests/Cgm.Creator.Tests/ViewModels/EntityCrudTests.cs`,
`tests/Cgm.Creator.Tests/ViewModels/MainWindowViewModelTests.cs`, `docs/SCOPE_GUARD.md`, and this
plan. Behavior covered: the Creator can now create and open ability and species documents, edit
ability hook JSON, edit species normal/hidden ability slots and forms JSON, and edit held-item
battle effects from the existing item editor. The implementation reuses the current snapshot undo,
session validation, and document/template patterns; it adds no new dependencies, schema fields, or
generic form-builder abstraction. Tests run: `D:\dotnet\dotnet.exe test
tests\Cgm.Creator.Tests\Cgm.Creator.Tests.csproj` (104 passed; first sandboxed run was blocked by
Avalonia telemetry writing under `%LOCALAPPDATA%`, then passed with approval),
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (855 passing tests). Next slice:
Battle presentation/action wiring.

Update (2026-07-09, part 29): attempted the Battle presentation/action wiring slice and stopped on
a real blocker. Trace covered `docs/ENGINE_RUNTIME_SPEC.md`, `src/Cgm.Runtime/RuntimeHost.cs`,
`src/Cgm.Runtime/Program.cs`, `src/Cgm.Runtime/Engine/*`, and
`tests/Cgm.Runtime.Tests/EngineTests.cs`. The current Runtime is still a clear-color Silk.NET host
with only headless helpers (viewport, camera, input state, scene stack). There is no battle scene,
menu/action-selection code, UI kit, gameplay event presenter, or dev-mode project loading path to
wire `ActivateForm(formId, moveIndex)` or display `FormChanged`. Implementing those foundations
inside this Phase 15 slice would be earlier-phase presentation work, not the smallest Phase 15
wiring change. No code was changed for item 6. Last verification before this blocker:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green) and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (855 passing tests). Blocker:
complete or explicitly schedule the deferred Runtime battle UI/scene/event presentation foundation
from earlier phases before item 6 can proceed.

Update (2026-07-09, part 30): completed the smallest Battle presentation/action wiring slice.
Changed files: `docs/ENGINE_RUNTIME_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Runtime/Engine/BattleScene.cs`,
`tests/Cgm.Runtime.Tests/EngineTests.cs`, and this plan. Behavior covered: Core now exposes
non-mutating action legality for UI filtering; Runtime has a headless battle scene that builds a
player action menu from legal Core actions, surfaces eligible `ActivateForm(formId, moveIndex)`
choices, submits the selected action to `BattleController`, and presents `FormChanged` through a
shared battle event presenter. This intentionally does not add GL text rendering, a UI kit, dev-mode
project loading, or new battle rules. Tests run: `D:\dotnet\dotnet.exe test
tests\Cgm.Runtime.Tests\Cgm.Runtime.Tests.csproj` (15 passed),
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (857 passing tests). Next slice:
demo showcase content.

Update (2026-07-09, part 31): completed the demo showcase content slice. Changed files:
`samples/demo-game/project.cgmproj`, `samples/demo-game/data/type/grass.json`,
`samples/demo-game/data/type/fire.json`, `samples/demo-game/data/move/leaf_jab.json`,
`samples/demo-game/data/move/cinder_burst.json`, `samples/demo-game/data/move/root_guard.json`,
`samples/demo-game/data/ability/rain_call.json`, `samples/demo-game/data/ability/ember_veil.json`,
`samples/demo-game/data/ability/thornhide.json`, `samples/demo-game/data/ability/steady_bloom.json`,
`samples/demo-game/data/item/bloom_stone.json`, `samples/demo-game/data/item/storm_band.json`,
`samples/demo-game/data/item/surge_sash.json`, `samples/demo-game/data/item/focus_badge.json`,
`samples/demo-game/data/species/asterling.json`,
`samples/demo-game/data/trainer/expert_rematch_mira.json`,
`samples/demo-game/data/sheet/showcase.json`, `samples/demo-game/data/map/showcase_room.json`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`, `docs/SCOPE_GUARD.md`, and this plan.
Behavior covered: added a small original demo project with one battle-temporary form, one
battle-timed form, four data-driven abilities, three held battle items carried by an expert rematch
trainer, and projected placeholder sprite IDs needed by validation. No official names/assets,
schema changes, new dependencies, or bespoke ability/item code were added. Tests run:
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --filter
"FullyQualifiedName~DemoGame_HasPhase15ShowcaseContent"` (1 passed),
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green), and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (858 passing tests). Next slice:
export/showcase verification.

Update (2026-07-09, part 32): attempted the export/showcase verification slice and stopped on a
real blocker. Trace covered `docs/EXPORT_PIPELINE_SPEC.md`, `docs/ENGINE_RUNTIME_SPEC.md`,
`src/Cgm.Tools/Program.cs`, `src/Cgm.Core/Serialization/Exporter.cs`,
`src/Cgm.Runtime/Program.cs`, `src/Cgm.Runtime/RuntimeHost.cs`,
`src/Cgm.Runtime/Engine/BattleScene.cs`, `tests/Cgm.Core.Tests/Serialization/ExporterTests.cs`,
`tests/Cgm.Core.Tests/Serialization/CgmPackTests.cs`, and the `templates/` output location.
The available data-only export for `samples/demo-game` succeeds and writes `game.cgmpack` plus
`config.json`, but there is no runtime template directory, no exported executable copy/patch step,
no runtime `--smoke` mode, and the Silk.NET host still does not read `config.json`/`game.cgmpack`
or present rendered gameplay. Therefore the exported-demo showcase fight path cannot be run yet.
No code was changed for item 8. Verification run: data-only export with
`D:\dotnet\dotnet.exe run --project src\Cgm.Tools -- export samples\demo-game <temp>` succeeded;
the first sandboxed `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` was blocked by Avalonia
telemetry writing under `%LOCALAPPDATA%`, then the approved build rerun passed; and
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` passed 858 tests. Blocker: complete
or explicitly schedule the deferred Phase 12/13 runtime template/export smoke/rendered runtime
loading path before Phase 15 can reach exit review.

Update (2026-07-09, part 33): resolved the export/showcase verification blocker. Changed files:
`docs/ENGINE_RUNTIME_SPEC.md`, `docs/EXPORT_PIPELINE_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Serialization/Exporter.cs`, `src/Cgm.Tools/Program.cs`, `src/Cgm.Runtime/Program.cs`,
`src/Cgm.Runtime/RuntimeHost.cs`, `src/Cgm.Runtime/Engine/BattleScene.cs`,
`src/Cgm.Runtime/Engine/ExportedGameBoot.cs`, `tests/Cgm.Core.Tests/Serialization/ExporterTests.cs`,
`tests/Cgm.Runtime.Tests/EngineTests.cs`, `templates/.gitkeep`, and this plan. Behavior covered:
standalone export now copies a runtime template folder and renames `Cgm.Runtime.exe` to the game
name, while `--data-only` keeps the old pack/config-only path; `Cgm.Tools export` can find the
local built Runtime output after `dotnet build`; Runtime `--smoke` reads exported config, verifies
pack manifest/runtime version/content hash, loads the start map, initializes the showcase battle,
and submits one legal showcase action; the Silk.NET host loads exported config/pack at normal boot
and draws a minimal nonblank battle showcase. Icon/version patching, Creator export UI,
self-contained CI template publishing, clean-VM testing, and the full sprite/text UI renderer remain
later Phase 12/18/19 work. Tests run: `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~ExporterTests"` (7 passed), `D:\dotnet\dotnet.exe test
tests\Cgm.Runtime.Tests\Cgm.Runtime.Tests.csproj --no-build --filter
"FullyQualifiedName~ExportedGameBootTests"` (2 passed), `D:\dotnet\dotnet.exe run --project
src\Cgm.Tools -- export samples\demo-game <temp>` (wrote `Demo Game.exe`, `game.cgmpack`, and
`config.json`), exported `Demo Game.exe --smoke` (passed), and `D:\dotnet\dotnet.exe test
CreatureGameMaker.slnx --no-build` (861 passing tests). Next slice: Phase 15 exit review.

Update (2026-07-09, part 34): resolved the real demo showcase fight blocker. Changed files:
`docs/ENGINE_RUNTIME_SPEC.md`, `docs/SCOPE_GUARD.md`,
`src/Cgm.Core/Battle/BattleController.cs`, `src/Cgm.Runtime/RuntimeHost.cs`,
`src/Cgm.Runtime/Engine/BattleScene.cs`, `src/Cgm.Runtime/Engine/ExportedGameBoot.cs`,
`samples/demo-game/data/move/flare_break.json`,
`samples/demo-game/data/species/asterling.json`,
`samples/demo-game/data/trainer/expert_rematch_mira.json`,
`tests/Cgm.Core.Tests/Validation/ValidationTests.cs`,
`tests/Cgm.Runtime.Tests/EngineTests.cs`, and this plan. Behavior covered: the exported demo now
boots directly into a curated 3v3 Mira showcase battle with a user-controlled flat menu for legal
moves, legal form activations, and legal switches; Smart AI chooses enemy actions from Core battle
state; Runtime maps keyboard Up/Down/Confirm into `InputState`, updates the battle scene, and
renders HP bars, party pips, selected menu, recent event log, and outcome text with GL
clear/scissor rectangles plus a tiny built-in bitmap font. The scripted demo path proves weather,
form change, switch, held-item consumption, and battle end events. This remains intentionally
below presentation polish: no sprite batch, asset-backed battle sprites, Bag/Run, capture,
overworld intro, audio, save/load, or reusable UI kit were added. Tests run:
`D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
`D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj --no-build --filter
"FullyQualifiedName~DemoGame_HasPhase15ShowcaseContent"` (1 passed),
`D:\dotnet\dotnet.exe test tests\Cgm.Runtime.Tests\Cgm.Runtime.Tests.csproj --no-build --filter
"FullyQualifiedName~BattleSceneTests|FullyQualifiedName~ExportedGameBootTests"` (8 passed),
`D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (865 passing tests),
`D:\dotnet\dotnet.exe run --project src\Cgm.Tools -- export samples\demo-game <temp>` (wrote
`Demo Game.exe`, `game.cgmpack`, and `config.json` with no validation issues), and exported
`Demo Game.exe --smoke` (passed). Next slice: Phase 15 exit review/closeout audit.
### Phase 15 remaining work priority queue (2026-07-09 audit)

The Core v6 work is green but Phase 15 is not done. Work this list in order; each item must be
the smallest spec-complete slice, with build/test green and this section updated.

1. **DONE - Ability assignment on generated instances (Core).** `InstanceGen.Create(...)` must choose and
   persist an ability for new creatures from species normal slots (and hidden slot only if the
   spec explicitly defines odds/eligibility). Add deterministic tests for seeded selection and the
   no-ability case. Do not invent hidden-ability policy silently.
2. **DONE - Focused Core v6 review fixes.** Audit hook-op placement and execution coverage against
   `BATTLE_SYSTEM_SPEC.md`: supported hook points only, no bespoke ability/item behavior, held item
   consume-once rules, weather duration extension, choice-lock reset, contact effects, condition
   forms, timed/temporary reversion, and move-remap PP behavior. Fix only confirmed gaps.
3. **DONE - Golden/integration coverage for Phase 15.** Add the smallest durable battle replay/golden or
   equivalent event-stream assertions covering multi-system hook ordering in one turn
   (ability + held item + weather + form) and document any intentional event-order lock.
4. **DONE - Phase 15 authoring validation hardening.** Fill any missing validation tests found by the
   review, especially ability refs, held-item battle op params, form override completeness, and
   once-per-battle key-item pairing. Do not add new schema fields unless `DATA_SCHEMA.md` and the
   migrator change in the same slice.
5. **DONE - Creator authoring surfaces.** Add the minimal editor support required by the phase: ability
   editor, held-item battle-effects editing in the item editor, forms tab in the creature editor,
   and validation wiring. Reuse the existing pathfinder editor/undo patterns; no new UI framework
   or generic form-builder abstraction.
6. **DONE - Battle presentation/action wiring.** Surface eligible `ActivateForm(formId, moveIndex)` from
   battle UI/menu code and display `FormChanged` through the existing battle event presentation path.
   Keep battle rules in Core; UI submits actions only. Done as a headless Runtime battle scene and
   event presenter; rendered GL text/menu drawing remains a separate Runtime display gap.
7. **DONE - Demo showcase content.** Add original, non-official demo data for one battle-temporary form,
   one battle-timed form, four abilities, and three held items on an expert rematch trainer. Keep
   sample data neutral and original; no PokeAPI names/assets in shipped samples.
8. **DONE - Export/smoke verification.** `templates/.gitkeep` now keeps the template location
   present, `Exporter` can copy and rename a runtime template folder, `Cgm.Tools export` defaults to
   a found template or local built Runtime output, Runtime supports `--smoke`, and the Silk.NET host
   loads exported `config.json`/`game.cgmpack` and renders the showcase battle. Verified with
   `samples/demo-game` exported to a temp folder, then the exported `Demo Game.exe --smoke` path.
9. **DONE - Real demo showcase fight.** Addendum v6 says Phase 15 is done when a Mega-style
   showcase fight exists in the demo. The exported demo now boots into a curated, user-controlled
   3v3 battle against Smart AI with readable HP bars, party summary, flat move/form/switch menu,
   event log, forms, weather, held-item firing, switching, fainting, and battle end coverage.
10. **NEXT - Phase 15 exit review.** Run a focused review against the full Phase 15
   deliverables and log the outcome in Review Outcomes. Only then advance `SCOPE_GUARD.md`/Status
   Board beyond Phase 15.

## Review Outcomes
- **Phase 15 audit (2026-07-09) - NO-GO, keep Phase 15 open.** Core v6 mechanics are materially
  implemented and tested: hook dispatch/execution, held-item effects, weather integration, form
  activation/reversion, schema v3 migration, validation hardening, Creator authoring surfaces, and
  exported-pack smoke coverage are green locally at 861 tests. The blocker is the demo-showcase
  gate: `samples/demo-game` contains original v6 showcase data and Runtime can smoke-submit one
  legal action, but the executable still draws only a minimal GL rectangle battle silhouette and
  has no real battle UI/text/sprite showcase. That does not satisfy Addendum v6's "Mega-style
  showcase fight in the demo" done criterion. Audit verification run:
  `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` (green),
  `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` (861 passed), and
  `Cgm.Tools export samples\demo-game <temp>` plus exported `Demo Game.exe --smoke` (passed).
  Phase 15 is not ready to close.
- **Phase 14 verification (2026-07-09) — PASS, go for Phase 15.** Core v5 effect ops and smart-AI
  baseline are accepted with 785 passing tests. The measured Smart-vs-Basic benchmark is 69.0% @400
  and Smart-vs-Smart side balance is 49.2%. Full balance tuning is explicitly deferred until the
  missing battle mechanics exist.
- **Phase 14 focused AI review (2026-07-09) — PASS for switch/prediction slices.** Checked
  `SmartAi` switch scoring, prediction memory use, and new tests against `BATTLE_AI_SPEC`.
  FIX-NOW found and fixed: the prediction regression test used duplicate move IDs via the shared
  damage helper, weakening the memory assertion. The helper now allows distinct test move IDs.
  `D:\dotnet\dotnet.exe build CreatureGameMaker.slnx` and
  `D:\dotnet\dotnet.exe test CreatureGameMaker.slnx --no-build` are green (785 tests).
- **Phase 2 review (2026-07-06) — PASS, go for Phase 3.** Evidence-based audit against
  DATA_SCHEMA + SCOPE_GUARD. No correctness bugs; no scope creep (no post-slice fields beyond the
  sanctioned empty `forms[]`; no battle/pack/UI logic in Core; `heldItem` appears only as an
  evolution condition + trainer party slot per schema); all entities are immutable `sealed record`s
  (no mutable setters); Core-purity test present; no source TODOs. Minor observations, all ACCEPT
  (non-blocking): (1) `MoveTarget`/`EncounterMethod`/`AiProfile` include values unused in MVP
  (allOpponents, water/surf, smart) — zero-logic enum values, forward-useful for import; (2)
  validation issue ordering follows file-enumeration order (non-deterministic across filesystems)
  — sort if deterministic CLI output is ever needed; (3) enum JSON is camelCase (`allOpponents`)
  vs PokeAPI kebab (`all-opponents`) — note for the Phase 17 importer to map; (4) `default(EntityId)`
  has a null slug (struct default) — handled defensively by loader integrity checks.
