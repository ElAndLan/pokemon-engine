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
  getOpponent: (mon: any) => {
    return { nickname: "Opponent", heldItem: "Leftovers" };
  },
  weather: { type: "None" },
};

async function testItemAbilities() {
  console.log("\n--- Testing Item Abilities ---");

  // Frisk
  const friskMon = createMon("FriskMon", "Frisk");
  await AbilityRegistry.trigger("Frisk", "onBattleStart", {
    owner: friskMon,
    battle: mockBattle,
  });
  // Expect text about Leftovers

  // Gluttony
  const gluttonyMon = createMon("Snorlax", "Gluttony");
  gluttonyMon.heldItem = "Sitrus Berry";
  gluttonyMon.currentHp = 40; // 40% < 50%
  await AbilityRegistry.trigger("Gluttony", "onTurnEnd", {
    owner: gluttonyMon,
    battle: mockBattle,
  });
  console.log(`Berry Eaten? ${gluttonyMon.heldItem} (Expected: undefined)`);
  console.log(`HP Restored? ${gluttonyMon.currentHp > 40} (Expected: true)`);

  // Harvest
  const harvestMon = createMon("Exeggutor", "Harvest");
  harvestMon.volatile["LastUsedBerry"] = "Sitrus Berry";
  mockBattle.weather.type = "Sun";
  await AbilityRegistry.trigger("Harvest", "onTurnEnd", {
    owner: harvestMon,
    battle: mockBattle,
  });
  console.log(
    `Berry Harvested? ${harvestMon.heldItem} (Expected: Sitrus Berry)`
  );
}

async function testTrapAbilities() {
  console.log("\n--- Testing Trap/Switch Abilities ---");

  // Regenerator
  const slowbro = createMon("Slowbro", "Regenerator");
  slowbro.currentHp = 10;
  await AbilityRegistry.trigger("Regenerator", "onSwitchOut", {
    owner: slowbro,
    battle: mockBattle,
  });
  console.log(`Regenerator HP: ${slowbro.currentHp} (Expected: ~43)`);

  // Wimp Out
  const wimpod = createMon("Wimpod", "Wimp Out");
  wimpod.currentHp = 40; // < 50%
  await AbilityRegistry.trigger("Wimp Out", "onAfterDamage", {
    owner: wimpod,
    battle: mockBattle,
  });
  // Expect text

  // Stakeout
  const gumshoos = createMon("Gumshoos", "Stakeout");
  const target = createMon("Target", "None");
  target.volatile["JustSwitchedIn"] = true;
  const atk = AbilityRegistry.applyModifier(
    gumshoos.ability,
    "onModifyAttack",
    100,
    { owner: gumshoos, target: target }
  );
  console.log(`Stakeout Damage: ${atk} (Expected: 200)`);

  // Zero to Hero
  const palafin = createMon("Palafin", "Zero to Hero");
  await AbilityRegistry.trigger("Zero to Hero", "onSwitchOut", {
    owner: palafin,
    battle: mockBattle,
  });
  console.log(`Hero Form? ${palafin.volatile["HeroForm"]} (Expected: 1)`);
}

async function run() {
  await testItemAbilities();
  await testTrapAbilities();
}

run();
