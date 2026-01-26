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
  setWeather: (w: string, duration: number) =>
    console.log(`[WEATHER] Set to ${w}`),
};

async function testGen78() {
  console.log("\n--- Testing Gen 7/8 Abilities ---");

  // Neuroforce
  const necrozma = createMon("Necrozma", "Neuroforce");
  const dmg = AbilityRegistry.applyModifier(
    necrozma.ability,
    "onDamageMultiplier",
    100,
    { owner: necrozma, effectiveness: 2 }
  );
  console.log(`Neuroforce Damage: ${dmg} (Expected: 125)`);

  // Propeller Tail / Stalwart
  const barraskewda = createMon("Barraskewda", "Propeller Tail");
  const move = { flags: {} } as any;
  AbilityRegistry.get("Propeller Tail")!.onModifyMove!(move, {
    owner: barraskewda,
  });
  console.log(
    `Propeller Tail Ignore Redirect? ${move.flags.ignoreRedirection} (Expected: true)`
  );

  // Screen Cleaner
  const mime = createMon("Mr. Mime", "Screen Cleaner");
  await AbilityRegistry.trigger("Screen Cleaner", "onBattleStart", {
    owner: mime,
    battle: mockBattle,
  });
  // Expect text about barriers

  // Neutralizing Gas
  const weezing = createMon("Weezing", "Neutralizing Gas");
  await AbilityRegistry.trigger("Neutralizing Gas", "onBattleStart", {
    owner: weezing,
    battle: mockBattle,
  });
  // Expect text about gas

  // Quick Draw
  const slowbro = createMon("Slowbro", "Quick Draw");
  let p = 0;
  // Simulate multiple calls to trigger 30%
  for (let i = 0; i < 20; i++) {
    const newP = AbilityRegistry.applyModifier(
      slowbro.ability,
      "onModifyPriority",
      0,
      { owner: slowbro, battle: mockBattle }
    );
    if (newP > 0) {
      p = newP;
      break;
    }
  }
  console.log(`Quick Draw Priority: ${p} (Expected: > 0 with high prob)`);

  // Curious Medicine
  const slowking = createMon("Slowking", "Curious Medicine");
  await AbilityRegistry.trigger("Curious Medicine", "onBattleStart", {
    owner: slowking,
    battle: mockBattle,
  });
  // Expect text
}

async function run() {
  await testGen78();
}

run();
