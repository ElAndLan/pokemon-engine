import {
  PokemonInstance,
  MoveData,
} from "../src/renderer/src/core/data/DataTypes";
import { AbilityRegistry } from "../src/renderer/src/core/battle/Abilities";
import { MoveEngine } from "../src/renderer/src/core/battle/MoveEngine";
import { CoreMoveLogic } from "../src/renderer/src/core/battle/CoreMoveLogic";

// Mock Pokemon Generator
function createMon(
  name: string,
  ability: string,
  hp: number = 100
): PokemonInstance {
  return {
    uuid: Math.random().toString(),
    speciesId: name.toLowerCase(),
    nickname: name,
    level: 50,
    ability: ability,
    currentHp: hp,
    currentStats: {
      hp,
      attack: 50,
      defense: 50,
      spAttack: 50,
      spDefense: 50,
      speed: 50,
    },
    statStages: { accuracy: 0, evasion: 0 },
    moves: [],
    types: ["Normal"],
    status: "None",
    volatile: {},
  } as any;
}

// Mock Battle Context
const mockBattle: any = {
  showText: async (msg: string) => console.log(`[TEXT] ${msg}`),
  game: {
    weatherManager: {
      currentWeather: "None",
    },
  },
};

async function testVitalSpirit() {
  console.log("\n--- Testing Vital Spirit ---");
  const mon = createMon("Mankey", "Vital Spirit");
  const ctx = { owner: mon, battle: mockBattle };

  // Test 1: Try to set Sleep
  console.log("Attempting to set Sleep...");
  const canSleep = AbilityRegistry.trigger(
    mon.ability,
    "onSetStatus",
    ctx,
    "Sleep"
  );
  // Note: trigger returns Promise<any> usually, but onSetStatus returns boolean directly in interface?
  // AbilityRegistry.trigger is async and returns Promise.
  // BUT AbilityRegistry.trigger implementation:
  // static async trigger(...) { ... return await ability[hook](...); }
  // If the hook is synchronous (returns boolean), it returns Promise<boolean>.
  // Wait, onSetStatus in interface returns boolean (not Promise).
  // So trigger will return Promise<boolean>.

  // However, AbilityRegistry.trigger might be designed for async hooks.
  // Let's check AbilityRegistry.trigger implementation in my memory or file.
  // It awaits the result.
  // So if I call it, I get a Promise.

  // But onSetStatus is used synchronously in AtomicEffects usually?
  // Let's check `Abilities.ts` interface.
  // onSetStatus?: (ctx: AbilityContext, status: string) => boolean;
  // It is synchronous.
  // So I should call it directly via AbilityRegistry.get(id).onSetStatus(...) if I want sync result,
  // OR use a sync trigger wrapper if it exists.
  // AbilityRegistry has `applyModifier` (sync) and `trigger` (async).
  // It does NOT have a sync trigger for boolean checks.
  // I'll access the ability directly for testing to be sure.

  const ability = AbilityRegistry.get(mon.ability);
  if (ability && ability.onSetStatus) {
    const result = ability.onSetStatus(ctx, "Sleep");
    console.log(`Can Sleep? ${result} (Expected: false)`);
    if (result === false) console.log("PASS");
    else console.log("FAIL");
  } else {
    console.log("FAIL: Ability not found or hook missing");
  }
}

async function testLeafGuard() {
  console.log("\n--- Testing Leaf Guard ---");
  const mon = createMon("Leafeon", "Leaf Guard");
  const ctx = { owner: mon, battle: mockBattle };

  // 1. No Weather
  mockBattle.game.weatherManager.currentWeather = "None";
  let ability = AbilityRegistry.get(mon.ability)!;
  let result = ability.onSetStatus ? ability.onSetStatus(ctx, "Poison") : true;
  console.log(`No Weather: Can Poison? ${result} (Expected: true)`);

  // 2. Sun
  mockBattle.game.weatherManager.currentWeather = "Sun";
  result = ability.onSetStatus ? ability.onSetStatus(ctx, "Poison") : true;
  console.log(`Sun: Can Poison? ${result} (Expected: false)`);
}

async function testHydration() {
  console.log("\n--- Testing Hydration ---");
  const mon = createMon("Vaporeon", "Hydration");
  mon.status = "Poison";
  const ctx = { owner: mon, battle: mockBattle };

  // 1. Rain
  mockBattle.game.weatherManager.currentWeather = "Rain";
  const ability = AbilityRegistry.get(mon.ability)!;
  if (ability.onTurnEnd) {
    await ability.onTurnEnd(ctx);
    console.log(`Status after Rain Turn End: ${mon.status} (Expected: None)`);
  }
}

async function testDrySkin() {
  console.log("\n--- Testing Dry Skin ---");
  const mon = createMon("Toxicroak", "Dry Skin", 50); // 50 HP
  mon.currentStats.hp = 100; // Max HP 100
  const ctx = { owner: mon, battle: mockBattle };
  const ability = AbilityRegistry.get(mon.ability)!;

  // 1. Hit by Water
  console.log("1. Hit by Water Move");
  const waterMove: MoveData = {
    name: "Water Gun",
    type: "Water",
    category: "Special",
    power: 40,
    accuracy: 100,
  } as any;
  const hitCtx = { owner: mon, move: waterMove, battle: mockBattle };
  const events: any[] = [];
  const allowHit = ability.onTryHit ? ability.onTryHit(hitCtx, events) : true;
  console.log(`Allow Hit? ${allowHit} (Expected: false)`);
  console.log(`HP: ${mon.currentHp} (Expected > 50)`);

  // 2. Hit by Fire (Damage Multiplier)
  console.log("2. Hit by Fire Move");
  const fireMove: MoveData = {
    name: "Ember",
    type: "Fire",
    category: "Special",
    power: 40,
    accuracy: 100,
  } as any;
  const multCtx = { owner: mon, move: fireMove, battle: mockBattle };
  const mult = ability.onDamageMultiplier
    ? ability.onDamageMultiplier(100, multCtx)
    : 100;
  console.log(`Damage Multiplier: ${mult} (Expected: 125)`);

  // 3. Rain Turn End
  console.log("3. Rain Turn End");
  mon.currentHp = 50;
  mockBattle.game.weatherManager.currentWeather = "Rain";
  if (ability.onTurnEnd) await ability.onTurnEnd(ctx);
  console.log(`HP after Rain: ${mon.currentHp} (Expected > 50)`);

  // 4. Sun Turn End
  console.log("4. Sun Turn End");
  mon.currentHp = 50;
  mockBattle.game.weatherManager.currentWeather = "Sun";
  if (ability.onTurnEnd) await ability.onTurnEnd(ctx);
  console.log(`HP after Sun: ${mon.currentHp} (Expected < 50)`);
}

async function testEvasion() {
  console.log("\n--- Testing Sand Veil / Evasion Hook ---");
  const defender = createMon("Garchomp", "Sand Veil");
  const attacker = createMon("Opponent", "None");
  const move: MoveData = {
    name: "Tackle",
    type: "Normal",
    category: "Physical",
    power: 40,
    accuracy: 100,
  } as any;

  // 1. Sandstorm
  mockBattle.game.weatherManager.currentWeather = "Sandstorm";

  // We can't easily check checkHit randomness, but we can check the hook return value
  const ability = AbilityRegistry.get(defender.ability)!;
  const evasionMod = ability.onModifyEvasion
    ? ability.onModifyEvasion(1.0, {
        owner: defender,
        variables: { weather: "Sandstorm" },
      } as any)
    : 1.0;
  console.log(
    `Sand Veil Evasion Mod (Sandstorm): ${evasionMod} (Expected: 1.25)`
  );

  // Verify CoreMoveLogic uses it
  // We'll run checkHit 1000 times and see hit rate?
  // Base 100 accuracy.
  // With 1.25 evasion -> 100 / 1.25 = 80% accuracy.

  let hits = 0;
  const trials = 1000;
  for (let i = 0; i < trials; i++) {
    if (CoreMoveLogic.checkHit(attacker, defender, move, "Sandstorm")) {
      hits++;
    }
  }
  console.log(`Hit Rate in Sandstorm: ${hits}/${trials} (Expected ~800)`);

  // 2. No Weather
  mockBattle.game.weatherManager.currentWeather = "None";
  const evasionModNone = ability.onModifyEvasion
    ? ability.onModifyEvasion(1.0, {
        owner: defender,
        variables: { weather: "None" },
      } as any)
    : 1.0;
  console.log(
    `Sand Veil Evasion Mod (None): ${evasionModNone} (Expected: 1.0)`
  );

  hits = 0;
  for (let i = 0; i < trials; i++) {
    if (CoreMoveLogic.checkHit(attacker, defender, move, "None")) {
      hits++;
    }
  }
  console.log(`Hit Rate in None: ${hits}/${trials} (Expected ~1000)`);
}

async function run() {
  await testVitalSpirit();
  await testLeafGuard();
  await testHydration();
  await testDrySkin();
  await testEvasion();
}

run();
