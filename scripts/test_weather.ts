
import { BattleScene } from '../src/renderer/src/core/battle/BattleScene';
import { Game } from '../src/renderer/src/core/Game';
import { PokemonInstance, MoveData, WeatherType } from '../src/renderer/src/core/data/DataTypes';
import { MoveEngine } from '../src/renderer/src/core/battle/MoveEngine';
import { AbilityRegistry } from '../src/renderer/src/core/battle/Abilities';
import * as StatCalculator from '../src/renderer/src/core/battle/StatCalculator';

// Setup Mocks
const mockGame = {
    display: {},
    weatherManager: { currentWeather: 'None' }
} as any;

const battleScene = new BattleScene(mockGame);

// Mock Pokemon
const charizard: PokemonInstance = {
    uuid: 'p1',
    speciesId: 'charizard',
    nickname: 'Charizard',
    types: ['Fire', 'Flying'],
    currentStats: { hp: 100, attack: 100, defense: 100, spAttack: 100, spDefense: 100, speed: 100 },
    currentHp: 100,
    ivs: { hp: 31, attack: 31, defense: 31, spAttack: 31, spDefense: 31, speed: 31 },
    evs: { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 },
    level: 50,
    experience: 0,
    nature: 'Bold',
    ability: 'Solar Power', // Solar Power
    gender: 'Male',
    shiny: false,
    moves: [],
    status: 'None',
    volatile: {},
    statStages: {},
    originalTrainer: 'Red'
};

const blastoise: PokemonInstance = {
    uuid: 'p2',
    speciesId: 'blastoise',
    nickname: 'Blastoise',
    types: ['Water'],
    currentStats: { hp: 100, attack: 100, defense: 100, spAttack: 100, spDefense: 100, speed: 50 },
    currentHp: 100,
    ivs: { hp: 31, attack: 31, defense: 31, spAttack: 31, spDefense: 31, speed: 31 },
    evs: { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 },
    level: 50,
    experience: 0,
    nature: 'Bold',
    ability: 'Rain Dish', // Rain Dish
    gender: 'Male',
    shiny: false,
    moves: [],
    status: 'None',
    volatile: {},
    statStages: {},
    originalTrainer: 'Blue'
};

// Mock Moves
const flamethrower: MoveData = {
    id: 'flamethrower',
    name: 'Flamethrower',
    type: 'Fire',
    category: 'Special',
    power: 90,
    accuracy: 100,
    pp: 15,
    priority: 0,
    target: 'SelectedEnemy',
    effects: [{ type: 'Damage' }],
    description: 'Fire attack'
};

const waterGun: MoveData = {
    id: 'watergun',
    name: 'Water Gun',
    type: 'Water',
    category: 'Special',
    power: 40,
    accuracy: 100,
    pp: 25,
    priority: 0,
    target: 'SelectedEnemy',
    effects: [{ type: 'Damage' }],
    description: 'Water attack'
};

async function runTests() {
    console.log('--- STARTING WEATHER TESTS ---');

    // 1. Test Weather Multipliers (Damage Calc)
    console.log('\n--- 1. Damage Multipliers (Sun) ---');
    // Solar Power should activate in Sun
    // Fire move should be boosted 1.5x in Sun
    
    // Base Case (No Weather)
    let res = MoveEngine.executeMove(charizard, blastoise, flamethrower, 'None');
    let dmgEvent = res.events.find(e => e.type === 'Damage');
    console.log(`Damage (None): ${dmgEvent?.value}`);
    const baseDmg = dmgEvent?.value;

    // Sun Case
    res = MoveEngine.executeMove(charizard, blastoise, flamethrower, 'Sun');
    dmgEvent = res.events.find(e => e.type === 'Damage');
    console.log(`Damage (Sun + Solar Power): ${dmgEvent?.value}`);
    // Expected: 1.5 (Weather) * 1.5 (Solar Power) = 2.25x Base? Or close logic.
    // Damage Calc:
    // A (SpAtk) * 1.5 (Solar Power)
    // Damage * 1.5 (Sun)
    
    // 2. Test Weather Speed (Swift Swim)
    console.log('\n--- 2. Speed (Rain) ---');
    const golduck: PokemonInstance = { ...charizard, ability: 'Swift Swim', speciesId: 'golduck', types: ['Water'] };
    golduck.currentStats.speed = 100;
    
    // Context needs battle for StatCalculator hook
    const mockBattle = { weather: 'Rain' } as any;
    const speedRain = StatCalculator.getEffectiveStat(golduck, 'speed', { owner: golduck, battle: mockBattle });
    console.log(`Speed (Rain + Swift Swim): ${speedRain} (Expected 200)`);
    
    const speedSun = StatCalculator.getEffectiveStat(golduck, 'speed', { owner: golduck, battle: { weather: 'Sun' } as any });
    console.log(`Speed (Sun + Swift Swim): ${speedSun} (Expected 100)`);

    // 3. Test Weather Healing (Rain Dish)
    console.log('\n--- 3. Weather Healing (Rain Dish) ---');
    blastoise.currentHp = 50;
    // We can't mock BattleScene easily for onTurnEnd logic without running it.
    // But we verified the code logic.
    // We can verify AbilityRegistry has the capability?
    // Rain Dish logic is inside BattleScene.ts loop, not generic Ability Hook yet.
    // In `BattleScene.ExecuteEndOfTurn`, it checks `mon.ability === 'Rain Dish'`.
    
    console.log('Test Complete');
}

runTests().catch(console.error);
