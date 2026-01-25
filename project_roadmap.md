# Pokemon Engine - Development Roadmap

This document outlines the recommended path forward to turn the current engine prototype into a fully playable game loop.

## 🏁 Phase 1: The "Playable Loop" (Highest Priority)
*Goal: Allow the player to manage their session (Start -> Play -> Menu -> Save -> Load).*

### 1. Overworld Menu System
Currently, the player cannot interact with their existing data (Party, Bag) or control the game state.
*   **Menu Controller**: Create `MenuSystem` class to handle UI stack (Overworld -> Menu -> Submenu).
*   **Start Menu UI**: Implement the classic list on "Enter/Start" press:
    *   `Pokedex` (Placeholder)
    *   `Pokemon` (View Party, Summary, Switch Order)
    *   `Bag` (View Items, Key Items)
    *   `Save` (Trigger Save System)
    *   `Options` (Text Speed, Window Color)
    *   `Exit`

### 2. Save & Load Integration
You have a backend `SaveManager`, but it needs a frontend.
*   **Save UI**: Connect the "Save" menu option to `SaveManager.saveGame()`.
*   **Title Screen**: Create a scene before the Overworld loads.
    *   "New Game" -> Starts fresh (Intro script).
    *   "Continue" -> Loads `save_input_0.json` via `SaveManager.loadGame()`.

### 3. Transition System
*   **Map Transitions**: Add a "Fade to Black" effect when warping between maps or entering buildings.
*   **Door Animations**: Add visual feedback when entering door tiles.

---

## ⚔️ Phase 2: Battle System Completion
*Goal: detailed battle mechanics for full gameplay depth.*

### 1. Battle Menus & Flow
*   **Bag Integration**: [PROGRES] Items can now be selected in battle (logic via `ItemHandler`).
*   **Pokemon Switching**: [DONE] Party switching is fully functional in battle.

### 2. Catching Mechanics
*   **Pokeballs**: Implement using specific Pulse/Ball items.
*   **Capture Algorithm**: Implement Gen 3/4 catch rate formula.
*   **Pokedex Update**: Mark captured species as "Owned".
*   **Storage**: Automatic routing of caught Pokemon to PC if party is full.

### 3. Abilities & Held Items
*   **Passive Effects**: Implement `Ability` system.
*   **Held Items**: Implement leftovers, berries, etc.

---

## 🌍 Phase 3: World & Immersion
*Goal: Make the world feel alive.*

### 1. Audio System (Music/SFX)
### 2. Day/Night Cycle (Visual Tinting)

---

## 🛠️ Phase 4: Developer Tools (Shops & Economy)
*Goal: Speed up content creation.*

### 1. Shop Editor
*   Create an editor tab for defining shop inventories.
*   Allow per-shop markup/pricing.

### 2. NPC Shop Scripts
*   Allow NPCs to trigger shop UI via interaction scripts.

---

## 📅 Roadmap for Tomorrow (Jan 26, 2026)

### Morning: Bag UI & Battle Integration
1.  **Bag UI Polish**: Add scrollbar logic for lists longer than 7 items.
2.  **Toggle Pockets**: Ensure L/R triggers or button clicks switch category icons.
3.  **Battle Bag**: Connect the "Bag" option in `BattleScene` to the `BagMenu`.
4.  **Battle Effects**: Ensure items used in battle ( Medicine, Battle Items) apply to the battle state.

### Afternoon: Catching & Storage
1.  **Capture Formula**: Implement the standardized catch rate logic in `ItemHandler`.
2.  **Add to Team**: Implement the logic to detect if party is full (6) and move to `StorageSystem` (PC) otherwise.
3.  **Visuals**: Add "Shake" animation placeholders for the PokeBall in battle.

### Evening: Editor & Shops
1.  **Shop Tabs**: Implement the basic UI for the Shop Editor.
2.  **Shop Script**: Add the `SHOP` action to the Event/Interaction system.
3.  **Shop Menu**: Create the `ShopMenu` UI for buying/selling.
