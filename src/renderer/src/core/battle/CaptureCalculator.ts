import { PokemonInstance, PokemonSpecies, StatusCondition } from '../data/DataTypes';
import { ItemData } from '../data/ItemData';

export interface CaptureContext {
    isDark?: boolean; // Dusk Ball
    isCave?: boolean; // Dusk Ball
    isWater?: boolean; // Dive Ball / Net Ball
    isUnderwater?: boolean; // Dive Ball
    turnCount?: number; // Timer Ball
    isFirstTurn?: boolean; // Quick Ball
    isFishing?: boolean; // Lure Ball (if supported)
    caughtSpecies?: boolean; // Repeat Ball (Requires Pokedex check passed in)
}

export interface CaptureResult {
    caught: boolean;
    shakes: number; // 0-4 (4 = caught)
    critical: boolean;
}

export class CaptureCalculator {
    
    /**
     * Calculate capture result using Gen 3/4 mixed formula
     */
    public static calculateCapture(
        target: PokemonInstance, 
        species: PokemonSpecies, 
        ball: ItemData, 
        context: CaptureContext = {}
    ): CaptureResult {
        
        // 1. Ball Bonus
        let ballBonus = 1.0;
        const ballId = ball.id;
        
        // Master Ball - Bypass everything
        if (ballId === 'master-ball') {
            return { caught: true, shakes: 4, critical: false }; // Always catch
        }

        // Standard Balls
        if (ballId === 'great-ball') ballBonus = 1.5;
        if (ballId === 'ultra-ball') ballBonus = 2.0;
        if (ballId === 'safari-ball') ballBonus = 1.5;

        // Special Balls
        if (ballId === 'net-ball') {
            if (species.types.includes('Water') || species.types.includes('Bug')) {
                ballBonus = 3.0; // Gen 7 is 3.5, keeping 3.0 for standard
            }
        }
        
        if (ballId === 'dive-ball') {
            if (context.isUnderwater || context.isWater) { 
                ballBonus = 3.5;
            }
        }
        
        if (ballId === 'nest-ball') {
            // ((41 - Level) / 10)
            if (target.level < 30) {
                ballBonus = Math.max(1, (41 - target.level) / 10);
            }
        }
        
        if (ballId === 'repeat-ball') {
            if (context.caughtSpecies) {
                ballBonus = 3.0; // Gen 7 is 3.5
            }
        }
        
        if (ballId === 'timer-ball') {
            // Gen 5+: 1 + Turns * 1229/4096 (approx 0.3 per turn), max 4.0
            const turns = context.turnCount || 1;
            ballBonus = Math.min(4.0, 1 + (turns * 0.3)); 
        }
        
        if (ballId === 'quick-ball') {
            if (context.isFirstTurn) {
                ballBonus = 5.0; // Gen 5+
            }
        }
        
        if (ballId === 'dusk-ball') {
            if (context.isDark || context.isCave) {
                ballBonus = 3.0; // Gen 7 is 3.0
            }
        }
        
        if (ballId === 'luxury-ball') ballBonus = 1.0;
        if (ballId === 'heal-ball') ballBonus = 1.0;
        if (ballId === 'premier-ball') ballBonus = 1.0;
        if (ballId === 'cherish-ball') ballBonus = 1.0;

        // Heavy Ball Stub (Weight logic needs weight in species)
        if (ballId === 'heavy-ball') {
            // Need weight. Assume 0 modifier for now.
            // Formula typically adds/subtracts from CatchRate directly, not a multiplier.
            // Complex to mix with Gen 3/4 formula which uses multipliers.
            // Simplified:
            ballBonus = 1.0;
        }

        // 2. Calculate Modified Catch Rate (X)
        // Formula: X = ( ( 3 * MaxHP - 2 * HP ) * (CatchRate * BallBonus) ) / ( 3 * MaxHP ) ) * StatusMod
        
        const maxHp = target.currentStats.hp;
        const currentHp = target.currentHp;
        const catchRate = species.catchRate || 45; // Default fallback
        
        // Status Modifier
        let statusMod = 1.0;
        if (target.status === 'Sleep' || target.status === 'Freeze') statusMod = 2.5; // Gen 5+
        else if (target.status !== 'None') statusMod = 1.5;
        
        const numerator = (3 * maxHp - 2 * currentHp) * (catchRate * ballBonus);
        const denominator = 3 * maxHp;
        
        let modifiedCatchRate = Math.floor((numerator / denominator) * statusMod);
        
        // Cap
        if (modifiedCatchRate < 1) modifiedCatchRate = 1;
        if (modifiedCatchRate > 255) modifiedCatchRate = 255;
        
        // 3. Shake Check (Gen 3/4 Logic)
        // Shake Probability (Y) = 65536 / (255 / X)^0.25 (Sort of)
        // Simplified Check:
        
        // Guaranteed catch?
        if (modifiedCatchRate === 255) {
             return { caught: true, shakes: 4, critical: false };
        }
        
        // Calculate B (Shake Probability)
        // B = 1048560 / sqrt(sqrt(16711680 / X))
        const b = 1048560 / Math.sqrt(Math.sqrt(16711680 / modifiedCatchRate));
        
        let shakes = 0;
        for (let i = 0; i < 4; i++) {
            const rand = Math.floor(Math.random() * 65536);
            if (rand < b) {
                shakes++;
            } else {
                break;
            }
        }
        
        return {
            caught: shakes === 4,
            shakes,
            critical: false // Critical capture logic omitted for simplicity unless requested
        };
    }
}
