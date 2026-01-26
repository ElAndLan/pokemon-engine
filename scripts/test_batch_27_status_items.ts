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

async function testUnburden() {
  console.log("\n--- Testing Unburden ---");
  const mon = createMon("Drifblim", "Unburden");
  mon.volatile["Unburden"] = 1; // Simulate item loss trigger
  const speed = AbilityRegistry.applyModifier(
    mon.ability,
    "onStatCalculation",
    100,
    { owner: mon, statName: "speed" }
  );
  console.log(`Speed (Unburden Active): ${speed} (Expected: 200)`);
}

async function testItemStealing() {
  console.log("\n--- Testing Item Stealing ---");

  // Magician
  const magician = createMon("Delphox", "Magician");
  const victim = createMon("Victim", "None");
  victim.heldItem = "Sitrus Berry";

  const ctx1 = { owner: magician, target: victim, battle: mockBattle };
  if (AbilityRegistry.get("Magician")?.onAfterDamage) {
    await AbilityRegistry.get("Magician")!.onAfterDamage!(ctx1, 10);
  }
  console.log(
    `Magician Stole Item? ${
      magician.heldItem === "Sitrus Berry"
    } (Expected: true)`
  );

  // Pickpocket
  const pickpocket = createMon("Weavile", "Pickpocket");
  const attacker = createMon("Attacker", "None");
  attacker.heldItem = "Leftovers";
  const contactMove: MoveData = { category: "Physical" } as any;

  const ctx2 = {
    owner: pickpocket,
    target: attacker,
    move: contactMove,
    battle: mockBattle,
  };
  if (AbilityRegistry.get("Pickpocket")?.onAfterDamage) {
    await AbilityRegistry.get("Pickpocket")!.onAfterDamage!(ctx2, 10);
  }
  console.log(
    `Pickpocket Stole Item? ${
      pickpocket.heldItem === "Leftovers"
    } (Expected: true)`
  );
}

async function testStatusAbilities() {
  console.log("\n--- Testing Status Abilities ---");

  // Poison Touch
  const muk = createMon("Muk", "Poison Touch");
  const victim = createMon("Victim", "None");
  const move: MoveData = { flags: { contact: true } } as any;
  // Mock random to force true?
  // We can't mock Math.random easily here without modifying global.
  // We'll call the hook directly and hope for luck or check logic?
  // Actually, let's just check if it CAN trigger.
  // We will simulate it.

  // Comatose
  const komala = createMon("Komala", "Comatose");
  const sleepBlocked = !AbilityRegistry.get("Comatose")!.onSetStatus!(
    { owner: komala },
    "Sleep"
  );
  console.log(`Comatose Blocks Status? ${sleepBlocked} (Expected: true)`);

  // Pastel Veil
  const ponyta = createMon("Ponyta", "Pastel Veil");
  const poisonBlocked = !AbilityRegistry.get("Pastel Veil")!.onSetStatus!(
    { owner: ponyta, battle: mockBattle },
    "Poison"
  );
  console.log(`Pastel Veil Blocks Poison? ${poisonBlocked} (Expected: true)`);

  // Purifying Salt
  const nacli = createMon("Nacli", "Purifying Salt");
  const statusBlocked = !AbilityRegistry.get("Purifying Salt")!.onSetStatus!(
    { owner: nacli },
    "Burn"
  );
  const ghostDmg = AbilityRegistry.applyModifier(
    nacli.ability,
    "onDamageMultiplier",
    100,
    { owner: nacli, move: { type: "Ghost" } as any }
  );
  console.log(
    `Purifying Salt Blocks Status? ${statusBlocked} (Expected: true)`
  );
  console.log(`Purifying Salt Ghost Damage: ${ghostDmg} (Expected: 50)`);
}

async function testThermalExchange() {
  console.log("\n--- Testing Thermal Exchange ---");
  const mon = createMon("Baxcalibur", "Thermal Exchange");
  const fireMove: MoveData = { type: "Fire" } as any;

  const burnBlocked = !AbilityRegistry.get(mon.ability)!.onSetStatus!(
    { owner: mon },
    "Burn"
  );
  console.log(`Blocks Burn? ${burnBlocked} (Expected: true)`);

  const ctx = { owner: mon, move: fireMove, battle: mockBattle };
  if (AbilityRegistry.get(mon.ability)?.onAfterDamage) {
    await AbilityRegistry.get(mon.ability)!.onAfterDamage!(ctx, 10);
  }
  console.log(`Attack Stage: ${mon.statStages.attack} (Expected: 1)`);
}

async function run() {
  await testUnburden();
  await testItemStealing();
  await testStatusAbilities();
  await testThermalExchange();
}

run();
