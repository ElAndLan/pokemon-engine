
import { BattleScene } from '../src/renderer/src/core/battle/BattleScene';
import { PokemonInstance, MoveData } from '../src/renderer/src/core/data/DataTypes';
import { AbilityRegistry } from '../src/renderer/src/core/battle/Abilities';
import { getEffectiveStat } from '../src/renderer/src/core/battle/StatCalculator';

// Setup Mock Game
const mockGame = {
    display: {},
    weatherManager: { currentWeather: 'None' },
    dataManager: {
        getAllMoves: () => [
            { id: 'scratch', name: 'Scratch', priority: 0 },
            { id: 'tackle', name: 'Tackle', priority: 0 }
        ],
        getMove: (id) => ({ id, name: id, priority: 0 }),
        getItem: (id) => ({ id, name: id })
    },
    menuSystem: { push: () => {}, pop: () => {} },
    bagSystem: { removeItem: () => {} }
} as any;

const battleScene = new BattleScene(mockGame);
// Patch showText to avoid hang
battleScene.showText = async (text) => { console.log(`[Text] ${text}`); };

// --- TEST DATA ---
const chansey: PokemonInstance = {
    uuid: 'p1', speciesId: 'chansey', nickname: 'Chansey',
    types: ['Normal'],
    currentStats: { hp: 100, attack: 10, defense: 10, spAttack: 10, spDefense: 10, speed: 10 },
    currentHp: 100,
    ability: 'Natural Cure',
    status: 'Poison', 
    volatile: {}, statStages: { speed: 0, spAttack: 0 }, moves: []
} as any;

const ekans: PokemonInstance = {
    uuid: 'p2', speciesId: 'ekans', nickname: 'Ekans',
    types: ['Poison'],
    currentStats: { hp: 100, attack: 10, defense: 10, spAttack: 10, spDefense: 10, speed: 10 },
    currentHp: 100,
    ability: 'Shed Skin',
    status: 'Burn',
    volatile: {}, statStages: { speed: 0 }, moves: []
} as any;

const yanma: PokemonInstance = {
    uuid: 'p3', speciesId: 'yanma', nickname: 'Yanma',
    types: ['Bug', 'Flying'],
    currentStats: { hp: 100, attack: 10, defense: 10, spAttack: 10, spDefense: 10, speed: 10 },
    currentHp: 100,
    ability: 'Speed Boost',
    status: 'None',
    volatile: {}, statStages: { speed: 0 }, moves: []
} as any;

const ludicolo: PokemonInstance = {
    uuid: 'p4', speciesId: 'ludicolo', nickname: 'Ludicolo',
    types: ['Water', 'Grass'],
    currentStats: { hp: 160, attack: 10, defense: 10, spAttack: 10, spDefense: 10, speed: 10 },
    currentHp: 100, // Injured
    ability: 'Rain Dish',
    status: 'None',
    volatile: {}, statStages: {}, moves: []
} as any;

const charizard: PokemonInstance = {
    uuid: 'p5', speciesId: 'charizard', nickname: 'Charizard',
    types: ['Fire', 'Flying'],
    currentStats: { hp: 80, attack: 100, defense: 50, spAttack: 100, spDefense: 50, speed: 100 },
    currentHp: 80,
    ability: 'Solar Power',
    status: 'None',
    volatile: {}, statStages: { spAttack: 0 }, moves: []
} as any;


async function runTests() {
    console.log('--- TURN ABILITY TESTS ---');

    // 1. Natural Cure (Switch Out)
    console.log('\n1. Natural Cure (Chansey)');
    console.log(`Status Before: ${chansey.status}`);
    // Simulate Trigger manually since switching involves UI logic we don't want to run fully
    await AbilityRegistry.trigger('Natural Cure', 'onSwitchOut', { owner: chansey, battle: battleScene });
    console.log(`Status After: ${chansey.status} (Expected None)`);

    // 2. Shed Skin (Turn End)
    console.log('\n2. Shed Skin (Ekans)');
    // Override random to ensure trigger? Or loop?
    // Let's loop 20 times, should happen.
    let cured = false;
    for (let i = 0; i < 20; i++) {
        if (ekans.status === 'None') { cured = true; break; }
        await AbilityRegistry.trigger('Shed Skin', 'onTurnEnd', { owner: ekans, battle: battleScene });
    }
    console.log(`Shed Skin Triggered: ${cured} (Expected true)`);

    // 3. Speed Boost (Turn End)
    console.log('\n3. Speed Boost (Yanma)');
    console.log(`Speed Stage Before: ${yanma.statStages?.speed}`);
    await AbilityRegistry.trigger('Speed Boost', 'onTurnEnd', { owner: yanma, battle: battleScene });
    console.log(`Speed Stage After: ${yanma.statStages?.speed} (Expected 1)`);

    // 4. Rain Dish (Turn End in Rain)
    console.log('\n4. Rain Dish (Ludicolo)');
    const mockBattleWithWeather = {
        ...battleScene,
        game: {
            weatherManager: { currentWeather: 'Rain' }
        },
        showText: console.log
    } as any;

    const hpBefore = ludicolo.currentHp;
    await AbilityRegistry.trigger('Rain Dish', 'onTurnEnd', { owner: ludicolo, battle: mockBattleWithWeather });
    console.log(`HP Before: ${hpBefore}, HP After: ${ludicolo.currentHp} (Expected increase)`);

    // 5. Solar Power (Sun)
    console.log('\n5. Solar Power (Charizard)');
    const mockBattleSun = {
         ...battleScene,
         game: {
             weatherManager: { currentWeather: 'Sun' }
         },
         showText: console.log 
    } as any;
    
    // Stat Check
    const spAtk = getEffectiveStat(charizard, 'spAttack', { owner: charizard, battle: mockBattleSun });
    console.log(`Sp. Atk in Sun: ${spAtk} (Base 100 * 1.5 = 150?)`); // 100 * 1.5 = 150.
    
    // HP Drain Check
    const hpStart = charizard.currentHp;
    await AbilityRegistry.trigger('Solar Power', 'onTurnEnd', { owner: charizard, battle: mockBattleSun });
    console.log(`HP Before: ${hpStart}, HP After: ${charizard.currentHp} (Expected drop)`);
}

runTests().catch(console.error);
