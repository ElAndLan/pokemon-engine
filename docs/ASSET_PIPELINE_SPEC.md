# ASSET_PIPELINE_SPEC

Status: **Implemented algorithm baseline plus Phase 17/18 package contracts authorized.** v0 manual grid, v1 common-size
suggestions, v2 transparent-gutter detection, character animation helper, and v5 atlas packing
are written and have matching code/tests. The slicer canvas/import browser UI, audio import UI,
and v3 connected-component slicing are **not** implemented here.

## Purpose
How art becomes runtime assets: PNG import, the slicing layers (manual grid → common-size →
gutter detection → connected-component), animation grouping, metadata, atlas packing, pack format.

## Must lock
- Slice-detection algorithms per layer (divisibility ranking, alpha-projection gutter fit,
  flood-fill component merge) with confidence + always-available manual override.
- Slice metadata format in `derived/`; sprites as projections (source PNG never modified).
- Atlas packing (skyline, ≤2048²) and how rects are rewritten into the `.cgmpack`.

## Slicing layers (v0–v2 written; v3–v5 land in their phases)

Slicing is **pure computation** (dimensions / alpha grid → cell rects) and lives in
`Cgm.Creator/Assets` — the runtime never slices. PNG decode (StbImageSharp) is a separate,
later concern that feeds width/height/alpha into these functions.

### Import v0 — manual grid (`GridSlicer`)
`Slice(imageW, imageH, GridSpec{cellW,cellH,offsetX,offsetY,spacingX,spacingY}) → Rect[]`.
Steps from `(offsetX,offsetY)`, strides `cell + spacing`, emits a rect only when it fully fits
in bounds. `cellW/cellH` must be > 0 (else throws). Fully-transparent cells are excluded by the
importer later (needs pixels), not here.

### Import v1 — common-size suggestion (`SizeSuggester`)
`Suggest(imageW, imageH, preferTileSize?) → int?`. Returns the largest of {16,32,48,64} that
divides **both** axes; if the project tile size also divides both, prefer it. No common divisor →
null (fall through). Pure arithmetic, no pixel inspection.

### Import v2 — transparent-gutter detection (`GutterDetector`)
`Detect(opaque[], width, height) → GutterFit{cellW,cellH,spacingX,spacingY,marginX,marginY}?`.
Projects opacity onto each axis (a column/row is "occupied" if any pixel is opaque), splits each
axis into runs, then requires a **uniform** pattern: leading gap = margin, equal-width occupied
runs = cell size, equal interior gaps = spacing. Non-uniform, or fewer than 2 cells on an axis
(no repeating grid) → null (fall through to v1). Trailing gap ignored.
<!-- ponytail: rejects 1×N guttered strips (needs ≥2 cells per axis); manual v0 covers them. -->

### Validation
Cells within image bounds; unique sprite ids per sheet (enforced by the sheet editor + EntityId).

### Import v5 — atlas packing (`AtlasPacker`)
Export-time packing of a category's sprite rects into one or more square-bounded atlases
(≤`maxSize`, default 2048). Pure computation (sizes → placements); pixel copy into the atlas
bitmap and the `.cgmpack` write are separate, later concerns that consume these placements.

`Pack(sizes: (int W, int H)[], maxSize = 2048) → Atlas[]` where
`Atlas{ Width, Height, Placements: AtlasPlacement[] }` and
`AtlasPlacement{ SpriteIndex, Rect }` (`Rect` in atlas-local coords). `SpriteIndex` is the
input index, so the caller maps index → sprite id and rewrites regions after packing.

**Algorithm — Skyline Bottom-Left** (simple skyline is sufficient at this scale, per Addendum §9):
- Sprites are placed largest-first: sort input indices by height desc, then width desc, then
  index asc (the final tie-break makes packing deterministic).
- Each atlas has a fixed pack width = `maxSize`; the skyline is a left-to-right list of
  `(x, y, width)` segments spanning `[0, maxSize)`. A rect is placed at the candidate x with the
  lowest resulting y (ties → lowest x) such that `x+w ≤ maxSize` and `y+h ≤ maxSize`.
- When a rect fits in no candidate of the current atlas, that atlas is closed and a fresh one is
  started (overflow split). `Atlas.Width/Height` report the tight used extent (max right / bottom),
  not `maxSize`.
- A single rect with `W > maxSize` or `H > maxSize` cannot be placed in any atlas → throws
  `ArgumentException`. Non-positive `W`/`H` also throw. Empty input → no atlases.

**Guarantees (tested):** no two placements in the same atlas overlap; every input sprite appears
in exactly one placement; all placements lie within `[0,maxSize)²` and within their atlas extent;
identical input → identical output (determinism). No padding/gutter between sprites (pixel art is
nearest-sampled under integer scaling); add a gutter param only if bleeding is ever observed.
Excludes (Addendum §9): runtime dynamic atlasing, mipmaps, compression formats beyond RGBA.

### Character animation template (`CharacterAnimation`, Phase 13 / Phase 4 deferral)
Builds walk clips from a standard character sheet: a 3-frame × 4-direction grid, row-major, rows
ordered Down, Left, Right, Up. `BuildWalkClips(baseSlug, gridSprites[12], frameMs=150)` → one looping
`Animation` per facing (`anim:<baseSlug>_walk_<dir>`). Pure; throws on a non-12 sprite count, a
non-positive frame duration, or an invalid base slug (EntityId grammar).

## Phase 17/18 specification completion contract

`IMPLEMENTATION_PLAN.md` v4 §§7.2/8.2 authorize the remaining import browser/canvas, v3 connected-
component, reimport, animation/audio metadata, atlas diagnostics, and production pack asset contracts.
Before 17B or 18C code, copy/reconcile the exact transaction, algorithm, ordering, hashing, rollback,
and acceptance decisions into this spec. No extra user confirmation is required under v4 §2.1.
