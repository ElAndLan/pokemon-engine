# Image Generation and Production Readiness

Use this guide for prompts, generated images, revisions, and production claims. It is tool-neutral so
Codex, Claude, and future agents can use whichever image system is available.

## Required request classification

Classify the requested deliverable before drafting a prompt:

| Class | Intended use | Honest completion claim |
|---|---|---|
| Concept | Explore shape, motif, palette, composition, or family direction | Concept-ready |
| Cleanup source | Supply focused source art for manual pixel reconstruction | Cleanup-ready source |
| Production candidate | Aim at the approved grid/layout for later validation | Production candidate |
| Review | Inspect a prompt or actual artifact | One of the audit statuses below |

Use **verified production-ready** only after inspecting the actual file and passing every applicable
technical check.

## Resolve inputs

Before generation, resolve:

- asset purpose and gameplay context;
- concept versus reusable production asset;
- owning specialist skill(s);
- named asset profile or explicit dimensions;
- view, perspective, camera, lighting, and scale;
- frame/grid layout, spacing, padding, baseline, anchor, and transparency;
- approved family references and traits to preserve;
- palette target, contrast role, and accessibility needs;
- required variants/states/animation beats;
- destination or intended import workflow; and
- legal/originality constraints.

Ask only when a missing answer materially changes compatibility, canon, or layout. For concept work,
state reversible assumptions. Never infer a production grid from genre convention alone.

## Generation workflow

1. Read `docs/ART_BIBLE.md` and `docs/art/VISUAL_MEMORY.md`.
2. Load the discipline-specific specialist skill and relevant project spec.
3. Select the closest template in `docs/art/PROMPT_TEMPLATES.md`.
4. Produce a focused brief. Split unrelated sheets or animations into separate generations.
5. Generate or hand the prompt to the available image tool.
6. Preserve the original output as source evidence.
7. Inspect the result visually at native scale and integer multiples.
8. For production candidates, validate file properties and reconstruct/clean in pixel-art tooling.
9. Run the applicable checklists in `docs/art/VALIDATION_CHECKLISTS.md`.
10. Assign an honest readiness status and list remaining corrections.

## Model limitations

Do not assume an image model produced exact dimensions, exact frame sizes, seamless tiles, stable
anatomy, correct alpha, a strict palette, registered frames, or non-overlapping cells. Text in images
and fine UI labels are also unreliable. A prompt can request these properties but cannot certify them.

Prefer small focused groups: approved base design, approved key poses, one action or directional set,
cleanup/alignment, then final sheet assembly.

## Prompt construction

Include these fields when relevant:

- role and deliverable class;
- asset purpose and game context;
- required source documents and approved references;
- exact asset list;
- nominal frame/canvas dimensions and whether they are strict targets or concept guides;
- rows, columns, ordering, spacing, padding, baseline, anchors, and background;
- perspective, camera, lighting, palette, outline, and material rules;
- modularity, tileability, state, and animation requirements;
- originality and negative constraints;
- expected output presentation; and
- explicit validation caveat.

Do not say only “production-ready,” “pixel perfect,” or “make it look good.” State observable
requirements.

## Revision workflow

When a result fails:

1. Preserve what already works.
2. Name the failure by location and observable property.
3. Change the smallest relevant prompt variables.
4. Regenerate only the affected asset or frame group when possible.
5. Re-run the failed checks plus regression checks for silhouette, palette, lighting, and family
   continuity.

Do not repair structural failures by adding texture, glow, or prompt length.

## Audit statuses

- **Concept approved:** direction may continue; not implementation art.
- **Direction approved:** key design decisions are accepted; execution may still change.
- **Cleanup required:** useful source with identifiable production corrections.
- **Production candidate:** suitable for file-level validation and final cleanup.
- **Production validation required:** looks complete but technical facts are unverified.
- **Rejected for style mismatch:** conflicts with the art bible or approved family.
- **Rejected for technical failure:** cannot satisfy the requested asset structure without rework.

An auditor must not approve tileability, alpha, dimensions, palette count, or registration without
inspecting the actual file with suitable technical validation.
