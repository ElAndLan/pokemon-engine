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

async function testBatch46() {
  console.log("\n--- Testing Batch 46 (Conquest III) ---");

  // Gulp
  const cramorant = createMon("Cramorant", "Gulp");
  mockBattle.weather.type = "Rain";
  cramorant.currentHp = 50;
  await AbilityRegistry.trigger("Gulp", "onTurnEnd", {
    owner: cramorant,
    battle: mockBattle,
  });
  console.log(`Gulp Healed? ${cramorant.currentHp > 50} (Expected: true)`);

  // Sandpit
  const sandygast = createMon("Sandygast", "Sandpit");
  mockBattle.weather.type = "Sandstorm";
  sandygast.currentHp = 50;
  await AbilityRegistry.trigger("Sandpit", "onTurnEnd", {
    owner: sandygast,
    battle: mockBattle,
  });
  console.log(`Sandpit Healed? ${sandygast.currentHp > 50} (Expected: true)`);

  // Hot Blooded
  const magmar = createMon("Magmar", "Hot Blooded");
  mockBattle.weather.type = "Harsh Sunlight";
  magmar.currentHp = 50;
  await AbilityRegistry.trigger("Hot Blooded", "onTurnEnd", {
    owner: magmar,
    battle: mockBattle,
  });
  console.log(`Hot Blooded Healed? ${magmar.currentHp > 50} (Expected: true)`);
  mockBattle.weather.type = "None";

  // Medic
  const chansey = createMon("Chansey", "Medic");
  await AbilityRegistry.trigger("Medic", "onTurnEnd", {
    owner: chansey,
    battle: mockBattle,
  });
  // Expect text

  // Life Force
  const mew = createMon("Mew", "Life Force");
  mew.currentHp = 90;
  await AbilityRegistry.trigger("Life Force", "onTurnEnd", {
    owner: mew,
    battle: mockBattle,
  });
  console.log(`Life Force Healed? ${mew.currentHp > 90} (Expected: true)`);

  // Nurse
  const blissey = createMon("Blissey", "Nurse");
  await AbilityRegistry.trigger("Nurse", "onTurnEnd", {
    owner: blissey,
    battle: mockBattle,
  });
  // Expect text

  // Melee
  const gallade = createMon("Gallade", "Melee");
  const contactMove = { flags: { contact: true } } as any;
  const dmg = AbilityRegistry.applyModifier(
    gallade.ability,
    "onModifyAttack",
    100,
    { owner: gallade, move: contactMove }
  );
  console.log(`Melee Damage Boost: ${dmg} (Expected: 120)`);

  // Sponge
  const starmie = createMon("Starmie", "Sponge");
  starmie.currentHp = 50;
  const waterMove = { type: "Water" } as any;
  const hit = AbilityRegistry.get("Sponge")!.onTryHit!(
    { owner: starmie, move: waterMove, battle: mockBattle },
    []
  );
  console.log(`Sponge Absorb? ${!hit} (Expected: true)`);
  console.log(`Sponge Healed? ${starmie.currentHp > 50} (Expected: true)`);
}

async function run() {
  await testBatch46();
}

run();
