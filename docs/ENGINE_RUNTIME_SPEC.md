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

## Outline (to be written)
Loop · Renderer · Virtual resolution/camera · Input · Scene stack · UI kit · Audio · Dev-mode load · Debug overlays.
