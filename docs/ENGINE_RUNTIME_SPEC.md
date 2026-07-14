# ENGINE_RUNTIME_SPEC

Status: **Implemented baseline; Phase 16A-16G implementation contracts locked in advance.** Headless runtime helpers are written
and tested (`FixedStepClock`, virtual resolution, camera, input state, scene stack, raw-folder
`GameDb`, battle action menu, battle event presenter, exported config/pack boot, Runtime
`--smoke`). The Silk.NET host loads exported `config.json`/`game.cgmpack` and renders a playable
showcase battle with GL clear/scissor rectangles plus a tiny built-in bitmap text drawer. Full
sprite-batch renderer, asset-backed sprites, reusable UI kit, audio, and dev-mode project loading
in the host are **not** implemented.

## Purpose
The engine contract: loop timing, renderer boundary, virtual resolution, input mapping, scene
stack, UI kit primitives, audio, dev-mode data loading, and debug tooling.

## Must lock
- Fixed 60 Hz sim + interpolated render (ADR-005); accumulator clamp (already in FixedStepClock).
- `IRenderer` surface (sprite batch, tilemap chunks, UI quads/text) — game code never sees GL.
- Virtual resolution + integer-scale/letterbox rules; camera clamp; input action map + rebinding.
- UI kit v1 primitives (9-slice, bitmap font, typewriter, cursor, HP bar); scene stack semantics.

## Built, pure & tested (Phase 6 — `Cgm.Runtime.Engine`)
- **Loop**: `FixedStepClock` (Core, Phase 1) — 60 Hz accumulator, 5-tick clamp.
- **Virtual resolution**: `VirtualResolution.Fit(window, virtual) → Viewport` — integer scale,
  centered, letterboxed; scale clamped ≥1.
- **Camera**: `Camera.Clamp(target, view, map)` — centers on target, clamps to map edges, centers
  a map smaller than the view. World coord at the viewport top-left, in pixels.
- **Input**: `GameAction` enum + `InputState` — edge-detected IsDown/WasPressed/WasReleased; the
  platform source feeds `Update(heldNow)`, the sim reads queries (replayable).
- **Scene stack**: `SceneStack<T>` — push/pop/replace/active; only the top scene is active.
- **Data**: dev-mode `GameDb` = `ProjectLoader.Load(folder)` (Core, Phase 2).
- **Battle action presentation**: `BattleScene` builds a player action menu from Core legality,
  including moves, eligible `ActivateForm(formId, moveIndex)` actions, and legal party switches.
  It submits selected actions to `BattleController`, presents `BattleEvent`s through
  `BattleEventPresenter`, and exposes a render snapshot of active creatures, party HP, menu state,
  recent log lines, and outcome.
- **Exported boot/smoke**: `ExportedGameBoot` reads exported `config.json`, verifies the pack
  manifest/runtime version/content hash, loads the configured start map, initializes the showcase
  battle, and smoke-submits one legal action.
- **Window wiring**: `RuntimeHost` still owns the Silk.NET loop and Esc-to-close behavior. When an
  exported config exists beside the executable, it uses the exported window title/resolution, maps
  Up/Down/Confirm keyboard state into `InputState`, updates the showcase battle, and draws a
  readable battle screen with clear/scissor rectangles and built-in bitmap text.

## Phase 16 implementation authority and start gate

`IMPLEMENTATION_PLAN.md` v4 §6 authorizes the contracts below. They were hardened in advance on
2026-07-13 so implementation agents do not invent behavior package-by-package. This documentation
work does **not** advance Phase 16: Phase 15 remains the sole implementation phase until its GO
review freezes the Core state/action/event/trace contracts. All 16A-16G code remains forbidden until
that prerequisite passes.

Packages execute strictly in this order:

`Phase 15 GO → 16A → 16B → 16C → 16D → 16E → 16F → 16G`

Each package must meet its entire acceptance matrix and receive the state transition recorded in
`IMPLEMENTATION_PLAN.md` before the next begins. A normal-path checkpoint is `IN PROGRESS`, not a
new package. Missing Core rules discovered in Phase 16 are Phase 15 regressions: specify and fix
them in Core, then resume the same Phase 16 package. Runtime must never patch around them.

## Phase-wide invariants

### Ownership boundary

| Concern | Owner | Runtime responsibility |
|---|---|---|
| Immutable definitions, IDs, validation, and `GameDb` | Core | Load one validated projection; never repair data |
| Movement, collision, encounters, trainers, flags, inventory, progression, saves, and battle rules | Core | Submit inputs/actions and apply returned operations/results |
| Mutable play-session state | Core models/controllers | Own one session instance and serialize only through the approved save path |
| Window, device input, files, OpenGL/OpenAL, scene flow, and presentation timing | Runtime | Adapt platform events to deterministic Core inputs and present Core output |
| Project authoring and validation UI | Creator | Not present in Runtime or Phase 16 |

- UI and scenes never mutate rule state directly. They submit a complete request to a Core operation
  or controller, then render the accepted result and emitted events.
- Runtime may cache immutable presentation data, decoded assets, and derived render chunks. A cache
  is never the source of game truth and may be rebuilt without changing simulation output.
- Simulation is single-thread-owned. Background work may read immutable bytes, but it may not mutate
  scene, session, Core, input, RNG, renderer, or audio state. Results cross to the main thread at a
  deterministic fixed-tick boundary.
- All simulation randomness uses injected Core `IRng` streams whose state is saved. Rendering,
  audio, and transitions consume no simulation RNG. No wall-clock value enters deterministic state.
- Entity/reference order is ordinal and stable. Filesystem enumeration, dictionary iteration,
  device enumeration, or asynchronous completion order may not decide simulation outcomes.
- Every disposable resource has one owner. Dispose children in reverse creation order, make
  disposal idempotent, and leave no callbacks capable of touching disposed state.
- Runtime contains no original-game content IDs, fallback parties, fallback maps, implicit fixture
  paths, or official-content names. Missing content is an error, never an invitation to substitute.
- Any new serialized project or save shape follows `DATA_SCHEMA.md`, version bump, migration, and
  fixture rules in the same implementation change. Runtime-local options are versioned separately.

### Phase 15 handoff audit required before 16A

Phase 15 GO must expose stable public contracts for battle construction, actions, typed targets,
replacements, outcomes, events, traces, legality, and queries. Before 16A is marked `SPEC READY`,
the implementing agent must also audit these existing world contracts rather than letting Runtime
invent missing behavior:

| Required Core surface | Phase 16 use | Drift response if absent or ambiguous |
|---|---|---|
| Stable map-entity identity/order | NPC updates, trainer selection, persistence, diagnostics | Reopen Core/schema contract; never use incidental object identity or filesystem order |
| Closed world interaction/trigger vocabulary | Dialogue, flags, pickup, service, and battle requests | Reopen Core/schema contract; never execute arbitrary strings or add a script interpreter |
| Battle request/result and overworld mutation boundary | Encounter/trainer entry and return | Reopen Core contract; never reconstruct rewards, blackout, or progression in Runtime |
| Atomic inventory/shop/storage/progression operations | 16D/16E menus | Reopen Core contract; UI may not perform list or money arithmetic itself |
| Save validation/migration and RNG snapshot contract | 16E persistence | Reopen Core/schema contract; Runtime owns file durability only |

Current baseline warning: `MapEntity` has no stable ID and `TriggerEntity.Actions` plus
`MapObject.Interaction` are open strings. Those shapes are not permission for Runtime string
switches. They must be reconciled through the schema/Core workflow before 16D implementation.

## 16A — Content-agnostic host and raw/pack parity

### Objective and boundary

Replace the showcase boot path with one content-neutral startup pipeline. 16A loads and validates
content, constructs shared services, and reaches `BootScene`; it does not open gameplay scenes,
render sprites, create a save, or construct a battle from arbitrary “first” entities.

### Command-line contract

- Development mode: `Cgm.Runtime.exe --project <folder> [--debug]`.
- Exported mode: `Cgm.Runtime.exe [--debug]`, reading adjacent `config.json` and its referenced pack.
- Smoke mode: add `--smoke` to either valid data mode; it executes the same boot pipeline and exits
  without entering the interactive loop.
- The 16D debug spawn grammar is `--spawn-map <entity-id> --spawn-x <int> --spawn-y <int>
  [--spawn-facing <down|up|left|right>]`. The three required spawn arguments are all-or-none, are
  legal only with `--project` and `--debug`, and default facing to the project start facing.
- `--project` is the only public data-source override. The current `--config` escape hatch is removed
  from the final host; tests call the loader directly when they require a non-adjacent fixture.
- Unknown, duplicate, missing-value, or mutually exclusive arguments fail with exit 2. Relative
  project paths resolve against the process working directory. Exported paths resolve against the
  executable/config directory, never the working directory.
- `--debug` changes diagnostics and enables later debug facilities; it never changes validation,
  rules, RNG, content selection, or save behavior.

### Startup state machine

The only legal order is:

1. Parse arguments without creating a window or reading content.
2. Select exactly one data source: explicit project folder or adjacent config plus pack.
3. Canonicalize all roots and reject a missing, wrong-kind, or inaccessible root.
4. Read the project/config/pack header and verify schema, pack, and required Runtime versions.
5. Verify the pack content hash before deserializing sections; raw mode validates the project using
   the same Core validator and severity gate used by pack compilation.
6. Construct the data source and asset source, then materialize the same immutable `GameDb` shape.
7. Resolve and validate required settings, start map/position/facing, referenced entities, and
   startup assets. Do not choose a fallback entity.
8. Return one owned `RuntimeContent`-equivalent aggregate to `BootScene` construction.

Failure stops immediately. Later steps do not run for diagnostic accumulation, and no gameplay
scene, gameplay window, input context, GL context, audio device, or save repository is created after
a boot failure. A minimal release error presenter may open only after the safe diagnostic exists;
it owns no content/session service and closing it returns the categorized exit code.

### Data and asset parity

- Raw and packed loaders are adapters into one canonical `GameDb`; scenes receive no source-mode
  flag and cannot branch on raw versus packed content.
- `IAssetSource` is the justified two-implementation seam. It exposes existence, metadata, and
  read-only byte access by validated content-relative asset key. It does not expose host paths.
- Asset keys use `/` separators, are relative, and reject rooted paths, empty segments, `.`/`..`,
  alternate data streams, and canonical paths escaping the selected root.
- Opening an asset returns a caller-owned stream/lease. Failed opens do not poison later opens.
  Caching begins only in 16B when decoded resources have an owner.
- Database parity compares settings plus every entity by stable ID and canonical serialized value;
  collection order is ordinal by ID. Asset parity compares key, length, and content hash.

### Error and diagnostic contract

| Exit | Category | Examples |
|---:|---|---|
| 0 | Success | Interactive close or successful smoke |
| 2 | Arguments/configuration | Bad CLI, missing config, invalid dimensions/path |
| 3 | Content validation/version | Schema/pack/runtime mismatch, hash failure, broken reference/start state |
| 4 | Asset load | Missing, unreadable, corrupt, or unsupported required asset |
| 5 | Save | Reserved for 16E save/load failure |
| 6 | Runtime initialization | Window, GL, input, or audio initialization failure |
| 10 | Smoke assertion | Boot succeeded but a declared smoke invariant failed |

- Emit exactly one final structured diagnostic to stderr with stable category, exit code, safe
  summary, and optional content-relative identifier. Debug mode may append exception type and stack
  detail; release output never exposes host paths or a stack trace.
- The release error surface shows the same safe summary and recovery hint. It does not create a
  gameplay scene or silently continue.
- Expected content/IO failures are translated at the boundary. Do not catch process-corrupting
  exceptions or use a blanket catch that labels programmer defects as bad user content.

### Lifetime and acceptance matrix

- Own resources in a single startup stack and transfer ownership only after the full aggregate is
  valid. Partial failure disposes only successfully created children, once, in reverse order.
- `ExportedGameBoot` and `RuntimeHost` may be reshaped or deleted; their showcase-specific behavior
  is not a compatibility contract.

| Required evidence | Minimum cases |
|---|---|
| CLI table | Each valid mode; unknown/duplicate/missing/conflicting arguments; relative project root |
| Version/validation table | Missing config/pack/project; newer/older schema policy; bad hash; missing start map; invalid start tile |
| Parity | Canonical `GameDb` and asset inventory equality for the neutral raw/pack fixture |
| Security | Rooted/traversal/mixed-separator asset keys; config pack escape; redacted diagnostics |
| Lifetime | Failure injected after each acquired resource; reverse, once-only disposal |
| Content neutrality | Automated scan finds no demo/official IDs, fallback construction, or fixture paths |
| Smoke | Raw and packed smoke success plus exit 10 assertion failure |

Exit: both sources reach the same `BootScene` input aggregate with no content assumptions.

## 16B — Fixed-step host and renderer

### Host-loop contract

One outer frame performs this order:

1. Poll platform events and devices once.
2. Feed elapsed monotonic milliseconds to `FixedStepClock`.
3. Merge keyboard/gamepad state and buffer press/release edges.
4. Execute zero through five fixed ticks. The first due tick receives all pending edges once;
   later ticks in the same outer frame receive current held state without repeated edges.
5. Apply queued scene-stack mutations only at the fixed-tick boundary.
6. Render exactly once using the clock interpolation alpha, then present.

Zero-tick frames still poll and render; edges remain buffered. If more than five ticks are due, the
clock applies its existing clamp and reports the dropped backlog to diagnostics without replaying it
later. Minimize/restore, debugger stalls, device reset, and resize stalls reset the accumulator and
interpolation history before the next tick. Simulation never receives render delta or wall time.

### Coordinate and viewport contract

- Logical UI and world units are virtual pixels with origin at top-left, +X right, +Y down.
- Tile positions convert through the project `tileSize`. Camera coordinates are the world pixel at
  the virtual viewport top-left. Rendering subtracts the camera; Core positions stay tile-based.
- Default virtual size is 240×160. The scale is the greatest positive integer that fits both axes.
  If the client is smaller, scale remains 1 and the virtual image is symmetrically clipped.
- Odd remainder pixels put the extra pixel on the right/bottom. Letterbox pixels are opaque black.
  Mouse mapping is not part of Phase 16.
- World interpolation reads the previous/current presentation snapshot and alpha. It never writes
  back to `GridMover`, scene state, or Core state.

### Renderer boundary

The Runtime owns one `IRenderer` implementation over Silk.NET OpenGL 3.3. Its surface covers:

- frame begin/end with viewport, clear color, camera, and diagnostics;
- texture creation from validated RGBA atlas bytes and idempotent disposal;
- sprite submission with texture, source rectangle, destination, layer, flip, tint, and sequence;
- tile-chunk submission through the same textured-quad pipeline;
- UI quad/glyph submission in virtual coordinates;
- nested scissor push/pop with intersection and automatic frame reset.

No scene sees `GL`, buffer handles, shader handles, texture handles beyond opaque Runtime leases, or
platform coordinates. Bitmap text expands to glyph quads; it is not a second renderer.

### Batching, blending, and resources

- Use one dynamic quad batch. Initial capacity is 2,048 quads; grow to the smallest power of two that
  fits the largest observed submission. Do not shrink or add pooling without a failed measurement.
- Record a monotonically increasing submission sequence. At frame end, stable-order by numeric layer
  then sequence. Flush on texture, capacity, layer, or scissor change. Equal-layer ordering is exact
  call order; renderer never guesses Y sorting.
- Source rectangles are integer atlas pixels using top-left semantics. Half-open bounds must remain
  inside the texture. Invalid rectangles fail asset loading, not the draw loop.
- Textures use nearest-neighbor min/mag sampling, clamp-to-edge, no mipmaps, no rotation, and no
  dynamic atlasing. Blend is premultiplied alpha: `ONE, ONE_MINUS_SRC_ALPHA`.
- One fixed textured/color-quad shader pair is permitted. Lighting, post-processing, custom content
  shaders, render graphs, cameras beyond the one 2D projection, and alternate backends are excluded.
- GL resources are created and destroyed on the context-owning thread. Context loss is a controlled
  Runtime initialization failure (exit 6); Phase 16 does not rebuild a lost context.
- Dispose scene-owned leases before shared atlases, then renderer buffers/programs, GL wrapper,
  input context, and window. Repeating disposal is harmless.

### Frame diagnostics and acceptance matrix

Per-frame debug metrics are ticks due/run/dropped, alpha, submitted quads, draw calls, flush reason
counts, texture count/bytes, update/render duration, and managed allocation delta. Collection may be
compiled out or hidden in release, but enabling it cannot alter submission or simulation order.

| Required evidence | Minimum cases |
|---|---|
| Loop table | 0/1/5/overflow ticks; edge on zero-tick frame; multiple ticks; suspend/resize reset |
| Viewport table | Exact fit, each letterbox axis, odd remainder, smaller client clipping, invalid virtual size |
| Command golden | World/UI coordinates, camera subtraction, flips, scissor intersection, stable layer/sequence |
| Batch table | Texture/capacity/layer/scissor flushes; 2,048 boundary and power-of-two growth |
| Texture failures | Bad dimensions/length/source rect; missing atlas; double disposal |
| GL smoke | Hidden neutral fixture screenshot at multiple integer scales |
| Lifetime | 100 atlas/map load-unload cycles with zero live GL-object delta |
| Determinism | Headless state/event replay byte-identical with rendering enabled and disabled |

Exit: the neutral animated fixture map renders at integer scale through `IRenderer`, with no GL
leak and no change to headless simulation output.

## 16C — UI kit, input, and scene flow

### Scene-stack contract

Every scene implements `Enter`, fixed `Update`, `Render`, `Exit`, and `Dispose` semantics:

- `Enter` runs once after the scene becomes stack-owned and before its first update/render.
- Only the top scene receives input and fixed updates. Covering a scene does not call `Exit`.
- `Exit` runs once when a scene is popped/replaced or the stack shuts down, before `Dispose`.
- Stack mutations requested during update are queued and applied after that tick; callbacks cannot
  re-enter lifecycle methods.
- Render begins at the highest scene that does not declare itself an overlay, then renders upward.
  Covered scenes below that point are not rendered.
- A scene owns its transient UI/presentation resources. Shared content/renderer/audio leases are
  borrowed and outlive scenes.

The locked normal flow is Boot → Title → New/Continue → Overworld. Menu is an overlay push. Battle
replaces Overworld presentation while preserving the session object and returns through a typed
result. Transition input is blocked. Default full-scene fade is 15 fixed ticks out, perform the
queued scene/content switch, then 15 ticks in. Loading occurs between ticks, never in `Render`.

### Input contract

- Actions are Up/Down/Left/Right/Confirm/Cancel/Menu/Run and debug-only DebugToggle.
- Keyboard defaults: arrows and WASD for directions, Enter/Z for Confirm, Escape/X for Cancel, C for
  Menu, Shift for Run, and F3 for debug-only DebugToggle. They do not vary by scene.
- First connected gamepad only: D-pad/left stick directions, south face Confirm, east face Cancel,
  Start Menu, west face Run, and Back DebugToggle in debug builds. Stick deadzone is 0.5.
- Keyboard and gamepad merge by action. Opposite directions on one axis cancel. When two perpendicular
  directions are held, the most recently pressed direction wins; exact same-frame ties use ordinal
  Up, Down, Left, Right. This resolver is recorded in replay input.
- Press/release edges survive zero-tick frames and are delivered once. Disconnect releases that
  device's held actions on the next tick and retains its binding profile for reconnection.
- Rebinding is per device. Duplicate bindings are rejected unless the user explicitly swaps them.
  Confirm and Cancel always retain a recovery chord/default. Invalid or unreadable options revert to
  defaults with one warning; they never prevent boot.

### UI kit contract

The only Phase 16 primitives are 9-slice panel, bitmap text, typewriter text, cursor, vertical list,
grid, prompt/choice, HP/resource bar, fade, and message log.

- Layout uses integer virtual pixels. Text measures before drawing and never depends on framebuffer
  scale. Newline is explicit; wrapping prefers the last whitespace and hard-breaks a token wider than
  the available width. Unsupported glyphs render the font's replacement glyph.
- Typewriter progression uses fixed ticks, not render frames. Default cadence is one visible glyph
  every two ticks; newline and formatting consume no beat. Confirm completes the current page; a
  later Confirm advances it. Held Confirm does not repeatedly dismiss pages.
- Lists/grids keep disabled entries visible and skip them during navigation. Wrapping occurs only when
  the control opts in. Empty controls accept Cancel and expose no selection. Removing the selected
  entry moves to the nearest remaining enabled entry, preferring the next then previous.
- Cancel restores the focus owner that opened the control. Scene replacement clears focus. No menu
  requires a pointer, hover state, or timing-sensitive key repeat.
- HP/resource bars clamp presentation to 0..max, handle max 0 without division, and animate only their
  displayed value. They never mutate the underlying resource.

Runtime options are separate from saves. Version 1 is
`{ optionsVersion: 1, keyboardBindings: action→ordered key list,
gamepadBindings: action→ordered control list, musicVolume: 100, sfxVolume: 100 }`. Action names are
the exact `GameAction` names and input names are the stable platform-adapter names, not numeric device
codes. Missing actions use defaults; unknown actions/inputs are ignored with one warning; duplicate
bindings, an unsupported newer `optionsVersion`, or unreadable JSON falls back to all defaults rather
than partially guessing. The 16C implementation copies this Runtime-local serialized contract into
`DATA_SCHEMA.md` in the same change before persistence code lands; it does not bump project schema.

### Acceptance matrix

| Required evidence | Minimum cases |
|---|---|
| Lifecycle golden | Push/pop/replace, overlay/opaque render, queued mutation, shutdown, once-only exit/dispose |
| Transition table | 15-out/switch/15-in, blocked input, load failure, no render-time load |
| Input table | Keyboard/gamepad merge, zero-tick edge, opposite/perpendicular directions, disconnect, rebind/swap/recovery |
| Focus/navigation | Empty/all-disabled, wrap/no-wrap, grid edges, removal, Cancel restoration |
| Text/typewriter | Newline, soft/hard wrap, missing glyph, 0/1/N pages, tick cadence, skip then advance |
| Options | Round trip, unknown/older/newer policy, corrupt fallback, duplicate binding rejection |
| Reachability | Headless input scripts reach Title, New, Continue, Overworld, Menu, and back |

Exit: content-driven title/new/continue/menu flow and the complete UI/input kit work without pointer
input or gameplay-rule duplication.

## 16D — Asset-backed overworld integration

### State and rendering ownership

`OverworldScene` owns presentation state, Core world-controller/session references, the current map
instance, derived collision, entity presentation instances, camera, and borrowed asset leases. Core
owns player position/facing/movement outcome, flags, encounters, trainer eligibility, inventory
mutation, blackout calculation, and all transition requests with gameplay meaning.

Map entry resolves all required tilesets, sprites, animations, entities, encounter tables, warps,
and services before the fade-in. A failure leaves the previous scene/session intact and reports an
asset/content diagnostic; it does not create a partially loaded map.

Render order is ground → deco-below → below objects → entities/player in explicit scene-submission
order → above objects → deco-above → overlays. Equal-layer entity ordering uses stable map-entity ID,
never collection or hash order. Camera follows the interpolated player presentation and uses the
existing clamp behavior; Core coordinates remain uninterpolated tile positions.

### Movement and update order

- At most one player movement intent is admitted at a time. Input may buffer one next direction
  through existing `GridMover` behavior; menus/interactions/transitions admit none.
- Each 16D fixed tick updates the player mover, completed-step pipeline, then NPCs in ordinal stable
  entity-ID order. Once 16E lands, its session clock advances at the phase-wide tick boundary before
  the active scene update. No NPC moves while a modal interaction or world transition is active.
- Occupancy is snapshotted before each entity decision and updated after each accepted movement so
  two entities cannot claim one cell. NPC RNG draws occur only at their defined decision point.
- Collision, ledges, occupancy, and warp legality call Core. Runtime must not inspect tile flags and
  independently decide movement.
- A blocked step changes facing as Core specifies but emits no completed-step trigger or encounter
  roll. Ledge completion invokes the pipeline once at the landing tile.

### Interaction and transition order

Confirm is accepted only while the player is idle and no modal owns input. One press resolves the
first applicable target in this exact order:

1. facing entity;
2. facing trigger or placed object;
3. current tile.

After an accepted completed step, stop at the first transition produced by:

1. warp;
2. tile trigger;
3. trainer sight;
4. random encounter.

Presentation does not continue down the list after a transition request. Warp arrival suppresses
the destination step-on warp until the player leaves that destination cell, preventing immediate
bounce loops. Door/stairs use the standard 15-out/15-in fade; edge transitions may preserve facing
and walking presentation but use the same atomic map switch.

Dialogue is modal, faces the speaker toward the player only in presentation unless Core returns a
facing mutation, and executes only the closed Core command/result vocabulary. Runtime must not parse
arbitrary command strings. Pickups perform one atomic Core add-item/flag operation; on failure the
item remains and the flag is unchanged. On success the entity disappears from presentation only
after the save-state flag changes.

Trainer sight uses Core `FirstSpotter`, stable ID order, the persisted derived defeat flag, and the
current collision/occupancy snapshot. The functional sequence is freeze world → trainer cue → intro
dialogue → typed battle request. A completed trainer result applies reward/defeat mutations once;
loss routes through the blackout result.

Random encounters occur only after completed eligible steps. Core owns rate, time/flag eligibility,
repel, slot, and level draws. Runtime requests the operation once and creates a battle transition
only from its returned encounter. Centers heal through Core then replace the respawn point. Mart and
PC objects may open their reusable scene shells in 16D; transactional functionality lands in 16E.

Blackout consumes one Core result containing restored party, money, checkpoint, and flags. Runtime
performs the transition and requests persistence only under the active save policy; it never
recomputes the penalty or healing.

### Debug spawn and exclusions

Debug project mode accepts the 16A `--spawn-*` contract only after ordinary project boot. It validates
map existence, bounds, collision, and required assets, does not alter the saved checkpoint, and is
unavailable in release flavor. Invalid spawn is exit 3, not a fallback.

16D does not add general scripting, cuttable/breakable field obstacles, water traversal, fishing,
biking, pushing puzzles, follower creatures, seamless connected-map streaming, or animated trainer
approach polish. Water retains the Core collision behavior frozen at Phase 15 GO unless the roadmap
is explicitly amended.

### Acceptance matrix

| Required evidence | Minimum cases |
|---|---|
| Map load/render | All layers, small/large map camera, chunk boundary, missing tileset/sprite/animation, atomic failed entry |
| Movement | Turn/tap/hold/buffer, block, occupancy, ledge landing, no trigger on block, one trigger after hop |
| Interaction | Exact facing entity/object/current priority; modal suppression; one action per press |
| Step pipeline | Warp beats trigger beats trainer beats encounter; each pairwise collision; stop after first |
| NPC replay | Stable ID order, occupied target, blocked draw, modal freeze, identical seed replay |
| Trainer | First spotter, blocked sight, range 0, already defeated, win flag/reward once, loss/blackout |
| Persistence | Pickup success/failure/reload, flag visibility, center checkpoint, warp destination suppression |
| Encounter | Trigger/no-trigger, eligibility, repel, exact RNG draws, battle return conservation |
| Parity | Identical raw/pack state, events, transitions, and presentation command golden |

Exit: the neutral fixture supports walking, dialogue, encounter, trainer, warp, pickup, center, mart,
and PC entry with deterministic replay and no Runtime-owned rules.

## 16E — Player systems, save, clock, audio, and debug overlay

### Player scenes and atomic operations

Party, Bag, Storage, and Shop scenes are UI adapters over Core operations:

- Party supports inspect, reorder, target selection, permitted field-item use, and progression prompts.
  It never recalculates stats, healing, legality, or evolution.
- Bag groups entries in project-authored pocket order, then stable item ID. Zero-count entries are not
  displayed. Consumption occurs only after Core accepts the complete use request.
- Storage uses project box count/capacity/names, conserves creature identity exactly, and exposes Core
  rejection reasons for full party/box or stranding the party.
- Shop buy/sell requests contain item and quantity. Core validates price, money, inventory capacity,
  and conservation atomically; UI previews only Core-provided totals.
- Move-learn and evolution prompts consume ordered Core results. Decline is offered only where Core
  marks it legal. Applying a choice is idempotent and cannot repeat after scene recreation.

### Save repository contract

- One manual slot is stored under `%APPDATA%/<validated SaveDirName>/save.json`; `.tmp` and `.bak`
  are siblings. `SaveDirName` is a single sanitized segment and cannot select another root.
- Phase 16 has no autosave. Saving is available only from the overworld/menu when no transition,
  battle, modal mutation, or pending Core operation exists.
- Serialize and validate a complete immutable save snapshot before touching the primary file. Write
  `.tmp` in the same directory, flush managed buffers and the file to disk, then atomically replace
  the primary while retaining the previous valid primary as `.bak`. A failure preserves the prior
  primary/backup and removes the temporary file best-effort.
- Load reads only the primary first. Corrupt/incompatible primary offers a separately validated
  backup; it never silently substitutes or overwrites. Loading backup marks the session recovered;
  the primary changes only on a later explicit save.
- Newer formats are refused safely. Older supported formats migrate through Core on an in-memory copy
  and are validated before use. Content-hash mismatch follows the schema compatibility policy and is
  never ignored by Runtime.
- New Game constructs the Core-defined initial state from validated project settings and does not
  create a save until the user explicitly saves.

### Clock contract

In-game clock mode advances exactly one game minute per 3,600 fixed simulation ticks while a loaded
game session is active; title/loading/error states do not count. Menus and battles remain part of the
loaded session and therefore count. `SaveFile.ClockTickRemainder` stores 0..3,599 residual ticks;
16E adds it through the `DATA_SCHEMA.md`/save-format migration workflow so save/reload cannot lose or
gain a minute. It resets to 0 only for New Game or an explicitly documented migration default.

Real-time mode obtains minute-of-day through an injected Runtime time source and passes the value to
Core; Core never reads the clock. Tests and replays provide a synthetic source. Time-of-day changes
affect future Core queries only and never retroactively alter an already-created encounter/battle.

### Audio contract

- Add only approved `Silk.NET.OpenAL` 2.23.0 when 16E becomes current. The published `win-x64`
  Runtime includes the approved native OpenAL implementation and license; no system install is assumed.
- Source format is RIFF/WAVE little-endian signed 16-bit PCM, mono/stereo, 44.1 or 48 kHz. Reject
  compressed, floating-point, malformed, unsupported-rate/channel, and truncated files during asset
  validation with a conversion hint.
- Music and Sfx buses each have integer volume 0..100, multiplied by master output without changing
  source data. Muting does not stop playback position.
- One streamed music track is active. Changing tracks performs a 30-fixed-tick linear crossfade;
  requesting the same track preserves playback. Stop fades to silence over the same duration.
- Permit sixteen simultaneous one-shot SFX voices. Reuse a completed voice first; if all are active,
  drop the oldest completed voice when one exists, otherwise drop the new request and increment a
  diagnostic counter. SFX timing never blocks simulation.
- Missing optional audio or device loss produces one warning and clean no-audio mode. Required asset
  corruption remains exit 4. Audio failure never changes Core state or RNG.
- Buffers/sources/stream worker/device/context have explicit ownership and shut down in reverse order.

### Debug overlay

DebugToggle shows FPS, ticks run/dropped, alpha, update/render time, draw/quad/texture counts, current
scene, map/player/facing, RNG stream identifiers/state snapshot, replay state, recent validation/
event/trace tail, and collision overlay. It is read-only. Release builds compile it out or make it
unreachable; content cannot enable it.

### Acceptance matrix

| Required evidence | Minimum cases |
|---|---|
| Party/bag | Party six boundary, reorder conservation, valid/invalid field use, zero count, target cancel |
| Storage/shop | Each full/invalid/stranding case; buy/sell affordability and overflow; exact conservation |
| Progression | Level/move-learn/evolution accept/decline, scene recreation, save/reload replay |
| Save durability | New/missing, round trip, temp/flush/replace failure injection, corrupt primary, valid/invalid backup, newer/older version |
| Clock | 3,599/3,600/3,601 ticks, rollover, save remainder, day/night, synthetic real-time source |
| Audio decode | Valid channel/rate matrix and each rejected header/format/truncation case |
| Audio runtime | Same/new/stop crossfade, volume/mute, 16/17 SFX, missing device, device loss, disposal |
| Publish | Clean machine loads bundled native OpenAL and includes license/notice |
| Debug | Required fields present in debug and unavailable in release |

Exit: player systems, clock, audio policy, and save/relaunch preserve identical Core state.

## 16F — Event-driven battle presentation

### State machine and Core boundary

The Battle scene has explicit states: Entering, CollectingActions, CollectingTargets,
SubmittingAtomicSet, PresentingEvents, CollectingReplacement, ApplyingOutcome, and Exiting. Only Core
responses move between legality-dependent states.

- Construct battles only from a typed Core battle request carrying topology, parties, ruleset,
  inventory/reward context, and saved RNG ownership. Runtime does not choose default creatures/moves.
- Enumerate legal actions, actor slots, typed targets, collective conflicts, and replacements from
  Core public queries. Disabled choices show the Core reason where available.
- Singles submit one complete required action. Doubles submit a complete atomic set for every required
  slot; Back may revise unsubmitted choices, but nothing reaches Core until the set is complete.
- Invalid submission returns to the appropriate selection state without changing PP/items/state/RNG.
  Runtime does not repair targets or replace an illegal action.
- AI actions are requested through Core after the player set is complete. Presentation cannot inspect
  or influence hidden AI inputs.

### Presentation catalog and timing

Every concrete public `BattleEvent` type maps to one or more generic presentation commands in a
single exhaustive catalog. The catalog may select text, sound, sprite motion, tint, bar animation,
or pause by event fields/tags, but it may not check a named move, species, item, or ability ID.

- Events are consumed exactly once and strictly in emitted order. Animation completion cannot reorder
  events or Core mutations.
- Each event has a minimum six-fixed-tick presentation beat. Confirm completes the current beat only.
  Held Confirm runs presentation at 4× by consuming four presentation ticks per simulation tick;
  Core receives neither extra ticks nor altered inputs.
- No animation completion callback mutates Core. HP/status/slot displays derive from event snapshots
  or the accepted post-resolution snapshot specified by the frozen Core contract.
- An unknown event is logged visibly in debug and uses a safe generic release message rather than
  disappearing. The event-catalog completeness test fails, blocking package/phase completion.
- Missing bespoke animation is valid: generic text plus the minimum beat must still present the event.

### Menus, replacements, and return

Layouts derive from active slots and support singles/doubles without separate controllers. Menus
cover move, optional form activation, item, switch, pass/fallback, capture where legal, typed target,
and simultaneous replacement. Capture/run/trainer restrictions come only from Core.

After `BattleOutcome`, apply its progression, capture, reward, defeat, inventory, party, and world
mutations to the session exactly once using an outcome application token/idempotence guard. A scene
re-entry or save retry cannot duplicate them. Then return a typed result to Overworld; loss routes to
the 16D blackout transition, draw uses the Core result, and success resumes only after all mandatory
prompts and replacements complete.

### Acceptance matrix

| Required evidence | Minimum cases |
|---|---|
| Catalog | Reflection/registry proves every concrete event mapped; injected unknown event fails test |
| Singles | Each legal action family, target, invalid resubmission, capture/trainer/run restriction |
| Doubles | Per-slot actions, typed ally/opponent/spread targets, collective conflict, pass/fallback |
| Replacement | One/both sides, simultaneous slots, invalid choice, no healthy replacement, draw |
| Timing | Six-tick beat, press skip, held 4×, event/state/RNG identity at normal/fast speed |
| Presentation neutrality | No content-ID branches; missing optional animation uses generic path |
| Return | Win/loss/draw/capture, progression prompts, reward and world mutation exactly once |
| Conservation | Battle→overworld party/HP/status/PP/items/money/flags/RNG match Core outcome |

Exit: every certified Phase 15 primitive and event can be selected and presented generically in
singles and doubles, with exact battle-to-world conservation.

## 16G — Runtime verification and phase gate

### Neutral fixture route

Use original neutral content only. One deterministic input script must execute:

New Game → movement/ledge/warp → dialogue/pickup → wild encounter/capture → party/storage → field
item/shop → manual save → process close/reload → trainer → progression/evolution → loss/blackout →
doubles debug battle → clean return to overworld.

The fixture exists to exercise Runtime contracts, not to preview the Phase 18 demo. It contains the
minimum content and assets required for every Phase 16 matrix row and no official/reference-corpus
content or demo-specific production branch.

### Parity and determinism proof

- Run identical scripted inputs twice in raw mode and twice in packed mode from the same initial RNG
  snapshot. Compare canonical Core state, save bytes excluding only documented nondeterministic file
  metadata, ordered events/traces, scene transitions, input consumption, audio command log, renderer
  command golden, and final screenshot evidence.
- Headless/null-renderer command output must match byte-for-byte. Hidden-GL screenshot comparison uses
  exact dimensions and a documented tolerance only for driver-level pixel variance; changing the
  tolerance requires review and a recorded reason.
- Re-run after save/relaunch from the scripted checkpoint. Continued output must match the uninterrupted
  reference from the first post-load tick onward.

### Performance and resource protocol

Record commit, configuration, OS, CPU, GPU/driver, RAM, resolution/scale, fixture hash, warm-up, sample
count, and measurement tool. Release build is authoritative.

- Sustain 60 Hz without missed simulation updates over the fixture.
- Over 10,000 measured frames after warm-up: p95 fixed update ≤4 ms and p95 render ≤12 ms.
- Steady-state managed allocation outside loads is ≤1 KB/frame.
- Startup to Title is ≤3 seconds warm and ≤6 seconds cold.
- Run 100 complete scene/map/battle cycles. After explicit disposal and collection, managed/native/GL/
  OpenAL resources show no monotonic growth and final usage is within 5% of the stabilized baseline.
- A failed budget requires measurement and the smallest direct correction. Do not add pooling,
  threading, caching layers, or renderer architecture speculatively.

### Manual, failure, and review gates

| Gate | Required evidence |
|---|---|
| Keyboard | Complete route using defaults and one rebound profile |
| Gamepad | Complete required menus/battle/world flow; disconnect/reconnect recovery |
| Window | Resize, minimize/restore, smaller-than-virtual clipping, focus loss, clean close |
| Malformed content | Every 16A content/asset category produces correct exit and safe diagnostic |
| Persistence | Save/reload, backup recovery, interrupted write, content/version refusal |
| Resource | Renderer/audio/map/scene/battle loops meet lifetime matrix |
| Security/IP | Path-root tests and scan for official content, hard-coded IDs, secrets, host paths |
| Accessibility | Pointer-free flow, visible focus, disabled-state distinction, text within virtual bounds |
| Review | Focused `cgm-review-pass` verdict GO with every FIX-NOW finding closed |

Run focused package tests, then full solution build/tests, whitespace/link checks, raw and packed
smoke, replay parity, performance/resource protocol, and keyboard/gamepad smoke. Record exact commands,
counts, artifacts, deviations, and commit hash in `IMPLEMENTATION_PLAN.md`.

Exit: the entire fixture loop passes end-to-end and all Phase 16 GO checkboxes have generated or
manually recorded evidence.

## Phase 16 exclusions and change control

Phase 16 excludes Creator workflows, production export/template authoring, original demo breadth,
new Core rules, general scripting/event graphs, field-obstacle/surf/fishing systems, localization,
networking, installer/update/signing work, telemetry/accounts/cloud, additional renderer backends,
lighting/post-processing, and content-specific Runtime branches.

An implementing agent may choose private file/type layout and ordinary BCL techniques under the
Ponytail ladder. It may not change package order, public behavior, locked timing, exit codes, formats,
budgets, dependency set, or exclusions without reconciling this spec and the authoritative roadmap.
New dependencies still require explicit user approval and `TECH_STACK.md` in the same change.
