# World Builder & Editor Implementation Plan

## Goal
Transform the application into a dual-purpose engine: a **Game Runtime** (to play) and a **World Builder** (to create). The initial focus is on the World Builder tools to allow full control over terrain and encounters.

## Core Feature List

### 1. Map Editor (Visual World Designer)
A "WYSIWYG" (What You See Is What You Get) editor for creating maps.

- **Tile Management**:
    - **Tileset Loader**: Load standard sprite sheets (png).
    - **Tile Palette**: Select tiles from the tileset to paint with.
    - **Layer System**:
        - **Ground Layer**: Base terrain (grass, sand, floor).
        - **Deco/Object Layer**: Trees, rocks, signs (with transparency).
        - **Collision Layer**: A special functional layer to define passable/impassable areas.
        - **Interaction Layer**: Define "Special Tiles" (Grass, Water, Ledges).

- **Tools**:
    - **Pencil**: Place single tiles.
    - **Rectangle/Fill**: Fill areas.
    - **Eraser**: Remove tiles.
    - **Picker**: Sample a tile from the map.

- **Metadata Painting**:
    - Instead of just graphics, users can switch to a "Metadata View" to paint logic.
    - **Red Overlay**: Impassable (Wall).
    - **Green Overlay**: Encounter Zone (Grass/Cave floor).
    - **Blue Overlay**: Surfable Water.

### 2. Map Object Editor (Items & Trainers)
A specialized mode for placing interactive entities on the Object Layer.

- **Item Spawns**:
    - **Visual Items**: Place visible Pokeballs.
    - **Hidden Items**: Place invisible items (detectable by Itemfinder).
    - **Properties**: Item ID (from database), Quantity, and a unique **Save Flag** (to prevent respawning).

- **NPCs & Trainers**:
    - **Placement**: Place characters on the map.
    - **Visuals**: Assign Sprite ID (e.g., "Youngster", "Bug Catcher").
    - **Behavior Settings**:
        - **Direction**: Initial facing direction.
        - **Movement Pattern**: "Fixed", "Look Around", "Patrol" (draw a path of nodes).
        - **Interaction**: Define dialog strings or script triggers.
    - **Trainer Data** (If Hostile):
        - **Team Builder**: Assign Pokemon, Levels, Moves, and AI Logic (Aggressive, Defensive).
        - **Sight Range**: How far they can "see" to trigger a battle.

- **Event Triggers (Script Zones)**:
    - Draw invisible rectangles on the map that trigger logic when entering/exiting.
    - **Event Examples**:
        - **Stepping on a bridge**: Triggers a cutscene.
        - **Doorways**: Warps to another map.
    - **Script Editor**: A simplified visual scripter (or JSON text editor) to define the specific actions:
        - `WalkTo(Actor, X, Y)`
        - `ShowMessage("Hello!")`
        - `StartBattle(TrainerID)`

### 3. Encounter Editor (Enhanced)
A dedicated UI for managing wild Pokemon data per map.

- **Zone Management**:
    - Select a Map (e.g., "Route 1").
    - **Walking**: Grass/Cave encounters.
    - **Surfing**: Water body encounters.
    - **Fishing**:
        - **Old Rod**: Basic low-level encounters (Magikarp).
        - **Good Rod**: Mid-tier encounters.
        - **Super Rod**: High-tier/Rare encounters.
    - **Time of Day**: Morning, Day, Night slots for each category.

- **Encounter Pool**:
    - Add Species to the pool (Dropdown search from `data/pokemon/*.json`).
    - **Level Range**: Min/Max levels (e.g., 2-4).
    - **Rate/Weight**: Define rarity (Common, Rare, Very Rare).
        - *Visual Feedback*: Show percentages (e.g., "Pidgey: 50%").

### 4. Additional Brainstormed Features
To make a *complete* Pokemon Engine, these features are highly recommended:

- **Global Flag/Variable System**: A database of "Save Flags" (e.g., `BADGE_1_EARNED`, `MET_RIVAL`) to track story progress. This is essential for:
    - Changing dialogue after events.
    - Blocking paths until a condition is met.
    - Removing items once picked up.
- **Warp/Connection Editor**: A visual tool to link map edges (Map A Exit -> Map B Entrance) without writing manual JSON coordinates.
- **Weather System**: Paint "Weather Volumes" (Rain in this corner, Sandstorm in that distinct desert area) that affect battle stats.
- **Shop/Mart Editor**: Define inventories for shopkeepers based on the number of badges or specific flags.


### 3. Data Structure Definitions

#### Map Metadata (Internal)
Extension of the standard Tiled format or a custom JSON sidecar.
```json
{
  "mapId": "route_1",
  "encounters": "encounter_table_route_1.json",
  "collisions": [ ...bitmask or grid... ],
  "biomes": [ ...grid... ]
}
```

#### Encounter Table Schema
```typescript
interface EncounterTable {
    mapId: string;
    zones: {
        walking?: EncounterMethod;
        surfing?: EncounterMethod;
        fishing?: {
            oldRod: EncounterMethod;
            goodRod: EncounterMethod;
            superRod: EncounterMethod;
        };
    };
}

interface EncounterMethod {
    rate: number; // Chance of encounter per step
    pool: EncounterEntry[];
}

interface EncounterEntry {
    speciesId: string; // "pidgey"
    minLevel: number;
    maxLevel: number;
    weight: number; // Relative weight for probability
    timeOfDay?: 'Morning' | 'Day' | 'Night' | 'Any';
}
```

#### Map Object Schema
```typescript
interface MapObject {
    id: string;
    x: number;
    y: number;
    type: 'Item' | 'NPC' | 'Trigger';
    properties: {
        // Item
        itemId?: string;
        amount?: number;
        hidden?: boolean;
        saveFlag?: string; // "ITEM_PICKUP_ROUTE1_POTION"
        
        // NPC
        sprite?: string;
        trainerData?: TrainerDefinition;
        dialog?: string[];
        movementType?: 'Fixed' | 'Patrol' | 'Spin';
        path?: {x: number, y: number}[];
        
        // Trigger
        onEnter?: ScriptAction[];
        conditionFlag?: string;
    }
}
```

## Technical Implementation Plan

### Phase 1: Editor Infrastructure
1.  **Editor Mode Toggle**: Add a Main Menu option or DevToggle to switch between "Play Mode" and "Editor Mode".
2.  **Editor UI Layout**:
    - **Viewport**: The central Canvas (re-using the Renderer).
    - **Sidebar (React/HTML)**: Controls for Layers, Tilesets, and Tools.
    - **Status Bar**: Coordinates, current tool.

### Phase 2: Map Interaction
1.  **Grid Input**: Map mouse coordinates to World Grid coordinates.
2.  **Live Rendering**: Update the local `MapInstance` in memory when "painting" and re-draw.
3.  **Persistence**: Implement `Save Map` button => Writes to `maps/xxx.json` via IPC.

### Phase 3: Object & Event System (Refined)
Enable full editing of map entities (NPCs, Items, Warp points).

**3.1. Object Selection & Inspection**
- **Interaction**: Clicking an existing object in the "Objects" tab selects it.
- **Visuals**: Highlight the selected object with a blue/cyan bounding box.
- **Form Binding**: 
    - Populate the Sidebar form with the selected object's data.
    - Implementing "Two-way binding": Typing in the sidebar updates the object in real-time.

**3.2. NPC & Trainer Logic**
- **Properties**:
    - `Facing`: [North, South, East, West].
    - `MovePattern`: [Fixed, Random, Spin, Patrol].
    - `Hostile`: Boolean (triggers trainer battle).
    - `SaveFlag`: Unique key (e.g., `NPC_ROUTE1_YOUNGSTER_BEATEN`).

**3.3. Script Actions (Triggers)**
- **Warp Points**: Fix the `Warp` object logic to properly serialize for the `Game` to use.
- **Trigger Zones**: Create invisible regions that fire script commands when entered.

### Phase 4: Encounter UI
1.  **Sidebar Panel**: "Encounters" tab.
2.  **Form Controls**: dynamic lists of Pokemon.
3.  **Validation**: Ensure total rates add up (or normalize them).


## User Workflow
1.  User opens app, selects "Map Editor".
2.  User loads "Pallet Town".
3.  User selects "Encounters" tab.
4.  User adds "Pidgey (Lvl 2-3, 50%)" and "Rattata (Lvl 2-3, 50%)".
5.  User saves.
6.  User switches to "Play Mode" and walks in the grass to verify.
### 1.5. Palette Zoom & Scaling
Improve the selection workflow for wide tilesets by allowing the palette to be zoomed out or fitted to the sidebar.

**Implementation Details**:
- **State**: Add `paletteZoom: number` to the `Editor` class (default 1.0).
- **UI**: 
    - Add a row of zoom buttons above or below the palette: `[ 0.25x | 0.5x | 1x | 2x | Fit ]`.
    - Update `renderTilePalette` to use `baseSize * paletteZoom` for display.
- **Logic**:
    - Ensure `mousedown` and `mousemove` calculations on the palette scale appropriately.
    - Implement "Fit to Width" which calculates a zoom level that matches the sidebar width.
