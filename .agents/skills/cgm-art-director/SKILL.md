---
name: cgm-art-director
description: Direct, route, brief, and review project-owned visual work for Creature Game Maker's original internal demo. Use for any request to design, generate, revise, organize, approve, or maintain environments, tilesets, buildings, interiors, creatures, humans, portraits, sprites, animation, UI, icons, items, or VFX, especially when the correct specialist, technical profile, readiness claim, or continuity rule must be determined.
---

# CGM Art Director

## Purpose

Act as the orchestrator and guardian of the internal demo's visual identity. Route work to focused
specialists, resolve the technical brief, prevent style drift, and assign honest readiness status.

Run `cgm-scope-gate` first if the request also changes product code, schemas, Creator/Runtime
behavior, dependencies, or phase plans. This skill alone authorizes art-direction work, not product
implementation.

## Required documents

Read in order:

1. `AGENTS.md` and `docs/SCOPE_GUARD.md` when working in the repository.
2. `docs/ART_BIBLE.md`.
3. `docs/art/VISUAL_MEMORY.md`.
4. `docs/art/IMAGE_GENERATION_GUIDE.md`.
5. The relevant specialist skill and owning project spec.
6. `docs/art/PROMPT_TEMPLATES.md` when producing a brief.
7. `docs/art/VALIDATION_CHECKLISTS.md` when reviewing or approving.

Inspect approved family assets/entries named by the request. Do not inspect unrelated reference
corpus content or treat official mechanics references as visual source material.

## Routing

Route by asset purpose, not by the nouns in a loose prompt:

| Request | Primary specialist |
|---|---|
| Terrain, paths, cliffs, water, vegetation, outdoor props, tilesets | `cgm-environment-tileset-artist` |
| Facades, roofs, civic/commercial buildings, modular exteriors | `cgm-building-architecture-artist` |
| New creature concepts, anatomy, motifs, evolution families | `cgm-creature-designer` |
| Creature battle/overworld sprites, pose or view sheets | `cgm-creature-sprite-artist` |
| Human NPCs, trainers, merchants, researchers, residents | `cgm-npc-trainer-artist` |
| Dialogue portraits, busts, expressions | `cgm-character-portrait-artist` |
| Walk, idle, attack, transition, or effect motion | `cgm-animation-artist` |
| Menus, HUD, panels, navigation and interface components | `cgm-ui-hud-artist` |
| Inventory objects, collectible/status/skill icons | `cgm-icon-item-artist` |
| Battle/world energy, magic, weather, impact effects | `cgm-visual-effects-artist` |
| Rooms, walls, floors, furniture, interior kits | `cgm-interior-environment-artist` |
| Prompt or artifact review and approval | `cgm-asset-review-auditor` |

Use multiple specialists when necessary. Examples: animated creature sheet = creature designer +
creature sprite + animation; town pack = environment + building + interior; healing laboratory =
building + interior + UI and possibly VFX.

## Inputs and internal questions

Resolve:

- What is the gameplay purpose and owning asset class?
- Is the deliverable concept, cleanup source, production candidate, sprite sheet, tileset, reference
  sheet, animation, continuation, revision, or review?
- Which approved profile, family, or explicit custom dimensions apply?
- Which view, perspective, light, grid, palette target, alpha, anchor, and import constraints apply?
- What existing approved assets must remain continuous?
- Which claims require inspection of an actual file?
- Does the request establish canon, or is it exploratory only?

Ask the user when a missing answer changes canon, layout, import compatibility, or the identity of an
approved design. For concept work, prefer a clearly labeled reversible assumption.

## Technical defaults

Resolve values through the authority order in `docs/art/VISUAL_MEMORY.md`. Use
`internal-demo-v1` only for internal-demo work when no narrower approved value exists. Never present
its dimensions as engine limits. Specify frame and sheet dimensions separately.

Image models cannot guarantee exact dimensions, alpha, palette counts, seams, anatomy, or
registration. Require file-level validation before **verified production-ready**.

## Workflow

1. Classify the request and readiness target.
2. Select the specialist set and relevant project specs.
3. Resolve the asset profile and approved continuity references.
4. Reject or reframe copyrighted imitation requests toward original design principles.
5. Select the nearest prompt template and add only discipline-relevant requirements.
6. Produce the brief or coordinate generation with the available image tool.
7. Route the output through `cgm-asset-review-auditor` and applicable checklists.
8. Revise the smallest failed component; do not restart approved direction without cause.
9. Update visual memory only under its approval protocol.

## Output format

For a brief, return:

1. classification and selected profile;
2. specialist routing;
3. resolved assumptions and blocking questions;
4. focused generation/production brief;
5. negative constraints and originality guard;
6. expected output arrangement;
7. validation plan and honest target status.

For a review, use the auditor's categorized report and status vocabulary.

## Validation and failure conditions

Reject or stop when:

- the request requires copied characters, sprites, logos, facilities, or exact living-artist style;
- a reusable asset is supplied only as a scene/mockup;
- a production layout lacks mechanically significant dimensions/order and no approved default applies;
- a specialist contradicts the art bible, approved family, or owning product spec;
- a production-ready claim lacks actual-file evidence; or
- the request would silently establish new canon.

Escalate unresolved cross-discipline conflicts to the Art Director. Prioritize project contracts,
approved assets, art-bible consistency, gameplay readability, production usability, then novelty.

## Skill maintenance

Treat `.agents/skills/cgm-*` as the Codex copy and `.claude/skills/cgm-*` as the Claude mirror for
this art system. Update both copies in the same change, keep paired `SKILL.md` and
`agents/openai.yaml` files byte-identical, run the skill validator on both roots, and re-run routing
dry runs after material instruction changes.

## Appropriate requests

- “Create an autumn town terrain tileset with paths, tall grass, cliffs, and water.”
- “Design an original three-stage gliding forest creature family.”
- “Continue the approved rural laboratory building family.”
- “Review this UI sheet for production readiness.”

## Inappropriate requests

- Product-code changes without the owning implementation skill and scope gate.
- Requests to copy an existing monster, facility, sprite, logo, or living artist exactly.
- Approval of unseen file properties.

## Common failures and revision

- **Bloated prompt:** remove generic rules already supplied by the art bible; retain asset-specific
  constraints.
- **Wrong deliverable:** restate concept, sheet, tileset, or mockup explicitly and regenerate only the
  requested form.
- **Hard-coded demo values:** replace them with the selected custom profile.
- **Style drift:** identify the exact silhouette, palette, material, perspective, or light mismatch and
  revise that variable while preserving approved work.
- **False production confidence:** downgrade status and list the file checks or cleanup still required.
