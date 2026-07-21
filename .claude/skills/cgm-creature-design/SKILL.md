---
name: cgm-creature-design
description: Creates and evaluates entirely original, game-ready monster sprites in the project's cohesive late-2000s handheld monster-RPG pixel-art aesthetic. Use whenever designing, generating, revising, converting, or reviewing creature sprites, battle sprites, sprite sheets, developmental stages, forms, or variants. Governs art style, appeal, production consistency, sheet layout, and export standards.
---

# Creature Design and Sprite Art Direction

## Purpose

This skill defines the shared visual language for every monster in the game, so
that radically different creatures still read as one game, one universe, one
hardware era, and one production team.

It controls pixel construction, appeal, silhouette readability, palette
discipline, outline behavior, shading, staging, cross-view consistency, sheet
layout, export standards, and originality.

It does not prescribe any specific body plan, appendage, palette, or species
trait. No anatomical feature is assumed unless chosen for the current creature.

---

# Required Documents and Authority

Read before producing or reviewing art:

* `docs/ART_BIBLE.md` — qualitative visual direction.
* `docs/art/VISUAL_MEMORY.md` — approved technical values, asset profiles,
  palette ceilings, and the durable record of what the roster already contains.

This skill governs **style, appeal, and production method**. It does not
override approved technical values. Resolve any numeric or technical conflict in
`VISUAL_MEMORY.md`'s stated order:

1. an explicitly approved asset brief
2. an approved asset-family or exception entry
3. a named asset profile
4. `ART_BIBLE.md` qualitative direction
5. ask the user when the missing value affects layout, compatibility, or canon

Where this skill states a number it is quoting the current `internal-demo-v1`
profile for convenience. If that profile changes, `VISUAL_MEMORY.md` wins and
this skill is stale.

---

# The Two Requirements

Every creature must satisfy both. Neither may be sacrificed for the other.

1. **Appeal** — it must be a creature a player wants to own, at thumbnail size,
   with no explanation.
2. **Originality** — it must not reproduce or closely resemble any existing
   copyrighted creature.

When these appear to conflict, appeal is the higher-ranked requirement and
originality is achieved by changing *which* familiar foundation is used and how
its parts are integrated — never by retreating into abstraction.

## Appeal (binding, and checked first)

A design has appeal when it is:

* immediately readable as a living thing with intent
* charming, striking, cool, or endearing on first sight
* built around one clear idea a player could describe in a short phrase
* possessed of a clear focal point, usually a face or an equivalent sensory
  structure
* proportioned with deliberate contrast (a large form against a small one)
* the kind of thing a player would pick as a starter

Appeal failures are as serious as originality failures. Reject a design that is:

* a shapeless lump, blob, or mass with features scattered across it
* an assembly of geometry with no evident front, focus, or intent
* readable only after the concept is explained in words
* strange without being likeable
* so unfamiliar that a player cannot tell what it is or how it lives

**Cute is a legitimate and valued target.** So are fierce, elegant, comical,
sinister, and noble. None of these is a failure mode.

---

# Familiar Foundations Are Correct

Successful monster rosters are overwhelmingly built on recognisable animal,
plant, and object foundations. A creature that reads as "a lizard, but —" or
"a moth, but —" is doing the right thing, not a lazy thing.

What is forbidden is the **unintegrated emblem**: an ordinary animal with a
symbol stuck on it, where the motif does not change the anatomy. The fix is to
integrate the motif into structure, material, locomotion, and behavior — not to
discard the animal foundation.

Compare:

* **Wrong** — a normal wolf with a flame decal on its shoulder.
* **Right** — a wolf whose coat has become slow-burning ember-fur, whose ribs
  vent heat, and whose gait is built for short explosive charges.
* **Also wrong** — an amorphous heat-mass with no discernible front, on the
  grounds that a wolf would be too familiar.

## Roster Distribution Target

Across the full roster, aim for approximately:

* **70% recognisably derived** from real animals, plants, or familiar objects,
  transformed by their concept.
* **30% exotic** — abstract, elemental, mineral, spectral, modular, colony-like,
  or anatomically unfamiliar.

The exotic minority is seasoning that makes the roster feel wide. It is not the
target. A roster that is mostly exotic reads as incoherent, not imaginative.

---

# Diversity Without Drift

Vary creature architecture while preserving the rendering language.

Before designing, compare the concept against recent creatures on body plan,
limb count, locomotion, dominant silhouette, sensory structure, surface
material, posture, and symmetry. When several recent creatures share a trait,
reduce the chance of reusing it.

**Bounded, not ratcheting.** Diversity is measured against the roster's overall
balance, never against "whatever was made last." A new creature does not have to
differ from its predecessor. It has to keep the roster's distribution near the
70/30 target. If recent designs have drifted exotic, the correcting move is a
*more familiar* creature — pushing further into strangeness is the failure this
rule exists to prevent.

**Where the roster lives.** Judge balance against the approved entries in
`docs/art/VISUAL_MEMORY.md`, not against memory of the current conversation. If
that file records no creature entries yet, the roster is unestablished: build the
early roster from familiar foundations and defer exotic designs until there is a
recognisable core to contrast against.

Never change the established art style to create variety.

---

# No Assumed Anatomy

Choose the body plan deliberately rather than inheriting it by habit. The agent
decides whether the creature has limbs, how many, whether it has a conventional
head or face, how many eyes, whether it is symmetrical, how it moves, and what
it is made of.

Available body organisations include bipedal, quadrupedal, hexapedal,
many-legged, limbless, serpentine, radial, segmented, floating, rooted,
shell-bodied, insectoid, crustacean, molluscan, piscine, avian, botanical,
fungal, mineral, spectral, mechanical, and mixed-material.

These are options, not a checklist. Choose one coherent body plan and develop it
clearly. Do not combine categories to manufacture novelty.

Optional features — wings, horns, tails, ears, paws, claws, fangs, crests,
spikes, markings — may be used freely when they strengthen the design. They are
not part of the global style, and their absence is equally valid. Include them
because the concept calls for them, not from habit and not from avoidance.

Creative liberty must produce meaningful variation, not random complexity. Every
design decision should support silhouette, function, habitat, movement,
personality, elemental identity, battle role, or developmental relationship.

---

# Controlled Design Method

1. Choose one foundational body concept.
2. Choose one movement or behavioral concept.
3. Choose one visual motif.
4. Choose one material language.
5. Add only the anatomy needed to express those choices.
6. Remove anything that does not contribute.
7. Simplify for sprite readability.
8. Verify appeal at thumbnail size before proceeding.

Do not solve originality by attaching more parts. A strong creature feels
surprising but inevitable once seen.

---

# Universal Style Pillars

## 1. Strong Silhouette

Every monster must be recognisable from its outer shape alone, at thumbnail
size. Prioritise a clear primary mass, readable secondary masses, intentional
negative space, and one dominant silhouette idea.

Avoid tangled forms, excessive protrusions, decorative clutter, accidentally
merged body regions, and features too small to affect recognition.

## 2. Species-Appropriate Proportions

Use stylised proportions suited to the creature — compact, elongated, squat,
top-heavy, bottom-heavy, radial, segmented, delicate, or massive.

Do not force every monster into one template. Deliberate proportional contrast
is a primary source of appeal. Expressiveness comes from posture, shape, eye
placement, body angle, silhouette, and colour contrast.

## 3. Sprite-First Design

Design for the final sprite resolution from the beginning. Every major feature
must survive at display size.

Retain details that communicate species identity, anatomy, behavior, material,
role, movement, personality, or developmental stage. Remove details that only
create noise: tiny ornaments, miniature patterns, individual strands, engraving,
subtle gradients, and low-contrast differences.

## 4. Hand-Placed Pixel Construction

The artwork must appear deliberately constructed pixel by pixel: crisp
boundaries, deliberate clusters, controlled stair-stepping, clear material
separation, compact highlights, clustered shadows.

Avoid automatic pixelation filters, noisy dithering, random isolated pixels,
inconsistent pixel density, partially smoothed contours, and resampling
artifacts. Pixel clusters describe form and material; they do not imitate
texture indiscriminately.

## 5. Limited Palette

Use a compact, vibrant palette designed for the current monster: one outline
family, one dominant material ramp, one or two supporting ramps, limited
accents.

Each major material uses a two-to-four-step ramp — shadow, base, light, optional
highlight. Not every material needs all four. Colours must stay distinguishable
at sprite scale. Do not repeat the same dominant colours across the roster
without a design reason.

**Ceiling: 16 colours per creature sprite** (`VISUAL_MEMORY.md` palette target),
counted from the actual file, with transparency reported separately. This is a
production cleanup target — an image model cannot certify the count, so verify it
on the file rather than asserting it from the prompt.

## 6. Compact Cel-Style Shading

Use simplified cel lighting translated into pixel clusters. Shading clarifies
volume, separates overlapping regions, explains material, and reinforces pose.

Light comes from the **upper left**, with form shadow falling to the lower right
(`VISUAL_MEMORY.md`). Self-shadowing on the creature's own body is expected and
required — it is what makes the form read.

This is distinct from a **floor or drop shadow beneath the creature**, which is
forbidden: the sprite must isolate cleanly, and a detached ground shadow becomes
a stray opaque blob once the background is keyed out.

Do not use airbrushing, global illumination, smooth gradients, painterly
blending, glossy 3D rendering, excessive rim light, bloom, or volumetrics.

## 7. Controlled Outlines

Use crisp, generally one-pixel outlines at native scale: dark enough to separate
forms, consistent in weight, clean around the silhouette, selectively adapted to
local colour. Pure black sparingly; dark coloured outlines often harmonise
better.

Avoid thick uniform borders, inconsistent line weight, and unnecessary internal
contour lines.

## 8. Expression and Focal Systems

A monster must communicate temperament clearly. A conventional face is the most
reliable way to achieve this and should be the default choice unless the concept
genuinely argues otherwise.

When a conventional face exists, keep it readable and economical. When it does
not, establish another unmistakable focal system — a sensory cluster, a core, an
aperture — positioned and weighted so the viewer's eye lands on it first. A
creature with no focal point has failed the appeal requirement.

Expression may also come from posture, body compression or expansion,
orientation, limb position, glow intensity, surface pattern, silhouette tension,
and asymmetry.

---

# Elemental Design Restraint

Elemental identity need not be a literal symbol. An electric creature does not
inherently need lightning marks; a water creature does not inherently need fins.

Identity may instead be conveyed through movement, material, palette, anatomy,
behavior, silhouette, attack posture, habitat adaptation, or energy flow.

Use literal motifs when they strengthen the design — restraint here means
integration, not avoidance.

---

# Battle Sprite Requirements

A standard battle pair is:

1. one front-facing three-quarter battle sprite
2. one back-facing three-quarter battle sprite

These labels describe camera orientation, not required anatomy. Both views must
depict the exact same design.

The **front view** presents the creature clearly, communicates battle behavior,
shows its primary visual idea, and makes orientation understandable.

The **back view** reconstructs the same body from the opposing viewpoint,
preserving structural logic, proportions, palette, and lighting, revealing
hidden surfaces consistently without inventing new structures. It must not be a
mirrored front view.

## Cross-View Consistency

The two views must agree on every feature that exists: body regions,
proportions, appendage count and placement, segmentation, surface pattern,
material, openings, sensory organs, palette, outline behavior, lighting
direction, sprite scale, and apparent developmental stage.

Check only features the creature actually possesses. Do not expect a tail when
none was designed, eyes when sensing works another way, or limbs when movement
is fluid, rolling, floating, rooted, or serpentine.

## Structural Connection Discipline

Every structure must connect or relate coherently. Functional structures emerge
from believable regions; segmented pieces align; armor plates overlap logically;
the center of mass supports the pose; locomotion is visually plausible; rear
structures correspond with the front.

A floating component is not automatically an error. It is an error when nothing
indicates the separation is intentional.

## Pose

The pose expresses the individual creature — playful, alert, defensive, proud,
aggressive, cautious, graceful, heavy, curious, predatory, serene, dormant,
rooted, drifting, or coiled.

Do not reuse one universal stance. Every pose still requires readable staging,
clear balance, intentional orientation, strong silhouette, and battle-ready
presentation.

---

# Developmental Stages

## Permanent Stages

A permanent stage represents growth, maturation, metamorphosis, or another
lasting change. Use neutral project-owned language: earlier stage, juvenile
stage, intermediate stage, mature stage, next developmental stage, metamorphic
stage, specialised branch form, final mature form.

Species need not share a stage count or process. A later stage should read as a
related member of the same line while carrying enough structural change to
justify its own gameplay identity. Changing scale alone is not a stage.

Preserve inherited anchors across a line: silhouette rhythm, palette
relationship, material language, focal structure, and recurring shape.

## Forms and Variants

A variant may represent environmental, regional, seasonal, sex-linked,
artificial, or elemental variation, or an alternate battle role. A variant is
not automatically stronger than its base.

---

# Stage-Line Visual Planning

When planning a creature line, plan **only what the image generator needs.**
Produce, per stage:

* the one-phrase concept
* body plan and silhouette change from the previous stage
* proportion change
* palette relationship to the previous stage
* which inherited anchors are preserved
* pose and battle-role impression

Do not produce habitat essays, ecology writeups, feeding behavior, social
structure, temperament studies, lore, naming, or stat suggestions. None of it
reaches the image generator, and it dilutes the brief that does.

The planning output is a short visual brief per stage. Nothing more.

---

# Colour-Key Transparency Standard

Unless another export format is requested, use a completely flat background of
`#FF00FF` — RGB `255, 0, 255`. This colour represents transparent space.

* The entire background must be that colour.
* No gradient, floor shadow, decoration, texture, border, or text.
* No foreground pixel may use exact RGB `255, 0, 255`, or any near-magenta that
  could be mistaken for it.

When pink, purple, or magenta matters to the creature, choose a visibly
different value — well separated from the key, not merely distinct.

---

# Sprite Sheet Layout

## The Frame Grid (binding)

Every two-view battle sheet is a **1 row x 2 column grid of identical square
cells**. This is the only layout. The engine's `GridSlicer` consumes a sheet as
`GridSpec{cellW, cellH, offsetX, offsetY, spacingX, spacingY}`; the layout below
is the one that slices with `offset = 0` and `spacing = 0`, which is the only
configuration that needs no manual measurement.

State these values explicitly for every sheet. Never infer sheet size from a
nominal frame, and never hard-code one profile's numbers into a prompt written
for a different approved profile.

| Property | Value | Source |
| --- | --- | --- |
| Cell width (`cellW`) | `FRAME` | profile |
| Cell height (`cellH`) | `FRAME` | profile |
| `FRAME` for `internal-demo-v1` | 64 px | `VISUAL_MEMORY.md` battle sprite nominal frame |
| Rows x columns | 1 x 2 | this skill |
| Sheet size | `2*FRAME` x `FRAME` (128 x 64 at `FRAME = 64`) | derived, stated explicitly |
| Column 0 | front-oriented three-quarter view | this skill |
| Column 1 | rear-oriented three-quarter view | this skill |
| Offset / spacing / margin | zero - cells are flush, no gutters | this skill |
| Baseline | shared, at a stated y within the cell | per family |
| Scaling | integer, nearest-neighbor, no smoothing | `VISUAL_MEMORY.md` |

`FRAME` should stay a multiple of 16 so both sheet axes remain divisible by a
candidate in the slicer's suggestion set {16, 32, 48, 64}. At `FRAME = 64` the
sheet is 128 x 64, divisible by 64 on both axes, which `Suggest()` resolves
without hand-entered geometry.

`FRAME = 64` is the internal-demo default, not an engine limit. An approved brief
or asset-family entry may set another value; when it does, restate every row of
this table for that profile rather than reusing the numbers above.

## Framing Within a Cell

* Each creature is centered horizontally in its own cell.
* Both creatures stand on one shared baseline.
* Leave at least 4 px of key colour between artwork and every cell edge.
* Artwork must never cross a cell boundary.
* Both views occupy the same visual scale.
* No labels, borders, captions, or decoration anywhere on the sheet.

## Generation Resolution — Block Scaling

Asking an image model for a small canvas does not produce pixel art. It produces
a small blurry illustration. Asking for "pixel art" at a large canvas produces a
high-resolution illustration in a pixel-art style — detail at every pixel, no
block structure, unusable as a sprite.

Neither is fixable by prohibition. The skill's existing bans on anti-aliasing and
downscaled-illustration appearance did not prevent either outcome.

**Generate block-scaled instead.** Ask for the native sheet rendered at an
integer multiple, with every art pixel drawn as a crisp square block of one flat
colour:

| Property | Value |
| --- | --- |
| Native sheet | `2*FRAME` x `FRAME` (128 x 64 at `FRAME = 64`) |
| Block scale | 8x (default) |
| Generation canvas | 1024 x 512 |
| Recovery | point-sample downscale by exactly the block scale |

This works because "each art pixel is a crisp 8 x 8 square of one flat colour" is
a *visual* instruction the model can execute, unlike an exact pixel count or an
exact RGB value. The exactness arrives afterward, from an integer downsample that
this project controls.

**What each dial does:**

* `FRAME` controls how much detail the creature can carry. More detail means a
  larger `FRAME`, agreed as a profile change — never a larger block scale.
* **Block scale** controls only how reliably the model renders discrete blocks.
  Raising it gives the model more canvas per art pixel and cleaner block edges.
  It does not add detail. 8x is the default; drop toward 4x only if the model
  produces cleaner work at a smaller canvas.

Downsampling must be point-sampled (nearest-neighbor) at exactly the block scale,
so each source block becomes exactly one pixel. Any other ratio or filter
reintroduces the blending this technique exists to avoid.

## Generation Reality (read this before claiming compliance)

An image model **cannot be relied upon** to emit an exact canvas size, an exact
background RGB, or true one-pixel-per-pixel placement. Requests for exact
`#FF00FF`, exact dimensions, and absent anti-aliasing state the *target*; they do
not guarantee the *result*. Observed failures on real project output include
backgrounds drifting to `#F606EC` and anti-aliased edge blending, both while
this direction was in force.

A generated sheet is therefore **raw input, never a finished asset.** It becomes
grid-aligned only after the project's normalisation step keys out the
background, trims to content, rescales each view to `FRAME`, and composes the
exact `2*FRAME` x `FRAME` canvas.

Never record a sheet as engine-ready on the strength of the prompt alone.

---

# Originality Requirements

Every monster must be independently designed. Do not reproduce, trace, recolor,
or fuse existing copyrighted creatures, imitate an iconic pose closely,
reproduce signature markings, use franchise-specific symbols, or include
protected names or logos.

Genre conventions may be used in original combinations. A familiar animal
foundation is not an originality risk; a familiar *specific creature* is.

When a design feels too close to an existing creature, change foundational
characteristics — body plan, proportion, material, motif — rather than applying
superficial edits, and rather than retreating into abstraction.

---

# Style Versus Creature Separation

**Style instructions** come from this skill: pixel construction, palette
discipline, shading, outlines, readability, staging, layout, transparency,
consistency, originality, appeal.

**Creature instructions** come from the current brief or a fresh creative
decision: body plan, anatomy, materials, palette, locomotion, habitat,
behavior, elemental identity, personality, pose, size, stage.

Never treat a feature from an earlier creature as part of the global style.

---

# Required Workflow

1. **Identify the mode** — new base creature, developmental stage, form or
   variant, revision of an approved asset, or evaluation pass.
2. **Read the brief** and extract explicitly required traits. Do not import
   anatomy from previous creatures.
3. **Establish creative freedom** — identify which decisions remain open.
4. **Choose the foundation** — pick the familiar base or, when the roster
   balance calls for it, an exotic one. Check against the 70/30 target.
5. **Define the core** — one structural, one movement, one behavioral, one
   material concept, and one visual motif that support each other.
6. **Check appeal** — state in one phrase why a player would want this creature.
   If that phrase cannot be written, redesign before continuing.
7. **Check roster balance** against recent designs.
8. **Simplify for sprite scale.**
9. **Establish palette ramps.**
10. **Construct the front view.**
11. **Construct the rear view** as the same creature from behind.
12. **Audit** structure, style, cross-view consistency, originality, appeal, and
    export safety.

---

# Reusable Generation Prompt Template

## How to use this template

Everything numeric and exact — colour values, pixel counts, colour budgets,
anti-aliasing — has been deliberately **removed** from this prompt and moved to
the Quality-Control checklist, where it is verified on the file.

That is not an oversight. An image model cannot honour an exact RGB value or an
exact pixel count, and long lists of prohibitions dilute the instructions it
*can* follow. Prompt for what the model can see; enforce the rest in code.

Keep the sent prompt short, positive, and visual. Resist re-adding negatives.

## The prompt

> Original creature sprite sheet, late-2000s handheld monster-RPG pixel art,
> drawn at large blocky scale.
>
> `[CREATURE BRIEF — one or two sentences: the foundation, the concept, the
> defining feature.]`
>
> Appealing and instantly readable, with a clear focal point and bold
> proportional contrast — a creature a player would want on their team.
>
> Two views side by side, filling the frame edge to edge: front three-quarter
> view on the left, rear three-quarter view on the right. Same creature in both,
> same size, standing on one shared baseline, each centred in its own half.
>
> Every art pixel is a crisp `[BLOCK]` x `[BLOCK]` square block of one flat
> colour, like a sprite magnified `[BLOCK]` times. Hard square edges everywhere,
> bold dark outlines, a small palette of flat colours in simple light-and-shadow
> steps, lit from the upper left.
>
> Flat solid magenta background, nothing else in the scene.
>
> Not smooth, not painted, not 3D. No background scenery, no shadow on the
> ground, no text.

## Before sending

* Substitute `[BLOCK]` with the block scale (8 by default).
* Request the generation canvas at `[2*FRAME * BLOCK]` x `[FRAME * BLOCK]` —
  1024 x 512 for `internal-demo-v1` at 8x — through whatever size control the
  tool provides, not by describing it in the prompt.
* Keep the creature brief to one or two sentences. A long brief crowds out the
  block-structure instruction, which is the part that makes the output usable.
* Send no other constraints. The originality, palette, colour-key, and dimension
  requirements are all verified after generation.

---

# Worked Example — What Success Looks Like

**Bulby** (project roster, approved).

* **Foundation:** a small feline — recognisable, familiar, appealing.
* **Concept in one phrase:** a bold pocket-sized cat that flies on ember wings.
* **Integration:** the wing feathers carry the same warm ramp as the ear tufts
  and tail, so the flight motif belongs to the animal rather than sitting on it.
* **Focal point:** large green eyes, high contrast against the yellow face,
  landing the viewer's eye immediately.
* **Proportional contrast:** big head and ears against a compact body and small
  paws.
* **Palette:** one dominant yellow ramp, one orange supporting ramp, red accent
  rings, dark outline family. Four ramps total.
* **Why it works:** a player can describe it instantly, wants it, and cannot
  point to an existing creature it copies.

This is the target. A creature may be far stranger than Bulby and still succeed,
but it must clear the same bar: readable, likeable, one clear idea, one clear
focal point.

---

# Quality-Control Checklist

## Appeal (check first)

* [ ] The creature is likeable, striking, or cool at thumbnail size.
* [ ] Its concept fits in one short phrase.
* [ ] It has one clear focal point.
* [ ] It reads as a living thing with intent.
* [ ] Proportional contrast is deliberate.
* [ ] It is not a shapeless mass or unfocused assembly of parts.
* [ ] It does not require explanation to make sense.

## Creative Design

* [ ] The body plan was chosen intentionally.
* [ ] The foundation suits the roster's 70/30 balance.
* [ ] The motif is integrated into anatomy, not applied as a symbol.
* [ ] The design has one clear primary idea.
* [ ] Secondary features support rather than compete with it.
* [ ] The design is surprising without becoming incoherent.

## Style

* [ ] The artwork reads as native pixel art.
* [ ] Pixel clusters appear intentionally placed.
* [ ] The silhouette is readable.
* [ ] The palette is limited and cohesive.
* [ ] Actual file colour count is at or under 16, transparency excluded.
* [ ] Shading uses compact clusters, lit upper-left, form shadow lower-right.
* [ ] Self-shadowing reads the form; no detached floor shadow exists.
* [ ] Outlines are crisp and controlled.
* [ ] Important features survive at native scale.

## Structure and Views

* [ ] All structures relate coherently.
* [ ] Locomotion fits the anatomy; center of mass fits the pose.
* [ ] Floating components are visibly intentional.
* [ ] Both views depict the same creature.
* [ ] Proportions, patterns, materials, counts, and placement match.
* [ ] Lighting and outline treatment match.
* [ ] The rear view does not invent anatomy.

## Technical — generated sheet (raw)

These are the requirements moved out of the prompt. None of them can be
guaranteed by asking; every one is verified by inspecting the file.

* [ ] Block structure is present: sampled colour-run lengths cluster at the
      block scale, not at 1 px. A median run of 1 px means the model produced a
      high-resolution illustration, not block-scaled pixel art — reject and
      regenerate.
* [ ] Canvas is at or near `[2*FRAME * BLOCK]` x `[FRAME * BLOCK]`, and its
      aspect ratio is 2:1.
* [ ] Background is a single flat colour across the whole canvas.
* [ ] Foreground contains no pixel at or near the key colour.
* [ ] No cast shadow, floor shadow, or background decoration.
* [ ] Both views share one visual scale and one baseline.
* [ ] No text, logo, signature, or watermark.

Measure run lengths rather than eyeballing the result. Block-scaled output shows
runs clustering at the block size; an illustration shows runs of 1 px. This is
the check that distinguishes real pixel art from the pixel-art look, and it is
the failure that survived every prohibition in earlier versions of this skill.

## Technical — normalised sheet (the deliverable)

* [ ] Downsample was point-sampled at exactly the block scale — no other ratio,
      no smoothing filter.
* [ ] Dimensions are exactly `2*FRAME` x `FRAME`.
* [ ] Both axes are divisible by the tile size.
* [ ] Actual file colour count is at or under 16, transparency excluded.
* [ ] Front view in column 0; rear view in column 1.
* [ ] Alpha channel present; background fully transparent.
* [ ] No residual key-coloured fringe around either silhouette.
* [ ] Neither silhouette crosses its cell boundary.
* [ ] Slicing at `cellW = cellH = FRAME`, offset 0, spacing 0 isolates both
      views with no manual measurement.

---

# Failure Conditions

Reject or revise when:

* the creature is a shapeless mass, blob, or unfocused assembly of geometry
* it has no clear focal point
* it cannot be described in one short phrase
* it is strange without being likeable
* it needs explanation before it makes sense
* the roster has drifted away from the 70/30 balance toward the exotic
* the design is an ordinary animal with an unintegrated emblem
* the creature resembles an existing copyrighted monster
* the artwork looks vector-drawn or automatically pixelated
* front and rear views contradict one another
* the pose conceals the core structure
* important details vanish at native size
* lighting or outline treatment changes between views
* the background is not the flat key colour, or the foreground contains it
* the output contains text, backgrounds, shadows, or watermarks
* a stage differs from its predecessor only in scale

---

# Revision Protocol

When revising an existing monster, preserve every approved design choice except
the traits explicitly targeted by the revision. Creative-liberty rules are not
permission to redesign an approved monster.

**Preserve** approved structural traits, proportions, materials, palette
decisions, pose elements, patterns, focal features, sprite scale, and style
decisions.

**Change** only the requested modifications.

**Verify** that the edit did not alter unrelated features or introduce anatomy
the creature did not previously possess.

---

# Agent Behavior

When this skill is active:

1. Treat the current brief as the authority for required species traits.
2. Treat this skill as the authority for style, appeal, and production quality.
3. Check appeal before originality; satisfy both.
4. Build on recognisable foundations by default; reserve exotic body plans for
   the roster's exotic minority.
5. Integrate motifs into anatomy rather than applying them as symbols.
6. Exercise real creative liberty when anatomy is unspecified, without treating
   unfamiliarity as a goal.
7. Measure diversity against the roster's balance, never against the last
   creature made.
8. Favor a clear structural concept over feature accumulation.
9. Keep every creature readable at native sprite scale.
10. Preserve approved traits during revisions.
11. Never claim exact pixel or colour compliance without inspecting the file.
12. Flag technical limitations when an image model cannot guarantee exact RGB
    values or true native pixel placement.

---

# Final Standard

Every monster should feel unique in structure, behavior, and visual identity
while unmistakably belonging to the same game — and every one of them should be
a creature a player wants.

Consistency comes from shared pixel craftsmanship, palette discipline, shading
logic, outline behavior, battle staging, technical standards, and visual
clarity.

Consistency must never depend on repeating the same body plan, limb count, face,
proportions, appendages, materials, pose, motifs, or palette.

The roster should look like a diverse living universe — not variations of one
template, and not a collection of abstractions.
