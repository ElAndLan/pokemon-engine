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

async function testBulletproof() {
  console.log("\n--- Testing Bulletproof ---");
  const chesnaught = createMon("Chesnaught", "Bulletproof");
  const ballMove: MoveData = { flags: { bullet: true } } as any;
  const events: any[] = [];

  const allowed = AbilityRegistry.get("Bulletproof")!.onTryHit!(
    { owner: chesnaught, move: ballMove, battle: mockBattle },
    events
  );
  console.log(`Bullet Move Allowed? ${allowed} (Expected: false)`);
}

async function testPerishBody() {
  console.log("\n--- Testing Perish Body ---");
  const cursola = createMon("Cursola", "Perish Body");
  const attacker = createMon("Attacker", "None");

  const ctx = {
    owner: cursola,
    target: attacker,
    battle: mockBattle,
    move: { flags: { contact: true } } as any,
  };
  await AbilityRegistry.trigger("Perish Body", "onAfterDamage", ctx);

  console.log(`Owner Perish? ${cursola.volatile["PerishSong"]} (Expected: 4)`);
  console.log(
    `Attacker Perish? ${attacker.volatile["PerishSong"]} (Expected: 4)`
  );
}

async function testWanderingSpirit() {
  console.log("\n--- Testing Wandering Spirit ---");
  const runerigus = createMon("Runerigus", "Wandering Spirit");
  const attacker = createMon("Attacker", "Blaze");

  const ctx = {
    owner: runerigus,
    target: attacker,
    battle: mockBattle,
    move: { flags: { contact: true } } as any,
  };
  await AbilityRegistry.trigger("Wandering Spirit", "onAfterDamage", ctx);

  console.log(`Runerigus Ability: ${runerigus.ability} (Expected: Blaze)`);
  console.log(
    `Attacker Ability: ${attacker.ability} (Expected: Wandering Spirit)`
  );
}

async function testMirrorArmor() {
  console.log("\n--- Testing Mirror Armor ---");
  const corviknight = createMon("Corviknight", "Mirror Armor");
  const attacker = createMon("Attacker", "Intimidate");

  // Attacker tries to lower Corviknight's Defense
  const ctx = { owner: corviknight, target: attacker, battle: mockBattle };
  const allowed = AbilityRegistry.get("Mirror Armor")!.onTryLowerStat!(
    ctx,
    "defense"
  );

  console.log(`Stat Lower Allowed? ${allowed} (Expected: false)`);
  console.log(
    `Attacker Defense Stage: ${attacker.statStages.defense} (Expected: -1)`
  );
  // Note: AtomicEffects applies stat change immediately inside hook in our impl.
}

async function testTypeBoosts() {
  console.log("\n--- Testing Type Boosts (Dragon/Steel/Electric) ---");
  const regidrago = createMon("Regidrago", "Dragon's Maw");
  const atk1 = AbilityRegistry.applyModifier(
    regidrago.ability,
    "onModifyAttack",
    100,
    { owner: regidrago, move: { type: "Dragon" } as any }
  );
  console.log(`Dragon's Maw: ${atk1} (Expected: 150)`);

  const regieleki = createMon("Regieleki", "Transistor");
  const atk2 = AbilityRegistry.applyModifier(
    regieleki.ability,
    "onModifyAttack",
    100,
    { owner: regieleki, move: { type: "Electric" } as any }
  );
  console.log(`Transistor: ${atk2} (Expected: 130)`);
}

async function run() {
  await testBulletproof();
  await testPerishBody();
  await testWanderingSpirit();
  await testMirrorArmor();
  await testTypeBoosts();
}

run();
