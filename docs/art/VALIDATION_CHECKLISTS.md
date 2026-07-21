# Art Validation Checklists

Apply the shared checks plus every asset-specific section that matches the deliverable. Report
blocking issues, significant inconsistencies, minor polish issues, production risks, recommended
corrections, and one status from `docs/art/IMAGE_GENERATION_GUIDE.md`.

## Art-direction consistency

- [ ] Matches `docs/ART_BIBLE.md` and the selected asset profile.
- [ ] Fits the world, region, gameplay role, and approved family.
- [ ] Uses approved nature/technology motifs without cyberpunk or industrial drift.
- [ ] Uses the approved perspective and upper-left lighting or records an approved exception.
- [ ] Uses an appropriate controlled palette and value hierarchy.
- [ ] Reads at native scale with a strong silhouette and limited noise.
- [ ] Is original and avoids protected characters, sprites, facilities, logos, and symbols.

## Pixel-art quality

- [ ] Edges are crisp with no accidental anti-aliasing or resampling blur.
- [ ] Gradients, if any are explicitly approved for non-sprite source art, are not accidental.
- [ ] Palette count and transparency were measured from the file when claimed.
- [ ] Outlines, light direction, hue shifts, and value steps are consistent.
- [ ] Pixel clusters describe form rather than isolated noise.
- [ ] The asset works at native scale and intended integer scales.

## Tileset and autotile readiness

- [ ] Actual tile dimensions match the approved profile/brief.
- [ ] Asset is a reusable tileset, not a scene or map mockup.
- [ ] Required centers, edges, inner/outer corners, endcaps, T-junctions, and crossroads exist.
- [ ] Terrain/elevation transitions and overlay/underlay relationships are complete.
- [ ] Repeated tests show seamless tiling with consistent texel density.
- [ ] Collision, entrance, ledge, water, and encounter surfaces read clearly.
- [ ] Variants preserve seams and contain no baked scene-specific lighting.
- [ ] Cells do not overlap and required transparent padding is clean.

## Building and interior readiness

- [ ] Footprint, grid, entrance, doors, windows, and collision intent are clear.
- [ ] Modular roof/wall/foundation/sign pieces cover required joins and variants.
- [ ] Exterior and interior layouts correspond.
- [ ] Landmark cues do not compromise reusable kit parts.
- [ ] The design is original and not a recognizable franchise facility recreation.

## Creature and character readiness

- [ ] Silhouette, dominant/secondary/accent features, role, and personality read clearly.
- [ ] Anatomy, costume, props, handedness, and directional asymmetry are stable.
- [ ] Details survive the target scale and remain animation-friendly.
- [ ] Evolution/family or regional continuity is preserved without simple enlargement.
- [ ] Front/back/side views agree on proportions and construction.

## Sprite-sheet readiness

- [ ] Actual canvas, frame dimensions, rows, columns, order, spacing, and padding were measured.
- [ ] Scale, anatomy, palette, outline, and lighting remain stable across frames.
- [ ] Ground contact, baseline, center of mass, and anchor points align.
- [ ] No frame is clipped, duplicated accidentally, overlapped, or contaminated by labels.
- [ ] Background alpha is correct and transparent cells contain no matte fringe.
- [ ] Mirroring is allowed only where directional asymmetry permits it.
- [ ] Import metadata and source-sheet naming follow project conventions.

## Animation readiness

- [ ] Frame order and timing intent are documented.
- [ ] Anticipation, action/contact, recovery, and loop transition are readable.
- [ ] Registration and grounding remain stable; secondary motion follows the primary action.
- [ ] The loop has no unintended jump, drift, or held duplicate frame.
- [ ] Each direction preserves design and motion logic.

## UI and icon readiness

- [ ] Text and icons read at native scale; pixel-font metrics are respected.
- [ ] Panels tile or nine-slice cleanly and do not bake labels into reusable chrome.
- [ ] Normal, hover where applicable, focus, selected, pressed, and disabled states are distinct.
- [ ] Keyboard/controller focus is visible without color alone.
- [ ] Contrast and color meaning remain accessible; information density fits 240 x 160 presentation.
- [ ] Icons are recognizable by silhouette and value grouping before small details.

## VFX readiness

- [ ] Anticipation, contact, and dissipation communicate timing and target area.
- [ ] Effect does not obscure actors, UI, targeting, or result readability.
- [ ] Palette, lighting, geometry, particle count, blend assumptions, and anchors are consistent.
- [ ] Frames are registered and loop/one-shot behavior is explicit.
- [ ] Glow, bloom, gradients, and particles are restrained or removed for production sprites.

## Legal and originality

- [ ] No copyrighted character, copied sprite, logo, trademarked symbol, or traced composition.
- [ ] No recognizable recreation of a protected facility, costume, or interface.
- [ ] No prompt requires exact imitation of a living artist.
- [ ] Reference provenance and license/permission are recorded when an external source is used.

## File-level production gate

- [ ] Open and inspect the actual file; a prompt or preview is insufficient.
- [ ] Verify format, dimensions, alpha mode, color mode, palette count, and frame/cell bounds.
- [ ] Test tile seams or animation playback using the intended grid and ordering.
- [ ] Check source path, stable IDs, naming, and intended import metadata.
- [ ] Confirm the asset introduces no unapproved canon or undocumented exception.

Only after every required item passes may the status be **verified production-ready**.
