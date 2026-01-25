import { PokemonInstance, MoveData, getEffectiveStat } from '../data/DataTypes';
import { getTypeEffectiveness } from './TypeChart';

export interface DamageResult {
    damage: number;
    isCritical: boolean;
    effectiveness: number; // 1 = normal, >1 = super, <1 = not very
    details: string[]; // For debugging logs
}

export const SETTINGS = {
    USE_RANDOM: true, // Toggle for testing
    CRIT_MULTIPLIER: 1.5
};

export function calculateDamage(attacker: PokemonInstance, defender: PokemonInstance, move: MoveData): DamageResult {
    const details: string[] = [];
    
    // Status Moves deal 0 direct damage (unless handled differently)
    if (move.category === 'Status') {
        return { damage: 0, isCritical: false, effectiveness: 1, details };
    }

    // --- Fixed Damage Moves ---
    if (move.id === 'night_shade' || move.id === 'seismic_toss') {
        details.push('Fixed Damage: Level');
        return { damage: attacker.level, isCritical: false, effectiveness: 1, details };
    }
    if (move.id === 'dragon_rage') {
        details.push('Fixed Damage: 40');
        return { damage: 40, isCritical: false, effectiveness: 1, details };
    }
    if (move.id === 'sonic_boom') {
        details.push('Fixed Damage: 20');
        return { damage: 20, isCritical: false, effectiveness: 1, details };
    }

    // 1. Get Effective Stats (Spec 3.2: A & D)
    let attackStat: 'attack' | 'spAttack' = 'attack';
    let defenseStat: 'defense' | 'spDefense' = 'defense';

    if (move.category === 'Special') {
        attackStat = 'spAttack';
        defenseStat = 'spDefense';
    }

    const A = getEffectiveStat(attacker, attackStat);
    const D = getEffectiveStat(defender, defenseStat);
    const Power = move.power || 0;
    const Level = attacker.level;

    details.push(`Level: ${Level}, Power: ${Power}, A: ${A}, D: ${D}`);

    if (Power === 0) {
        return { damage: 0, isCritical: false, effectiveness: 1, details };
    }

    // 2. Base Damage Calculation
    // floor( floor( floor( ((2 * Level / 5) + 2) * Power * A / D) / 50 ) + 2 )
    const baseDamage = Math.floor(Math.floor(Math.floor((2 * Level / 5 + 2) * Power * A / D) / 50) + 2);
    details.push(`Base Damage: ${baseDamage}`);

    // 3. Modifiers
    // Modifier = STAB * Type * Crit * Random(0.85, 1.0) * Burn * Weather
    
    // STAB
    let stab = 1.0;
    if (attacker.types.some(t => t.toLowerCase() === move.type.toLowerCase())) {
        stab = 1.5;
        details.push('STAB applied (1.5x)');
    }

    // Type Effectiveness
    const typeEff = getTypeEffectiveness(move.type, defender.types);
    if (typeEff !== 1) details.push(`Type Effectiveness: ${typeEff}x`);

    // Critical (Spec 3.2 rule: 1.0 unless extended. We use 1/16 chance usually)
    let crit = 1.0;
    let isCritical = false;
    // Simple 6.25% chance for now
    if (SETTINGS.USE_RANDOM && Math.random() < 0.0625) { 
        crit = SETTINGS.CRIT_MULTIPLIER;
        isCritical = true;
        details.push('Critical Hit! (1.5x)');
    }

    // Random Variance (0.85 to 1.00)
    let random = 1.0;
    if (SETTINGS.USE_RANDOM) {
        random = (Math.floor(Math.random() * 16) + 85) / 100; // 0.85 to 1.00
        details.push(`Random Factor: ${random}`);
    }

    // Final Calculation
    const damage = Math.floor(baseDamage * stab * typeEff * crit * random);
    details.push(`Final Damage: ${damage}`);

    return { damage, isCritical, effectiveness: typeEff, details };
}
