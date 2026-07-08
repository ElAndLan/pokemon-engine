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

## `.cgmpack` binary layout (Phase 12, implemented)

Little-endian. One Core loader (`CgmPack`) produces the same `GameDb` from a pack as
`GameDb.FromProject` does from a raw folder — the pack is just a validated container (ADR-006).

```
magic            4 bytes  = "CGMP"
packFormatVer    int32    = 1
manifestLen      int32
manifest         UTF-8 JSON, manifestLen bytes
blobRegion       concatenated section blobs; manifest offsets are relative to its start
```

**Manifest** (`PackManifest`): `{ packFormatVersion, requiredRuntimeVersion, gameName,
buildTimestamp (UTC ISO-8601), contentHash, sections: [{ type, offset, length, codec }] }`.
`contentHash` = lowercase hex SHA-256 of the **uncompressed** concatenation of every section's
payload in index order (so it is codec- and timestamp-independent; two packs of the same data
have the same hash even if built at different times).

**Sections (v1):** exactly one — `type="data"`, `codec="deflate"`. Payload = UTF-8 JSON
`{ settings, entities: [{ category, json }] }` where each `json` is the entity serialized by its
concrete record type (same bytes the folder format writes). On read, `category → Type` comes from
the shared `EntityRegistry`, and each entity is deserialized version-tolerantly, exactly as
`ProjectLoader` does — this is what guarantees pack==folder GameDb equality.
<!-- ponytail: codec is a per-section string, so zstd (TECH_STACK primary) can be added as a second
     codec later without a format bump; deflate (stdlib) is the sanctioned fallback and is plenty for
     JSON game data at this scale, and keeps Cgm.Core free of an external compression package. -->

**Load-time verification (refuse before touching content):** magic must match; `packFormatVersion`
must equal the loader's supported version (else `InvalidDataException`); after decompression the
recomputed `contentHash` must equal the manifest's (tamper detection → `InvalidDataException`).
`requiredRuntimeVersion` is surfaced on the manifest for the runtime to compare against its own
version and refuse mismatches with a friendly dialog (runtime glue, not the pack reader).

## config.json (beside the exe, Phase 12, implemented)

The runtime reads `config.json` next to the exe and fails fast with a friendly dialog if it is
missing/invalid (Addendum §6). Shape (`RuntimeConfig`, serialized via `CgmJson`):
`{ schemaVersion, gameName, windowTitle, virtualWidth, virtualHeight, saveDirName, packPath, debug }`.

- `gameName` defaults to the project name; `windowTitle` defaults to `gameName`.
- `virtualWidth/Height` default to **240×160** (the integer-scaled internal resolution; 4× fills the
  960×640 window). <!-- ponytail: constant until the editor exposes a resolution setting; then it
  becomes a ProjectSettings field feeding this. -->
- `saveDirName` = the game name reduced to a filesystem-safe folder name (`%APPDATA%/<saveDirName>`);
  falls back to `Game` if nothing safe remains.
- `packPath` = the pack filename beside the exe (`game.cgmpack`). `debug` = the export flavor flag.

## Export operation (`Exporter`, Phase 12, implemented)

`Exporter.ExportData(project, options, outFolder)` is the data half of export (the exe template
copy/patch + smoke test are build/CI concerns): run `Validator` as a **hard gate** (any error aborts
unless `options.OverrideValidation`), then write `<outFolder>/game.cgmpack` and `<outFolder>/config.json`.
Returns the validation report + written paths. `Cgm.Tools export <project> <out>` wraps it.

## Outline (remaining, Phase 12)
Template patch · Debug vs release exe flavors · Smoke · Clean-VM.
