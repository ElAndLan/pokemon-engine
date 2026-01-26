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

async function testSoulHeart() {
  console.log("\n--- Testing Soul-Heart ---");
  const magearna = createMon("Magearna", "Soul-Heart");
  // Faint someone
  const ctx = { owner: magearna, battle: mockBattle };
  const events = AbilityRegistry.get("Soul-Heart")!.onFaint!(
    ctx,
    createMon("Victim", "None")
  );

  // Should return stat boost event
  const boost = events.find(
    (e) => e.type === "StatChange" && e.value.stat === "spAttack"
  );
  console.log(`Boosts SpAtk? ${!!boost} (Expected: true)`);
}

async function testLingeringAroma() {
  console.log("\n--- Testing Lingering Aroma ---");
  const oink = createMon("Oinkologne", "Lingering Aroma");
  const attacker = createMon("Attacker", "Blaze");

  const ctx = {
    owner: oink,
    target: attacker,
    battle: mockBattle,
    move: { flags: { contact: true } } as any,
  };
  await AbilityRegistry.trigger("Lingering Aroma", "onAfterDamage", ctx);

  console.log(
    `Attacker Ability: ${attacker.ability} (Expected: Lingering Aroma)`
  );
}

async function testDamp() {
  console.log("\n--- Testing Damp ---");
  const politoed = createMon("Politoed", "Damp");
  const events: any[] = [];

  const explode = { name: "Explosion" } as any;
  const allowed = AbilityRegistry.get("Damp")!.onTryHit!(
    { owner: politoed, move: explode, battle: mockBattle },
    events
  );
  console.log(`Explosion Allowed? ${allowed} (Expected: false)`);
}

async function testOblivious() {
  console.log("\n--- Testing Oblivious ---");
  const slowpoke = createMon("Slowpoke", "Oblivious");
  const events: any[] = [];

  const attract = { name: "Attract" } as any;
  const allowed = AbilityRegistry.get("Oblivious")!.onTryHit!(
    { owner: slowpoke, move: attract, battle: mockBattle },
    events
  );
  console.log(`Attract Allowed? ${allowed} (Expected: false)`);
}

async function testSuctionCups() {
  console.log("\n--- Testing Suction Cups ---");
  const octillery = createMon("Octillery", "Suction Cups");
  const events: any[] = [];

  const roar = { name: "Roar", category: "Status" } as any;
  const allowed = AbilityRegistry.get("Suction Cups")!.onTryHit!(
    { owner: octillery, move: roar, battle: mockBattle },
    events
  );
  console.log(`Roar Allowed? ${allowed} (Expected: false)`);
}

async function testStickyHold() {
  console.log("\n--- Testing Sticky Hold ---");
  const muk = createMon("Muk", "Sticky Hold");
  const events: any[] = [];

  const trick = { name: "Trick", category: "Status" } as any;
  const allowed = AbilityRegistry.get("Sticky Hold")!.onTryHit!(
    { owner: muk, move: trick, battle: mockBattle },
    events
  );
  console.log(`Trick Allowed? ${allowed} (Expected: false)`);
}

async function run() {
  await testSoulHeart();
  await testLingeringAroma();
  await testDamp();
  await testOblivious();
  await testSuctionCups();
  await testStickyHold();
}

run();
