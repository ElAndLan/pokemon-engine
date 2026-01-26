import { PokemonInstance, MoveData, StatName, StatusCondition, WeatherType } from '../data/DataTypes';

export interface BattleContext {
    attacker: PokemonInstance;
    defender: PokemonInstance;
    allParticipants: PokemonInstance[];
    weather?: WeatherType;
    terrain?: string;
}

export type MoveEventType = 
    | 'Text'            // Display message
    | 'Damage'          // HP reduction animation
    | 'Heal'            // HP restoration animation
    | 'Status'          // Condition applied (icon change)
    | 'StatChange'      // Stage change animation (Up/Down)
    | 'Blink'           // Sprite blink
    | 'Faint'           // Sprite slide down
    | 'EffectUnique'    // Special logic trigger
    | 'Fail';           // "But it failed!"

export interface MoveEvent {
    type: MoveEventType;
    message?: string;
    targetId: string;   // Unique ID of the affected Pokemon
    value?: any;        // Damage amount, stat name, etc.
}

export interface MoveExecutionResult {
    success: boolean;
    events: MoveEvent[];
    finalAttackerState: PokemonInstance;
    finalDefenderState: PokemonInstance;
    allParticipants: PokemonInstance[];
}

/**
 * Functional Interface for atomic effect handlers
 */
export interface EffectHandler {
    apply(effect: any, context: BattleContext): MoveEvent[];
}
