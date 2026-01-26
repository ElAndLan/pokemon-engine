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
    return { nickname: "Opponent" };
  },
};

async function testPassiveEffects() {
  console.log("\n--- Testing Passive Effects ---");

  // Moody
  const bidoof = createMon("Bidoof", "Moody");
  await AbilityRegistry.trigger("Moody", "onTurnEnd", {
    owner: bidoof,
    battle: mockBattle,
  });
  // Expect 2 stats to change (one up 2, one down 1)
  const stages = Object.values(bidoof.statStages);
  const hasUp = stages.includes(2);
  const hasDown = stages.includes(-1);
  console.log(`Moody Boosted? ${hasUp} (Expected: true)`);
  console.log(`Moody Lowered? ${hasDown} (Expected: true)`);

  // Illusion
  const zoroark = createMon("Zoroark", "Illusion");
  zoroark.volatile["Illusion"] = 1;
  await AbilityRegistry.trigger("Illusion", "onAfterDamage", {
    owner: zoroark,
    battle: mockBattle,
  });
  console.log(
    `Illusion Broken? ${!zoroark.volatile["Illusion"]} (Expected: true)`
  );

  // Infiltrator
  const crobat = createMon("Crobat", "Infiltrator");
  const move = { flags: {} } as any;
  AbilityRegistry.get("Infiltrator")!.onModifyMove!(move, { owner: crobat });
  console.log(`Ignore Barriers? ${move.flags.ignoreBarriers} (Expected: true)`);
  console.log(
    `Bypass Substitute? ${move.flags.bypassSubstitute} (Expected: true)`
  );

  // Magic Bounce
  const espeon = createMon("Espeon", "Magic Bounce");
  const statusMove = { category: "Status", name: "Thunder Wave" } as any;
  const attacker = createMon("Attacker", "None");

  const allowed = AbilityRegistry.get("Magic Bounce")!.onTryHit!(
    { owner: espeon, target: attacker, move: statusMove, battle: mockBattle },
    []
  );
  console.log(`Status Move Allowed? ${allowed} (Expected: false)`);

  // Parental Bond
  const kanga = createMon("Kangaskhan", "Parental Bond");
  const punch = { category: "Physical" } as any;
  const hits = AbilityRegistry.applyModifier(
    kanga.ability,
    "onModifyMultiHit",
    1,
    { owner: kanga, move: punch }
  );
  console.log(`Parental Bond Hits: ${hits} (Expected: 2)`);

  // Dark Aura / Fairy Aura
  const yveltal = createMon("Yveltal", "Dark Aura");
  const darkMove = { type: "Dark" } as any;
  const boost = AbilityRegistry.applyModifier(
    yveltal.ability,
    "onModifyAttack",
    100,
    { owner: yveltal, move: darkMove }
  );
  console.log(`Dark Aura Boost: ${boost} (Expected: 133)`);

  const xerneas = createMon("Xerneas", "Fairy Aura");
  const fairyMove = { type: "Fairy" } as any;
  const boost2 = AbilityRegistry.applyModifier(
    xerneas.ability,
    "onModifyAttack",
    100,
    { owner: xerneas, move: fairyMove }
  );
  console.log(`Fairy Aura Boost: ${boost2} (Expected: 133)`);
}

async function run() {
  await testPassiveEffects();
}

run();
