import { PokemonInstance, StatName, getStatStageMultiplier } from '../data/DataTypes';
import { AbilityRegistry } from './Abilities';

export function getEffectiveStat(mon: PokemonInstance, stat: StatName, ctx?: any): number {
    let value = mon.currentStats[stat] || 0;
    
    // Apply Stage Multiplier
    const stage = mon.statStages?.[stat] || 0;
    value *= getStatStageMultiplier(stage);
    
    // Status Modifiers (Spec 4.2)
    if (stat === 'attack' && mon.status === 'Burn') {
        if (mon.ability !== 'Guts') {
            value *= 0.5;
        }
    }
    
    if (stat === 'speed' && mon.status === 'Paralysis') {
        value *= 0.5;
    }

    // Ability Modifiers
    // ctx usually contains { owner: mon, ... }
    // If ctx is not provided, we create a minimal one.
    const abilityCtx = ctx || { owner: mon };
    
    // Apply Ability Modifiers (e.g. Huge Power, Guts)
    // Hook: onStatCalculation(stat, value, ctx) -> newValue
    // But applyModifier signature is: (id, hook, value, ctx) -> value
    // So the hook will receive (value, ctx) and return value.
    // We need to pass 'stat' in ctx.
    const statCtx = { ...abilityCtx, statName: stat };
    value = AbilityRegistry.applyModifier(mon.ability, 'onStatCalculation', value, statCtx);

    return Math.floor(value);
}
