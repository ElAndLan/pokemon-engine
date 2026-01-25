const fs = require('fs');
const path = require('path');

const POKEDEX_PATH = path.join(__dirname, '../data/db/pokedex.json');
const LEARNSET_PATH = path.join(__dirname, '../data/db/pokemon_moves_and_evolutions.json');
const OUTPUT_PATH = path.join(__dirname, '../data/db/pokedex.json');

console.log('Merging learnset data into pokedex.json...\n');

const pokedex = JSON.parse(fs.readFileSync(POKEDEX_PATH, 'utf8'));
const learnsetData = JSON.parse(fs.readFileSync(LEARNSET_PATH, 'utf8'));

let updatedCount = 0;
let skippedCount = 0;
let errorCount = 0;

Object.keys(learnsetData).forEach(id => {
    const pokemon = learnsetData[id];
    
    if (!pokemon.learnset || pokemon.learnset.length === 0) {
        skippedCount++;
        return;
    }

    if (!pokedex[id]) {
        console.warn(`  Warning: Pokemon #${id} (${pokemon.name}) not found in pokedex.json`);
        errorCount++;
        return;
    }

    const oldLearnset = pokedex[id].learnset || [];
    pokedex[id].learnset = pokemon.learnset;
    
    const hasLearnsetChanged = JSON.stringify(oldLearnset) !== JSON.stringify(pokemon.learnset);
    if (hasLearnsetChanged) {
        updatedCount++;
        console.log(`  Updated #${id} ${pokemon.name}: ${oldLearnset.length} -> ${pokemon.learnset.length} moves`);
    } else {
        skippedCount++;
    }
});

console.log(`\nSummary:`);
console.log(`  Updated: ${updatedCount} Pokemon`);
console.log(`  Skipped: ${skippedCount} Pokemon (no changes or empty learnset)`);
console.log(`  Errors: ${errorCount} Pokemon (not found in pokedex)`);

fs.writeFileSync(OUTPUT_PATH, JSON.stringify(pokedex, null, 2));
console.log(`\n✓ Successfully merged learnset data into pokedex.json`);
