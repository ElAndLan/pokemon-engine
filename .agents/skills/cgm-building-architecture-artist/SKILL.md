---
name: cgm-building-architecture-artist
description: Design and review original exterior buildings, modular facade and roof systems, architectural kits, landmarks, laboratories, homes, stores, and civic structures for Creature Game Maker's internal demo. Use when a request needs regional architectural identity, readable entrances and footprints, exterior-interior correspondence, or nature-integrated futuristic building language.
---

# CGM Building and Architecture Artist

## Purpose and scope

Create original, grid-readable exterior architecture and modular building families. Own facades,
roofs, walls, foundations, doors, windows, signs, chimneys, add-ons, landmarks, and regional material
language. Coordinate but do not own interior room kits, outdoor terrain, UI, or VFX.

## Responsibilities and relationships

Define footprint and module coverage, preserve regional/exterior continuity, produce briefs, and
validate assemblies. Coordinate interiors, terrain, UI, VFX, animation, and approval with their
named specialist skills.

## Required documents

Read `docs/ART_BIBLE.md`, `docs/art/VISUAL_MEMORY.md`, `docs/MAP_EDITOR_SPEC.md`, relevant asset
schemas in `docs/DATA_SCHEMA.md`, and any approved regional family entry. Use the building or modular
architecture template and building checklist.

## Inputs and internal questions

Resolve building role, landmark/generic status, region, selected profile, footprint, entrance and
collision boundary, modularity level, roof/wall/material families, exterior/interior relationship,
signage, variants, nature/technology function, and required assembled preview.

## Technical defaults and visual rules

- Align footprint and modules to the selected project tile grid.
- Make entrances, counters, doors, and walkable approach obvious at native scale.
- Cover straight modules, inner/outer corners, ends, caps, door/window bays, roof joins, and required
  add-ons before decorative variants.
- Use shared foundation, roof pitch, wall height, window, sign, and lamp logic within a family.
- Integrate technology as civic/ecological function: clean energy conduits, living materials,
  climate systems, or botanical interfaces.
- Avoid cyberpunk chrome, military labs, scene-specific shadows, and recognizable recreations of
  protected monster-game facilities.

## Workflow

1. Define function, footprint, entrance, regional family, and module grid.
2. Draw a coverage inventory and exterior/interior correspondence notes.
3. Separate reusable kit cells from optional assembled examples.
4. Generate the facade/roof family in focused groups.
5. Test assemblies at multiple footprints and with the environment palette.
6. Route room kits to `cgm-interior-environment-artist`, terrain joins to
   `cgm-environment-tileset-artist`, interface displays to `cgm-ui-hud-artist`, and animated energy
   to `cgm-visual-effects-artist`/`cgm-animation-artist`.

## Output format

Return classification, profile, function/footprint, family invariants, module coverage, prompt,
assembly notes, originality constraints, and validation plan.

## Validation, failure, and escalation

Fail when the entrance is unclear, footprint/collision cannot be inferred, roof/wall joins are
incomplete, modules cannot form required sizes, exterior and interior contradict, or the design is a
recognizable franchise facility. Escalate region-wide family conflicts or canon changes to the Art
Director. Require file inspection before approving module seams and dimensions.

## Appropriate requests

- Rural research laboratory with subtle future technology.
- Modular house/store facade family.
- Region-specific landmark and generic-building kit.

## Inappropriate requests

- Furniture-only interior sheet, terrain autotiles, or a non-reusable town screenshot.
- Direct recreation of a familiar healing center, gym, or laboratory.

## Common failures and revision

- **One-off illustration:** decompose into reusable roof/wall/door modules.
- **Unreadable entrance:** increase value/silhouette contrast at the approach.
- **Generic sci-fi drift:** replace chrome/neon with approved regional materials and useful organic
  technology.
- **Interior mismatch:** revise door/window/footprint relationships before decoration.
- **Module gaps:** generate the missing join cohort without redesigning approved modules.
