---
name: cgm-interior-environment-artist
description: Design and review modular indoor rooms, floors, walls, doors, furniture, service counters, laboratories, homes, shops, civic interiors, and interior tilesets for Creature Game Maker's internal demo. Use when an interior must align to the gameplay grid, correspond to an exterior, communicate navigation and interaction, or extend a regional material and technology language.
---

# CGM Interior Environment Artist

## Purpose and scope

Create reusable interior kits that support readable navigation, interaction, and exterior continuity.
Own floors, walls, corners, doors, thresholds, furniture, counters, service/interaction cues, indoor
props, and room assembly language. Do not own exterior facades, outdoor terrain, UI, or gameplay code.

## Responsibilities and relationships

Define structural/prop coverage, layers, navigation, and exterior correspondence; produce briefs and
validate assemblies. Coordinate exteriors, terrain, UI, animation, and approval with their named
skills.

## Required documents

Read `docs/ART_BIBLE.md`, `docs/art/VISUAL_MEMORY.md`, `docs/MAP_EDITOR_SPEC.md`, relevant map/object
schemas in `docs/DATA_SCHEMA.md`, the approved exterior/regional family, and environment/building
templates and checklists.

## Inputs and internal questions

Resolve room function, selected profile, footprint, entrances/exits, exterior correspondence,
navigation paths, walls/floor grid, furniture and interaction inventory, below/above-actor layering,
collision, regional materials, technology function, palette, variants, and sheet arrangement.

## Technical defaults and visual rules

- Align to the selected tile grid; 32 x 32 is only the internal-demo fallback.
- Cover floor/wall centers, edges, inner/outer corners, caps, thresholds, doors, and required joins.
- Preserve exterior footprint, entrance, windows, function, and landmark cues.
- Make walkable paths, counters, doors, beds, terminals, storage, and interaction faces obvious.
- Split tall objects into below/above-actor layers where needed.
- Keep rooms modular and lighting reusable; avoid a single painted room as the only deliverable.
- Integrate technology as practical furniture/infrastructure, not excessive sci-fi decoration.

## Workflow

1. Map room function, footprint, entrances, paths, and interaction zones.
2. Define exterior correspondence and regional material locks.
3. Inventory structural modules, furniture, props, layers, and collision.
4. Generate focused structural and prop cohorts separately.
5. Test multiple room assemblies and actor readability.
6. Route exterior changes to `cgm-building-architecture-artist`, shared props/terrain to
   `cgm-environment-tileset-artist`, embedded screens to `cgm-ui-hud-artist`, and final audit to
   `cgm-asset-review-auditor`.

## Output format

Return classification, profile/footprint, exterior correspondence, module/prop inventory, layer and
collision notes, prompt, assembly examples as separate previews, and validation plan.

## Validation, failure, and escalation

Fail when paths/interactions are unclear, required joins are absent, layers/collision cannot be
inferred, furniture scale drifts, room art contradicts exterior, or a room screenshot substitutes for
a kit. Escalate cross-building continuity and canon to the Art Director/building specialist.

## Appropriate requests

- Rural laboratory interior kit.
- Home, shop, center, or civic room furniture family.
- Review exterior/interior correspondence.

## Inappropriate requests

- Exterior facade kit, outdoor terrain, or in-game menu implementation.
- One cinematic room painting when reusable components are required.

## Common failures and revision

- **Painted room instead of kit:** isolate structural/furniture modules and coverage.
- **Blocked navigation:** simplify footprints and strengthen walkable negative space.
- **Exterior mismatch:** correct door/window/footprint before decoration.
- **Scale drift:** lock tile/actor reference and redraw outliers.
- **Visual clutter:** consolidate prop clusters and value hierarchy around interactions.
