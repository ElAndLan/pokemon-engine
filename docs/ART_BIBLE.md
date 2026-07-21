# Creature Game Maker Art Bible

Version: 1.1
Scope: internal demo and project-owned production art
Status: approved direction; technical values remain profile-driven

## Authority and scope

Use this document as the visual source of truth for art created by the project team and its agents.
It does not constrain games authored by end users of Creature Game Maker. Product, legal, schema,
and asset-pipeline contracts still win in their own domains.

Resolve a conflict in this order:

1. `AGENTS.md` and current scope/specification documents.
2. An explicitly approved asset brief or approved family entry in `docs/art/VISUAL_MEMORY.md`.
3. This art bible.
4. Gameplay readability.
5. Production usability.
6. Novelty.

Read `docs/art/VISUAL_MEMORY.md` for current technical profiles and approved continuity decisions,
`docs/art/IMAGE_GENERATION_GUIDE.md` for output-readiness language, and
`docs/art/VALIDATION_CHECKLISTS.md` before approving an asset.

## Vision

Create an original monster-collecting RPG world with the readability and charm of late handheld
pixel-art RPGs, executed with cleaner modern control and a subtle nature-integrated futuristic
language. The world should feel warm, adventurous, hopeful, cozy, and slightly mysterious.

This is not an attempt to recreate Pokemon artwork. Never copy or closely reconstruct protected
characters, sprites, facilities, symbols, logos, maps, or interface designs. Do not request exact
imitation of a living artist.

## Visual priorities

Apply these priorities in order:

1. Readability at native scale.
2. Strong, distinct silhouettes.
3. Consistent proportions, perspective, and lighting.
4. Gameplay function and collision readability.
5. Controlled palette and visual density.
6. Animation-friendly construction.
7. Production consistency.

Prefer the simpler design when added detail does not improve identity or function.

## Shared visual language

- Use crisp, deliberately placed pixels for production sprites.
- For production sprite cells, use no anti-aliased edges, soft brushes, blur, or gradients.
- Use upper-left key lighting and lower-right cast/form shadows unless an approved exception says
  otherwise.
- Use two to four readable value groups. Avoid pillow shading.
- Use dark selective outlines, lighter or broken toward the light where readability permits.
- Favor saturated midtones, warm highlights, and slightly cooler shadows.
- Use clean geometric accents, living metals, energy veins, crystals, botanical circuitry, and
  artificial ecosystems to integrate technology with nature.
- Keep silhouettes readable without glow, gradients, or surface noise.
- Use a slightly elevated three-quarter view for overworld assets unless a specialist brief defines
  another approved view.

Avoid cyberpunk neon clutter, military-industrial science fiction, chrome robots, modern firearms,
photorealism, soft brushes, accidental anti-aliasing, uncontrolled gradients, bloom, random
high-frequency texture, inconsistent lighting, and screenshot-like presentations of reusable assets.

## Palette and value

Treat palette counts in `docs/art/VISUAL_MEMORY.md` as targets for the named asset profile, not as
engine limits and not as facts an image model can guarantee. Reuse approved neutrals and family
colors. Reserve the highest contrast for focal interaction points, faces, entrances, attack contacts,
and current UI selection. Validate exact colors from the file before claiming compliance.

Regional mood palettes may vary. Autumn is one regional option, never the universal palette.

## Environments and tiles

- Build maps from reusable, grid-aligned parts rather than painted scenes.
- Keep terrain boundaries, elevation, water edges, paths, encounter surfaces, and collision shapes
  legible during play.
- Use controlled variants that preserve seams and texel density.
- Keep large surfaces quiet; add variation through authored clusters, not random pixels.
- Distinguish a concept scene, map mockup, visual asset sheet, modular tileset, and true autotile set.
  Never substitute a mockup for requested reusable tiles.

## Buildings and interiors

- Make entrances obvious, footprints predictable, and roof/wall systems modular.
- Give regions recognizable material, roof, window, sign, lamp, and foundation families.
- Integrate futuristic elements as useful civic or ecological technology, not as a separate sci-fi
  skin.
- Preserve exterior/interior correspondence: footprint, doors, windows, function, and landmark cues.
- Do not recreate recognizable facilities from existing monster-collecting franchises.

## Creatures

- Build each creature around one dominant feature, one secondary feature, and one memorable accent.
- Relate anatomy to habitat, behavior, abilities, and gameplay role.
- Keep anatomy stable, poseable, and feasible at the target sprite scale.
- Carry structural motifs through an evolution family without merely enlarging the same design.
- Avoid ordinary-animal-plus-symbol designs, direct analogues, unrelated motif piles, or identity
  that depends on glow and gradients.

Shape tendencies: rounded forms read friendly; angular forms read aggressive; heavy masses read
ancient; long curves read elegant; clean geometry reads technological; controlled asymmetry reads
mystical. Treat these as tools, not formulas.

## Humans and portraits

- Make role, age, temperament, and region readable through silhouette, posture, clothing blocks, and
  one or two purposeful props.
- Preserve anatomy, costume construction, palette, handedness, and directional asymmetry across
  views and frames.
- Let portraits add expression and material detail without contradicting the sprite design.
- Keep trainers original; do not borrow recognizable outfits or class silhouettes.

## Animation

- Establish the approved base design and key poses before requesting frame groups.
- Prioritize anticipation, action/contact, recovery, grounding, and readable silhouette change.
- Keep ground contact, center of mass, scale, light direction, and anchors stable across frames.
- Use the fewest frames that communicate the action cleanly; timing belongs in the animation brief,
  not in frame count alone.
- Generate focused actions separately. Assemble and validate final sheets after cleanup.

## UI, icons, and effects

- Build UI from reusable nine-slice panels, bitmap-compatible typography, clear focus/selection/
  disabled states, and controller/keyboard-readable hierarchy.
- Keep technology motifs restrained and consistent with the world rather than generic sci-fi chrome.
- Make icons identifiable by silhouette and value grouping before interior detail.
- Keep VFX contact points and gameplay area clear. Use limited particles, clean geometry, and a short
  visual hierarchy: anticipation, contact, dissipation.
- Never let decorative effects obscure actors, HP/resource information, or selection state.

## Production truth

Classify every output as one of:

- **Concept-ready:** useful for direction decisions; not an implementation asset.
- **Cleanup-ready source:** useful input for pixel-art cleanup and reconstruction.
- **Production candidate:** intended dimensions/layout are present but file validation remains.
- **Verified production-ready:** the actual file passed the required technical and visual checks.

Do not call generated imagery production-ready from the prompt alone. Exact dimensions, palette,
alpha, seams, frame registration, and sheet layout require file-level inspection and often manual
pixel-art cleanup.

## Canon and continuity

Experimental generations create no canon. Record a visual decision in
`docs/art/VISUAL_MEMORY.md` only when the user approves it, a project contract establishes it, an
approved production asset establishes a reusable convention, or the request explicitly asks to
formalize it. Record provenance and scope with every decision.

## Approval test

Before approving an asset, verify that it:

- fits this world and remains original;
- reads at native scale and in gameplay context;
- follows the approved profile, perspective, lighting, palette family, and asset-family language;
- is modular and reusable when requested;
- has an honest readiness status; and
- passes the relevant file-level checks before receiving production approval.
