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
  weather: { type: "None", turns: 0 },
  terrain: "None",
};

async function testWeatherSpeed() {
  console.log("\n--- Testing Weather Speed ---");

  // Sand Rush
  const sandshrew = createMon("Sandshrew", "Sand Rush");
  mockBattle.weather.type = "Sandstorm";
  const speed1 = AbilityRegistry.applyModifier(
    sandshrew.ability,
    "onStatCalculation",
    100,
    { owner: sandshrew, battle: mockBattle, statName: "speed" }
  );
  console.log(`Sand Rush Speed: ${speed1} (Expected: 200)`);

  // Slush Rush
  const sandshrewAlola = createMon("Alolan Sandshrew", "Slush Rush");
  mockBattle.weather.type = "Hail";
  const speed2 = AbilityRegistry.applyModifier(
    sandshrewAlola.ability,
    "onStatCalculation",
    100,
    { owner: sandshrewAlola, battle: mockBattle, statName: "speed" }
  );
  console.log(`Slush Rush Speed (Hail): ${speed2} (Expected: 200)`);

  mockBattle.weather.type = "Snow";
  const speed3 = AbilityRegistry.applyModifier(
    sandshrewAlola.ability,
    "onStatCalculation",
    100,
    { owner: sandshrewAlola, battle: mockBattle, statName: "speed" }
  );
  console.log(`Slush Rush Speed (Snow): ${speed3} (Expected: 200)`);

  // Surge Surfer
  const raichu = createMon("Alolan Raichu", "Surge Surfer");
  mockBattle.weather.type = "None";
  mockBattle.terrain = "Electric";
  const speed4 = AbilityRegistry.applyModifier(
    raichu.ability,
    "onStatCalculation",
    100,
    { owner: raichu, battle: mockBattle, statName: "speed" }
  );
  console.log(`Surge Surfer Speed: ${speed4} (Expected: 200)`);
}

async function testFlowerGift() {
  console.log("\n--- Testing Flower Gift ---");
  const cherrim = createMon("Cherrim", "Flower Gift");
  mockBattle.weather.type = "Sun";

  const atk = AbilityRegistry.applyModifier(
    cherrim.ability,
    "onModifyAttack",
    100,
    { owner: cherrim, battle: mockBattle }
  );
  console.log(`Flower Gift Attack (Sun): ${atk} (Expected: 150)`);

  const def = AbilityRegistry.applyModifier(
    cherrim.ability,
    "onModifyDefense",
    100,
    {
      owner: cherrim,
      battle: mockBattle,
      statName: "spDefense",
      move: { category: "Special" } as any,
    }
  );
  console.log(`Flower Gift Sp.Def (Sun): ${def} (Expected: 150)`);
}

async function testIceFace() {
  console.log("\n--- Testing Ice Face ---");
  const eiscue = createMon("Eiscue", "Ice Face");
  const physMove: MoveData = { category: "Physical" } as any;
  const specMove: MoveData = { category: "Special" } as any;
  const events: any[] = [];

  // Physical Hit
  const hit1 = AbilityRegistry.get("Ice Face")!.onTryHit!(
    { owner: eiscue, move: physMove, battle: mockBattle },
    events
  );
  console.log(`Physical Hit Allowed? ${hit1} (Expected: false)`);
  console.log(`Face Broken? ${eiscue.volatile["NoiceFace"]} (Expected: 1)`);

  // Special Hit
  const hit2 = AbilityRegistry.get("Ice Face")!.onTryHit!(
    { owner: eiscue, move: specMove, battle: mockBattle },
    events
  );
  console.log(`Special Hit Allowed? ${hit2} (Expected: true)`);

  // Restore
  mockBattle.weather.type = "Snow";
  await AbilityRegistry.trigger("Ice Face", "onTurnEnd", {
    owner: eiscue,
    battle: mockBattle,
  });
  console.log(
    `Face Restored? ${!eiscue.volatile["NoiceFace"]} (Expected: true)`
  );
}

async function testStatusBoosts() {
  console.log("\n--- Testing Status Boosts ---");

  // Toxic Boost
  const zangoose = createMon("Zangoose", "Toxic Boost");
  zangoose.status = "Poison";
  const physMove: MoveData = { category: "Physical" } as any;
  const atk = AbilityRegistry.applyModifier(
    zangoose.ability,
    "onModifyAttack",
    100,
    { owner: zangoose, move: physMove }
  );
  console.log(`Toxic Boost Attack: ${atk} (Expected: 150)`);

  // Flare Boost
  const drifblim = createMon("Drifblim", "Flare Boost");
  drifblim.status = "Burn";
  const specMove: MoveData = { category: "Special" } as any;
  const spAtk = AbilityRegistry.applyModifier(
    drifblim.ability,
    "onModifyAttack",
    100,
    { owner: drifblim, move: specMove }
  );
  console.log(`Flare Boost Sp.Atk: ${spAtk} (Expected: 150)`);
}

async function testIceScales() {
  console.log("\n--- Testing Ice Scales ---");
  const frosmoth = createMon("Frosmoth", "Ice Scales");
  const specMove: MoveData = { category: "Special" } as any;
  const dmg = AbilityRegistry.applyModifier(
    frosmoth.ability,
    "onDamageMultiplier",
    100,
    { owner: frosmoth, move: specMove }
  );
  console.log(`Ice Scales Damage: ${dmg} (Expected: 50)`);
}

async function run() {
  await testWeatherSpeed();
  await testFlowerGift();
  await testIceFace();
  await testStatusBoosts();
  await testIceScales();
}

run();
