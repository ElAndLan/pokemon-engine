const fs = require('fs');
const path = require('path');

// Read existing pokedex
const pokedexPath = path.join(__dirname, '../data/db/pokedex.json');
const pokedex = require(pokedexPath);

console.log('Updating asset paths to use three-digit IDs...\n');

let updatedCount = 0;

// Update all Pokemon entries
Object.keys(pokedex).forEach(id => {
    const pokemon = pokedex[id];
    const paddedId = id.padStart(3, '0');
    
    // Check if assets exist
    if (!pokemon.assets) {
        console.log(`⚠ Pokemon ${id} (${pokemon.name}) has no assets object, creating one...`);
        pokemon.assets = {};
    }
    
    // Update asset paths to use three-digit format
    const oldAssets = JSON.stringify(pokemon.assets);
    
    pokemon.assets = {
        front: `data/pokemon/images/${paddedId}/front.png`,
        back: `data/pokemon/images/${paddedId}/back.png`,
        icon: `data/pokemon/icons/${paddedId}.png`,
        overworld: `data/pokemon/overworld/${paddedId}.png`,
        shinyFront: `data/pokemon/images/${paddedId}/shiny_front.png`,
        shinyBack: `data/pokemon/images/${paddedId}/shiny_back.png`
    };
    
    if (oldAssets !== JSON.stringify(pokemon.assets)) {
        console.log(`✓ Updated Pokemon ${id} (${pokemon.name})`);
        updatedCount++;
    }
});

// Write back to file
fs.writeFileSync(pokedexPath, JSON.stringify(pokedex, null, 2));

console.log(`\n✓ Complete! Updated ${updatedCount} Pokemon entries.`);
console.log('All Pokemon now reference:');
console.log('  - Three-digit ID format (001, 002, etc.)');
console.log('  - Shiny sprite variants (shinyFront, shinyBack)');
