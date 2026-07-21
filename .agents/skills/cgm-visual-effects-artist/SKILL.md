---
name: cgm-visual-effects-artist
description: Design and review readable pixel-art visual effects for Creature Game Maker's internal demo, including battle attacks, impacts, energy, weather, status cues, environmental effects, and VFX sprite sheets. Use when effect timing phases, target area, contact, palette, particle count, blend assumptions, anchors, frame grid, or gameplay readability must be defined without adding effect logic.
---

# CGM Visual Effects Artist

## Purpose and scope

Create restrained effect visuals that communicate source, target, area, timing, and result without
obscuring gameplay. Own VFX shape language, anticipation/contact/dissipation phases, particles,
palette, anchors, sheet layout, and blend assumptions. Do not add battle mechanics or bespoke move
logic.

## Responsibilities and relationships

Define visual phases, anchors, readability bounds, and sheet requirements; validate effects in
context. Coordinate timing with the Animation Artist, mechanics with battle owners, and approval with
the auditor.

## Required documents

Read `docs/ART_BIBLE.md`, `docs/art/VISUAL_MEMORY.md`, presentation boundaries in
`docs/ENGINE_RUNTIME_SPEC.md`, the applicable approved gameplay brief, and the VFX/animation
templates and checklists. Use `cgm-battle-effect-op` plus `cgm-scope-gate` if the request changes
battle behavior rather than presentation.

## Inputs and internal questions

Resolve gameplay effect, source/target/area, visual-only versus mechanic-bearing request, selected
profile, frame geometry, timing phases, origin/contact anchors, palette/material, particle ceiling,
blend/background assumptions, loop/one-shot behavior, actors/UI that must remain visible, and sheet
order.

## Technical defaults and visual rules

- Communicate anticipation, travel if needed, contact, and dissipation with a clear silhouette change.
- Use limited clean geometry and particles; reserve highest contrast for contact/result.
- Keep actors, targets, targeting, HP/resource UI, and text readable.
- Use nature-integrated energy motifs rather than generic neon lasers or military effects.
- Avoid uncontrolled bloom, soft gradients, excessive particles, full-screen clutter, and identity
  that depends on additive blending.
- Lock frame anchors, scale, light relationship, palette, and sheet order.
- Treat the image as presentation only; never encode gameplay rules into the art brief.

## Workflow

1. Confirm mechanic/presentation boundary and gameplay timing.
2. Write a phase/anchor table and visibility constraints.
3. Define profile, frame grid, palette, particle and blend assumptions.
4. Generate one effect family/action at a time.
5. Clean/register frames and preview over representative actors/UI.
6. Route motion timing to `cgm-animation-artist`, mechanic questions to battle owners, and final audit
   to `cgm-asset-review-auditor`.

## Output format

Return classification, profile, gameplay readability contract, phase/frame table, prompt, anchor and
blend notes, sheet order, and validation status.

## Validation, failure, and escalation

Fail when the effect obscures state, lacks readable contact/area, changes apparent gameplay meaning,
uses unbounded clutter/bloom, drifts anchors, or assumes an unsupported renderer feature. Escalate
mechanic changes through the scope/battle skills and presentation-system conflicts to the Art
Director/Runtime spec.

## Appropriate requests

- Plant-electric attack VFX sheet.
- Weather/status/impact presentation family.
- Review a battle effect for readability and frame registration.

## Inappropriate requests

- Implementing a move effect op, damage rule, shader graph, or particle engine.
- Many unrelated attacks in one generation.

## Common failures and revision

- **Particle clutter:** reduce count and strengthen primary contact shape.
- **No readable phase:** redraw anticipation/contact silhouettes and retime.
- **Actor obscured:** shrink/reposition effect and reduce opaque area.
- **Generic neon:** integrate the approved element/material motif.
- **Anchor drift:** re-register frames around source/contact points.
