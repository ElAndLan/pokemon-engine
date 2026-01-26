
import { BattleScene } from '../src/renderer/src/core/battle/BattleScene';
import { PokemonInstance } from '../src/renderer/src/core/data/DataTypes';
import { AbilityRegistry } from '../src/renderer/src/core/battle/Abilities';

// Setup Mock Game
const mockGame = {
    display: {},
    weatherManager: { 
        currentWeather: 'None',
        setWeather: function(w) { this.currentWeather = w; console.log(`[Weather] Set to ${w}`); }
    },
    dataManager: {
        getAllMoves: () => [],
        getMove: (id) => ({ id, name: id, priority: 0 }),
        getItem: (id) => ({ id, name: id }),
        getPokemonSpecies: (id) => ({ assets: {} }), // for loadSprite
    },
    menuSystem: { push: () => {}, pop: () => {} },
    bagSystem: { removeItem: () => {} }
} as any;

const battleScene = new BattleScene(mockGame);
// Patch showText
battleScene.showText = async (text) => { console.log(`[Text] ${text}`); };
// Patch loadSprite/loadBackground to avoid errors
battleScene['loadSprite'] = async () => {};
battleScene['loadBackground'] = async () => {};

// --- TEST DATA ---
const gyarados: PokemonInstance = {
    uuid: 'p1', speciesId: 'gyarados', nickname: 'Gyarados',
    types: ['Water', 'Flying'],
    currentStats: { hp: 100, attack: 100, defense: 100, spAttack: 100, spDefense: 100, speed: 80 },
    currentHp: 100, ability: 'Intimidate', status: 'None', volatile: {}, statStages: { attack: 0 }, moves: []
} as any;

const pikachu: PokemonInstance = {
    uuid: 'p2', speciesId: 'pikachu', nickname: 'Pikachu',
    types: ['Electric'],
    currentStats: { hp: 100, attack: 100, defense: 50, spAttack: 50, spDefense: 50, speed: 90 }, // Faster
    currentHp: 100, ability: 'Static', status: 'None', volatile: {}, statStages: { attack: 0 }, moves: []
} as any;

const politoed: PokemonInstance = {
    uuid: 'p3', speciesId: 'politoed', nickname: 'Politoed',
    types: ['Water'],
    currentStats: { hp: 100, speed: 60 },
    currentHp: 100, ability: 'Drizzle', status: 'None', volatile: {}, statStages: {}, moves: []
} as any;

const porygon: PokemonInstance = {
    uuid: 'p4', speciesId: 'porygon', nickname: 'Porygon',
    types: ['Normal'],
    currentStats: { hp: 100, speed: 70 },
    currentHp: 100, ability: 'Download', status: 'None', volatile: {}, statStages: { attack: 0, spAttack: 0 }, moves: []
} as any;

const gardevoir: PokemonInstance = {
    uuid: 'p5', speciesId: 'gardevoir', nickname: 'Gardevoir',
    types: ['Psychic'],
    currentStats: { hp: 100, speed: 80 },
    currentHp: 100, ability: 'Trace', status: 'None', volatile: {}, statStages: {}, moves: []
} as any;

// Target for Download: Low Def, High SpDef
const snorlax: PokemonInstance = {
    uuid: 'p6', speciesId: 'snorlax', nickname: 'Snorlax',
    currentStats: { hp: 160, defense: 60, spDefense: 110 },
    currentHp: 160, ability: 'Thick Fat', statStages: {}
} as any;


async function runTests() {
    console.log('--- ENTRY ABILITY TESTS ---');

    console.log('\n1. Intimidate (Gyarados vs Pikachu)');
    // Pikachu is faster, but Intimidate triggers on Gyarados entry regardless of order relative to attack, 
    // but startBattle logic triggers fast then slow.
    // Pikachu (Static - no entry effect) then Gyarados (Intimidate).
    
    // Setup Battle
    // We call startBattle directly but use a limited scope
    // Actually we can just manually invoke the logic if we want, or call startBattle.
    // Let's call startBattle to test the sorting logic too.
    
    console.log(`Pikachu Attack Stage Before: ${pikachu.statStages.attack}`);
    await battleScene.startBattle(pikachu, gyarados); 
    // Wait for async? startBattle is async.
    
    console.log(`Pikachu Attack Stage After: ${pikachu.statStages.attack} (Expected -1)`);

    console.log('\n2. Drizzle (Politoed)');
    mockGame.weatherManager.currentWeather = 'None';
    await AbilityRegistry.trigger('Drizzle', 'onBattleStart', { owner: politoed, battle: battleScene });
    console.log(`Weather: ${mockGame.weatherManager.currentWeather} (Expected Rain)`);

    console.log('\n3. Download (Porygon vs Snorlax)');
    // Snorlax Def < SpDef (60 < 110) -> Should boost Attack.
    // Mock opponents
    battleScene.playerPokemon = porygon;
    battleScene.enemyPokemon = snorlax;
    
    console.log(`Porygon Attack Stage Before: ${porygon.statStages.attack}`);
    await AbilityRegistry.trigger('Download', 'onBattleStart', { owner: porygon, battle: battleScene });
    console.log(`Porygon Attack Stage After: ${porygon.statStages.attack} (Expected 1)`);

    console.log('\n4. Trace (Gardevoir vs Gyarados)');
    // Gardevoir should Trace Intimidate
    // And then trigger Intimidate -> Lower Gyarados Attack
    battleScene.playerPokemon = gardevoir;
    battleScene.enemyPokemon = gyarados;
    // Reset Gyarados stages
    gyarados.statStages.attack = 0;
    
    console.log(`Gardevoir Ability Before: ${gardevoir.ability}`);
    await AbilityRegistry.trigger('Trace', 'onBattleStart', { owner: gardevoir, battle: battleScene });
    
    console.log(`Gardevoir Ability After: ${gardevoir.ability} (Expected Intimidate)`);
    console.log(`Gyarados Attack Stage: ${gyarados.statStages.attack} (Expected -1 from Traced Intimidate)`);
    // 5. Snow Warning (Abomasnow)
    console.log('\n5. Snow Warning (Abomasnow)');
    const abomasnow = { ...politoed, nickname: 'Abomasnow', ability: 'Snow Warning' } as any;
    mockGame.weatherManager.currentWeather = 'None';
    await AbilityRegistry.trigger('Snow Warning', 'onBattleStart', { owner: abomasnow, battle: battleScene });
    console.log(`Weather: ${mockGame.weatherManager.currentWeather} (Expected Hail)`);
}

runTests().catch(console.error);
