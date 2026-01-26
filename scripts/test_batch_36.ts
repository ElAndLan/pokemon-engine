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
};

async function testProtection() {
  console.log("\n--- Testing Protection Abilities ---");

  // Aroma Veil
  const spritzee = createMon("Spritzee", "Aroma Veil");
  const taunt = { name: "Taunt" } as any;
  const allowed = AbilityRegistry.get("Aroma Veil")!.onTryHit!(
    { owner: spritzee, move: taunt, battle: mockBattle },
    []
  );
  console.log(`Taunt Allowed? ${allowed} (Expected: false)`);

  // Flower Veil
  const flabebe = createMon("Flabebe", "Flower Veil");
  flabebe.types = ["Fairy", "Grass"]; // Needs Grass type for self-protection logic
  const statAllowed = AbilityRegistry.get("Flower Veil")!.onTryLowerStat!(
    { owner: flabebe, battle: mockBattle },
    "defense"
  );
  console.log(`Stat Drop Allowed? ${statAllowed} (Expected: false)`);

  // Disguise
  const mimikyu = createMon("Mimikyu", "Disguise");
  const hit = { category: "Physical" } as any;
  const hitAllowed = AbilityRegistry.get("Disguise")!.onTryHit!(
    { owner: mimikyu, move: hit, battle: mockBattle },
    []
  );
  console.log(`First Hit Allowed? ${hitAllowed} (Expected: false)`);
  console.log(`Busted? ${mimikyu.volatile["Busted"]} (Expected: 1)`);
  console.log(`HP Reduced? ${mimikyu.currentHp < 100} (Expected: true)`);
}

async function testBypass() {
  console.log("\n--- Testing Bypass Abilities ---");

  // Turboblaze
  const reshiram = createMon("Reshiram", "Turboblaze");
  const move = { flags: {} } as any;
  AbilityRegistry.get("Turboblaze")!.onModifyMove!(move, { owner: reshiram });
  console.log(`Ignores Ability? ${move.flags.ignoreAbility} (Expected: true)`);

  // Unseen Fist
  const urshifu = createMon("Urshifu", "Unseen Fist");
  const contactMove = { flags: { contact: true } } as any;
  AbilityRegistry.get("Unseen Fist")!.onModifyMove!(contactMove, {
    owner: urshifu,
  });
  console.log(
    `Bypasses Protect? ${contactMove.flags.bypassProtect} (Expected: true)`
  );
}

async function run() {
  await testProtection();
  await testBypass();
}

run();
