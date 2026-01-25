const fs = require('fs');
const path = require('path');

const pokedexPath = path.join(__dirname, '../data/db/pokedex.json');
const pokedex = require(pokedexPath);

console.log('Updating Gen 1 Pokemon with Gen 2 evolutions...\n');

// Cross-generation evolutions to add
const crossGenEvolutions = {
    '42': { // Golbat
        name: 'Golbat',
        evolution: {
            targetSpeciesId: '169', // Crobat
            friendship: 220
        }
    },
    '113': { // Chansey
        name: 'Chansey',
        evolution: {
            targetSpeciesId: '242', // Blissey
            friendship: 220
        }
    },
    '95': { // Onix
        name: 'Onix',
        evolution: {
            targetSpeciesId: '208', // Steelix
            trade: true,
            item: 'Metal Coat'
        }
    },
    '123': { // Scyther
        name: 'Scyther',
        evolution: {
            targetSpeciesId: '212', // Scizor
            trade: true,
            item: 'Metal Coat'
        }
    },
    '117': { // Seadra
        name: 'Seadra',
        evolution: {
            targetSpeciesId: '230', // Kingdra
            trade: true,
            item: 'Dragon Scale'
        }
    },
    '137': { // Porygon
        name: 'Porygon',
        evolution: {
            targetSpeciesId: '233', // Porygon2
            trade: true,
            item: 'Up Grade'
        }
    },
    '133': { // Eevee - has multiple Gen 2 evolutions
        name: 'Eevee',
        evolutions: [
            {
                targetSpeciesId: '196', // Espeon
                friendship: 220,
                timeOfDay: 'day'
            },
            {
                targetSpeciesId: '197', // Umbreon
                friendship: 220,
                timeOfDay: 'night'
            }
        ]
    }
};

let updatedCount = 0;

for (const [id, data] of Object.entries(crossGenEvolutions)) {
    if (pokedex[id]) {
        const pokemon = pokedex[id];
        
        // Initialize evolution object if it doesn't exist
        if (!pokemon.evolution) {
            pokemon.evolution = {};
        }
        
        // Initialize next array if it doesn't exist
        if (!pokemon.evolution.next) {
            pokemon.evolution.next = [];
        }
        
        // Add new evolution(s)
        if (data.evolutions) {
            // Multiple evolutions (like Eevee)
            for (const evo of data.evolutions) {
                pokemon.evolution.next.push(evo);
            }
            console.log(`✓ Updated ${data.name} (#${id}) - Added ${data.evolutions.length} Gen 2 evolutions`);
        } else {
            // Single evolution
            pokemon.evolution.next.push(data.evolution);
            console.log(`✓ Updated ${data.name} (#${id}) - Added Gen 2 evolution`);
        }
        
        updatedCount++;
    } else {
        console.log(`✗ Pokemon #${id} not found in pokedex`);
    }
}

// Write back to file
fs.writeFileSync(pokedexPath, JSON.stringify(pokedex, null, 2));

console.log(`\n✓ Complete! Updated ${updatedCount} Gen 1 Pokemon with Gen 2 evolutions.`);
