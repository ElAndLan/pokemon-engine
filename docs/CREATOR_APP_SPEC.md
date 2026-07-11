# CREATOR_APP_SPEC

Status: **Shell + editor-pattern + pathfinder editors written (Phase 3).** Map/slicer/creature/
other editors are added to this doc as their phases land. Source of the big picture: MASTER_PLAN
§5. Architecture rules: CLAUDE.md, CODING_STANDARDS.md. The Creator edits data only and launches
the Runtime for playtest (ADR-009) — it never simulates gameplay.

---

## 1. Technology & conventions
- **Avalonia 12 MVVM.** Views (`.axaml`) are dumb; `ViewModels` hold state and commands; `Services`
  do IO and cross-cutting work. No code-behind logic beyond `InitializeComponent`.
- **CommunityToolkit.Mvvm** for `ObservableObject`/`[ObservableProperty]`/`[RelayCommand]` — this is
  the one new dependency Phase 3 adds (record it in TECH_STACK.md before use). It is a UI helper,
  not a game framework; allowed.
- All project reads/writes go through `Cgm.Core` (`ProjectLoader`, `ProjectFile`, `CgmJson`,
  `Validator`). The Creator never re-implements schema or validation logic.
- One editor pattern (§4), copied for every entity type. Build it once; every later editor is a
  fill-in-the-blank of the same shape.

## 2. Shell layout
A single main window, four regions in a dock:
- **Left — navigation tree.** Project name at top; one expandable node per entity category
  (Types, Species, Moves, Items, Maps, …) listing that category's entities by display name.
  Double-click opens the entity in a document tab. Right-click → New / Duplicate / Delete.
- **Center — document tabs.** One tab per open entity (re-focus if already open). Tab shows a
  dirty marker (•). Middle-click / Ctrl+W closes (prompting if the document has unsaved edits at
  the project level is unnecessary — see §3 save model).
- **Right — inspector.** Context panel for the active document (per-editor content). Collapsible.
- **Bottom — validation strip.** Live issue count (`3 errors, 1 warning`); click to expand a list;
  click an issue to navigate to its entity (§5).
- **Top — menu/command bar.** File (New/Open/Save/Recent/Close), Edit (Undo/Redo), Project
  (Validate, Playtest [Phase 7]).

## 3. Project lifecycle & save model
- **New Project:** dialog collects name, parent folder, tile size (16/32). Creates the folder,
  writes a minimal valid `project.cgmproj` (mirrors `samples/fixture-min` shape), opens it.
- **Open:** folder picker; `ProjectLoader.Load`. Load failures show the `Cgm.Core` error message in
  a dialog, not a crash.
- **Recent projects:** last ~10 folders persisted to `%APPDATA%/CreatureGameMaker/recent.json`.
- **Save model — explicit, whole-project.** Edits mutate in-memory entity view-models; **Save**
  (Ctrl+S) writes every dirty entity to its `data/<cat>/<slug>.json` via `CgmJson` (byte-stable).
  A per-document dirty flag drives the • marker and the "unsaved changes" guard on app close.
  No autosave in Phase 3 (Phase 17).
- **Entity ops:** New (prompts for slug, validated against EntityId grammar + uniqueness), Duplicate
  (new slug, deep copy), Delete (lists referencing entities as a warning first), Rename (edits
  display `name` only — the `id` is immutable, per DATA_SCHEMA §2).

## 4. The editor pattern (build once, reuse everywhere)
Every entity editor is the same four pieces:
1. **`<Entity>DocumentViewModel`** — wraps one entity as editable observable state. Exposes
   `EntityId Id`, `bool IsDirty`, the editable fields, and `Entity ToModel()` / `FromModel(entity)`
   round-trip. Editing a field pushes an **undo command** (§4.1) and marks dirty.
2. **`<Entity>View.axaml`** — bound controls only.
3. **Validation registration** — the document re-runs the relevant rules on change (§5).
4. **Nav integration** — the category node lists these documents; open/create/delete route here.

### 4.1 Undo/redo
- Per-document `UndoStack` of `IEditCommand { void Do(); void Undo(); }`.
- **Small entities (type chart, item, move, species, …): whole-entity snapshot commands.** Each
  edit captures the entity record before/after; Undo swaps the record back into the view-model.
  Records are immutable (Phase 2), so a snapshot is just holding two references — cheap and
  bulletproof. (Map/tile editing uses per-cell commands — MAP_EDITOR_SPEC, Phase 5.)
- Ctrl+Z / Ctrl+Y. Stack depth ≥ 100. Dirty state = "stack position ≠ last-saved position".
- View-models are tested headless (no UI) for undo/redo correctness and dirty tracking
  (CODING_STANDARDS test policy).

## 5. Validation strip
- On any edit (debounced ~400 ms), the shell runs `Validator.Run(currentProjectSnapshot)` and
  updates the strip. Phase 3 runs the full default rule set (fast enough at this scale); an
  incremental/affected-only pass is a Phase 17 optimization, not now (YAGNI).
- Strip shows counts by severity; expanding lists `ValidationIssue.ToString()` lines.
- Clicking an issue with an `EntityId` opens/focuses that entity's document tab.
- The strip is informational in the editor; the hard zero-error gate is enforced at **export**
  (Phase 17/18), not on every edit.

## 6. Pathfinder editors (Phase 3 deliverables)
The three simplest editors, chosen to prove the pattern before the hard canvases (slicer/map).

### 6.1 Type chart editor (`type:*`)
- **Edits:** the set of `TypeDef` entities and their `doubleDamageTo`/`halfDamageTo`/`noDamageTo`
  lists (DATA_SCHEMA §4.2).
- **UI:** an N×N grid, attacker rows × defender columns, each cell cycling 0 → ½ → 1 → 2 on click
  (writes into the attacker type's three lists). Add/remove type (row+column) with name + slug.
- **Validation:** `broken-reference` (type refs), plus duplicate-type guard.
- **MVP:** the grid + add/remove. **Later:** color themes, bulk presets.

### 6.2 Item editor (`item:*`)
- **Edits:** `Item` fields (DATA_SCHEMA §4.5) — name, description, pocket (dropdown from
  `project.pockets`), price, key-item + usability flags (field/battle/holdable/consumable), and
  the effect list (using only the ops that exist: heal amount, capture ballBonus).
- **UI:** a form; effects as an add/remove list of `{op, chance?, params}` rows.
- **Validation:** effect param ranges; pocket ∈ project pockets.
- **MVP:** heal/ball/key items. **Later:** held-item + TM authoring (Phase 17; Core mechanics Phase 15).

### 6.3 Move editor — basic (`move:*`)
- **Edits:** `Move` fields (DATA_SCHEMA §4.4) — name, type (reference picker), damage class,
  power/accuracy/pp/priority/critStage numerics (range-enforced), target, and the effect list
  (damage op now; full palette grows with the battle layers).
- **UI:** a form + the same effect-list control as the item editor (shared).
- **Validation:** the `move` rule (status⇔null power, ranges).
- **MVP:** damage + ailment%. **Later:** the full effect-op palette (Phase 14).

## 7. Shared controls
- **Reference picker:** a searchable dropdown listing entities of a given category by display name,
  binding an `EntityId`. Reused by every editor (move.type, item icon, species learnset, …). Built
  once in Phase 3.
- **Effect-list editor:** add/remove/reorder rows of `{op, chance?, params}`; shared by item + move.

## 8. Phase 3 done criteria (mirrors IMPLEMENTATION_PLAN)
Create a project in the UI → add three types and fill the chart → create an item and a move →
break a reference and see it in the strip → click the issue to navigate → undo → save → reopen →
state identical. Item editor is demonstrably a copy of the type-chart editor's pattern (proves
reuse). ViewModel-level tests cover undo/redo, dirty tracking, and the reference picker.
