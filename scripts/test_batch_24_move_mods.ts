import {
  PokemonInstance,
  MoveData,
} from "../src/renderer/src/core/data/DataTypes";
import { AbilityRegistry } from "../src/renderer/src/core/battle/Abilities";
import { MoveEngine } from "../src/renderer/src/core/battle/MoveEngine";
import { AtomicEffects } from "../src/renderer/src/core/battle/AtomicEffects";
import { getTypeEffectiveness } from "../src/renderer/src/core/battle/TypeChart";

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

async function testLiquidVoice() {
  console.log("\n--- Testing Liquid Voice ---");
  const mon = createMon("Primarina", "Liquid Voice");
  const move: MoveData = {
    name: "Hyper Voice",
    type: "Normal",
    flags: { sound: true },
  } as any;

  // Check type modification hook directly
  const newType = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyType",
    move.type,
    { owner: mon, move }
  );
  console.log(`New Type (Sound): ${newType} (Expected: Water)`);

  const move2: MoveData = { name: "Tackle", type: "Normal", flags: {} } as any;
  const newType2 = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyType",
    move2.type,
    { owner: mon, move: move2 }
  );
  console.log(`New Type (No Sound): ${newType2} (Expected: Normal)`);
}

async function testGalvanize() {
  console.log("\n--- Testing Galvanize ---");
  const mon = createMon("Geodude", "Galvanize");
  const move: MoveData = { type: "Normal", power: 100 } as any;

  const newType = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyType",
    move.type,
    { owner: mon, move }
  );
  console.log(`New Type: ${newType} (Expected: Electric)`);

  const newPower = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyBasePower",
    move.power,
    { owner: mon, move }
  );
  console.log(`New Power: ${newPower} (Expected: 120)`);
}

async function testRefrigerate() {
  console.log("\n--- Testing Refrigerate ---");
  const mon = createMon("Glalie", "Refrigerate");
  const move: MoveData = { type: "Normal" } as any;

  const newType = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyType",
    move.type,
    { owner: mon, move }
  );
  console.log(`New Type: ${newType} (Expected: Ice)`);
}

async function testPixilate() {
  console.log("\n--- Testing Pixilate ---");
  const mon = createMon("Sylveon", "Pixilate");
  const move: MoveData = { type: "Normal" } as any;

  const newType = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyType",
    move.type,
    { owner: mon, move }
  );
  console.log(`New Type: ${newType} (Expected: Fairy)`);
}

async function testAerilate() {
  console.log("\n--- Testing Aerilate ---");
  const mon = createMon("Mega Pinsir", "Aerilate");
  const move: MoveData = { type: "Normal" } as any;

  const newType = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyType",
    move.type,
    { owner: mon, move }
  );
  console.log(`New Type: ${newType} (Expected: Flying)`);
}

async function testNormalize() {
  console.log("\n--- Testing Normalize ---");
  const mon = createMon("Delcatty", "Normalize");
  const move: MoveData = { type: "Water", power: 100 } as any;

  const newType = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyType",
    move.type,
    { owner: mon, move }
  );
  console.log(`New Type: ${newType} (Expected: Normal)`);

  const newPower = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyBasePower",
    move.power,
    { owner: mon, move }
  );
  console.log(`New Power: ${newPower} (Expected: 120)`);
}

async function testIntegration() {
  console.log("\n--- Testing Integration (Ghost Immunity) ---");
  const attacker = createMon("Attacker", "Normalize"); // All moves become Normal
  const defender = createMon("Ghost", "None");
  defender.types = ["Ghost"];

  const move: MoveData = {
    name: "Thunderbolt",
    type: "Electric",
    power: 90,
    category: "Special",
    target: "Normal",
  } as any;

  // Thunderbolt becomes Normal type. Ghost is immune to Normal.
  // MoveEngine should detect immunity.

  const result = MoveEngine.executeMove(attacker, defender, move);
  const immune = result.events.some((e) =>
    e.message?.includes("doesn't affect")
  );
  console.log(`Immunity Triggered? ${immune} (Expected: true)`);

  // Also verify type change logic inside MoveEngine
  // We can't see internal variables, but immunity confirms it.
  // If it stayed Electric, it would hit Ghost (1x).
}

async function run() {
  await testLiquidVoice();
  await testGalvanize();
  await testRefrigerate();
  await testPixilate();
  await testAerilate();
  await testNormalize();
  await testIntegration();
}

run();
