# LapiesLog

## Development Log

### 2025-01-25 - Stat Calculation Refactor & Level-Up UI Improvements

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
