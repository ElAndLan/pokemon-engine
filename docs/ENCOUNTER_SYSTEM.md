# Wild Encounter System - Usage Guide

## Overview
The EncounterManager handles all wild Pokemon generation with proper IVs, movesets, shiny determination, and stat calculation.

## Features
✅ **IV Randomization**: Each stat gets random IV (0-31)
✅ **Level-Appropriate Moves**: Pokemon know the 4 strongest moves for their level
✅ **Shiny Chance**: 1/8192 base rate (customizable)
✅ **Nature Effects**: 25 natures with stat modifiers
✅ **Weighted Encounters**: Rate-based but not hard-limited
✅ **Stat Calculation**: Proper Gen 3+ stat formulas

## Basic Usage

```javascript
const EncounterManager = require('./src/managers/EncounterManager');
const encounterManager = new EncounterManager();

// Generate a wild encounter
const wildPokemon = encounterManager.generateEncounter('route_1', 'grass');

if (wildPokemon) {
    console.log(`Wild ${wildPokemon.name} appeared!`);
    console.log(`Level: ${wildPokemon.level}`);
    console.log(`Shiny: ${wildPokemon.isShiny}`);
    console.log(`Moves: ${wildPokemon.moves.join(', ')}`);
}
```

## Encounter Types

### Standard Encounters
- `grass` - Regular grass
- `tallGrass` - Tall grass (higher levels)
- `cave` - Cave encounters
- `surf` - Surfing on water
- `diving` - Underwater encounters

### Fishing
- `oldRod` - Old Rod fishing
- `goodRod` - Good Rod fishing
- `superRod` - Super Rod fishing

### Special Encounters
- `safari` - Safari Zone
- `darkGrass` - Gen 5 dark grass
- `headbutt` - Headbutt trees
- `rockSmash` - Rock Smash
- `swarm` - Daily swarms
- `pokeRadar` - Poké Radar
- `rustlingGrass` / `shakingGrass` - Phenomenon encounters
- `sweetScent` - Sweet Scent encounters

### Static Encounters
```javascript
// Static encounters are defined in encounters.json
"static": [
    {
        "id": "mewtwo_encounter",
        "pokemonId": "150",
        "level": 70,
        "position": { "x": 15, "y": 8 },
        "respawn": false,
        "shinyLocked": false
    }
]
```

## Generated Pokemon Structure

```javascript
{
    id: "25",                    // Pokemon ID
    name: "Pikachu",            // Pokemon name
    level: 5,                   // Generated level
    types: ["Electric"],        // Type(s)
    baseStats: {...},           // Base stats
    stats: {                    // Calculated stats
        hp: 20,
        attack: 12,
        defense: 10,
        spAttack: 13,
        spDefense: 11,
        speed: 15
    },
    ivs: {                      // Random IVs (0-31)
        hp: 15,
        attack: 22,
        defense: 8,
        spAttack: 31,
        spDefense: 12,
        speed: 19
    },
    evs: {...},                 // EVs (all 0 for wild)
    nature: "Jolly",            // Random nature
    moves: ["thunder_shock", "growl", "tail_whip"],
    currentHP: 20,
    maxHP: 20,
    isShiny: false,             // 1/8192 chance
    ability: "Static",          // Random from possible abilities
    experience: 125,            // XP for current level
    friendship: 70,             // Base friendship
    status: null,               // No status
    isWild: true                // Marks as wild Pokemon
}
```

## Customizing Shiny Chance

```javascript
// Default: 1/8192
encounterManager.setShinyChance(8192);

// With Shiny Charm: 1/2731
encounterManager.setShinyChance(2731);

// For testing: 1/100
encounterManager.setShinyChance(100);
```

## Move Selection Logic

Pokemon learn the 4 strongest moves available at their level:
1. Filters all moves learnable up to current level
2. Sorts by learn level (most recent first)
3. Then sorts by power (strongest first)
4. Takes top 4 moves

Example: Level 25 Charizard knows:
- `flamethrower` (learned at 24, power 90)
- `slash` (learned at 21, power 70)
- `ember` (learned at 7, power 40)
- `growl` (learned at 1, power 0)

## Integration Example

```javascript
// In your game loop
class GameEngine {
    constructor() {
        this.encounterManager = new EncounterManager();
    }
    
    checkForEncounter(currentRoute, terrainType) {
        // 1/187.5 chance per step in grass
        if (Math.random() < 1/187.5) {
            const wildPokemon = this.encounterManager.generateEncounter(
                currentRoute,
                terrainType
            );
            
            if (wildPokemon) {
                this.startBattle(wildPokemon);
            }
        }
    }
}
```

## Notes

- **Performance**: All data loaded once at startup, lookups are O(1)
- **Randomness**: Uses weighted random, not hard limits
- **Stat Calculation**: Uses Gen 3+ formulas with nature modifiers
- **Extensibility**: Easy to add new encounter types or modify rates
