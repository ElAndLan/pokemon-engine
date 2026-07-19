# ADR-011 — Overlaid battle move sets (Transform / temporary move replacement)

Status: Accepted (2026-07-19, user-directed)

## Context

Phase 15F-6 needs a creature to fight with a move set that differs from its own — Transform copies
the target's moves (with a fresh PP pool), and temporary move replacement (Mimic/Sketch-style) swaps
one slot. The 15F-1 overlay system already carries a `MoveListOverlay`, but move **selection** and PP
spending read the live `BattleCreature.Moves` list directly (`MoveAt` / `EffectiveMoveIndex` /
`invocation.PpOwner.UsePp()`); the `MoveListOverlay` is consumed only by damage/STAB identity queries.
Writing a `MoveListOverlay` for Transform without changing selection would desync selection from STAB
(select move A, compute STAB for move B).

Two approaches were considered:

- **(A) Route selection/PP through the effective move list.** Reversion is automatic (overlays clear on
  switch/faint/end), but it touches every reader of `creature.Moves` — selection, legality, PP, AI,
  fallback — a broad, high-risk change across the battle engine.
- **(B) Runtime move-list swap on the creature.** A `BattleCreature.OverrideMoves(list)` /
  `RestoreMoves()` pair; the controller overrides on Transform and restores on switch/faint/battle-end.
  Small selection surface (selection keeps reading `creature.Moves`), reversion is controller-managed
  rather than overlay-driven.

## Decision

Adopt **(B)**. `BattleCreature.OverrideMoves` replaces the live `Moves` with the copied list (fresh PP)
and remembers the pre-transform list once; `RestoreMoves` puts it back. Reversion is wired at the same
points that already reset volatiles: `RestoreMoves` is called from `ClearVolatiles` (switch-out) and at
faint cleanup; battle end ends the battle so no restore is observable. The Transform value overlays
(types/stats/ability/form marker) continue to revert via the overlay cleanup, so the two halves clear
together.

## Consequences

- Move selection, legality, PP, and AI keep reading `creature.Moves` unchanged — no engine-wide rewire.
- Transform's copied moves get a fresh PP pool (Transform gives each copied move `min(5, base PP)`), and
  spending them does not touch the originals, which are restored intact on reversion.
- Reversion is imperative, so any new "creature leaves the field" path must call `RestoreMoves` (today:
  `ClearVolatiles` on switch-out and the faint cleanup). This is the one maintenance cost of (B).
- Temporary single-slot move replacement can reuse the same override with a duration tracked by the
  controller.
