# EXPORT_PIPELINE_SPEC

Status: **Stub** — current source is `MASTER_PLAN.md` §12 and `ARCHITECTURE_ADDENDUM.md` §10.
Full write due **before Phase 12** (write the `.cgmpack` binary layout before implementing it).
Blocks: Phase 12.

## Purpose
How a validated project becomes a standalone Windows game: pack format, template-exe patching,
config generation, debug/release flavors, the smoke-test contract, and the clean-machine ritual.

## Must lock
- `.cgmpack` binary layout (header, manifest with RequiredRuntimeVersion + content hash, section
  index, zstd blobs) — and the invariant that one GameDb loader consumes pack or raw folder.
- Template process (CI-built self-contained exes; copy → rename → icon/version patch → config.json).
- Smoke-test contract (`--smoke` steps + distinct exit codes) and version-mismatch refusal.

## Outline (to be written, Phase 12)
Validate gate · Compile · Pack format · Template patch · config.json · Debug vs release · Smoke · Clean-VM.
