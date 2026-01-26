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
    gender: "Male", // Default
  } as any;
}

const mockBattle: any = {
  showText: async (msg: string) => console.log(`[TEXT] ${msg}`),
  getOpponent: (mon: any) => {
    return {
      nickname: "Opponent",
      moves: [
        { name: "Hyper Beam", power: 150 },
        { name: "Tackle", power: 40 },
      ],
      gender: "Male",
    };
  },
};

async function testStench() {
  console.log("\n--- Testing Stench ---");
  const muk = createMon("Muk", "Stench");
  const target = createMon("Target", "None");

  // Simulate multiple hits to trigger 10%
  let flinched = false;
  for (let i = 0; i < 50; i++) {
    await AbilityRegistry.trigger("Stench", "onAfterDamage", {
      owner: muk,
      target: target,
      battle: mockBattle,
    });
    if (target.volatile["Flinch"]) {
      flinched = true;
      break;
    }
  }
  console.log(
    `Flinched Triggered? ${flinched} (Expected: true with high prob)`
  );
}

async function testColorChange() {
  console.log("\n--- Testing Color Change ---");
  const kecleon = createMon("Kecleon", "Color Change");
  const move = { type: "Fire" } as any;

  await AbilityRegistry.trigger("Color Change", "onAfterDamage", {
    owner: kecleon,
    move: move,
    battle: mockBattle,
  });
  console.log(`New Type: ${kecleon.types[0]} (Expected: Fire)`);
}

async function testTruant() {
  console.log("\n--- Testing Truant ---");
  const slaking = createMon("Slaking", "Truant");

  // Turn 1 (Move)
  let canMove = await AbilityRegistry.get("Truant")!.onBeforeMove!({
    owner: slaking,
    battle: mockBattle,
  });
  console.log(`Turn 1 Can Move? ${canMove} (Expected: true)`);
  console.log(`Truant Set? ${slaking.volatile["Truant"]} (Expected: 1)`);

  // Turn 2 (Loaf)
  canMove = await AbilityRegistry.get("Truant")!.onBeforeMove!({
    owner: slaking,
    battle: mockBattle,
  });
  console.log(`Turn 2 Can Move? ${canMove} (Expected: false)`);
  console.log(
    `Truant Cleared? ${slaking.volatile["Truant"]} (Expected: undefined)`
  );
}

async function testRivalry() {
  console.log("\n--- Testing Rivalry ---");
  const nido = createMon("Nidoking", "Rivalry"); // Male
  const opp = createMon("Opponent", "None"); // Male

  const boost = AbilityRegistry.applyModifier(
    nido.ability,
    "onModifyAttack",
    100,
    { owner: nido, target: opp }
  );
  console.log(`Same Gender Damage: ${boost} (Expected: 125)`);

  opp.gender = "Female";
  const nerf = AbilityRegistry.applyModifier(
    nido.ability,
    "onModifyAttack",
    100,
    { owner: nido, target: opp }
  );
  console.log(`Opposite Gender Damage: ${nerf} (Expected: 75)`);
}

async function testForewarn() {
  console.log("\n--- Testing Forewarn ---");
  const drowzee = createMon("Drowzee", "Forewarn");
  await AbilityRegistry.trigger("Forewarn", "onBattleStart", {
    owner: drowzee,
    battle: mockBattle,
  });
  // Expect text about Hyper Beam (150 power)
}

async function testScrappy() {
  console.log("\n--- Testing Scrappy ---");
  const miltank = createMon("Miltank", "Scrappy");
  const move = { type: "Normal", flags: {} } as any;
  AbilityRegistry.get("Scrappy")!.onModifyMove!(move, { owner: miltank });
  console.log(`Ignore Immunity? ${move.flags.ignoreImmunity} (Expected: true)`);
}

async function run() {
  await testStench();
  await testColorChange();
  await testTruant();
  await testRivalry();
  await testForewarn();
  await testScrappy();
}

run();
