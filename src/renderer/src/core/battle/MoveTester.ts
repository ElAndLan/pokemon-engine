import { PokemonInstance, MoveData } from '../data/DataTypes';
import { MoveEngine } from './MoveEngine';
import { BattleSim } from './BattleSim';

export interface TestResult {
    moveId: string;
    passed: boolean;
    errors: string[];
}

export class MoveTester {
    /**
     * Tests a single move for functional correctness
     */
    public static testMove(move: MoveData): TestResult {
        const errors: string[] = [];
        
        // Setup
        const attacker = BattleSim.createMockMon('Attacker', ['Normal']);
        const defender = BattleSim.createMockMon('Defender', ['Normal']);
        
        // Execute
        const result = MoveEngine.executeMove(attacker, defender, move);

        // Assertions
        if (!result.success && move.accuracy <= 100) {
            // Note: Misses happen randomly. In a real test we'd mock Math.random.
            // For mass audit, we'll just track if it crashes.
        }

        // Basic sanity check: Move should generate events
        if (result.events.length === 0) {
            errors.push(`Move ${move.id} generated 0 events.`);
        }

        // Specific effect checks
        for (const effect of (move.effects || [])) {
            switch (effect.type) {
                case 'Damage':
                    if (defender.currentHp === defender.currentStats.hp && move.accuracy > 0) {
                        // If it didn't miss, HP should change
                        const missed = result.events.some(e => e.message?.includes('missed'));
                        const immune = result.events.some(e => e.message?.includes('affect'));
                        if (!missed && !immune) errors.push(`Damage effect didn't reduce HP.`);
                    }
                    break;
                case 'Status':
                    // Note: Chance based, but check logic
                    break;
                case 'Unique':
                    if (effect.volatileStatus === 'LeechSeed' && !defender.volatile['LeechSeed']) {
                        errors.push(`Leech Seed move didn't apply volatile status.`);
                    }
                    break;
            }
        }

        return {
            moveId: move.id,
            passed: errors.length === 0,
            errors
        };
    }
}
