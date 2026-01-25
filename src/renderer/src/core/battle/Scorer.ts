import { BattleState } from './StateReader';
import { Action } from './Simulator';
// import { calculateDamage } from './DamageCalculator'; // We already simulated damage, we check result state

// SPEC 4. Scoring Framework
// TotalScore = DamageScore + StatusScore + StatScore + TacticalBonus - RiskPenalty

export class Scorer {
    
    public static score(initialState: BattleState, predictedState: BattleState, action: Action): number {
        let totalScore = 0;
        
        // Context
        // We are scoring from the perspective of the ACTOR (Action.actorId).
        // If actor is 'enemy', we want 'player' to suffer and 'enemy' to prosper.
        
        const actorBefore = action.actorId === 'enemy' ? initialState.enemy : initialState.player;
        const targetBefore = action.actorId === 'enemy' ? initialState.player : initialState.enemy;
        
        const actorAfter = action.actorId === 'enemy' ? predictedState.enemy : predictedState.player;
        const targetAfter = action.actorId === 'enemy' ? predictedState.player : predictedState.enemy;
        
        const move = action.move;
        if (!move) return 0; // Switching not yet scored

        // --- 4.1 DAMAGE SCORE ---
        const damageDealt = targetBefore.currentHp - targetAfter.currentHp;
        const damagePercent = damageDealt / targetBefore.currentHp; // % of Current HP taken
        const damageBase = damagePercent * 100;
        
        // Kill Bonus
        let killBonus = 0;
        if (targetAfter.currentHp <= 0) killBonus = 1000;
        
        // Accuracy Weight
        const acc = (move.accuracy === undefined || move.accuracy === 0) ? 1.0 : (move.accuracy / 100);
        
        const damageScore = (damageBase * acc) + killBonus;
        totalScore += damageScore;
        
        // --- 4.2 STATUS SCORE ---
        let statusScore = 0;
        // Did we inflict status?
        if (targetBefore.status === 'None' && targetAfter.status !== 'None') {
            const s = targetAfter.status;
            if (s === 'Sleep' || s === 'Freeze') statusScore = 60;
            else if (s === 'Burn') statusScore = 40; // Add check for Physical Attacker later
            else if (s === 'Paralysis') statusScore = 40;
            else if (s === 'Poison') statusScore = 20;
        }
        totalScore += statusScore;

        // --- 4.3 STAT SCORE (Setup) ---
        // TODO: Compare statStages in before/after
        
        // --- 4.5 RISK PENALTY (Accuracy) ---
        // If accuracy < 80% and NOT a kill shot, penalize
        if (acc < 0.8 && killBonus === 0) {
            // Apply Penalty
            totalScore -= 20; 
        }

        return totalScore;
    }
}
