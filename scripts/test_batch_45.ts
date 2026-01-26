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
    side: "player",
  } as any;
}

const mockBattle: any = {
  showText: async (msg: string) => console.log(`[TEXT] ${msg}`),
  setWeather: (w: string, duration: number) =>
    console.log(`[WEATHER] Set to ${w}`),
  weather: { type: "None" },
};

async function testBatch45() {
  console.log("\n--- Testing Batch 45 (Conquest II) ---");

  // Wave Rider
  const buizel = createMon("Buizel", "Wave Rider");
  mockBattle.weather.type = "Rain";
  const speed = AbilityRegistry.applyModifier(
    buizel.ability,
    "onStatCalculation",
    100,
    { owner: buizel, statName: "speed", battle: mockBattle }
  );
  console.log(`Wave Rider Speed (Rain): ${speed} (Expected: 150)`);

  // Skater
  const surskit = createMon("Surskit", "Skater");
  mockBattle.weather.type = "Hail";
  const speed2 = AbilityRegistry.applyModifier(
    surskit.ability,
    "onStatCalculation",
    100,
    { owner: surskit, statName: "speed", battle: mockBattle }
  );
  console.log(`Skater Speed (Hail): ${speed2} (Expected: 150)`);
  mockBattle.weather.type = "None";

  // Thrust
  const heracross = createMon("Heracross", "Thrust");
  // Simulate push chance
  let pushed = false;
  for (let i = 0; i < 20; i++) {
    await AbilityRegistry.trigger("Thrust", "onAfterDamage", {
      owner: heracross,
      battle: mockBattle,
    });
    // We can't easily detect console log output in script, but if it runs without error it's good.
    // We'll rely on seeing [TEXT] output.
  }

  // Parry
  const scizor = createMon("Scizor", "Parry");
  const eva = AbilityRegistry.applyModifier(
    scizor.ability,
    "onModifyEvasion",
    0,
    { owner: scizor }
  );
  console.log(`Parry Evasion: ${eva} (Expected: 1)`);

  // Tenacity
  const machop = createMon("Machop", "Tenacity");
  let endured = false;
  for (let i = 0; i < 50; i++) {
    machop.currentHp = 10;
    const allowKO = AbilityRegistry.get("Tenacity")!.onTryKO!({
      owner: machop,
      battle: mockBattle,
    });
    if (allowKO === false) {
      endured = true;
      break;
    }
  }
  console.log(`Tenacity Endured? ${endured} (Expected: true)`);

  // Pride
  const gyarados = createMon("Gyarados", "Pride");
  await AbilityRegistry.trigger("Pride", "onKOTarget", {
    owner: gyarados,
    battle: mockBattle,
  });
  console.log(
    `Pride Attack Boost: ${gyarados.statStages.attack} (Expected: 1)`
  );

  // Deep Sleep
  const snorlax = createMon("Snorlax", "Deep Sleep");
  snorlax.status = "Sleep";
  snorlax.currentHp = 50;
  await AbilityRegistry.trigger("Deep Sleep", "onTurnEnd", {
    owner: snorlax,
    battle: mockBattle,
  });
  console.log(`Deep Sleep Healed? ${snorlax.currentHp > 50} (Expected: true)`);

  // Power Nap
  const teddiursa = createMon("Teddiursa", "Power Nap");
  teddiursa.status = "Sleep";
  teddiursa.currentHp = 50;
  await AbilityRegistry.trigger("Power Nap", "onTurnEnd", {
    owner: teddiursa,
    battle: mockBattle,
  });
  console.log(`Power Nap Healed? ${teddiursa.currentHp > 50} (Expected: true)`);

  // Spirit
  const lucario = createMon("Lucario", "Spirit");
  lucario.currentHp = 20; // < 100/3
  await AbilityRegistry.trigger("Spirit", "onAfterDamage", {
    owner: lucario,
    battle: mockBattle,
  });
  console.log(`Spirit Healed? ${lucario.currentHp > 20} (Expected: true)`);

  // Warm Blanket
  const magcargo = createMon("Magcargo", "Warm Blanket");
  mockBattle.weather.type = "Harsh Sunlight";
  magcargo.currentHp = 50;
  await AbilityRegistry.trigger("Warm Blanket", "onTurnEnd", {
    owner: magcargo,
    battle: mockBattle,
  });
  console.log(
    `Warm Blanket Healed? ${magcargo.currentHp > 50} (Expected: true)`
  );
}

async function run() {
  await testBatch45();
}

run();
