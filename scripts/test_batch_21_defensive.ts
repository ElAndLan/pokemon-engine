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

async function testWeakArmor() {
  console.log("\n--- Testing Weak Armor ---");
  const mon = createMon("Omanyte", "Weak Armor");
  const move: MoveData = { category: "Physical" } as any;
  const ctx = { owner: mon, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) {
    await ability.onAfterDamage(ctx, 10); // 10 damage
  }

  console.log(`Defense Stage: ${mon.statStages.defense} (Expected: -1)`);
  console.log(`Speed Stage: ${mon.statStages.speed} (Expected: 2)`);
}

async function testGooey() {
  console.log("\n--- Testing Gooey ---");
  const mon = createMon("Goodra", "Gooey");
  const attacker = createMon("Attacker", "None");
  const move: MoveData = { category: "Physical" } as any;
  const ctx = { owner: mon, target: attacker, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) {
    await ability.onAfterDamage(ctx, 10);
  }

  console.log(
    `Attacker Speed Stage: ${attacker.statStages.speed} (Expected: -1)`
  );
}

async function testTanglingHair() {
  console.log("\n--- Testing Tangling Hair ---");
  const mon = createMon("Dugtrio", "Tangling Hair");
  const attacker = createMon("Attacker", "None");
  const move: MoveData = { category: "Physical" } as any;
  const ctx = { owner: mon, target: attacker, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) {
    await ability.onAfterDamage(ctx, 10);
  }

  console.log(
    `Attacker Speed Stage: ${attacker.statStages.speed} (Expected: -1)`
  );
}

async function testMummy() {
  console.log("\n--- Testing Mummy ---");
  const mon = createMon("Cofagrigus", "Mummy");
  const attacker = createMon("Attacker", "Overgrow");
  const move: MoveData = { category: "Physical" } as any;
  const ctx = { owner: mon, target: attacker, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onAfterDamage) {
    await ability.onAfterDamage(ctx, 10);
  }

  console.log(`Attacker Ability: ${attacker.ability} (Expected: Mummy)`);
}

async function testCursedBody() {
  console.log("\n--- Testing Cursed Body ---");
  // Needs chance override or loop
  const mon = createMon("Gengar", "Cursed Body");
  const attacker = createMon("Attacker", "None");
  attacker.lastMoveUsed = "shadow_ball";
  const move: MoveData = { category: "Special" } as any; // Cursed body works on any damage
  const ctx = { owner: mon, target: attacker, move, battle: mockBattle };

  const ability = AbilityRegistry.get(mon.ability)!;

  // Loop until trigger (30% chance)
  let triggered = false;
  for (let i = 0; i < 20; i++) {
    if (attacker.volatile["Disable"]) {
      triggered = true;
      break;
    }
    if (ability.onAfterDamage) {
      await ability.onAfterDamage(ctx, 10);
    }
  }

  console.log(`Triggered: ${triggered}`);
  if (triggered) {
    console.log(
      `Disabled Move: ${attacker.disabledMoveId} (Expected: shadow_ball)`
    );
    console.log(`Disable Turns: ${attacker.volatile["Disable"]} (Expected: 4)`);
  }
}

async function run() {
  await testWeakArmor();
  await testGooey();
  await testTanglingHair();
  await testMummy();
  await testCursedBody();
}

run();
