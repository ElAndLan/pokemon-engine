# CREATOR_APP_SPEC

Status: **Stub** — current source is `MASTER_PLAN.md` §5. Written incrementally: shell + editor
pattern in **Phase 3**, then one section per editor as its phase lands. Blocks: Creator UI phases.

## Purpose
Per-screen specs for the Creator: purpose, data edited, controls, validation, and the reusable
patterns (undo command stack, validation strip, reference picker, editor template).

## Must lock
- The MVVM editor template every editor copies; undo semantics (snapshot commands for small
  entities, per-cell for maps); dirty tracking.
- Validation strip behavior (debounced, click-to-navigate) and how editors register validators.
- Project lifecycle (new/open/save/recent), entity create/duplicate/delete-with-usage-warning.

## Outline (to be written, per phase)
Shell · Editor template · Dashboard · Asset browser · Slicer · Tileset · Map · each data editor.
