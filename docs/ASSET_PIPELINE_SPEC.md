# ASSET_PIPELINE_SPEC

Status: **Stub** — current source is `MASTER_PLAN.md` §9 and `ARCHITECTURE_ADDENDUM.md` §9
(import layers v0–v5). Full write due **before Phase 4** (v0–v2), extended at Phases 12/17.
Blocks: Phases 4, 12, 17.

## Purpose
How art becomes runtime assets: PNG import, the slicing layers (manual grid → common-size →
gutter detection → connected-component), animation grouping, metadata, atlas packing, pack format.

## Must lock
- Slice-detection algorithms per layer (divisibility ranking, alpha-projection gutter fit,
  flood-fill component merge) with confidence + always-available manual override.
- Slice metadata format in `derived/`; sprites as projections (source PNG never modified).
- Atlas packing (skyline, ≤2048²) and how rects are rewritten into the `.cgmpack`.

## Outline (to be written, per layer)
Import v0 · v1 · v2 · v3 · v4 (animation) · v5 (atlas/pack) · Validation.
