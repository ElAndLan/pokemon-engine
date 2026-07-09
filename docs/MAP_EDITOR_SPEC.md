# MAP_EDITOR_SPEC

Status: **Partial / implemented sections are binding.** Layer model and headless map tool ops
are written and tested. The Avalonia map canvas, entity placement UI, validation overlays, and
playtest-from-map workflow are **not** implemented here.

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

## Outline (later this phase)
Canvas · Collision derivation (tile flags + `collisionOverrides`) · Encounter zones · Entities ·
Validation overlays · Playtest.
