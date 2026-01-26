
import { BattleScene } from '../src/renderer/src/core/battle/BattleScene';
import { PokemonInstance, MoveData } from '../src/renderer/src/core/data/DataTypes';
import { AbilityRegistry } from '../src/renderer/src/core/battle/Abilities';

// Setup Mock Game
const mockGame = {
    display: {},
    weatherManager: { currentWeather: 'None' },
    dataManager: {
        getAllMoves: () => [
             { id: 'thunderwave', name: 'Thunder Wave', priority: 0, category: 'Status' },
             { id: 'bravebird', name: 'Brave Bird', priority: 0, type: 'Flying', category: 'Physical' },
             { id: 'cometpunch', name: 'Comet Punch', basePower: 18, category: 'Physical' }, // 18 * x hits? Let's treat as single for power check
             { id: 'quickattack', name: 'Quick Attack', basePower: 40, priority: 1, category: 'Physical' },
             { id: 'takedown', name: 'Take Down', basePower: 90, category: 'Physical' } 
        ],
        getMove: (id) => {
             const moves = {
                 'thunderwave': { id: 'thunderwave', name: 'Thunder Wave', priority: 0, category: 'Status' },
                 'bravebird': { id: 'bravebird', name: 'Brave Bird', priority: 0, type: 'Flying', category: 'Physical' },
                 'cometpunch': { id: 'cometpunch', name: 'Comet Punch', basePower: 18, category: 'Physical' },
                 'quickattack': { id: 'quickattack', name: 'Quick Attack', basePower: 40, priority: 1, category: 'Physical' },
                 'takedown': { id: 'takedown', name: 'Take Down', basePower: 90, category: 'Physical' }
             };
             return moves[id];
        },
        getItem: (id) => ({ id, name: id })
    },
    menuSystem: { push: () => {}, pop: () => {} },
    bagSystem: { removeItem: () => {} }
} as any;

const battleScene = new BattleScene(mockGame);

// --- TEST DATA ---
const murkrow: PokemonInstance = {
    uuid: 'p1', speciesId: 'murkrow', nickname: 'Murkrow',
    currentStats: { hp: 100 }, currentHp: 100, ability: 'Prankster', moves: []
} as any;

const talonflame: PokemonInstance = {
    uuid: 'p2', speciesId: 'talonflame', nickname: 'Talonflame',
    currentStats: { hp: 100 }, currentHp: 100, types: ['Fire', 'Flying'], ability: 'Gale Wings', moves: []
} as any;

const hitmonchan: PokemonInstance = {
    uuid: 'p3', speciesId: 'hitmonchan', nickname: 'Hitmonchan',
    currentStats: { hp: 100 }, currentHp: 100, ability: 'Iron Fist', moves: []
} as any;

const scyther: PokemonInstance = {
    uuid: 'p4', speciesId: 'scyther', nickname: 'Scyther',
    currentStats: { hp: 100 }, currentHp: 100, ability: 'Technician', moves: []
} as any;

const staraptor: PokemonInstance = {
    uuid: 'p5', speciesId: 'staraptor', nickname: 'Staraptor',
    currentStats: { hp: 100 }, currentHp: 100, ability: 'Reckless', moves: []
} as any;

async function runTests() {
    console.log('--- PRIORITY & MOVE PROPERTY TESTS ---');

    console.log('\n1. Prankster (Murkrow)');
    const twave = mockGame.dataManager.getMove('thunderwave');
    const p1 = AbilityRegistry.applyModifier('Prankster', 'onModifyPriority', twave.priority, { owner: murkrow, move: twave });
    console.log(`Thunder Wave Priority: ${p1} (Expected 1)`);

    console.log('\n2. Gale Wings (Talonflame)');
    const bbird = mockGame.dataManager.getMove('bravebird');
    const p2 = AbilityRegistry.applyModifier('Gale Wings', 'onModifyPriority', bbird.priority, { owner: talonflame, move: bbird });
    console.log(`Brave Bird Priority (Full HP): ${p2} (Expected 1)`);
    
    // Injured check
    talonflame.currentHp = 50;
    const p3 = AbilityRegistry.applyModifier('Gale Wings', 'onModifyPriority', bbird.priority, { owner: talonflame, move: bbird });
    console.log(`Brave Bird Priority (Injured): ${p3} (Expected 0)`);

    console.log('\n3. Iron Fist (Hitmonchan)');
    const punch = mockGame.dataManager.getMove('cometpunch');
    const pow1 = AbilityRegistry.applyModifier('Iron Fist', 'onModifyBasePower', punch.basePower, { owner: hitmonchan, move: punch });
    console.log(`Comet Punch Power: ${pow1} (Expected 18 * 1.2 = 21.6)`);

    console.log('\n4. Technician (Scyther)');
    const quick = mockGame.dataManager.getMove('quickattack'); // 40 BP
    const pow2 = AbilityRegistry.applyModifier('Technician', 'onModifyBasePower', quick.basePower, { owner: scyther, move: quick });
    console.log(`Quick Attack Power (40 BP): ${pow2} (Expected 40 * 1.5 = 60)`);
    
    const strong = mockGame.dataManager.getMove('takedown'); // 90 BP
    const pow3 = AbilityRegistry.applyModifier('Technician', 'onModifyBasePower', strong.basePower, { owner: scyther, move: strong });
    console.log(`Take Down Power (90 BP): ${pow3} (Expected 90 - No Boost)`);
    
    console.log('\n5. Reckless (Staraptor)');
    const pow4 = AbilityRegistry.applyModifier('Reckless', 'onModifyBasePower', strong.basePower, { owner: staraptor, move: strong });
    console.log(`Take Down Power: ${pow4} (Expected 90 * 1.2 = 108)`);
}

runTests().catch(console.error);
