# ASSET_PIPELINE_SPEC

Status: **17B implemented (2026-07-23).** v0 manual grid, v1 common-size suggestions, v2
transparent-gutter detection, v3 connected components, character animation helper, and v5 atlas
packing are written with matching code/tests, as are the import/reimport transactions, the slicer
canvas (SheetDocument/SheetView), the sound and animation editors, and the asset-file
diagnostics.

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

## 17B — Asset authoring (locked 2026-07-22)

### Import transaction

Import never modifies the user's source file, and a failed import never leaves partial project
state. Order is validate-first:

1. **Decode/validate** the picked PNG in place (StbImageSharp). A malformed file rejects here —
   nothing was copied, nothing changed.
2. **Slug + collision.** The proposed sheet slug must satisfy the EntityId grammar. If
   `sheet:<slug>` already exists, prompt: **Replace** (a reimport of that sheet, below) or **new
   slug**. If the target file name `assets/<file>.png` is taken by a *different* sheet, the copy
   is renamed `<file>_<slug>.png` — one asset file per sheet, never a silent overwrite.
3. **Copy** into `assets/` (canonical project asset folder), compute **SHA-256** of the bytes into
   `SpriteSheet.ContentHash`, record `ImageW/ImageH`.
4. **Slice + register.** Initial slicing = the v2→v1→v0 suggestion ladder (gutter fit, else
   common size, else project tile size, else whole image). The sheet entity joins the session
   dirty (it is saved by the ordinary transactional save, not written directly).

### Reimport

Reimport replaces a sheet's pixels while preserving its identity and authored work:

- The sheet's `EntityId` and every authored cell (sprite ids, classes, rects, includes) are kept.
- The new file is decoded/validated first, then copied over the sheet's existing `Asset` path;
  `ContentHash`/`ImageW/ImageH` update.
- Cells whose rect (or grid cell) no longer fits inside the new bounds are **invalidated**:
  reported to the user before commit, and the commit is confirmation-gated. Declining leaves the
  project untouched (the file copy happens only on confirm). Invalidated cells are removed on
  commit — their sprite ids disappear and the broken-reference rule surfaces every consumer.

### Import v3 — connected components (`ComponentSlicer`)

`Detect(opaque[], width, height, mergeThreshold = 2) → Rect[]` — for irregular sheets no grid
fits:

- **Flood fill** 4-neighbor over opaque pixels (alpha > 0); each component's tight bounds is a
  candidate rect. Iterative fill (explicit stack) — a 4096² image must not recurse.
- **Noise discard:** components of 1 pixel are dropped; fully transparent images yield `[]`.
- **Merge:** two bounds merge when they overlap or when the gap between them is ≤
  `mergeThreshold` px on one axis and they overlap on the other (a sprite whose outline breaks
  into pieces reads as one). Merging repeats to a fixed point.
- **Sort:** top-to-bottom by `Y`, then left-to-right by `X` — reading order, deterministic.
- Snap-to-grid is off by default (the plan's default); the canvas may snap on request.

### Slice acceptance & naming

- Accepting a suggestion (any layer) replaces the sheet's cells wholesale as **one undo step**.
- Batch naming: a pattern containing `{n}` (e.g. `coin_{n}`) names accepted cells
  `coin_0, coin_1, …` in cell order; a pattern without `{n}` gets `_{n}` appended. Names must
  satisfy the slug grammar; the sprite id is `sprite:<sheetSlug>_<name>`.
- Include/exclude: excluding a cell removes it from the sheet (undo restores it; a grid re-slice
  regenerates grid cells). No excluded-cell flag is stored — absence *is* exclusion, matching how
  fully-transparent grid cells are already dropped at import. Add a persisted flag only if
  round-tripping exclusions through re-slice ever matters.

### Canvas semantics (view layer over the headless document)

- Zoom 25–800% stepped, pan, pixel grid at ≥400%; rect edit = drag handles at 100%+ pixel
  precision. All state changes route through the document's undo stack; the canvas holds no
  authoring state of its own. One drag = one undo step.

### Orphans & diagnostics

- An `assets/` file no sheet references is an **orphan**: reported by validation as a warning
  (not deleted — deleting files is the user's call, offered in the browser).
- A sheet whose `Asset` file is missing, or whose `ContentHash` no longer matches the file, is
  an error with a fix hint naming reimport.

### Audio (decided 2026-07-23: `sound` entity category, schema v12)

Audio imports as a `sound:*` entity (DATA_SCHEMA §4.6b): kind (music|sfx), loop intent, volume
0–100, `assets/audio/` file + SHA-256 hash. Import is container-validated by `WavProbe`
(RIFF/WAVE magic + intact length) **before any copy** — deliberately not a decoder: the Runtime's
`PcmWaveDecoder` stays the one authority on playable PCM (no-second-decoder rule), so a
container-valid but unplayable file surfaces at playtest with the decoder's conversion hint.
`map.bgm` may name a `sound:*` id (the Runtime resolves it to the asset) or remain a legacy raw
path. The Creator plays no audio; audition is the Runtime.
<!-- ponytail: per-sound volume/loop are authored but the mixer applies channel volume only;
     plumb per-sound gain when the mixer grows it (18C pack work). -->

### Asset-file diagnostics (Creator-side)

Core validation never reads the machine's filesystem, so these live in the Creator shell and
append to the validation strip: missing asset file (error, reimport hint), content-hash mismatch
against the recorded SHA-256 (error — the file changed outside the Creator), and orphaned
`assets/` files nothing references (warning; deletion stays the user's call). Hashes are cached
by (mtime, length) so the debounced validation pass never re-hashes unchanged art.
