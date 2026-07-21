# EXPORT_PIPELINE_SPEC

Status: **Implemented data-export baseline plus Phase 18/19 package contracts authorized.** `.cgmpack` layout, manifest/hash
verification, `config.json`, runtime template folder copy/rename, `Cgm.Tools export`, and Runtime
`--smoke` over exported config/pack are written and tested. CI self-contained template publishing,
exe icon/metadata patching, Creator export UI, and clean-VM testing are **not** implemented.

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
- `virtualWidth/Height` default to **256×192** (the integer-scaled internal resolution; 4× fills a
  1024×768 window). Amended 2026-07-21 for Gen 4 DS-era single-screen alignment (user directive;
  `IMPLEMENTATION_PLAN` §6.1, `ENGINE_RUNTIME_SPEC` 16B); was 240×160. Existing exported configs
  serialize both values explicitly, so the change affects only newly written configs and callers
  that omit the fields — no migration is required and `schemaVersion` is unchanged.
  <!-- ponytail: constant until the editor exposes a resolution setting; then it becomes a
  ProjectSettings field feeding this. -->
- `saveDirName` = the game name reduced to a filesystem-safe folder name (`%APPDATA%/<saveDirName>`);
  falls back to `Game` if nothing safe remains.
- `packPath` = the pack filename beside the exe (`game.cgmpack`). `debug` = the export flavor flag.

## Export operation (`Exporter`, Phase 12, implemented)

`Exporter.ExportData(project, options, outFolder)` runs `Validator` as a **hard gate** (any error
aborts unless `options.OverrideValidation`), optionally copies `options.TemplateFolder`, renames
`Cgm.Runtime.exe` to `<GameName>.exe`, then writes `<outFolder>/game.cgmpack` and
`<outFolder>/config.json`. Returns the validation report + written paths. `Cgm.Tools export
<project> <out>` wraps it; by default it uses `templates/<flavor>/`, `templates/`, or the local
`src/Cgm.Runtime/bin/Debug/net10.0` build output as the runtime template, and `--data-only` preserves
the old pack/config-only path.

## Runtime smoke (Phase 12, implemented)

`Cgm.Runtime --smoke` reads `config.json`, verifies the pack manifest/runtime version and content
hash, loads the start map, initializes the showcase battle path, submits one legal showcase action,
and exits `0`. Load/smoke failures exit nonzero with a console error.

## Pack asset sections (pack format v2)

Amended 2026-07-21 (user directive; `ENGINE_RUNTIME_SPEC` 16H prerequisite). Asset embedding was
listed below as Phase 18 work; it moved forward because the runtime cannot render authored art
without it, and a demo judged on flat colour proves nothing. The Phase 18 rows that remain are CI
templates, transactional export/rollback, icon handling, and distribution.

- **Container**: unchanged. The pack is still `magic | formatVersion | manifestLen | manifest | blob`,
  and the manifest still indexes sections by `{type, offset, length, codec}`. Assets are additional
  sections; nothing about the data section changed.
- **Section type**: `asset:<path>`, where `<path>` is the canonical project-relative path from
  `AssetPath.Normalize` (forward slashes, no `..`, not rooted). The reader re-canonicalizes rather
  than trusting the stored name — a pack is untrusted input.
- **Codec**: `stored`. PNGs are already deflate-compressed internally, so a second pass costs time
  and saves essentially nothing.
- **Content hash**: covers the decoded concatenation of every section in index order, so asset bytes
  are integrity-checked exactly like rules data. A tampered image fails the same gate.
- **Determinism**: assets are written in ordinal path order, so the same project exports
  byte-identical packs regardless of dictionary iteration order or host path separators.
- **Format version**: `2`. Readers accept `1`–`2`; a v1 pack simply carries no asset sections and
  still loads.
- **Export gate**: `Exporter` reads every asset its sheets reference from the project folder. A
  missing file, an unsafe path, or a `contentHash` that no longer matches **aborts the export** —
  shipping a game whose art silently differs from what was authored is worse than not shipping.
- **Runtime**: `IAssetSource` has exactly two implementations — `FolderAssetSource` (raw project
  mode) and `PackAssetSource` (exported mode). Scenes receive one via `RuntimeContent.Assets` and
  cannot tell which; this is the ADR-006 parity guarantee extended to art.

## Phase 18/19 specification completion contract

`IMPLEMENTATION_PLAN.md` v4 §§8-9 authorize production asset sections, CI templates, transactional
export/rollback, optional icon handling without a new dependency, smoke exit codes, completeness,
clean-machine proof, unsigned zip distribution, compatibility, and release artifacts. Reconcile the
exact package defaults into this spec before 18C/18D/19C code. No additional user confirmation is
required unless v4 §2.1 reserves a dependency, credential, cost, or destructive compatibility choice.
