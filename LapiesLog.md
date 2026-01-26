# LapiesLog

## Development Log

### 2025-01-25 10:00 - Stat Calculation Refactor & Level-Up UI Improvements

#### Stat Calculation System
- **Created** `src/renderer/src/core/stat/StatCalculator.ts`
  - Centralized Gen 3+ stat calculation formulas
  - Supports HP and non-HP stats
  - Accounts for IVs (0-31), EVs (4 EVs = 1 stat point at Lv 100), and Nature modifiers (10% boost/hinder)

- **Modified** `src/renderer/src/core/ExperienceCalculator.ts`
  - Removed duplicate `getStat()` and `getHp()` methods
  - Now uses `StatCalculator.calculateAllStats()`

- **Modified** `src/renderer/src/core/EncounterManager.ts`
  - Replaced 58-line `calculateStats()` and 26-line `getNatureModifier()` with 3-line StatCalculator call

- **Modified** `src/renderer/src/core/items/ItemHandler.ts`
  - Replaced local stat recalculation with StatCalculator

#### Bug Fixes
- **Fixed** level-up stat popup not appearing after "X grew to Lv. Y" text
  - Issue: State overwrite in `BattleScene.executePlayerMove()` (line 489) set state to `BATTLE_END_WAIT` immediately after `handleExperienceGain()`
  - Fix: Added condition to only set `BATTLE_END_WAIT` if not in `LEVEL_UP_STATS` or `LEVEL_UP_STATS_2` state

- **Fixed** persistent stat popup failure
  - Issue: Second state overwrite in `BattleScene.executeEndOfTurn()` (line 888) overrode `LEVEL_UP_STATS` state
  - Fix: Added same state check condition
  - Added debug logging for state tracking

#### Level-Up Stat Popup UI Redesign
- **Redesigned** `BattleScene.renderLevelUpBox()` to match Pokemon Emerald style
  - Changed from textarea-contained layout to vertical overlay box outside textarea
  - Positioned to left of Pokemon nameplate (x: `width - 460`)

- **Display Features**
  - Shows all 6 stats at once: HP, Attack, Defense, Sp. Atk, Sp. Def, Speed
  - Two-screen system:
    - **Screen 1 (`LEVEL_UP_STATS`)**: Current stat values with increases displayed (e.g., `HP: 27 +2`)
    - **Screen 2 (`LEVEL_UP_STATS_2`)**: New stat values only (no increases shown)
  - One-line per stat format: `StatName: Value +Increase`
  - Increases shown in green (+) or red (-) based on stat change

- **Positioning Adjustments**
  - Initial position: `height - 240`
  - Final position: `height - 320` (moved up 80 pixels total)
  - Box dimensions: 180x180 pixels

#### Technical Details
- **File**: `src/renderer/src/core/battle/BattleScene.ts`
- **Method**: `renderLevelUpBox(ctx, width, height)`
- **State Machine**: Uses `LEVEL_UP_STATS` and `LEVEL_UP_STATS_2` states for two-screen display
- **Data Structure**: `this.levelUpData` contains `oldStats`, `newStats`, and `diff` objects

#### Dependencies
- No new libraries added
- Uses existing Canvas rendering API
- TypeScript class-based design maintained

---

### 2025-01-25 14:30 - Move Replacement UI for Pokemon with Full Move Slots

#### New Features
- **Created** `src/renderer/src/core/ui/MoveReplacementMenu.ts`
  - Menu component for selecting which move to replace when Pokemon has 4 moves
  - Pokemon-style UI with white background, dark border, and light blue highlight (#a0d8ef)
  - Displays move names and PP counts for all 4 existing moves
  - Includes "STOP" option to cancel move learning
  - Supports keyboard navigation (Arrow keys, WASD) and selection (Space/Enter/Z)
  - Autocloses after selection via menuSystem.pop()

- **Modified** `src/renderer/src/core/battle/BattleScene.ts`
  - Integrated MoveReplacementMenu for battle context rare candy usage
  - Added moveReplacementMenu property to track active menu
  - Implemented async move replacement handling in handleLevelUp method
  - Added menuSystem.pop() call to autoclose menu after selection
  - Fixed MoveLearningManager.replaceMove parameter mismatch (oldMoveId → oldMoveIndex)

- **Modified** `src/renderer/src/core/ui/BagMenu.ts`
  - Added handleMoveReplacement method for overworld rare candy move learning
  - Integrated MoveReplacementMenu for overworld context
  - Optimized dialog timing - only shows dialogs for first move in sequence
  - Added menuSystem.pop() call to autoclose menu after selection
  - Supports multiple consecutive move replacements

- **Modified** `src/renderer/src/core/items/ItemHandler.ts`
  - Extended ItemUseResult interface with MoveToReplace type
  - Added movesToReplace array to track moves that need replacement
  - Added pokemonInstanceId field to identify target Pokemon
  - Updated rare candy logic to collect movesToReplace when move slots are full

#### Bug Fixes
- **Fixed** MoveReplacementMenu canvas error
  - Issue: Used undefined `this.game.canvas` property
  - Fix: Changed to `this.game.display` for width/height calculations

- **Fixed** Menu not autoclosing after selection
  - Issue: Menu remained open after move replacement
  - Fix: Added `this.game.menuSystem.pop()` in onResult callbacks

- **Fixed** Duplicate dialogs for consecutive move replacements
  - Issue: All moves in sequence showed "wants to learn" and "already knows 4 moves" dialogs
  - Fix: Added showDialogs flag to only show dialogs for first move (currentIndex === 0)

#### UI Design
- MoveReplacementMenu dimensions: 320x200 pixels
- Centered on screen using display width/height
- List shows 4 existing moves + STOP option (5 total)
- Compact layout with 30px height per list item
- Bold 13px font for move names, 12px font for PP values
- Blue highlight (#a0d8ef) for selected item
- PP display format: "PP:current/max" aligned to right

#### Technical Details
- **MoveReplacementMenu**: Implements Menu interface
- **Callback pattern**: onResult(replaced: boolean, oldMoveId?: string)
- **State management**: Selection index (0-4), updates via keyboard input
- **Move data**: Uses game.dataManager.getMove() to fetch move details
- **PP tracking**: Displays current PP and max PP from PokemonInstance.moves

#### Files Modified
- src/renderer/src/core/ui/MoveReplacementMenu.ts (created)
- src/renderer/src/core/battle/BattleScene.ts
- src/renderer/src/core/ui/BagMenu.ts
- src/renderer/src/core/items/ItemHandler.ts
- .gitignore (created)

#### Dependencies
- No new libraries added
- Reuses existing MenuSystem, DataManager, and MoveLearningManager
- Follows existing menu component patterns

---

### 2025-01-25 - Pokemon Name Display Fix for Evolution

#### Problem
Evolved Pokemon were displaying their old species name instead of the new name in health boxes and evolution messages. This occurred because the display logic used `nickname || 'Unknown'` fallback, which didn't account for updated species data after evolution.

#### Solution
Implemented consistent name resolution via helper functions across all display locations:

- **Pokemon WITH nicknames**: Always display the nickname (even after evolution) - preserves original Pokemon behavior
- **Pokemon WITHOUT nicknames**: Display current species name (updates after evolution)

#### Code Changes

**Modified** `src/renderer/src/core/battle/BattleScene.ts`
- Added `getPokemonDisplayName(mon: PokemonInstance): string` helper method
- Updated `renderHealthBox()` to use helper instead of `mon.nickname || 'Unknown'`
- Updated `checkForEvolution()` to use helper for evolution messages

**Modified** `src/renderer/src/core/ui/BagMenu.ts`
- Added `getPokemonDisplayName(pokemon: any): string` helper method
- Updated `checkForEvolution()` to use helper for overworld evolution messages
- Updated move learning dialogs to use helper:
  - "X wants to learn Y!" message
  - "X learned Y!" message

**Modified** `src/renderer/src/core/ui/PCMenu.ts`
- Added `getPokemonDisplayName(mon: PokemonInstance): string` helper method
- Updated withdraw/deposit console logs to use helper
- Updated PC display rendering to use helper:
  - Grid view nickname display
  - Details panel nickname display

**Modified** `src/renderer/src/core/data/DataManager.ts`
- Changed `createPokemonInstance()` to set `nickname: undefined` instead of `nickname: species.name`
- Prevents auto-nicknaming of newly created Pokemon
- Ensures new Pokemon without nicknames display current species name after evolution

**Modified** `save_input_0.json`
- Manually cleared nickname from user's pre-existing Bulbasaur (uuid: `0a10eb3e-8930-4b1c-8c03-6a16ab633f90`)
- Changed `"nickname": "Bulbasaur"` to `"nickname": null`
- Allows this Pokemon to display current species name after evolution

#### Technical Details

**Helper Function Pattern**
```typescript
private getPokemonDisplayName(mon: PokemonInstance): string {
  if (mon.nickname) return mon.nickname;
  const species = this.game.dataManager.getPokemonSpecies(mon.speciesId);
  return species?.name || mon.speciesId || 'Unknown';
}
```

- Checks for nickname first (preserves custom names through evolution)
- Falls back to current species data (ensures name updates after evolution)
- Final fallback to speciesId or 'Unknown' for safety

**Nicknames Are Preserved**
- Custom nicknames are a core Pokemon feature and remain fully functional
- A Pokemon named "Sparky" will still be called "Sparky" after evolving from Pikachu to Raichu
- Only Pokemon without nicknames show species name changes

#### Files Modified
- src/renderer/src/core/battle/BattleScene.ts
- src/renderer/src/core/ui/BagMenu.ts
- src/renderer/src/core/ui/PCMenu.ts
- src/renderer/src/core/data/DataManager.ts
- save_input_0.json

#### Dependencies
- No new libraries added
- Uses existing DataManager.getPokemonSpecies() for species lookup
- Follows existing PokemonInstance data structure

---

### 2025-01-25 - Evolution Stat Bonus Fix

#### Problem
Evolved Pokemon were not receiving proper stat bonuses after evolution. The HP calculation was incorrect, leading to:
1. Wrong HP ratio calculation using base stats instead of current max HP
2. Incorrect new HP calculation using base stats instead of recalculated stats
3. Potential HP values that didn't match the Pokemon's actual maximum HP

#### Solution
Fixed the evolution stat calculation in EvolutionManager.evolvePokemon() to correctly:

1. Calculate HP ratio using the Pokemon's current maximum HP (pokemon.currentStats.hp)
2. Calculate new current HP using the newly recalculated stats (newStats.hp)
3. Ensure HP never drops below 1 after evolution

#### Code Changes

**Modified** `src/renderer/src/core/battle/EvolutionManager.ts`

Changed from:
```typescript
const newStats = ExperienceCalculator.recalculateStats(pokemon, targetSpecies);
pokemon.currentStats = newStats;

const hpRatio = pokemon.currentHp / (oldSpecies.baseStats.hp + 100);
pokemon.currentHp = Math.floor(targetSpecies.baseStats.hp + 100 * hpRatio);
if (pokemon.currentHp > newStats.hp) {
  pokemon.currentHp = newStats.hp;
}
```

Changed to:
```typescript
const oldMaxHp = pokemon.currentStats.hp;
const hpRatio = pokemon.currentHp / oldMaxHp;

const newStats = ExperienceCalculator.recalculateStats(pokemon, targetSpecies);
pokemon.currentStats = newStats;

pokemon.currentHp = Math.floor(newStats.hp * hpRatio);
if (pokemon.currentHp > newStats.hp) {
  pokemon.currentHp = newStats.hp;
}
if (pokemon.currentHp < 1) {
  pokemon.currentHp = 1;
}
```

#### Technical Details

**HP Ratio Calculation**
- Old (incorrect): `pokemon.currentHp / (oldSpecies.baseStats.hp + 100)`
  - Used raw base stats + 100 instead of actual max HP
  - Did not account for IVs, EVs, level, or nature
  
- New (correct): `pokemon.currentHp / pokemon.currentStats.hp`
  - Uses the Pokemon's actual current maximum HP
  - Correctly represents the HP percentage before evolution

**New Current HP Calculation**
- Old (incorrect): `Math.floor(targetSpecies.baseStats.hp + 100 * hpRatio)`
  - Used raw base stats + 100 instead of recalculated max HP
  - Did not account for IVs, EVs, level, or nature of the new species
  
- New (correct): `Math.floor(newStats.hp * hpRatio)`
  - Uses the newly recalculated maximum HP with all modifiers
  - Correctly applies the HP percentage to the new species

**Safety Check**
- Added minimum HP check to ensure Pokemon never has 0 HP after evolution
- Maintains at least 1 HP to prevent invalid state

#### Impact
- Evolved Pokemon now correctly receive stat bonuses from their new species' base stats
- HP percentage is preserved through evolution
- All other stats (Attack, Defense, Sp. Atk, Sp. Def, Speed) were already correctly recalculated via StatCalculator
- Prevents edge cases where a Pokemon could have more HP than their maximum or 0 HP

#### Files Modified
- src/renderer/src/core/battle/EvolutionManager.ts

#### Dependencies
- No new libraries added
- Uses existing ExperienceCalculator.recalculateStats() for stat recalculation
- Follows existing PokemonInstance and PokemonSpecies data structures
