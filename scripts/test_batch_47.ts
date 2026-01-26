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
  terrain: "None",
};

async function testBatch47() {
  console.log("\n--- Testing Batch 47 (Final) ---");

  // Stealth
  const kecleon = createMon("Kecleon", "Stealth");
  const eva = AbilityRegistry.applyModifier(
    kecleon.ability,
    "onModifyEvasion",
    0,
    { owner: kecleon }
  );
  console.log(`Stealth Evasion: ${eva} (Expected: 1)`);

  // Grass Cloak
  const burmy = createMon("Burmy", "Grass Cloak");
  mockBattle.terrain = "Grassy";
  const def = AbilityRegistry.applyModifier(
    burmy.ability,
    "onModifyDefense",
    100,
    { owner: burmy, battle: mockBattle }
  );
  console.log(`Grass Cloak Defense: ${def} (Expected: 150)`);
  mockBattle.terrain = "None";

  // Celebrate
  const pika = createMon("Pikachu", "Celebrate");
  await AbilityRegistry.trigger("Celebrate", "onKOTarget", {
    owner: pika,
    battle: mockBattle,
  });
  // Expect text

  // Lullaby
  const jiggly = createMon("Jigglypuff", "Lullaby");
  // Simulate until trigger
  for (let i = 0; i < 20; i++) {
    await AbilityRegistry.trigger("Lullaby", "onTurnEnd", {
      owner: jiggly,
      battle: mockBattle,
    });
  }

  // Frighten
  const snubbull = createMon("Snubbull", "Frighten");
  await AbilityRegistry.trigger("Frighten", "onBattleStart", {
    owner: snubbull,
    battle: mockBattle,
  });
  // Expect text

  // Omnipotent
  const arceus = createMon("Arceus", "Omnipotent");
  arceus.currentHp = 50;
  arceus.status = "Paralysis";
  await AbilityRegistry.trigger("Omnipotent", "onTurnEnd", {
    owner: arceus,
    battle: mockBattle,
  });
  console.log(`Omnipotent HP: ${arceus.currentHp} (Expected: 75)`);
  console.log(`Omnipotent Status: ${arceus.status} (Expected: None)`);
  console.log(
    `Omnipotent Atk Boost: ${arceus.statStages.attack} (Expected: 1)`
  );

  // Flame Boost
  const emboar = createMon("Emboar", "Flame Boost");
  const fireMove = { type: "Fire" } as any;
  await AbilityRegistry.trigger("Flame Boost", "onAfterDamage", {
    owner: emboar,
    move: fireMove,
    battle: mockBattle,
  });
  console.log(`Flame Boost Attack: ${emboar.statStages.attack} (Expected: 1)`);

  // Aqua Boost
  const samurott = createMon("Samurott", "Aqua Boost");
  const waterMove = { type: "Water" } as any;
  await AbilityRegistry.trigger("Aqua Boost", "onAfterDamage", {
    owner: samurott,
    move: waterMove,
    battle: mockBattle,
  });
  console.log(`Aqua Boost Attack: ${samurott.statStages.attack} (Expected: 1)`);

  // Conqueror
  const rhyperior = createMon("Rhyperior", "Conqueror");
  await AbilityRegistry.trigger("Conqueror", "onKOTarget", {
    owner: rhyperior,
    battle: mockBattle,
  });
  console.log(
    `Conqueror Atk: ${rhyperior.statStages.attack}, Def: ${rhyperior.statStages.defense} (Expected: 1, 1)`
  );

  // Decoy
  const croagunk = createMon("Croagunk", "Decoy");
  const eva2 = AbilityRegistry.applyModifier(
    croagunk.ability,
    "onModifyEvasion",
    0,
    { owner: croagunk }
  );
  console.log(`Decoy Evasion: ${eva2} (Expected: 1)`);

  // Shield
  const bastiodon = createMon("Bastiodon", "Shield");
  const dmg = AbilityRegistry.applyModifier(
    bastiodon.ability,
    "onDamageMultiplier",
    100,
    { owner: bastiodon }
  );
  console.log(`Shield Damage: ${dmg} (Expected: 80)`);
}

async function run() {
  await testBatch47();
}

run();
