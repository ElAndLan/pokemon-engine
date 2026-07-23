# Creature Game Maker Creator Bible

Status: **Living product vision and design direction for review**

Last updated: 2026-07-23

## 1. Purpose and authority

The Creator is not a collection of JSON forms. It is the main product: a dependable, visual,
content-authoring environment in which a person can build a complete original creature RPG without
editing source files or learning the engine's internal representation.

This Bible records:

- the long-range goal and quality bar for the Creator;
- the mental model presented to authors;
- accepted product direction that must not be lost between implementation phases;
- candidate functionality to review, refine, prioritize, and promote into executable specs;
- the intended relationship between assets, tiles, maps, game data, validation, playtesting, and
  export;
- workflow and usability requirements that turn individual editors into one coherent tool.

This document is a product-direction document, not an implementation authorization by itself.
Current phase scope and implementation order remain governed by `SCOPE_GUARD.md` and
`IMPLEMENTATION_PLAN.md`. Exact serialized shapes remain governed by `DATA_SCHEMA.md`; exact editor,
asset, and map behavior remains governed by `CREATOR_APP_SPEC.md`, `ASSET_PIPELINE_SPEC.md`, and
`MAP_EDITOR_SPEC.md`. A Bible item becomes executable work only when it is reconciled into the
owning spec and active roadmap package with migrations and tests where required.

When those documents are silent about long-range Creator intent, this Bible is the product-design
starting point. When an executable spec conflicts with an accepted direction here, the conflict
must be surfaced and deliberately reconciled rather than silently ignored.

### 1.1 Decision labels

The Bible uses four labels:

- **Accepted direction**: the user wants this capability in the Creator. Details may still require
  specification, migration design, and prioritization.
- **Candidate**: a recommended capability awaiting user review.
- **Deferred**: useful, but intentionally not part of the near-term Creator plan.
- **Rejected**: conflicts with the product, architecture, safety rules, or desired scope.

Absence of a label means the surrounding section is a design principle or quality requirement.

## 2. North-star goal

The Creator should let an author move through this complete loop without leaving the application:

1. Create or open a project safely.
2. Import and organize original visual and audio assets.
3. Turn source sheets into sprites, animations, tilesets, stamps, and reusable objects.
4. Author game data through structured, understandable editors.
5. Build maps visually at any practical size.
6. Add collision, encounters, entities, transitions, interactions, and presentation settings.
7. Validate the entire project and navigate directly to every problem.
8. Playtest from the exact location or content being edited.
9. Iterate quickly with reliable undo, recovery, and deterministic previews.
10. Export a self-contained game whose behavior matches the Creator preview.

The experience should feel closer to a focused professional level editor than a simple form-based
utility. Power must come from composition, reusable data, and strong tools, not from exposing a
general-purpose programming language.

## 3. Product principles

### 3.1 Visual first, structured underneath

Authors should manipulate meaningful things: a forest brush, a house prefab, an encounter region,
a trainer, a move effect, or a warp destination. The Creator translates those actions into stable,
validated project data. Raw JSON is a diagnostic escape hatch for developers, not a required
authoring workflow.

### 3.2 Simple first use, deep continued use

A new author should be able to paint a basic map immediately. Advanced controls should become
available through inspectors, tool options, reusable presets, and dedicated modes without crowding
the first-use surface.

### 3.3 Safe by default

The Creator must prefer reversible operations:

- every meaningful edit is undoable;
- destructive operations explain their impact before committing;
- save is transactional;
- recovery never silently replaces source data;
- references are never silently cleared or repointed;
- migrations preserve old projects or fail loudly with actionable diagnostics.

### 3.4 One concept, one representation

The UI should not use one overloaded term for several different behaviors. In particular:

- a **tile** occupies one project grid cell;
- a **stamp** paints a reusable arrangement of tiles;
- an **object prefab** is a reusable, semantically meaningful multi-cell placement;
- an **entity** is a placed gameplay or interaction instance;
- a **visual layer** contains presentation;
- a **semantic layer** contains gameplay meaning such as collision or encounters.

### 3.5 Reuse beats repetition

Anything an author will build twice should be eligible to become a reusable asset, brush, stamp,
prefab, template, palette, or preset. Reuse must retain stable references and expose usage search
before destructive changes.

### 3.6 The Creator edits; the Runtime plays

The Creator may render editor previews and deterministic visual simulations of authoring data, but
it does not run gameplay rules in-process. Playtest launches the Runtime with explicit project,
map, position, and debug configuration.

### 3.7 Content agnostic and original

The Creator is for original games. It does not ship official Pokemon assets, names, maps, cries,
music, or content packs. Its terminology, examples, templates, and generated fixtures remain
content-neutral and original.

### 3.8 Performance follows measurable budgets

Large canvases, palettes, and data sets must virtualize and cull. Performance work should be tied to
observable budgets and representative stress projects, not speculative frameworks.

## 4. Intended users and workflows

The Creator should serve several overlapping authoring styles:

- **First-time creator**: needs guided defaults, clear terminology, examples, and visible next
  actions.
- **World builder**: spends long sessions painting, selecting, stamping, arranging layers, and
  navigating maps.
- **Systems designer**: authors creatures, moves, items, encounters, trainers, and progression
  through structured data.
- **Artist/content integrator**: imports sheets, slices sprites, groups animations, assigns
  metadata, and validates presentation.
- **Tester/balancer**: jumps directly into Runtime scenarios, inspects validation and diagnostics,
  and iterates without reconstructing setup.
- **Advanced solo developer**: needs keyboard-efficient bulk operations, project-wide search,
  stable files, and reliable version-control behavior.

The interface should not require users to choose one role permanently. Workspaces and panels may
adapt to the active document while navigation and global commands remain consistent.

## 5. Creator-wide experience

### 5.1 Project hub and onboarding

**Candidate**

When no project is open, the Creator should provide:

- New Project, Open Project, and recent projects;
- missing-path and recovery status on recent entries;
- optional starter templates containing only original, redistributable content;
- a short project checklist: import assets, create a tileset, create a map, set player start,
  validate, playtest;
- links to the relevant in-app help for the currently selected workflow;
- clear separation between creating a blank project and opening an example.

Project creation should collect only decisions that genuinely affect the project contract, such as
name and base tile size. Advanced defaults belong in project settings and can be changed later when
safe.

### 5.2 Workspace layout

The primary workspace should retain a stable structure:

- project/navigation tree;
- center document tabs;
- contextual inspector or properties panel;
- validation/issues area;
- command bar and active-tool options;
- optional asset palette, layers, history, and minimap panels.

**Candidate**

Panels should be hideable, resizable, and resettable to a known layout. The product should use its
existing Avalonia layout rather than add a general docking framework. A small set of built-in
workspace presets is sufficient:

- General;
- World Building;
- Assets and Animation;
- Game Data;
- Validation and Testing.

### 5.3 Navigation and command consistency

Every editor should follow the same interaction vocabulary:

- double-click opens;
- Ctrl+S saves;
- Ctrl+Z/Ctrl+Y undo and redo;
- Ctrl+F searches the active surface;
- project-wide search has a separate command;
- Delete previews impact when references or placed content are involved;
- Escape cancels the active gesture or closes a transient picker;
- validation issues navigate to the owning document and field or map coordinate;
- command availability and reasons for disabled commands are visible.

The navigation tree should support:

- category counts;
- search/filter;
- favorites;
- recent documents;
- context actions;
- broken-reference and validation badges;
- drag reordering only where order is semantically meaningful;
- stable display labels without changing immutable IDs.

### 5.4 Search and command palette

**Candidate**

A project-wide command/search surface should find:

- entities by display name or stable ID;
- maps, regions, coordinates, and placed entity keys;
- sprites, animations, tilesets, stamps, and prefabs;
- fields containing a value;
- validation issues;
- usages of a selected asset or entity;
- Creator commands such as Validate, Playtest Here, or Open Project Settings.

Results should state why they matched and navigate directly to the most precise available target.

### 5.5 Contextual help

**Candidate**

The Creator should explain unfamiliar fields without requiring an external manual:

- concise tooltips;
- examples that use original neutral content;
- range and unit labels;
- links from validation messages to the relevant concept;
- first-use callouts that can be dismissed and reset;
- a searchable glossary for tile, stamp, object, entity, layer, collision, encounter, anchor, and
  other foundational terms.

Help must not cover errors by silently correcting data. Validation should remain explicit.

## 6. Reliability, lifecycle, and history

The existing transactional save, recovery snapshot, project lock, dirty-state, reference search,
and grouped undo contracts are foundational and remain mandatory.

### 6.1 Undo and history

**Accepted direction**

Every user gesture should be represented at the level the user understands:

- one paint stroke is one history entry;
- one stamp placement is one history entry;
- a rectangle or bucket fill is one history entry;
- a multi-layer stamp is one grouped history entry;
- moving a selection across layers is one grouped history entry;
- replacing references and deleting the original is one grouped history entry;
- accepting a slice proposal is one history entry.

**Candidate**

Expose a readable History panel with entries such as:

- `Painted 42 cells on Ground`;
- `Placed Oak House at (128, -32)`;
- `Changed collision on 12 cells`;
- `Moved North Gate group`;
- `Replaced 8 usages of object:old_tree`.

Selecting an older entry may preview the affected document, but arbitrary history jumping should
not ship until branching and dirty-state semantics are precisely defined. Ordinary sequential
undo/redo remains the reliable baseline.

### 6.2 External changes and version control

**Candidate**

The Creator should detect when project files change outside the application:

- unchanged local document: offer reload;
- locally dirty document: show a conflict and offer compare, keep local, or reload;
- deleted referenced file: surface an error without silently dropping the in-memory entity;
- asset bytes changed externally: report hash mismatch and route to reimport.

Project files should remain deterministic, human-diffable where practical, and stable in ordering.
The Creator does not need to become a Git client. It should avoid making source control harder.

### 6.3 Recovery and backups

Recovery should show:

- project;
- snapshot timestamp;
- source timestamp;
- number of changed documents;
- recovery/apply/decline consequences.

**Candidate**

Allow the user to create named project checkpoints stored outside exported project content.
Restoring a checkpoint should first create a recovery snapshot of the current state and require
confirmation.

## 7. Asset library and import pipeline

### 7.1 Asset library

**Candidate**

The asset library should present all source and derived assets visually with:

- thumbnail or waveform preview;
- type and classification;
- dimensions and grid;
- source file and content hash status;
- usage count;
- tags;
- validation status;
- recently used and favorites filters;
- missing, changed, off-grid, and orphan filters.

Folders or collections should organize the Creator view without changing stable asset IDs or
requiring the on-disk source layout to mirror UI organization.

### 7.2 Import queue

**Candidate**

Multi-file import should use a review queue:

1. Decode and validate every selected file.
2. Suggest classifications and slicing independently.
3. Show conflicts, duplicate names, and unsupported files.
4. Allow per-file corrections.
5. Commit the accepted batch as one grouped operation.
6. Leave the project untouched if the user cancels before commit.

Filename-derived names are suggestions, never silent canonical IDs.

### 7.3 Sprite-sheet slicing

The sheet editor should support:

- manual grid slicing;
- offsets, spacing, and margins;
- common-size suggestions;
- transparent-gutter detection;
- connected-component suggestions;
- rectangular freeform slices;
- inclusion/exclusion of individual proposed cells;
- batch naming;
- class and tag assignment;
- native-scale and zoomed previews;
- visible source-image bounds and uncovered strips;
- one-action acceptance with undo;
- reimport that never pairs new image dimensions with stale grid metadata.

**Accepted direction**

An author must be able to select a rectangular region covering X by Y base grid cells and create a
single reusable multi-cell asset from it. The UI should show both the pixel rectangle and its grid
dimensions.

That action must offer two destinations:

- **Create Stamp** when the result is a reusable arrangement of atomic tiles.
- **Create Object Prefab** when the result has a footprint, anchor, collision, layering, animation,
  or interaction semantics.

### 7.4 Animation authoring

**Candidate**

Animation authoring should include:

- ordered frame list with drag reorder;
- per-frame duration and batch duration editing;
- loop, once, ping-pong, and hold-last preview modes where supported by Runtime contracts;
- onion-skin or previous/next-frame overlay;
- native-scale and enlarged preview;
- anchor/registration overlay;
- playback speed preview that does not rewrite authored timing;
- duplicate-frame and missing-frame diagnostics;
- reusable directional templates for common character sheets;
- synchronized preview of directional clips.

Runtime-supported behavior must remain the authority. The Creator cannot promise playback modes the
Runtime does not implement.

### 7.5 Audio authoring

**Candidate**

Audio assets should provide:

- metadata editor for music/SFX kind, volume, and loop intent;
- duration, channel, sample-rate, and container diagnostics from the Runtime's accepted formats;
- play/stop preview through a shared audio-preview service;
- loop-boundary preview when loop points become supported;
- usage search;
- missing/hash-mismatch diagnostics;
- map and event assignment through the shared reference picker.

The Creator should not contain a second decoder whose acceptance differs from the Runtime.

## 8. World-building vocabulary

### 8.1 Atomic tile

**Accepted direction**

A tile is one project grid cell, normally 16x16 or 32x32 pixels according to project settings. It
may reference a sprite or supported animation and carries atomic terrain/collision metadata.

Atomic tiles remain small because:

- tile collision and terrain lookup stay predictable;
- autotile rules operate on one-cell adjacency;
- palette indexing stays understandable;
- a map can replace or inspect individual cells;
- Runtime culling and rendering remain simple.

### 8.2 Stamp

**Accepted direction**

A stamp is a reusable rectangular or sparse arrangement of tile placements painted as one gesture.
It can represent:

- cliff corners;
- path intersections;
- shoreline segments;
- bridge segments;
- decorative clusters;
- repeated room layouts;
- a building assembled from multiple tiles;
- any hand-authored motif that should be reusable.

A stamp should store:

- stable ID and display name;
- width, height, and origin/anchor cell;
- one or more visual layer payloads;
- optional semantic payloads such as collision or encounter painting;
- tags and palette collections;
- preview thumbnail;
- empty cells that intentionally preserve underlying content versus cells that intentionally erase.

Stamp placement should offer an impact preview. A multi-layer stamp commits all affected layers as
one undo step.

### 8.3 Object prefab

**Accepted direction**

An object prefab is a reusable placed definition with semantic identity. It is appropriate for
trees, rocks, buildings, doors, bridges, machines, signs, furniture, and other things whose meaning
is more than a tile arrangement.

An object prefab should support:

- sprite or animation;
- multi-cell footprint;
- anchor/placement cell;
- per-cell collision mask;
- rendering relationship to the player;
- interaction point or interaction area;
- optional structured world actions;
- optional variant set;
- tags and categories;
- native-scale placement preview.

The placed instance stores the prefab reference, stable instance key, map position, and only
explicit instance overrides. Shared definition changes should update instances unless an instance
has deliberately detached or overridden a supported property.

### 8.4 Entity

Entities are placed gameplay instances such as player starts, NPCs, warps, pickups, signs, triggers,
trainers, and object instances. Each has a stable never-reused key within its map. Moving or
reordering an entity must not change its identity.

### 8.5 Brush

**Candidate**

A brush is a saved painting behavior, not content storage. Brushes may select:

- one tile;
- a weighted set of tile variants;
- a terrain/autotile rule;
- a stamp set;
- size and shape;
- replacement constraints;
- spacing or scatter behavior;
- a deterministic random seed policy.

Brush presets should be reusable across maps and visible in palette collections.

## 9. Map canvas and effectively infinite authoring

### 9.1 Grid and paintability

**Accepted direction**

The map canvas must make the paintable world legible. It should provide independent toggles for:

- base tile grid;
- major grid every configurable N cells;
- chunk boundaries;
- axes and origin;
- cursor coordinate;
- selection bounds;
- object footprints and anchors;
- collision;
- encounters;
- regions;
- triggers and entity markers;
- active-layer tint;
- dimming of inactive layers.

Grid color and opacity should remain readable over both bright and dark art. At very low zoom, the
Creator may replace individual cell lines with chunk and major-grid lines to avoid visual noise.

### 9.2 Effectively infinite map

**Accepted direction**

The map should feel infinite during authoring:

- panning is not stopped by the currently occupied rectangle;
- painting into empty space automatically creates storage;
- negative coordinates or transparent origin rebasing prevent left/up expansion from becoming a
  special case;
- empty space costs essentially nothing;
- deleting the last content in a chunk allows that chunk to disappear;
- the practical map size is constrained by explicit safety budgets, not an arbitrary small canvas.

The recommended storage model is sparse fixed-size chunks, aligned with the canvas and Runtime
culling model. A 32x32-cell chunk is the current preferred starting point because the existing map
canvas already uses that visual chunk size. The exact serialized shape and coordinate type are not
locked by this Bible.

The implementation specification must choose and test:

- coordinate range and overflow-safe arithmetic;
- maximum occupied chunks per map;
- maximum entities and semantic records per map;
- decoded-memory and save-size budgets;
- deterministic chunk ordering in serialized output;
- behavior at the hard cap;
- migration from dense width/height arrays;
- Runtime loading and culling;
- export validation;
- save/reopen equality.

The hard cap should be high enough that a legitimate project is expected to hit performance and
usability warnings long before coordinates are exhausted. Reaching it must produce a clear
diagnostic and never wrap coordinates or corrupt neighboring cells.

### 9.3 Bounds and map utilities

**Candidate**

Even an effectively infinite map needs useful occupied bounds:

- frame all content;
- frame selection;
- jump to origin, player start, bookmark, region, entity, or validation issue;
- trim empty outer chunks;
- rebase origin while preserving all relative positions and references;
- show occupied extents and estimated serialized size;
- define optional gameplay/export bounds without constraining the editing canvas;
- warn when isolated content exists far from the main map.

### 9.4 Camera and navigation

The canvas should support:

- smooth pan;
- zoom centered on pointer;
- 25-800% baseline zoom with exact native-scale reset;
- fit selection and fit content;
- middle-mouse or space-drag pan;
- keyboard pan;
- minimap/overview for large occupied areas;
- named bookmarks;
- coordinate entry;
- back/forward navigation between recent locations.

## 10. Layer system

### 10.1 Arbitrary visual layers

**Accepted direction**

Authors should be able to create as many visual layers as a practical safety budget permits.
Visual layers are stored separately and retain stable identity when renamed or reordered.

Each visual layer should have:

- stable key;
- editable name;
- ordered position;
- visibility;
- edit lock;
- editor opacity;
- render relationship or category;
- sparse tile content;
- optional tint used only by the editor.

Required operations:

- create;
- rename;
- reorder;
- duplicate;
- clear;
- delete with impact preview;
- merge down with confirmation;
- move or copy selection to another layer;
- show only this layer;
- lock all others;
- select all content on layer;
- find layer usages in stamps or prefabs where applicable.

The layer panel should clearly show the active paint target. Hidden or locked layers cannot receive
accidental edits.

### 10.2 Layer groups

**Candidate**

Layer groups organize complex maps without changing Runtime semantics:

- collapse/expand;
- group visibility and lock;
- group duplication;
- descriptive names such as Ground, Architecture, Vegetation, and Roofs.

Groups are an authoring convenience. They should not require a general scene-graph framework.

### 10.3 Render relationships

**Candidate**

Instead of relying only on a layer's arbitrary order, the map contract should define the limited
render relationships the Runtime understands, such as:

- below actors;
- actor-depth or Y-sorted objects;
- above actors;
- foreground/overlay.

Exact behavior must be locked with the Runtime renderer. Unsupported blend modes or freeform shader
graphs are rejected.

### 10.4 Semantic layers

**Accepted direction**

Gameplay meaning remains in typed semantic layers rather than ordinary visual layers:

- collision and movement rules;
- encounter assignments;
- regions;
- triggers;
- entities;
- optional elevation/bridge connectivity if later accepted;
- optional audio or environment zones if later accepted.

Semantic layers can be shown, hidden, locked, and selected in the same panel, but cannot be merged
into visual pixels or arbitrarily reordered into meaningless states.

## 11. Painting and selection tools

### 11.1 Core tools

**Accepted direction**

The world builder should include:

- pencil/brush;
- eraser;
- line;
- rectangle fill;
- rectangle outline;
- ellipse fill and outline where grid behavior is clear;
- bucket fill;
- replace;
- eyedropper;
- rectangular selection;
- lasso selection;
- move;
- stamp placement;
- object/entity placement.

Every tool should preview its result before commit when the gesture affects more than one cell.
Dragging must interpolate between pointer samples so fast motion does not leave gaps.

### 11.2 Brush constraints

**Candidate**

Brush options should include:

- square, circle, and custom stamp shape;
- size;
- paint only empty cells;
- replace only the sampled tile;
- replace only selected tile types or terrain tags;
- preserve non-empty cells;
- apply to current layer only or an explicit multi-layer stamp;
- spacing for decorative scatter;
- deterministic weighted variation.

Tool options should stay visible near the canvas and persist per project or workspace where useful.

### 11.3 Selection editing

**Accepted direction**

Selections should support:

- copy, cut, paste;
- duplicate;
- move;
- delete;
- crop/copy from visible composite or chosen layers;
- move/copy to another visual layer;
- save as stamp;
- create object prefab;
- replace tile;
- fill;
- flip horizontally or vertically only when metadata and directional semantics remain valid;
- rotate only when the selected content and directional metadata can be transformed safely.

Paste should use a floating preview until committed. The user can choose which selected visual and
semantic layers participate.

### 11.4 Clipboard

**Candidate**

The internal clipboard should preserve structured map data rather than flattening it to an image.
Cross-map paste should:

- retain tileset, stamp, object, and entity references when available;
- report missing references before commit;
- offer explicit reference mapping;
- generate new stable entity keys;
- never duplicate player-start or other unique semantic entities silently.

System clipboard interchange may use a documented text format later, but is not required for the
first robust version.

## 12. Terrain, autotiles, and procedural assistance

### 12.1 Terrain rule sets

**Candidate**

Terrain rule sets should let an author define the tiles needed for:

- center;
- edges;
- outer corners;
- inner corners;
- isolated cells;
- narrow horizontal/vertical segments;
- transitions between compatible terrain families.

Painting a terrain type should select the correct atomic tiles based on neighboring terrain state.
The rule editor must show missing cases and preview every resolved adjacency.

The system should be data-driven, deterministic, and limited to explicit rule sets. It is not a
general procedural-generation language.

### 12.2 Paths, walls, cliffs, and shorelines

**Candidate**

Specialized rule-set templates may accelerate common structures:

- paths and roads;
- walls and fences;
- cliffs;
- shorelines;
- room walls;
- bridge segments.

Templates produce ordinary editable terrain rules. Authors can inspect and adjust every assigned
case.

### 12.3 Weighted variation brushes

**Accepted direction**

Natural terrain needs variation without repetitive manual placement. A weighted brush should:

- select from author-defined variants;
- display exact weights and calculated percentages;
- use a deterministic gesture seed;
- optionally avoid immediate identical neighbors;
- allow rerolling a selection without changing its shape;
- preserve undo/redo exactly;
- serialize only the chosen tiles, not hidden randomness.

### 12.4 Scatter and decoration

**Candidate**

A constrained scatter brush may place decorative tiles, stamps, or object prefabs using:

- density;
- minimum spacing;
- allowed terrain tags;
- forbidden collision/region types;
- rotation/flip variants explicitly approved by the asset;
- deterministic seed;
- preview before commit.

It should never place gameplay entities or destructive collision changes implicitly.

## 13. Palette, catalog, and reuse

### 13.1 Tile palette

**Accepted direction**

The palette should display paintable assets in a visible grid with:

- native or enlarged thumbnails;
- tile/stamp/object distinction;
- stable name and optional index;
- search;
- tags;
- tileset and collection filters;
- favorites;
- recent selections;
- validation badges;
- missing-sprite placeholders;
- configurable thumbnail scale.

The palette itself must visibly communicate which cells are selectable and which source-sheet
regions became accepted paintable assets.

### 13.2 Collections

**Candidate**

Authors should be able to create palette collections such as:

- Autumn Forest;
- Coastal Town;
- Laboratory Interior;
- Frequently Used;
- Route Details.

Collections reference assets without copying or reindexing them. Removing a collection never
deletes its contents.

### 13.3 Usage search

Every reusable asset should answer:

- which maps use it;
- which layers and approximate coordinates use it;
- which stamps or prefabs contain it;
- which entities reference it;
- whether it is safe to delete or change;
- what would be affected by replacement.

Bulk replacement must be previewed and committed as one grouped operation.

## 14. Collision, navigation, and movement authoring

### 14.1 Collision tools

**Accepted direction**

Collision should combine tile defaults with explicit map overrides. The canvas should visualize:

- passable;
- solid;
- directional ledge;
- water or other movement categories supported by Core;
- override versus inherited value;
- object-footprint collision.

The eyedropper should distinguish inherited collision from an explicit override. Erasing an
override restores inherited behavior rather than forcing open.

### 14.2 Collision shapes and footprints

Atomic map movement remains grid-based. Object prefabs may provide per-cell collision masks across
their footprint. Freeform polygon collision is **rejected** unless the Runtime movement model later
changes through an explicit architecture decision.

### 14.3 Reachability and path diagnostics

**Accepted direction**

The Creator should visualize:

- cells reachable from player start;
- disconnected walkable islands;
- exits or required entities outside reachable space;
- NPC patrol paths crossing blocked cells;
- warp arrivals in solid or invalid cells;
- one-way ledge direction;
- object collisions that seal required routes.

Reachability analysis must use the same Core movement rules the Runtime uses.

### 14.4 Bridge and elevation semantics

**Candidate**

Bridges, overpasses, and layered walkways may require more than visual layers. Before adding a
generic height system, define the smallest reusable semantic model required by real maps:

- which connectivity plane an actor occupies;
- where transitions between planes occur;
- how collision and rendering change;
- how NPC paths and encounters behave.

A decorative bridge that needs no overlapping walkable routes should remain an ordinary stamp or
object. A general elevation system is deferred until a concrete gameplay requirement proves it is
needed.

## 15. Regions, encounters, and environment zones

### 15.1 Named regions

**Candidate**

Named regions are reusable painted selections with stable IDs and display properties. They can
support:

- navigation and bookmarks;
- encounter assignment;
- environment or audio settings;
- validation scope;
- trigger conditions from a closed vocabulary;
- map organization.

Regions should allow holes and disconnected cells only when explicitly supported; the editor must
make their extent visible.

### 15.2 Encounter painting

**Accepted direction**

Encounter painting should provide:

- color-coded table assignments;
- legend with calculated rates and methods;
- eyedropper;
- bucket/selection assignment;
- unassigned encounter-surface warnings;
- overlapping or conflicting assignment diagnostics;
- quick navigation to the encounter-table editor;
- deterministic simulation through Core rules.

Simulation results are authoring diagnostics, not a replacement for Runtime playtesting.

### 15.3 Environment and audio zones

**Candidate**

If maps need localized presentation changes, structured zones may assign:

- music or ambient sound;
- indoor/outdoor tint policy;
- weather presentation allowed by game rules;
- camera or transition presentation from a closed catalog.

These zones must not become an open scripting system. Runtime ownership and precedence between map
defaults, regions, and events must be specified before implementation.

### 15.4 Creature Encounter System priority

**Accepted direction - engine work is an emergency prerequisite**

Creature encounters are a critical engine-to-Creator vertical. The Creator must eventually make
them approachable and highly customizable, but the engine contract comes first. The encounter
editor must not offer fields that are merely preserved in JSON or approximated by Creator logic.
Every selectable encounter option must have a complete, deterministic path through:

1. schema and migration;
2. Core validation and condition evaluation;
3. Core encounter selection and creature generation;
4. Runtime battle launch;
5. battle and capture persistence;
6. save/load and replay determinism;
7. focused and end-to-end tests;
8. only then, Creator authoring controls.

The missing engine work in this section is promoted to the emergency encounter-engine package in
`IMPLEMENTATION_PLAN.md`. The later Creator surface is blocked on that package's exit gate.

### 15.5 Encounter Area and Encounter Pool separation

**Accepted direction**

The Creator presents one understandable **Encounter Area** workflow while the underlying model keeps
two responsibilities separate:

- an **Encounter Area** says where and under what overworld circumstances encounters may start;
- an **Encounter Pool** says which creatures may be selected and how each resulting creature is
  generated.

The existing `encounter:*` entity evolves into the reusable Encounter Pool. An Encounter Area is a
stable, map-owned semantic placement containing painted cells and a reference to one pool. It is not
inferred from the visual tile layer.

Creating an area from the map should create a matching private pool by default. This keeps the
beginner workflow linear:

1. choose **New Encounter Area**;
2. name it;
3. paint its cells;
4. add creatures;
5. validate and save.

Advanced authors may instead reference an existing reusable pool. The Creator must explain when
editing a shared pool will affect multiple areas and show every usage before destructive changes.

An example conceptual model is:

```text
Encounter Area
  key: route_1_grass_north
  displayName: Route 1 North Grass
  map: map:route_1                 (derived from ownership)
  pool: encounter:route_1_grass
  activation: completedStep
  method: grass
  paintedCells: 436

Encounter Pool
  id: encounter:route_1_grass
  displayName: Route 1 Grass Creatures
  slots: [...]
```

The map is not redundantly stored as a user-entered property on the pool. Location is derived from
the area placements that reference it. The pool usage panel may therefore report:

```text
Used by:
  Route 1 / Route 1 North Grass - 436 cells
  Route 1 / Route 1 South Grass - 281 cells
```

Stable identity follows the project-wide rules:

- `encounter:route_1_grass` is the immutable pool EntityId;
- an area has a stable key within its owning map;
- display names may change without changing either identity;
- moving or repainting an area does not recreate it;
- duplicate creates a new stable identity rather than aliasing accidentally.

### 15.6 Painted semantic areas

**Accepted direction**

Ordinary walking, cave, and water encounters use a sparse semantic overlay painted on the map.
Encounter behavior is independent of terrain artwork:

- decorative grass may be safe;
- visually identical grass may use different pools;
- cave encounters may occur on ordinary floor graphics;
- a town may contain grass art without random encounters;
- events may enable, disable, or redirect an area without repainting its visuals.

The Encounter Area editor should support brush, eraser, rectangle, bucket, selection fill,
eyedropper, focus selection, area-wide select, color-coded overlays, a legend, and undoable strokes.
One ordinary random Encounter Area may own a cell at a time. Overlap between random areas is an
error rather than an implicit precedence rule. Scenario trigger regions remain a separate semantic
overlay and may coincide with encounter cells because the Core step pipeline defines which outcome
wins.

An area may contain holes or disconnected islands, but the editor must make that structure visible
and warn when disconnected components appear accidental. Empty areas are invalid. Unreachable
painted components are warnings or errors according to whether any legal player state can reach
them.

### 15.7 Eligible movement and trigger semantics

**Accepted direction - missing engine work**

Random encounter rolls occur only after a **completed eligible player step** landing on a cell owned
by an enabled Encounter Area.

The following do not roll:

- turning in place;
- attempting to walk into collision;
- NPC movement;
- map-editor cursor movement;
- camera movement;
- ordinary scenario-controlled movement, unless the scenario node explicitly opts in;
- a step already consumed by an earlier warp, tile trigger, or trainer-sight outcome.

The engine-owned step order remains deliberate and tested:

```text
warp -> tile/scenario trigger -> trainer sight -> random encounter
```

An earlier outcome consumes no encounter RNG.

Encounter method must become functional rather than descriptive metadata. Engine-owned method
presets define compatible traversal and surface requirements:

- **grass**: completed on-foot step and grass-compatible semantic surface;
- **cave**: completed eligible step in a cave-compatible area/environment;
- **water**: completed water-traversal step and water-compatible semantic surface;
- **tile**: explicit step activation independent of grass/cave/water presets;
- **interact**: interaction resolution, never an ordinary movement roll.

The exact traversal-state and terrain-semantic vocabulary must be locked in the owning engine and
schema specifications before implementation. Authors may use an engine-supported custom
combination of eligible traversal modes and required semantic tags, but they may not type arbitrary
method names. Presets populate valid defaults and validation explains any override.

The current engine does not yet enforce these method semantics. The emergency package must add the
required Core traversal context and validation rather than teaching Runtime or Creator to guess.

### 15.8 Area frequency policy

**Accepted direction**

Encounter frequency and creature selection weight are separate concepts:

- **frequency** decides whether this eligible step begins an encounter;
- **slot weight** decides which eligible creature is selected after an encounter begins.

The initial engine-complete area policy must support:

- base per-eligible-step rate;
- enabled condition;
- eligible traversal modes;
- method/surface requirements;
- repel behavior through the existing Core rule;
- deterministic reset behavior after map changes and battles.

The engine contract should be designed to add, through typed options, the following author controls
when their rules are implemented:

- grace steps after entering a map or area;
- grace steps after completing an encounter;
- minimum steps between encounters;
- maximum dry streak or pity threshold;
- roll on area entry only versus every eligible step;
- rate modifiers for supported movement modes;
- temporary campaign/scenario overrides.

These controls must use fixed simulation state and injected RNG. Wall-clock time, render frames,
and Creator-side probability logic are forbidden.

### 15.9 Encounter Pool and slot model

**Accepted direction - missing engine work**

An Encounter Pool contains an ordered set of stable slot entries. Slot order exists for authoring,
diffs, and deterministic tie behavior; probability is determined by weight among eligible slots.
Each slot needs a stable key so conditions, validation, diagnostics, and future migrations can refer
to it without relying on list position.

Every slot supports:

- creature/species reference;
- optional supported form policy;
- positive selection weight;
- minimum and maximum level;
- level distribution from an engine-owned catalog;
- move-generation policy;
- ability-generation policy;
- per-stat IV policy;
- nature policy;
- gender policy;
- held-item policy;
- typed eligibility condition;
- enabled state for authoring without deletion;
- optional author notes that never affect Runtime behavior.

The editor shows effective percentages for a selected preview context. Percentages are normalized
only across slots whose conditions are currently eligible. If no slot is eligible, no encounter is
created and validation/simulation must identify the empty state.

### 15.10 Move-generation policy

**Accepted direction - missing engine work**

Wild encounter moves must be authored through an explicit policy:

- **Automatic legal moves** - choose the engine-defined legal moves from the creature's learnset at
  the rolled level. This is the recommended default.
- **Fixed moves** - use one to four authored move references in their authored order.
- **Random authored pool** - choose a deterministic subset from an authored pool, only after this
  policy is implemented and proven.

The Creator must show only policies supported by the current engine catalog. Fixed moves validate
existence, count, duplicates, PP availability, and creature/level legality. A project may permit an
intentional legality override where the owning ruleset supports it, but the editor must show the
override clearly; it may never silently repair or replace a move.

Move selection belongs to Core encounter generation. Runtime does not derive a different moveset
from the species after Core has resolved the encounter.

### 15.11 Ability, form, nature, gender, and held-item policies

**Accepted direction - missing engine work**

Each optional creature property uses a typed policy rather than a nullable field with ambiguous
meaning:

- **Ability**: random normal ability, fixed valid ability, or hidden ability when explicitly
  permitted.
- **Form**: default form, fixed supported form, or an engine-owned conditional form policy.
- **Nature**: random, fixed, or deterministic selection from an authored allowed pool.
- **Gender**: species distribution, fixed compatible value, or an authored supported distribution.
- **Held item**: none, fixed item, or deterministic weighted item pool when implemented.

Pickers are filtered by the chosen species/form and still preserve an unsupported legacy value as a
blocking diagnostic rather than silently deleting it. Compatibility is validated in Core. A field
does not appear merely because the serializer can carry it.

### 15.12 IV ranges and generated instance fidelity

**Accepted direction - missing engine work**

Each of the six IVs supports:

- random full range `0-31`;
- one fixed value;
- an inclusive authored range such as `15-31`.

The Creator provides an **Apply to all stats** convenience and then permits per-stat refinement.
Core validates `0 <= min <= max <= 31` and samples each stat in a fixed documented order.

The resolved creature instance must retain every generated or authored property through the entire
lifecycle:

- encounter selection;
- battle launch;
- battle state;
- capture;
- party or storage deposit;
- save;
- reload;
- later battle use.

The current capture deposit path does not preserve the complete generated instance identity. The
emergency package must close this gap for IVs, nature, ability, form, held item, moves/PP, gender,
and every other supported persistent instance field. Capture may update battle-mutated values such
as current HP and status, but it must not regenerate or discard the creature's authored/generated
identity.

### 15.13 Typed encounter conditions

**Accepted direction - missing engine work**

Encounter eligibility uses the same closed, engine-owned condition language intended for Campaign
and Scenario authoring. There is no free-text predicate or Creator-only evaluator.

The emergency package must at minimum support the conditions required by this approved workflow:

- time of day;
- boolean or integer flag comparison where the flag system supports it;
- player party creature count with equal, minimum, maximum, and range comparisons.

Conditions compose through explicit `All`, `Any`, and `Not` groups. Evaluation is pure,
deterministic, and consumes no RNG. The registry can later expose implemented conditions such as
campaign milestone, quest state, lead-creature level, item count, or seen/caught state, but none is
selectable before the required game state and evaluator exist.

The Creator produces a plain-language summary and Core validation detects:

- contradictory conditions;
- invalid comparison values;
- missing flag or entity references;
- a slot that can never become eligible;
- an area whose enabled condition can never be true;
- supported contexts in which an enabled area has no eligible slots.

### 15.14 Core encounter resolution contract

**Accepted direction - emergency engine boundary**

Core must resolve a complete encounter result. The present species-and-level-only outcome is
insufficient for authored moves, abilities, IV ranges, forms, held items, and richer conditions.

The target responsibility flow is:

```text
Completed player step
  -> Core validates area activation context
  -> Core performs the area frequency roll
  -> Core filters slots without consuming RNG
  -> Core selects a weighted slot
  -> Core resolves level and every generation policy
  -> Core returns a complete wild-creature encounter specification
  -> Runtime launches battle from that exact specification
  -> capture persists that exact instance
```

The owning specification must lock the result type and catalog inputs before code. Runtime may
assemble presentation resources, but it must not reroll, replace, normalize, or infer creature
properties. Creator simulation must call the same Core resolver.

RNG order is part of the public deterministic contract. The spec and tests must state which draws
occur for frequency, slot selection, level, IV stats, nature, gender, ability, form, moves, and held
item, including which disabled or fixed policies consume zero draws.

### 15.15 Interaction, fixed, and scenario encounters

**Accepted direction**

Not every encounter is a painted random area:

- **Interaction encounter**: fishing spot, shaking tree, smashable rock, or other interactable
  references an Encounter Pool or a fixed encounter specification.
- **Fixed encounter**: a visible overworld creature or object starts a specific authored creature
  battle and has an explicit defeated/captured/respawn policy.
- **Scenario encounter**: the Campaign/Event Graph starts a wild or fixed encounter as a typed
  action after dialogue, movement, puzzle, or story conditions.

These mechanisms reuse the same Core creature-generation policies where applicable. They do not
fake a painted step or bypass capture fidelity. Their trigger ownership remains with interaction,
entity, or scenario systems, so the random-area layer stays understandable.

### 15.16 Encounter editor workflow

**Accepted direction - tool work follows the emergency engine package**

The eventual editor has three synchronized surfaces:

1. **Area list** for the current map;
2. **painted map overlay** for spatial extent;
3. **pool slot list** for available creatures.

When an area has no slots, display:

> Area has no encounters yet.

The empty state includes a prominent `+ Add Creature` action. The add dialog uses progressive
sections:

1. creature and optional form;
2. weight and live effective percentage;
3. level policy;
4. move policy;
5. ability policy;
6. IV ranges;
7. nature, gender, and held-item policies where supported;
8. typed conditions;
9. plain-language result summary.

`Add` or `Save` validates through Core, closes the dialog only on success, and shows the added slot
in the pool. Cancel makes no change. Editing, adding, duplicating, removing, repainting, and changing
area settings are undoable. A combined workflow may dirty both the map and pool, but explicit Save
commits them through the existing whole-project transaction so source is never half-updated.

The area panel includes immutable ID/key, display name, owning map, pool reference, method,
frequency, enabled condition, painted-cell count, usage, and validation state. Location is displayed
from map ownership rather than typed into a redundant field.

### 15.17 Encounter preview and Runtime playtest

**Accepted direction**

The editor provides a deterministic Core-powered preview with authored context:

- time of day;
- party count;
- supported flags;
- lead level or other registered condition inputs;
- seed;
- number of trials.

It reports:

- chance of an encounter per eligible step;
- expected eligible steps per encounter;
- effective probability of every creature;
- level distribution;
- move, ability, form, held-item, nature, gender, and IV outcomes where supported;
- probability and reason for no eligible slot;
- deterministic trace for a selected trial.

This preview is an authoring diagnostic, not gameplay simulation. **Playtest from Area** launches
the real Runtime out of process at a validated painted cell with a configurable debug party after
the ordinary validation gate.

### 15.18 Encounter validation and emergency exit gate

**Accepted direction**

Blocking validation includes:

- area has no painted cells;
- overlapping random areas;
- missing pool, species, move, ability, form, or item reference;
- pool has no enabled slots;
- non-positive weights;
- invalid level or IV ranges;
- invalid fixed move count or generation policy;
- incompatible fixed ability/form policy;
- invalid condition tree;
- method, traversal, or required-surface contract cannot be satisfied;
- a reachable enabled state has no eligible slot when the ruleset requires an encounter;
- an unsupported legacy field would otherwise be lost.

Warnings include:

- unreachable or disconnected painted components;
- grass/water preset painted over an apparently mismatched semantic surface when an intentional
  supported override exists;
- unusually high or low effective frequency;
- duplicate equivalent slots;
- fixed moves outside the ordinary learnset where an explicit override is legal;
- an unused reusable pool;
- a common preview state with no eligible slot.

The emergency engine package is complete only when:

1. the owning specs and schema/migration are locked;
2. Encounter Areas and expanded Pool slots round-trip and migrate deterministically;
3. method/traversal/surface semantics affect real Core eligibility;
4. typed conditions include time, flags, and party-count comparisons;
5. Core resolves a complete creature specification with the approved generation policies;
6. Runtime starts the battle without rerolling or deriving replacement values;
7. capture, storage, save, and reload preserve the complete instance;
8. deterministic RNG-draw and no-draw cases are tested;
9. existing encounter projects migrate without silent behavior changes;
10. build, full tests, focused encounter traces, and an end-to-end walk-to-capture-to-reload test are
    green.

Only after this exit gate may the Encounter Area and Pool editor expose the corresponding controls.

## 16. Entities, paths, interactions, and prefabs

### 16.1 Placement experience

Entity placement should show:

- native-scale preview;
- occupied cells;
- anchor;
- facing;
- collision interaction;
- invalid-placement reasons;
- stable key after commit;
- validation status.

Moving an entity preserves its key. Duplicating assigns a new never-reused key and previews any
fields that must be unique.

### 16.2 Entity inspector

**Accepted direction**

Every placed entity must be fully configurable without raw JSON. The inspector should expose only
fields relevant to its kind, with searchable reference pickers and inline validation.

Examples:

- NPC: sprite, facing, movement mode, radius/path, dialogue, trainer reference;
- warp: target map, target coordinate, transition;
- pickup: item, quantity, persistence flag;
- sign: text;
- trigger: condition and ordered structured actions;
- object: prefab reference and allowed instance overrides.

### 16.3 Path authoring

**Candidate**

NPC patrol paths should be drawn directly on the map:

- click or drag 4-connected steps;
- show step order and direction;
- reject or highlight blocked segments;
- allow closed-loop or reverse-at-end behavior only when supported by Runtime;
- preview the path without simulating gameplay in Creator;
- navigate from validation issues to the exact segment.

### 16.4 Warp authoring

**Accepted direction**

Warp creation should be a paired workflow:

1. Place or select the source.
2. Choose target map visually.
3. Pick target coordinate from a live map thumbnail/canvas.
4. Preview arrival facing and collision.
5. Optionally create or link the reciprocal warp.
6. Validate both ends.

A map-connection graph should show broken and one-way links.

### 16.5 Structured interactions

**Accepted direction**

Interactions and triggers use the closed, typed world-action catalog owned by Core. The editor
should provide:

- action picker grouped by purpose;
- typed parameter fields;
- add/remove/reorder;
- inline validation;
- plain-language summary;
- explicit condition editor from the supported vocabulary;
- usage/reference navigation.

A general-purpose scripting language, arbitrary code execution, and plugin-based runtime behavior
are **rejected for the pre-1.0 Creator**.

### 16.6 Composite gameplay prefabs

**Candidate**

For repeated structures such as doors, shops, healing stations, or treasure arrangements, a
composite prefab may bundle:

- object or stamp placement;
- one or more entities;
- structured parameter slots;
- validation rules;
- a placement preview.

Parameters must be explicit references or values, such as destination map or shop inventory.
Composite prefabs cannot introduce new executable logic.

## 17. Map organization and world topology

### 17.1 Map browser

**Candidate**

Maps should be browsable as:

- searchable list;
- thumbnail grid;
- folders/collections;
- world-connection graph;
- recent maps;
- maps with validation errors.

Map thumbnails should be cached and invalidated by content changes rather than fully regenerated on
every navigation.

### 17.2 World graph

**Candidate**

The world graph should visualize:

- maps as nodes;
- warps and edge transitions as links;
- one-way links;
- broken targets;
- start map;
- disconnected map groups;
- optional author-defined spatial arrangement.

It is a navigation and validation tool, not the Runtime world representation.

### 17.3 Neighbor preview

**Candidate**

When aligning routes or rooms, the map canvas may ghost a selected neighboring map beyond an edge.
The preview should:

- never merge the maps;
- use an explicit connection or temporary alignment;
- allow snapping compatible edges;
- show tile-size or alignment conflicts;
- stay editor-only unless a formal seamless-transition contract is later adopted.

### 17.4 Bookmarks and notes

**Candidate**

Authors should be able to create editor-only bookmarks and notes attached to:

- project;
- map coordinate;
- entity;
- data document;
- validation workflow.

They are never exported into the game unless deliberately converted into supported content.

## 18. Structured game-data authoring

### 18.1 No-JSON completeness

**Accepted direction**

Every serialized game-data field intended for authors must have a structured Creator surface. A
schema-to-editor coverage registry should fail tests whenever an authorable field has no editor.
Raw JSON may be viewable for diagnostics, but a user must be able to complete a valid game without
editing it.

### 18.2 Forms and list editing

Editors should provide:

- field grouping by author intent;
- inline descriptions and units;
- range-constrained numeric inputs;
- searchable stable-reference pickers;
- add/remove/reorder list controls;
- duplicate row/item;
- calculated summaries;
- validation beside the field and in the global strip;
- exact undo/redo;
- meaningful empty states.

### 18.3 Bulk editing and tables

**Candidate**

Large categories should offer a table view for safe bulk work:

- choose columns;
- sort and filter;
- multi-select;
- set one field across selection;
- find/replace supported values;
- copy/paste tabular data with validation preview;
- export/import CSV for non-structural scalar fields;
- show changed-cell counts before commit.

Bulk operations must preserve stable IDs and refuse ambiguous nested-structure changes.

### 18.4 Templates and duplication

**Candidate**

Entity templates should accelerate repeated authoring while keeping each created entity independent:

- create from selected entity;
- choose which fields are copied;
- generate a new valid stable ID;
- clear instance-specific references by explicit rule;
- preview validation impact.

Templates do not silently link future edits unless a separate reusable-definition model exists.

### 18.5 Cross-reference view

**Candidate**

A selected entity or asset should expose inbound and outbound references as a navigable graph or
grouped list. This is particularly useful for:

- species and learnsets;
- moves and effect references;
- items and shops;
- trainers and maps;
- encounter tables and regions;
- sprites and their consuming definitions.

## 19. Engine-backed creation system

### 19.1 Goal

**Accepted direction**

Creating moves, items, abilities, creatures, trainers, encounters, objects, and other game content
must be simple enough that a user is not left guessing what a field means, which values are valid,
or whether the engine actually supports the authored behavior.

The Creator must provide a powerful guided creation system in which:

- every selectable mechanic is implemented by the current engine build;
- every parameter uses a typed control with a visible unit, range, and explanation;
- compatible mechanics can be composed into ordered multi-effect behavior;
- invalid or unsupported combinations are prevented or explained immediately;
- the authored behavior is summarized in plain language;
- the user can inspect exact timing, target, chance, failure, and interaction semantics;
- the same Core validation and compilation used by Runtime determine whether content is valid;
- no writable raw JSON is required;
- advanced power comes from reusable effect composition, hooks, conditions, and presets rather than
  arbitrary scripts.

This system should feel like designing game behavior, not filling out an undocumented database row.

### 19.2 Current baseline and gap

The engine already has the important architectural foundation:

- moves store an ordered `effects[]` list;
- items have ordinary and held/battle effect lists;
- abilities contain hook points, each with an ordered effect list;
- Core compiles authored move operations into typed battle effects;
- Core owns effect resolution, events, traces, conditions, queries, and deterministic RNG;
- validation can reject unknown operations, parameters, ranges, targets, scopes, and combinations;
- effect behavior is data-driven rather than implemented per named move.

The current Creator baseline does not yet satisfy this Bible:

- several complex structures are still writable as raw JSON;
- the basic move editor does not expose the complete effect list;
- an operation existing in a compiler switch is not currently a sufficient user-facing capability
  contract;
- users cannot reliably discover what an operation does, which contexts support it, or what units
  its parameters use;
- multi-effect ordering and interaction are not explained visually;
- a user cannot launch a focused Runtime sandbox directly from a configured effect row.

The catalog-driven Phase 17 mechanics editor closes this gap. The Bible expands its product
requirements and usability standard.

### 19.3 The Creator Ready guarantee

**Accepted direction**

An operation, condition, hook, query, target, or preset is selectable only when it is **Creator
Ready for the exact authoring context**.

Creator Ready requires evidence that:

1. A machine-readable descriptor exists.
2. Valid authored parameters serialize and reload without loss.
3. Core validation accepts valid values and rejects invalid, missing, unknown, and incompatible
   values.
4. The current engine compiler or dispatcher accepts the descriptor's serialized output.
5. The resolver performs the behavior and emits the documented events and trace.
6. Timing, target, scope, topology, ruleset, failure/no-op, and RNG behavior are tested.
7. AI uses the same mechanic or has an explicitly neutral/non-scoring disposition where AI
   understanding is required.
8. Runtime can present the emitted outcome without the UI inferring rules from state differences.
9. The Creator can author, undo, redo, save, reopen, duplicate, and delete the operation without
   losing data.
10. At least one representative end-to-end author -> save -> load -> compile -> resolve test is
    green for that mechanic family.

An effect may be Creator Ready for moves but not for held-item hooks, or ready for an ability hook
but not for field items. Readiness is a `(capability, context)` fact, never a single optimistic
boolean.

The Creator must not expose a planned catalog row merely because it is documented for future engine
coverage. Unsupported imported data may be preserved and diagnosed, but cannot be silently enabled
or presented as a working choice.

### 19.4 Capability states

The engine-owned catalog should expose one of these states per context:

- **Ready**: fully authorable and proven under §19.3.
- **Read-only legacy**: the engine can preserve existing data, but the Creator cannot safely create
  or modify it.
- **Unavailable**: not implemented in this engine/catalog version.
- **Deprecated**: still supported for existing content, but new content should use the named
  replacement.
- **Invalid/unknown**: not recognized by the current build; payload is preserved and blocks
  validation/export.

The ordinary add menu shows Ready entries only. An optional `Show unavailable` help view may explain
future or legacy capabilities and their requirement strings, but those entries remain visibly
disabled and cannot be committed.

### 19.5 Engine capability registry

**Accepted direction**

Core must own a machine-readable capability registry. The Creator generates controls and
compatibility filtering from that registry instead of maintaining a second handwritten list of
effect names.

Each capability descriptor should provide:

- stable operation/capability ID;
- display label and search aliases;
- family and subcategory;
- concise description;
- detailed behavior/help key;
- supported authoring contexts;
- supported hook points;
- source and target scopes;
- move-target and damage-class compatibility;
- singles/doubles/topology requirements;
- ruleset compatibility;
- execution timing;
- hit, success, damage, contact, or other prerequisite semantics;
- whether chance is allowed and what the chance gates;
- ordered typed parameter descriptors;
- defaults;
- legal range and step;
- unit;
- enum or reference source;
- conditional visibility/enabling rules;
- conflicts and multiplicity limits;
- deprecation and replacement;
- emitted event/trace families;
- AI disposition;
- readiness evidence/version;
- plain-language summary template;
- neutral examples.

Parameter types should include only supported structured controls, such as:

- integer;
- exact fraction;
- percentage;
- turns/duration;
- probability;
- boolean;
- enum;
- stat;
- persistent or volatile status;
- type;
- entity reference;
- move/item/ability reference;
- target/scope;
- condition;
- hook point;
- weighted table;
- numeric band table;
- ordered typed child rows where the catalog explicitly permits them.

Opaque comma-separated strings and editable JSON are not acceptable substitutes for a proper
parameter descriptor.

### 19.6 Catalog completeness and drift prevention

**Accepted direction**

Automated coverage must fail when:

- the compiler accepts an operation with no catalog descriptor;
- a Ready descriptor has no compiler/dispatcher path;
- a descriptor omits a serialized parameter;
- code accepts an undocumented enum or parameter value;
- the Creator has a handwritten operation choice absent from Core;
- an ability hook, item context, or move target is exposed without exact compatibility metadata;
- a deprecated operation lacks a preservation and replacement policy;
- a Ready capability loses its end-to-end proof.

The engine, Creator, validation, documentation, and export pipeline should identify the same catalog
version. Opening content authored against a newer catalog must preserve it, diagnose it, and refuse
unsafe export rather than discard or reinterpret it.

### 19.7 Guided and advanced modes

**Accepted direction**

Every complex content editor should provide two views over the same underlying data:

- **Guided mode** asks understandable questions, supplies safe defaults, and presents common
  recipes.
- **Advanced mode** exposes the complete ordered effect/hook structure, every supported parameter,
  timing detail, and compatibility rule.

Switching modes must be lossless. Guided mode is not a second simplified schema, and Advanced mode
does not become raw JSON.

Example guided move flow:

1. Is this move physical damage, special damage, or status-only?
2. Who does it target?
3. Does it deal ordinary damage, use another supported damage model, or deal no damage?
4. Should it add another effect?
5. When and with what chance should that effect occur?
6. Does the user pay a cost, heal, change stats, apply a condition, alter the field, or perform
   another supported operation?
7. Review the plain-language behavior and validation result.

The Advanced view immediately shows the resulting ordered operation list and permits exact editing.

### 19.8 Effect chooser

**Accepted direction**

`Add Effect` opens a searchable chooser rather than an empty operation-name field.

The chooser should include:

- search by plain-language terms and stable operation ID;
- categories such as Damage, Healing, Status, Stats, Costs, Targeting, Turn Control, Conditions,
  Weather/Terrain, Items, Abilities, and Field/Side Effects;
- recently used and favorite operations;
- context filtering from the host editor;
- compatibility filtering from current move/item/ability fields;
- Ready status;
- a one-sentence behavior summary;
- timing and target badges;
- whether the effect supports chance;
- required topology/ruleset indicators;
- a details panel with parameters, failure cases, events, and a neutral example.

Search aliases should let a user type phrases such as `paralyze`, `lower defense`, `heal user`,
`drain`, `recoil`, `weather`, or `switch target` without knowing internal operation IDs.

The chooser should default to compatible entries. A user may inspect why another operation is
incompatible, but cannot add it until the host fields are changed or the conflict is resolved.

### 19.9 Parameter editor

**Accepted direction**

Choosing an effect opens a generated parameter form. Every input must answer:

- What does this value control?
- What unit is it in?
- What is the allowed range?
- What is the default?
- Who does it affect?
- When is it evaluated?
- Is the result exact, a fraction, a percentage, or derived from another value?
- What happens at zero, at the limit, or when the target is ineligible?

Examples:

- `Heal 20 HP` uses an integer control labeled `HP`.
- `Heal 1/2 of maximum HP` uses an exact fraction or percentage control labeled `of max HP`.
- `Heal 50% of damage dealt` uses a percentage labeled `of actual damage dealt`.
- `Raise Attack by 1 stage` uses a stat picker and stage control limited to the engine's stage
  bounds.
- `30% chance to paralyze` uses a probability control and persistent-status picker.
- `Lasts 5 turns` uses a duration control with application/expiry timing explained.

The UI must not flatten distinct formulas into a vague `Value` box.

### 19.10 Honest drain semantics

The user's drain example illustrates why units and formulas must be explicit.

If the current registered drain effect heals a fraction of actual damage dealt, the Creator should
show:

> Deal the move's normal damage, then heal the user for **50% of the actual damage dealt**.

It should not label that parameter simply `Drain: 50`, and it should not imply a fixed 50 HP
transfer.

If the user wants:

> Remove exactly 15 HP from the target and heal the user by exactly the amount removed.

that option is selectable only if the current engine has a separately registered and Creator Ready
fixed HP-transfer capability. Otherwise the chooser explains that the current drain operation is
damage-derived and does not offer the unsupported fixed-transfer behavior.

This rule applies to every formula: labels describe the engine's exact semantics, not an
approximation that sounds convenient.

### 19.11 Ordered multi-effect composer

**Accepted direction**

Moves, item uses, held-item hooks, ability hooks, conditions, and other supported hosts should use a
shared ordered effect composer.

Each effect card should show:

- drag handle and execution position;
- friendly label and stable operation ID;
- target/scope;
- timing;
- chance;
- compact parameter summary;
- compatibility/validation status;
- emitted-outcome summary;
- duplicate, remove, and expand/collapse actions.

The composer must support:

- add;
- configure;
- reorder;
- duplicate;
- remove;
- copy/paste between compatible hosts;
- save compatible compositions as recipes;
- one undo entry per meaningful action;
- grouped undo when applying a recipe;
- keyboard reordering and accessible controls.

Effects execute according to the engine's documented timing and ordered-list semantics. The Creator
must not imply that visible order overrides an earlier/later engine hook. Cards therefore show both
list order and resolved timing phase.

### 19.12 Composition semantics

**Accepted direction**

For every multi-effect composition, the Creator should explain:

- whether later effects require the move to hit;
- whether they require actual damage greater than zero;
- whether they run when the target is immune, protected, fainted, or already affected;
- whether chance is rolled once per action, target, or hit;
- whether a chance gates one effect or an allowed group;
- whether self-effects run after target failure;
- whether the effect uses authored or current/live values;
- what is snapshotted for delayed effects;
- how multiple targets are traversed;
- which failures stop the remaining list and which are visible no-ops;
- exact RNG draw order where randomness is used.

This information should be generated from catalog metadata and normalized Core explanation, not
recreated as UI-only battle logic.

The current flat ordered effect list remains the default. Nested conditions, branches, loops, or
effect groups are selectable only when the engine has a closed typed representation for them.
The Creator never invents a generic `if`, `else`, loop, or script block that Runtime cannot execute.

### 19.13 Multi-effect examples

**Accepted direction**

The following kinds of composition must be straightforward when their constituent capabilities are
Creator Ready:

**Damage plus target status**

1. Deal standard damage using the move's power and damage class.
2. After a successful hit, apply paralysis to the target with a 30% chance.

Plain-language summary:

> Deals 60-power special damage to one selected opponent. After a successful hit, has a 30% chance
> to paralyze that target.

**Damage plus self stat increase**

1. Deal standard damage.
2. Raise the user's Attack by 1 stage under the catalog's documented success timing.

The summary must say whether the boost requires a hit or damage; it cannot leave that interaction
ambiguous.

**Drain attack**

1. Deal standard damage.
2. Heal the user by a chosen supported fraction of actual damage dealt.

**Damage plus target stat decrease**

1. Deal standard damage.
2. After a successful hit, lower the target's Defense by 1 stage with a selected supported chance.

**Status move with multiple stat changes**

1. Raise the user's Attack by 1 stage.
2. Raise the user's Speed by 1 stage.

Both stage changes remain distinct ordered operations so event order, caps, guards, and partial
success remain visible.

**Healing plus cure**

1. Restore a supported amount or fraction of HP.
2. Cure one or more supported persistent statuses.

The composer must validate the intended target and whether both effects are valid for a move, field
item, battle item, or hook.

### 19.14 Recipes and presets

**Accepted direction**

The Creator should offer behavior-named recipes assembled entirely from Ready operations. Recipes
speed up common authoring without creating hidden engine special cases.

Initial recipe families should include, where supported:

- standard damaging move;
- damage plus status chance;
- damage plus self stat change;
- damage plus target stat change;
- drain attack;
- recoil attack;
- fixed-damage move;
- multi-hit move;
- healing move;
- status-only move;
- multi-stat setup move;
- direct healing item;
- status-curing item;
- healing-plus-cure item;
- held-item hook;
- switch-in ability hook;
- outgoing/incoming damage modifier ability hook;
- end-of-turn ability hook;
- encounter slot table;
- simple NPC interaction;
- warp pair.

Applying a recipe expands to ordinary visible fields, hooks, and effects. The user can inspect,
reorder, modify, or delete every generated row. Recipes do not remain opaque and do not dispatch by
recipe name at Runtime.

Users may save project-local recipes from valid compositions. A recipe records only supported
structured data and parameter placeholders; it cannot inject arbitrary JSON.

### 19.15 Plain-language behavior summary

**Accepted direction**

Every complex document should show a continuously updated behavior summary.

For a move, it should cover:

- damage class and power/model;
- accuracy and bypass semantics;
- target;
- priority;
- contact;
- each ordered effect;
- chance and timing;
- duration;
- cost, recoil, drain, or self-faint behavior;
- field/side/condition changes;
- important failure or compatibility constraints.

For an ability, it should group behavior by hook:

> When this creature enters battle: [effects in order].
>
> Before outgoing damage is finalized: [modifier and conditions].
> At end of turn: [effects in order].

For an item, it should separate:

- field use;
- active battle use;
- held-item hooks;
- target;
- consumption behavior;
- effects in order.

Summaries are generated from normalized data and catalog templates. They are explanations, not a
second source of mechanics.

### 19.16 Normalized technical preview

**Accepted direction**

Advanced users should be able to expand a read-only technical preview showing:

- normalized operation IDs;
- resolved defaults;
- target and scope;
- timing phases;
- requirements and filters;
- hook registrations;
- condition IDs;
- expected event and trace families;
- catalog/schema version.

This replaces writable raw JSON as the way to understand exactly what will be saved and executed.
It helps diagnose behavior without asking ordinary users to understand serialization.

### 19.17 Live validation and guided correction

**Accepted direction**

Validation runs as the user edits and appears both inline and in the global strip.

The composer should detect:

- unknown or unavailable operation;
- missing required parameter;
- invalid range or enum;
- unknown parameter;
- incompatible target;
- incompatible damage class;
- incompatible hook point;
- effect requiring damage on a status-only host;
- duplicate singleton effect;
- incompatible operation pair;
- invalid chance;
- invalid duration;
- topology or ruleset mismatch;
- reference to missing type, status, condition, move, item, or ability;
- effect ordering that cannot produce the intended behavior;
- no-op configuration;
- operation that compiles but is not Creator Ready for this context.

Safe corrections may be offered as explicit actions:

- `Change move to Status`;
- `Change target to User`;
- `Remove unsupported Chance`;
- `Use percentage of damage dealt`;
- `Pick a persistent status`;
- `Move this effect to the On End Of Turn hook`.

Automatic fixes must show exactly what they change and be one undoable edit. The Creator must not
silently rewrite behavior to make validation green.

### 19.18 Effect compatibility assistant

**Candidate**

When a user changes a foundational field such as move target, damage class, item usability, or
ability hook, the Creator should:

1. Re-evaluate every effect.
2. Keep compatible effects unchanged.
3. Mark incompatible effects without deleting them.
4. Explain the exact conflict.
5. Offer explicit compatible transformations where the catalog defines a lossless mapping.
6. Block save/export only according to validation severity and existing project rules.

Changing a move from damaging to status-only, for example, must not silently discard drain,
recoil, or damage-dependent secondary behavior.

### 19.19 Move creation studio

**Accepted direction**

The move editor should be organized into:

1. **Identity**: name, stable ID, description where supported, type, icon/animation references.
2. **Core behavior**: damage class, power or damage model, accuracy, PP, priority, critical stage,
   contact, and target.
3. **Effects**: ordered composer.
4. **Presentation**: supported battle animation, sound, and text/event presentation references.
5. **Summary and validation**: plain-language result, warnings, and technical preview.
6. **Test**: launch the Runtime battle sandbox when Phase 17F is available.

The editor should use progressive disclosure:

- ordinary power is the default for physical/special moves;
- specialized damage formulas appear through the effect chooser;
- fields incompatible with the chosen damage class are hidden or disabled with an explanation;
- target diagrams show who is affected in singles and doubles;
- `never misses` is described as a mechanic, not represented by a mysterious blank without help;
- chance belongs to the specific effect it gates.

### 19.20 Item creation studio

**Accepted direction**

Items should distinguish three contexts:

- **Field use**;
- **Active battle use**;
- **Held behavior**.

The item editor should provide:

- identity, pocket, price, icon, key/consumable flags;
- explicit context toggles;
- target rules per context;
- ordered field-use effects;
- ordered active-battle effects;
- held-item hook list with ordered effects per hook;
- consumption timing and failure behavior;
- plain-language summary per context;
- usage references from shops, trainers, creatures, and maps;
- Runtime test scenarios where supported.

Only operations ready for the selected item context appear. A healing operation that works for
active item use must not automatically appear in a held-item damage hook without separate proof.

### 19.21 Ability creation studio

**Accepted direction**

Abilities should be authored as a list of supported hook cards.

`Add Hook` opens a hook chooser containing only hook points implemented by the current engine.
After choosing a hook, its effect composer shows only operations ready for that hook and source/
target scope.

Each hook card should show:

- trigger in plain language;
- source and target;
- allowed filters/requirements;
- ordered effects;
- timing relative to moves, items, conditions, weather, terrain, and fainting;
- multiplicity and conflict rules;
- emitted events;
- AI disposition.

Examples:

- when the creature enters battle;
- when modifying outgoing damage;
- when modifying incoming damage;
- when a status is attempted;
- when contact is received;
- at end of turn;
- when weather or terrain changes;
- when grounded state is queried;
- when an escape is attempted;
- when the creature faints.

The Creator must not offer a free-text hook name, arbitrary event listener, or script.

### 19.22 Creature/species creation studio

**Accepted direction**

Creature creation should be a guided multi-section studio rather than one long form:

1. **Identity and classification**: name, stable ID, one or two types, size/weight, tags where
   supported.
2. **Battle stats**: six-stat editor, total, distribution visualization, min/max validation, and
   optional archetype starting points that remain fully editable.
3. **Progression**: growth curve, base experience, capture, happiness, EV yield, and related
   supported values with units and help.
4. **Abilities**: normal and hidden slots through reference pickers with duplicate and availability
   checks.
5. **Learnset**: sortable level/move rows, duplicate/order validation, filters by type/category, and
   direct navigation to move creation.
6. **Evolutions**: structured trigger builder containing only engine-supported triggers and
   parameters.
7. **Forms**: structured activation, stat/type/ability/art overrides, requirements, duration, and
   move remaps where supported.
8. **Presentation**: front, back, icon, overworld, cry, and native-scale previews according to the
   current schema.
9. **Relationships**: evolution-family and form graph.
10. **Summary and readiness**: completeness checklist, validation, usages, and playtest links.

Archetype buttons such as balanced, fast attacker, or defensive may populate neutral starting
values, but they never hide or add engine behavior and should not be treated as balance guarantees.

### 19.23 Evolution and form builders

**Accepted direction**

Evolution creation should ask:

- What is the target creature?
- Which supported trigger causes the evolution?
- Which parameters does that trigger require?
- Are time, item, held item, known move, happiness, location, or gender constraints supported by the
  current engine?
- Can the condition ever be satisfied under current project data?

Only fields relevant to the chosen trigger are displayed.

Form creation should similarly show only engine-supported activation modes and overlays. A form
configuration is not selectable merely because the schema can preserve its fields; it must meet the
same Creator Ready context proof as effects.

### 19.24 Trainer, encounter, shop, and object creation

**Accepted direction**

The same design language extends beyond battle mechanics:

- **Trainer**: guided party builder, AI profile, rewards, dialogue, defeat state, and placed usages.
- **Encounter table**: method, weighted slots, levels, conditions, calculated percentages, and
  deterministic simulation.
- **Shop**: ordered inventory with item picker, price preview, availability conditions where
  supported, and map usages.
- **Object/interaction**: prefab presentation/collision plus a structured world-action composer.
- **Trigger**: condition chooser and ordered world-action composer.
- **Dialogue/sign**: structured text, speaker/presentation references where supported, and Runtime
  fit preview.

Each chooser is driven by the engine-owned catalog or schema vocabulary for that context. Free-text
operation names are not allowed.

### 19.25 Reusable composer controls

The Creator should build one family of tested controls and reuse it:

- capability chooser;
- typed parameter form;
- ordered effect list;
- hook list;
- condition/filter builder;
- target/scope picker;
- exact fraction/percentage editor;
- probability editor;
- duration editor;
- stat/type/status picker;
- entity reference picker;
- weighted table;
- numeric band/formula table;
- plain-language summary;
- read-only normalized preview;
- inline compatibility and validation panel.

Domain editors configure which contexts and catalogs apply. They do not copy battle rules into
their ViewModels.

### 19.26 Copy, paste, favorites, and recipes

**Candidate**

Advanced users should be able to:

- duplicate an effect within a list;
- copy effects between moves;
- copy a valid composition from a move into a recipe;
- paste into another context with a compatibility preview;
- favorite effects and recipes;
- view recent effects;
- replace one effect operation with a compatible alternative while retaining shared parameters;
- search project-wide for a capability's usages.

Paste never drops unsupported fields silently. It either maps every field, asks for explicit
choices, or refuses with a clear reason.

### 19.27 Runtime mechanic sandbox

**Accepted direction**

Once Phase 17F Runtime process workflows are available, complex editors should provide `Test`
without simulating battle rules in Creator.

The battle sandbox should let the user configure a neutral scenario:

- source and target creature;
- level, HP, stats, types, status, stages, ability, and held item;
- singles or doubles topology where supported;
- weather, terrain, side/field conditions, and ruleset;
- move or item under test;
- deterministic seed;
- selected action sequence.

The Runtime returns:

- events;
- effect trace;
- HP/status/stage changes;
- chance and RNG draws;
- failures/immunities/guards;
- normalized mechanic IDs;
- resulting battle snapshot;
- crash or validation diagnostics.

The Creator presents the report next to the authored content and links trace rows back to effect
cards. A successful sandbox run is useful evidence, but does not replace catalog certification and
automated tests.

### 19.28 Streamlining without hiding power

The system should minimize user confusion through:

- sensible context-aware defaults;
- recipes for common patterns;
- plain-language names;
- search aliases;
- diagrams for targets and timing;
- exact units;
- immediate validation;
- previews before destructive or structural changes;
- progressive disclosure;
- direct links between references;
- `Create New` from reference pickers without losing the current edit;
- completion checklists;
- no empty screens that require knowing an internal ID.

It should preserve power through:

- ordered multi-effects;
- multiple hooks;
- exact formulas and parameters;
- reusable conditions/presets;
- target/scope/timing visibility;
- copy/paste and recipes;
- normalized technical preview;
- Runtime sandbox traces;
- no arbitrary small limit on effects beyond documented safety and semantic constraints.

Simple means understandable and efficient, not mechanically shallow.

### 19.29 Authoring safety and preservation

Unknown, newer-version, or legacy effects must be displayed as preserved blocking cards containing:

- original operation ID;
- original parameter names and read-only values;
- catalog/schema version when known;
- why the current engine cannot author it;
- whether Runtime can still preserve or execute it;
- migration/replacement action when one exists.

The Creator must never:

- delete an unknown effect on save;
- replace an unknown effect with a guessed operation;
- silently clamp a value with different semantics;
- flatten multiple effects into one lossy summary;
- reorder effects during load/save;
- expose a capability from documentation that the current build cannot execute;
- implement game logic in the ViewModel to make a preview appear functional.

### 19.30 Acceptance criteria

The engine-backed creation system is ready only when:

- schema-to-editor coverage is 100% for authorable content;
- compiler/dispatcher-to-catalog coverage is 100%;
- every selectable parameter has type, unit, default, range, help, and compatibility metadata;
- every Ready capability has §19.3 evidence for each exposed context;
- every content category can be created, edited, duplicated, validated, saved, reopened, and deleted
  without writable JSON;
- effect lists support add/configure/reorder/duplicate/remove/copy/paste/undo;
- multi-effect behavior summaries match normalized Core explanations;
- incompatible host changes preserve affected rows and provide actionable diagnostics;
- unknown and deprecated payloads round-trip without loss;
- representative recipes expand to ordinary visible operations;
- moves support damage plus status, damage plus stat change, drain, recoil, multi-hit, healing, and
  other Ready composition families without bespoke move code;
- items clearly separate field, battle-use, and held contexts;
- abilities expose only Ready hook/effect combinations;
- creature evolutions/forms expose only engine-supported triggers and fields;
- a representative entry from every mechanic family passes author -> save -> load -> validate ->
  compile -> resolve -> event/trace verification;
- keyboard, focus, screen-reader labels, and error navigation pass accessibility tests;
- the no-JSON project-authoring trial is completed by a user who did not implement the engine.

## 20. Campaign, Scenario, and Event Graph

### 20.1 Goal

**Accepted direction**

The Creator must let an author build and track a complete campaign without writing scripts or
manually coordinating undocumented flag names.

The campaign system has two connected scales:

- **Campaign Story** describes the game's overarching progression: chapters, main quests, side
  quests, objectives, branches, unlocks, endings, and postgame.
- **Scenarios** describe concrete sequences that occur in the world: an off-screen rival enters,
  speaks, battles the player, changes story state, and leaves; a puzzle opens a door; interacting
  with an object starts a conversation; stepping into a region triggers an ambush.

Both use engine-owned, typed conditions, triggers, actions, and event nodes. The Creator never
offers an arbitrary code node, free-text operation name, reflection call, or general scripting
language.

### 20.2 Authoring vocabulary

**Accepted direction**

The system should use distinct concepts:

- **Campaign**: the project's top-level story/progression definition.
- **Arc/Chapter**: an author-facing organizational group and optional progression boundary.
- **Quest**: a tracked main, side, optional, hidden, or system objective group.
- **Objective**: one measurable piece of quest progress.
- **Milestone**: a stable campaign state used for validation, testing, and game completion.
- **Scenario**: a reusable, executable event graph triggered in a world or campaign context.
- **Trigger**: the engine-backed event that attempts to start a scenario.
- **Condition**: a typed, read-only requirement evaluated by Core.
- **Event node**: one Creator Ready action or flow operation in a scenario.
- **Story state**: declared bool/int flags, quest/objective state, and other closed campaign values
  stored in the save.
- **Actor binding**: a stable reference from a scenario role such as `rival` to a placed or
  scenario-spawned entity.
- **Scenario instance**: the running state of one triggered scenario.

These terms should appear consistently in the map editor, campaign editor, validation, Runtime
trace, and generated help.

### 20.3 Campaign Story graph

**Accepted direction**

The Campaign Story editor should provide a high-level graph containing:

- New Game entry;
- chapters/arcs;
- main quests;
- side and optional quests;
- milestones;
- mutually exclusive branches where supported;
- required and optional prerequisites;
- unlocks;
- rewards;
- ending conditions;
- postgame entry;
- game-complete state.

The graph is not the event execution graph. It shows progression relationships and links each
objective or milestone to the scenarios, battles, items, maps, flags, or conditions that advance
it.

An author should be able to select any node and answer:

- What makes this available?
- What starts it?
- What completes it?
- What can fail or lock it?
- What content does it unlock?
- Which scenarios read or write it?
- Can the player reach it from New Game?
- Is it required for an ending?
- What player-facing text tracks it?

### 20.4 Quest and objective model

**Accepted direction**

Quests should support these classifications:

- main;
- side;
- optional;
- hidden;
- system/tutorial;
- postgame.

Quest states should use a closed engine-owned vocabulary, initially:

- unavailable;
- available;
- active;
- completed.

Failed, canceled, repeatable, timed, or branching terminal states are selectable only when the
engine has explicit, tested semantics for them.

Creator Ready objective types may include:

- reach a map;
- enter or leave a named region;
- interact with an entity or object;
- speak with an NPC;
- defeat a trainer;
- win a referenced battle scenario;
- acquire or possess an item;
- capture or possess a creature;
- set or compare a declared story value;
- solve a referenced puzzle;
- complete another objective or quest;
- trigger or complete a scenario;
- reach a supported progression value.

Each objective descriptor must define what event advances it, whether progress is boolean or
counted, how it is saved, and how duplicate events are handled.

### 20.5 Scenario definition

**Accepted direction**

A scenario should contain:

- stable scenario ID and display name;
- description;
- entry trigger bindings;
- start conditions;
- actor bindings;
- local variables from a closed typed vocabulary where needed;
- event graph;
- completion exits;
- cancellation/failure exits where supported;
- story-state mutations;
- re-entry/repeat policy;
- priority and exclusivity policy;
- debug/test starting state;
- plain-language summary;
- usage list.

Scenarios may be map-specific or reusable. A reusable scenario declares actor and location
parameters rather than embedding one map's entity keys.

### 20.6 Scenario trigger catalog

**Accepted direction**

The Creator should offer these triggers only as they become Creator Ready:

- player enters one tile;
- player enters any tile in a painted trigger region;
- player leaves a tile or region;
- player interacts with an NPC;
- player interacts with an object;
- player interacts with a trigger volume;
- player approaches within a supported distance;
- trainer/NPC line-of-sight detects the player;
- player enters a map;
- player leaves a map;
- a referenced puzzle becomes solved;
- a quest or objective changes state;
- a declared bool/int story condition becomes true;
- a battle produces a supported result;
- the player acquires a referenced item;
- the player captures or obtains a referenced creature;
- a referenced scenario completes;
- a supported time-of-day condition is reached;
- an explicit typed campaign transition requests it.

The map editor should paint or place spatial triggers and display:

- trigger area;
- facing/interaction side when relevant;
- scenario reference;
- conditions;
- priority;
- once/repeat policy;
- validation state.

### 20.7 Trigger policies

**Accepted direction**

Every trigger should declare an engine-supported activation policy:

- once per save;
- once while a condition remains true;
- once per map entry;
- repeatable after scenario completion;
- manually reset by a typed campaign action;
- disabled after a referenced flag/objective state.

Cooldowns or real-time timers are not exposed until their persistence and determinism contracts are
defined.

When multiple triggers are eligible at one checkpoint, Core must choose deterministically. The
Creator should display the exact priority/order and warn when two blocking triggers compete for the
same tile, interaction, sight result, or campaign transition.

Existing overworld ordering such as warp, tile trigger, trainer sight, and encounter resolution must
be deliberately reconciled into the scenario dispatcher. A scenario graph cannot silently bypass or
reorder Core's checkpoint rules.

### 20.8 Trigger conditions

**Accepted direction**

Triggers and branches should use a shared typed condition composer. Creator Ready conditions may
read:

- declared bool/int story values;
- quest/objective state;
- scenario completion state;
- trainer defeated state;
- item possession/count;
- creature seen/caught/owned state where supported;
- current map/region;
- time of day;
- player direction;
- difficulty profile;
- supported battle result;
- supported party state;
- puzzle state.

The composer should support closed `all`, `any`, and `not` groups with depth/size safety limits. It
must show a plain-language expression and identify every value read.

Conditions never mutate state. Mutation occurs only through typed action nodes.

### 20.9 Event node catalog

**Accepted direction**

Scenario graphs should use a Core/Runtime-owned catalog with the same Creator Ready guarantee as
mechanic effects.

Initial node families should include, where supported:

**Flow**

- entry;
- sequence;
- branch on typed condition;
- wait for a child action to complete;
- parallel presentation group only when deterministic join semantics exist;
- scenario complete;
- scenario cancel/fail where supported;
- call a reusable scenario with typed parameters and bounded recursion.

**Story state**

- set/clear bool;
- set/add/subtract bounded int;
- start/complete objective;
- start/complete quest;
- record milestone;
- unlock supported content;

**Actor and map**

- bind placed actor;
- show/hide scenario actor;
- place/spawn scenario-owned actor at an authored anchor;
- remove a scenario-owned actor;
- face direction;
- face another actor;
- move along authored path;
- walk toward an adjacent interaction position;
- play supported sprite animation;
- enable/disable supported entity interaction;
- warp player;
- change supported object/entity state.

**Dialogue and choices**

- show dialogue;
- set speaker/portrait/expression where supported;
- present a typed choice;
- branch from choice;
- show notification;

**Presentation**

- lock/release player control;
- camera focus/path where supported;
- fade or supported screen transition;
- play/stop/change music;
- play SFX;
- show emote;
- wait fixed ticks;
- invoke a registered presentation timeline;

**Gameplay requests**

- start referenced trainer battle;
- start referenced fixed/wild battle scenario;
- give/remove item through Core;
- heal through Core;
- open shop/storage/service;
- request a Creator Ready world action;

**Campaign**

- update quest/objective;
- activate another scenario;
- request ending/postgame transition where supported.

No node may directly edit player HP, inventory, battle state, money, creature state, save files, or
Runtime scene state outside the corresponding Core/request boundary.

### 20.10 Rival-arrival scenario

**Accepted direction**

The user's off-screen rival example should be straightforward to author:

1. Paint a trigger region across the selected route tiles.
2. Set start conditions such as `rival_battle_1` not completed.
3. Bind the `rival` role to a hidden placed NPC or scenario-owned actor at an off-screen anchor.
4. On trigger, lock player movement and reserve the participating actors.
5. Show the rival.
6. Move the rival down an authored path until reaching an adjacent interaction point.
7. Face the rival and player toward one another.
8. Show the authored monologue.
9. Start the referenced trainer battle through the typed battle request.
10. Branch on the supported battle result.
11. On player victory, set the trainer/scenario defeat state and complete the campaign objective.
12. Run post-battle dialogue or movement.
13. Hide/remove or release the rival actor according to the scenario.
14. Release player control.
15. Mark the scenario complete so the trigger cannot fire again.

The graph preview should show the path, final adjacency, dialogue, battle reference, state writes,
and every exit. Playtest should be launchable from the trigger with a temporary precondition state.

### 20.11 Interaction scenarios

**Accepted direction**

The same scenario system should support:

- interact with an NPC to begin dialogue, choice, service, quest, or battle;
- interact with an object to inspect, collect, activate, move, or change it through typed actions;
- interact with a door, console, statue, sign, machine, chest, switch, or puzzle component;
- step on a tile or region;
- approach an actor;
- complete a battle;
- complete a puzzle;
- satisfy a campaign condition.

The author should not need a distinct hard-coded trigger class for every story use. Trigger type
selects when the scenario begins; the graph defines what happens.

### 20.12 Puzzle completion

**Accepted direction**

Puzzles should integrate through declared, engine-backed puzzle state rather than an arbitrary
script check.

A puzzle definition may contain:

- stable ID;
- participating entity keys/objects;
- typed input events;
- bounded bool/int state;
- reset policy;
- completion condition;
- solved state;
- completion scenario;
- visualization and debug reset.

Creator Ready puzzle component families may eventually include:

- switches and doors;
- ordered switches;
- pressure plates;
- movable grid objects;
- rotating objects;
- matching symbols;
- path/connection state;
- item-use interaction;
- trainer/battle completion gates.

When a puzzle's completion condition changes from false to true, it may trigger a scenario. The
Creator should show every input capable of changing the puzzle and detect impossible or circular
completion conditions.

This remains a closed component catalog, not a general logic-programming surface.

### 20.13 Actor bindings and ownership

**Accepted direction**

Scenario actors should be bound by stable role names such as:

- player;
- rival;
- companion;
- guard;
- door;
- target object.

A role may bind to:

- one placed map entity key;
- the triggering entity;
- the interacting entity;
- a scenario-owned actor definition and spawn anchor;
- another explicitly supported contextual actor.

The Creator must validate:

- correct entity kind;
- actor exists in every use;
- required sprite/animation;
- spawn cell/path is valid;
- no conflicting scenario owns the same actor;
- removal applies only to scenario-owned or explicitly removable actors.

Blocking scenarios reserve their controlled actors. Ambient movement, trainer sight, or another
scenario cannot simultaneously move or rotate a reserved actor.

### 20.14 Scenario execution model

**Accepted direction**

The execution contract should be deterministic:

- graphs compile into a closed typed node plan;
- node and edge order is stable;
- waits use fixed ticks;
- randomness uses a named injected RNG stream only for nodes whose descriptors allow it;
- a node emits events/traces for start, completion, failure, and state mutation;
- Runtime presents node requests but does not decide campaign rules;
- Core owns story state and gameplay mutations;
- transitions occur only at defined checkpoints;
- recursion, total executed nodes, and nested scenario depth have hard safety caps;
- a malformed or over-budget scenario fails visibly rather than hanging.

The initial safe concurrency model should allow at most one blocking scenario controlling the player
and map actors. Non-blocking presentation or ambient scenarios should be deferred until ownership,
save, and conflict semantics are proven.

### 20.15 Save and resume policy

**Accepted direction**

Campaign progress, quest/objective states, scenario completion, puzzle state, and declared story
values belong in versioned save data.

The recommended first scenario-save policy is:

- saving is disabled while a blocking scenario is between safe checkpoints;
- every scenario has a stable pre-start and post-completion state;
- battle transitions may save only under the ordinary Runtime save policy;
- map reload resumes from committed story state, not from an arbitrary presentation frame;
- temporary actor positions, camera state, partial dialogue, and node instruction pointers are not
  saved initially.

Mid-scenario save/resume is selectable only after every node type declares serializable state,
rollback behavior, asset/reference compatibility, and migration rules.

### 20.16 Campaign and scenario editor

**Accepted direction**

The Creator should provide:

- Campaign Story graph;
- quest/objective list and inspector;
- scenario graph;
- typed node chooser;
- actor-binding panel;
- declared story-value panel;
- map/region/entity picker;
- plain-language graph outline;
- read/write dependency view;
- validation panel;
- trace panel;
- minimap/scene preview for spatial nodes;
- test-state builder;
- usages and reverse references;
- search by scenario, quest, objective, value, map, or actor.

Graph editing should support:

- add/remove/connect;
- selection and multi-select;
- align and tidy layout;
- collapse organization groups that do not change execution;
- comments/notes excluded from Runtime;
- copy/paste with reference mapping;
- reusable scenario templates;
- undo/redo for every graph edit;
- keyboard navigation;
- no executable behavior hidden inside visual grouping nodes.

### 20.17 State simulator and Runtime trace

**Accepted direction**

The Creator may perform read-only static evaluation of conditions and graph reachability, but actual
scenario execution belongs to Runtime.

`Test Scenario` should launch Runtime with:

- selected scenario/trigger;
- map and spawn position;
- declared story values;
- quest/objective states;
- puzzle state;
- party/inventory/difficulty preset where relevant;
- deterministic seed;
- optional start node only in debug mode.

Runtime should return:

- executed node order;
- condition results;
- actor movements;
- dialogue/choice path;
- battle request/result;
- story values read and written;
- quest/objective transitions;
- scenario completion state;
- failures, blocked nodes, and safety-limit diagnostics.

Trace rows should navigate back to exact graph nodes and map coordinates.

### 20.18 Scenario templates and reuse

**Accepted direction**

Built-in templates should expand into visible ordinary nodes:

- walk-on rival challenge;
- interaction trainer battle;
- NPC conversation;
- item pickup with dialogue;
- locked door and key;
- switch opens door;
- puzzle solved event;
- quest start;
- quest completion and reward;
- cutscene then warp;
- battle then actor leaves;
- one-time map entrance;
- choose one of several rewards.

Project-local templates may expose typed parameters such as actor, path, dialogue, battle, reward,
flag, or objective. They cannot contain hidden script text or bypass node validation.

### 20.19 Campaign validation and softlock analysis

**Accepted direction**

Validation should detect:

- graph node with no entry or exit;
- unreachable node;
- unbounded cycle;
- recursive scenario call cycle;
- missing actor/map/region/entity reference;
- story value read but never declared or written;
- value written but never read;
- objective with no completion source;
- required quest with no activation path;
- completed objective that cannot advance its quest;
- ending with no path from New Game;
- mutually exclusive prerequisites that make a node impossible;
- scenario that can retrigger while already running;
- competing blocking triggers;
- actor ownership conflict;
- movement path crossing invalid collision;
- battle branch missing a supported result exit;
- trigger that remains permanently true after a repeatable completion;
- puzzle completion with no possible input path;
- campaign milestone that depends on itself;
- required content available only after it is required.

The analyzer should show a dependency path rather than only an error string.

### 20.20 In-game campaign tracking

**Accepted direction**

The author should choose which campaign information appears to the player.

Supported presentation may include:

- main objective HUD line;
- quest journal;
- active/completed sections;
- objective descriptions;
- progress counts;
- map/region hints;
- hidden objectives;
- completion notifications;
- main-story recap;
- optional pin/unpin where supported.

Campaign state exists independently of whether it is displayed. A linear game may use the Campaign
Story graph only for authoring and validation, while another game may expose a full journal.

### 20.21 Campaign acceptance criteria

The Campaign/Scenario system is ready only when:

- every node, trigger, condition, and state mutation is engine-owned and Creator Ready;
- a complete main-story path from New Game to ending can be authored without JSON or code;
- main, side, optional, hidden, tutorial, and postgame classifications round-trip correctly;
- tile, region, interaction, NPC, object, trainer-sight, puzzle, battle-result, and campaign triggers
  work where marked Ready;
- the rival-arrival scenario in §20.10 runs deterministically and only once;
- actor reservation prevents conflicting movement/scenarios;
- quest/objective progress persists through save/reload;
- partial blocking scenarios obey the locked save policy;
- graph validation catches unreachable nodes, unsafe cycles, missing exits, and undeclared state;
- Runtime traces navigate back to nodes and coordinates;
- scenario templates expand into inspectable ordinary nodes;
- no general script, arbitrary expression, or UI-owned gameplay mutation exists;
- a deterministic acceptance journey proves campaign start, branching scenario, puzzle completion,
  trainer battle, objective update, save/reload, ending, and postgame transition.

## 21. Trainer and NPC Behavior Studio

### 21.1 Definition versus placement behavior

**Accepted direction**

Trainer authoring should separate:

- **Trainer definition**: identity, art, battle team, preset AI difficulty, rewards, and dialogue.
- **Placed trainer behavior**: map position, facing, trigger mode, sight, rotation, patrol route,
  movement timing, campaign conditions, and scenario integration.

This allows one trainer definition to be used in different maps or story contexts without embedding
map coordinates and patrol paths into battle-team data.

The placed behavior owns overworld movement. The trainer definition owns battle content. A scenario
may temporarily take control of the placed actor through the reservation contract in §20.13.

### 21.2 Trainer identity and battle configuration

**Accepted direction**

The Trainer Studio should configure:

- stable ID;
- name;
- class/title;
- overworld sprite/animation;
- portrait;
- battle sprite;
- battle transition/presentation profile where supported;
- battle music where supported;
- preset AI difficulty;
- trainer importance/role only through engine-provided presets;
- money and other supported rewards;
- party;
- finite battle items where supported;
- sight/approach/pre-battle/defeat/post-defeat dialogue;
- defeat-state and rematch policy where supported;
- campaign/quest usages.

All fields should use structured controls and native-scale previews.

### 21.3 Trigger modes

**Accepted direction**

A placed trainer should offer Creator Ready trigger modes:

- sight;
- interaction;
- scenario/event graph;
- tile/region scenario;
- campaign-triggered;
- disabled/ambient until enabled by a condition.

Sight and interaction may coexist: an undefeated trainer can spot the player at range or be spoken to
from outside its sight line. Scenario-only trainers do not independently initiate battle.

Trigger configuration should include:

- required story/quest conditions;
- defeated-state behavior;
- once/repeat/rematch policy where supported;
- trigger priority;
- linked pre-battle scenario;
- fallback when the trainer cannot approach;
- post-battle scenario.

### 21.4 Movement modes

**Accepted direction**

A placed trainer or ordinary NPC should select one Creator Ready movement mode:

- static;
- face/rotate in place;
- wander within radius;
- patrol route;
- scenario-controlled;
- disabled.

Movement mode and detection are separate. A rotating, wandering, or patrolling trainer can retain an
active sight line while moving.

The Creator should expose only timing and movement options implemented by the current Runtime.
Arbitrary speed curves or physics movement are not supported.

### 21.5 Rotation in place

**Accepted direction**

Rotation behavior should support:

- initial facing;
- rotation direction: clockwise, counter-clockwise, or a supported authored facing sequence;
- ticks/seconds between turns using a typed fixed-tick duration;
- optional wait count per facing;
- pause while the player or another actor blocks the trainer;
- active sight check after each facing change;
- optional interaction trigger in every facing;
- preview and deterministic trace.

`Rotation speed` must be labeled in understandable time and stored in deterministic fixed ticks.
Random rotation is selectable only if it uses an injected RNG stream with tested save/replay
semantics.

### 21.6 Patrol route editor

**Accepted direction**

The route editor should let an author draw ordered waypoints directly on the map:

```text
A -> B -> C -> D -> A
```

Each waypoint may define supported options:

- tile coordinate;
- wait duration;
- facing while waiting;
- optional emote/animation;
- optional scenario marker;
- pathfinding versus exact authored cell path.

Route policies should include:

- loop to start;
- ping-pong/reverse at end;
- one pass then stop;
- scenario-owned route.

The user's required baseline is a looped route returning from the final waypoint to the first.

The map should display:

- waypoint numbers;
- direction arrows;
- connecting path;
- invalid/blocked cells;
- sight rays from selected path positions;
- start position;
- loop closure;
- estimated loop duration.

The Creator should allow dragging waypoints, inserting between points, deleting, reordering, and
playing a fixed-tick preview.

### 21.7 Exact path versus pathfinding

**Accepted direction**

The author should deliberately choose:

- **Exact cell path**: the actor follows every authored cell. Best for cinematic and precise routes.
- **Waypoint pathfinding**: the actor uses the shared deterministic grid pathfinder between
  waypoints. Best when the route should survive modest map edits.

The preview must show the resolved path. If a waypoint becomes unreachable, validation blocks the
route rather than teleporting or silently skipping it.

Dynamic avoidance policy must be explicit. The recommended baseline is:

- wait when another moving actor temporarily blocks the next cell;
- resume when clear;
- report a diagnostic after a bounded stall in debug mode;
- do not reroute unless the selected movement profile explicitly permits deterministic rerouting.

### 21.8 Sight-line configuration

**Accepted direction**

Trainer sight should expose:

- enabled/disabled;
- sight distance in grid cells;
- current facing;
- detection while static;
- detection while rotating;
- detection while wandering;
- detection during patrol;
- blockers visualized from shared collision/entity rules;
- campaign/defeat conditions;
- debug overlay.

The current engine uses a straight line in the trainer's facing direction. The Creator should show
that exact line, not a cone, until a cone or wider field-of-view model becomes separately
implemented and Creator Ready.

Sight distance should be clamped to an engine-owned safe range and drawn on the map. Solid cells and
eligible blocking entities should stop the ray exactly as Runtime does.

### 21.9 Sight while moving

**Accepted direction**

A trainer following a route must perform sight checks at deterministic movement checkpoints:

- after completing a step;
- after changing facing;
- after arriving at a waypoint and applying its authored facing;
- before beginning the next patrol step if the checkpoint contract requires it.

The exact order must be shared with Runtime and visible in the behavior summary.

If the trainer detects the player:

1. Pause its route.
2. Reserve the trainer and player for the encounter scenario.
3. Show the configured alert/emote where supported.
4. Face the player.
5. Compute or validate the approach path.
6. Walk to the configured adjacent interaction position.
7. Run sight/pre-battle dialogue.
8. Start the trainer battle.
9. Apply defeat/reward/campaign results.
10. Run post-battle scenario.
11. Resume, reset, stop, or change behavior according to the selected post-battle policy.

### 21.10 Approach behavior

**Accepted direction**

Approach configuration should support:

- stop adjacent to player;
- preferred interaction side where reachable;
- maximum approach distance;
- shared collision/pathfinding;
- behavior if no adjacent cell is reachable;
- alert/emote timing;
- walking animation;
- player control lock;
- final facing;
- battle-start delay/transition.

The trainer must never walk through walls, other blocking actors, or invalid tiles merely to make the
battle happen.

If approach becomes impossible after detection, the engine should use one explicit profile:

- remain in place and speak/start battle at range if the profile permits;
- cancel detection and resume route;
- fail visibly in debug and cancel safely.

The Creator must not leave this as undefined Runtime improvisation.

### 21.11 Combining movement and triggers

**Accepted direction**

The Creator should make these combinations straightforward:

- static + sight;
- static + interaction;
- rotating + sight + interaction;
- wander + sight + interaction;
- patrol loop + sight + interaction;
- patrol loop + campaign condition + sight;
- scenario-controlled arrival + dialogue + battle;
- patrol until objective state, then static;
- post-defeat ambient NPC with different dialogue and no battle sight.

The inspector should summarize the complete composition:

> Patrols Route A in a loop. Waits 1 second at points B and D. Looks clockwise every 0.5 seconds
> while waiting. Detects the player up to 5 cells ahead during movement and rotation. On detection,
> approaches, speaks, and starts the referenced trainer battle. After defeat, stops patrolling and
> uses Post-Defeat dialogue.

### 21.12 Campaign and scenario integration

**Accepted direction**

A trainer placement should be usable as:

- an autonomous sight trainer;
- an interaction trainer;
- an actor controlled by a scenario;
- a campaign objective target;
- a puzzle participant;
- a post-defeat NPC;
- an actor whose movement mode changes when story state changes.

Scenario control temporarily overrides ambient movement and sight through actor reservation. When
released, the trainer resumes from a defined state:

- resume current route segment;
- reset to route start;
- remain at scenario destination;
- switch to another Creator Ready movement profile;
- disable after defeat.

The selected policy must be visible and validated.

### 21.13 Trainer dialogue lifecycle

**Accepted direction**

Trainer dialogue should distinguish:

- alert/sighted text or emote;
- approach dialogue;
- battle introduction;
- optional battle-result dialogue request where supported;
- defeat dialogue;
- post-defeat interaction dialogue;
- rematch dialogue where supported;
- scenario-specific overrides.

Dialogue should use references to structured conversations once that system exists rather than
duplicating large strings in each placement.

### 21.14 Team customization

**Accepted direction**

Trainer teams should support every battle field that is Creator Ready:

- ordered party;
- lead/slot placement;
- creature;
- level;
- form;
- ability;
- nature;
- IVs;
- EVs when the trainer model supports them;
- held item;
- exact move set;
- current PP/default PP policy;
- finite trainer item stock where supported;
- doubles active-slot arrangement;
- legal overrides explicitly supported by the ruleset.

The Studio should provide:

- creature and move search;
- legality validation;
- computed stats through Core;
- move/type coverage;
- speed ordering;
- duplicate/restriction checks;
- native battle-art preview;
- seeded battle sandbox;
- comparison against an expected player team.

### 21.15 Preset-only AI policy

**Accepted direction**

Authors and players must not edit AI scoring weights, thresholds, noise, prediction ratios,
knowledge flags, switch formulas, or other internal tuning values.

The only AI choices exposed in the Creator or exported game are engine-provided, versioned,
pre-tuned presets that have been implemented and tested. The initial choices map to the current
engine profiles:

- Random;
- Basic;
- Smart.

Friendly labels such as Beginner, Advanced, or Expert may replace or map to those names only when
the corresponding preset is fully implemented and the mapping is locked by `BATTLE_AI_SPEC.md`.

The Creator may show:

- intended challenge;
- supported capabilities;
- fairness/knowledge policy;
- whether switching or items are supported;
- recommended trainer role;
- seeded benchmark results;
- engine/catalog version.

The Creator must not show:

- weight sliders;
- numeric score components as editable values;
- noise controls;
- switch thresholds;
- prediction weighting;
- hidden-information toggles not represented by a fixed preset;
- a `Custom` AI option;
- editable AI profile JSON.

Trainer importance such as regular, boss, rival, or postgame may be exposed only as another fixed
engine preset or as content presentation metadata. It cannot become a back door to editable AI
weights.

Debug score tables remain read-only diagnostics for engine developers and author testing.

### 21.16 Trainer preview and testing

**Accepted direction**

The Trainer Studio should provide:

- map preview of movement, rotation, route, sight, and approach;
- fixed-tick playback controls;
- show sight rays at every waypoint/facing;
- simulate temporary blockers;
- trigger the encounter from a selected player tile;
- Runtime playtest with campaign prerequisites;
- seeded battle sandbox against a selected expected player team;
- read-only AI decision traces;
- post-battle state preview;
- save/reload behavior test.

The preview must use the same route, collision, sight, pathfinding, and timing helpers as Runtime or
be labeled static-only. The Creator cannot implement a parallel approximation and certify it as
exact.

### 21.17 Trainer validation

**Accepted direction**

Validation should detect:

- missing trainer definition;
- missing overworld/battle art;
- empty or illegal party;
- unavailable AI preset;
- sight distance outside supported bounds;
- sight ray permanently blocked at every possible facing;
- rotation interval invalid;
- route with fewer than required points;
- loop that cannot return to start;
- unreachable waypoint;
- exact route crossing collision;
- patrol cell conflicting with another fixed blocker;
- no legal adjacent approach cell;
- approach path exceeding maximum;
- scenario and ambient movement owning trainer simultaneously;
- trainer sight active after permanent defeat when not intended;
- post-battle resume policy incompatible with removed/hidden actor;
- dialogue or battle reference missing;
- campaign condition impossible;
- two trainers capable of triggering at the same checkpoint without deterministic priority;
- save/reload losing a required persistent trainer state.

### 21.18 Trainer acceptance criteria

The trainer/NPC behavior system is ready only when:

- static, rotation, wander, patrol, and scenario-controlled modes work where marked Ready;
- a looped A -> B -> C -> A route runs deterministically;
- rotation speed and waypoint waits use fixed ticks and survive preview/Runtime parity tests;
- trainer sight remains active during patrol and rotation;
- walls and blocking NPCs stop sight exactly as Core specifies;
- detection pauses ambient behavior and reserves actor ownership;
- approach uses valid collision/pathfinding and handles no-path cases safely;
- dialogue -> battle -> result -> campaign update ordering is deterministic;
- post-defeat behavior and dialogue are correct;
- team customization round-trips every Creator Ready field;
- only fixed pre-made AI presets are selectable;
- no editable AI weight, threshold, noise, or custom profile exists;
- route/sight/approach/scenario edits are undoable and save/reopen identically;
- a Runtime acceptance scenario proves a patrolling rotating trainer sees the player mid-route,
  approaches, speaks, battles, updates the main quest, and changes to its configured post-defeat
  behavior.

## 22. Dedicated content editors

The following editors should share Creator patterns but provide domain-specific views rather than
generic record forms.

### 22.1 Creature/species editor

**Candidate**

- identity, description, type, stats, growth, capture, and progression sections;
- front/back/icon/overworld sprite preview;
- stat total and distribution visualization;
- learnset table with level/order validation;
- evolution/form relationship view;
- missing-art and broken-reference diagnostics;
- preview at Runtime-native scale.

### 22.2 Move and effect editor

**Accepted direction**

- catalog-driven effect operations only;
- typed parameters and conditions;
- ordered effect pipeline;
- plain-language effect summary;
- legality and range validation;
- target and doubles-context explanation;
- source-derived or neutral test-vector preview where allowed;
- no move-name or move-ID bespoke behavior.

### 22.3 Item editor

**Candidate**

- inventory classification and price;
- field/battle/held usability;
- catalog-driven effects;
- icon preview;
- shop and usage references;
- plain-language behavior summary.

### 22.4 Encounter editor

**Accepted direction - engine-gated**

- slot table;
- weights with computed percentages;
- level ranges;
- method and conditional fields;
- deterministic distribution simulation;
- map/region usage list;
- warnings for unreachable or never-active conditions.

The complete approved encounter architecture, engine gaps, emergency prerequisite, painted-area
workflow, instance policies, validation, simulation, and Runtime fidelity gates are specified in
§15.4-§15.18. This editor is not eligible to expose a field until the matching emergency engine
acceptance row is green.

### 22.5 Trainer and party editor

**Accepted direction**

- trainer identity, art, dialogue, fixed AI preset, rewards, and defeat behavior;
- ordered party with species, level, moves, IVs, held items, and overrides;
- party legality and reference validation;
- compact matchup/coverage diagnostics powered by Core queries;
- placed behavior editor for trigger, rotation, patrol, sight, approach, and post-battle policy;
- campaign/scenario actor and objective integration;
- launchable battle sandbox once Runtime prerequisites are satisfied.

### 22.6 Dialogue and text editor

**Candidate**

- multiline text with Runtime-size preview;
- speaker/portrait references where supported;
- variable/token validation from a closed catalog;
- search across all authored text;
- overflow and unsupported-character diagnostics;
- navigation from placed signs, NPCs, and actions.

This does not authorize a general event scripting system or localization workflow before those are
separately scoped.

## 23. Validation and project health

### 23.1 Validation as a working surface

**Accepted direction**

Validation should be continuously useful, not a final modal dialog:

- errors, warnings, and information grouped and filterable;
- document and field/coordinate navigation;
- fix hints;
- safe automatic fixes only when the transformation is unambiguous and previewable;
- suppressions only for explicitly suppressible advisory rules, with reason;
- full validation forced before save/export gates where specified;
- validation summaries on navigation nodes and map thumbnails.

### 23.2 World-building diagnostics

The map editor should detect and visualize:

- missing ground or exposed empty cells inside gameplay bounds;
- invalid or missing tile references;
- off-map or isolated content;
- collision contradictions;
- unreachable required areas;
- invalid NPC paths;
- broken warp targets;
- duplicate or missing entity keys;
- overlapping unique entities;
- object footprints with missing cells or invalid anchors;
- encounter surfaces without tables;
- tables painted onto incompatible terrain;
- hidden content on locked or invisible layers;
- excessive layers, chunks, entities, or serialized size;
- unsupported Runtime render relationships.

### 23.3 Performance inspector

**Candidate**

Per map and project, show:

- occupied chunks;
- non-empty cells per layer;
- visual layer count;
- entities;
- unique sprites/animations;
- estimated draw submissions in representative viewports;
- serialized and decoded size;
- validation and export-budget status.

Warnings should link to the exact cause and suggested remediation. Metrics are diagnostics, not
arbitrary blockers unless a documented hard safety cap is reached.

### 23.4 Project health dashboard

**Candidate**

A project dashboard should summarize:

- validation counts;
- missing or changed assets;
- unused assets and entities;
- maps without player-access paths;
- incomplete authoring coverage;
- recent saves and recovery status;
- playtest/export readiness;
- schema and engine version;
- highest-impact next actions.

## 24. Playtest, preview, and debugging

### 24.1 Playtest entry points

**Accepted direction**

The Creator should be able to launch Runtime:

- from project start;
- from current map and cursor;
- from selected player start or warp arrival;
- from an encounter region;
- into a configured battle sandbox when that workflow is in scope.

Every launch goes through save/validation gates and explicit arguments. The Creator does not embed
Runtime gameplay.

### 24.2 Temporary playtest configuration

**Candidate**

A playtest configuration may specify:

- debug party;
- inventory/money;
- flags;
- time or environment state;
- selected map and position;
- deterministic seed where supported.

Configurations are Creator-private or explicitly marked development data and are excluded from
production export.

### 24.3 Return diagnostics

**Candidate**

When Runtime exits a development playtest, it may write a structured diagnostic report that the
Creator can open:

- last map and coordinate;
- warnings or assertion failures;
- missing asset/reference failures;
- deterministic replay or seed identifier;
- captured screenshot path;
- performance counters.

This is file-based process communication, not in-process simulation.

### 24.4 Visual previews

Creator previews should use the same source data and presentation rules as Runtime wherever
practical. When a preview is approximate, label it. Useful previews include:

- sprite and animation;
- tile, stamp, and object placement;
- map composite;
- collision and reachability;
- dialogue box fit;
- encounter distribution;
- creature and UI art at native scale.

## 25. Accessibility and ergonomics

### 25.1 Input

**Accepted direction**

- complete keyboard access to menus, document tabs, form fields, lists, and validation;
- visible focus;
- remappable world-editor shortcuts where practical;
- no tool that requires high-precision dragging without a numeric or keyboard alternative;
- cancelable gestures;
- adjustable double-click/drag assumptions through platform conventions.

### 25.2 Visual accessibility

**Candidate**

- UI scaling;
- high-contrast theme;
- color-blind-safe overlay palettes;
- patterns/icons in addition to color for collision and encounter types;
- adjustable grid and overlay opacity;
- native-scale preview reset;
- readable minimum text and icon sizes;
- no validation state communicated by color alone.

### 25.3 Motion and audio

**Candidate**

- animation-preview pause;
- reduced-motion option for Creator transitions;
- audio preview volume and stop-all command;
- no autoplay of audio on document open.

## 26. Performance and scale requirements

The robust Creator should remain responsive under representative stress fixtures, including:

- large sparse maps;
- many visual layers with mostly empty chunks;
- thousands of palette assets;
- hundreds of entities;
- project-wide usage search;
- bulk validation;
- large imported sheets within documented limits.

**Candidate acceptance budgets**

Exact numbers belong in implementation specs, but every relevant package should define and test:

- viewport redraw budget;
- pointer-to-preview latency;
- paint-stroke commit time;
- undo/redo time for large gestures;
- project open/save time;
- search and validation time;
- memory ceiling for a representative large project;
- cancellation behavior for longer analyses.

Long-running read-only work should be cancelable and report progress. Edits remain atomic: cancel
commits nothing.

## 27. Extensibility boundaries

### 27.1 Data-driven catalogs

The Creator should grow through engine-owned closed catalogs:

- effect operations;
- conditions;
- target modes;
- world actions;
- movement modes;
- render relationships;
- validation metadata.

Catalog-driven editors reduce duplicated UI without exposing arbitrary executable code.

### 27.2 Plugins and scripting

**Deferred**

A general plugin API, user-authored programming language, arbitrary scripts, and executable project
extensions are outside the pre-1.0 plan. They carry security, compatibility, export, debugging, and
support costs that would undermine the reliable data-driven product.

Structured conditions, actions, prefabs, templates, and catalogs should solve the intended game
without requiring code.

### 27.3 Import/export interoperability

**Candidate**

Safe interoperability may include:

- PNG and supported audio import;
- CSV for carefully scoped tabular scalar data;
- documented deterministic project JSON;
- image export of map overviews;
- diagnostic/report export.

Arbitrary third-party engine project import is deferred unless a specific, legally safe format and
mapping are separately scoped.

## 28. Complete world-building workflows

### 28.1 Build a forest route

The intended workflow:

1. Import a transparent terrain sheet as a tileset.
2. Confirm the visible slicing grid and accepted paintable cells.
3. Tag terrain tiles and define a grass/path autotile rule.
4. Select tree cells and create an object prefab with footprint, anchor, and collision.
5. Create a weighted foliage brush from decorative variants.
6. Open a map and pan into empty space.
7. Paint ground on a named visual layer.
8. Paint paths with automatic connections.
9. Scatter foliage under explicit constraints.
10. Place tree prefabs with collision preview.
11. Paint an encounter region.
12. Inspect reachability and collision.
13. Playtest from the current cursor.

No step requires selecting every tree cell individually or editing JSON.

### 28.2 Build a town

1. Create palette collections for roads, buildings, vegetation, and props.
2. Create multi-layer building stamps or object prefabs.
3. Paint base ground and road layers.
4. Place buildings with door anchors and collision footprints.
5. Create or link door warps through the destination picker.
6. Place NPCs and edit their movement/dialogue through the inspector.
7. Add signs, pickups, and structured interactions.
8. Use the world graph to inspect interiors and links.
9. Validate reachability and warp arrivals.
10. Playtest from each entrance.

### 28.3 Build a bridge

The editor should distinguish:

- decorative bridge with ordinary ground connectivity: stamp or object prefab;
- bridge with different visual layers but one walkable route: multi-layer stamp plus collision;
- true overpass with overlapping traversable planes: requires the separately specified elevation/
  connectivity model and cannot be faked by visual layer order.

### 28.4 Rework an existing area

1. Select all uses of a terrain family or prefab.
2. Preview affected maps and locations.
3. Replace assets or repaint a selected region.
4. Move selected content between layers.
5. Reroll weighted decoration without changing occupied cells.
6. Review validation changes.
7. Undo the whole operation in understandable units if needed.

## 29. Delivery horizons

These horizons express product dependency and value, not permission to bypass the active roadmap.

### Horizon A - Trustworthy baseline

- visual QA of every existing Creator view;
- explicit map and sheet grids;
- no stale-dimension reimport state;
- save/reopen equality;
- sub-100% map zoom;
- complete entity configuration forms;
- complete structured editor coverage;
- publish the engine-owned capability registry and Creator Ready evidence model;
- replace writable effect/hook/form JSON with structured preserved cards and controls;
- validation navigation and no-JSON trial;
- measured current large-map behavior.

### Horizon B - Efficient world building

- searchable effect/hook/action chooser with context filtering;
- guided and advanced move/item/ability/creature studios;
- ordered multi-effect composer, recipes, summaries, and normalized preview;
- structured trainer team editor with fixed AI presets;
- trainer trigger, rotation, patrol, sight, approach, and post-battle behavior editor;
- selection, copy/cut/paste, and move;
- reusable single-layer stamps;
- object-prefab creation from sheet or map selection;
- palette search, tags, favorites, recent assets, and collections;
- line/outline/replace tools;
- named bookmarks and minimap;
- stronger collision/reachability overlays;
- visual warp destination workflow.

### Horizon C - Robust composition

- arbitrary stable visual layers and groups;
- multi-layer stamps;
- weighted variation brushes;
- terrain/autotile rule sets;
- reusable brush presets;
- world graph and map thumbnails;
- bulk replacement and usage-by-coordinate;
- entity and composite prefab workflows.
- Campaign Story graph, quests, objectives, milestones, and in-game tracking;
- Scenario/Event Graph with tile/region/interaction/NPC/object/trainer/puzzle/battle triggers;
- typed actor movement/dialogue/battle/story nodes and scenario templates;
- campaign/scenario validation and Runtime trace.

### Horizon D - Large-world architecture

- sparse chunked map schema;
- effectively infinite canvas;
- coordinate hard caps and performance budgets;
- dense-map migration;
- Runtime chunk loading/culling;
- trim/rebase/frame utilities;
- isolated-content and map-size diagnostics.

### Horizon E - Advanced production workflow

- table/bulk data editing;
- external-change conflict handling;
- named project checkpoints;
- playtest configurations and return diagnostics;
- environment/audio zones where supported;
- neighbor-map ghost previews;
- project health dashboard;
- progression/softlock analysis from New Game to ending;
- player-journey campaign replays;
- full accessibility and stress-verification gate.

## 30. Definition of robust Creator readiness

The Creator is not robust merely because every entity has a form. Readiness requires all of the
following:

- Every selectable mechanic is Creator Ready for its exact context and current engine/catalog
  version.
- Moves, items, and abilities support understandable ordered multi-effect composition without raw
  JSON.
- Creatures, evolutions, forms, trainers, encounters, objects, and triggers expose only functional
  engine-backed choices.
- A complete Campaign Story from New Game to ending can be authored, tracked, validated, and
  playtested without code.
- Scenarios compose spatial triggers, actor movement, dialogue, battle requests, puzzles, and story
  state through Creator Ready graph nodes.
- Trainer placements combine fixed AI presets with custom teams, rotation, patrol, sight, approach,
  dialogue, battle, campaign conditions, and post-defeat behavior.
- No user-facing AI weight, threshold, noise, prediction, or custom-profile editor exists.
- Every parameter communicates its unit, range, target, timing, and failure behavior.
- A user can read a plain-language summary and inspect a normalized technical preview.
- An author can build and connect at least two substantial maps without raw JSON.
- The palette clearly distinguishes tiles, stamps, objects, and entities.
- Large structures can be created and placed without cell-by-cell repetition.
- Visual layers are independently stored, named, ordered, visible, and lockable.
- Semantic layers remain typed and cannot be accidentally flattened into artwork.
- Map size is constrained by explicit tested budgets rather than a small arbitrary canvas.
- Selection, reuse, bulk operations, and project-wide search make rework practical.
- Every destructive operation is previewable and undoable.
- Validation navigates to fields and coordinates and uses Core/Runtime truth.
- Save, recovery, reimport, migration, and external-change behavior protect authored work.
- Playtest launches the Runtime at the relevant context.
- Performance is measured against large representative projects.
- Keyboard access, focus, overlay readability, and scalable UI pass an accessibility audit.
- Exported behavior matches validated Creator data.

## 31. Decisions to refine with the user

The following decisions are intentionally open for the next review:

1. Should stamps be first-class project entities with stable IDs, or Creator-private palette
   resources compiled into ordinary placements?
2. Should multi-layer stamps be live-linked definitions or copy their content into maps on place?
3. Should object prefab instances inherit all later definition changes, and which fields may be
   overridden per instance?
4. Should sparse maps support negative coordinates directly, or should automatic origin rebasing
   keep serialized coordinates non-negative?
5. What coordinate and occupied-chunk caps provide safe practical infinity?
6. Are visual layer render relationships limited to below/actor-depth/above/foreground?
7. Do authors need optional gameplay/export bounds inside an effectively infinite authoring canvas?
8. Should named regions be a universal primitive or remain separate encounter/audio/environment
   overlays?
9. Which autotile model is easiest for authors while covering the target terrain art?
10. Should weighted brush results be rerollable per selection with stored gesture metadata, or only
    committed as final atomic tiles?
11. Which composite prefabs are valuable before a general prefab parameter system becomes
    over-complex?
12. Which table editors should support CSV interchange, and which nested data must remain
    Creator-only?
13. What information should Runtime return after a development playtest?
14. Which Horizon B/C features are required for the first serious hand-authored demo area?
15. Should unavailable mechanics be completely hidden, or visible in a disabled educational view
    with exact requirement strings?
16. Which initial recipes should appear in Guided mode for moves, items, and abilities?
17. May users save project-local recipes immediately, or should that wait until the built-in recipe
    vocabulary is proven?
18. Which compatible transformations may the Creator offer when a foundational field invalidates
    existing effects?
19. Should a move's ordinary damage appear as an explicit effect card, or as a Core Behavior card
    followed by its additional ordered effects?
20. How much timing/RNG detail belongs in the ordinary summary versus the expandable technical
    preview?
21. Which creature archetype starting points are useful without implying a balance guarantee?
22. Which Runtime sandbox scenario controls are essential for the first mechanics-editor release?
23. Should campaign quests support failure/cancellation in the first version, or only unavailable,
    available, active, and completed?
24. Should trigger regions be reusable named regions or a dedicated scenario-trigger overlay?
25. Should a reusable scenario be allowed to call another scenario in the first release, or should
    composition remain flat until recursion/trace usability is proven?
26. Which initial puzzle component families are required for the first complete authored campaign?
27. Should patrol state reset on map load by default, or persist exact position/waypoint/facing in
    saves for selected actors?
28. When a post-battle scenario retains a trainer at a new position, is that position persistent or
    reconstructed from campaign state on map load?
29. Should interaction and sight triggers both remain active by default for trainers, or should the
    Creator require explicit opt-in to each?
30. Which fixed friendly AI labels should be shown for the current Random/Basic/Smart presets?

## 32. Explicit non-goals

The Creator is not:

- a general-purpose game engine;
- a 3D editor;
- an arbitrary code IDE;
- a shader graph;
- a general event-scripting language;
- a Git client;
- a marketplace;
- a cloud collaboration platform;
- a vehicle for official Pokemon content;
- a substitute for Runtime verification.

The goal is narrower and stronger: the best focused desktop environment this project can provide
for authoring original, data-driven, grid-based creature RPGs with a custom deterministic Runtime.
