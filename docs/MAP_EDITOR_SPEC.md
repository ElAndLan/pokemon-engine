# MAP_EDITOR_SPEC

Status: **Stub** — current source is `MASTER_PLAN.md` §10. Full write due **before Phase 5**.
Blocks: Phase 5.

## Purpose
The map editor: tile painting tools, layer semantics, collision/encounter overlays, entity
placement and per-instance params, validation overlays, and playtest-from-map.

## Must lock
- Layer model (ground/deco-below/deco-above + non-visual collision, encounter-zone, trigger layers).
- Tool behaviors (brush/rect/bucket/eyedropper/eraser), stroke-grouped undo, chunked canvas.
- Entity placement params (player start, NPC movement types, warp targets, pickups, signs, triggers)
  and the validation rules they introduce (warp targets, reachable start, dangling refs).

## Outline (to be written, Phase 5)
Canvas · Tools · Layers · Collision · Encounter zones · Entities · Validation overlays · Playtest.
