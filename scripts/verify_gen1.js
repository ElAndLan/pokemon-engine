const pokedex = require('../data/db/pokedex.json');
const fs = require('fs');
const path = require('path');

const ids = Object.keys(pokedex).map(id => parseInt(id)).sort((a, b) => a - b);
const missing = [];

for (let i = 1; i <= 151; i++) {
    if (!ids.includes(i)) {
        missing.push(i);
    }
}

console.log(`Total Pokemon in DB: ${ids.length}`);
if (missing.length > 0) {
    console.log(`Missing IDs (Gen 1): ${missing.join(', ')}`);
} else {
    console.log('All 151 Gen 1 Pokemon present in DB.');
}

// Check assets
const missingAssets = [];
for (let i = 1; i <= 151; i++) {
    const idStr = i.toString().padStart(3, '0'); // Pokedex uses "001" or "1"? 
    // Actually pokedex.json uses "1", "100".
    
    // Check if entry exists to verify assets
    const entry = pokedex[i.toString()] || pokedex[i.toString().padStart(3, '0')];
    
    if (entry) {
        if (!entry.assets || !entry.assets.shinyFront) {
            missingAssets.push(i);
        }
    }
}

if (missingAssets.length > 0) {
    console.log(`Pokemon missing Shiny Assets config: ${missingAssets.join(', ')}`);
} else {
    console.log('All 151 have asset config.');
}
