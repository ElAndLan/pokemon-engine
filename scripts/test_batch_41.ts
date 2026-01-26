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

async function testSpecialAbilities() {
  console.log("\n--- Testing Special Abilities ---");

  // Delta Stream
  const rayquaza = createMon("Rayquaza", "Delta Stream");
  await AbilityRegistry.trigger("Delta Stream", "onBattleStart", {
    owner: rayquaza,
    battle: mockBattle,
  });
  // Expect Weather set

  // Beast Boost
  const kartana = createMon("Kartana", "Beast Boost");
  kartana.currentStats.attack = 200; // Highest
  await AbilityRegistry.trigger("Beast Boost", "onKOTarget", {
    owner: kartana,
    battle: mockBattle,
  });
  console.log(`Attack Boosted? ${kartana.statStages.attack} (Expected: 1)`);

  // Full Metal Body
  const solgaleo = createMon("Solgaleo", "Full Metal Body");
  const attacker = createMon("Attacker", "Intimidate");
  const allowed = AbilityRegistry.get("Full Metal Body")!.onTryLowerStat!(
    { owner: solgaleo, target: attacker, battle: mockBattle },
    "attack"
  );
  console.log(`Stat Lower Allowed? ${allowed} (Expected: false)`);

  // Shadow Shield
  const lunala = createMon("Lunala", "Shadow Shield");
  const dmg = AbilityRegistry.applyModifier(
    lunala.ability,
    "onDamageMultiplier",
    100,
    { owner: lunala }
  );
  console.log(`Full HP Damage: ${dmg} (Expected: 50)`);

  lunala.currentHp = 50;
  const dmg2 = AbilityRegistry.applyModifier(
    lunala.ability,
    "onDamageMultiplier",
    100,
    { owner: lunala }
  );
  console.log(`Low HP Damage: ${dmg2} (Expected: 100)`);

  // Prism Armor
  const necrozma = createMon("Necrozma", "Prism Armor");
  const seDmg = AbilityRegistry.applyModifier(
    necrozma.ability,
    "onDamageMultiplier",
    100,
    { owner: necrozma, effectiveness: 2 }
  );
  console.log(`Super Effective Damage: ${seDmg} (Expected: 75)`);

  const normalDmg = AbilityRegistry.applyModifier(
    necrozma.ability,
    "onDamageMultiplier",
    100,
    { owner: necrozma, effectiveness: 1 }
  );
  console.log(`Normal Damage: ${normalDmg} (Expected: 100)`);
}

async function run() {
  await testSpecialAbilities();
}

run();
