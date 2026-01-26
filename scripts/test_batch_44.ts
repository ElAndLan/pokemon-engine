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
    side: "player",
  } as any;
}

const mockBattle: any = {
  showText: async (msg: string) => console.log(`[TEXT] ${msg}`),
  setWeather: (w: string, duration: number) =>
    console.log(`[WEATHER] Set to ${w}`),
};

async function testConquest() {
  console.log("\n--- Testing Conquest Abilities ---");

  // Mountaineer
  const onix = createMon("Onix", "Mountaineer");
  const rockThrow = { type: "Rock", name: "Rock Throw" } as any;
  const hit = AbilityRegistry.get("Mountaineer")!.onTryHit!(
    { owner: onix, move: rockThrow, battle: mockBattle },
    []
  );
  console.log(`Mountaineer Avoid Rock? ${!hit} (Expected: true)`);

  // Jagged Edge
  const rhyhorn = createMon("Rhyhorn", "Jagged Edge");
  const attacker = createMon("Attacker", "None");
  attacker.currentHp = 100;
  attacker.currentStats.hp = 100;
  const contactMove = { flags: { contact: true } } as any;

  await AbilityRegistry.trigger("Jagged Edge", "onAfterDamage", {
    owner: rhyhorn,
    target: attacker,
    move: contactMove,
    battle: mockBattle,
  });
  console.log(
    `Jagged Edge Recoil: ${100 - attacker.currentHp} (Expected: ~12)`
  );

  // Frostbite
  const sealeo = createMon("Sealeo", "Frostbite");
  const victim = createMon("Victim", "None");
  // Simulate until freeze (30% chance)
  let frozen = false;
  for (let i = 0; i < 20; i++) {
    victim.status = "None";
    await AbilityRegistry.trigger("Frostbite", "onAfterDamage", {
      owner: sealeo,
      target: victim,
      move: contactMove,
      battle: mockBattle,
    });
    if (victim.status === "Freeze") {
      frozen = true;
      break;
    }
  }
  console.log(`Frostbite Frozen? ${frozen} (Expected: true)`);

  // Perception
  const gardevoir = createMon("Gardevoir", "Perception");
  const ally = createMon("Ally", "None");
  ally.uuid = "ally-uuid";
  ally.side = "player"; // Same side

  // Test hit from ally
  const allyHit = AbilityRegistry.get("Perception")!.onTryHit!(
    { owner: gardevoir, target: ally, battle: mockBattle },
    []
  );
  console.log(
    `Perception Avoid Ally? ${!allyHit} (Expected: false -> Wait, implementation returns true currently?)`
  );
  // Wait, implementation: if (ally) return true; ... wait, onTryHit returns FALSE to block.
  // My implementation: if (ally) return true; which allows it.
  // I need to fix implementation to return false to BLOCK.

  // Lunchbox
  const snorlax = createMon("Snorlax", "Lunchbox");
  snorlax.currentHp = 50;
  await AbilityRegistry.trigger("Lunchbox", "onTurnEnd", {
    owner: snorlax,
    battle: mockBattle,
  });
  console.log(`Lunchbox Healed? ${snorlax.currentHp > 50} (Expected: true)`);

  // Vanguard
  const scyther = createMon("Scyther", "Vanguard");
  const slowpoke = createMon("Slowpoke", "None");
  scyther.currentStats.speed = 100;
  slowpoke.currentStats.speed = 10;

  const dmg = AbilityRegistry.applyModifier(
    scyther.ability,
    "onDamageMultiplier",
    100,
    { owner: scyther, target: slowpoke }
  );
  console.log(`Vanguard Damage: ${dmg} (Expected: 120)`);

  // Herbivore
  const sawsbuck = createMon("Sawsbuck", "Herbivore");
  sawsbuck.status = "Paralysis";
  sawsbuck.currentHp = 50;
  await AbilityRegistry.trigger("Herbivore", "onTurnEnd", {
    owner: sawsbuck,
    battle: mockBattle,
  });
  console.log(`Herbivore Healed? ${sawsbuck.currentHp > 50} (Expected: true)`);
}

async function run() {
  await testConquest();
}

run();
