# PHASE_16_DEMO_PLAN — Interactive Playable Demo Gate

Version 1.1 — 2026-07-21. Status: **ACTIVE — Phase 16 is the current focus.** (v1.1 records the
same-day user directives: Phase 15 paused, Phase 16 activated, Gen 4 alignment locked at 256×192.)

User directive (2026-07-21): Phase 16 completion must include a human-playable basic demo of the
engine covering the overworld → battle → progression/capture lifecycle. This document is the
detailed plan for that directive. It **supplements** `IMPLEMENTATION_PLAN.md` §6; it does not
replace, reorder, or relax any 16A–16G package, acceptance list, or budget. On any conflict,
`IMPLEMENTATION_PLAN.md` and `ARCHITECTURE_ADDENDUM.md` win.

## 1. Authority and constraints

- **Phase 15 is PAUSED and Phase 16 is active** (2026-07-21 user directive; recorded in
  `IMPLEMENTATION_PLAN.md` §6 prerequisite note and §10 items 10–11, and `SCOPE_GUARD.md`).
  Phase 16 consumes the Core contract at the pause baseline; Core edits only as recorded targeted
  regressions; no 15x package may be taken. Demo learnsets use only moves certified at
  content-authoring time (173/937 at pause).
- **Gen 4 alignment (user directive).** The demo targets a Pokemon Gen 4 *experience* in look and
  feel: 256×192 single-screen virtual resolution (DS-era; locked in §6.1), Gen 4-style battle
  staging and pacing, DS-era pixel-art direction per `ART_BIBLE.md`. Alignment is presentation
  only: no dual screen, no touch input, no official assets/names/cries/music/maps, and no
  mechanic exists in the demo unless Core already certifies it. Gen 4-era depth (physical/special
  split, abilities, held items, weather, day/night) appears exactly to the extent the paused Core
  baseline supports it, and no further.
- Reading order per `AGENTS.md` §1 applies to every implementing task. Owning specs:
  `ENGINE_RUNTIME_SPEC.md` (host/render/scenes), `BATTLE_SYSTEM_SPEC.md` (rules),
  `DATA_SCHEMA.md` (content shapes), `docs/art/VISUAL_MEMORY.md` (asset profiles).
- Hard rules that bind every item below: Core purity (Runtime computes no rules), determinism
  (injected `IRng` only), stable `category:slug` IDs, schema changes via `DATA_SCHEMA.md`,
  closed dependency list, moves are data.
- **IP rule.** No official creature names, sprites, cries, music, or maps anywhere, including
  this demo. "Gen 3/4 battle layout" is a **design-time layout reference only** — the same
  standing as `docs/pokeapi-results`. The demo's capture item is an original item in the
  `item:` category (working ID `item:capture_orb`; display name is data). The word "Pokemon"
  never appears in content, code, or assets.

## 2. What the existing Phase 16 already delivers

The requested lifecycle maps almost entirely onto packages already locked in §6:

| Demo lifecycle requirement | Owning package(s) | Already planned mechanism |
| --- | --- | --- |
| Walk around a map | 16B render, 16D overworld | Fixed-tick Core movement/collision/ledge; camera; tile chunks from assets |
| Tall grass starts a wild battle | 16D | Completed-step trigger order: warp → tile trigger → trainer sight → random encounter (Core encounter tables, Core RNG) |
| Trainer's path starts a trainer battle | 16D | Trainer sight trigger, once/defeat flag, deterministic replay |
| Battle laid out properly | 16F | BattleScene renders slot layouts; event-driven presentation |
| Real moves via the real move engine | 16F + Phase 15 contract | Runtime enumerates legal actions from Core and consumes Core events; it never predicts damage/legality/faint |
| Experience gained after battle | 16F return + 16E | Battle return applies Core outcome/progression/reward mutations once; level/evolution prompts consume Core results |
| Capture with random break-out | Core (Phase 9 baseline) + 16F | Capture is a legal battle action where rules permit; shake/break-out counts come from Core capture resolution through injected RNG; Runtime animates the event stream it is told |

The renderer ("full render pipeline") is package **16B** and its locked defaults in §6.1:
OpenGL 3.3 core, 256×192 virtual resolution (Gen 4 alignment, §1), integer scale + letterbox,
nearest-neighbor, one
dynamic quad batch (2,048 start, power-of-two growth), premultiplied alpha, layer+sequence
ordering, tile chunks, UI/world projections, scissor — game code never sees GL (`IRenderer`).
This plan adds no renderer feature beyond that list; requests for shaders, rotation, dynamic
atlasing, or additional backends remain excluded.

## 3. The gap this plan fills — package 16H

16G proves the engine with **scripted** inputs (headless replay + hidden-window smoke). Nothing
in 16A–16G requires that a person can sit down, play, and complete the loop. 16H closes that.

### 16H — Interactive playable demo gate (`PLANNED`; prerequisites 16A–16G)

**Principle: the demo is the 16G fixture made playable, not new breadth.** §6's exclusion of
"original demo breadth" stands. 16H reuses the 16G neutral fixture content and adds only what
interactive play requires. One map region, a handful of species, one trainer. Anything more is
Phase 18 vertical-slice work and is refused here.

#### 3.1 Demo content inventory (fixture pack, authored as ordinary project data)

All IDs original, `category:slug`, immutable once created. No new schema shapes are expected;
if authoring reveals a missing shape, that is a `DATA_SCHEMA.md` change with version bump and
migration note **before** content is written.

- **Species: 4–6**, drawn from the approved original roster (current candidates: the
  `bulby` line and the three-stage line now in `raw_images/`), each with front/back battle
  sprites at the `internal-demo-v1` 64px frame via the normalization pipeline, base stats,
  one type each from the project type chart, level-up learnset of **certified Phase 15
  moves only** (no move may appear that is not conformance-certified), growth rate, capture
  rate, and one evolution by level within the demo line.
- **Moves: none authored.** The demo consumes existing certified moves exclusively. If a
  desired demo moment needs an uncertified move, the demo moment changes, not the corpus.
- **Items:** `item:capture_orb` (capture device, uses Core capture formula), one healing
  item. Shop/bag breadth beyond what 16E already requires is excluded.
- **Map:** one outdoor fixture map (project tile grid 32×32) containing: walkable routes, at
  least one tall-grass encounter region with an encounter table (2+ species, level ranges),
  one trainer NPC with sight-line path and party of 2, one heal point, one warp pair, ledges,
  and blocked terrain — exercising every 16D interaction class. Tileset produced through the
  `cgm-tileset-production` process (RGBA deliverable, grid-aligned).
- **Player character:** 32×32 walk sheet, 3 frames × 4 directions, per `VISUAL_MEMORY.md`.
- **Audio: none required for 16H.** 16E's audio system ships regardless; demo music/SFX
  assets are optional and their absence must produce the documented clean no-audio fallback,
  not a failure.

#### 3.2 Demo battle presentation contract (Gen 4-style layout, original execution)

Locked as the concrete instantiation of 16F's layout rules at 256×192 virtual resolution:

- Opponent creature upper-right on a ground ellipse; opponent info panel upper-left with
  name, level, HP bar (and status icon when Core reports one). No numeric HP for opponents.
- Player creature lower-left, back sprite, on a ground ellipse; player info panel lower-right
  with name, level, HP bar, numeric HP, and EXP bar.
- Bottom message panel: typewriter text (16C primitive) for every consumed Core event; the
  four-slot action menu (Fight/Bag/Party/Run — display names are data) and move list with
  PP and type shown from Core-provided data.
- All panels are 16C UI primitives (9-slice, bitmap text, HP/resource bar). No new primitive
  is introduced for the demo; a layout need that seems to require one is escalated.
- Every visible number/state originates from a Core event or Core query. The scene renders
  HP bar motion as presentation of an HP-change event, never by recomputing damage.

#### 3.3 Demo lifecycle acceptance script (the user's three requirements, made testable)

1. **Overworld:** From New Game, reach the grass region and the trainer sight-line using
   keyboard and gamepad; collision, ledges, warp, and heal point behave per Core decisions.
   Entering tall grass rolls encounters through Core tables (deterministic under seeded
   replay); crossing the trainer's sight starts the trainer battle exactly once until defeat
   flag rules say otherwise.
2. **Battle:** Wild and trainer battles present per §3.2. Move selection lists only
   Core-legal actions; using moves produces damage/status/faint exclusively via consumed
   events; attempting `Run` and `item:capture_orb` in a trainer battle is refused by Core
   and presented as such (capture/trainer restriction test from 16F reused interactively).
3. **Progression and capture:** Winning grants EXP per Core progression (visible EXP bar
   motion, level-up prompt when Core says so). In a wild battle, throwing `item:capture_orb`
   yields Core-resolved shake events with genuine break-out probability through injected RNG
   — demonstrably variable across seeds, byte-identical under a fixed seed. A successful
   capture adds the creature to party (or Core storage on overflow) and survives
   save/relaunch.

#### 3.4 Acceptance evidence (all required)

- The §3.3 script passes **live** (human input, windowed) on keyboard and on gamepad, and
  the equivalent scripted input sequence passes headless with byte-identical Core state on
  two runs of the same seed.
- Capture statistics: a seeded harness across ≥1,000 capture attempts at fixed HP/status
  matches Core's expected capture formula distribution within documented tolerance (proves
  "random chance to break out" is real and Core-owned).
- Raw and packed fixture forms of the demo produce equal Core/save/event outcomes (16A
  parity applied to the demo pack).
- Zero official-IP scan over demo content and assets passes (same scanner as 16A).
- No Runtime source change is demo-specific: grep-level check that no demo content ID
  appears in Runtime code.
- 16G budgets hold while playing the demo route (no new budgets; the fixture *is* the route).

**Exit:** a person with no repository knowledge can launch `Cgm.Runtime` against the demo
pack, play lifecycle 1→2→3 with the shipped default bindings, and every automated item above
is green.

## 4. Explicit "do NOT build yet" for 16H

Multiple maps/towns/interiors beyond the single fixture map; more than one trainer; day/night
content; breeding/abilities/held-item/weather content (Addendum §3 deferrals stand); music
composition; menus beyond 16C/16E scope; difficulty tuning (Phase 14 baseline stands);
Creator authoring UI for any of this (Phase 17); export-template polish (Phase 18); any new
effect op, species mechanic, or move (Phase 15 owns the corpus, and it is frozen at GO).

## 5. Risks and open decisions

- **R1 — Art pipeline unproven at generation.** The 64px block-scale generation method is
  designed but not yet validated against the image generator; sprite QC (≤16 colours, block
  structure) has never passed on real output. Mitigation: validate during 16A-16C (art is not
  on their critical path) and before 16D/16H content authoring; the normalizer and QC checks
  already exist.
- **R2 — Capture math ownership.** If capture resolution proves incomplete in the paused Core
  baseline, that is a Core targeted-regression fix under the §6 prerequisite note — recorded,
  minimal, in Core — never patched in Runtime for the demo.
- **R2b — Paused-contract churn.** Phase 16 builds on an unfrozen Core contract. Every
  targeted Core regression fix during Phase 16 risks invalidating Phase 15 conformance
  artifacts; each fix must run the existing conformance suite and record any golden change
  with its reason. When Phase 15 resumes, the accumulated deltas are reconciled first.
- **R3 — Scope pressure.** The demo invites "just one more" content additions. §4 is the
  answer; additions go to `SCOPE_GUARD.md` §Idea Ledger.
- **D1 (user decision, non-blocking now):** demo species final selection and display names —
  needed only when 16H content authoring begins.

## 6. Reconciliation applied with this plan

`IMPLEMENTATION_PLAN.md` §6 gains package **16H** (after 16G), one GO checklist line, and a
pointer to this document, recording the 2026-07-21 user directive. The §6 exclusion of
"original demo breadth" is narrowed only by §3.1's fixed inventory; everything else stands.
