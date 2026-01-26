import { PokemonInstance, MoveData } from "../data/DataTypes";
import { getEffectiveStat } from "./StatCalculator";
import { getTypeEffectiveness } from "./TypeChart";
import { AbilityRegistry } from "./Abilities";

export interface DamageResult {
  damage: number;
  isCritical: boolean;
  effectiveness: number; // 1 = normal, >1 = super, <1 = not very
  details: string[]; // For debugging logs
}

export const SETTINGS = {
  USE_RANDOM: true, // Toggle for testing
  CRIT_MULTIPLIER: 1.5,
};

import { WeatherType } from "../data/DataTypes";

export function calculateDamage(
  attacker: PokemonInstance,
  defender: PokemonInstance,
  move: MoveData,
  weather: WeatherType = "None"
): DamageResult {
  const details: string[] = [];

  // Status Moves deal 0 direct damage (unless handled differently)
  if (move.category === "Status") {
    return { damage: 0, isCritical: false, effectiveness: 1, details };
  }

  // --- Fixed Damage Moves ---
  if (move.id === "night_shade" || move.id === "seismic_toss") {
    details.push("Fixed Damage: Level");
    return {
      damage: attacker.level,
      isCritical: false,
      effectiveness: 1,
      details,
    };
  }
  if (move.id === "dragon_rage") {
    details.push("Fixed Damage: 40");
    return { damage: 40, isCritical: false, effectiveness: 1, details };
  }
  if (move.id === "sonic_boom") {
    details.push("Fixed Damage: 20");
    return { damage: 20, isCritical: false, effectiveness: 1, details };
  }

  // 1. Get Effective Stats (Spec 3.2: A & D)
  let attackStat: "attack" | "spAttack" = "attack";
  let defenseStat: "defense" | "spDefense" = "defense";

  if (move.category === "Special") {
    attackStat = "spAttack";
    defenseStat = "spDefense";
  }

  const A = getEffectiveStat(attacker, attackStat);
  const D = getEffectiveStat(defender, defenseStat);
  let power = move.power || 0;

  // 4. Ability Modifiers (e.g. Technician, Galvanize)
  // Galvanize/Pixilate/etc boost power by 1.2x (Gen 7)
  // Note: This needs to happen after type modification is acknowledged,
  // but the hook onModifyBasePower checks original move type usually?
  // "Normal moves become Electric and get a power boost."
  // Yes, if original type was Normal.

  // We pass the context with the ORIGINAL move, but potentially we should pass effective type?
  // The implementation of Galvanize checks `ctx.move.type === 'Normal'`.
  // If we pass `move` (which is original move), it works.

  const ctx = { owner: attacker, move: move, target: defender };
  power = AbilityRegistry.applyModifier(
    attacker.ability,
    "onModifyBasePower",
    power,
    ctx
  );

  // 5. Calculate Attack/Defense stats

  const Level = attacker.level;

  // --- ABILITY HOOKS (Stats) ---
  // Context is created here. Note: BattleScene is missing, so weather checks won't work yet.
  // We treat 'attacker' as owner for Attack hooks, and 'defender' as owner for Defense hooks.

  // 1. Modify Attack (e.g. Huge Power, Overgrow)
  let modifiedA = A;
  // We need to pass the move context to the ability
  const atkCtx = { owner: attacker, target: defender, move, battle: undefined }; // We lack Battle context here :(
  // TODO: DamageCalculator needs Battle Context or Weather passed in.
  // We can assume Weather is passed via SETTINGS or we need to update signature?
  // User requirement: "All weather boosts..."
  // We can't access BattleScene.weather here efficiently without refactor.
  // QUICK FIX: Pass 'weather' as optional arg or access global Game if possible?
  // Accessing global Game is messy.
  // Better: Update calculateDamage signature to accept 'weather'.
  // Or we use a singleton accessible Weather state?
  // AbilityRegistry uses callbacks.
  // Let's modify calculateDamage to take 'weather' argument.

  // For now, let's use a "Mock" or assume we update signature.
  // I will update the signature in next step. For now I write logic with 'weather' variable assuming it exists.

  // Wait, I can't write invalid code. I should update signature FIRST.
  // Let me update signature now.

  modifiedA = AbilityRegistry.applyModifier(
    attacker.ability,
    "onModifyAttack",
    A,
    atkCtx
  );

  // 2. Modify Defense (e.g. Fur Coat, Marvel Scale)
  let modifiedD = D;
  const defCtx = { owner: defender, target: attacker, move };
  modifiedD = AbilityRegistry.applyModifier(
    defender.ability,
    "onModifyDefense",
    D,
    defCtx
  );

  details.push(
    `Level: ${Level}, Power: ${power}, A: ${modifiedA} (was ${A}), D: ${modifiedD} (was ${D})`
  );

  if (power === 0) {
    return { damage: 0, isCritical: false, effectiveness: 1, details };
  }

  // 2. Base Damage Calculation
  // floor( floor( floor( ((2 * Level / 5) + 2) * Power * A / D) / 50 ) + 2 )
  const baseDamage = Math.floor(
    Math.floor(
      Math.floor((((2 * Level) / 5 + 2) * power * modifiedA) / modifiedD) / 50
    ) + 2
  );
  details.push(`Base Damage: ${baseDamage}`);

  // 3. Modifiers
  // Modifier = STAB * Type * Crit * Random(0.85, 1.0) * Burn * Weather

  // 3. Modifiers
  // Modifier = STAB * Type * Crit * Random(0.85, 1.0) * Burn * Weather

  // Weather Type Mod
  let weatherMult = 1.0;
  if (weather === "Sun") {
    if (move.type === "Fire") weatherMult = 1.5;
    if (move.type === "Water") weatherMult = 0.5;
  }
  if (weather === "Rain") {
    if (move.type === "Water") weatherMult = 1.5;
    if (move.type === "Fire") weatherMult = 0.5;
  }
  if (weatherMult !== 1.0) details.push(`Weather Mod: ${weatherMult}x`);

  // STAB
  let stab = 1.0;
  if (attacker.types.some((t) => t.toLowerCase() === move.type.toLowerCase())) {
    stab = 1.5;
    // Ability: Adaptability (STAB = 2.0)
    if (attacker.ability === "Adaptability") stab = 2.0;

    details.push(`STAB applied (${stab}x)`);
  }

  // Type Effectiveness
  const typeEff = getTypeEffectiveness(move.type, defender.types);
  if (typeEff !== 1) details.push(`Type Effectiveness: ${typeEff}x`);

  // Critical (Spec 3.2 rule: 1.0 unless extended. We use 1/16 chance usually)
  let crit = 1.0;
  let isCritical = false;

  // Check if defender prevents crits (Battle Armor, Shell Armor)
  const defenderPreventsCrit =
    AbilityRegistry.get(defender.ability)?.onPreventCrit?.(defCtx) || false;

  // Check if attacker forces crit (Merciless on poisoned targets)
  const attackerForcesCrit =
    AbilityRegistry.get(attacker.ability)?.onForceCrit?.(atkCtx) || false;

  if (!defenderPreventsCrit) {
    // Simple 6.25% chance for now, or forced crit
    if (attackerForcesCrit || (SETTINGS.USE_RANDOM && Math.random() < 0.0625)) {
      let critMult = SETTINGS.CRIT_MULTIPLIER;
      critMult = AbilityRegistry.applyModifier(
        attacker.ability,
        "onCriticalMultiplier",
        critMult,
        atkCtx
      );
      crit = critMult;
      isCritical = true;
      details.push(`Critical Hit! (${crit}x)`);
    }
  } else {
    details.push(`Critical hit prevented by ${defender.ability}`);
  }

  // Random Variance (0.85 to 1.00)
  let random = 1.0;
  if (SETTINGS.USE_RANDOM) {
    random = (Math.floor(Math.random() * 16) + 85) / 100; // 0.85 to 1.00
    details.push(`Random Factor: ${random}`);
  }

  // Ability: Filter / Solid Rock / Levitate (Immunity handled in MoveEngine loop usually but Levitate makes TypeEff 0)
  // We'll stick to Multipliers here.
  let abilityMult = 1.0;

  // Update Contexts with effectiveness
  const atkCtxWithEff = { ...atkCtx, effectiveness: typeEff };
  const defCtxWithEff = { ...defCtx, effectiveness: typeEff };

  abilityMult = AbilityRegistry.applyModifier(
    attacker.ability,
    "onDamageMultiplier",
    abilityMult,
    atkCtxWithEff
  ); // Life Orb etc, Tinted Lens
  abilityMult = AbilityRegistry.applyModifier(
    defender.ability,
    "onDamageMultiplier",
    abilityMult,
    defCtxWithEff
  ); // Filter etc.

  if (abilityMult !== 1.0) details.push(`Ability Multiplier: ${abilityMult}x`);

  // Final Calculation
  const damage = Math.floor(
    baseDamage * stab * typeEff * crit * random * abilityMult * weatherMult
  );
  details.push(`Final Damage: ${damage}`);

  return { damage, isCritical, effectiveness: typeEff, details };
}
