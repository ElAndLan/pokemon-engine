import { PokemonInstance, Stats } from '../data/DataTypes';

// SPEC 2. Architecture & Modules: StateReader
// Encapsulates HP, status, stats, field effects

export interface BattleState {
    player: PokemonInstance;
    enemy: PokemonInstance;
    
    // Field Effects (Future)
    weather: { type: string, duration: number };
    terrain: { type: string, duration: number };
    
    // Helper to track whose turn it is in simulation (optional)
    isAiTurn: boolean;
}

// Deep Clone to ensure simulation doesn't mutate real state
export function clonePokemon(mon: PokemonInstance): PokemonInstance {
    return {
        ...mon,
        baseStats: { ...mon.baseStats },
        currentStats: { ...mon.currentStats },
        statStages: { ...mon.statStages },
        types: [...mon.types],
        volatile: { ...mon.volatile },
        moves: mon.moves.map(m => ({ ...m })) // Clone moves to track PP if needed
    };
}

export function createBattleState(player: PokemonInstance, enemy: PokemonInstance): BattleState {
    return {
        player: clonePokemon(player),
        enemy: clonePokemon(enemy),
        weather: { type: 'None', duration: 0 },
        terrain: { type: 'None', duration: 0 },
        isAiTurn: true
    };
}
