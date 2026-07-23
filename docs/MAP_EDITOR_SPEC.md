# MAP_EDITOR_SPEC

Status: **17C implemented (2026-07-23).** Tileset editor, map document + canvas, entity placement,
resize, and play-from-map argument assembly are written and tested against the 17C contract
(§ below). Remaining polish: full per-instance entity config forms (deeper fields are 17D
structured-data territory) and sub-100% canvas zoom. The play-from-map process launch is 17F.

## Purpose
The map editor: tile painting tools, layer semantics, collision/encounter overlays, entity
placement and per-instance params, validation overlays, and playtest-from-map.

## Must lock
- Layer model (ground/deco-below/deco-above + non-visual collision, encounter-zone, trigger layers).
- Tool behaviors (brush/rect/bucket/eyedropper/eraser), stroke-grouped undo, chunked canvas.
- Entity placement params (player start, NPC movement types, warp targets, pickups, signs, triggers)
  and the validation rules they introduce (warp targets, reachable start, dangling refs).

## Layer model (locked)
- Visual layers (`ground`, `decoBelow`, `decoAbove`) are row-major `int[width*height]` of **tile
  indices**; `-1` = empty. A tile index is **global across the map's `tilesets[]`**, concatenated
  in reference order (tileset 0's tiles first, then tileset 1's, …).
- Editing is pure and immutable-friendly (`MapLayerOps`): each op returns a **new** layer array,
  so the editor commits it as one undoable snapshot (stroke = one undo step).

## Tools (v0, headless-tested — the canvas that drives them is Phase 5 UI)
- **Paint**: set one cell; out-of-bounds is a no-op.
- **Rect fill**: fill an inclusive rectangle (any corner order), clamped to bounds.
- **Bucket fill**: 4-connected flood fill of the contiguous same-index region; same-index target
  is a no-op; diagonals are not filled.
- **Resize**: new `int[]`, `-1`-padded, preserving the top-left overlap.
  <!-- ponytail: top-left anchor only; add an anchor param when the resize UI needs it. -->

## Phase 17C specification completion contract

`IMPLEMENTATION_PLAN.md` v4 §7.2 package 17C authorizes the remaining canvas, chunk, layer/overlay,
tileset/object, stroke undo, entity/path/warp, validation, resize, and play-from-map behavior. Expand
this spec with those exact defaults and acceptance rows before code. No additional user confirmation
is required unless v4 §2.1 reserves the decision.

## 17C — World authoring (locked 2026-07-23)

### Tileset editor (`TilesetDocument`)

A tileset is an ordered `Tile[]` (DATA_SCHEMA §4.9); its list position **is** the tile's local
index, and a map's global index space concatenates its tilesets in reference order (`TilePalette`).
Reordering or deleting a tile therefore renumbers every map that references it — so:

- **Add tile** appends (never inserts) — appending cannot renumber existing indices.
- **Delete tile** is offered only with a warning naming the maps that use the tileset; it is not a
  safe-delete replacement flow (tiles have no EntityId), just an explicit confirm. It removes the
  last tiles or leaves a gap? — **delete is append-only's inverse: only the trailing tile may be
  removed** without renumbering; deleting an interior tile is refused with the reason. (A "clear"
  action zeroes a tile's flags/sprite in place instead.)
- Per-tile edits (each an undoable whole-record `Tileset` snapshot): `sprite` (reference picker
  over `sprite:*`), `solid`, `grass`, `water`, `counter` bools, `ledge` (none/up/down/left/right),
  `terrainTag` string. `anim` is deferred (animation-driven tiles are a later layer) and shown
  read-only if present.
- The editor shows each tile's sprite thumbnail + its flags; this is the surface where tileset
  authoring bugs (wrong flag, wrong sprite, misordered tiles) are found and fixed.

### Map document (`MapDocument`)

Holds one `Map`; every edit is an undoable whole-record snapshot through the document stack. Visual
layers (`ground`, `decoBelow`, `decoAbove`) are painted through the pure `MapLayerOps`; collision
and encounter overlays edit the sparse `collisionOverrides`/`encounterZones` lists.

- **Active state** (not serialized): current layer, current tool, selected palette tile (global
  index), selected collision value, selected encounter table.
- **Tools** over the active visual layer: paint (one cell), rect fill, bucket fill, eyedropper
  (reads a cell into the selected tile), erase (paint `-1`). Out-of-bounds is a no-op.
- **Stroke = one undo step.** A pointer press-drag-release is grouped: the document exposes
  `BeginStroke()`/`StrokePaint(x,y)`/`EndStroke()`, buffering the working layer and committing one
  snapshot on end (repeated cells store the before once and the after once, via undo grouping).
- **Overlays**: collision-override paint sets/clears one cell's `CollisionValue`; encounter paint
  sets/clears a cell's `encounter:*` table. Both edit sparse lists keyed by cell index.
- **Resize** uses `MapLayerOps.Resize` on all three layers (top-left anchor) and drops overrides/
  zones/entities that fall outside the new bounds, reported before commit.

### Map canvas (view over the document)

- 32×32-tile visual chunks; only chunks overlapping the viewport draw. Zoom 25–800% stepped
  (integer scale ≥100%, fractional below), pan via scroll. Layer visibility + lock toggles; a
  locked layer is not editable. Tile palette lists the map's tilesets' tiles by global index with
  sprite thumbnails. One pointer down-drag-up = one stroke = one undo command.
- Collision, encounter, and trigger overlays are toggleable translucent layers over the tiles.

### Entity placement

Select/move/configure the placement kinds (DATA_SCHEMA §4.11a): player-start, npc
(static/wander+radius/patrol+path), warp (map + target-tile picker), pickup (item + qty + flag),
sign (text), trigger (condition + actions), object (`object:*`). Placement assigns a stable `key`
(`{kind}_{n}`, unique in the map, never reused). Move updates `pos`; delete removes by key. Paths
are 4-connected tiles validated against collision. All edits undoable.

### Validation overlays

The shared validation strip already runs Core's map rules (warp target exists, reachable start,
broken refs, entity keys). The canvas surfaces the entity-addressed ones as markers on the offending
cell; clicking still navigates via the strip. No new Core rules — 17C is authoring UI over existing
validation.

### Play-from-map

Shift+F5 assembles the Runtime argument line `--project <folder> --map <mapId> --at <x>,<y>`
against the current map and the cursor tile (validated in-bounds and non-solid), after the same
save-gate as F5. The actual process launch is **17F** — 17C only builds and unit-tests the argument
string.
