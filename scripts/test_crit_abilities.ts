import { calculateDamage, SETTINGS } from '../src/renderer/src/core/battle/DamageCalculator';
import { PokemonInstance, MoveData } from '../src/renderer/src/core/data/DataTypes';
import { AbilityRegistry } from '../src/renderer/src/core/battle/Abilities';

// Disable randomness for consistent testing
SETTINGS.USE_RANDOM = false;

console.log('\n=== BATCH 18: CRITICAL HIT ABILITIES ===\n');

// Test Pokemon
const attacker: PokemonInstance = {
    uuid: 'atk1',
    speciesId: 'test',
    nickname: 'Attacker',
    types: ['Normal'],
    level: 50,
    currentStats: { hp: 100, attack: 100, defense: 50, spAttack: 50, spDefense: 50, speed: 50 },
    currentHp: 100,
    ability: 'None',
    status: 'None',
    statStages: {},
    volatile: {},
    moves: []
} as any;

const defender: PokemonInstance = {
    uuid: 'def1',
    speciesId: 'test',
    nickname: 'Defender',
    types: ['Normal'],
    level: 50,
    currentStats: { hp: 100, attack: 50, defense: 50, spAttack: 50, spDefense: 50, speed: 50 },
    currentHp: 100,
    ability: 'None',
    status: 'None',
    statStages: {},
    volatile: {},
    moves: []
} as any;

const testMove: MoveData = {
    id: 'tackle',
    name: 'Tackle',
    type: 'Normal',
    category: 'Physical',
    power: 50,
    accuracy: 100,
    pp: 35
} as any;

console.log('1. Testing Battle Armor (Prevents Crits)');
defender.ability = 'Battle Armor';
SETTINGS.USE_RANDOM = true; // Enable random for crit chance

let critCount = 0;
for (let i = 0; i < 100; i++) {
    const result = calculateDamage(attacker, defender, testMove);
    if (result.isCritical) critCount++;
}
console.log(`  Crits in 100 attacks: ${critCount} (Expected: 0)`);
console.log(`  ${critCount === 0 ? '✓ PASS' : '✗ FAIL'}`);

console.log('\n2. Testing Shell Armor (Prevents Crits)');
defender.ability = 'Shell Armor';
critCount = 0;
for (let i = 0; i < 100; i++) {
    const result = calculateDamage(attacker, defender, testMove);
    if (result.isCritical) critCount++;
}
console.log(`  Crits in 100 attacks: ${critCount} (Expected: 0)`);
console.log(`  ${critCount === 0 ? '✓ PASS' : '✗ FAIL'}`);

console.log('\n3. Testing Merciless (Forces Crit on Poisoned)');
defender.ability = 'None';
defender.status = 'Poison';
attacker.ability = 'Merciless';
SETTINGS.USE_RANDOM = false; // Disable random

const result1 = calculateDamage(attacker, defender, testMove);
console.log(`  Crit on poisoned target: ${result1.isCritical} (Expected: true)`);
console.log(`  ${result1.isCritical ? '✓ PASS' : '✗ FAIL'}`);

defender.status = 'None';
const result2 = calculateDamage(attacker, defender, testMove);
console.log(`  Crit on non-poisoned target: ${result2.isCritical} (Expected: false)`);
console.log(`  ${!result2.isCritical ? '✓ PASS' : '✗ FAIL'}`);

console.log('\n4. Testing Anger Point (Maximizes Attack on Crit)');
defender.ability = 'Anger Point';
defender.statStages.attack = 0;

// Manually trigger onReceiveCrit
const angerPointAbility = AbilityRegistry.get('Anger Point');
if (angerPointAbility?.onReceiveCrit) {
    angerPointAbility.onReceiveCrit({ owner: defender, battle: undefined }).then(() => {
        console.log(`  Attack stage after crit: ${defender.statStages.attack} (Expected: 6)`);
        console.log(`  ${defender.statStages.attack === 6 ? '✓ PASS' : '✗ FAIL'}`);
        console.log('\n=== ALL TESTS COMPLETE ===\n');
    });
} else {
    console.log('  ✗ FAIL - onReceiveCrit not found');
    console.log('\n=== ALL TESTS COMPLETE ===\n');
}
