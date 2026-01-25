import { BattleState, clonePokemon } from './StateReader';
import { PokemonInstance, MoveData } from '../data/DataTypes';
import { calculateDamage } from './DamageCalculator';

// SPEC 2. Simulator
// Returns a new BattleState representing the predicted outcome.

export interface Action {
    type: 'move' | 'switch' | 'item';
    actorId: string; // 'player' or 'enemy'
    move?: MoveData;
    // switchTo?: string;
    // item?: string;
}

export class Simulator {
    
    public static run(initialState: BattleState, action: Action): BattleState {
        // 1. Clone State (Immutable operation)
        const nextState: BattleState = {
            player: clonePokemon(initialState.player),
            enemy: clonePokemon(initialState.enemy),
            weather: { ...initialState.weather },
            terrain: { ...initialState.terrain },
            isAiTurn: initialState.isAiTurn
        };

        // 2. Identify Actor & Target
        let actor: PokemonInstance | null = null;
        let target: PokemonInstance | null = null;

        if (action.actorId === 'enemy') {
            actor = nextState.enemy;
            target = nextState.player;
        } else {
            actor = nextState.player;
            target = nextState.enemy;
        }

        // 3. Apply Action
        if (action.type === 'move' && action.move) {
            Simulator.applyMove(actor, target, action.move);
        }

        // 4. End of Turn Effects (Future: Burn damage, Weather damage)
        // Simulator.applyEndTurn(actor);
        // Simulator.applyEndTurn(target);

        return nextState;
    }

    private static applyMove(actor: PokemonInstance, target: PokemonInstance, move: MoveData): void {
        // A. Damage
        if (move.category !== 'Status') {
            const result = calculateDamage(actor, target, move);
            target.currentHp = Math.max(0, target.currentHp - result.damage);
        }

        // B. Status Effects (Simplified prediction)
        // The AI assumes success for calculation (Optimistic evaluation)
        // or we use probability weights in the Scorer. 
        // For the Simulator, let's apply the "Success" state so the Scorer can value it.
        // The Scorer will discount it by accuracy. 
        if (move.effects) {
            for (const effect of move.effects) {
                // Apply Status
                if (effect.type === 'Status' && effect.status) {
                    if (target.status === 'None') {
                        target.status = effect.status;
                    }
                }
                
                // Apply Stat Changes
                if (effect.type === 'StatChange' && effect.stat) {
                    const stages = effect.stages || 0;
                    if (effect.stat === 'all') {
                        // Ancient Power logic
                    } else if (effect.stat !== 'accuracy' && effect.stat !== 'evasion') {
                        const currentStage = (target.statStages as any)[effect.stat] || 0;
                        // Determine target (Self or Enemy is usually inherent in move data, but logic here assumes 'target')
                        // Wait, buffs define target usually.
                        // For simplicity in this iteration, we assume debuffs target enemy, buffs target self.
                        // We need move metadata for 'target'.
                        
                        // For now: Positive = Self, Negative = Enemy? 
                        // Actually MoveData effects usually imply it.
                        // Let's assume standard behavior:
                        // If effect.target === 'self' -> apply to actor
                        // If effect.target === 'enemy' -> apply to target
                    }
                }
            }
        }
    }
}
