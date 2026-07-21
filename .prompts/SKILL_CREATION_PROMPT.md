You are responsible for designing and implementing a complete, production-grade art-direction skill system for this project.

The purpose of this system is to help Codex and GPT-5.6 consistently design, generate, review, organize, and maintain visual assets for an original monster-collecting RPG.

The project uses a pixel-art visual identity inspired by the readability and charm of late Game Boy Advance and Nintendo DS-era RPGs, enhanced with cleaner modern execution and a subtle nature-integrated futuristic aesthetic.

This system must be robust enough to guide the creation of:

- Overworld tilesets
- Terrain
- Towns
- Buildings
- Props
- Vegetation
- Creatures
- Creature animations
- NPCs
- Trainers
- Portraits
- Items
- Inventory icons
- UI
- VFX
- Battle effects
- Interior environments
- Sprite sheets
- Concept exploration
- Production-ready game assets

Do not immediately create a single oversized generic skill.

First inspect the repository, understand its existing conventions, and then implement a modular skill architecture with a primary Art Director skill and specialized supporting skills.

# Mandatory Repository Review

Before creating or modifying files:

1. Locate and read the root `AGENTS.md`.
2. Locate and read all nested `AGENTS.md` files relevant to the directories you may modify.
3. Read all existing project documentation related to:
   - Art direction
   - World design
   - Creatures
   - Environments
   - Animation
   - UI
   - Asset generation
   - Asset pipelines
   - Naming conventions
   - Repository structure

4. Locate the existing `ART_BIBLE.md`, if present.
5. Locate any existing skills, agent definitions, prompt libraries, templates, or asset-generation workflows.
6. Identify the repository’s established skill format and directory conventions.
7. Follow all Ponytail standards and any other standards, plugins, linters, schemas, or project documentation already present in the repository.
8. Do not assume the proposed paths in this prompt are correct if the repository already defines a different canonical structure.
9. Do not overwrite established documents unless an update is clearly required.
10. Preserve useful existing content and integrate it into the new system.

Repository documentation and `AGENTS.md` instructions take precedence over this prompt whenever they conflict.

# Primary Goal

Create a modular art-direction system centered around a primary Art Director skill.

The Art Director must act as the orchestrator and guardian of the project’s visual identity.

It should:

- Interpret visual asset requests.
- Read the appropriate project art documentation.
- Classify the requested asset.
- Select the appropriate specialist skill or skills.
- Resolve technical asset requirements.
- Produce a strong image-generation brief or prompt.
- Prevent style drift.
- Validate outputs against project standards.
- Distinguish concept art from production assets.
- Preserve visual continuity across the project.
- Update registries or decision logs when appropriate.
- Avoid direct imitation of copyrighted characters, artwork, logos, or proprietary visual assets.

The Art Director should not duplicate all specialist guidance inside itself. It should reference and coordinate focused specialist skills.

# Required Architecture

Create the best architecture for the repository’s existing conventions.

At minimum, the system should include equivalents of the following roles.

## Main Skill

### Art Director

Responsibilities:

- Own the overall visual identity.
- Read and enforce the art bible.
- Route requests to specialist skills.
- Determine whether the user needs concept art, a production asset, a sprite sheet, a tileset, a reference sheet, an animation, or a review.
- Select the correct prompt template.
- Enforce technical requirements.
- Run consistency and production-readiness validation.
- Require original designs.
- Record durable art-direction decisions when appropriate.
- Prevent specialist skills from contradicting the shared art direction.
- Resolve conflicts by prioritizing:
  1. Project documentation
  2. Existing approved assets and registries
  3. Art-bible consistency
  4. Gameplay readability
  5. Production usability
  6. Novelty

## Specialist Skills

Create focused specialist skills for at least:

1. Environment and Tileset Artist
2. Building and Architecture Artist
3. Creature Designer
4. Creature Sprite Artist
5. NPC and Trainer Artist
6. Character Portrait Artist
7. Animation Artist
8. UI and HUD Artist
9. Icon and Item Artist
10. Visual Effects Artist
11. Interior Environment Artist
12. Asset Review and Consistency Auditor

Combine roles only when doing so clearly improves maintainability. Do not collapse unrelated disciplines merely to reduce file count.

# Shared Visual Direction

All skills must consistently enforce the established visual identity.

Use the existing `ART_BIBLE.md` as the primary source of truth. Improve it only when necessary and only after inspecting its current content.

The intended visual identity is:

- Original monster-collecting RPG
- Pixel-art presentation
- Strong GBA/DS-era readability
- Slightly more polished and futuristic than historical handheld sprites
- Nature and advanced technology integrated harmoniously
- Warm, adventurous, hopeful, cozy, and slightly mysterious
- Strong silhouettes
- Controlled palettes
- Crisp pixel edges
- Clear upper-left lighting
- Minimal visual noise
- Gameplay readability over realism
- Production consistency over novelty

Avoid:

- Direct Pokémon imitation
- Existing Pokémon designs
- Existing Pokémon architecture
- Existing Pokémon symbols or logos
- Cyberpunk styling
- Industrial military science fiction
- Photorealism
- Painterly rendering for production sprites
- Soft brushes
- Uncontrolled gradients
- Anti-aliased sprite edges
- Excessive bloom
- Excessive particle clutter
- Random high-detail noise
- Inconsistent lighting
- Inconsistent perspective
- Assets presented as screenshots when reusable assets were requested

# Important Technical Principle

The skill system must be honest about image-generation limitations.

Image-generation models may create strong visual concepts and useful source artwork, but they do not reliably guarantee:

- Exact pixel dimensions
- Exact frame dimensions
- Perfect tile seams
- Identical character anatomy across every frame
- Correct alpha transparency
- Strict palette counts
- Engine-ready sprite-sheet alignment
- Perfectly repeatable assets
- Exact animation registration

Therefore, the skills must distinguish between:

1. Concept-ready output
2. Cleanup-ready source output
3. Production-candidate output
4. Verified production-ready output

Do not describe an image as production-ready merely because the prompt requested production-ready artwork.

The skills should require validation and, where necessary, cleanup in appropriate pixel-art tooling before an asset is considered verified for implementation.

# Skill Design Requirements

Each skill should be concise enough to load effectively but complete enough to guide reliable execution.

Every specialist skill should define:

- Purpose
- Scope
- Responsibilities
- Required project documents to read
- Inputs it expects
- Questions it must resolve internally
- Technical defaults
- Visual rules
- Workflow
- Output format
- Validation checklist
- Failure conditions
- Escalation rules
- Relationships to other skills
- Examples of appropriate requests
- Examples of inappropriate requests
- Common model failure modes
- How to revise a failed result

Do not fill files with vague statements such as “make it look good” or “ensure quality.”

Use concrete, testable instructions.

# Art Director Routing Logic

The Art Director should classify requests before execution.

Create clear routing rules such as:

- Terrain, paths, cliffs, water, vegetation, outdoor props:
  Environment and Tileset Artist
- Houses, stores, town halls, laboratories, architectural kits:
  Building and Architecture Artist
- New monster concepts, evolution families, anatomy, motifs:
  Creature Designer
- Battle sprites, overworld creature sprites, pose sheets:
  Creature Sprite Artist
- Humans, trainers, merchants, researchers, town residents:
  NPC and Trainer Artist
- Dialogue portraits, busts, expressions:
  Character Portrait Artist
- Walk cycles, idle loops, attacks, transitions:
  Animation Artist
- Menus, HUDs, panels, interface components:
  UI and HUD Artist
- Inventory objects, collectible icons, skill icons:
  Icon and Item Artist
- Energy, magic, weather, impact, battle effects:
  Visual Effects Artist
- Indoor rooms, walls, floors, furniture, interior kits:
  Interior Environment Artist
- Review of generated or manually produced assets:
  Asset Review and Consistency Auditor

Requests may route to multiple skills.

For example:

- An animated creature battle sheet requires Creature Designer, Creature Sprite Artist, and Animation Artist.
- A town asset pack requires Environment, Building, and Interior skills.
- A futuristic healing center requires Building, UI, Interior, and VFX guidance.

# Art Documentation

Inspect the existing documentation and create or improve supporting documents only where useful.

Potential documents include equivalents of:

- `ART_BIBLE.md`
- `VISUAL_LANGUAGE.md`
- `COLOR_PALETTES.md`
- `ENVIRONMENT_STYLE.md`
- `BUILDING_STYLE.md`
- `CREATURE_STYLE.md`
- `NPC_STYLE.md`
- `UI_STYLE.md`
- `ANIMATION_GUIDE.md`
- `IMAGE_GENERATION_GUIDE.md`
- `ASSET_REGISTRY.md`
- `TILESET_REGISTRY.md`
- `STYLE_DECISIONS.md`
- `ASSET_NAMING.md`
- `EXPORT_AND_IMPORT_GUIDE.md`

Do not create empty documentation merely to satisfy a directory layout.

Each document must have a clear purpose and must not unnecessarily duplicate other documents.

The Art Director and specialist skills should reference these documents by their canonical repository-relative paths.

# Visual Memory and Registries

Implement a durable visual-memory system appropriate for the repository.

The system should be able to record established facts such as:

- Standard tile sizes
- Standard sprite sizes
- Camera angle
- Lighting direction
- Outline rules
- Palette families
- Technology motifs
- Regional architectural styles
- Approved tree families
- Approved roof shapes
- Approved lamp designs
- Creature anatomy conventions
- UI component conventions
- Animation frame standards
- Asset naming patterns
- Export rules
- Known exceptions

Do not place rapidly changing implementation status inside the art bible.

Use registries or decision logs for evolving production information.

Define when the Art Director is allowed to update those records.

It must not silently create new canon from an experimental generation.

A visual decision should be recorded only when:

- The user explicitly approves it.
- An existing project document establishes it.
- An approved production asset establishes a reusable convention.
- The request explicitly asks to formalize the decision.

# Prompt Templates

Create reusable prompt templates for at least:

- Environment tileset
- Terrain autotile set
- Nature asset sheet
- Town prop sheet
- Building asset sheet
- Modular architecture kit
- Creature concept sheet
- Creature battle sprite
- Creature overworld sprite
- NPC sprite sheet
- Character portrait
- Item icon sheet
- UI component sheet
- VFX sheet
- Animation sheet
- Asset revision
- Style-matching continuation
- Asset review

Templates should use explicit fields or sections such as:

- Role
- Asset purpose
- Game context
- Required source documents
- Visual direction
- Asset list
- Canvas or nominal sprite dimensions
- Grid layout
- Perspective
- Lighting
- Palette rules
- Transparency requirements
- Modularity requirements
- Animation requirements
- Negative constraints
- Consistency references
- Expected output
- Validation caveats

Do not hardcode every prompt to autumn. Autumn should be a region or request-specific theme, not the universal project palette.

# Environment and Tileset Requirements

The Environment and Tileset skill must understand:

- Seamless tiles
- Autotile families
- Edge tiles
- Inner corners
- Outer corners
- T-junctions
- Crossroads
- Endcaps
- Terrain transitions
- Elevation transitions
- Cliffs
- Water boundaries
- Overlay tiles
- Underlay tiles
- Collision-readable design
- Tall-grass encounter tiles
- Decorative variants
- Tile variation without seam-breaking
- Reusable props
- Grid alignment
- Map-editor usability

It must distinguish:

- A visual asset sheet
- A true modular tileset
- An autotile-compatible set
- A map mockup
- A concept scene

It should never accept a map mockup as a substitute for a reusable tileset.

# Building Skill Requirements

The Building and Architecture skill must understand:

- Modular facades
- Roof systems
- Wall materials
- Doors and entrances
- Windows
- Foundations
- Signage
- Chimneys
- Add-on modules
- Regional architectural identity
- Readable entrances
- Collision footprint
- Tile-grid footprint
- Exterior and interior correspondence
- Subtle futuristic integration
- Landmark versus generic building design

It must avoid recognizable recreations of existing Pokémon facilities.

# Creature Design Requirements

The Creature Designer skill must enforce:

- Originality
- Strong silhouette
- One dominant feature
- One secondary feature
- One memorable accent
- Clear personality
- Animation-friendly anatomy
- Evolution-family continuity
- Gameplay-role readability
- Controlled visual complexity
- Scale-appropriate details
- A meaningful relationship between anatomy, habitat, behavior, and abilities

It should discourage designs that are merely:

- An ordinary animal plus a decorative symbol
- A direct Pokémon analogue
- Overloaded with unrelated motifs
- Too intricate to animate
- Dependent on gradients or glow to remain readable
- Anatomically inconsistent across views

# Sprite Requirements

Sprite-focused skills must understand the distinction between:

- Battle sprite
- Back sprite
- Front sprite
- Overworld sprite
- Portrait
- Icon
- Animation frame
- Reference turnaround

They should specify nominal dimensions while acknowledging that generated images require pixel-level validation.

They should also define:

- Baseline
- Center of mass
- Ground contact
- Pose readability
- Sprite occupancy
- Outline consistency
- Light direction
- Frame-to-frame registration
- Mirroring rules
- Directional asymmetry
- Transparent padding
- Anchor points

# Animation Requirements

The Animation Artist skill must define:

- Animation purpose
- Frame count target
- Frame order
- Timing guidance
- Loop behavior
- Anticipation
- Action
- Recovery
- Contact frames
- Grounding
- Registration
- Secondary motion
- Silhouette changes
- Directional consistency

It must avoid asking an image model for too many unrelated animations in one generation.

It should favor:

1. Approved base design
2. Approved key poses
3. Small focused frame groups
4. Cleanup and alignment
5. Final sheet assembly

# UI Requirements

The UI skill must understand:

- Pixel-font compatibility
- Readability at native scale
- Nine-slice panels
- Button states
- Selection states
- Disabled states
- Focus states
- Icon readability
- HUD hierarchy
- Controller and keyboard navigation
- Color accessibility
- Information density
- Futuristic motifs consistent with the world
- Avoiding overdesigned sci-fi interfaces

# Asset Review Skill

The Asset Review and Consistency Auditor must be able to review:

- A generated image
- A sprite sheet
- A tileset
- A building
- A creature design
- A UI sheet
- An animation
- A prompt before generation

It should report findings in clear categories:

- Blocking issues
- Significant inconsistencies
- Minor polish issues
- Production risks
- Recommended corrections
- Approval status

Possible statuses:

- Concept approved
- Direction approved
- Cleanup required
- Production candidate
- Production validation required
- Rejected for style mismatch
- Rejected for technical failure

The auditor should never approve exact tileability, transparency, dimensions, or palette limits without inspecting the actual file using suitable technical validation.

# Validation Checklists

Create reusable checklists for at least:

## Art-Direction Consistency

- Matches the art bible
- Fits the world
- Uses approved motifs
- Uses correct lighting
- Uses correct perspective
- Uses appropriate palette
- Avoids style drift
- Maintains originality

## Pixel-Art Quality

- Crisp pixel edges
- No accidental anti-aliasing
- No unintended gradients
- Controlled palette
- Consistent outlines
- Readable silhouette
- No excessive noise
- Works at native scale

## Tileset Readiness

- Correct nominal grid
- Complete edge and corner cases
- Seamless tiling
- Transition compatibility
- Consistent texel density
- Collision-readable shapes
- No baked-in scene lighting that prevents reuse
- No overlapping assets
- Transparent padding where required

## Sprite-Sheet Readiness

- Correct frame order
- Consistent character scale
- Stable anatomy
- Stable palette
- Stable lighting
- Aligned ground contact
- Consistent anchor point
- No frame clipping
- Clear frame spacing
- Transparent background
- Export validation still required

## Legal and Originality

- No copyrighted characters
- No copied sprites
- No copied logos
- No trademarked symbols
- No recognizable facility recreation
- No direct tracing
- No style instruction that requires exact imitation of a living artist

# Workflow

Implement this work incrementally.

## Phase 1: Discovery

- Inspect repository instructions and documentation.
- Identify existing skill conventions.
- Identify existing art documentation.
- Identify conflicts, duplication, and missing guidance.
- Produce a concise implementation plan in the appropriate project planning file or working notes, following repository conventions.

Do not stop after planning.

## Phase 2: Architecture

- Choose canonical directories and naming.
- Define the main skill and specialist boundaries.
- Define shared references.
- Define prompt-template organization.
- Define checklist organization.
- Define registries and decision logs.
- Avoid circular references.

## Phase 3: Main Art Director

- Create the primary Art Director skill.
- Make its routing logic explicit.
- Make its document-loading behavior explicit.
- Make its output behavior explicit.
- Make its validation responsibilities explicit.
- Keep it orchestration-focused.

## Phase 4: Specialist Skills

Create each specialist skill one by one.

After each skill:

1. Check it against the Art Director.
2. Remove duplicated generic guidance.
3. Add concrete discipline-specific rules.
4. Verify references resolve.
5. Confirm it has actionable workflows and validation.
6. Confirm it does not contradict repository documentation.

## Phase 5: Templates and Checklists

- Create reusable prompt templates.
- Create shared validation checklists.
- Ensure specialists reference them appropriately.
- Avoid forcing every discipline into one universal template.

## Phase 6: Documentation Integration

- Update the art documentation where required.
- Add links between the art bible, specialist guides, skills, templates, and registries.
- Preserve a single authoritative location for each rule.
- Avoid copying identical rules into many files.

## Phase 7: Testing

Test the skill system with realistic example requests, including:

1. “Create an autumn town terrain tileset with grass, paths, tall grass, cliffs, and water.”
2. “Design an original three-stage creature family based on a gliding forest mammal and bioelectric seed pods.”
3. “Create an overworld walk cycle for an approved NPC design.”
4. “Create a subtle futuristic research laboratory for a rural town.”
5. “Create a battle VFX sheet for a plant-electric attack.”
6. “Review this generated tileset for production readiness.”
7. “Create a menu panel kit for creature inventory management.”
8. “Continue an existing building family without changing its architectural language.”

For each test, verify that:

- The correct specialist skills are selected.
- The correct documents are referenced.
- The resulting prompt is focused.
- Technical caveats are accurate.
- The request does not drift from the project style.
- The output is not bloated with irrelevant instructions.

Do not call image generation merely to test prompt routing unless repository tooling and the current task explicitly support it. Prompt-level dry runs are acceptable.

## Phase 8: Final Audit

Before finishing:

- Verify every referenced path exists.
- Verify there are no broken links.
- Verify naming is consistent.
- Verify the main Art Director is not overloaded.
- Verify specialist responsibilities do not conflict.
- Verify each skill has a clear activation condition.
- Verify prompts are usable by GPT-5.6 image generation.
- Verify the system follows all `AGENTS.md` instructions.
- Verify Ponytail standards are satisfied.
- Run any available documentation, schema, formatting, or repository validation tools.
- Review the complete diff.
- Remove placeholder text that should not remain.
- Remove redundant documentation.
- Ensure all new files are committed-ready.

# Output Behavior

Perform the implementation directly in the repository.

Do not merely describe what the files should contain.

Do not stop after producing an outline.

Do not create all files blindly before inspecting the repository.

Work through the system one component at a time and continuously validate integration.

When complete, provide a concise implementation report containing:

- Architecture created
- Files added
- Files updated
- Specialist skills included
- Prompt templates included
- Validation checklists included
- Existing documentation preserved or integrated
- Tests or dry runs performed
- Validation commands run
- Remaining limitations or follow-up work

Do not claim exact production readiness for generated visual assets without file-level validation.

# Quality Bar

This skill system should feel like a maintainable internal art-production framework, not a collection of generic image prompts.

It should give future Codex sessions enough structure to reliably answer questions such as:

- What art documents must be read?
- Which specialist owns this request?
- Is this concept art or a production asset?
- What technical requirements apply?
- What prompt format should be used?
- What must be validated after generation?
- What visual decisions are already established?
- What should be recorded for future continuity?
- What would constitute a style or production failure?

Favor explicit rules, small focused files, clear references, and repository-native organization.

Begin by inspecting the repository and all applicable instructions.
