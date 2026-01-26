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

async function testPowerBoosters() {
  console.log("\n--- Testing Power Boosters ---");

  // Steelworker
  const steelMon = createMon("Dhelmise", "Steelworker");
  const steelMove: MoveData = { type: "Steel" } as any;
  const bp1 = AbilityRegistry.applyModifier(
    steelMon.ability,
    "onModifyBasePower",
    100,
    { owner: steelMon, move: steelMove }
  );
  console.log(`Steelworker: ${bp1} (Expected: 150)`);

  // Transistor
  const elecMon = createMon("Regieleki", "Transistor");
  const elecMove: MoveData = { type: "Electric" } as any;
  const bp2 = AbilityRegistry.applyModifier(
    elecMon.ability,
    "onModifyBasePower",
    100,
    { owner: elecMon, move: elecMove }
  );
  console.log(`Transistor: ${bp2} (Expected: 150)`);

  // Dragon's Maw
  const dragMon = createMon("Regidrago", "Dragon's Maw");
  const dragMove: MoveData = { type: "Dragon" } as any;
  const bp3 = AbilityRegistry.applyModifier(
    dragMon.ability,
    "onModifyBasePower",
    100,
    { owner: dragMon, move: dragMove }
  );
  console.log(`Dragon's Maw: ${bp3} (Expected: 150)`);

  // Rocky Payload
  const rockMon = createMon("Bombirdier", "Rocky Payload");
  const rockMove: MoveData = { type: "Rock" } as any;
  const bp4 = AbilityRegistry.applyModifier(
    rockMon.ability,
    "onModifyBasePower",
    100,
    { owner: rockMon, move: rockMove }
  );
  console.log(`Rocky Payload: ${bp4} (Expected: 150)`);

  // Punk Rock (Offensive)
  const punkMon = createMon("Toxtricity", "Punk Rock");
  const soundMove: MoveData = { flags: { sound: true } } as any;
  const bp5 = AbilityRegistry.applyModifier(
    punkMon.ability,
    "onModifyBasePower",
    100,
    { owner: punkMon, move: soundMove }
  );
  console.log(`Punk Rock (Offensive): ${bp5} (Expected: 130)`);

  // Sharpness
  const sharpMon = createMon("Gallade", "Sharpness");
  const sliceMove: MoveData = {
    name: "Psycho Cut",
    flags: { slicing: true },
  } as any;
  const bp6 = AbilityRegistry.applyModifier(
    sharpMon.ability,
    "onModifyBasePower",
    100,
    { owner: sharpMon, move: sliceMove }
  );
  console.log(`Sharpness: ${bp6} (Expected: 150)`);

  // Tough Claws
  const clawMon = createMon("Charizard", "Tough Claws");
  const contactMove: MoveData = {
    category: "Physical",
    flags: { contact: true },
  } as any;
  const bp7 = AbilityRegistry.applyModifier(
    clawMon.ability,
    "onModifyBasePower",
    100,
    { owner: clawMon, move: contactMove }
  );
  console.log(`Tough Claws: ${bp7} (Expected: 130)`);

  // Water Bubble (Offensive)
  const bubbleMon = createMon("Araquanid", "Water Bubble");
  const waterMove: MoveData = { type: "Water" } as any;
  const bp8 = AbilityRegistry.applyModifier(
    bubbleMon.ability,
    "onModifyBasePower",
    100,
    { owner: bubbleMon, move: waterMove }
  );
  console.log(`Water Bubble (Offensive): ${bp8} (Expected: 200)`);
}

async function testDefensiveAbilities() {
  console.log("\n--- Testing Defensive Abilities ---");

  // Punk Rock (Defensive)
  const punkMon = createMon("Toxtricity", "Punk Rock");
  const soundMove: MoveData = { flags: { sound: true } } as any;
  const dmg1 = AbilityRegistry.applyModifier(
    punkMon.ability,
    "onDamageMultiplier",
    100,
    { owner: punkMon, move: soundMove }
  );
  console.log(`Punk Rock (Defensive): ${dmg1} (Expected: 50)`);

  // Fluffy
  const fluffyMon = createMon("Bewear", "Fluffy");
  const contactMove: MoveData = {
    category: "Physical",
    flags: { contact: true },
  } as any;
  const fireMove: MoveData = { type: "Fire" } as any;

  const dmg2 = AbilityRegistry.applyModifier(
    fluffyMon.ability,
    "onDamageMultiplier",
    100,
    { owner: fluffyMon, move: contactMove }
  );
  console.log(`Fluffy (Contact): ${dmg2} (Expected: 50)`);

  const dmg3 = AbilityRegistry.applyModifier(
    fluffyMon.ability,
    "onDamageMultiplier",
    100,
    { owner: fluffyMon, move: fireMove }
  );
  console.log(`Fluffy (Fire): ${dmg3} (Expected: 200)`);

  // Water Bubble (Defensive & Status)
  const bubbleMon = createMon("Araquanid", "Water Bubble");
  const fireMove2: MoveData = { type: "Fire" } as any;

  const dmg4 = AbilityRegistry.applyModifier(
    bubbleMon.ability,
    "onDamageMultiplier",
    100,
    { owner: bubbleMon, move: fireMove2 }
  );
  console.log(`Water Bubble (Fire): ${dmg4} (Expected: 50)`);

  const burnBlocked = !AbilityRegistry.get(bubbleMon.ability)!.onSetStatus!(
    { owner: bubbleMon, battle: mockBattle },
    "Burn"
  );
  console.log(`Water Bubble (Burn Blocked): ${burnBlocked} (Expected: true)`);
}

async function testEntryEffects() {
  console.log("\n--- Testing Entry Effects ---");

  // Intrepid Sword
  const zacian = createMon("Zacian", "Intrepid Sword");
  const ctx1 = { owner: zacian, battle: mockBattle };
  await AbilityRegistry.trigger(zacian.ability, "onBattleStart", ctx1);
  console.log(
    `Intrepid Sword Attack Stage: ${zacian.statStages.attack} (Expected: 1)`
  );

  // Dauntless Shield
  const zamazenta = createMon("Zamazenta", "Dauntless Shield");
  const ctx2 = { owner: zamazenta, battle: mockBattle };
  await AbilityRegistry.trigger(zamazenta.ability, "onBattleStart", ctx2);
  console.log(
    `Dauntless Shield Defense Stage: ${zamazenta.statStages.defense} (Expected: 1)`
  );
}

async function run() {
  await testPowerBoosters();
  await testDefensiveAbilities();
  await testEntryEffects();
}

run();
