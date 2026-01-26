import {
  PokemonInstance,
  MoveData,
  PokemonType,
  WeatherType,
} from "../data/DataTypes";
import { getEffectiveStat } from "./StatCalculator";
import { getTypeEffectiveness, TypeChart } from "./TypeChart";
import { AbilityRegistry } from "./Abilities";

export class CoreMoveLogic {
  /**
   * Checks if a move hits based on accuracy and evasion stages
   */
  public static checkHit(
    attacker: PokemonInstance,
    defender: PokemonInstance,
    move: MoveData,
    weather: WeatherType = "None"
  ): boolean {
    if (!move) return false;

    // 1. No Guard (Check IDs or Registry if we had a flag? IDs for now plus hook potential?)
    // Currently we don't have a specific "bypass accuracy" hook, but we can verify against known abilities OR check registry?
    // Let's rely on standard ID check for No Guard as it's cleaner than a generic "onCheckHit" hook right now.
    // Or better: Use applyModifier('onModifyAccuracy') but that modifies number.
    // No Guard is absolute.
    if (attacker.ability === "No Guard" || defender.ability === "No Guard") {
      return true;
    }

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
      "-6": 0.33,
      "-5": 0.37,
      "-4": 0.43,
      "-3": 0.5,
      "-2": 0.6,
      "-1": 0.75,
      "0": 1.0,
      "1": 1.33,
      "2": 1.66,
      "3": 2.0,
      "4": 2.33,
      "5": 2.66,
      "6": 3.0,
    };

    let finalAccuracy = accuracy * (stageMultipliers[combinedStage] || 1.0);

    // 2. Modify Accuracy (Compound Eyes)
    // Hook: onModifyAccuracy(val, ctx) -> val
    finalAccuracy = AbilityRegistry.applyModifier(
      attacker.ability,
      "onModifyAccuracy",
      finalAccuracy,
      { owner: attacker, move, target: defender }
    );

    // 3. Modify Evasion (Sand Veil, Snow Cloak)
    // Evasion boost reduces hit chance. 1.25x Evasion => 0.8x Accuracy
    let evasionMod = 1.0;
    // Pass weather in variables for abilities that need it
    evasionMod = AbilityRegistry.applyModifier(
      defender.ability,
      "onModifyEvasion",
      evasionMod,
      { owner: defender, move, target: attacker, variables: { weather } }
    );

    if (evasionMod !== 1.0) {
      finalAccuracy /= evasionMod;
    }

    return Math.random() * 100 < finalAccuracy;
  }

  /**
   * Determines if a move is a critical hit
   */
  public static checkCritical(
    attacker: PokemonInstance,
    defender: PokemonInstance,
    move: MoveData
  ): boolean {
    // Base Stage
    let stage = 0;
    if (move.critRate) stage += move.critRate;
    if (move.description?.toLowerCase().includes("high critical")) stage += 1; // Basic handling if critRate missing

    // Ability Hook: Super Luck
    // We need ctx.
    // Note: CoreMoveLogic static methods usually just take simple args.
    // But we imported AbilityRegistry.
    stage = AbilityRegistry.applyModifier(
      attacker.ability,
      "onModifyCritStage",
      stage,
      { owner: attacker, move, target: defender }
    );

    // Crit Rates (Gen 6+):
    // 0: 4.17% (1/24) -> We used 6.25% (1/16) as Gen 2-5 standard.
    // Let's stick to Gen 7:
    // 0: 4% (1/24 in Gen 7?) -> Actually 1/24 = 4.16%
    // 1: 12.5% (1/8)
    // 2: 50% (1/2)
    // 3+: 100%

    let threshold = 6.25; // Default Gen 2-5
    if (stage >= 3) threshold = 100;
    else if (stage === 2) threshold = 50;
    else if (stage === 1) threshold = 12.5;

    return Math.random() * 100 < threshold;
  }

  /**
   * Gets total type effectiveness multiplier
   */
  public static getTypeMultiplier(
    moveType: string,
    defenderTypes: string[]
  ): number {
    let multiplier = 1.0;
    const atkType = moveType as PokemonType; // Already Capitalized in MoveData usually, assuming input is valid

    for (const defTypeStr of defenderTypes) {
      const defType = defTypeStr as PokemonType; // Already Capitalized in PokemonInstance
      if (TypeChart[atkType] && TypeChart[atkType][defType] !== undefined) {
        multiplier *= TypeChart[atkType][defType]!;
      }
    }
    return multiplier;
  }
}
