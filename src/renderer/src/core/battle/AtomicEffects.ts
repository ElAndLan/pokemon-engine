import { PokemonInstance, MoveData, StatName, StatusCondition } from '../data/DataTypes';
import { BattleContext, MoveEvent } from './MoveEngineTypes';
import { calculateDamage } from './DamageCalculator';

export class AtomicEffects {
    /**
     * Applies Damage effect
     */
    public static applyDamage(attacker: PokemonInstance, defender: PokemonInstance, move: MoveData): MoveEvent[] {
        const events: MoveEvent[] = [];
        const result = calculateDamage(attacker, defender, move);
        
        defender.currentHp = Math.max(0, defender.currentHp - result.damage);
        
        events.push({ type: 'Blink', targetId: defender.uuid });
        events.push({ type: 'Damage', targetId: defender.uuid, value: result.damage });

        if (result.effectiveness > 1) events.push({ type: 'Text', message: "It's super effective!", targetId: defender.uuid });
        if (result.effectiveness < 1 && result.effectiveness > 0) events.push({ type: 'Text', message: "It's not very effective...", targetId: defender.uuid });
        if (result.effectiveness === 0) events.push({ type: 'Text', message: `It doesn't affect ${defender.nickname}!`, targetId: defender.uuid });
        if (result.isCritical) events.push({ type: 'Text', message: "A critical hit!", targetId: defender.uuid });

        return events;
    }

    /**
     * Applies Status effect (Paralyze, Sleep, etc)
     */
    public static applyStatus(target: PokemonInstance, status?: StatusCondition, chance: number = 100): MoveEvent[] {
        const events: MoveEvent[] = [];
        if (!status) {
            console.warn(`[MoveEngine] Status effect missing 'status' property.`);
            return events;
        }
        if (target.status === 'None' && Math.random() * 100 < chance) {
            target.status = status;
            events.push({ type: 'Status', targetId: target.uuid, value: status });
            events.push({ type: 'Text', message: `${target.nickname} was ${status.toLowerCase()}ed!`, targetId: target.uuid });
            
            // Initialize Sleep Counter
            if (status === 'Sleep') {
                target.volatile['SleepTurns'] = Math.floor(Math.random() * 3) + 2; // 2-4 turns (since decr happens before move?)
                // Standard is 1-3 turns of sleep.
                // If we check at start of turn and decrement:
                // 3 -> 2 (Sleeps)
                // 2 -> 1 (Sleeps)
                // 1 -> 0 (Wakes up)
            }
        }
        return events;
    }

    /**
     * Applies Stat Change effect
     */
    public static applyStatChange(target: PokemonInstance, stat?: StatName, stages?: number, chance: number = 100): MoveEvent[] {
        const events: MoveEvent[] = [];
        if (!stat || stages === undefined) {
             console.warn(`[MoveEngine] StatChange missing 'stat' or 'stages'.`);
             return events;
        }
        if (Math.random() * 100 < chance) {
            const current = target.statStages[stat] || 0;
            const next = Math.max(-6, Math.min(6, current + stages));
            
            if (next === current) {
                const limit = current === 6 ? "won't go any higher!" : "won't go any lower!";
                events.push({ type: 'Text', message: `${target.nickname}'s ${stat} ${limit}`, targetId: target.uuid });
            } else {
                target.statStages[stat] = next;
                const changeMsg = stages > 1 ? "rose sharply!" : stages > 0 ? "rose!" : stages < -1 ? "fell harshly!" : "fell!";
                events.push({ type: 'StatChange', targetId: target.uuid, value: { stat, stages: next } });
                events.push({ type: 'Text', message: `${target.nickname}'s ${stat} ${changeMsg}`, targetId: target.uuid });
            }
        }
        return events;
    }

    /**
     * Applies Heal effect
     */
    public static applyHeal(target: PokemonInstance, percent: number): MoveEvent[] {
        const healAmt = Math.floor(target.currentStats.hp * (percent / 100));
        const oldHp = target.currentHp;
        target.currentHp = Math.min(target.currentStats.hp, target.currentHp + healAmt);
        const actualHeal = target.currentHp - oldHp;

        return [
            { type: 'Heal', targetId: target.uuid, value: actualHeal },
            { type: 'Text', message: `${target.nickname} regained health!`, targetId: target.uuid }
        ];
    }

    /**
     * Applies Volatile Status (LeechSeed, etc)
     */
    public static applyVolatile(target: PokemonInstance, status: string, chance: number = 100): MoveEvent[] {
        const events: MoveEvent[] = [];
        if (!target.volatile[status] && Math.random() * 100 < chance) {
            // Default Flag (1)
            let value = 1;
            
            // Special Logic for Randomized Durations
            if (status === 'Confusion' || status === 'Bound') {
                value = Math.floor(Math.random() * 4) + 2; // 2-5 (Turns: 1-4)
            }
            
            target.volatile[status] = value;
            events.push({ type: 'EffectUnique', targetId: target.uuid, value: status });
            
            let msg = '';
            if (status === 'LeechSeed') msg = `${target.nickname} was seeded!`;
            if (status === 'Confusion') msg = `${target.nickname} became confused!`;
            
            if (msg) events.push({ type: 'Text', message: msg, targetId: target.uuid });
        } else if (Math.random() * 100 < chance) {
             events.push({ type: 'Text', message: `But it failed!`, targetId: target.uuid });
        }
        return events;
    }
}
