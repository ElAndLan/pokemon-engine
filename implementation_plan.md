# Pokemon Engine Implementation Plan (Desktop)

## Goal Description
Create a **Desktop-based** "old-generation" Pokemon Game Engine from scratch. The engine will run as a standalone application (exe), mimicking original mechanics (Gen 3/4 style), and supporting custom regions loaded from the local file system.

## User Review Required
> [!IMPORTANT]
> **Tech Stack Adjustment**:
> - **Platform**: **Electron** (Allows us to use TypeScript/Canvas for logic/rendering but wrap it in a native Window with full file system access).
> - **Language**: TypeScript.
> - **Rendering**: HTML5 Canvas API (inside Electron's Renderer process).
> - **Build Tool**: Vite (integrated with Electron).

> [!NOTE]
> **Why Electron?**:
> It meets your "Installable Desktop App" requirement while allowing us to iterate rapidly with the TS/Canvas stack proposed earlier. It also makes "Loading Custom Regions" easy because we have native OS file access to read/write JSON files and save games.

## Proposed Changes

### Project Initialization
#### [NEW] [package.json](file:///package.json)
- Setup dependencies: `electron`, `vite`, `typescript`.
- Scripts: `dev` (runs electron + vite), `make` (builds the exe).

#### [NEW] [src/main](file:///src/main)
- `main.ts`: The Electron Main Process. Handles window creation and OS interactions (File I/O).

#### [NEW] [src/renderer](file:///src/renderer)
- `index.html`: The entry point for the UI.
- `src/core/*`: The actual Game Engine code (reused from previous plan).
    - `Game.ts`: Loop.
    - `Display.ts`: Canvas interactions.

### Architecture Analysis

#### 1. The Game Loop
Standard `requestAnimationFrame` within the Electron Renderer process.
Input is captured via window events.

#### 2. File System & Custom Regions
Use Electron's **IPC (Inter-Process Communication)**.
- The Engine (Renderer) requests: `loadMap("pallet_town.json")`.
- The Main Process uses `fs.readFile` to get the file from the user's hard drive and sends it back.
- This allows users to "Install" the engine and then just drop new Region folders into a `maps/` directory.

#### 3. The Data Structures
Same as before: Strict TypeScript Interfaces for Species, Moves, etc.
We will store the "Base Data" (Pokedex, Attacks) as JSON files bundled with the app.

## Verification Plan

### Automated Tests
- Unit tests (`vitest`) for Battle Mechanics do not require Electron and can run headlessly.

### Manual Verification
- **Build Test**: Run `npm run make` to generate an installable `.exe` and verify it launches.
- **Save System**: Verify a "Save Game" button creates a real file on the user's disk (e.g., in `AppData`).

### PC Menu Updates
#### [MODIFY] [PCMenu.ts](file:///src/renderer/src/core/ui/PCMenu.ts)
- Update `renderDetailsPanel` to show expanded stats: SpAtk, SpDef, Speed, Ability, Nature.

### Battle HUD Updates
#### [MODIFY] [BattleScene.ts](file:///src/renderer/src/core/battle/BattleScene.ts)
- Update `renderHealthBox` to display status icons (SLP, PSN, BRN, PAR, FRZ) and volatile statuses (Fusion, Infatuation) under the health bar.

### Bug Fixes
#### [FIX] Type Effectiveness
- [x] Investigate `TypeChart.ts` to ensure Fire > Steel effectiveness is correct (2.0x). (Refactored to Use 2.0x Standard)

#### [FIX] Confusion Logic
- [x] Investigate `MoveEngine.ts` or `AtomicEffects` to ensure Confusion lasts 1-4 turns properly and doesn't clear immediately. (Fixed Init Logic)

#### [FIX] Targeting Logic
- [x] Fix self-targeting moves failing due to immunity.

#### [FIX] Move Mechanics
- [x] Implement Fixed Damage (Night Shade) and Binding Damage (Fire Spin).
- [x] **Data Migration**: Update `moves.json` to correct 'Drain' moves, add 'Recoil', and 'MultiHit' properties based on descriptions. (27 Moves Patched)
- [x] **Engine Update**: Implement `Recoil` and `MultiHit` logic in `MoveEngine.ts`.
- [x] **Multi-Turn Moves**: Implemented support for Charge (Solar Beam), Recharge (Hyper Beam), and Semi-Invulnerable (Fly, Dig) moves.
- [x] **Special Effects**: Implemented `Disable` (tracks last move) and `Explosion`/`Self-Destruct` (MaxHP Recoil).

### Intuitive Encounter System (Redesign)
To address the disjointed workflow of creating encounters, we will unify the "Metadata" and "Data" aspects.

#### 1. "Zone Painting" Concept
Instead of placing "EncounterZone" objects (which are invisible points), we will paint **Regions** directly onto the map.
-   **Zone ID**: Each unique area (Grass A, Grass B, Water) gets a unique numerical ID (1, 2, 3...) on the map's `Encounters` layer.
-   **Zone Mapping**: The map file stores a mapping of `ID -> Zone Key` (e.g., `1 -> "route_1_grass"`, `2 -> "route_1_water"`).

#### 2. Editor Integration
-   **Unified Tab**: The "Encounters" tab becomes a one-stop shop.
-   **Visual Feedback**:
    -   Selecting a Zone in the list automatically activates the **Paint Tool**.
    -   Painting on the map applies that Zone's ID.
    -   The Editor renders colored overlays for each zone (Zone 1 = Red, Zone 2 = Blue) so you can see boundaries clearly.
-   **Workflow**:
    1.  Click "Add Zone" -> Name it "Cave Entrance".
    2.  "Cave Entrance" is assigned ID #3.
    3.  User paints tiles.
    4.  User adds Zubat to the table for "Cave Entrance".
    5.  Save.

#### 3. Data Migration
-   We will deprecate the old `EncounterZone` object type.
-   Existing map logic for random battles will check `layer.get(player.x, player.y)` to find the Zone ID, then look up the Zone Key, then query the Encounter Table.

### Item & Economy Systems
#### [NEW] [ShopEditor.ts](file:///src/editor/src/tabs/ShopEditor.ts)
-   A new tab in the editor to create and manage Shop Databases.
-   Shops are stored as `shops.json` with a mapping of `ShopID -> { items: string[], markup: number }`.

#### [MODIFY] [NPC.ts](file:///src/renderer/src/core/entities/NPC.ts)
-   Add support for a `SHOP` action in NPC interaction scripts.
-   Example: `["DIALOG", "Welcome! How can I help you?"], ["SHOP", "general_store_1"]`.

#### [NEW] [ShopMenu.ts](file:///src/renderer/src/core/ui/ShopMenu.ts)
-   A specialized menu for buying and selling items.
-   Interfaces with `BagSystem` to modify inventory and `SaveSystem` for currency (Money).

### Pokemon Catching System
#### [MODIFY] [ItemHandler.ts](file:///src/renderer/src/core/items/ItemHandler.ts)
-   Implement `usePokeball` logic using the standard capture formula.
-   $a = \lfloor \frac{(3 \times HP_{max} - 2 \times HP_{curr}) \times rate \times bonus_{ball}}{3 \times HP_{max}} \rfloor \times bonus_{status}$
-   If $a \ge 255$, the Pokemon is caught.

#### [MODIFY] [BattleScene.ts](file:///src/renderer/src/core/battle/BattleScene.ts)
-   Integrate Item usage into the battle UI.
-   Handle the logic for adding a caught Pokemon to the `Party` (via `StorageSystem`) or the `PC`.
