# Creature Game Maker — Architecture Refinement Addendum

Version 1.0 — 2026-07-06
Status: Authoritative refinement of MASTER_PLAN.md. Where this addendum and MASTER_PLAN.md conflict, **this addendum wins**. It does not replace the plan; it tightens it.

2026-07-08 status correction: this document was written before implementation advanced past
the early phases. `TECH_STACK.md` is the source of truth for currently referenced packages.
`SCOPE_GUARD.md` and `IMPLEMENTATION_PLAN.md` are the source of truth for current phase status.
Smart AI is a verified Phase 14 Core baseline. Phase 15 now owns ability/held-item/form weather
interactions and remains open until the real demo showcase fight exists. Atlas packing, the data
half of export, local runtime template copy, and exported `--smoke` exist; CI self-contained
templates, icon/metadata patching, Creator export UI, clean-VM testing, and the full rendered
runtime remain open.

2026-07-10 user-directed scope rebase: `IMPLEMENTATION_PLAN.md` v3 and `SCOPE_GUARD.md` v3
supersede older phase-assignment and deferral statements in this addendum. Phase 15 now owns
complete Core game logic and exact conformance for all 937 move JSON files in the local
`docs/pokeapi-results/move/` corpus. This includes doubles Core topology and any reusable timing,
targeting, condition, query, mutation, or ruleset primitive required by those moves. It does not
permit bespoke move code, official shipped content, doubles UI, breeding, or multiplayer/netcode.

---

## 1. Final Tech Stack Lock

**Correction accepted: target .NET 10 LTS (released Nov 2025, supported to Nov 2028), not .NET 8.** Starting a multi-year project in mid-2026 on .NET 8 (EOL Nov 2026) would force a mid-project migration. Avalonia 11.x and Silk.NET both run on .NET 10 (both target `net8.0`+ / netstandard and are forward-compatible; verify exact package versions in Week 1 — this is the only conditional in the lock). If a Week-1 spike shows a blocking incompatibility (unlikely), fall back to .NET 9 with a planned bump, never .NET 8.

| Dependency | Choice | Why needed | Why not a "game engine" | Risk | Fallback |
|---|---|---|---|---|---|
| Runtime platform | **.NET 10 LTS, C# 14** | One language for Core/Creator/Runtime; LTS spans the whole project | Language runtime, not an engine | Low | .NET 9 → bump later |
| Creator UI | **Avalonia 11.x (MVVM)** | Mature XAML desktop UI: docking, virtualized lists, custom canvases | Desktop UI toolkit (spreadsheet-app class), no game concepts | Low-Med (custom canvas work is ours) | WPF (Windows-only, older) |
| Windowing/input/GL context | **Silk.NET.Windowing + Silk.NET.Input** | Cross-platform window, swapchain, keyboard/gamepad events | Thin auto-generated bindings over GLFW/SDL; provides zero game functionality (no sprites, no scenes, no loop) | Low | Raw GLFW via GLFW.NET, or Silk.NET.SDL |
| Rendering | **Silk.NET.OpenGL (GL 3.3 core)** — renderer itself is 100% custom | Need a GPU API; we write the sprite batch, atlas, tilemap chunks ourselves | It's the raw GL API surface; every drawing abstraction above it is this project's code | Med (our largest from-scratch subsystem) | Silk.NET.Direct3D11 behind the same `IRenderer` boundary |
| Audio | **Silk.NET.OpenAL** | BGM streaming + SFX one-shots | Raw audio API bindings; mixer/streamer logic is ours | Low-Med | miniaudio via a thin P/Invoke shim |
| Image loading | **StbImageSharp** | PNG decode for slicer + runtime | Pure decoder | Low | ImageSharp (heavier) |
| Compression | **ZstdSharp** (pack blobs) + `System.IO.Compression` (zips, save gzip) | Pack size + fast load | Codec | Low | Deflate only |
| Serialization | **System.Text.Json + source generators** (project data, saves); **custom binary writer** for `.cgmpack` header/index | Git-diffable authoring data; fast versioned pack | BCL + our own format | Low | Newtonsoft (last resort) |
| Testing | **xUnit + Verify** (snapshot/golden files) | Golden battle replays are snapshot tests | — | Low | NUnit |
| CI | **GitHub Actions, windows-latest** | Build, test, publish runtime templates, export demo game | — | Low | Local scripts |
| Installer/packaging | **None for now.** Creator distributed as self-contained zip; exported games are folders (exe + pack). Revisit Velopack/MSIX post-vertical-slice | Avoid installer complexity early | — | Low | Velopack |

Forbidden list (add to TECH_STACK.md): Unity, Godot, Unreal, MonoGame, FNA, Raylib(-cs), SDL_gfx-style helper suites, Stride, any package whose description says "game engine" or "game framework". Silk.NET is acceptable *only* for its binding packages (Windowing, Input, OpenGL, OpenAL) — do not pull `Silk.NET` meta-packages that drag in extras.

---

## 2. Architecture Decision Records

**ADR-001: C#/.NET over Rust/C++.**
Decision: C#/.NET 10 for all three components. Context: solo dev + AI agents over many months; need one shared data model. Alternatives: Rust (safety, but egui/Tauri weak for a docking editor; slower exploratory gameplay iteration), C++ (max control; memory-bug tax on AI-generated code; two-language pressure for the editor). Consequences: single source of truth in Cgm.Core; fast iteration; GC pauses irrelevant at this workload with pooling. Risks: temptation to lean on game frameworks in the .NET ecosystem — mitigated by forbidden list; self-contained exe size ~70–90 MB — accepted.

**ADR-002: Avalonia for the Creator.**
Decision: Avalonia 11 MVVM. Context: editor needs trees, tabs, inspectors, undo, and two heavy custom canvases (slicer, map). Alternatives: WPF (Windows-only, aging), ImGui-style editor (fast to start, poor for data-heavy forms/accessibility/undo), web/Electron (second language). Consequences: XAML learning curve; excellent long-term editor ergonomics; keeps future Linux/macOS creator possible. Risks: custom canvas performance — mitigate with our own render-to-bitmap/GL-interop canvas control built once and reused by slicer + map editor.

**ADR-003: Custom renderer on raw OpenGL 3.3.**
Decision: hand-written sprite batch + tilemap chunk renderer over Silk.NET.OpenGL. Context: total feature set = textured quads, atlases, ortho camera, integer scaling. Alternatives: wgpu-style abstraction (needless), D3D11 (Windows-lock, no benefit), software rendering (CPU cost for zero gain). Consequences: ~2–3k lines of GL code we fully own; trivially sufficient perf. Risks: GL context/driver edge cases — mitigate with GL 3.3 core only, no extensions, and an `IRenderer` seam so a D3D11 backend could be added without touching game code.

**ADR-004: No ECS for MVP (and probably ever).**
Decision: plain typed classes (`Player`, `Npc`, `Warp`, `Trigger`, `Pickup`) sharing small components by composition (`Mover`, `SpriteAnimator`). Context: <200 entities/map, deeply heterogeneous behavior, grid logic. Alternatives: full ECS (solves problems we don't have; hurts debuggability and AI-agent code clarity). Consequences: simpler code, easier review. Risks: entity-type proliferation later — revisit only if a phase demonstrably needs it; record in a new ADR.

**ADR-005: Fixed 60 Hz timestep with render interpolation.**
Decision: accumulator loop, sim at exactly 60 Hz, visuals interpolate. Context: determinism is required for golden battle replays and movement tests. Alternatives: variable dt (non-deterministic, tuning pain for grid movement). Consequences: all sim code takes ticks, not seconds; testable headless. Risks: spiral-of-death on stalls — clamp accumulator to 5 ticks/frame.

**ADR-006: JSON source data + compiled binary runtime pack.**
Decision: authoring format = one JSON file per entity (git-diffable, `schemaVersion` everywhere); export compiles to `game.cgmpack` (binary index + zstd blobs + interned IDs). Context: humans/agents/git edit source; runtime wants fast, validated, immutable loads. Alternatives: SQLite project file (poor diffs), JSON at runtime (slow, tamper-prone, no atlasing step). Consequences: a compile step exists and is also the validation gate. Risks: format drift between dev-mode (raw JSON) and pack loading — mitigate: one loader in Core produces `GameDb` from either source; pack is just a container.

**ADR-007: Template-executable export.**
Decision: CI prebuilds `Cgm.Runtime` self-contained exes (debug/release); export = copy template, rename, patch icon/metadata, write `config.json`, drop `game.cgmpack` beside it. Context: end users must not need any SDK. Alternatives: compile-on-export (requires .NET SDK on user machine — disqualifying), embedding pack into the exe (nice-to-have later via appended overlay or single-file bundle). Consequences: runtime is content-agnostic by construction; export is file ops + one resource-patch step. Risks: template/pack version skew — pack manifest carries required runtime version; runtime refuses mismatches with a clear error.

**ADR-008: Battle logic (and all game rules) live in Cgm.Core.**
Decision: battle engine, movement rules, capture/exp/evolution math, save model — all in Core, headless, deterministic, injected RNG. Context: same code must run under Creator playtest, exported runtime, and xUnit. Alternatives: rules in Runtime (untestable without a window; Creator would duplicate). Consequences: Core is the crown jewel; ~90% of gameplay is CI-testable. Risks: Core sprouting UI/GL deps — enforce with an architecture test (Core references only BCL + serialization).

**ADR-009: Creator never owns runtime game logic.**
Decision: Creator edits data and *launches* the runtime process for playtest (dev mode reading the raw project folder); it never simulates gameplay in-process. Context: prevents two diverging implementations and keeps the separation constraint honest. Alternatives: embedded playtest viewport (tempting, deferred; if ever done it hosts the actual Runtime, not a re-implementation). Consequences: playtest = spawn `Cgm.Runtime --project <path> --spawn map:tile`; clean process isolation; a Creator crash never corrupts a play session and vice versa. Risks: slower playtest startup — acceptable (<2s); mitigate with dev-mode lazy asset loading.

---

## 3. Scope Tightening

**Phase 1 only:** solution scaffold, docs, CI, empty Avalonia shell, runtime clear-color window, FixedStepClock + its test. Nothing else.

**MVP required (end of Phase 9-ish):** manual+grid sprite slicing; tileset (solid/grass only); one-map editor with brush/fill/collision/encounter paint/warps/NPC-dialogue; grid movement; dialogue box; Battle v0–v2 (damage, type chart, accuracy, crits, potion, capture); exp/level/level-up learnset; party of 6 (no boxes UI yet — auto-deposit silently); save/load; playtest; export.

**Vertical slice required:** Battle v3–v4 (trainers, switching, statuses, stat stages, priority); storage box UI; bag pockets; marts/money; evolutions (level, item, happiness, time-of-day); day/night clock; ledges; trainer sight-line (instant version); audio; validation dashboard; demo game; export smoke + clean-machine test.

**Current Core completion target (Phase 15):** every move in the locked local PokeAPI move corpus
must normalize, validate, compile, and behave correctly using reusable data-driven primitives.
Abilities, held items, weather, terrain, rooms, forms/gimmicks, and doubles Core topology are built
when required for move correctness. Every primitive must be reusable by custom-authored moves.

**Later product work:** Runtime/Creator integration, move presentation, audio, original demo
content, production export, eventing, surf/fishing, connected maps, controller support, bulk
editing, and the private-use import wizard land in Phases 16-19 per IMPLEMENTATION_PLAN v3.

**Still deferred/refused:** procedural generation; breeding/eggs; localization framework;
macOS/Linux; multiplayer/trading/netcode; plugin API; scripting language; official content packs.
The local PokeAPI corpus remains design-time mechanics reference and is never shipped.

---

## 4. First 30 Days Execution Plan

**Week 1 — Toolchain lock & scaffold.**
Goal: everything compiles, CI green, stack risks retired. Deliverables: solution (Core/Creator/Runtime/Core.Tests) on .NET 10; Avalonia shell opens; Silk.NET window clears color at fixed 60 Hz; spike doc confirming Avalonia+Silk.NET on .NET 10. Files: `CreatureGameMaker.sln`, `src/*`, `.github/workflows/ci.yml`, `docs/TECH_STACK.md` (final), `AGENTS.md`, `CODING_STANDARDS.md`, `SCOPE_GUARD.md`, `README.md`. Tests: FixedStepClock tick-count test; Core-has-no-UI-deps architecture test. Done: fresh clone → `dotnet test` green, both apps launch. Don't build: any editor UI, any rendering beyond clear. Failure mode: package-version yak-shaving on .NET 10. Mitigation: timebox 1 day; drop to .NET 9 per §1 fallback and move on.

**Week 2 — Data schema freeze (paper) + Core skeleton.**
Goal: DATA_SCHEMA.md fully written and reviewed *before* typing schema code. Deliverables: DATA_SCHEMA.md v1 (every MVP entity, field-by-field, ID grammar, versioning policy); Core: `EntityId`, `SchemaVersion`, `Project` root model, JSON load/save for project + 3 pathfinder entities (type chart, item, move-basic). Tests: ID parse/format property tests; round-trip serialization; unknown-field tolerance. Done: hand-written fixture project loads and re-saves byte-stably. Don't build: creature/map/trainer schemas in code (paper only), editors. Failure: schema bikeshedding forever. Mitigation: 2-day writing cap, then a decision review; changes after freeze require a migration note.

**Week 3 — Validation framework + remaining MVP schemas.**
Goal: full MVP schema set in code with a working validator. Deliverables: species/map/tileset/spritesheet/encounter/trainer/save models; `IValidationRule` framework + first 10 rules (broken refs, weights, stat bounds, warp targets, start-map exists); CLI `Cgm.Tools validate <project>`. Tests: one test per rule (pass + fail fixture); fixture project `samples/fixture-min/` committed. Done: `validate` reports 0 errors on fixture, correct errors on 5 broken fixtures. Don't build: any UI, any rendering. Failure: rules coupled to UI concerns. Mitigation: rules take `Project`, return `ValidationIssue` — enforced by living in Core.

**Week 4 — Creator shell v1 (pathfinder editors).**
Goal: prove the whole MVVM pattern on the easiest editors so every later editor is a copy of a known shape. Deliverables: project new/open/save; left nav from project tree; tabbed documents; undo/redo command stack; validation strip wired to Week-3 validator (debounced); type-chart grid editor + item editor + basic move editor fully working. Files: `src/Cgm.Creator/**`, `docs/CREATOR_APP_SPEC.md` updated to match reality. Tests: ViewModel-level tests for undo/redo and dirty tracking; validator integration test. Done: create a project in the UI, edit a type chart, break a reference, see the error, undo, save, reopen. Don't build: slicer, map editor, any Runtime work. Failure: undo system rabbit hole. Mitigation: command-pattern with whole-document snapshots for small entities (optimize later, per-field commands only for maps).

---

## 5. Repo Structure

```
CreatureGameMaker/
├─ CreatureGameMaker.sln
├─ README.md
├─ .gitignore                     # ignores exports/, .cgm-cache/, bin/, obj/
├─ .github/workflows/ci.yml
├─ docs/
│  ├─ MASTER_PLAN.md
│  ├─ ARCHITECTURE_ADDENDUM.md    # this file
│  ├─ AGENTS.md … SCOPE_GUARD.md  # the 15-doc set
│  ├─ adr/                        # ADR-001.md … numbered, append-only
│  └─ reference/pokeapi-results/  # DEV-ONLY mechanics reference; excluded
│                                 # from all release/export artifacts
├─ src/
│  ├─ Cgm.Core/                   # schemas, IDs, validation, battle, saves,
│  │                              # pack format, loop clock. NO UI/GL deps.
│  ├─ Cgm.Creator/                # Avalonia app (Views/ ViewModels/ Services/)
│  ├─ Cgm.Runtime/                # engine: Platform/ Render/ Audio/ Scenes/ Ui/
│  └─ Cgm.Tools/                  # CLI: validate, compile-pack, export, smoke
├─ tests/
│  ├─ Cgm.Core.Tests/             # unit + golden/ (battle replays, Verify files)
│  ├─ Cgm.Runtime.Tests/          # headless-testable runtime pieces
│  └─ fixtures/                   # PNG sheets, broken projects, old saves
├─ samples/
│  ├─ fixture-min/                # minimal valid project (test target)
│  └─ demo-game/                  # the golden game (vertical slice content)
└─ templates/                     # CI-built runtime template exes land here
                                  # for local export testing (gitignored blobs)
```

Where NOT to put things: no game logic in `Cgm.Runtime` that isn't rendering/IO glue (rules go in Core); no schemas defined in Creator ViewModels; no test fixtures inside `src/`; no official Pokemon assets anywhere in the repo, including tests; no generated packs/exports committed (only their manifests' schema docs); `docs/reference/` never copied by the export compiler.

---

## 6. Initial Interfaces And Contracts (shape only)

- **FixedStepClock** (Core): `Advance(elapsedMs) → int ticksDue` (accumulator, clamp 5), `TickRate = 60`, `InterpolationAlpha`. Pure, no time source — caller feeds elapsed time; tests feed synthetic time.
- **Project model** (Core): `Project { Settings, IReadOnlyDictionary<EntityId, IEntity> per category }`; `ProjectLoader.Load(folder)`, `Save(folder)`; entities immutable records; edits produce replacements (undo = keep old record).
- **EntityId**: `category:slug` — slug `[a-z0-9_]+`, category from a closed enum-like registry; `EntityId.Parse/TryParse`, value-equal, sortable. Renames forbidden; display names are data.
- **Validation**: `IValidationRule { Id, Severity, IEnumerable<ValidationIssue> Check(Project) }`; `ValidationIssue { RuleId, Severity, EntityId?, Message, FixHint? }`; `Validator.Run(project, ruleSet)`. Rules are stateless; registered in one catalog.
- **Asset manifest** (project side): `assets/manifest.json` — `{ path, contentHash, importKind, importedAtUtc }` per file; slicer metadata references assets by hash+path so re-imports are detected.
- **Slice metadata**: `SpriteSheetDef { AssetRef, Mode(Grid|Rects), CellW/H, OffsetX/Y, SpacingX/Y, Cells[{ Index|Rect, SpriteId, Tags }] }` stored in `derived/`; sprites are *projections* of this metadata, not copies.
- **Runtime config** (`config.json` beside exe): `{ GameName, WindowTitle, VirtualWidth/Height, SaveDirName, PackPath, Debug flags }`. Runtime fails fast with a friendly dialog if missing/invalid.
- **Game pack manifest** (inside `.cgmpack` header): `{ PackFormatVersion, RequiredRuntimeVersion, GameName, BuildTimestamp, ContentHash, Section index[type, offset, length, codec] }`. Loader verifies versions + hash before touching content.
- **Renderer boundary** (Runtime): `IRenderer { BeginFrame(camera), DrawSprite(texHandle, srcRect, worldPos, layer, flip), DrawTilemapChunk(handle), DrawUiQuad/Text(...), EndFrame() }` + `ITexture LoadTexture(bytes)`. Game/scene code never sees a GL type — this is the seam for a future backend and for headless tests (null renderer).
- **Input action map**: `enum GameAction { Up, Down, Left, Right, Confirm, Cancel, Menu, Run }`; `IInputSource.Poll() → InputState { IsDown/WasPressed per action }`; bindings data-driven from config; sim consumes `InputState` only (replayable).
- **Save versioning**: `SaveFile { SaveFormatVersion, GameContentHash, PlayerState }`; `SaveMigrator.Migrate(json, fromVersion) → latest`; loading an unknown-newer version fails safely with backup preserved; every write keeps `save.bak` of the previous file.
- **Battle module boundary** (Core): `BattleController.Create(BattleRequest, IRng) → controller`; `Submit(sideId, BattleAction) → ActionResult(accepted|rejected reason)`; `AdvanceUntilInputNeeded() → IReadOnlyList<BattleEvent>`; `State` (read-only snapshot); `Outcome?`. UI/AI are pure producers of `BattleAction` and consumers of `BattleEvent` — no other surface exists.

---

## 7. Documentation Expansion Priority

| Priority | Doc | Must be fully written | Must contain / lock | Blocks phases |
|---|---|---|---|---|
| 1 | **TECH_STACK.md** | Week 1 | Pinned versions, forbidden list, .NET 10 decision | All |
| 1 | **AGENTS.md** | Week 1 | Reading order, build/test commands, definition of done, "specs win over code," no-new-deps rule | All |
| 1 | **SCOPE_GUARD.md** | Week 1 | §3 tables verbatim; append-only ideas list; per-phase not-yet lists | All |
| 1 | **CODING_STANDARDS.md** | Week 1 | RNG-injection rule, Core-purity rule, immutability of entities, undo pattern, test-per-rule policy | All |
| 2 | **DATA_SCHEMA.md** | Week 2 (frozen before schema code) | Field-by-field MVP schemas, ID grammar, `schemaVersion` policy, migration rules | 2+ (everything touching data) |
| 2 | **TESTING_STRATEGY.md** | Week 2 | Golden-file workflow (Verify), fixture conventions, determinism rules | 2+ |
| 3 | **ARCHITECTURE.md** | Week 3 | Module boundaries, ADR index, §6 contracts, dependency rules diagram | 3+ |
| 3 | **CREATOR_APP_SPEC.md** | Week 4 (shell + pathfinder editors sections), rest incrementally per editor | Undo semantics, validation strip behavior, editor template pattern | 3–5 |
| 4 | **ENGINE_RUNTIME_SPEC.md** | Before Phase 6 | Loop timing, renderer contract, virtual resolution, input map, scene stack | 6–7 |
| 4 | **ASSET_PIPELINE_SPEC.md** | Before Phase 4 | Slicing layer algorithms (§9), metadata format | 4, 12 |
| 4 | **MAP_EDITOR_SPEC.md** | Before Phase 5 | Layer semantics, tool behaviors, entity params | 5 |
| 5 | **BATTLE_SYSTEM_SPEC.md** | Formula appendix before Phase 8; effect-op catalog before Battle v4/v5 (incremental, versioned per battle layer) | Exact formulas with rounding order, event catalog, AI scoring | 8–11 |
| 5 | **EXPORT_PIPELINE_SPEC.md** | Before Phase 12 | Pack binary layout, template patch process, smoke contract | 12 |
| ongoing | **PROJECT_OVERVIEW.md** | Week 1 draft, low churn | Vision, loop, legal boundary | Onboarding only |
| ongoing | **IMPLEMENTATION_PLAN.md** | Week 1, updated at every phase gate | Phase status, deviations log | Process |

Rule: an agent may not start a phase whose blocking docs are stubs.

---

## 8. Battle System Scope Correction (layered)

Each layer ends with green goldens; a layer's exclusions are hard errors if found in its PR.

- **v0 — Damage only.** Data: species (stats), move (power/type ignored, class), level; flat 100% accuracy, no types. Systems: BattleState, action submit/validate, turn order by Speed, damage formula core, faint, win/loss, event stream. Tests: damage table vs hand-computed values; first golden replay. Done: headless 1v1 fight runs to completion from an action script. Excludes: type chart, crits, accuracy, items, everything else.
- **v1 — Type chart, accuracy, crits.** Data: type chart entity, move accuracy/critStage, STAB. Systems: effectiveness multiplier (incl. ×0 immunity), accuracy roll, crit roll + stage-ignore rules, 85–100 damage roll. Tests: effectiveness matrix tests; statistical crit/accuracy tests over seeded runs; goldens updated intentionally. Done: calculator-cross-checked values match. Excludes: statuses, stages, items.
- **v2 — Items and capture.** Data: item effects (heal, capture ballBonus), catch rate. Systems: item action path, capture formula + shake events, party/box routing, exp/level-up/learn on win. Tests: capture distribution (10k seeded rolls within tolerance); heal clamping; exp table tests. Done: wild loop catch/faint/run complete. Excludes: trainer battles, statuses. **← End of MVP battle scope.**
- **v3 — Trainer battles and switching.** Data: trainer entity (party, dialogue, money), aiProfile `basic`. Systems: no-run/no-catch rules, switch action + free-switch-on-faint, `basic` greedy AI, rewards/flags. Tests: AI picks max-damage move deterministically; switch legality; goldens ×5. Done: full trainer fight with mid-battle switches. Excludes: `smart` AI, statuses.
- **v4 — Statuses and stat stages.** Data: persistent statuses on creature instance; move effects `ailment`, `statStage`; priority field live. Systems: brn/psn/tox/par/slp/frz with exact Gen 3/4 behavior, stage math −6..+6, accuracy/evasion stages, priority brackets, end-of-turn residuals. Tests: full status matrix (apply/block/persist-after-battle/cure); stage multiplier table; priority ordering goldens. Done: BATTLE_SYSTEM_SPEC status appendix matches implementation exactly. **← End of vertical-slice battle scope.**
- **v5 — Advanced effect ops.** Data: effect-op palette extension (multiHit, drain, recoil, flinch, chargeTurn, protect, hazards, fixedDamage, ohko, forceSwitch, weatherSet — mechanics only). Systems: resolver pipeline generalization, volatile-state store. Tests: one test file per op; goldens per batch of ~5 ops. Done: 30-move demo set expressible purely in data. Excludes: abilities, held items.
- **v6 / Phase 15 — Complete Core mechanics and move conformance.** The implemented
  ability/held-item/weather/form hook foundations expand only through reusable primitives required
  by the 937-entry move corpus. Systems include generalized singles/doubles topology, target
  resolution, ordered effects, scoped conditions, queued intents, query hooks, item/ability/type/
  form mutation, move references, snapshot overlays, damage memory, switch flow, ruleset policies,
  deterministic events, and conformance traces. Tests include per-primitive units, per-move
  compile/resolve cases, family goldens, and seeded singles/doubles fuzzing. Done means 937/937
  certified, 0 unsupported, and 0 move-name/ID branches. Excludes: final UI/presentation, breeding,
  official content packs, and netplay.

---

## 9. Asset Pipeline Scope Correction (layered)

- **v0 — Manual grid slicing.** Feature: user enters cellW/H/offset/spacing; grid overlay; per-cell include + naming. Algorithm: pure arithmetic. Fixtures: one clean 16px sheet. Validation: cells within image; unique sprite IDs. Excludes: all auto-detection. **← Phase 4 minimum; everything else can technically wait.**
- **v1 — Common-size suggestions.** Feature: one-click suggestions ranked by confidence. Algorithm: test divisibility by {16,32,48,64} on both axes; prefer largest divisor; tie-break toward project tileSize. Fixtures: sheets at each size + a non-divisible sheet (must yield "no suggestion"). Validation: suggestion never overrides manual values without click. Excludes: pixel inspection.
- **v2 — Transparent gutter detection.** Feature: infer cell/spacing/margin from fully-transparent row/column bands. Algorithm: alpha-projection histograms per axis → band runs → periodicity fit. Fixtures: guttered sheets (1px/2px gutters, with margin), a gutterless sheet (must fall through to v1). Validation: reject fits explaining <90% of bands. Excludes: irregular sprites.
- **v3 — Connected-component irregular slicing.** Feature: suggest rects for non-grid sheets. Algorithm: flood-fill opaque components → bounding boxes → merge boxes overlapping/within 2px → optional snap. Fixtures: mixed-size prop sheet; noisy sheet with stray pixels (min-area threshold). Validation: overlapping suggested rects flagged; user must confirm each. Excludes: auto-naming, auto-classification.
- **v4 — Animation grouping helpers.** Feature: ordered multi-select → clip; 4-dir×3-frame character template auto-clips; per-frame ms editing; preview player. Algorithm: index arithmetic on the template. Fixtures: standard character sheet. Validation: clip frames all exist; nonzero durations. Excludes: onion-skinning, retiming tools.
- **v5 — Atlas packing + pack integration.** Feature: export-time packing into ≤2048² atlases per category; rect rewrite; zstd into `.cgmpack`. Algorithm: skyline/MaxRects (simple skyline is fine at this scale). Fixtures: project overflowing one atlas (must split correctly). Validation: no rect overlap; every referenced sprite present in exactly one atlas; hash check in smoke test. Excludes: runtime dynamic atlasing, mipmaps, compression formats beyond raw RGBA-in-zstd.

---

## 10. Export Pipeline Reality Check

The honest path, in order of what exists when:

1. **Dev-mode runtime (exists from Phase 6/7):** `Cgm.Runtime.exe --project <folder>` loads raw JSON + PNGs directly. This is playtest, and it means "export" is *not* on the critical path to a playable game — a major de-risking fact.
2. **Prebuilt runtime template (Phase 12):** CI runs `dotnet publish -c Release -r win-x64 --self-contained /p:PublishSingleFile=true` for debug & release flavors → `templates/`. The Creator ships these bytes; the user's machine never compiles anything.
3. **Project data pack:** export runs validation (hard gate) → compiles data + atlases + audio into `game.cgmpack`.
4. **Icon/name metadata:** copy template → `<GameName>.exe`; patch icon + version resources with a resource-editing step (managed PE resource writer; if patching single-file bundles proves fragile, fallback: publish template *without* single-file and patch the apphost — decided by a Phase 12 spike). Write `config.json`.
5. **Debug vs release:** two templates; debug enables overlays/console/free-warp; release strips them and disables `--project` raw-folder mode.
6. **Smoke test:** Creator launches `<GameName>.exe --smoke`: boot → verify pack manifest/hash → load start map → instantiate battle system with first species → write+read a temp save → exit 0. Non-zero or timeout = export marked failed.
7. **Clean-machine test (release ritual, manual/VM):** copy the export folder to a Windows VM with no dev tools; run; play 2 minutes; save; relaunch; load. Do this at every phase-12+ milestone.
8. **Developer machine needs:** .NET 10 SDK, git. **End-user machine needs: nothing** — self-contained publish bundles the runtime; no .NET install, no VC++ redist (OpenGL via OS drivers, OpenAL via bundled soft_oal.dll).

---

## 11. Risk Register Upgrade

| Risk | Sev | Prob | Mitigation | Early warning sign | Owner/Phase |
|---|---|---|---|---|---|
| Custom renderer rabbit hole | High | Med | GL 3.3 core only; `IRenderer` seam; feature list closed (sprites/tiles/UI, nothing else) | Shader files multiplying; "lighting" appears in a PR | Phase 6 |
| Editor canvas complexity (slicer/map) | High | High | Build ONE reusable canvas control (Week ~5); virtualize/chunk; pathfinder editors first (Week 4) | Map editor lags on 100×100 map; canvas code copy-pasted between editors | Phases 4–5 |
| Battle mechanics explosion | **Critical** | High | Layered v0–v6 (§8); formulas frozen in spec before each layer; goldens; effect-op palette closed per layer | A move implemented as bespoke code instead of ops; goldens changing "accidentally" | Phases 8–11 |
| Export/build fragility | High | Med | Template approach; smoke test mandatory; icon-patch spike early in Phase 12; clean-VM ritual | Export works only on the dev machine; smoke test skipped "temporarily" | Phase 12 |
| Save compatibility breaks | Med | Med | Version+migrator from first save; old-save fixtures in CI; never delete fields | A PR edits `PlayerState` without a migration note | Phase 9+ |
| AI-generated architecture drift | High | High | AGENTS.md reading order; specs win; Core-purity architecture test; ADRs append-only; review prompts (§12) run after every phase | New NuGet package in a PR; Runtime code doing rules math; duplicate helper classes | Continuous |
| Data schema churn | High | Med | Paper freeze (Week 2) before code; `schemaVersion` + migration policy; changes require DATA_SCHEMA.md diff in same PR | Editors breaking on fixture project; fixtures edited to match code instead of vice versa | Phase 2+ |
| Asset import edge cases | Med | High | Layered v0–v5 with manual override always available; fixture-driven; heuristics capped | Slicer bug reports blocking map work | Phase 4 |
| Performance assumptions wrong | Low | Low | Budget test (60 fps on 100×100 map, 50 entities) as a CI-adjacent check; profile only on failure | Frame-time test starts flaking | Phase 6–7 |
| Windows-only blind spots | Low | Med | Keep Silk.NET/Avalonia cross-platform surfaces; no Win32 P/Invoke outside export module; path handling via BCL | Hardcoded backslashes; registry usage | Continuous |
| IP/legal | High | Low | No official assets in repo/tests/releases; pokeapi reference dir excluded from artifacts; neutral branding; demo game 100% original | Official sprite in a test fixture "temporarily" | Continuous |
| Solo-dev burnout | **Critical** | Med | Phases sized to ship a visible win each; dev-mode playable long before export; SCOPE_GUARD absorbs new ideas; vertical-slice polish timeboxed to 4 weeks; celebrate goldens | A phase open >6 weeks; commits stop; enthusiasm channeled into new features not the current phase | Continuous |

---

## 12. Implementation Prompts

**Prompt A — GPT 5.5: Phase 1 scaffold.**
> You are implementing Phase 1 of Creature Game Maker. Read `docs/MASTER_PLAN.md` and `docs/ARCHITECTURE_ADDENDUM.md` fully first; the addendum wins on conflicts. Scope is EXACTLY Addendum §4 Week 1: .NET 10 LTS solution with `src/Cgm.Core`, `src/Cgm.Creator` (Avalonia 11 empty shell: nav placeholder, tab area, status bar), `src/Cgm.Runtime` (Silk.NET window, 960×640, fixed 60 Hz accumulator loop with 5-tick clamp, clear-color, Esc exits), `src/Cgm.Tools` (empty CLI stub), `tests/Cgm.Core.Tests`. Create the repo structure from Addendum §5 exactly, including docs stubs; fully write AGENTS.md, CODING_STANDARDS.md, SCOPE_GUARD.md, TECH_STACK.md from plan+addendum content. CI: GitHub Actions windows-latest, build + test. Required tests: FixedStepClock produces exactly N ticks for synthetic elapsed time including clamp behavior; architecture test asserting Cgm.Core references no UI/graphics assemblies. Allowed packages ONLY: Avalonia, Silk.NET.Windowing/Input/OpenGL, xUnit, Verify — pin versions in TECH_STACK.md. If any package fails on .NET 10, follow Addendum §1 fallback (.NET 9) and document it in TECH_STACK.md. Do NOT build: editors, schemas, rendering beyond clear, game logic. Definition of done: fresh clone → `dotnet build` + `dotnet test` green, both apps launch and close cleanly, CI green, no TODOs referencing future phases. Finish by reporting deviations from this prompt.

**Prompt B — Opus 4.8: scaffold review.**
> You are reviewing the Phase 1 scaffold of Creature Game Maker. Read `docs/MASTER_PLAN.md`, `docs/ARCHITECTURE_ADDENDUM.md`, then the entire repo. Verify: (1) repo matches Addendum §5 exactly — list deviations; (2) no forbidden dependencies (any engine/framework per TECH_STACK.md forbidden list) and no packages beyond the allowed set; (3) Cgm.Core has zero UI/GL references and the architecture test enforces it; (4) FixedStepClock is pure (no time source, no statics) and its tests cover clamp behavior; (5) the runtime loop matches ADR-005; (6) AGENTS/CODING_STANDARDS/SCOPE_GUARD/TECH_STACK are complete, consistent with the addendum, and would actually constrain a future agent; (7) no Phase 2+ code smuggled in. Do not add features. Output: a findings list ordered by severity with file references, each finding tagged FIX-NOW / FIX-PHASE-2 / ACCEPT, and a verdict on whether Phase 2 may start. Update `docs/IMPLEMENTATION_PLAN.md` with the review outcome.

**Prompt C — GPT 5.5: Phase 2 project format & validation.**
> You are implementing Phase 2 (project format + validation) of Creature Game Maker per `docs/MASTER_PLAN.md` Phase 2 and `docs/ARCHITECTURE_ADDENDUM.md` §4 Weeks 2–3 and §6 contracts. Prerequisite: `docs/DATA_SCHEMA.md` must be fully written first — write it (all MVP entities field-by-field, EntityId grammar, schemaVersion/migration policy per addendum), get it internally consistent, THEN implement. Implement in Cgm.Core only: EntityId, Project model, ProjectLoader (folder JSON per Addendum §5/§6), all MVP schemas (project settings, spritesheet, sprite, animation, tileset/tile, map, encounter table, species, move, item, type chart, trainer, save file shell), IValidationRule + Validator + ≥10 rules from MASTER_PLAN §4, and `Cgm.Tools validate <path>`. Create `samples/fixture-min/` (valid) plus ≥5 broken fixture variants under `tests/fixtures/`. Required tests: round-trip byte-stable serialization per schema; EntityId property tests; one pass+fail test per validation rule; unknown-field tolerance; a v0→v1 migration walkthrough test proving the migration mechanism works. Scope limits: NO UI, NO rendering, NO battle logic (schemas only), NO pack/binary format yet, no schema fields for post-slice features beyond the empty `forms[]` placeholder. Update DATA_SCHEMA.md and IMPLEMENTATION_PLAN.md in the same change. Done: `Cgm.Tools validate samples/fixture-min` → 0 errors; broken fixtures produce exactly the expected issues; CI green.

**Prompt D — Opus 4.8: Phase 2 review.**
> Review Phase 2 of Creature Game Maker for correctness and scope creep. Read `docs/MASTER_PLAN.md` §7, `docs/ARCHITECTURE_ADDENDUM.md` §3/§6, `docs/DATA_SCHEMA.md`, then the Phase 2 code and tests. Check: (1) code matches DATA_SCHEMA.md exactly — every field, both directions; flag doc/code divergence as the highest-severity finding; (2) EntityId immutability and grammar enforcement; (3) entities are immutable records suitable for the snapshot-undo pattern in CODING_STANDARDS.md; (4) validation rules are stateless, Core-pure, and each has pass+fail tests; (5) scope creep scan: any battle logic, UI hooks, pack format, or post-slice schema fields (anything from Addendum §3's deferred lists — abilities, weather, held items, breeding, doubles, event scripting) must be flagged for removal, not "kept since it's written"; (6) serialization is byte-stable and unknown-field tolerant; (7) fixtures are honest (not tailored to hide bugs). Output findings by severity (FIX-NOW / FIX-LATER / ACCEPT), a scope-creep verdict, and go/no-go for Phase 3. Append the outcome to IMPLEMENTATION_PLAN.md.

---

## 13. Final Verdict

**Feasible?** Yes — genuinely, not politely — *because* of three structural choices: rules live in a headless, golden-tested Core; the runtime's feature list is closed and small; export is file-copying a prebuilt template, not compilation. Without those, this project would be a multi-year death march. With them, it's an 8–12-month part-time build to a vertical slice.

**Stack still recommended?** Yes, with the one correction applied: **.NET 10 LTS** (not 8),
Avalonia, Silk.NET bindings, StbImageSharp, System.Text.Json, and xUnit. OpenAL, ZstdSharp,
and Verify remain allowed/planned but are not currently referenced; see TECH_STACK.md for the
current installed package list. Nothing on that list is a game engine; everything above the
bindings is this project's code.

**Biggest project-killer:** battle-mechanics scope explosion combined with AI code drift — an agent implementing "just this one move" as bespoke code, fifty times, until the battle system is an untestable swamp. The defenses are the layered v0–v6 plan, the closed effect-op palette, frozen formula specs, golden replays, and the alternating build/review prompt cadence. Enforce them without exception. The second killer is burnout; the dev-mode runtime existing early (playable long before export) is the morale insurance.

**First thing to build:** Week 1 exactly — the .NET 10 scaffold with the FixedStepClock and its test, plus the four priority-1 docs. It retires the only stack unknown (.NET 10 package compatibility) in day one.

**Current focus:** complete Phase 15 Core mechanics and certify every move in the local corpus.
Do not build final Runtime/Creator presentation while Phase 15 is open. Breeding, event scripting,
the private-use PokeAPI import wizard, official content packs, and netplay remain outside Phase 15.
Every move behavior must be composed from reusable primitives; a named move branch is a phase-
blocking architecture defect even if it makes a conformance test pass.

---
*End of addendum.*
