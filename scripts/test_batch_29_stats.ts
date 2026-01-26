import {
  PokemonInstance,
  MoveData,
} from "../src/renderer/src/core/data/DataTypes";
import { AbilityRegistry } from "../src/renderer/src/core/battle/Abilities";
import { AtomicEffects } from "../src/renderer/src/core/battle/AtomicEffects";
import { MoveEngine } from "../src/renderer/src/core/battle/MoveEngine";

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

async function testSlowStart() {
  console.log("\n--- Testing Slow Start ---");
  const regigigas = createMon("Regigigas", "Slow Start");
  const ctx = { owner: regigigas, battle: mockBattle };

  await AbilityRegistry.trigger("Slow Start", "onBattleStart", ctx);
  console.log(
    `Slow Start Active? ${regigigas.volatile["SlowStart"] > 0} (Expected: true)`
  );

  const atk = AbilityRegistry.applyModifier(
    regigigas.ability,
    "onModifyAttack",
    100,
    { owner: regigigas }
  );
  console.log(`Attack (Start): ${atk} (Expected: 50)`);

  // Simulate 5 turns
  for (let i = 0; i < 5; i++) {
    await AbilityRegistry.trigger("Slow Start", "onTurnEnd", ctx);
  }
  console.log(
    `Slow Start Ended? ${
      regigigas.volatile["SlowStart"] === 0
    } (Expected: true)`
  );
  const atkEnd = AbilityRegistry.applyModifier(
    regigigas.ability,
    "onModifyAttack",
    100,
    { owner: regigigas }
  );
  console.log(`Attack (End): ${atkEnd} (Expected: 100)`);
}

async function testDefeatist() {
  console.log("\n--- Testing Defeatist ---");
  const archeops = createMon("Archeops", "Defeatist");
  archeops.currentHp = 40; // < 50%
  const atk = AbilityRegistry.applyModifier(
    archeops.ability,
    "onModifyAttack",
    100,
    { owner: archeops }
  );
  console.log(`Attack (<50%): ${atk} (Expected: 50)`);

  archeops.currentHp = 80;
  const atk2 = AbilityRegistry.applyModifier(
    archeops.ability,
    "onModifyAttack",
    100,
    { owner: archeops }
  );
  console.log(`Attack (>50%): ${atk2} (Expected: 100)`);
}

async function testGorillaTactics() {
  console.log("\n--- Testing Gorilla Tactics ---");
  const darmanitan = createMon("Darmanitan", "Gorilla Tactics");
  const physMove: MoveData = { category: "Physical" } as any;
  const specMove: MoveData = { category: "Special" } as any;

  const atk = AbilityRegistry.applyModifier(
    darmanitan.ability,
    "onModifyAttack",
    100,
    { owner: darmanitan, move: physMove }
  );
  console.log(`Attack (Physical): ${atk} (Expected: 150)`);

  const atk2 = AbilityRegistry.applyModifier(
    darmanitan.ability,
    "onModifyAttack",
    100,
    { owner: darmanitan, move: specMove }
  );
  console.log(`Attack (Special): ${atk2} (Expected: 100)`);
}

async function testSteamEngine() {
  console.log("\n--- Testing Steam Engine ---");
  const coalossal = createMon("Coalossal", "Steam Engine");
  const fireMove: MoveData = { type: "Fire" } as any;
  const ctx = { owner: coalossal, move: fireMove, battle: mockBattle };

  if (AbilityRegistry.get("Steam Engine")?.onAfterDamage) {
    await AbilityRegistry.get("Steam Engine")!.onAfterDamage!(ctx, 10);
  }
  console.log(`Speed Stage: ${coalossal.statStages.speed} (Expected: 6)`);
}

async function testAngerShell() {
  console.log("\n--- Testing Anger Shell ---");
  const klawf = createMon("Klawf", "Anger Shell");
  // Start full HP
  klawf.currentHp = 100;

  // Damage to 40 (below half)
  const damage = 60;
  // We update HP BEFORE calling hook in real engine?
  // MoveEngine updates HP then calls hooks.
  // So currentHp should be 40.
  // And damage was 60.
  // Logic: if current <= 50% AND (current + damage) > 50%.
  klawf.currentHp = 40;

  const ctx = { owner: klawf, battle: mockBattle };
  if (AbilityRegistry.get("Anger Shell")?.onAfterDamage) {
    await AbilityRegistry.get("Anger Shell")!.onAfterDamage!(ctx, damage);
  }

  console.log(`Attack Stage: ${klawf.statStages.attack} (Expected: 1)`);
  console.log(`Defense Stage: ${klawf.statStages.defense} (Expected: -1)`);
}

async function testWindRider() {
  console.log("\n--- Testing Wind Rider ---");
  const brambleghast = createMon("Brambleghast", "Wind Rider");
  const windMove: MoveData = { flags: { wind: true } } as any;
  const events: any[] = [];

  const hit = AbilityRegistry.get("Wind Rider")!.onTryHit!(
    { owner: brambleghast, move: windMove, battle: mockBattle },
    events
  );
  console.log(`Hit Allowed? ${hit} (Expected: false)`);

  // Check for StatChange event
  const statChange = events.find(
    (e) => e.type === "StatChange" && e.value.stat === "attack"
  );
  console.log(`Attack Boost Event? ${!!statChange} (Expected: true)`);

  // Note: AtomicEffects.applyStatChange in hook pushed events, but did NOT apply changes to dummy object if it returns events?
  // Wait, AtomicEffects.applyStatChange applies changes AND returns events.
  // Let's verify stat stage.
  console.log(`Attack Stage: ${brambleghast.statStages.attack} (Expected: 1)`);
}

async function testKOBonuses() {
  console.log("\n--- Testing KO Bonuses (Moxie/Neigh) ---");

  // Moxie
  const krookodile = createMon("Krookodile", "Moxie");
  const victim = createMon("Victim", "None");
  victim.currentHp = 0; // KO

  const move: MoveData = { power: 50, category: "Physical" } as any;

  // We can simulate KO check directly or use MoveEngine if we mock everything.
  // Let's call the hook directly to verify logic.
  const ctx = { owner: krookodile, target: victim, battle: mockBattle };
  await AbilityRegistry.trigger("Moxie", "onKOTarget", ctx);

  console.log(
    `Moxie Attack Stage: ${krookodile.statStages.attack} (Expected: 1)`
  );

  // Grim Neigh
  const spectrier = createMon("Spectrier", "Grim Neigh");
  const ctx2 = { owner: spectrier, target: victim, battle: mockBattle };
  await AbilityRegistry.trigger("Grim Neigh", "onKOTarget", ctx2);

  console.log(
    `Grim Neigh SpAttack Stage: ${spectrier.statStages.spAttack} (Expected: 1)`
  );
}

async function run() {
  await testSlowStart();
  await testDefeatist();
  await testGorillaTactics();
  await testSteamEngine();
  await testAngerShell();
  await testWindRider();
  await testKOBonuses();
}

run();
