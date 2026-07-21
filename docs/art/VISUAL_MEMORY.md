# Visual Memory and Asset Profiles

This file records durable, approved visual facts for project-owned art. It is not an implementation
status tracker and does not constrain end-user games made with Creature Game Maker.

## Authority model

Resolve technical art values in this order:

1. An explicitly approved asset brief.
2. An approved asset-family or exception entry below.
3. A named asset profile below.
4. `docs/ART_BIBLE.md` qualitative direction.
5. Ask the user when the missing value changes layout, compatibility, or canon. For concept-only
   work, state a reversible assumption instead.

Never promote a generated experiment into this file without approval.

## Internal demo profile: `internal-demo-v1`

These are current authoring defaults for the original internal demo. They are not engine limits.
Custom sprite-sheet cells are supported by the project asset model, and future Creator workflows
will expose project-specific authoring values.

| Property | Default | Interpretation |
|---|---:|---|
| Project tile grid | 32 x 32 px | Internal demo choice; current project schema permits 16 or 32 |
| Battle sprite nominal frame | 64 x 64 px | Front/back candidate canvas; verify occupancy and anchors per family |
| Overworld character nominal frame | 32 x 32 px | Per-frame canvas, not total sheet size |
| Standard character walk sheet | 3 frames x 4 directions | Rows Down, Left, Right, Up; current asset helper contract |
| Portrait nominal canvas | 128 x 128 px | Bust/profile use; brief may specify crop |
| Small icon nominal canvas | 16 x 16 px | Inventory/status readability target |
| Virtual presentation size | 256 x 192 px | Runtime default (2026-07-21 Gen 4 alignment directive; was 240 x 160), not an asset-size restriction |
| Lighting | Upper-left | Lower-right form/cast shadow |
| Production scaling | Integer, nearest-neighbor | No smoothing or mipmap assumptions |

### Palette targets

| Asset class | Target ceiling | Notes |
|---|---:|---|
| Creature sprite | 16 colors | Count actual file colors; transparent is reported separately |
| NPC/character sprite | 16 colors | Share approved neutrals where practical |
| Environment family | 24 colors | Treat as a coherent family budget, not per-tile license |
| UI component family | 16 colors | Preserve accessible contrast and state differentiation |

These are production cleanup targets. Image-generation models cannot certify the counts.

## Shared technical conventions

- Use top-left image coordinates and integer pixel rectangles when discussing engine regions.
- Specify frame width, frame height, rows, columns, spacing, margin, transparent padding, baseline,
  ground-contact point, and anchor independently. Do not infer sheet size from a nominal frame.
- Use transparent backgrounds for reusable sprites unless the asset brief requires an opaque panel or
  concept board.
- Keep source sheets immutable after import; slicing and animation metadata refer to source regions.
- Use nearest-neighbor review at native scale and integer multiples.
- Treat `cellW` and `cellH` as request/profile values. Never hard-code the internal demo defaults into
  a specialist prompt for another approved profile.

## Approved environment-tileset authoring convention

Environment tileset generations for the internal demo are reusable source palettes for painting
maps in the Creator. They are not composed maps, scenic previews, adjacency diagrams, or substitutes
for the user's own map design.

- Use a genuinely transparent RGBA canvas with isolated, non-overlapping asset families and generous
  negative space.
- Prioritize broad painting variety: ground swatches, paths and transitions, trees and tree clusters,
  tall grass, shrubs, flowers, rocks, logs, stumps, leaf clusters, water/shore pieces when requested,
  and useful regional props.
- Every visible cell or object must differ meaningfully in silhouette, topology, arrangement, or
  interior detail. Do not pad sheets with exact or near-identical duplicates.
- Preserve polished cohesive pixel art; a mechanically complete but visually crude grid is not an
  acceptable replacement for the requested source art.
- Treat user-provided examples as structural references only. Never copy their specific artwork.
- Present only the actual alpha result as the deliverable. A chroma-key intermediate is processing
  evidence, never the final sheet.

## Naming conventions

Use stable lowercase snake-case slugs inside the repository's namespaced IDs. Display names remain
separate. Prefer:

- `sheet:<family>_<purpose>` for source sheets;
- `sprite:<subject>_<view>_<state>_<index>` for cells;
- `anim:<subject>_<action>_<direction>` for clips;
- `tileset:<region>_<purpose>` for tile families; and
- descriptive asset filenames such as `<family>_<purpose>_vNN.png` without embedding approval status.

Do not rename an established ID. Record variants with a meaningful suffix rather than overwriting an
approved source.

## Approved asset families

No production asset family has been approved yet. Add a row only after approval.

| Family ID | Scope | Defining assets | Locked traits | Approved by/date |
|---|---|---|---|---|

## Known exceptions

No exceptions are approved. Record the rule, exact scope, reason, and approval source; do not weaken
the general rule.

| Exception ID | Rule overridden | Exact scope | Reason | Approved by/date |
|---|---|---|---|---|

## Decision log

| Date | Decision | Scope and source |
|---|---|---|
| 2026-07-15 | The art system governs project-team/internal-demo work only, not end-user games. | User approval |
| 2026-07-15 | Values in `internal-demo-v1` are defaults, not engine limits; approved custom profiles override them. | User approval |
| 2026-07-15 | The shared direction is original handheld-readable pixel art with nature-integrated future motifs. | User prompt and `docs/ART_BIBLE.md` |
| 2026-07-16 | Environment tilesets are transparent, separated, varied painting palettes rather than map compositions or duplicate-filled topology boards. | User approval and structural reference |

## Update protocol

The Art Director may edit this file only when one of these is true:

- the user explicitly approves the decision;
- an authoritative project document establishes it;
- an approved production asset establishes a reusable convention; or
- the request explicitly asks to formalize a decision.

For every update, record date, provenance, exact scope, affected family/profile, and whether it
supersedes an older entry. Never record prompt experiments, rejected directions, or rapidly changing
task status as canon.
