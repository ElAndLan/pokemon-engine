const fs = require('fs');
const path = require('path');
const pokedex = require('../data/db/pokedex.json');

const POKEDEX_PATH = path.join(__dirname, '../data/db/pokedex.json');

console.log('Updating Pokedex assets...');

let count = 0;
for (const id in pokedex) {
    if (parseInt(id) > 151) continue; // Only Gen 1 for now

    const entry = pokedex[id];
    
    // Ensure assets object exists
    if (!entry.assets) entry.assets = {};

    // Update/Overwrite paths
    entry.assets.front = `data/pokemon/images/${id}/front.png`;
    entry.assets.back = `data/pokemon/images/${id}/back.png`;
    entry.assets.shinyFront = `data/pokemon/images/${id}/shiny_front.png`;
    entry.assets.shinyBack = `data/pokemon/images/${id}/shiny_back.png`;
    
    // Keep icon path
    entry.assets.icon = `data/pokemon/icons/${id}.png`;
    
    // Keep overworld if exists
    if (!entry.assets.overworld) {
        entry.assets.overworld = `data/pokemon/overworld/${id}.png`;
    }

    count++;
}

fs.writeFileSync(POKEDEX_PATH, JSON.stringify(pokedex, null, 2));
console.log(`Updated assets for ${count} Pokemon.`);
