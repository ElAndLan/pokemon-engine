import { PokemonInstance, MoveData, getEffectiveStat } from '../data/DataTypes';
import { TypeChart, PokemonType } from './TypeChart';

export class CoreMoveLogic {
    /**
     * Checks if a move hits based on accuracy and evasion stages
     */
    public static checkHit(attacker: PokemonInstance, defender: PokemonInstance, move: MoveData): boolean {
        if (!move) return false;
        const acc = move.accuracy ?? 100;
        // Never-miss moves (Accuracy > 100 in our DB)
        if (acc > 100) return true;

        // Base Accuracy
        let accuracy = acc;

        // Stages (simplified: 1.0 +/- 0.25 per stage for now)
        const accStage = attacker.statStages?.accuracy ?? 0;
        const evaStage = defender.statStages?.evasion ?? 0;
        const combinedStage = Math.max(-6, Math.min(6, accStage - evaStage));
        
        const stageMultipliers: Record<number, number> = {
            '-6': 0.33, '-5': 0.37, '-4': 0.43, '-3': 0.5, '-2': 0.6, '-1': 0.75,
            '0': 1.0,
            '1': 1.33, '2': 1.66, '3': 2.0, '4': 2.33, '5': 2.66, '6': 3.0
        };

        const finalAccuracy = accuracy * (stageMultipliers[combinedStage] || 1.0);
        
        return Math.random() * 100 < finalAccuracy;
    }

    /**
     * Determines if a move is a critical hit
     */
    public static checkCritical(attacker: PokemonInstance, defender: PokemonInstance, move: MoveData): boolean {
        // Standard 1/16 chance (6.25%)
        // High crit ratio moves could be 1/8 or 1/4
        const isHighCrit = (move.critRate && move.critRate > 0) || move.description?.toLowerCase().includes('high critical') || false;
        const threshold = isHighCrit ? 12.5 : 6.25;
        
        return Math.random() * 100 < threshold;
    }

    /**
     * Gets total type effectiveness multiplier
     */
    public static getTypeMultiplier(moveType: string, defenderTypes: string[]): number {
        let multiplier = 1.0;
        const atkType = moveType.toLowerCase() as PokemonType;

        for (const defTypeStr of defenderTypes) {
            const defType = defTypeStr.toLowerCase() as PokemonType;
            if (TypeChart[atkType] && TypeChart[atkType][defType] !== undefined) {
                multiplier *= TypeChart[atkType][defType]!;
            }
        }
        return multiplier;
    }
}
