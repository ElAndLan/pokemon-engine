# ADR-012 — Mid-turn self-switch replacement selection

Status: Accepted (2026-07-19, user-directed)

## Context

Self-switch moves (Baton Pass, U-turn/Volt Switch/Flip Turn) switch the user out during move resolution.
When the user has two or more healthy reserves, real games pause mid-turn for the player to pick the
replacement, then place it before the rest of the turn continues. Core (Cgm.Core) resolves an entire
turn in one deterministic `ResolveTurn` call and only pauses for replacement selection at end of turn
(after faints, via `ResolveReplacements`). Three options were considered for the multi-reserve pick:

- **(A) Turn suspend/resume.** `ResolveTurn` returns an "awaiting self-switch selection" state and a new
  entry point resumes with the choice. Faithful timing, but threads suspension/serialization state
  through the whole resolver and every caller — a large, golden-churning change.
- **(B) Deferred end-of-turn replacement.** The user leaves immediately and the incoming is chosen at
  end of turn through the existing replacement flow. Reuses machinery, but the active slot is empty for
  the rest of the turn, so same-turn opponent moves lose their target — incorrect.
- **(C) Deterministic Core auto-select, immediate switch-in.** Core switches to the party-index-first
  healthy reserve immediately (identical to the already-shipped single-reserve path); the interactive
  player choice is a Runtime concern layered on later.

## Decision

Adopt **(C)**. `SelfSwitch` switches to the first healthy reserve in party-index order whenever at least
one exists, immediately, carrying the passable state. This matches the spec's "candidate lists use
party-index order" rule and keeps single- and multi-reserve self-switches consistent and deterministic
(golden-replay friendly). The player's actual replacement choice for a self-switch is presentation/
interaction and belongs to the Runtime battle layer (Phase 16), which will supply the chosen party index
through the same action-submission seam that voluntary switches and faint replacements already use;
Core's party-index-first pick is the deterministic default the Runtime overrides.

## Consequences

- Baton Pass and pivots work for any reserve count with no turn-model change and no empty-slot window.
- Core stays a one-shot deterministic resolver; no suspend/resume state to serialize for saves/replays.
- The default replacement is not the human player's free choice until the Runtime adds interactive
  self-switch selection (Phase 16). This is acceptable because Phase 15 certifies move *mechanics*, not
  interaction, and the choice does not affect move correctness or conformance.
- AI-vs-AI and headless conformance are fully deterministic today.
