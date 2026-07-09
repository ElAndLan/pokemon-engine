---
name: cgm-schema-change
description: Safely change Creature Game Maker serialized project data, save data, EntityIds, JSON loaders, migrations, fixtures, and schema tests. Use when editing DATA_SCHEMA.md, src/Cgm.Core/Model/Entities, ProjectSettings, Save, RuntimeConfig, EntityId, EntityRegistry, CgmJson, ProjectLoader, Migrator, samples/fixture-min, tests/fixtures, or any code that changes JSON shape, schemaVersion, saveFormatVersion, or entity categories.
---

# CGM Schema Change

## Overview

Use this skill whenever a change can alter serialized data. The goal is simple: old projects and saves do not break silently, docs and code stay field-for-field aligned, and fixtures remain honest.

Run `cgm-scope-gate` first. If the serialized change is not in the current phase, stop before editing.

## Required Reads

Read only what applies, but do not skip the first three:

1. `docs/DATA_SCHEMA.md`
2. `docs/CODING_STANDARDS.md`
3. `docs/TECH_STACK.md` if serialization dependencies are touched
4. `src/Cgm.Core/Serialization/CgmJson.cs`
5. `src/Cgm.Core/Serialization/ProjectLoader.cs`
6. `src/Cgm.Core/Serialization/Migrator.cs`
7. `src/Cgm.Core/Serialization/EntityRegistry.cs`
8. The entity/model file being changed under `src/Cgm.Core/Model`
9. Existing tests under `tests/Cgm.Core.Tests/Serialization`
10. Relevant fixtures in `samples/fixture-min` and `tests/fixtures/projects`

## What Counts As A Schema Change

Treat these as schema changes:

- Adding, removing, renaming, retyping, or changing defaults for serialized properties.
- Changing enum values that serialize to JSON.
- Changing `EntityId` grammar, category registry, folder layout, or filename-to-id rules.
- Changing polymorphic map/entity payloads or effect payload shapes.
- Changing save files, runtime config, pack manifest JSON, or project settings.
- Changing what is written by `CgmJson` or tolerated by loaders.
- Changing fixture JSON to match code.

Do not treat "only tests" or "only a fixture" as safe. Fixtures are part of the compatibility contract.

## Workflow

1. Gate scope with `cgm-scope-gate`.
2. Identify every serialized type affected.
3. Compare current code against `docs/DATA_SCHEMA.md`. If they already diverge, report that first.
4. Decide whether this is:
   - **Doc-only correction**: code already matches intended contract.
   - **Compatible additive change**: old JSON loads without migration because the new field has a safe default.
   - **Breaking shape change**: migration required.
5. Update `docs/DATA_SCHEMA.md` in the same change as code.
6. If the shape changes, bump the appropriate version:
   - Project data: `schemaVersion`.
   - Saves: `saveFormatVersion`.
   - Runtime config or pack metadata: use the owning spec/version field.
7. Add or update migration code in `Migrator` for every old shape that must still load.
8. Add an old-shape fixture under `tests/fixtures` when migration behavior changes.
9. Update `samples/fixture-min` only after understanding why the new shape requires it.
10. Add tests before claiming done.

## Implementation Rules

- Keep definitions immutable records.
- Keep Core pure: no UI, filesystem UI prompts, graphics, audio, windowing, or runtime-only dependencies in models.
- Preserve byte-stable output: stable property order, 2-space JSON, unknown fields tolerated on read, unknown fields not written back.
- Preserve `EntityId` immutability. Renaming a display name never renames an ID.
- Do not add future-phase fields. The only sanctioned placeholder is the empty `forms[]` species field already in the schema.
- Do not add a package for serialization convenience. Use `System.Text.Json` unless the user explicitly approves a dependency and `TECH_STACK.md` is updated.
- Prefer one small migration step over loader special cases scattered across callers.

## Required Tests

Add the smallest set that proves the contract:

- Round-trip serialization for changed entities.
- Byte-stability when writer output changes.
- Unknown-field tolerance if loader behavior is relevant.
- Migration test from old fixture to latest shape for version bumps.
- Loader diagnostics for malformed or inconsistent files.
- Validation tests if the new shape creates a new invalid state.

Run at least:

```powershell
D:\dotnet\dotnet.exe test tests\Cgm.Core.Tests\Cgm.Core.Tests.csproj
```

For broad schema changes, run the full solution:

```powershell
D:\dotnet\dotnet.exe test CreatureGameMaker.slnx
```

## Completion Report

Report these facts:

- Serialized shapes changed.
- Version bump or no version bump, with reason.
- Migration added or not needed, with reason.
- Fixtures changed.
- Tests run and result.
- Any doc/code divergence found and reconciled.

## Refusal Conditions

Stop and ask before proceeding if the requested schema change adds a deferred system such as abilities, held-item battle behavior, form activation, breeding, doubles, event scripting, localization, multiplayer, or PokeAPI import UI before its phase.
