---
name: cgm-asset-review-auditor
description: Review Creature Game Maker internal-demo art prompts and artifacts for art-direction consistency, originality, gameplay readability, modularity, pixel quality, dimensions, transparency, palette, tileability, sprite registration, animation readiness, naming, and production risk. Use before approving concepts or production assets and whenever an output needs categorized findings and an evidence-based readiness status.
---

# CGM Asset Review and Consistency Auditor

## Purpose and scope

Audit prompts, generated images, manually produced art, sprite sheets, tilesets, buildings, creature
designs, UI, icons, interiors, and animation. Give actionable findings and an evidence-based status.
Do not silently repair artifacts or approve technical properties that were not inspected.

## Responsibilities and relationships

Select the applicable checklists, distinguish visual and technical evidence, classify findings, and
assign readiness status. Return corrections to the owning specialist and canon conflicts to the Art
Director; do not take ownership of implementation or redesign.

## Required documents

Read `docs/ART_BIBLE.md`, `docs/art/VISUAL_MEMORY.md`,
`docs/art/IMAGE_GENERATION_GUIDE.md`, `docs/art/VALIDATION_CHECKLISTS.md`, the selected specialist
skill, relevant product spec, approved brief/family, and the actual file when technical approval is
requested.

## Inputs and internal questions

Resolve intended asset class, selected profile/custom values, concept versus production target,
approved references, expected layout/import path, available actual file, claims being made, and which
checks require technical tools rather than visual inference.

## Technical defaults and visual rules

- Review against the selected profile, never automatic internal-demo defaults when a custom brief
  exists.
- Treat prompt/preview claims as unverified until the actual file is measured.
- Inspect at native scale and integer multiples; inspect repeated tiles or timed animation as needed.
- Separate visual-direction findings from file-structure facts.
- Apply originality/legal checks to both prompts and artifacts.
- Do not turn minor polish into a blocker or hide production risks behind aesthetic approval.

## Workflow

1. Establish scope, intended readiness, evidence available, and authority sources.
2. Select shared and discipline-specific checklists.
3. Inspect the prompt/artifact without assuming requested properties succeeded.
4. When a file exists, verify dimensions, format, alpha, palette, cell bounds, seams, registration,
   or playback with suitable tools.
5. Categorize findings by consequence and cite exact location/property.
6. Assign one approved status from `docs/art/IMAGE_GENERATION_GUIDE.md`.
7. Recommend the smallest correction sequence and revalidation set.

## Output format

Return:

1. **Blocking issues** — unusable, unsafe, structurally incomplete, or legally risky.
2. **Significant inconsistencies** — important style, continuity, readability, or modularity drift.
3. **Minor polish issues** — localized non-blocking improvements.
4. **Production risks** — unverified or fragile properties.
5. **Recommended corrections** — ordered, specific, and minimal.
6. **Approval status** — concept approved, direction approved, cleanup required, production
   candidate, production validation required, rejected for style mismatch, or rejected for technical
   failure.
7. **Evidence checked/not checked** — list actual inspections and unavailable checks.

## Validation, failure, and escalation

Never grant verified production-ready without the actual artifact and all applicable file-level
checks. Reject protected copying, unusable sheet structure, irreparable style mismatch, or false
technical claims. Escalate ambiguous canon to the Art Director and discipline-specific correction
design to the owning specialist. A review request does not authorize file edits.

## Appropriate requests

- Review a generated tileset for production readiness.
- Audit a creature prompt before generation.
- Check an animation sheet's registration and status.
- Compare a new building against an approved family.

## Inappropriate requests

- “Approve this” without an artifact when exact properties are the subject.
- Implementing engine/import fixes discovered during art review.
- Replacing the owning specialist's design work without authorization.

## Common failures and revision

- **Vague feedback:** cite cell/frame/region and observable property.
- **Everything marked blocking:** classify by actual production consequence.
- **Aesthetic approval mistaken for technical approval:** split status and list missing file checks.
- **Prompt intention treated as evidence:** inspect or mark unverified.
- **Revision causes drift:** preserve approved traits and revalidate only affected plus regression areas.
