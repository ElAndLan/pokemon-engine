import { BattleScene } from "../src/renderer/src/core/battle/BattleScene";
import {
  PokemonInstance,
  MoveData,
} from "../src/renderer/src/core/data/DataTypes";
import { MoveEngine } from "../src/renderer/src/core/battle/MoveEngine";
import { AbilityRegistry } from "../src/renderer/src/core/battle/Abilities";

// Setup Mocks
const mockGame = {
  display: {},
  weatherManager: { currentWeather: "None" },
  dataManager: {
    getAllMoves: () => [],
    getMove: (id) => ({ id, name: id, priority: 0 }),
    getItem: (id) => ({ id, name: id }),
  },
} as any;

const battleScene = new BattleScene(mockGame);

// Pokemon
const shedinja: PokemonInstance = {
  uuid: "p1",
  speciesId: "shedinja",
  nickname: "Shedinja",
  types: ["Bug", "Ghost"],
  currentStats: {
    hp: 1,
    attack: 100,
    defense: 100,
    spAttack: 100,
    spDefense: 100,
    speed: 100,
  },
  currentHp: 1,
  ability: "Wonder Guard",
  level: 50,
  ivs: {},
  evs: {},
  moves: [],
  status: "None",
  volatile: {},
  originalTrainer: "",
  statStages: {},
} as any;

const jolteon: PokemonInstance = {
  uuid: "p2",
  speciesId: "jolteon",
  nickname: "Jolteon",
  types: ["Electric"],
  currentStats: {
    hp: 100,
    attack: 100,
    defense: 100,
    spAttack: 100,
    spDefense: 100,
    speed: 100,
  },
  currentHp: 50, // Hurt
  ability: "Volt Absorb",
  level: 50,
  ivs: {},
  evs: {},
  moves: [],
  status: "None",
  volatile: {},
  originalTrainer: "",
  statStages: {},
} as any;

const arcanine: PokemonInstance = {
  uuid: "p3",
  speciesId: "arcanine",
  nickname: "Arcanine",
  types: ["Fire"],
  currentStats: {
    hp: 100,
    attack: 100,
    defense: 100,
    spAttack: 100,
    spDefense: 100,
    speed: 100,
  },
  currentHp: 100,
  ability: "Flash Fire",
  level: 50,
  ivs: {},
  evs: {},
  moves: [],
  status: "None",
  volatile: {},
  originalTrainer: "",
  statStages: {},
} as any;

// Moves
const scratch: MoveData = {
  id: "scratch",
  name: "Scratch",
  type: "Normal",
  category: "Physical",
  power: 40,
  accuracy: 100,
  target: "SelectedEnemy",
  effects: [{ type: "Damage" }],
  pp: 35,
  priority: 0,
  description: "Scratches the foe.",
};
const flamethrower: MoveData = {
  id: "flamethrower",
  name: "Flamethrower",
  type: "Fire",
  category: "Special",
  power: 90,
  accuracy: 100,
  target: "SelectedEnemy",
  effects: [{ type: "Damage" }],
  pp: 15,
  priority: 0,
  description: "Burns the foe.",
};
const thunderbolt: MoveData = {
  id: "thunderbolt",
  name: "Thunderbolt",
  type: "Electric",
  category: "Special",
  power: 90,
  accuracy: 100,
  target: "SelectedEnemy",
  effects: [{ type: "Damage" }],
  pp: 15,
  priority: 0,
  description: "Zaps the foe.",
};

async function runTests() {
  console.log("--- IMMUNITY TESTS ---");

  console.log("\n1. Wonder Guard (Shedinja)");
  // Hit with Normal (Immune)
  console.log("- Hit with Scratch (Normal vs Bug/Ghost) -> Immune?");
  const res1 = await MoveEngine.executeMove(jolteon, shedinja, scratch);
  const hit1 = res1.events.find((e) => e.type === "Damage");
  console.log(`Damage Event: ${!!hit1} (Expected false)`);

  // Hit with Fire (Super Effective) -> Hit
  console.log("- Hit with Flamethrower (Fire vs Bug/Ghost) -> Hit?");
  const res2 = await MoveEngine.executeMove(arcanine, shedinja, flamethrower);
  const hit2 = res2.events.find((e) => e.type === "Damage");
  console.log(`Damage Event: ${!!hit2} (Expected true)`);

  console.log("\n2. Volt Absorb (Jolteon)");
  // Hit with Thunderbolt -> Heal
  console.log("- Hit with Thunderbolt -> Heal?");
  const hpBefore = jolteon.currentHp;
  const res3 = await MoveEngine.executeMove(shedinja, jolteon, thunderbolt);
  const heal = res3.events.find((e) => e.type === "Heal");
  console.log(`Heal Event: ${!!heal} (Expected true)`);
  console.log(
    `HP Change: ${hpBefore} -> ${jolteon.currentHp} (Expected increase)`
  );

  console.log("\n3. Flash Fire (Arcanine)");
  // Hit with Flamethrower -> Boost
  console.log("- Hit with Flamethrower -> Boost?");
  const res4 = await MoveEngine.executeMove(shedinja, arcanine, flamethrower);
  const damage4 = res4.events.find((e) => e.type === "Damage");
  console.log(`Damage Event: ${!!damage4} (Expected false - Immune)`);
  console.log(
    `Volatile FlashFire: ${arcanine.volatile["FlashFire"]} (Expected 1)`
  );
}

runTests().catch(console.error);
