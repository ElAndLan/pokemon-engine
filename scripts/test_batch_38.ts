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

async function testFormChanges() {
  console.log("\n--- Testing Form Change Abilities ---");

  // Imposter
  const ditto = createMon("Ditto", "Imposter");
  await AbilityRegistry.trigger("Imposter", "onBattleStart", {
    owner: ditto,
    battle: mockBattle,
  });
  // Expect text about transform

  // Zen Mode
  const darmanitan = createMon("Darmanitan", "Zen Mode");
  darmanitan.currentHp = 40; // < 50%
  await AbilityRegistry.trigger("Zen Mode", "onTurnEnd", {
    owner: darmanitan,
    battle: mockBattle,
  });
  console.log(
    `Zen Mode Active? ${darmanitan.volatile["ZenMode"]} (Expected: 1)`
  );

  // Stance Change
  const aegislash = createMon("Aegislash", "Stance Change");
  const attackMove = { category: "Physical", name: "Iron Head" } as any;
  const shieldMove = { category: "Status", name: "King's Shield" } as any;

  await AbilityRegistry.get("Stance Change")!.onBeforeMove!({
    owner: aegislash,
    move: attackMove,
    battle: mockBattle,
  });
  console.log(`Blade Forme? ${aegislash.volatile["BladeForme"]} (Expected: 1)`);

  await AbilityRegistry.get("Stance Change")!.onBeforeMove!({
    owner: aegislash,
    move: shieldMove,
    battle: mockBattle,
  });
  console.log(
    `Blade Forme? ${aegislash.volatile["BladeForme"]} (Expected: undefined)`
  );

  // Schooling
  const wishiwashi = createMon("Wishiwashi", "Schooling");
  wishiwashi.currentHp = 100; // > 25%
  await AbilityRegistry.trigger("Schooling", "onBattleStart", {
    owner: wishiwashi,
    battle: mockBattle,
  });
  console.log(
    `Schooling Active? ${wishiwashi.volatile["Schooling"]} (Expected: 1)`
  );

  // Battle Bond
  const greninja = createMon("Greninja", "Battle Bond");
  await AbilityRegistry.trigger("Battle Bond", "onKOTarget", {
    owner: greninja,
    battle: mockBattle,
  });
  console.log(
    `Ash Greninja? ${greninja.volatile["AshGreninja"]} (Expected: 1)`
  );

  const shuriken = { name: "Water Shuriken", power: 15 } as any;
  AbilityRegistry.get("Battle Bond")!.onModifyMove!(shuriken, {
    owner: greninja,
  });
  console.log(`Shuriken Power: ${shuriken.power} (Expected: 20)`);

  // Hunger Switch
  const morpeko = createMon("Morpeko", "Hunger Switch");
  await AbilityRegistry.trigger("Hunger Switch", "onTurnEnd", {
    owner: morpeko,
    battle: mockBattle,
  });
  console.log(`Hangry Mode? ${morpeko.volatile["HangryMode"]} (Expected: 1)`);
  await AbilityRegistry.trigger("Hunger Switch", "onTurnEnd", {
    owner: morpeko,
    battle: mockBattle,
  });
  console.log(
    `Hangry Mode? ${morpeko.volatile["HangryMode"]} (Expected: undefined)`
  );

  // Gulp Missile
  const cramorant = createMon("Cramorant", "Gulp Missile");
  cramorant.volatile["GulpPrey"] = 1;
  const attacker = createMon("Attacker", "None");
  attacker.currentHp = 100;

  await AbilityRegistry.trigger("Gulp Missile", "onAfterDamage", {
    owner: cramorant,
    target: attacker,
    battle: mockBattle,
  });
  console.log(`Prey Spat? ${!cramorant.volatile["GulpPrey"]} (Expected: true)`);
  console.log(`Attacker Damaged? ${attacker.currentHp < 100} (Expected: true)`);
}

async function run() {
  await testFormChanges();
}

run();
