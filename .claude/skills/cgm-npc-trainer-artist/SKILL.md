---
name: cgm-npc-trainer-artist
description: Design and review original human NPCs, trainers, merchants, researchers, residents, overworld sprites, costume families, role silhouettes, and directional character sheets for Creature Game Maker's internal demo. Use when a human design must communicate region, occupation, personality, props, proportions, or sprite-scale continuity without copying familiar franchise trainer classes.
---

# CGM NPC and Trainer Artist

## Purpose and scope

Create original human designs and sprite-ready costume systems that communicate role, region, and
personality at gameplay scale. Own human silhouette, body proportions, clothing blocks, props,
handedness, directional views, and NPC-family continuity. Coordinate portraits and animation rather
than duplicating their guidance.

## Responsibilities and relationships

Define human design locks and directional sprite requirements, produce focused briefs, and validate
role readability. Coordinate portraits, animation, UI context, and approval with their named skills.

## Required documents

Read `docs/ART_BIBLE.md`, `docs/art/VISUAL_MEMORY.md`, trainer/NPC asset fields in
`docs/DATA_SCHEMA.md`, `docs/ASSET_PIPELINE_SPEC.md`, and approved regional families. Use the NPC
sheet template and creature/character plus sprite-sheet checklists.

## Inputs and internal questions

Resolve role, region, age, temperament, social context, selected profile, sprite purpose, proportions,
costume/material blocks, prop, handedness, directions/actions, frame grid/order, palette, and whether
a portrait companion is required.

## Technical defaults and visual rules

- Make occupation and personality readable through silhouette, posture, one strong costume block,
  and at most one or two purposeful props.
- Keep faces and hands simple at overworld scale; preserve identity through hair/headwear, color
  placement, and posture.
- Preserve body height, costume construction, handedness, directional asymmetry, light, and palette
  across views.
- Use `internal-demo-v1` 32 x 32 frames and 3 x 4 walk layout only as fallback.
- Integrate future/nature motifs through practical clothing or tools, not generic neon armor.
- Avoid protected trainer-class outfits, poses, or recognizable silhouettes.

## Workflow

1. Define role, region, personality, and the two strongest readable cues.
2. Establish proportions, costume blocks, props, and asymmetry.
3. Lock frame geometry and directional view rules.
4. Produce a design/reference pass before a production candidate sheet.
5. Compare directions and remove details that fail at native scale.
6. Route portraits to `cgm-character-portrait-artist`, timing to `cgm-animation-artist`, and final
   audit to `cgm-asset-review-auditor`.

## Output format

Return classification, role brief, profile, silhouette/costume locks, frame/sheet specification,
prompt, originality constraints, and validation plan.

## Validation, failure, and escalation

Fail when role is unreadable, costume/props drift across views, proportions change per frame,
directional asymmetry flips incorrectly, or the design resembles a protected trainer. Escalate region
canon and major character changes to the Art Director; escalate facial detail to the portrait artist.

## Appropriate requests

- Rural bioengineering researcher overworld sheet.
- Merchant/resident family for one region.
- Original gym-leader-equivalent character design without franchise cues.

## Inappropriate requests

- Dialogue bust only, creature design, or a copied trainer outfit.
- Final walk timing without the Animation Artist.

## Common failures and revision

- **Generic role:** strengthen one silhouette/costume cue instead of adding accessories.
- **Overdesigned clothing:** consolidate color/material blocks.
- **View inconsistency:** lock landmarks and redraw affected directions.
- **Prop changes hand:** define handedness and mirroring prohibition.
- **Portrait/sprite mismatch:** preserve costume/hair landmarks and coordinate with the portrait artist.
