const fs = require('fs');
const path = require('path');

// Read existing pokedex
const pokedexPath = path.join(__dirname, '../data/db/pokedex.json');
const pokedex = require(pokedexPath);

// Missing Pokemon data (Gen 1 accurate stats and moves)
const missingPokemon = {
    "21": {
        "id": "21",
        "name": "Spearow",
        "types": ["Normal", "Flying"],
        "baseStats": {
            "hp": 40,
            "attack": 60,
            "defense": 30,
            "spAttack": 31,
            "spDefense": 31,
            "speed": 70
        },
        "learnset": [
            { "level": 1, "moveId": "peck" },
            { "level": 1, "moveId": "growl" },
            { "level": 7, "moveId": "leer" },
            { "level": 13, "moveId": "fury_attack" },
            { "level": 25, "moveId": "aerial_ace" },
            { "level": 31, "moveId": "mirror_move" },
            { "level": 37, "moveId": "drill_peck" },
            { "level": 43, "moveId": "agility" }
        ],
        "evolution": {
            "next": [{ "targetSpeciesId": "22", "level": 20 }]
        },
        "possibleAbilities": ["Keen Eye"],
        "catchRate": 255,
        "expYield": 52,
        "assets": {
            "front": "data/pokemon/images/21/front.png",
            "back": "data/pokemon/images/21/back.png",
            "icon": "data/pokemon/icons/21.png",
            "overworld": "data/pokemon/overworld/21.png",
            "shinyFront": "data/pokemon/images/21/shiny_front.png",
            "shinyBack": "data/pokemon/images/21/shiny_back.png"
        }
    },
    "22": {
        "id": "22",
        "name": "Fearow",
        "types": ["Normal", "Flying"],
        "baseStats": {
            "hp": 65,
            "attack": 90,
            "defense": 65,
            "spAttack": 61,
            "spDefense": 61,
            "speed": 100
        },
        "learnset": [
            { "level": 1, "moveId": "peck" },
            { "level": 1, "moveId": "growl" },
            { "level": 1, "moveId": "leer" },
            { "level": 1, "moveId": "fury_attack" },
            { "level": 7, "moveId": "leer" },
            { "level": 13, "moveId": "fury_attack" },
            { "level": 26, "moveId": "aerial_ace" },
            { "level": 32, "moveId": "mirror_move" },
            { "level": 40, "moveId": "drill_peck" },
            { "level": 47, "moveId": "agility" }
        ],
        "evolution": {
            "preEvolutionId": "21"
        },
        "possibleAbilities": ["Keen Eye"],
        "catchRate": 90,
        "expYield": 155,
        "assets": {
            "front": "data/pokemon/images/22/front.png",
            "back": "data/pokemon/images/22/back.png",
            "icon": "data/pokemon/icons/22.png",
            "overworld": "data/pokemon/overworld/22.png",
            "shinyFront": "data/pokemon/images/22/shiny_front.png",
            "shinyBack": "data/pokemon/images/22/shiny_back.png"
        }
    },
    "23": {
        "id": "23",
        "name": "Ekans",
        "types": ["Poison"],
        "baseStats": {
            "hp": 35,
            "attack": 60,
            "defense": 44,
            "spAttack": 40,
            "spDefense": 54,
            "speed": 55
        },
        "learnset": [
            { "level": 1, "moveId": "wrap" },
            { "level": 1, "moveId": "leer" },
            { "level": 8, "moveId": "poison_sting" },
            { "level": 13, "moveId": "bite" },
            { "level": 20, "moveId": "glare" },
            { "level": 25, "moveId": "screech" },
            { "level": 32, "moveId": "acid" },
            { "level": 37, "moveId": "haze" }
        ],
        "evolution": {
            "next": [{ "targetSpeciesId": "24", "level": 22 }]
        },
        "possibleAbilities": ["Intimidate", "Shed Skin"],
        "catchRate": 255,
        "expYield": 58,
        "assets": {
            "front": "data/pokemon/images/23/front.png",
            "back": "data/pokemon/images/23/back.png",
            "icon": "data/pokemon/icons/23.png",
            "overworld": "data/pokemon/overworld/23.png",
            "shinyFront": "data/pokemon/images/23/shiny_front.png",
            "shinyBack": "data/pokemon/images/23/shiny_back.png"
        }
    },
    "24": {
        "id": "24",
        "name": "Arbok",
        "types": ["Poison"],
        "baseStats": {
            "hp": 60,
            "attack": 85,
            "defense": 69,
            "spAttack": 65,
            "spDefense": 79,
            "speed": 80
        },
        "learnset": [
            { "level": 1, "moveId": "wrap" },
            { "level": 1, "moveId": "leer" },
            { "level": 1, "moveId": "poison_sting" },
            { "level": 1, "moveId": "bite" },
            { "level": 8, "moveId": "poison_sting" },
            { "level": 13, "moveId": "bite" },
            { "level": 20, "moveId": "glare" },
            { "level": 28, "moveId": "screech" },
            { "level": 38, "moveId": "acid" },
            { "level": 46, "moveId": "haze" }
        ],
        "evolution": {
            "preEvolutionId": "23"
        },
        "possibleAbilities": ["Intimidate", "Shed Skin"],
        "catchRate": 90,
        "expYield": 147,
        "assets": {
            "front": "data/pokemon/images/24/front.png",
            "back": "data/pokemon/images/24/back.png",
            "icon": "data/pokemon/icons/24.png",
            "overworld": "data/pokemon/overworld/24.png",
            "shinyFront": "data/pokemon/images/24/shiny_front.png",
            "shinyBack": "data/pokemon/images/24/shiny_back.png"
        }
    }
};

// Add missing Pokemon to pokedex
Object.assign(pokedex, missingPokemon);

// Sort by ID
const sortedPokedex = {};
Object.keys(pokedex)
    .map(id => parseInt(id))
    .sort((a, b) => a - b)
    .forEach(id => {
        sortedPokedex[id.toString()] = pokedex[id.toString()];
    });

// Write back to file
fs.writeFileSync(pokedexPath, JSON.stringify(sortedPokedex, null, 2));

console.log('✓ Added Pokemon 21-24 to pokedex.json');
console.log('  - Spearow (#21)');
console.log('  - Fearow (#22)');
console.log('  - Ekans (#23)');
console.log('  - Arbok (#24)');
