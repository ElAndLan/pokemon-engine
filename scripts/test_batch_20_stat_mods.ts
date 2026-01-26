import { PokemonInstance } from "../src/renderer/src/core/data/DataTypes";
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

function testSimple() {
  console.log("\n--- Testing Simple ---");
  const mon = createMon("Bidoof", "Simple");

  // Apply +1 Attack
  console.log("Applying +1 Attack...");
  AtomicEffects.applyStatChange(mon, "attack", 1, 100, mon);
  console.log(`Attack Stage: ${mon.statStages.attack} (Expected: 2)`);

  // Apply -1 Defense
  console.log("Applying -1 Defense...");
  AtomicEffects.applyStatChange(mon, "defense", -1, 100, mon);
  console.log(`Defense Stage: ${mon.statStages.defense} (Expected: -2)`);
}

function testContrary() {
  console.log("\n--- Testing Contrary ---");
  const mon = createMon("Inkay", "Contrary");

  // Apply -1 Attack (Intimidate scenario)
  console.log("Applying -1 Attack...");
  const opponent = createMon("Opponent", "Intimidate");
  AtomicEffects.applyStatChange(mon, "attack", -1, 100, opponent);
  console.log(`Attack Stage: ${mon.statStages.attack} (Expected: 1)`);

  // Apply +2 Speed
  console.log("Applying +2 Speed...");
  AtomicEffects.applyStatChange(mon, "speed", 2, 100, mon);
  console.log(`Speed Stage: ${mon.statStages.speed} (Expected: -2)`);
}

function testBigPecks() {
  console.log("\n--- Testing Big Pecks ---");
  const mon = createMon("Pidove", "Big Pecks");
  const opponent = createMon("Opponent", "None");

  // Apply -1 Defense
  console.log("Applying -1 Defense from opponent...");
  AtomicEffects.applyStatChange(mon, "defense", -1, 100, opponent);
  console.log(`Defense Stage: ${mon.statStages.defense} (Expected: 0)`);

  // Apply -1 Attack (Should pass)
  console.log("Applying -1 Attack from opponent...");
  AtomicEffects.applyStatChange(mon, "attack", -1, 100, opponent);
  console.log(`Attack Stage: ${mon.statStages.attack} (Expected: -1)`);
}

function testDefiant() {
  console.log("\n--- Testing Defiant ---");
  const mon = createMon("Pawniard", "Defiant");
  const opponent = createMon("Opponent", "Intimidate");

  // Apply -1 Speed
  console.log("Applying -1 Speed from opponent...");
  const events = AtomicEffects.applyStatChange(mon, "speed", -1, 100, opponent);

  // Check if Attack rose
  console.log(`Speed Stage: ${mon.statStages.speed} (Expected: -1)`);
  console.log(`Attack Stage: ${mon.statStages.attack} (Expected: 2)`);

  // Check Logs
  console.log(
    "Events:",
    events.map((e) => e.message || e.type)
  );
}

function testCompetitive() {
  console.log("\n--- Testing Competitive ---");
  const mon = createMon("Milotic", "Competitive");
  const opponent = createMon("Opponent", "Intimidate");

  // Apply -1 Accuracy
  console.log("Applying -1 Accuracy from opponent...");
  AtomicEffects.applyStatChange(mon, "accuracy", -1, 100, opponent);

  // Check if Sp. Atk rose
  console.log(`Accuracy Stage: ${mon.statStages.accuracy} (Expected: -1)`);
  console.log(`Sp. Atk Stage: ${mon.statStages.spAttack} (Expected: 2)`);
}

function run() {
  testSimple();
  testContrary();
  testBigPecks();
  testDefiant();
  testCompetitive();
}

run();
