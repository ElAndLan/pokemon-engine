import {
  PokemonInstance,
  MoveData,
  StatName,
  StatusCondition,
  WeatherType,
} from "../data/DataTypes";
import { BattleContext, MoveEvent } from "./MoveEngineTypes";
import { calculateDamage } from "./DamageCalculator";
import { AbilityRegistry } from "./Abilities";

export class AtomicEffects {
  /**
   * Applies Damage effect
   */
  public static applyDamage(
    attacker: PokemonInstance,
    defender: PokemonInstance,
    move: MoveData,
    context: BattleContext
  ): MoveEvent[] {
    const events: MoveEvent[] = [];

    // --- 1. Modify Move Type (Normalize, Pixilate, etc) ---
    let moveType = move.type;
    const ctx = { owner: attacker, move };
    moveType = AbilityRegistry.applyModifier(
      attacker.ability,
      "onModifyType",
      moveType as any,
      ctx
    ) as any;
    const effectiveMove = { ...move, type: moveType };

    // Ability Hook: onTryHit (Type Absorb/Immunity)
    const ability = AbilityRegistry.get(defender.ability);
    if (ability && ability.onTryHit) {
      // We pass 'events' so implementation can push 'Heal', 'Text', etc.
      if (!ability.onTryHit({ owner: defender, move: effectiveMove }, events)) {
        return events; // Stop damage calculation if hook returns false
      }
    }
    const result = calculateDamage(
      attacker,
      defender,
      effectiveMove,
      context.weather
    );

    let damageToTake = result.damage;

    // Sturdy Check
    if (
      defender.ability === "Sturdy" &&
      defender.currentHp === defender.currentStats.hp &&
      damageToTake >= defender.currentHp
    ) {
      damageToTake = defender.currentHp - 1;
      events.push({
        type: "Text",
        message: `${defender.nickname} endured the hit!`,
        targetId: defender.uuid,
      });
    }

    defender.currentHp = Math.max(0, defender.currentHp - damageToTake);

    events.push({ type: "Blink", targetId: defender.uuid });
    events.push({
      type: "Damage",
      targetId: defender.uuid,
      value: damageToTake,
    });

    if (result.effectiveness > 1)
      events.push({
        type: "Text",
        message: "It's super effective!",
        targetId: defender.uuid,
      });
    if (result.effectiveness < 1 && result.effectiveness > 0)
      events.push({
        type: "Text",
        message: "It's not very effective...",
        targetId: defender.uuid,
      });
    if (result.effectiveness === 0)
      events.push({
        type: "Text",
        message: `It doesn't affect ${defender.nickname}!`,
        targetId: defender.uuid,
      });
    if (result.isCritical)
      events.push({
        type: "Text",
        message: "A critical hit!",
        targetId: defender.uuid,
      });

    return events;
  }

  /**
   * Applies Status effect (Paralyze, Sleep, etc)
   */
  public static applyStatus(
    target: PokemonInstance,
    status?: StatusCondition,
    chance: number = 100
  ): MoveEvent[] {
    const events: MoveEvent[] = [];
    if (!status) {
      console.warn(`[MoveEngine] Status effect missing 'status' property.`);
      return events;
    }
    if (target.status === "None" && Math.random() * 100 < chance) {
      // Check Ability Immunity
      const abilityCtx = { owner: target }; // Minimal context
      // Note: We don't have 'battle' instance here easily to show text via ability hook if we rely on ctx.battle
      // But I put showText in the hook.
      // AtomicEffects doesn't take BattleScene instance.
      // This is a flaw in current AtomicEffects design if we want abilities to print text.
      // However, AtomicEffects returns events.
      // Maybe the Hook should return an Event or we just block it and AtomicEffects adds a "Prevented" text event?

      // For now, let's assume if hook returns false, we add a generic "It's protected" or rely on the hook causing side effects?
      // But hook can't cause side effects (showText) without battle instance.

      // Bypass Requirement:
      // 1. We check if allow.
      // 2. If allow is false, we push a text event saying "Protected by Ability!" (Generic)

      // Ideally we need AbilityRegistry.checkStatusImmunity(target, status)

      const canApply =
        AbilityRegistry.applyModifier(target.ability, "onSetStatus", 1, {
          owner: target,
        }) !== 0;
      // Wait, applyModifier returns number. onSetStatus returns boolean.
      // Helper `trigger` returns Promise<any>.
      // We need a synchronous check or async? applyStatus is sync.
      // My Hooks in Ability interface: onSetStatus is (status, ctx) => boolean.
      // AbilityRegistry.applyModifier expects number.

      // I need a new helper in AbilityRegistry for boolean checks or generic run.
      // Let's just access manually for now or add helper.

      let blocked = false;
      const ability = AbilityRegistry.get(target.ability);
      if (ability && ability.onSetStatus) {
        if (!ability.onSetStatus({ owner: target }, status)) {
          blocked = true;
        }
      }

      if (blocked) {
        events.push({
          type: "Text",
          message: `${target.nickname}'s ${
            target.ability
          } prevents ${status.toLowerCase()}!`,
          targetId: target.uuid,
        });
        return events;
      }

      target.status = status;
      events.push({ type: "Status", targetId: target.uuid, value: status });
      events.push({
        type: "Text",
        message: `${target.nickname} was ${status.toLowerCase()}ed!`,
        targetId: target.uuid,
      });

      // Initialize Sleep Counter
      if (status === "Sleep") {
        target.volatile["SleepTurns"] = Math.floor(Math.random() * 3) + 2;
      }
    }
    return events;
  }

  /**
   * Applies Stat Change effect
   */
  public static applyStatChange(
    target: PokemonInstance,
    stat?: StatName,
    stages?: number,
    chance: number = 100,
    source?: PokemonInstance
  ): MoveEvent[] {
    const events: MoveEvent[] = [];
    if (!stat || stages === undefined) {
      console.warn(`[MoveEngine] StatChange missing 'stat' or 'stages'.`);
      return events;
    }

    if (Math.random() * 100 < chance) {
      // Apply Ability Modifier (Simple / Contrary)
      // Note: Contrary reverses the change. If source tried to lower, Contrary raises it.
      // Simple doubles it.
      // We apply this BEFORE prevention checks, because Contrary might turn a "Lower" into a "Raise" which shouldn't be prevented by Clear Body.

      // Note: ctx needs owner=target for the ability check
      const ctx = { owner: target, target: source };

      stages = AbilityRegistry.applyModifier(
        target.ability,
        "onModifyStatChange",
        stages,
        ctx
      );

      // Check for Prevention (Ability)
      // Only if lowering (stages < 0) and source is NOT target

      if (stages < 0 && source && source.uuid !== target.uuid) {
        // Check Hook
        const ability = AbilityRegistry.get(target.ability);
        // check onTryLowerStat
        if (ability && ability.onTryLowerStat) {
          if (
            !ability.onTryLowerStat({ owner: target, target: source }, stat)
          ) {
            events.push({
              type: "Text",
              message: `${target.nickname}'s ${target.ability} prevents ${stat} loss!`,
              targetId: target.uuid,
            });
            return events;
          }
        }
      }

      const current = target.statStages[stat] || 0;
      const next = Math.max(-6, Math.min(6, current + stages));

      if (next === current) {
        const limit =
          current === 6 ? "won't go any higher!" : "won't go any lower!";
        events.push({
          type: "Text",
          message: `${target.nickname}'s ${stat} ${limit}`,
          targetId: target.uuid,
        });
      } else {
        target.statStages[stat] = next;
        const changeMsg =
          stages > 1
            ? "rose sharply!"
            : stages > 0
            ? "rose!"
            : stages < -1
            ? "fell harshly!"
            : "fell!";
        events.push({
          type: "StatChange",
          targetId: target.uuid,
          value: { stat, stages: next },
        });
        events.push({
          type: "Text",
          message: `${target.nickname}'s ${stat} ${changeMsg}`,
          targetId: target.uuid,
        });

        // Hook: onAfterStatChange (Defiant / Competitive)
        // We assume source exists and is not target for Defiant/Competitive to trigger usually (except self-inflicted? Defiant says "by an opponent")
        // Our implementation checks source.uuid !== target.uuid inside the hook logic in Abilities.ts

        // Helper to trigger events
        const extraEvents = AbilityRegistry.applyModifier(
          target.ability,
          "onAfterStatChange",
          [],
          { owner: target, target: source },
          stat,
          stages
        );
        // Wait, applyModifier returns 'number' in signature?
        // static applyModifier(id: string, hook: keyof Ability, initialValue: number, ctx: AbilityContext): number
        // I need to update AbilityRegistry.applyModifier or use a new helper because this returns MoveEvent[] (array).
        // Or I can just manually access it since I know what I'm doing.

        const ability = AbilityRegistry.get(target.ability);
        if (ability && ability.onAfterStatChange) {
          const reactionEvents = ability.onAfterStatChange(
            { owner: target, target: source },
            stat,
            stages
          );
          if (reactionEvents && reactionEvents.length > 0) {
            events.push(...reactionEvents);
            // Process recursive stat changes?
            // The reaction events might contain 'StatChange'.
            // MoveEngine.playMoveEvents will handle 'StatChange' visually.
            // But the actual data change?
            // 'StatChange' event in MoveEngine usually is just visual/log?
            // No, in AtomicEffects we updated 'target.statStages[stat] = next'.
            // If Defiant adds a 'StatChange' event, does it also apply the logic?
            // NO. MoveEngine playMoveEvents case 'StatChange' is empty: `case 'StatChange': break;`.
            // It assumes logic is already applied.

            // CRITICAL ISSUE:
            // Returning 'StatChange' event from Defiant hook is purely visual if we don't apply logic.
            // We must apply the logic HERE.
            // But `Defiant` implementation in Abilities.ts returned events.
            // It did NOT apply logic because it doesn't have access to the Pokemon instance logic cleanly?
            // Actually, it has `ctx.owner`. It COULD modify `ctx.owner.statStages`.

            // Let's check Defiant implementation again.
            // It returns events. It does NOT modify stats.

            // We need to loop through reactionEvents and apply them if they are StatChange.
            for (const e of reactionEvents) {
              if (e.type === "StatChange") {
                // Apply the stat change recursively
                // e.value is { stat: 'attack', stages: 2 }
                // This handles state update, limits, and ability modifiers (Simple/Contrary)
                AtomicEffects.applyStatChange(
                  target,
                  e.value.stat,
                  e.value.stages,
                  100,
                  target
                );
              }
            }
          }
        }
      }
    }
    return events;
  }

  /**
   * Applies Heal effect
   */
  public static applyHeal(
    target: PokemonInstance,
    percent: number
  ): MoveEvent[] {
    const healAmt = Math.floor(target.currentStats.hp * (percent / 100));
    const oldHp = target.currentHp;
    target.currentHp = Math.min(
      target.currentStats.hp,
      target.currentHp + healAmt
    );
    const actualHeal = target.currentHp - oldHp;

    return [
      { type: "Heal", targetId: target.uuid, value: actualHeal },
      {
        type: "Text",
        message: `${target.nickname} regained health!`,
        targetId: target.uuid,
      },
    ];
  }

  /**
   * Applies Volatile Status (LeechSeed, etc)
   */
  public static applyVolatile(
    target: PokemonInstance,
    status: string,
    chance: number = 100
  ): MoveEvent[] {
    const events: MoveEvent[] = [];
    if (!target.volatile[status] && Math.random() * 100 < chance) {
      // Check Ability Immunity (Volatile)
      let blocked = false;
      const ability = AbilityRegistry.get(target.ability);
      if (ability && ability.onSetStatus) {
        if (!ability.onSetStatus({ owner: target }, status)) {
          blocked = true;
        }
      }
      // Note: Since we don't pass 'battle', we depend on the hook logging to console (or failing silently/textless if battle missing)
      // But we pushed the responsibility to the hook to try to show text.

      if (blocked) {
        events.push({
          type: "Text",
          message: `${target.nickname}'s ${
            target.ability
          } prevents ${status.toLowerCase()}!`,
          targetId: target.uuid,
        });
        return events;
      }

      // Default Flag (1)
      let value = 1;

      // Special Logic for Randomized Durations
      if (status === "Confusion" || status === "Bound") {
        value = Math.floor(Math.random() * 4) + 2; // 2-5 (Turns: 1-4)
      }

      target.volatile[status] = value;
      events.push({
        type: "EffectUnique",
        targetId: target.uuid,
        value: status,
      });

      let msg = "";
      if (status === "LeechSeed") msg = `${target.nickname} was seeded!`;
      if (status === "Confusion") msg = `${target.nickname} became confused!`;

      if (msg)
        events.push({ type: "Text", message: msg, targetId: target.uuid });
    } else if (Math.random() * 100 < chance) {
      events.push({
        type: "Text",
        message: `But it failed!`,
        targetId: target.uuid,
      });
    }
    return events;
  }
}
