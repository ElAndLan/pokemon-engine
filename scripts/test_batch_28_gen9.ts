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
    heldItem: undefined,
  } as any;
}

const mockBattle: any = {
  showText: async (msg: string) => console.log(`[TEXT] ${msg}`),
};

async function testHazards() {
  console.log("\n--- Testing Hazard Abilities ---");

  // Toxic Debris
  const glimmet = createMon("Glimmet", "Toxic Debris");
  const physMove: MoveData = { category: "Physical" } as any;
  const specMove: MoveData = { category: "Special" } as any;

  // Physical Trigger
  const ctx1 = { owner: glimmet, move: physMove, battle: mockBattle };
  if (AbilityRegistry.get("Toxic Debris")?.onAfterDamage) {
    await AbilityRegistry.get("Toxic Debris")!.onAfterDamage!(ctx1, 10);
  }
  // Expected: Log output "Toxic Debris scattered poison spikes!" (Manual verification of log)

  // Special Trigger (Should fail)
  const ctx2 = { owner: glimmet, move: specMove, battle: mockBattle };
  if (AbilityRegistry.get("Toxic Debris")?.onAfterDamage) {
    await AbilityRegistry.get("Toxic Debris")!.onAfterDamage!(ctx2, 10);
  }

  // Seed Sower
  const arboliva = createMon("Arboliva", "Seed Sower");
  const ctx3 = { owner: arboliva, move: physMove, battle: mockBattle };
  if (AbilityRegistry.get("Seed Sower")?.onAfterDamage) {
    await AbilityRegistry.get("Seed Sower")!.onAfterDamage!(ctx3, 10);
  }
}

async function testElectric() {
  console.log("\n--- Testing Electric Abilities ---");

  // Electromorphosis
  const bellibolt = createMon("Bellibolt", "Electromorphosis");
  const ctx1 = { owner: bellibolt, battle: mockBattle };
  if (AbilityRegistry.get("Electromorphosis")?.onAfterDamage) {
    await AbilityRegistry.get("Electromorphosis")!.onAfterDamage!(ctx1, 10);
  }
  console.log(`Charged Up? ${bellibolt.volatile["Charge"]} (Expected: 1)`);

  const elecMove: MoveData = { type: "Electric" } as any;
  const power = AbilityRegistry.applyModifier(
    bellibolt.ability,
    "onModifyBasePower",
    100,
    { owner: bellibolt, move: elecMove }
  );
  console.log(`Power (Charged): ${power} (Expected: 200)`);

  // Wind Power
  const wattrel = createMon("Wattrel", "Wind Power");
  const windMove: MoveData = { flags: { wind: true } } as any;
  const ctx2 = { owner: wattrel, move: windMove, battle: mockBattle };
  if (AbilityRegistry.get("Wind Power")?.onAfterDamage) {
    await AbilityRegistry.get("Wind Power")!.onAfterDamage!(ctx2, 10);
  }
  console.log(
    `Wind Power Charged? ${wattrel.volatile["Charge"]} (Expected: 1)`
  );
}

async function testImmunity() {
  console.log("\n--- Testing Immunity Abilities ---");

  // Earth Eater
  const orthworm = createMon("Orthworm", "Earth Eater");
  const groundMove: MoveData = { type: "Ground" } as any;
  const events: any[] = [];
  const hit = AbilityRegistry.get("Earth Eater")!.onTryHit!(
    { owner: orthworm, move: groundMove, battle: mockBattle },
    events
  );
  console.log(`Earth Eater Hit? ${hit} (Expected: false)`);
  console.log(
    `Heal Event? ${events.some((e) => e.type === "Heal")} (Expected: true)`
  );

  // Well-Baked Body
  const dachsbun = createMon("Dachsbun", "Well-Baked Body");
  const fireMove: MoveData = { type: "Fire" } as any;
  const events2: any[] = [];
  const hit2 = AbilityRegistry.get("Well-Baked Body")!.onTryHit!(
    { owner: dachsbun, move: fireMove, battle: mockBattle },
    events2
  );
  console.log(`Well-Baked Hit? ${hit2} (Expected: false)`);
  console.log(
    `Protection Text? ${events2.some((e) =>
      e.message?.includes("protects")
    )} (Expected: true)`
  );

  // Good as Gold
  const gholdengo = createMon("Gholdengo", "Good as Gold");
  const statusMove: MoveData = { category: "Status", target: "Normal" } as any;
  const events3: any[] = [];
  const hit3 = AbilityRegistry.get("Good as Gold")!.onTryHit!(
    { owner: gholdengo, move: statusMove, battle: mockBattle },
    events3
  );
  console.log(`Good as Gold Hit? ${hit3} (Expected: false)`);

  const selfMove: MoveData = { category: "Status", target: "Self" } as any;
  const hit4 = AbilityRegistry.get("Good as Gold")!.onTryHit!(
    { owner: gholdengo, move: selfMove, battle: mockBattle },
    events3
  );
  console.log(`Self Status Hit? ${hit4} (Expected: true)`);
}

async function run() {
  await testHazards();
  await testElectric();
  await testImmunity();
}

run();
