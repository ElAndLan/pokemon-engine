# Art Prompt Templates

Use these as structured briefs, not prose to copy blindly. Resolve fields from an approved asset
brief, family entry, or named profile. Delete irrelevant fields. Every prompt must state an honest
deliverable class and validation caveat.

## Contents

1. Shared header
2. Environment tileset
3. Terrain autotile set
4. Nature asset sheet
5. Town prop sheet
6. Building asset sheet
7. Modular architecture kit
8. Creature concept sheet
9. Creature battle sprite
10. Creature overworld sprite
11. NPC sprite sheet
12. Character portrait
13. Item icon sheet
14. UI component sheet
15. VFX sheet
16. Animation sheet
17. Asset revision
18. Style-matching continuation
19. Asset review

## Shared header

```text
Role: [specialist role]
Deliverable class: [concept | cleanup source | production candidate | review]
Asset purpose: [gameplay/production use]
Game context: [location, actor, state, camera]
Required sources: docs/ART_BIBLE.md; docs/art/VISUAL_MEMORY.md; [relevant spec/family]
Selected profile: [profile ID or explicit custom values]
Visual direction: [silhouette, mood, motifs, perspective, lighting]
Originality: create an original design; do not copy protected characters, facilities, logos, or art
Negative constraints: no soft brush, accidental anti-aliasing, uncontrolled gradients/bloom/noise,
  screenshot framing, labels over reusable cells, or unrequested scene background
Expected output: [sheet/image arrangement and background]
Validation caveat: requested dimensions, alpha, palette, tileability, and registration require
  inspection and cleanup of the actual file before production approval
```

## Environment tileset

Append:

```text
Asset list: [ground, paths, cliffs, water, encounter terrain, transitions, overlays, props]
Tile/grid: [tile W x H; rows/columns; margin/spacing; transparent padding]
Coverage: [centers, edges, inner/outer corners, endcaps, T/cross junctions]
Perspective and elevation: [three-quarter rules; cliff height; overlap rules]
Modularity: no map composition; every cell isolated; variants preserve seams and texel density
Collision readability: [solid/passable/ledge/water/encounter cues]
Palette: [family and target]
Output: orthographic reusable tileset sheet on transparent background
```

## Terrain autotile set

Append:

```text
Terrain pair: [base and adjoining terrain]
Autotile convention: [engine/editor target or explicit required adjacency cases]
Required cells: full center/edge/corner/junction/isolated coverage
Transition width: [pixels/percentage] consistent on every matching edge
Repeat test: cells must tolerate repeated neighbors without scene-specific shadows or landmarks
Output: indexed cell diagram plus clean unlabeled asset cells; diagram is not the import sheet
```

## Nature asset sheet

Append:

```text
Family: [trees/shrubs/flowers/rocks/water plants]
Asset list and footprint: [each item with tile footprint]
Variation system: [trunk/canopy/season/age variants that remain one family]
Layering: [base, below-actor, above-actor pieces]
Silhouette/noise: large readable masses, controlled clusters, no random microtexture
Output: isolated reusable assets, no scenic composition
```

## Town prop sheet

Append:

```text
Regional context: [materials, civic function, technology motif]
Asset list: [signs, lamps, benches, fences, bins, kiosks, planters, utility props]
Footprints/anchors: [per asset]
Interaction cues: [readable face/side and access point]
Family consistency: shared fasteners, materials, color accents, and scale
```

## Building asset sheet

Append:

```text
Building role: [home/store/lab/civic/landmark]
Footprint and entrance: [tile dimensions; door position; collision boundary]
Views: [facade/roof/side modules or assembled exterior]
Architecture: [regional materials, roof, windows, foundation, sign system]
Nature/technology integration: [specific useful motif]
Exterior/interior correspondence: [door/window/footprint constraints]
Originality: avoid recognizable monster-franchise facility language
```

## Modular architecture kit

Append:

```text
Kit scope: [facades/roofs/walls/windows/doors/foundations/add-ons]
Module grid: [tile footprint and join rules]
Coverage matrix: straight, inner/outer corner, end, junction, cap, entrance, window, damaged/variant
Assembly constraints: matching edges, consistent wall height, no baked building-specific shadow
Example assembly: optional separate mockup; never merge it into the reusable kit sheet
```

## Creature concept sheet

Append:

```text
Creature premise: [habitat + behavior + ability/gameplay role]
Design hierarchy: one dominant feature; one secondary feature; one memorable accent
Anatomy: [locomotion, limbs, joints, center of mass, animation constraints]
Views/poses: [turnaround and personality/action poses]
Evolution continuity: [stage role, inherited structures, meaningful transformation]
Complexity ceiling: details must survive [target scale]
Output: concept board; not a sprite sheet or production asset
```

## Creature battle sprite

Append:

```text
Approved design reference: [required]
View/state: [front/back; idle/action]
Nominal frame: [W x H] with [padding], baseline [value], anchor [value]
Occupancy: [percentage/bounds] and silhouette priority
Anatomy/palette locks: [traits that cannot drift]
Background: transparent; no floor card, labels, UI, or scene
Output: one focused sprite or small approved pose group
```

## Creature overworld sprite

Append:

```text
Approved design reference: [required]
Directions/states: [down/left/right/up; idle/walk]
Frame/grid: [custom values or named profile], row/column order, spacing, padding
Registration: ground contact, center, anchor, directional asymmetry, mirroring permission
Simplification: list features that must survive and details to omit
```

## NPC sprite sheet

Append:

```text
Role/region/personality: [observable silhouette and costume cues]
Approved design reference: [required for production candidate]
Directions/actions: [walk/idle/interaction]
Frame/grid/order: [values]
Locks: height, body proportions, costume blocks, props, handedness, palette
Registration: baseline, anchor, foot contact, no frame-to-frame scale drift
```

## Character portrait

Append:

```text
Character/scene purpose: [dialogue/emotion/profile]
Crop/canvas: [head/bust; dimensions; safe area]
Expression and gaze: [specific]
Design locks: face, hair, costume, accessories, palette, light direction
Readability: strong facial value grouping at native scale; transparent background unless panel requested
```

## Item icon sheet

Append:

```text
Item list/categories: [one clear function per icon]
Icon cell/grid/order: [values]
Silhouette/value hierarchy: recognizable before interior detail
State variants: [normal/disabled/selected only if requested]
Consistency: shared camera, scale, outline, light, palette, padding
No labels, inventory panel, perspective mismatch, or cell overlap
```

## UI component sheet

Append:

```text
UI context: [menu/HUD/dialogue/inventory]
Virtual resolution and safe areas: [values]
Components: [nine-slice parts, buttons, cursor, bars, tabs, prompts]
States: normal, focused, selected, pressed, disabled, warning/error as required
Typography/icon slots: [bitmap font metrics and padding; do not render unreliable final copy]
Accessibility: contrast, non-color state cues, keyboard/controller focus
Modularity: isolated components; no screenshot mockup as the only output
```

## VFX sheet

Append:

```text
Gameplay effect: [source, target, area, timing, element]
Phases: anticipation, travel if any, contact, dissipation
Frame/grid/order: [values]; one effect family per generation
Anchors/blend: [origin/contact point; opaque/additive assumptions]
Readability: actors, targets, and HUD remain visible; limited particles and palette
```

## Animation sheet

Append:

```text
Approved base design/key poses: [required]
Action and purpose: [one focused action]
Frames/order/timing: [target count and per-frame/beat guidance]
Motion structure: anticipation, action/contact, recovery, loop behavior
Registration: baseline, anchor, center of mass, scale, lighting, palette
Secondary motion: [what follows and with what delay]
```

## Asset revision

```text
Source asset: [path/reference]
Current approval status: [status]
Preserve exactly: [approved silhouette, palette, layout, family traits]
Observed failures: [location + measurable/visible issue]
Requested corrections: [smallest changes]
Do not change: [unaffected regions/frames]
Revalidation: [failed checks plus regression checks]
Expected output: revised source/candidate, not a new interpretation
```

## Style-matching continuation

```text
Approved family: [VISUAL_MEMORY entry and defining assets]
New asset/function: [what must be added]
Match: scale, perspective, light, palette roles, outline, materials, modules, naming
Preserve family invariants: [list]
Allowed novelty: [limited area]
Reject if: continuation alters the established family language or copies an external franchise
```

## Asset review

```text
Artifact or prompt: [path/text]
Intended class/profile: [values]
Required sources: [art bible, family, technical spec]
Inspect: visual consistency, originality, gameplay readability, structure, dimensions, alpha,
  palette, seams/registration, naming/import risks
Report: blocking issues; significant inconsistencies; minor polish; production risks;
  recommended corrections; approval status
Evidence rule: do not approve uninspected file properties
```
