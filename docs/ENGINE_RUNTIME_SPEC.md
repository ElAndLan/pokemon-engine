# ENGINE_RUNTIME_SPEC

Status: **Stub** — current source is `MASTER_PLAN.md` §6. Full write due **before Phase 6**.
Blocks: Phases 6–7 (runtime foundation, walking prototype).

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

## Outline (later — needs a display / GL)
Renderer (sprite batch, tilemap chunks) · UI kit · Audio · Debug overlays · window wiring.
