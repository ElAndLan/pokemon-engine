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

async function testGen9() {
  console.log("\n--- Testing Gen 9 Abilities ---");

  // As One (Glastrier)
  const calyrexG = createMon("Calyrex", "As One (Glastrier)");
  await AbilityRegistry.trigger("As One (Glastrier)", "onBattleStart", {
    owner: calyrexG,
    battle: mockBattle,
  });
  await AbilityRegistry.trigger("As One (Glastrier)", "onKOTarget", {
    owner: calyrexG,
    battle: mockBattle,
  });
  console.log(
    `Glastrier Attack Boost? ${calyrexG.statStages.attack} (Expected: 1)`
  );

  // As One (Spectrier)
  const calyrexS = createMon("Calyrex", "As One (Spectrier)");
  await AbilityRegistry.trigger("As One (Spectrier)", "onBattleStart", {
    owner: calyrexS,
    battle: mockBattle,
  });
  await AbilityRegistry.trigger("As One (Spectrier)", "onKOTarget", {
    owner: calyrexS,
    battle: mockBattle,
  });
  console.log(
    `Spectrier SpAtk Boost? ${calyrexS.statStages.spAttack} (Expected: 1)`
  );

  // Mind's Eye
  const ursa = createMon("Ursaluna", "Mind's Eye");
  const move = { type: "Normal", flags: {} } as any;
  // Mind's Eye uses onModifyType to set ignoreImmunity in the existing implementation
  AbilityRegistry.get("Mind's Eye")!.onModifyType!(move.type, {
    owner: ursa,
    move: move,
  });
  console.log(
    `Mind's Eye Ignore Immunity? ${move.flags.ignoreImmunity} (Expected: true)`
  );

  // Supersweet Syrup
  const dipplin = createMon("Dipplin", "Supersweet Syrup");
  await AbilityRegistry.trigger("Supersweet Syrup", "onBattleStart", {
    owner: dipplin,
    battle: mockBattle,
  });
  // Expect text

  // Hospitality
  const poltchageist = createMon("Poltchageist", "Hospitality");
  await AbilityRegistry.trigger("Hospitality", "onBattleStart", {
    owner: poltchageist,
    battle: mockBattle,
  });
  // Expect text (Placeholder)

  // Tera Shell
  const terapagos = createMon("Terapagos", "Tera Shell");
  const dmg = AbilityRegistry.applyModifier(
    terapagos.ability,
    "onDamageMultiplier",
    100,
    { owner: terapagos }
  );
  console.log(`Tera Shell Damage: ${dmg} (Expected: 50)`);

  terapagos.currentHp = 50;
  const dmg2 = AbilityRegistry.applyModifier(
    terapagos.ability,
    "onDamageMultiplier",
    100,
    { owner: terapagos }
  );
  console.log(`Tera Shell Low HP Damage: ${dmg2} (Expected: 100)`);
}

async function run() {
  await testGen9();
}

run();
