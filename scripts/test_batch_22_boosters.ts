import {
  PokemonInstance,
  MoveData,
} from "../src/renderer/src/core/data/DataTypes";
import { AbilityRegistry } from "../src/renderer/src/core/battle/Abilities";
import { AtomicEffects } from "../src/renderer/src/core/battle/AtomicEffects";

// Mock Pokemon
function createMon(name: string, ability: string): PokemonInstance {
  return {
    uuid: Math.random().toString(),
    speciesId: name.toLowerCase(),
    nickname: name,
    level: 50,
    ability: ability,
    currentHp: 100,
    currentStats: {
      hp: 100,
      attack: 100,
      defense: 100,
      spAttack: 100,
      spDefense: 100,
      speed: 100,
    },
    statStages: {
      hp: 0,
      attack: 0,
      defense: 0,
      spAttack: 0,
      spDefense: 0,
      speed: 0,
      accuracy: 0,
      evasion: 0,
    },
    moves: [],
    types: ["Normal"],
    status: "None",
    volatile: {},
  } as any;
}

const mockBattle: any = {
  showText: async (msg: string) => console.log(`[TEXT] ${msg}`),
};

async function testMoxie() {
  console.log("\n--- Testing Moxie ---");
  const mon = createMon("Krookodile", "Moxie");
  const victim = createMon("Victim", "None");
  const ctx = { owner: mon, target: victim, battle: mockBattle };

  // Simulate Faint
  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onFaint) {
    const events = ability.onFaint(ctx, victim);
    // Moxie returns events but doesn't auto-apply them in hook?
    // Wait, the implementation returns events.
    // We need to apply them manually in test or verify logic.
    // In real BattleScene, these events are processed.
    // But for StatChange, we usually use AtomicEffects.applyStatChange inside the hook if we want immediate effect?
    // Let's check my implementation.
    // I returned events: { type: 'StatChange' ... }
    // I did NOT call AtomicEffects inside Moxie hook.
    // This means Moxie relies on BattleScene to process the returned events.
    // OK for verification, we just check if events are returned.

    console.log(`Events Generated: ${events.length}`);
    if (events.length > 0) {
      console.log(`Event Type: ${events[1].type}`); // Index 1 is usually StatChange
      console.log(`Stat: ${events[1].value.stat}`);
      console.log(`Stages: ${events[1].value.stages}`);
    }
  }
}

async function testJustified() {
  console.log("\n--- Testing Justified ---");
  const mon = createMon("Lucario", "Justified");
  const move: MoveData = { type: "Dark" } as any;
  const ctx = { owner: mon, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) {
    await ability.onAfterDamage(ctx, 10);
  }
  console.log(`Attack Stage: ${mon.statStages.attack} (Expected: 1)`);
}

async function testRattled() {
  console.log("\n--- Testing Rattled ---");
  const mon = createMon("Magikarp", "Rattled");

  // Test Dark
  const moveDark: MoveData = { type: "Dark" } as any;
  const ctxDark = { owner: mon, move: moveDark, battle: mockBattle };
  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) await ability.onAfterDamage(ctxDark, 10);
  console.log(`Speed Stage (Dark): ${mon.statStages.speed} (Expected: 1)`);

  // Test Ghost
  const moveGhost: MoveData = { type: "Ghost" } as any;
  const ctxGhost = { owner: mon, move: moveGhost, battle: mockBattle };
  if (ability.onAfterDamage) await ability.onAfterDamage(ctxGhost, 10);
  console.log(`Speed Stage (Ghost): ${mon.statStages.speed} (Expected: 2)`);

  // Test Bug
  const moveBug: MoveData = { type: "Bug" } as any;
  const ctxBug = { owner: mon, move: moveBug, battle: mockBattle };
  if (ability.onAfterDamage) await ability.onAfterDamage(ctxBug, 10);
  console.log(`Speed Stage (Bug): ${mon.statStages.speed} (Expected: 3)`);
}

async function testStamina() {
  console.log("\n--- Testing Stamina ---");
  const mon = createMon("Mudsdale", "Stamina");
  const move: MoveData = { type: "Normal" } as any;
  const ctx = { owner: mon, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) await ability.onAfterDamage(ctx, 10);
  console.log(`Defense Stage: ${mon.statStages.defense} (Expected: 1)`);
}

async function testWaterCompaction() {
  console.log("\n--- Testing Water Compaction ---");
  const mon = createMon("Palossand", "Water Compaction");
  const move: MoveData = { type: "Water" } as any;
  const ctx = { owner: mon, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) await ability.onAfterDamage(ctx, 10);
  console.log(`Defense Stage: ${mon.statStages.defense} (Expected: 2)`);
}

async function testBerserk() {
  console.log("\n--- Testing Berserk ---");
  const mon = createMon("Drampa", "Berserk");
  // HP 100/100
  const move: MoveData = { type: "Normal" } as any;
  const ctx = { owner: mon, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;

  // Damage to 60 (No trigger)
  console.log("Hit 1: 100 -> 60");
  mon.currentHp = 60; // Simulate damage application before hook?
  // Wait, onAfterDamage(ctx, damageTaken).
  // My implementation checks: previous = current + damageTaken.
  // So if I set currentHp = 60 and pass damageTaken = 40.
  // Previous = 100. Max = 100.
  // Previous > 50, Current > 50. No trigger.
  if (ability.onAfterDamage) await ability.onAfterDamage(ctx, 40);
  console.log(`Sp. Atk Stage: ${mon.statStages.spAttack} (Expected: 0)`);

  // Damage to 40 (Trigger)
  console.log("Hit 2: 60 -> 40");
  mon.currentHp = 40;
  // Previous = 60. Max = 100.
  // Previous > 50, Current <= 50. Trigger!
  if (ability.onAfterDamage) await ability.onAfterDamage(ctx, 20);
  console.log(`Sp. Atk Stage: ${mon.statStages.spAttack} (Expected: 1)`);
}

async function run() {
  await testMoxie();
  await testJustified();
  await testRattled();
  await testStamina();
  await testWaterCompaction();
  await testBerserk();
}

run();
