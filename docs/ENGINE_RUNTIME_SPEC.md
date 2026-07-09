# ENGINE_RUNTIME_SPEC

Status: **Partial / implemented sections are binding.** Headless runtime helpers are written
and tested (`FixedStepClock`, virtual resolution, camera, input state, scene stack, raw-folder
`GameDb`, battle action menu, battle event presenter, exported config/pack boot, Runtime
`--smoke`). The Silk.NET host loads exported `config.json`/`game.cgmpack` and renders a minimal
battle showcase with GL clear/scissor rectangles. Full sprite-batch renderer, text/UI drawing,
audio, and dev-mode project loading in the host are **not** implemented.

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
  including eligible `ActivateForm(formId, moveIndex)` actions, submits selected actions to
  `BattleController`, and presents `BattleEvent`s through `BattleEventPresenter`.
- **Exported boot/smoke**: `ExportedGameBoot` reads exported `config.json`, verifies the pack
  manifest/runtime version/content hash, loads the configured start map, initializes the showcase
  battle, and smoke-submits one legal action.
- **Window wiring**: `RuntimeHost` still owns the Silk.NET loop and Esc-to-close behavior. When an
  exported config exists beside the executable, it uses the exported window title/resolution and
  draws a minimal nonblank battle showcase with clear/scissor rectangles.

## Outline (later)

Renderer (sprite batch, tilemap chunks) · UI kit text/menu drawing · Audio · Debug overlays ·
dev-mode project loading in the host.
