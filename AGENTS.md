# AGENTS.md — Creature Game Maker

You are working on **Creature Game Maker**: a desktop Creator Application (Avalonia) plus a
custom 2D runtime engine (Silk.NET), sharing one rules library (Cgm.Core), that lets users
build Pokemon-style creature RPGs from original assets and export standalone Windows games.

This file is binding. If anything you are about to do conflicts with it, stop and say so.

---

## 1. Reading order (do this before non-trivial work)

1. `AGENTS.md` (this file) - always.
2. `docs/SCOPE_GUARD.md` - before accepting ANY task. If the task isn't in the current
   phase's deliverables, it does not get built.
3. `docs/IMPLEMENTATION_PLAN.md` - current phase, ordered work packages, status, and exit gates.
4. `docs/ARCHITECTURE_ADDENDUM.md` - **wins over MASTER_PLAN.md on every conflict.**
5. `docs/MASTER_PLAN.md` - product vision and older full plan, as amended by the files above.
6. `docs/AGENTS.md` - task-area to owning-spec map.
7. The spec that owns your task area. If its relevant section is incomplete, consult
   `IMPLEMENTATION_PLAN.md` v4's package contract. When that contract supplies locked defaults and
   marks specification work authorized, complete/reconcile the owning spec first and proceed without
   another user confirmation. Block only when neither document resolves a mechanically significant
   decision or v4 §2.1 reserves the decision for the user.

Documents are the contract. Code that contradicts a spec is a bug in one of them; reconcile
explicitly (update the doc in the same change, with a note) — never silently diverge.

## 2. Hard rules — never violate

- **No game engines or frameworks.** Unity, Godot, Unreal, MonoGame, FNA, Raylib, Stride,
  SDL helper suites, or anything describing itself as a game engine/framework. Allowed
  dependencies are pinned in `docs/TECH_STACK.md`; that list is closed. Adding ANY package
  requires updating TECH_STACK.md in the same change and flagging it to the user first.
- **Cgm.Core stays pure.** No UI, graphics, audio, windowing, or filesystem-of-the-machine
  dependencies. All game rules (battle, movement, capture, exp, evolution, saves,
  validation) live in Core. Runtime code that computes rules is a defect.
- **Creator never simulates gameplay in-process** (ADR-009). Playtest spawns Cgm.Runtime.
- **UI never mutates game state.** UI submits actions; controllers validate; resolvers
  apply; state owns truth. This applies to battles AND editors (editors go through the
  undoable command stack).
- **Determinism.** All randomness through injected `IRng`. No `new Random()`, no statics,
  no wall-clock reads in sim code. Golden replay tests depend on this.
- **Stable IDs.** `category:slug` EntityIds are immutable after creation. Never rename an
  ID; display names are data.
- **Schema changes require DATA_SCHEMA.md.** Any change to a serialized shape updates
  `docs/DATA_SCHEMA.md` in the same change, bumps `schemaVersion`, and adds a migration
  note. Saves are never silently broken.
- **No official Pokemon assets, names, cries, music, or maps** anywhere — including test
  fixtures and "temporary" placeholders. `docs/reference/pokeapi-results` (currently
  `docs/pokeapi-results`) is a design-time mechanics reference only and must never be
  copied into builds, packs, samples, or exports.
- **Moves are data, not code.** Move behavior is composed from the closed effect-op
  palette in BATTLE_SYSTEM_SPEC.md. Implementing a specific move as bespoke code is the
  project's named failure mode. If an op is missing, propose the op — don't special-case.

## 3. Scope discipline

- Work only on the current phase (tracked in `docs/IMPLEMENTATION_PLAN.md`). Each phase in
  MASTER_PLAN.md §15 has a "do NOT build yet" column — treat it as a build error.
- New ideas (yours or the user's) mid-phase: append to `docs/SCOPE_GUARD.md` §Idea Ledger
  with one line of context. Do not implement them. Do not "stub them out for later."
- The deferred lists in ARCHITECTURE_ADDENDUM.md §3 are binding as amended by the
  2026-07-10 Phase 15 scope rebase. Phase 15 now permits every reusable Core mechanic
  required to make all 937 local PokeAPI move entries conform, including doubles Core
  topology where a move requires it. This is not permission for doubles UI, official
  content packs, breeding, event scripting, localization, or multiplayer/netcode.
- Battle work follows the rebased Phase 15 conformance contract in IMPLEMENTATION_PLAN.md
  and Addendum §8; asset-import work follows v0–v5 (Addendum §9). A layer's exclusions
  found in a change = remove them, even if written.
- If the user asks for something out of scope, say so, point at the relevant section, and
  offer to log it in the Idea Ledger — then do what the user decides. You flag; they rule.

## 4. Anti-slop standards

### Ponytail is mandatory on every coding task (non-negotiable)

The **Ponytail** plugin (`~/.claude/plugins/cache/ponytail/ponytail/4.8.4/skills`, default
intensity **full**) governs all code in this repo. It is **active on every response** and is
never ignored, skipped, or allowed to drift back to over-building. It turns off only if the
user literally says "stop ponytail" / "normal mode". When the `ponytail` / `ponytail-review` /
`ponytail-audit` / `ponytail-debt` skills are available in-session, invoke them; when they are
not, apply the method below anyway — it is a working style, not just a tool call.

**Climb the ladder before writing anything — stop at the first rung that holds:**

1. Does this need to exist at all? Speculative need → skip it, say so in one line. (YAGNI)
2. Already in this codebase (helper/util/type/pattern)? Reuse it — look before you write.
3. Does the stdlib do it? Use it.
4. Native platform/framework feature? Prefer it over custom code or a new dependency.
5. Already-installed dependency? Use it. Never add a new package for what a few lines do
   (and per §2, new deps need TECH_STACK.md + user sign-off regardless).
6. Can it be one line? One line.
7. Only then: the minimum code that works.

**Enforced rules:** no unrequested abstractions (no interface with one impl, no factory for one
product, no config for a constant); deletion over addition; fewest files; shortest working diff
— _after_ you understand the problem (trace every file the change touches first; a small diff in
the wrong place is a second bug). Bug fix = root cause in the shared function, not a per-caller
symptom patch. Mark deliberate simplifications with a `// ponytail:` comment naming the ceiling
and upgrade path.

**Never lazy about (build it fully):** understanding the problem; input validation at trust
boundaries; error handling that prevents data loss; security; accessibility; anything the user
or a spec explicitly requires. This is why Ponytail does **not** license skipping this project's
mandated per-phase test suites, golden replays, or validation rules — those are explicitly asked
for by DATA_SCHEMA/BATTLE_SYSTEM/TESTING specs, so building them is spec-complete, not
over-building. Minimal here means _spec-complete and tested with the least code_, never
under-built. Ponytail reinforces SCOPE_GUARD.md; it does not override the specs or phase plan.

- **No filler.** No boilerplate comments ("// constructor"), no restating the plan in code
  comments, no speculative abstraction ("might need this later" = delete it), no wrapper
  classes with one caller, no interfaces with one implementation unless a seam is named in
  the addendum (IRenderer, IRng, IInputSource, IValidationRule).
- **Match existing patterns.** Before writing a new editor/screen/system, read the
  pathfinder implementation of the same shape and copy its structure. One way to do each
  thing.
- **Small, verifiable increments.** Every change compiles, tests pass, and completes one coherent
  acceptance criterion. "Small" limits conceptual scope and unnecessary code; it does not mean the
  fewest lines an agent can submit. No 3,000-line "implemented the battle system" drops, and no
  one-line checkpoint while the selected criterion remains incomplete.
- **Tests are the deliverable, not decoration.** Per CODING_STANDARDS.md: every validation
  rule, effect op, formula, and schema gets tests; goldens change only intentionally, with
  the reason stated in the change.
- **Report honestly.** Failing tests, skipped steps, and deviations get stated plainly.
  Never claim done without running the tests. "It should work" is not a status.
- **Don't invent requirements.** If a spec is silent, apply IMPLEMENTATION_PLAN v4 §2.1's authority
  order and locked package defaults, write the decision into the owning spec, and test it. Ask only
  for a decision v4 expressly reserves for the user; never decide silently and bury it in code.

### Work-unit integrity — no artificial micro-slices

- Define the slice by an observable acceptance criterion from the active roadmap package before
  editing. A slice is the smallest **complete behavior**, not the smallest possible diff, method,
  branch, file, test, or tool call. Line count, token budget, elapsed time, and turn boundaries are
  never completion criteria.
- Complete every layer required by that criterion in the same slice: owning-spec reconciliation,
  production behavior, validation/compiler/resolver/events/trace/AI integration as applicable,
  focused tests, required progress documentation, and verification. Do not split those layers into
  separate tasks merely to produce an early response.
- Continue working while the selected criterion is incomplete and safe in-scope work is known. Do
  not stop after only adding a type member, signature, guard, mapping, comment, test skeleton, or
  similarly preparatory one- or two-line edit. Such an edit is a valid finished slice only when it
  independently fixes the root cause, closes the whole criterion, and is verified by the required
  tests.
- Do not manufacture a checkpoint because a package is large or a refactor spans several files.
  Checkpoints exist only for an unavoidable context boundary or a genuine blocker. A checkpoint
  must be normal-path green, must record the package as `IN PROGRESS` with the exact remaining
  acceptance work, must not be reported as completion, and must be resumed before any new task.
- Before reporting, ask: "Which named acceptance criterion is now fully green that was not green
  before?" If there is no concrete answer, the slice is not complete; continue or report the real
  blocker. Activity, a small diff, or a passing pre-existing suite is not delivered value.

## 5. Definition of done (every task)

1. Matches the owning spec, or the spec was updated in the same change.
2. `dotnet build` and `dotnet test` green; new behavior has new tests.
3. No new dependencies (or TECH_STACK.md updated + user informed).
4. No scope-creep code, no dead code, no TODOs referencing future phases.
5. `docs/IMPLEMENTATION_PLAN.md` updated if phase status changed.
6. Deviations from the task as given are listed explicitly in your report.

## 6. When unsure

Blocked on a real user-reserved decision → ask, with a recommendation. Other ambiguity a spec should
resolve → apply plan v4 §2.1 and fix the spec first. Tempted by a "better architecture" than the ADRs → write a proposed
ADR in `docs/adr/` and stop; ADRs change by decision, not by drift.

## 7. Iteration loop command

When the user says "loop", "continue", "iterate", "run the loop", or asks for autonomous progress:

1. Read the required documents in §1.
2. Identify the current phase from `docs/IMPLEMENTATION_PLAN.md`.
3. Pick the next incomplete in-scope package, then its next coherent unmet acceptance criterion.
4. Implement that criterion completely under the work-unit integrity rules above. Never redefine
   "next slice" as a self-created micro-task that leaves the criterion open.
5. Run `dotnet build`.
6. Run `dotnet test`.
7. Fix failures caused by the change.
8. Update `docs/IMPLEMENTATION_PLAN.md` with:
   - completed work
   - changed files
   - tests run
   - remaining work
   - blockers, if any
9. Stop and report if blocked, out of scope, tests fail for unrelated reasons, or user approval is needed.

A roadmap package may span multiple model turns. Package size or a substantial refactor is not a
blocker and does not justify a zero-change response. If a context boundary prevents completion, land
only a normal-path green checkpoint, record the package as `IN PROGRESS` with the exact continuation,
and resume that package before selecting another. Do not relabel internal checkpoints as new roadmap
slices or advance certification before the full package exit passes.

Do not advance phases unless the current phase definition of done is met.
Do not implement ideas from the Idea Ledger unless the user explicitly moves them into scope.
