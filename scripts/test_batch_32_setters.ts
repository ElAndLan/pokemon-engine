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
  setWeather: (type: string, turns: number) => {
    console.log(`[BATTLE] Weather set to ${type}`);
    mockBattle.weather.type = type;
  },
  setTerrain: (type: string, turns: number) => {
    console.log(`[BATTLE] Terrain set to ${type}`);
    mockBattle.terrain = type;
  },
};

async function testTerrainSetters() {
  console.log("\n--- Testing Terrain Setters ---");

  // Electric Surge
  const koko = createMon("Tapu Koko", "Electric Surge");
  await AbilityRegistry.trigger("Electric Surge", "onBattleStart", {
    owner: koko,
    battle: mockBattle,
  });
  console.log(
    `Terrain is Electric? ${mockBattle.terrain === "Electric"} (Expected: true)`
  );

  // Psychic Surge
  const lele = createMon("Tapu Lele", "Psychic Surge");
  await AbilityRegistry.trigger("Psychic Surge", "onBattleStart", {
    owner: lele,
    battle: mockBattle,
  });
  console.log(
    `Terrain is Psychic? ${mockBattle.terrain === "Psychic"} (Expected: true)`
  );

  // Grassy Surge
  const bulu = createMon("Tapu Bulu", "Grassy Surge");
  await AbilityRegistry.trigger("Grassy Surge", "onBattleStart", {
    owner: bulu,
    battle: mockBattle,
  });
  console.log(
    `Terrain is Grassy? ${mockBattle.terrain === "Grassy"} (Expected: true)`
  );

  // Misty Surge
  const fini = createMon("Tapu Fini", "Misty Surge");
  await AbilityRegistry.trigger("Misty Surge", "onBattleStart", {
    owner: fini,
    battle: mockBattle,
  });
  console.log(
    `Terrain is Misty? ${mockBattle.terrain === "Misty"} (Expected: true)`
  );
}

async function testWeatherSetters() {
  console.log("\n--- Testing Weather Setters ---");

  // Primordial Sea
  const kyogre = createMon("Kyogre", "Primordial Sea");
  await AbilityRegistry.trigger("Primordial Sea", "onBattleStart", {
    owner: kyogre,
    battle: mockBattle,
  });
  console.log(
    `Weather is HeavyRain? ${
      mockBattle.weather.type === "HeavyRain"
    } (Expected: true)`
  );

  // Desolate Land
  const groudon = createMon("Groudon", "Desolate Land");
  await AbilityRegistry.trigger("Desolate Land", "onBattleStart", {
    owner: groudon,
    battle: mockBattle,
  });
  console.log(
    `Weather is HarshSun? ${
      mockBattle.weather.type === "HarshSun"
    } (Expected: true)`
  );

  // Sand Spit
  const conda = createMon("Sandaconda", "Sand Spit");
  const ctx = { owner: conda, battle: mockBattle };
  if (AbilityRegistry.get("Sand Spit")?.onAfterDamage) {
    await AbilityRegistry.get("Sand Spit")!.onAfterDamage!(ctx, 10);
  }
  console.log(
    `Weather is Sandstorm? ${
      mockBattle.weather.type === "Sandstorm"
    } (Expected: true)`
  );
}

async function testMimicry() {
  console.log("\n--- Testing Mimicry ---");
  const stunfisk = createMon("Stunfisk", "Mimicry");
  mockBattle.terrain = "Electric";

  await AbilityRegistry.trigger("Mimicry", "onTurnStart", {
    owner: stunfisk,
    battle: mockBattle,
  });
  console.log(`Mimicry Type: ${stunfisk.types[0]} (Expected: Electric)`);

  mockBattle.terrain = "Grassy";
  await AbilityRegistry.trigger("Mimicry", "onTurnStart", {
    owner: stunfisk,
    battle: mockBattle,
  });
  console.log(`Mimicry Type: ${stunfisk.types[0]} (Expected: Grass)`);
}

async function testGrassPelt() {
  console.log("\n--- Testing Grass Pelt ---");
  const skiddo = createMon("Skiddo", "Grass Pelt");
  mockBattle.terrain = "Grassy";
  const def = AbilityRegistry.applyModifier(
    skiddo.ability,
    "onModifyDefense",
    100,
    { owner: skiddo, battle: mockBattle }
  );
  console.log(`Grass Pelt Defense: ${def} (Expected: 150)`);
}

async function testTeraformZero() {
  console.log("\n--- Testing Teraform Zero ---");
  const terapagos = createMon("Terapagos", "Teraform Zero");
  // Set field state
  mockBattle.weather.type = "Rain";
  mockBattle.terrain = "Electric";

  await AbilityRegistry.trigger("Teraform Zero", "onBattleStart", {
    owner: terapagos,
    battle: mockBattle,
  });
  console.log(
    `Weather Cleared? ${mockBattle.weather.type === "None"} (Expected: true)`
  );
  console.log(
    `Terrain Cleared? ${mockBattle.terrain === "None"} (Expected: true)`
  );
}

async function run() {
  await testTerrainSetters();
  await testWeatherSetters();
  await testMimicry();
  await testGrassPelt();
  await testTeraformZero();
}

run();
