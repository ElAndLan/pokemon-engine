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
  weather: { type: "None", turns: 0 },
  terrain: "None",
};

async function testForecast() {
  console.log("\n--- Testing Forecast ---");
  const castform = createMon("Castform", "Forecast");
  mockBattle.weather.type = "Sun";
  await AbilityRegistry.trigger("Forecast", "onTurnStart", {
    owner: castform,
    battle: mockBattle,
  });
  console.log(`Forecast Type (Sun): ${castform.types[0]} (Expected: Fire)`);

  mockBattle.weather.type = "Rain";
  await AbilityRegistry.trigger("Forecast", "onTurnStart", {
    owner: castform,
    battle: mockBattle,
  });
  console.log(`Forecast Type (Rain): ${castform.types[0]} (Expected: Water)`);
}

async function testSynchronize() {
  console.log("\n--- Testing Synchronize ---");
  const umbreon = createMon("Umbreon", "Synchronize");
  const attacker = createMon("Attacker", "None");

  // Simulate Umbreon getting poisoned by Attacker
  const ctx = { owner: umbreon, target: attacker, battle: mockBattle };
  const allowed = AbilityRegistry.get("Synchronize")!.onSetStatus!(
    ctx,
    "Poison"
  );
  console.log(`Allowed Status? ${allowed} (Expected: true)`);
  console.log(`Attacker Status: ${attacker.status} (Expected: Poison)`);
}

async function testShieldsDown() {
  console.log("\n--- Testing Shields Down ---");
  const minior = createMon("Minior", "Shields Down");
  // Start HP 100% -> Should be Meteor Form
  // But we init as "None". Logic: >50% and CoreForm -> Deactivate (become Meteor).
  // If <=50% and !CoreForm -> Activate (become Core).
  // Let's set CoreForm = 1 initially (simulate battle start/transform)
  minior.volatile["CoreForm"] = 1;

  // HP > 50%
  await AbilityRegistry.trigger("Shields Down", "onTurnEnd", {
    owner: minior,
    battle: mockBattle,
  });
  console.log(
    `Core Form Active? ${minior.volatile["CoreForm"]} (Expected: undefined)`
  ); // Should deactivate

  // Test Status Immunity (Meteor Form)
  const immune = !AbilityRegistry.get("Shields Down")!.onSetStatus!(
    { owner: minior, battle: mockBattle },
    "Burn"
  );
  console.log(`Meteor Form Immune? ${immune} (Expected: true)`);

  // HP <= 50%
  minior.currentHp = 40;
  await AbilityRegistry.trigger("Shields Down", "onTurnEnd", {
    owner: minior,
    battle: mockBattle,
  });
  console.log(`Core Form Active? ${minior.volatile["CoreForm"]} (Expected: 1)`); // Should activate

  // Test Status Susceptibility (Core Form)
  const immune2 = !AbilityRegistry.get("Shields Down")!.onSetStatus!(
    { owner: minior, battle: mockBattle },
    "Burn"
  );
  console.log(`Core Form Immune? ${immune2} (Expected: false)`);
}

async function testSweetVeil() {
  console.log("\n--- Testing Sweet Veil ---");
  const slurpuff = createMon("Slurpuff", "Sweet Veil");
  const sleepAllowed = AbilityRegistry.get("Sweet Veil")!.onSetStatus!(
    { owner: slurpuff, battle: mockBattle },
    "Sleep"
  );
  console.log(`Sleep Allowed? ${sleepAllowed} (Expected: false)`);

  const burnAllowed = AbilityRegistry.get("Sweet Veil")!.onSetStatus!(
    { owner: slurpuff, battle: mockBattle },
    "Burn"
  );
  console.log(`Burn Allowed? ${burnAllowed} (Expected: true)`);
}

async function testEarlyBird() {
  console.log("\n--- Testing Early Bird ---");
  const natu = createMon("Natu", "Early Bird");
  natu.status = "Sleep";
  natu.volatile["SleepTurns"] = 3;

  await AbilityRegistry.trigger("Early Bird", "onTurnEnd", {
    owner: natu,
    battle: mockBattle,
  });
  console.log(`Sleep Turns: ${natu.volatile["SleepTurns"]} (Expected: 2)`);
  // Decremented by 1 (Total 2 decrements if engine also decrements)
  // Here we just test the hook decrements.
}

async function run() {
  await testForecast();
  await testSynchronize();
  await testShieldsDown();
  await testSweetVeil();
  await testEarlyBird();
}

run();
