const EncounterManager = require('../src/managers/EncounterManager');

// Initialize the encounter manager
const encounterManager = new EncounterManager();

console.log('='.repeat(60));
console.log('WILD ENCOUNTER SYSTEM TEST');
console.log('='.repeat(60));
console.log();

// Test 1: Generate grass encounter on Route 1
console.log('Test 1: Route 1 Grass Encounter');
console.log('-'.repeat(60));
const grassEncounter = encounterManager.generateEncounter('route_1', 'grass');
if (grassEncounter) {
    console.log(`Encountered: ${grassEncounter.name} (Level ${grassEncounter.level})`);
    console.log(`Shiny: ${grassEncounter.isShiny ? 'YES! ✨' : 'No'}`);
    console.log(`Nature: ${grassEncounter.nature}`);
    console.log(`Ability: ${grassEncounter.ability}`);
    console.log(`IVs:`, grassEncounter.ivs);
    console.log(`Stats:`, grassEncounter.stats);
    console.log(`Moves: ${grassEncounter.moves.join(', ')}`);
}
console.log();

// Test 2: Generate fishing encounter
console.log('Test 2: Route 19 Super Rod Encounter');
console.log('-'.repeat(60));
const fishEncounter = encounterManager.generateEncounter('route_19', 'superRod');
if (fishEncounter) {
    console.log(`Encountered: ${fishEncounter.name} (Level ${fishEncounter.level})`);
    console.log(`Shiny: ${fishEncounter.isShiny ? 'YES! ✨' : 'No'}`);
    console.log(`Nature: ${fishEncounter.nature}`);
    console.log(`Moves: ${fishEncounter.moves.join(', ')}`);
}
console.log();

// Test 3: Generate multiple encounters to test distribution
console.log('Test 3: 20 Route 1 Grass Encounters (Distribution Test)');
console.log('-'.repeat(60));
const distribution = {};
for (let i = 0; i < 20; i++) {
    const enc = encounterManager.generateEncounter('route_1', 'grass');
    if (enc) {
        distribution[enc.name] = (distribution[enc.name] || 0) + 1;
    }
}
console.log('Distribution:', distribution);
console.log();

// Test 4: Test shiny chance
console.log('Test 4: Shiny Chance Test (1000 encounters)');
console.log('-'.repeat(60));
let shinyCount = 0;
for (let i = 0; i < 1000; i++) {
    const enc = encounterManager.generateEncounter('route_1', 'grass');
    if (enc && enc.isShiny) shinyCount++;
}
console.log(`Shinies found: ${shinyCount}/1000`);
console.log(`Expected: ~0.12 (1/8192 = 0.000122)`);
console.log();

// Test 5: Test custom shiny chance
console.log('Test 5: Custom Shiny Chance (1/512 with Shiny Charm)');
console.log('-'.repeat(60));
encounterManager.setShinyChance(512);
shinyCount = 0;
for (let i = 0; i < 1000; i++) {
    const enc = encounterManager.generateEncounter('route_1', 'grass');
    if (enc && enc.isShiny) shinyCount++;
}
console.log(`Shinies found: ${shinyCount}/1000`);
console.log(`Expected: ~2 (1/512 = 0.00195)`);
console.log();

// Test 6: Safari Zone encounter
console.log('Test 6: Safari Zone Encounter');
console.log('-'.repeat(60));
encounterManager.setShinyChance(8192); // Reset to default
const safariEncounter = encounterManager.generateEncounter('safari_zone_area_1', 'safari');
if (safariEncounter) {
    console.log(`Encountered: ${safariEncounter.name} (Level ${safariEncounter.level})`);
    console.log(`Shiny: ${safariEncounter.isShiny ? 'YES! ✨' : 'No'}`);
    console.log(`Moves: ${safariEncounter.moves.join(', ')}`);
}
console.log();

// Test 7: Dark Grass encounter (Gen 5)
console.log('Test 7: Route 10 Unova - Dark Grass Encounter');
console.log('-'.repeat(60));
const darkGrassEncounter = encounterManager.generateEncounter('route_10_unova', 'darkGrass');
if (darkGrassEncounter) {
    console.log(`Encountered: ${darkGrassEncounter.name} (Level ${darkGrassEncounter.level})`);
    console.log(`Shiny: ${darkGrassEncounter.isShiny ? 'YES! ✨' : 'No'}`);
    console.log(`Stats:`, darkGrassEncounter.stats);
    console.log(`Moves: ${darkGrassEncounter.moves.join(', ')}`);
}
console.log();

console.log('='.repeat(60));
console.log('ALL TESTS COMPLETE');
console.log('='.repeat(60));
