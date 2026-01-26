import {
  PokemonInstance,
  MoveData,
} from "../src/renderer/src/core/data/DataTypes";
import { AbilityRegistry } from "../src/renderer/src/core/battle/Abilities";
import { MoveEngine } from "../src/renderer/src/core/battle/MoveEngine";
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

async function testHustle() {
  console.log("\n--- Testing Hustle ---");
  const mon = createMon("Durant", "Hustle");
  const move: MoveData = { category: "Physical" } as any;

  const acc = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyAccuracy",
    100,
    { owner: mon, move }
  );
  console.log(`Accuracy: ${acc} (Expected: 80)`);

  const atk = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyAttack",
    100,
    { owner: mon, move }
  );
  console.log(`Attack: ${atk} (Expected: 150)`);
}

async function testTangledFeet() {
  console.log("\n--- Testing Tangled Feet ---");
  const mon = createMon("Spinda", "Tangled Feet");
  mon.volatile["Confusion"] = 3;

  const eva = AbilityRegistry.applyModifier(mon.ability, "onModifyEvasion", 1, {
    owner: mon,
  });
  console.log(`Evasion: ${eva} (Expected: 2)`);
}

async function testAnalytic() {
  console.log("\n--- Testing Analytic ---");
  const mon = createMon("Magnezone", "Analytic");
  mon.currentStats.speed = 10;
  const target = createMon("Target", "None");
  target.currentStats.speed = 100;

  // Slower speed -> assumption: moved last
  const power = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyBasePower",
    100,
    { owner: mon, target }
  );
  console.log(`Power (Slower): ${power} (Expected: 130)`);
}

async function testSheerForce() {
  console.log("\n--- Testing Sheer Force ---");
  const mon = createMon("Nidoking", "Sheer Force");
  const move: MoveData = { name: "Sludge Bomb", secondaryChance: 30 } as any;

  const power = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyBasePower",
    100,
    { owner: mon, move }
  );
  console.log(`Power (Has Effect): ${power} (Expected: 130)`);

  const chance = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyEffectChance",
    30,
    { owner: mon, move }
  );
  console.log(`Effect Chance: ${chance} (Expected: 0)`);
}

async function testSkillLink() {
  console.log("\n--- Testing Skill Link ---");
  const mon = createMon("Cloyster", "Skill Link");
  const move: MoveData = { multiHit: { min: 2, max: 5 } } as any;

  const hits = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyMultiHit",
    0,
    { owner: mon, move }
  );
  console.log(`Hits: ${hits} (Expected: 5)`);
}

async function testProtean() {
  console.log("\n--- Testing Protean ---");
  const mon = createMon("Greninja", "Protean");
  mon.types = ["Water"];
  const move: MoveData = { type: "Dark" } as any;

  const ctx = { owner: mon, move, battle: mockBattle };
  await AbilityRegistry.trigger(mon.ability, "onBeforeMove", ctx);
  console.log(`Type: ${mon.types[0]} (Expected: Dark)`);
}

async function testSandForce() {
  console.log("\n--- Testing Sand Force ---");
  const mon = createMon("Excadrill", "Sand Force");
  const move: MoveData = { type: "Ground" } as any;

  // Needs battle context with weather
  const battle = {
    ...mockBattle,
    game: { weatherManager: { currentWeather: "Sandstorm" } },
  };

  const power = AbilityRegistry.applyModifier(
    mon.ability,
    "onModifyBasePower",
    100,
    { owner: mon, move, battle }
  );
  console.log(`Power (Sandstorm): ${power} (Expected: 130)`);
}

async function testOvercoat() {
  console.log("\n--- Testing Overcoat ---");
  const mon = createMon("Reuniclus", "Overcoat");
  const move: MoveData = {
    name: "Sleep Powder",
    flags: { powder: true },
  } as any;

  const events: any[] = [];
  const ctx = { owner: mon, move, battle: mockBattle };

  const result = AbilityRegistry.get(mon.ability)?.onTryHit?.(ctx, events);
  console.log(`Powder Hit Allowed? ${result} (Expected: false)`);
}

async function run() {
  await testHustle();
  await testTangledFeet();
  await testAnalytic();
  await testSheerForce();
  await testSkillLink();
  await testProtean();
  await testSandForce();
  await testOvercoat();
}

run();
