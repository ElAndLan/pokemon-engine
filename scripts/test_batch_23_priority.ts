import {
  PokemonInstance,
  MoveData,
} from "../src/renderer/src/core/data/DataTypes";
import { AbilityRegistry } from "../src/renderer/src/core/battle/Abilities";
import { MoveEngine } from "../src/renderer/src/core/battle/MoveEngine";

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

async function testGaleWings() {
  console.log("\n--- Testing Gale Wings ---");
  const mon = createMon("Talonflame", "Gale Wings");
  const move: MoveData = { type: "Flying", priority: 0 } as any;
  const ctx = { owner: mon, move, battle: mockBattle };

  // Full HP
  let p = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyPriority",
    0,
    ctx
  );
  console.log(`Priority at Full HP: ${p} (Expected: 1)`);

  // Damaged
  mon.currentHp = 50;
  p = AbilityRegistry.applyModifier(mon.ability, "onModifyPriority", 0, ctx);
  console.log(`Priority at Low HP: ${p} (Expected: 0)`);
}

async function testTriage() {
  console.log("\n--- Testing Triage ---");
  const mon = createMon("Comfey", "Triage");
  const healMove: MoveData = { flags: { heal: true }, priority: 0 } as any;
  const dmgMove: MoveData = { flags: {}, priority: 0 } as any;

  let p = AbilityRegistry.applyModifier(mon.ability, "onModifyPriority", 0, {
    owner: mon,
    move: healMove,
  });
  console.log(`Priority (Heal): ${p} (Expected: 3)`);

  p = AbilityRegistry.applyModifier(mon.ability, "onModifyPriority", 0, {
    owner: mon,
    move: dmgMove,
  });
  console.log(`Priority (Damage): ${p} (Expected: 0)`);
}

async function testMyceliumMight() {
  console.log("\n--- Testing Mycelium Might ---");
  const mon = createMon("Toedscruel", "Mycelium Might");
  const statusMove: MoveData = { category: "Status", priority: 0 } as any;

  const p = AbilityRegistry.applyModifier(mon.ability, "onModifyPriority", 0, {
    owner: mon,
    move: statusMove,
  });
  console.log(`Priority (Status): ${p} (Expected: -6)`);
}

async function testQueenlyMajesty() {
  console.log("\n--- Testing Queenly Majesty ---");
  const defender = createMon("Tsareena", "Queenly Majesty");
  const attacker = createMon("Attacker", "None");

  // Priority Move (Quick Attack)
  const priorityMove: MoveData = {
    name: "Quick Attack",
    priority: 1,
    target: "Normal",
    description: "Quick",
  } as any;

  const result = MoveEngine.executeMove(attacker, defender, priorityMove);
  const blocked = result.events.some((e) =>
    e.message?.includes("blocks priority moves")
  );
  console.log(`Blocked Priority Move? ${blocked} (Expected: true)`);

  // Normal Move
  const normalMove: MoveData = {
    name: "Tackle",
    priority: 0,
    target: "Normal",
    description: "Tackle",
  } as any;
  const result2 = MoveEngine.executeMove(attacker, defender, normalMove);
  const blocked2 = result2.events.some((e) =>
    e.message?.includes("blocks priority moves")
  );
  console.log(`Blocked Normal Move? ${blocked2} (Expected: false)`);
}

async function testDazzlingPrankster() {
  console.log("\n--- Testing Dazzling vs Prankster ---");
  const defender = createMon("Bruxish", "Dazzling");
  const attacker = createMon("Grimmsnarl", "Prankster");

  // Status Move (Prankster boosts priority to 1)
  const statusMove: MoveData = {
    name: "Taunt",
    category: "Status",
    priority: 0,
    target: "Normal",
    description: "Taunt",
  } as any;

  // NOTE: MoveEngine itself doesn't calculate priority.
  // BUT the blocking logic inside Dazzling DOES call getEffectivePriority which uses Prankster.

  const result = MoveEngine.executeMove(attacker, defender, statusMove);
  const blocked = result.events.some((e) =>
    e.message?.includes("blocks priority moves")
  );
  console.log(`Blocked Prankster Status Move? ${blocked} (Expected: true)`);
}

async function run() {
  await testGaleWings();
  await testTriage();
  await testMyceliumMight();
  await testQueenlyMajesty();
  await testDazzlingPrankster();
}

run();
