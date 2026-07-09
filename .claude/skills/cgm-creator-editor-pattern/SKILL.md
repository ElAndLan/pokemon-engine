---
name: cgm-creator-editor-pattern
description: Build or review Creature Game Maker Creator editor screens, Avalonia Views, ViewModels, document tabs, validation strip behavior, entity CRUD, reference pickers, and undo/redo editing. Use before editing src/Cgm.Creator ViewModels, Views, ProjectSession, UndoStack, MainWindowViewModel, editor documents, Creator tests, or CREATOR_APP_SPEC.md editor behavior.
---

# CGM Creator Editor Pattern

## Overview

Use this skill when adding or changing Creator UI/editor behavior. The Creator edits authoring data; it does not simulate gameplay, compute rules, or mutate project state outside the undoable command stack.

Run `cgm-scope-gate` first. If the editor exposes a new serialized field, also use `cgm-schema-change`.

## Required Reads

1. `docs/CREATOR_APP_SPEC.md`
2. `docs/CODING_STANDARDS.md` architecture invariants
3. `src/Cgm.Creator/ViewModels/EditorDocument.cs`
4. `src/Cgm.Creator/ViewModels/MoveDocument.cs`
5. `src/Cgm.Creator/ViewModels/ItemDocument.cs`
6. `src/Cgm.Creator/ViewModels/TypeChartDocument.cs`
7. `src/Cgm.Creator/ProjectSession.cs`
8. `src/Cgm.Creator/Editing/UndoStack.cs`
9. Existing matching tests in `tests/Cgm.Creator.Tests`
10. The model/entity type in `src/Cgm.Core/Model` being edited

## Creator Rules

- UI never mutates game state directly.
- Editors mutate project data through `ProjectSession` and undoable commands.
- Small entity editors use whole-record snapshot edits.
- Map/layer-style large data may use focused commands, but still goes through the undo stack.
- Creator does not run gameplay in-process. Playtest spawns Runtime.
- Creator ViewModels can validate authoring data; they do not duplicate Core rules.
- Views are thin Avalonia bindings over ViewModels.
- Headless ViewModel tests are required for behavior.

## Implementation Workflow

1. Find the closest existing editor shape and copy its structure.
2. Put behavior in a ViewModel/document first; add the View only after the behavior is testable.
3. Use `EntityEditorDocument<T>` for simple entity editors.
4. Each setter should:
   - compare the new value to the current model
   - create `Model with { ... }`
   - call `Edit(...)`
5. Use `ProjectSession` for entity add/remove/update and validation snapshot.
6. Keep reference lists derived from `Session.All<T>()`, sorted consistently.
7. Keep dialog/file-picker dependencies behind services such as `IDialogService`.
8. Make delete/rename/duplicate respect `EntityId` immutability and reference warnings.
9. Add the View with existing XAML conventions and compiled bindings.
10. Update `docs/CREATOR_APP_SPEC.md` if behavior differs from or extends the spec.

## Do Not Build

- New docking/window framework.
- Gameplay simulation inside Creator.
- Runtime rendering embedded in Creator unless a future ADR changes ADR-009.
- A new state-management framework.
- One-off editor plumbing when an existing document/session/undo pattern fits.
- A speculative reference picker/control if the current editor only needs a simple combo.
- UI for out-of-phase systems.

## Test Requirements

Add headless tests in `tests/Cgm.Creator.Tests`:

- Setter/edit applies the exact record change.
- Undo restores the exact previous record.
- Redo reapplies it.
- Dirty state changes correctly.
- Save marks clean when relevant.
- Reference lists include the expected IDs.
- Invalid entity CRUD is rejected or warned as existing patterns require.
- Validation strip/navigation behavior is tested when changed.

Manual UI checks are still useful, but they do not replace ViewModel tests.

Run:

```powershell
D:\dotnet\dotnet.exe test tests\Cgm.Creator.Tests\Cgm.Creator.Tests.csproj
```

For changes touching shared models or validation:

```powershell
D:\dotnet\dotnet.exe test CreatureGameMaker.slnx
```

## Review Checklist

- Is behavior copied from the closest pathfinder editor?
- Are edits undoable?
- Does the ViewModel avoid direct file dialogs, process launches, and UI-only dependencies?
- Does the View stay thin?
- Are references shown as stable `EntityId`s or display labels without mutating IDs?
- Does validation still run through Core/project validation?
- Is any new UI in the current phase?
- Are tests headless and deterministic?

## Completion Report

Report the editor/viewmodel added or changed, the pathfinder copied, tests run, and any manual UI verification that could not be performed.
