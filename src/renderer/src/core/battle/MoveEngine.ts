import { PokemonInstance, MoveData } from '../data/DataTypes';
import { BattleContext, MoveEvent, MoveExecutionResult } from './MoveEngineTypes';
import { CoreMoveLogic } from './CoreMoveLogic';
import { AtomicEffects } from './AtomicEffects';

export class MoveEngine {
    /**
     * Executes a move and returns a sequence of events for the UI to play
     */
    public static executeMove(attacker: PokemonInstance, defender: PokemonInstance, move: MoveData): MoveExecutionResult {
        const events: MoveEvent[] = [];
        const context: BattleContext = { attacker, defender, allParticipants: [attacker, defender] };

        const allParticipants = [attacker, defender];


        console.log(`[MoveEngine] Executing ${move.name} from ${attacker.nickname} to ${defender.nickname}`);

        // 0. Disable Check
        if (attacker.volatile['Disable'] && attacker.disabledMoveId === move.id) {
            events.push({ type: 'Text', message: `${attacker.nickname}'s ${move.name} is disabled!`, targetId: attacker.uuid });
            return { success: false, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
        }

        // 0. Pre-Turn Checks (Recharge)
        if (attacker.volatile['Recharging']) {
            delete attacker.volatile['Recharging'];
            events.push({ type: 'Text', message: `${attacker.nickname} must recharge!`, targetId: attacker.uuid });
            return { success: false, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
        }

        // 0.5 Check Charge/Invulnerable State (Execution Phase)
        // If we are currently charging THIS move, checking the flag
        const chargeKey = `Charging_${move.id}`;
        const isCharging = !!attacker.volatile[chargeKey];
        
        if (!isCharging && (move.flags?.charge || move.flags?.invulnerable)) {
            // Start Charge Turn
            attacker.volatile[chargeKey] = 1;
            
            // Set Invulnerability
            if (move.flags.invulnerable) {
                const invulnKey = `Invuln_${move.flags.invulnerable}`;
                attacker.volatile[invulnKey] = 1;
                
                let msg = `${attacker.nickname} hid!`;
                if (move.flags.invulnerable === 'Fly' || move.flags.invulnerable === 'Bounce' || move.flags.invulnerable === 'SkyDrop') msg = `${attacker.nickname} flew up high!`;
                if (move.flags.invulnerable === 'Dig') msg = `${attacker.nickname} burrowed underground!`;
                if (move.flags.invulnerable === 'Dive') msg = `${attacker.nickname} hid underwater!`;
                if (move.flags.invulnerable === 'ShadowForce' || move.flags.invulnerable === 'PhantomForce') msg = `${attacker.nickname} vanished instantly!`;
                events.push({ type: 'Text', message: msg, targetId: attacker.uuid });
            } else {
                // Normal Charge
                let msg = `${attacker.nickname} is charging up!`;
                if (move.name === 'Solar Beam' || move.name === 'Solar Blade') msg = `${attacker.nickname} took in sunlight!`;
                if (move.name === 'Skull Bash') msg = `${attacker.nickname} lowered its head!`;
                if (move.name === 'Sky Attack') msg = `${attacker.nickname} became cloaked in a harsh light!`;
                if (move.name === 'Geomancy') msg = `${attacker.nickname} is absorbing energy!`;
                events.push({ type: 'Text', message: msg, targetId: attacker.uuid });
            }
            
            // End turn immediately
            return { success: true, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
        }
        
        // If we ARE charging (isCharging === true), we proceed to execute. 
        if (isCharging) {
             delete attacker.volatile[chargeKey];
             // Clear all invuln keys
             Object.keys(attacker.volatile).forEach(k => {
                 if (k.startsWith('Invuln_')) delete attacker.volatile[k];
             });
        }
        
        // Update Last Move Used (for Disable/Sketch/Mimic)
        attacker.lastMoveUsed = move.id;

        // Status Checks... (Sleep/Freeze/Paralysis...) (PREVIOUSLY EXISTING CODE)
        if (attacker.status === 'Sleep') {
            const turns = attacker.volatile['SleepTurns'] || 0;
            if (turns > 1) {
                attacker.volatile['SleepTurns'] = turns - 1;
                events.push({ type: 'Text', message: `${attacker.nickname} is fast asleep.`, targetId: attacker.uuid });
                return { success: false, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
            } else {
                attacker.status = 'None';
                events.push({ type: 'Text', message: `${attacker.nickname} woke up!`, targetId: attacker.uuid });
            }
        }

        if (attacker.status === 'Freeze') {
            if (Math.random() < 0.2) {
                attacker.status = 'None';
                events.push({ type: 'Text', message: `${attacker.nickname} thawed out!`, targetId: attacker.uuid });
            } else {
                events.push({ type: 'Text', message: `${attacker.nickname} is frozen solid!`, targetId: attacker.uuid });
                return { success: false, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
            }
        }

        if (attacker.volatile['Flinch']) {
             events.push({ type: 'Text', message: `${attacker.nickname} flinched!`, targetId: attacker.uuid });
             return { success: false, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
        }
        
        if (attacker.volatile['Confusion']) {
            events.push({ type: 'Text', message: `${attacker.nickname} is confused!`, targetId: attacker.uuid });
            const turns = attacker.volatile['Confusion'];
            if (turns <= 1) {
                delete attacker.volatile['Confusion'];
                events.push({ type: 'Text', message: `${attacker.nickname} snapped out of its confusion!`, targetId: attacker.uuid });
            } else {
                attacker.volatile['Confusion'] = turns - 1;
                if (Math.random() < 0.5) {
                    events.push({ type: 'Text', message: `It hurt itself in its confusion!`, targetId: attacker.uuid });
                    const confusionMove: any = { name: 'Confusion', power: 40, type: 'Normal' as any, category: 'Physical', accuracy: 100 };
                    const dmgEvents = AtomicEffects.applyDamage(attacker, attacker, confusionMove);
                    events.push(...dmgEvents);
                    return { success: false, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
                }
            }
        }
        
        if (attacker.status === 'Paralysis') {
            if (Math.random() < 0.25) {
                events.push({ type: 'Text', message: `${attacker.nickname} is fully paralyzed!`, targetId: attacker.uuid });
                return { success: false, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
            }
        }

        // 1. Initial Message
        events.push({ type: 'Text', message: `${attacker.nickname} used ${move.name}!`, targetId: defender.uuid });

         // 2. Type Immunity Check & Invulnerability Check
        if (move.target !== 'Self') {
             // Invulnerable Check
             const isInvuln = Object.keys(defender.volatile).some(k => k.startsWith('Invuln_'));
             if (isInvuln) {
                 // TODO: Check for moves that bypass (Thunder vs Fly, Earthquake vs Dig)
                 events.push({ type: 'Text', message: `${defender.nickname} avoided the attack!`, targetId: defender.uuid });
                 return { success: true, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
             }

             let typeMult = CoreMoveLogic.getTypeMultiplier(move.type, defender.types);
             if (typeMult === 0) {
                 events.push({ type: 'Text', message: `It doesn't affect ${defender.nickname}!`, targetId: defender.uuid });
                 return { success: true, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
             }
        }

        // 3. Accuracy Check
        if (!CoreMoveLogic.checkHit(attacker, defender, move)) {
            console.log(`[MoveEngine] ${move.name} missed.`);
            events.push({ type: 'Text', message: `${attacker.nickname}'s attack missed!`, targetId: defender.uuid });
            
            // If High Jump Kick / Jump Kick -> Crash
            if (move.id === 'high_jump_kick' || move.id === 'jump_kick') {
                const crashDmg = Math.floor(attacker.currentStats.hp / 2);
                attacker.currentHp = Math.max(0, attacker.currentHp - crashDmg);
                events.push({ type: 'Text', message: `${attacker.nickname} kept going and crashed!`, targetId: attacker.uuid });
                events.push({ type: 'Damage', targetId: attacker.uuid, value: crashDmg });
            }
            
            return { success: true, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
        }

        // 4. Process Effects Sequentially (Multi-Hit)
        let lastDamageDealt = 0;
        let hits = 1;
        
        if (move.multiHit) {
            hits = Math.floor(Math.random() * (move.multiHit.max - move.multiHit.min + 1)) + move.multiHit.min;
        } else if (move.description.includes('2 to 5 times')) {
            // Fallback if data not yet migrated (Legacy support)
            hits = Math.floor(Math.random() * 4) + 2; 
        } else if (move.description.includes('hits twice')) {
             hits = 2;
        }

        let totalHits = 0;
        for (let i = 0; i < hits; i++) {
            totalHits++;
            for (const effect of (move.effects || [])) {
                 const { events: effectEvents, damageDealt } = this.processSingleEffect(effect, context, move, lastDamageDealt);
                 events.push(...effectEvents);
                 if (damageDealt > 0) lastDamageDealt = damageDealt;
                 
                 // Stop if defender fainted
                 if (defender.currentHp <= 0) break;
            }
            if (defender.currentHp <= 0) break;
        }

        if (hits > 1) {
            events.push({ type: 'Text', message: `Hit ${totalHits} time(s)!`, targetId: attacker.uuid });
        }

        // 5. Recoil
        if (move.recoil) {
            let recoilDmg = 0;
            if (move.recoil.type === 'MaxHP') {
                // Suicide moves like Explosion/Self-Destruct
                recoilDmg = Math.floor(attacker.currentStats.hp * (move.recoil.percent / 100)); // Should be 100% usually
                // Or currentHp? Usually they faint, so taking MaxHP damage guarantees it unless Sturdy.
                // Actually Self-Destruct just faints the user.
            } else if (lastDamageDealt > 0) {
                // Regular recoil (Take Down, etc)
                recoilDmg = Math.floor(lastDamageDealt * (move.recoil.percent / 100));
            }
            
            if (recoilDmg > 0) {
                attacker.currentHp = Math.max(0, attacker.currentHp - recoilDmg);
                events.push({ type: 'Damage', targetId: attacker.uuid, value: recoilDmg });
                events.push({ type: 'Text', message: `${attacker.nickname} is hit with recoil!`, targetId: attacker.uuid });
            }
        }
        
        // 6. Drain (Native Property)
        if (move.drainPercent && lastDamageDealt > 0) {
             const healAmt = Math.floor(lastDamageDealt * (move.drainPercent / 100));
             if (healAmt > 0) {
                attacker.currentHp = Math.min(attacker.currentStats.hp, attacker.currentHp + healAmt);
                events.push({ type: 'Heal', targetId: attacker.uuid, value: healAmt });
                events.push({ type: 'Text', message: `${attacker.nickname} drained energy!`, targetId: attacker.uuid });
             }
        }
        
        // 7. Apply Recharge (if applicable)
        if (move.flags?.recharge) {
            attacker.volatile['Recharging'] = 1;
        }

        console.log(`[MoveEngine] ${move.name} execution finished with ${events.length} events.`);
        return { success: true, events, finalAttackerState: attacker, finalDefenderState: defender, allParticipants };
    }

    private static processSingleEffect(effect: any, context: BattleContext, move: MoveData, lastDamage: number): { events: MoveEvent[], damageDealt: number } {
        const { attacker, defender } = context;
        let damageDealt = 0;
        let events: MoveEvent[] = [];

        switch (effect.type) {
            case 'Damage':
                events = AtomicEffects.applyDamage(attacker, defender, move);
                // Extract damage value if needed for Drain
                const dmgEvt = events.find(e => e.type === 'Damage');
                if (dmgEvt) damageDealt = dmgEvt.value;
                break;

            case 'Status':
                events = AtomicEffects.applyStatus(defender, effect.status, effect.chance);
                break;

            case 'StatChange':
                const target = effect.target === 'Self' || move.target === 'Self' ? attacker : defender;
                events = AtomicEffects.applyStatChange(target, effect.stat, effect.stages, effect.chance);
                break;

            case 'Heal':
                events = AtomicEffects.applyHeal(attacker, effect.healPercent);
                break;

            case 'Drain':
                // Special: Heals attacker based on percentage of damage just dealt
                const healPercent = effect.healPercent || 50;
                const healAmt = Math.floor(lastDamage * (healPercent / 100));
                if (healAmt > 0) {
                     attacker.currentHp = Math.min(attacker.currentStats.hp, attacker.currentHp + healAmt);
                     events.push({ type: 'Heal', targetId: attacker.uuid, value: healAmt });
                     events.push({ type: 'Text', message: `${attacker.nickname} drained energy!`, targetId: attacker.uuid });
                }
                break;

            case 'Unique':
                if (effect.volatileStatus === 'StealItem') {
                    if (!attacker.heldItem && defender.heldItem) {
                        attacker.heldItem = defender.heldItem;
                        defender.heldItem = undefined;
                        events.push({ type: 'Text', message: `${attacker.nickname} stole ${attacker.heldItem}!`, targetId: attacker.uuid });
                    }
                } else if (effect.volatileStatus === 'Disable') {
                    // Disable Logic
                    if (defender.lastMoveUsed) {
                        defender.volatile['Disable'] = 4; // 4 turns? "For 5 turns" usually means 4? Or 5.
                        defender.disabledMoveId = defender.lastMoveUsed;
                         events.push({ type: 'Text', message: `${defender.nickname}'s ${defender.lastMoveUsed} was disabled!`, targetId: defender.uuid });
                    } else {
                         events.push({ type: 'Text', message: `But it failed!`, targetId: defender.uuid });
                    }
                } else if (effect.volatileStatus) {
                    events = AtomicEffects.applyVolatile(defender, effect.volatileStatus, effect.chance);
                } else {
                    console.warn(`[MoveEngine] Unique effect missing volatileStatus`, effect);
                }
                break;
        }

        return { events, damageDealt };
    }
}
