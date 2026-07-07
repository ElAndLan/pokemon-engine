# ASSET_PIPELINE_SPEC

Status: **Stub** — current source is `MASTER_PLAN.md` §9 and `ARCHITECTURE_ADDENDUM.md` §9
(import layers v0–v5). Full write due **before Phase 4** (v0–v2), extended at Phases 12/17.
Blocks: Phases 4, 12, 17.

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

## Outline (later layers)
v3 (connected-component) · v4 (animation) · v5 (atlas/pack).
