import { BattleState, createBattleState } from './StateReader';
import { Simulator, Action } from './Simulator';
import { Scorer } from './Scorer';
import { PokemonInstance, MoveData } from '../data/DataTypes';
import { DataManager } from '../data/DataManager';

export class BattleAI {
    private dataManager: DataManager;

    constructor(dataManager: DataManager) {
        this.dataManager = dataManager;
    }

    public getBestAction(player: PokemonInstance, enemy: PokemonInstance): { moveIndex: number, score: number, debug: string } {
        // 1. Create Snapshot
        const state = createBattleState(player, enemy);
        
        let bestActionIndex = -1;
        let bestScore = -Infinity;
        let debugLog = 'AI Thinking:\n';

        // 2. Iterate Moves
        // (Assuming enemy moves are what we are choosing)
        const moves = enemy.moves;
        
        moves.forEach((moveInst, index) => {
            // Need Move Data (Passed in or loaded? BattleScene needs to ensure it's loaded)
            // We'll assume DataManager is available or pass the full Move objects
            const moveData = this.dataManager.getMove(moveInst.moveId);
            
            if (!moveData) {
                console.warn(`[BattleAI] Move data missing for ${moveInst.moveId}`);
                return;
            }
            
            // 3. Create Action Candidate
            const action: Action = {
                type: 'move',
                actorId: 'enemy',
                move: moveData
            };
            
            // 4. Simulate
            const predictedState = Simulator.run(state, action);
            
            // 5. Score
            const score = Scorer.score(state, predictedState, action);
            
            debugLog += `Move ${moveData.name}: Score ${score.toFixed(1)}\n`;
            
            if (score > bestScore) {
                bestScore = score;
                bestActionIndex = index;
            }
        });

        console.log(debugLog);
        return { moveIndex: bestActionIndex, score: bestScore, debug: debugLog };
    }
}
