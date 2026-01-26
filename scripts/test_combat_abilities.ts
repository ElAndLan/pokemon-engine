
import { BattleScene } from '../src/renderer/src/core/battle/BattleScene';
import { PokemonInstance, MoveData, WeatherType } from '../src/renderer/src/core/data/DataTypes';
import { MoveEngine } from '../src/renderer/src/core/battle/MoveEngine';
import { AbilityRegistry } from '../src/renderer/src/core/battle/Abilities';
import { calculateDamage } from '../src/renderer/src/core/battle/DamageCalculator';

// Setup Mocks
const mockGame = {
    display: {},
    weatherManager: { currentWeather: 'None' },
    dataManager: {
        getAllMoves: () => [],
        getMove: (id) => ({ id, name: id, priority: 0 }),
        getItem: (id) => ({ id, name: id })
    }
} as any;

const battleScene = new BattleScene(mockGame);

// Mock Pokemon
const machamp: PokemonInstance = {
    uuid: 'p1',
    speciesId: 'machamp',
    nickname: 'Machamp',
    types: ['Fighting'],
    currentStats: { hp: 100, attack: 100, defense: 100, spAttack: 50, spDefense: 100, speed: 50 },
    currentHp: 100,
    ivs: { hp:31, attack:31, defense:31, spAttack:31, spDefense:31, speed:31 },
    evs: { hp:0, attack:0, defense:0, spAttack:0, spDefense:0, speed:0 },
    level: 50, experience: 0, nature: 'Adamant', ability: 'Guts', gender: 'Male', shiny: false, moves: [], status: 'None', volatile: {}, originalTrainer: 'Red', statStages: {}
};

const scizor: PokemonInstance = {
    uuid: 'p2',
    speciesId: 'scizor',
    nickname: 'Scizor',
    types: ['Bug', 'Steel'],
    currentStats: { hp: 100, attack: 100, defense: 100, spAttack: 50, spDefense: 100, speed: 60 },
    currentHp: 100,
    ivs: { hp:31, attack:31, defense:31, spAttack:31, spDefense:31, speed:31 },
    evs: { hp:0, attack:0, defense:0, spAttack:0, spDefense:0, speed:0 },
    level: 50, experience: 0, nature: 'Adamant', ability: 'Technician', gender: 'Male', shiny: false, moves: [], status: 'None', volatile: {}, originalTrainer: 'Blue', statStages: {}
};

const onix: PokemonInstance = {
    uuid: 'p3',
    speciesId: 'onix',
    nickname: 'Onix',
    types: ['Rock', 'Ground'],
    currentStats: { hp: 100, attack: 50, defense: 150, spAttack: 30, spDefense: 50, speed: 40 },
    currentHp: 100,
    ivs: { hp:31, attack:31, defense:31, spAttack:31, spDefense:31, speed:31 },
    evs: { hp:0, attack:0, defense:0, spAttack:0, spDefense:0, speed:0 },
    level: 50, experience: 0, nature: 'Impish', ability: 'Rock Head', gender: 'Male', shiny: false, moves: [], status: 'None', volatile: {}, originalTrainer: 'Brock', statStages: {}
};

// Moves
const bulletPunch: MoveData = {
    id: 'bullet_punch', name: 'Bullet Punch', type: 'Steel', category: 'Physical', power: 40, accuracy: 100, pp: 30, priority: 1, target: 'SelectedEnemy', effects: [{ type: 'Damage' }], description: 'Priority move.'
};

const doubleEdge: MoveData = {
    id: 'double_edge', name: 'Double Edge', type: 'Normal', category: 'Physical', power: 120, accuracy: 100, pp: 15, priority: 0, target: 'SelectedEnemy', effects: [{ type: 'Damage' }], recoil: { type: 'Damage', percent: 33 }, description: 'High recoil.'
};

const earthquake: MoveData = {
    id: 'earthquake', name: 'Earthquake', type: 'Ground', category: 'Physical', power: 100, accuracy: 100, pp: 10, priority: 0, target: 'SelectedEnemy', effects: [{ type: 'Damage' }], description: 'Ground move.'
};

const machPunch: MoveData = {
    id: 'mach_punch', name: 'Mach Punch', type: 'Fighting', category: 'Physical', power: 40, accuracy: 100, pp: 30, priority: 1, target: 'SelectedEnemy', effects: [{ type: 'Damage' }], description: 'Punch.'
};


async function runTests() {
    console.log('--- STARTING COMBAT ABILITY TESTS ---');

    // 1. Technician (Scizor use Bullet Punch 40 BP -> 60 BP)
    console.log('\n--- 1. Technician (Base Power: 40 -> 60) ---');
    // We check via DamageCalculator loop trace or result.
    // Calculate Damage (No Tech): 
    // scizor.ability = 'None'; 
    // let resBase = calculateDamage(scizor, machamp, bulletPunch);
    // console.log(`Damage (No Ability): ${resBase.damage}`);
    
    // scizor.ability = 'Technician';
    // let resTech = calculateDamage(scizor, machamp, bulletPunch);
    // console.log(`Damage (Technician): ${resTech.damage}`);
    // Expected: 1.5x increase roughly.
    
    // Manually run
    const resTech = calculateDamage(scizor, machamp, bulletPunch);
    console.log(`Damage (Technician): ${resTech.damage} (BP 60 effectively)`);
    
    // 2. Guts (Machamp Burned -> 1.5x Attack)
    console.log('\n--- 2. Guts (Burn -> 1.5x Atk) ---');
    machamp.status = 'Burn';
    const resGuts = calculateDamage(machamp, scizor, machPunch); // Mach Punch 40
    console.log(`Damage (Guts + Burn): ${resGuts.damage} (Should be boosted, and Burn Halving ignored? Wait, Burn Halving is usually ignored by Guts!)`);
    // Note: Guts ignores Burn Attack Drop. 
    // Currently StatCalculator handles Burn Drop (Spec 4.2).
    // Guts adds 1.5x.
    // DOES GUTS IGNORE BURN DROP? Yes, "ignoring the attack drop from a burn".
    // I need to update StatCalculator or Ability implementation for Guts to cancel Burn drop or apply 1.5x ON TOP of 0.5x?
    // Actually, usually Guts implementation strictly sets "Ignore Burn".
    // My StatCalculator applies 0.5x if Burn.
    // Guts applies 1.5x.
    // Total = 0.75x ?? That's wrong. Should be 1.5x net. 
    // So Guts needs to handle "Prevent Burn Drop" or StatCalculator needs check.
    
    // 3. Rock Head
    console.log('\n--- 3. Rock Head (No Recoil) ---');
    onix.currentHp = 100;
    // Hit something
    const resRock = MoveEngine.executeMove(onix, machamp, doubleEdge);
    // doubleEdge has recoil.
    // Check events for 'Damage' on Onix (Self).
    const selfDmg = resRock.events.filter(e => e.targetId === onix.uuid && e.type === 'Damage');
    console.log(`Recoil Events on Onix: ${selfDmg.length} (Expected 0)`);
    
    // 4. Levitate (Flygon/Gengar vs Earthquake)
    console.log('\n--- 4. Levitate (Ground Immunity) ---');
    const flygon: PokemonInstance = { ...onix, nickname: 'Flygon', speciesId: 'flygon', types: ['Ground', 'Dragon'], ability: 'Levitate' };
    const resLev = MoveEngine.executeMove(machamp, flygon, earthquake);
    const hit = resLev.events.find(e => e.type === 'Damage');
    console.log(`Earthquake Hit Flygon: ${!!hit} (Expected false)`);
}

runTests().catch(console.error);
