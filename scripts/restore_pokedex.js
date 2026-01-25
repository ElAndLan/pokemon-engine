const fs = require('fs');
const path = require('path');
const https = require('https');

const pokedexPath = path.join(__dirname, '../data/db/pokedex.json');
const existingPokedex = require(pokedexPath);

// Keep existing Pokemon that are correct
const restoredPokedex = { ...existingPokedex };

console.log('Starting to restore missing Pokemon data...\n');
console.log(`Current Pokemon count: ${Object.keys(existingPokedex).length}`);

// Function to fetch data from PokeAPI
function fetchPokemonData(id) {
    return new Promise((resolve, reject) => {
        const url = `https://pokeapi.co/api/v2/pokemon/${id}`;
        https.get(url, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                if (res.statusCode === 200) {
                    resolve(JSON.parse(data));
                } else {
                    reject(new Error(`Failed to fetch Pokemon ${id}: ${res.statusCode}`));
                }
            });
        }).on('error', reject);
    });
}

function fetchSpeciesData(id) {
    return new Promise((resolve, reject) => {
        const url = `https://pokeapi.co/api/v2/pokemon-species/${id}`;
        https.get(url, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                if (res.statusCode === 200) {
                    resolve(JSON.parse(data));
                } else {
                    reject(new Error(`Failed to fetch species ${id}: ${res.statusCode}`));
                }
            });
        }).on('error', reject);
    });
}

// Convert PokeAPI data to our format
function convertToOurFormat(pokemonData, speciesData, id) {
    const paddedId = id.toString().padStart(3, '0');
    
    // Extract types
    const types = pokemonData.types
        .sort((a, b) => a.slot - b.slot)
        .map(t => t.type.name.charAt(0).toUpperCase() + t.type.name.slice(1));
    
    // Extract base stats
    const stats = {};
    pokemonData.stats.forEach(s => {
        const statName = s.stat.name;
        if (statName === 'hp') stats.hp = s.base_stat;
        else if (statName === 'attack') stats.attack = s.base_stat;
        else if (statName === 'defense') stats.defense = s.base_stat;
        else if (statName === 'special-attack') stats.spAttack = s.base_stat;
        else if (statName === 'special-defense') stats.spDefense = s.base_stat;
        else if (statName === 'speed') stats.speed = s.base_stat;
    });
    
    // Extract abilities
    const abilities = pokemonData.abilities
        .filter(a => !a.is_hidden)
        .map(a => a.ability.name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' '));
    
    // Get evolution chain info (simplified for now)
    const evolution = {};
    if (speciesData.evolves_from_species) {
        const preEvoId = speciesData.evolves_from_species.url.split('/').filter(Boolean).pop();
        evolution.preEvolutionId = preEvoId;
    }
    
    // Name
    const name = pokemonData.name.charAt(0).toUpperCase() + pokemonData.name.slice(1);
    
    return {
        id: id.toString(),
        name: name,
        types: types,
        baseStats: stats,
        learnset: [], // Will need to be populated separately or left empty
        evolution: evolution,
        possibleAbilities: abilities.length > 0 ? abilities : ['None'],
        catchRate: speciesData.capture_rate || 255,
        expYield: pokemonData.base_experience || 50,
        assets: {
            front: `data/pokemon/images/${paddedId}/front.png`,
            back: `data/pokemon/images/${paddedId}/back.png`,
            icon: `data/pokemon/icons/${paddedId}.png`,
            overworld: `data/pokemon/overworld/${paddedId}.png`,
            shinyFront: `data/pokemon/images/${paddedId}/shiny_front.png`,
            shinyBack: `data/pokemon/images/${paddedId}/shiny_back.png`
        }
    };
}

async function restoreMissingPokemon() {
    const missingIds = [];
    
    // Find missing IDs (1-151)
    for (let i = 1; i <= 151; i++) {
        if (!existingPokedex[i.toString()]) {
            missingIds.push(i);
        }
    }
    
    console.log(`\nMissing ${missingIds.length} Pokemon: ${missingIds.slice(0, 10).join(', ')}${missingIds.length > 10 ? '...' : ''}\n`);
    
    for (const id of missingIds) {
        try {
            console.log(`Fetching Pokemon #${id}...`);
            const [pokemonData, speciesData] = await Promise.all([
                fetchPokemonData(id),
                fetchSpeciesData(id)
            ]);
            
            const converted = convertToOurFormat(pokemonData, speciesData, id);
            restoredPokedex[id.toString()] = converted;
            
            console.log(`✓ Added ${converted.name} (#${id})`);
            
            // Be polite to the API
            await new Promise(r => setTimeout(r, 200));
        } catch (error) {
            console.error(`✗ Failed to fetch Pokemon #${id}: ${error.message}`);
        }
    }
    
    // Sort by ID
    const sortedPokedex = {};
    Object.keys(restoredPokedex)
        .map(id => parseInt(id))
        .sort((a, b) => a - b)
        .forEach(id => {
            sortedPokedex[id.toString()] = restoredPokedex[id.toString()];
        });
    
    // Write to file
    fs.writeFileSync(pokedexPath, JSON.stringify(sortedPokedex, null, 2));
    
    console.log(`\n✓ Complete! Pokedex now has ${Object.keys(sortedPokedex).length} Pokemon.`);
}

restoreMissingPokemon().catch(console.error);
