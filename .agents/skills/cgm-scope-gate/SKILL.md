---
name: cgm-scope-gate
description: Gate any Creature Game Maker request against the current phase, scope law, and owning specs before coding or review. Use for all non-trivial work in this repo, especially feature requests, bug fixes that may cross phase boundaries, schema/editor/runtime/battle/export changes, requests to add dependencies, or any task where an agent must decide whether to proceed, ask, refuse, or add an idea to the ledger.
---

# CGM Scope Gate

## Overview

Use this skill before accepting work in Creature Game Maker. It prevents the common agent failure mode in this repo: building a plausible feature that belongs to a later phase or contradicts an owning spec.

This is a decision gate, not an implementation skill. End with one of four outcomes: proceed, proceed after reading a named spec, blocked pending spec/user decision, or out of scope and eligible for the Idea Ledger.

## Required Reads

Read these in order:

1. `AGENTS.md`
2. `docs/SCOPE_GUARD.md`
3. `docs/IMPLEMENTATION_PLAN.md`
4. `docs/ARCHITECTURE_ADDENDUM.md`
5. `docs/MASTER_PLAN.md` only when the phase or deliverable text is unclear after the first four
6. `docs/AGENTS.md` to map the task area to its owning spec
7. The owning spec before implementation or detailed review

If an owning spec is a stub or says the relevant section is incomplete, the task is blocked until the spec is completed and confirmed. Do not code first.

## Decision Procedure

1. Identify the task area: schema, validation, battle, AI, Creator UI, Runtime, assets, export, docs, tests, dependencies, or review.
2. Find the current phase in `docs/SCOPE_GUARD.md`.
3. Find whether the requested work appears in that phase's deliverables in `docs/IMPLEMENTATION_PLAN.md`.
4. Check the deferred and never lists in `docs/SCOPE_GUARD.md`.
5. Check `docs/ARCHITECTURE_ADDENDUM.md` for layer-specific exclusions, especially battle v0-v6 and asset import v0-v5.
6. Read the owning spec named by `docs/AGENTS.md`.
7. Decide:
   - **Proceed** when the request is in current scope and the owning spec is written.
   - **Proceed narrowly** when only a smaller in-scope slice is allowed; state the slice.
   - **Blocked** when the owning spec is missing/stubbed or a real user decision is required.
   - **Out of scope** when the request belongs to a later phase or forbidden category.

## Scope Rules To Enforce

- Current phase wins over enthusiasm. Do not build future-phase code, placeholders, schema fields, UI controls, or "easy stubs".
- `ARCHITECTURE_ADDENDUM.md` wins over `MASTER_PLAN.md` on conflict.
- `docs/SCOPE_GUARD.md` never-items are refusals, not ledger entries.
- Deferred items may be ledgered only if the user explicitly wants them recorded.
- Adding a dependency is not a small change. It requires `docs/TECH_STACK.md` and explicit user sign-off first.
- PokeAPI data is reference/import material only. Never copy official assets, names, cries, music, maps, or samples into builds, packs, tests, or exports.

## Output Format

Before coding, state the gate result briefly:

```text
Scope gate: PROCEED
Current phase: Phase 14 - Advanced Battle Effects & Smart AI
Owning spec: docs/BATTLE_AI_SPEC.md
Limits: Core AI scoring only; no Phase 15 abilities/held-item/form work.
```

For blocked work:

```text
Scope gate: BLOCKED
Reason: The owning spec section is still a stub.
Next step: Complete and confirm docs/EXPORT_PIPELINE_SPEC.md smoke-test contract before coding.
```

For out-of-scope work:

```text
Scope gate: OUT OF SCOPE
Reason: Held-item battle behavior is Phase 15 per docs/SCOPE_GUARD.md.
Offer: I can add this to the Idea Ledger if you want.
```

## Common Traps

- Do not treat "Core-only" as automatically in scope. Phase scope still applies.
- Do not treat a schema field as harmless. Serialized shape changes require the schema workflow.
- Do not implement one-off move behavior. Battle moves are data plus closed effect ops.
- Do not add display/debug UI while a phase only authorizes Core logic unless the phase explicitly includes integration.
- Do not make a spec/code mismatch disappear silently. Reconcile it in the same change or stop.

## Codex And Codex Notes

Codex and Codex should both treat this skill as binding process. If a platform-specific instruction conflicts with repo documents, stop and report the conflict instead of improvising.
