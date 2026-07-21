---
name: cgm-tileset-production
description: End-to-end production process for original, game-ready terrain tilesets, autotile families, environment object sheets, and biome kits in the project's late-2000s handheld monster-RPG pixel-art aesthetic. Use when planning a biome, defining required tile coverage, generating tileset source sheets, normalising them for import, or auditing a tileset for grid alignment, seam integrity, transition completeness, and collision readability.
---

# Tileset Production Process

## Purpose

This skill owns the **process** by which a terrain tileset goes from a brief to
an importable source sheet: planning, coverage definition, generation,
normalisation, and audit.

It is a production pipeline, not a second art-direction voice. Art direction for
outdoor environments belongs to `cgm-environment-tileset-artist`; buildings to
`cgm-building-architecture-artist`; interiors to
`cgm-interior-environment-artist`; animation timing to `cgm-animation-artist`;
final approval to `cgm-asset-review-auditor`. Route to them rather than
restating their judgements here.

---

# Required Documents and Authority

Read before planning or generating:

* `docs/ART_BIBLE.md` — qualitative visual direction.
* `docs/art/VISUAL_MEMORY.md` — approved technical values, asset profiles,
  palette ceilings, the tileset authoring convention, and naming conventions.
* `docs/ASSET_PIPELINE_SPEC.md` — slicing, atlas packing, import layers.
* `docs/MAP_EDITOR_SPEC.md` — how the Creator consumes a tileset.
* `docs/DATA_SCHEMA.md` — `tileSize` and tileset entity shape.

This skill does not override approved technical values. Resolve conflicts in
`VISUAL_MEMORY.md`'s stated order:

1. an explicitly approved asset brief
2. an approved asset-family or exception entry
3. a named asset profile
4. `ART_BIBLE.md` qualitative direction
5. ask the user when the missing value affects layout, compatibility, or canon

Numbers quoted here are the current `internal-demo-v1` profile. If that profile
changes, `VISUAL_MEMORY.md` wins and this skill is stale.

---

# Technical Profile

| Property | Value | Source |
| --- | --- | --- |
| Project tile grid | 32 x 32 px | `VISUAL_MEMORY.md` (`internal-demo-v1`) |
| Schema-permitted tile sizes | 16 or 32 | `DATA_SCHEMA.md` |
| Camera | three-quarter top-down | `ART_BIBLE.md` |
| Lighting | upper-left, form shadow lower-right | `VISUAL_MEMORY.md` |
| Scaling | integer, nearest-neighbor, no smoothing | `VISUAL_MEMORY.md` |
| Environment palette ceiling | 24 colours per family | `VISUAL_MEMORY.md` |
| Pixel placement | whole-pixel only | this skill |
| Deliverable format | genuine RGBA alpha | `VISUAL_MEMORY.md` |

Large objects — trees, cliffs, buildings, bridges — may span several tiles, but
their footprint must be a whole number of tiles aligned to the grid.

---

# The Deliverable Is a Source Palette, Not a Map

Per `VISUAL_MEMORY.md`, environment tileset generations are **reusable source
palettes for painting maps in the Creator.** They are not composed maps, scenic
previews, adjacency diagrams, or substitutes for the user's own map design.

This is the most common way a tileset request fails. A beautiful painted town
scene is worthless as a tileset; a grid of isolated, reusable cells is the
deliverable. When a map composition is genuinely useful, label it a **separate
preview**, never the tileset itself.

Requirements carried directly from canon:

* Isolated, non-overlapping asset families with generous negative space.
* Broad painting variety — ground swatches, paths and transitions, trees and
  clusters, tall grass, shrubs, flowers, rocks, logs, stumps, leaf clusters,
  water and shore pieces when requested, regional props.
* Every visible cell must differ meaningfully in silhouette, topology,
  arrangement, or interior detail. Do not pad a sheet with near-identical
  duplicates.
* Polished cohesive pixel art. A mechanically complete but visually crude grid
  is not an acceptable substitute for the requested source art.
* Treat user-supplied examples as structural references only. Never copy their
  specific artwork.

---

# Transparency: Key Is Intermediate, Alpha Is Final

`VISUAL_MEMORY.md` is explicit: **"Present only the actual alpha result as the
deliverable. A chroma-key intermediate is processing evidence, never the final
sheet."**

The pipeline is therefore two-stage:

1. **Generation** — the image model produces the sheet on a flat `#FF00FF`
   background, because models cannot reliably emit true alpha.
2. **Normalisation** — the key is removed and the sheet is delivered as genuine
   RGBA with transparent negative space.

A magenta-backed PNG is never the deliverable. It is an intermediate.

While generating, the key rules still apply: the background must be completely
flat `#FF00FF`, no foreground pixel may use exact `#FF00FF` or a near-magenta
that could be mistaken for it, and no gradient, shadow, border, or texture may
appear in transparent space. When magenta or pink is genuinely needed in the art,
choose a value well separated from the key.

Fully filled ground tiles need no transparent space, but still ship as RGBA for
format consistency.

---

# Generation Reality

An image model **cannot be relied upon** to produce exact tile alignment, exact
canvas dimensions, exact RGB values, or truly seamless repetition. Every
instruction in this skill about grids, dimensions, and exact colour states the
*target*, not the guaranteed result.

Observed failures on real project output include backgrounds drifting off the
key colour and anti-aliased edge blending, both while explicit instructions
forbade them.

Therefore:

* A generated sheet is **raw input**, never a finished asset.
* Seam integrity, grid alignment, and colour compliance are verified **on the
  file**, never asserted from the prompt.
* Never record a tileset as import-ready without inspecting it.

---

# Two Modes

## 1. Planning Mode

Use when asked to plan a biome, tileset, town, cave, route, dungeon, or interior
kit; to define required tile coverage; or to establish environment direction
before generation.

Planning output is written. It may contain labels, inventories, coverage
matrices, and palette notes — **but the plan covers only what generation and
import need.** Do not produce lore, regional history, climate essays, ecology
writeups, or narrative justification. None of it reaches the image generator.

## 2. Generation Mode

Use when asked for the actual asset sheet. The output contains only the
requested assets arranged for extraction.

Never carry planning elements into an asset sheet. Unless explicitly requested,
exclude explanatory text, tile names, arrows, annotations, UI framing, palette
charts, sample maps, decorative borders, legends, logos, and watermarks.

---

# Asset Taxonomy

Identify the category before planning.

**Ground tile set** — filled walkable or blocking surfaces: grass, dirt, sand,
snow, mud, stone, cave floor, wooden floor, shallow water.

**Transition / autotile set** — where materials meet: centers, N/S/E/W edges,
outer corners, inner corners, narrow strips, isolated patches, T-junctions,
crossroads, endcaps.

**Elevation set** — cliffs, ledges, terraces, slopes, stairs, cave walls,
shoreline drops, bridges, waterfalls.

**Environmental object sheet** — trees, bushes, flowers, rocks, stumps, logs,
reeds, signs, fences, lamps, statues, debris.

**Animation sheet** — looping environment motion: water, waterfalls, torches,
machinery, smoke, blinking signs, moving foliage.

**Biome kit** — a coordinated package across several categories, planned as a
system rather than a collection.

Buildings and interiors are their own specialists — route those out.

---

# Terrain Production Blueprint

Before generating a biome kit, large tileset, or town/cave kit, produce a
blueprint. For a single small asset, keep it brief and internal.

1. **Environment thesis** — one sentence on visual and functional identity.
2. **Grid and camera** — tile size, perspective, player-relative scale.
3. **Material families** — natural and constructed materials in use.
4. **Palette structure** — dominant, supporting, accent, shadow, transition,
   within the 24-colour family ceiling.
5. **Ground system** — main materials and how many repeating variants each needs.
6. **Transition system** — which material pairs must connect, and the full
   adjacency cohort each pair requires.
7. **Elevation system** — cliff height, wall depth, ledge behavior, stairs.
8. **Object system** — vegetation, rocks, signs, fences, landmarks, interactables.
9. **Collision readability** — walkable, blocked, hazardous, ledge, encounter,
   and interactable surfaces, and how each reads visually.
10. **Animation requirements** — which assets loop, and frame counts.
11. **Export sheets** — the separate sheets to generate, with cell counts.
12. **Sheet geometry** — for each sheet: `cellW`, `cellH`, rows, columns,
    spacing, margin. Stated explicitly, never inferred.

The blueprint is not the asset sheet. Generate only after approval.

---

# Sheet Layout and Grid Discipline

## Geometry (state explicitly, never infer)

The engine's `GridSlicer` consumes a sheet as
`GridSpec{cellW, cellH, offsetX, offsetY, spacingX, spacingY}`. The layout that
slices without hand-measurement is a flush grid with zero offset and zero
spacing.

| Property | Value |
| --- | --- |
| `cellW` / `cellH` | the profile tile size (32 at `internal-demo-v1`) |
| Offset / spacing / margin | zero — cells flush |
| Sheet width | `columns * cellW` |
| Sheet height | `rows * cellH` |

Both sheet axes must be exact multiples of the tile size. `Suggest()` ranks
divisibility by {16, 32, 48, 64} across both axes and returns nothing when no
candidate divides both — which forces hand-entered slice geometry and is the
most common import failure.

Multi-tile objects occupy a whole number of adjacent cells and declare their
footprint in the blueprint.

## Construction rules

* Exact tile boundaries, whole-pixel alignment, integer dimensions.
* Predictable anchor points and consistent vertical offsets.
* Consistent collision footprints across a family.
* No half-pixel placement, no one-pixel drift between related tiles.
* No decorative pixels crossing an extraction boundary.

---

# Seamless Repetition

Repeating tiles must connect on every intended edge.

* Distribute texture clusters across the tile rather than concentrating detail
  centrally.
* Avoid obvious repeating symbols, strong diagonals, and isolated high-contrast
  pixels near borders unless they continue correctly.
* Provide several compatible variants rather than one obviously repeated tile.
* Vary interior clusters while preserving boundary pixels — that is what keeps
  variants seam-compatible.

**Test in blocks larger than 3 x 3 before approval.** Reject tiles that reveal
seams, grids, stripes, or repeated stamps unless the material is intentionally
regular.

---

# Transitions

When two materials meet, plan the whole relationship rather than one sample edge.
A usable transition set covers straight edges in every direction, outer corners,
inner corners, narrow paths, isolated patches, one-tile gaps, T-junctions,
elevation interaction, and overlay compatibility.

The transition must communicate which material sits above, below, inside,
outside, or on top of the other. Ambiguous edges that read two contradictory ways
are a defect.

---

# Elevation and Collision Readability

Elevation must be readable without debug overlays, through consistent cliff-face
depth, shadow bands, top-edge highlights, ledge lips, stair direction, wall caps,
shoreline drop-offs, and material breaks.

Walkable and non-walkable areas must be visually distinguishable. A player should
infer likely collision from the art alone. Never rely on invisible collision to
correct visually misleading terrain.

---

# Material Language

**Grass and foliage** — clustered blade and leaf suggestions, broad masses,
restrained highlights, readable canopy shapes. Never render every blade.

**Dirt and sand** — sparse pebbles, compact clusters, gentle value variation,
directional marks only for paths or wind. Avoid noisy speckling.

**Stone and cliffs** — simplified cracks, broad planes, compact shadow clusters,
controlled edge highlights, readable slab grouping. Do not make every environment
mineral-heavy.

**Water** — repeating wave clusters, controlled highlights, readable shore edges,
compact animation cycles. No smooth gradients or realistic reflection.

**Wood** — simplified grain, clear plank direction, restrained knots, readable
trim.

**Metal and artificial** — stronger highlight contrast, clean panel divisions,
sparse rivets, clear functional construction. Do not over-detail at tile scale.

---

# Palette and Lighting

Use compact families: one major ground ramp, one vegetation ramp, one structure
ramp, one shadow family, one or two accents, limited transition colours. Prefer
two-to-four-step ramps per material. Use hue shifts to separate materials and
build depth.

**Ceiling: 24 colours per environment family** (`VISUAL_MEMORY.md`), counted from
the actual file. Treat it as a family budget, not a per-tile licence. An image
model cannot certify the count — verify on the file.

Light comes from the **upper left**, form shadow to the lower right, held
consistent across ground, cliffs, vegetation, objects, and animated assets.
Shadows clarify volume and overlap; they do not simulate global illumination.

Do not use smooth gradients, near-duplicate colours, photographic variation,
uncontrolled saturation, airbrushing, bloom, ambient-occlusion gradients,
reflections, or painterly blending.

---

# Environmental Objects

Objects need a clear footprint, the shared camera angle and light direction,
communicated collision, readability at native size, and open space around
interaction points.

Large objects may extend above their collision footprint, but the visible base
must show where the object meets the ground.

Do not reuse one universal tree, rock, bush, or fence silhouette across every
biome.

---

# Animation

Animated assets must loop cleanly using the fewest frames that communicate
motion — 2 for subtle flicker, 3–4 for water, torches, machinery, or foliage,
more only when genuinely required.

Every frame keeps identical dimensions, stable anchors, a constant collision
footprint, no drift, and palette consistency. Animation is selective — do not
animate every object. Route timing questions to `cgm-animation-artist`.

---

# Naming

Follow `VISUAL_MEMORY.md` naming conventions — stable lowercase snake-case slugs
inside namespaced IDs, display names kept separate:

* `sheet:<family>_<purpose>` for source sheets
* `tileset:<region>_<purpose>` for tile families
* `sprite:<subject>_<view>_<state>_<index>` for cells
* `anim:<subject>_<action>_<direction>` for clips

---

# Originality

Every terrain asset must be independently designed. Do not copy a recognisable
map, trace an existing tileset, reproduce a copyrighted building, recreate
signature routes, towns, or landmarks, use franchise symbols, recolor an existing
tileset, or describe output as official franchise artwork.

Genre conventions may be recombined originally. The environment must own its
layout logic, palette identity, material language, architecture, object
silhouettes, and biome structure.

---

# Workflow

1. **Identify the mode** — planning, generation, revision, audit, or reformat.
2. **Identify the category** from the taxonomy.
3. **Read the brief** — tile size, camera, theme, required materials and objects,
   engine constraints, dimensions, animation needs.
4. **Select the profile** and restate its numbers explicitly.
5. **Build the blueprint** (brief for a single small asset).
6. **Lock grid, perspective, footprints, and lighting.**
7. **Establish palette ramps** within the family ceiling.
8. **Build the ground system** — seamless bases plus variants.
9. **Build transitions** — the full adjacency cohort per material pair.
10. **Build elevation.**
11. **Build objects**, at consistent scale.
12. **Build animation frames** for blueprint-listed assets only.
13. **Normalise** — key out the background, deliver genuine RGBA on the exact
    grid.
14. **Test repetition and adjacency** — 3 x 3 or larger, every transition
    combination, object footprints, animation alignment.
15. **Audit** — gameplay readability, style, originality, grid, export safety.

Generate focused groups, not an entire region at once.

---

# Reusable Planning Template

```
## Environment
[NAME]

## Thesis
[ONE SENTENCE — VISUAL AND FUNCTIONAL IDENTITY]

## Technical Profile
- Profile:            [internal-demo-v1 or approved alternative]
- Tile size:          [cellW x cellH]
- Camera:             three-quarter top-down
- Lighting:           upper-left
- Deliverable format: RGBA
- Palette ceiling:    [n] colours

## Palette and Materials
- Dominant ground:
- Supporting grounds:
- Vegetation:
- Stone / cliff:
- Constructed:
- Accents:

## Required Ground Tiles
[LIST WITH VARIANT COUNTS]

## Required Transitions
[EVERY MATERIAL PAIR + ADJACENCY COHORT]

## Required Elevation
[LIST]

## Required Objects
[LIST WITH TILE FOOTPRINTS]

## Collision-Relevant Surfaces
[WALKABLE / BLOCKED / LEDGE / HAZARD / ENCOUNTER / INTERACTABLE]

## Animated Assets
[LIST WITH FRAME COUNTS]

## Export Sheets
[PER SHEET: name, cellW, cellH, rows, columns, total px]
```

---

# Reusable Generation Prompt

Create an entirely original, game-ready terrain asset sheet in a polished
late-2000s handheld monster-RPG pixel-art aesthetic.

Asset brief:

`[INSERT THE CURRENT ASSET BRIEF.]`

This is a **reusable source palette for painting maps**, not a composed scene.
Present isolated, non-overlapping assets on a clean grid with generous negative
space. Do not produce a map, a scenic preview, or a decorated presentation board.

Use a `[cellW]` x `[cellH]` pixel tile grid — 32 x 32 for the `internal-demo-v1`
profile. Arrange the assets in `[rows]` rows and `[columns]` columns of flush
cells with no spacing, margin, or offset between them. Output the image at
exactly `[columns * cellW]` x `[rows * cellH]` pixels. Substitute the approved
profile's values before sending; never leave a placeholder or reuse another
profile's numbers.

Use a three-quarter top-down RPG perspective, upper-left lighting with form
shadow to the lower right, and consistent player-relative scale across every
asset on the sheet.

Construct the assets directly as native pixel art with crisp pixel boundaries,
deliberate clusters, compact two-to-four-step colour ramps, restrained texture,
readable material separation, clean tile alignment, and consistent object
footprints.

Every repeatable tile must connect seamlessly on every intended edge. Every
transition set must include the requested straight edges, outer corners, inner
corners, isolated patches, and junctions. Every visible cell must differ
meaningfully from the others — do not pad the sheet with near-identical
duplicates.

Place everything on a completely flat background of exactly `#FF00FF`, RGB
`255, 0, 255`. No foreground pixel may use exact `#FF00FF` or any near-magenta
colour.

Do not use anti-aliasing, semi-transparent pixels, gradients, painterly
rendering, 3D-rendered appearance, automatic pixelation artifacts, realistic
microtexture, text, labels, arrows, legends, palette charts, sample maps, UI
framing, borders, logos, or watermarks.

Do not reproduce, trace, recolor, or closely resemble an existing copyrighted
tileset, map, structure, or landmark.

---

# Revision Protocol

Preserve every approved element except the traits the revision targets:
tile size, perspective, scale, lighting, palette relationships, material
language, transition logic, object footprints, animation alignment, and sheet
ordering.

Change only what was requested, unless the asset fails a mandatory technical
rule. After revising, retest seams, corners, adjacency, collision readability,
and transparency.

---

# Quality-Control Checklist

## Planning

* [ ] The kit was planned before final generation.
* [ ] Every required category is represented.
* [ ] Every material pair's transitions are identified.
* [ ] Collision and interaction needs are stated.
* [ ] Each export sheet declares cellW, cellH, rows, columns, and total pixels.
* [ ] The plan contains no lore, ecology, or narrative padding.

## Deliverable Shape

* [ ] The sheet is a reusable source palette, not a composed map.
* [ ] Assets are isolated and non-overlapping with clear negative space.
* [ ] No cell duplicates another without meaningful difference.
* [ ] Any map composition is labelled a separate preview.

## Grid and Import

* [ ] Sheet width equals `columns * cellW`; height equals `rows * cellH`.
* [ ] Both axes are divisible by the tile size.
* [ ] Offset, spacing, and margin are zero.
* [ ] Multi-tile objects occupy whole cells and match declared footprints.
* [ ] Slicing at the profile tile size isolates every cell without manual
      measurement.

## Tile Construction

* [ ] Repeatable tiles loop seamlessly, tested at 3 x 3 or larger.
* [ ] Variants preserve boundary pixels.
* [ ] No accidental seams, stripes, or dominant repeated stamps.
* [ ] Corners, inner corners, and junctions are complete.

## Gameplay Readability

* [ ] Walkable and blocked surfaces are distinguishable.
* [ ] Elevation reads from the art alone.
* [ ] Entrances, exits, and ledges are obvious.
* [ ] Interactive objects show their interaction face.
* [ ] Hazardous terrain is visually distinct.

## Style

* [ ] Reads as native handheld-era pixel art, not a downscaled painting.
* [ ] Perspective and lighting are consistent across the sheet.
* [ ] Palette family is within the 24-colour ceiling, counted on the file.
* [ ] Texture is restrained and readable at native scale.

## Animation

* [ ] Frames share identical dimensions and stable anchors.
* [ ] The loop is clean with no drift.
* [ ] Collision footprint is constant across frames.

## Technical

* [ ] The deliverable is genuine RGBA, not a magenta-backed intermediate.
* [ ] Transparent space is fully transparent.
* [ ] No residual key-coloured fringe survives around any asset.
* [ ] No anti-aliasing or unintended semi-transparency.
* [ ] The sheet contains no planning elements, labels, or watermarks.
* [ ] IDs follow the `VISUAL_MEMORY.md` naming conventions.

---

# Failure Conditions

Reject or revise when:

* the output is a composed map or scenic preview instead of reusable cells
* a magenta-backed intermediate is presented as the deliverable
* the sheet is padded with near-identical duplicate cells
* the art is mechanically complete but visually crude
* tile edges do not connect, or repetition reveals seams or stamps
* transition sets are incomplete
* sheet dimensions are not exact multiples of the tile size
* offset or spacing is non-zero without an approved format requiring it
* perspective, lighting, or object scale drifts across the sheet
* collision boundaries are visually misleading
* elevation cannot be read from the art
* animation frames drift or fail to loop
* the foreground contains the key colour, or transparent space does not
* an asset sheet carries planning panels, labels, or explanatory text
* the environment copies a recognisable copyrighted map or tileset
* one biome reuses another's trees, rocks, and material language without reason

---

# Agent Behavior

When this skill is active:

1. Distinguish Planning Mode from Generation Mode and say which is active.
2. Plan complete kits before generating multi-sheet assets.
3. Treat tile size, perspective, scale, and sheet geometry as binding, and state
   them explicitly rather than inferring them.
4. Produce reusable modular cells, never a painted scene.
5. Deliver genuine RGBA; treat the chroma key as an intermediate only.
6. Build complete transition cohorts rather than isolated sample edges.
7. Test seams and adjacency on the file before approval.
8. Treat collision readability as part of the art direction.
9. Keep planning output limited to what generation and import consume.
10. Route buildings, interiors, animation timing, and final approval to their
    named specialist skills.
11. Never claim exact alignment, seam integrity, or colour compliance without
    inspecting the file.
12. Flag image-model limitations on exact tile alignment, exact RGB, and seamless
    repetition whenever compliance cannot be visually verified.

---

# Final Standard

Every environment should feel specific to its biome while unmistakably belonging
to the same game and painting cleanly onto the same grid.

Consistency comes from shared pixel craftsmanship, camera logic, grid discipline,
palette discipline, lighting, material readability, scale, collision clarity, and
export standards — never from reusing the same tree, rock, cliff, or palette
across every environment.

The world should feel varied and handcrafted while remaining modular, readable,
and production-ready.
