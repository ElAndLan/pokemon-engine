# Creature Game Maker

A Windows desktop **Creator** app plus a custom lightweight 2D **runtime engine** for building
Pokemon-style creature-battler RPGs from original assets and exporting them as standalone games.
Not built on any existing game engine (see `docs/TECH_STACK.md`).

- **Cgm.Core** — schemas, IDs, validation, battle math, saves. Headless, no UI/graphics deps.
- **Cgm.Creator** — Avalonia authoring app.
- **Cgm.Runtime** — Silk.NET (GL 3.3) game engine; ships as the export template.
- **Cgm.Tools** — CLI (`validate`, `export`).

## Prerequisites
- .NET 10 SDK. On this dev machine it lives at `D:\dotnet` (C: is full) — see
  `docs/TECH_STACK.md` → "Local dev environment". Newly-opened terminals resolve `dotnet`
  automatically; otherwise use `D:\dotnet\dotnet.exe`.

## Build / test / run
```
dotnet build CreatureGameMaker.slnx
dotnet test  CreatureGameMaker.slnx
dotnet run --project src/Cgm.Creator          # authoring app
dotnet run --project src/Cgm.Runtime -- --debug   # runtime window (Esc to exit)
dotnet run --project src/Cgm.Tools -- --help
```
Or double-click `run.bat` for a launch menu.

## Where to start reading
`CLAUDE.md` (rules) → `docs/SCOPE_GUARD.md` (current phase) → `docs/MASTER_PLAN.md` →
`docs/ARCHITECTURE_ADDENDUM.md` (wins on conflicts) → `docs/IMPLEMENTATION_PLAN.md` (lifecycle).
`docs/AGENTS.md` is the working guide for contributors.
