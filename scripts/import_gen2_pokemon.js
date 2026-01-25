const fs = require('fs');
const path = require('path');
const https = require('https');

const pokedexPath = path.join(__dirname, '../data/db/pokedex.json');
const movesPath = path.join(__dirname, '../data/db/moves.json');

const existingPokedex = require(pokedexPath);
const movesDb = require(movesPath);

// Create reverse lookup: PokeAPI name -> moveId
const moveNameMap = {};
Object.keys(movesDb).forEach(moveId => {
    const moveName = movesDb[moveId].name.toLowerCase().replace(/[^a-z0-9]/g, '');
    moveNameMap[moveName] = moveId;
});

console.log(`Loaded ${Object.keys(moveNameMap).length} moves for mapping\n`);

// Function to map PokeAPI move name to our moveId
function mapMoveName(pokeApiName) {
    const normalized = pokeApiName.toLowerCase().replace(/-/g, '_');
    
    // Direct match
    if (movesDb[normalized]) {
        return normalized;
    }
    
    // Try without special characters
    const stripped = pokeApiName.toLowerCase().replace(/[^a-z0-9]/g, '');
    if (moveNameMap[stripped]) {
        return moveNameMap[stripped];
    }
    
    // Special cases
    const specialCases = {
        'sand-attack': 'sand_attack',
        'solarbeam': 'solar_beam',
        'thunderpunch': 'thunder_punch',
        'ancientpower': 'ancient_power',
        'extremespeed': 'extreme_speed'
    };
    
    if (specialCases[pokeApiName]) {
        return specialCases[pokeApiName];
    }
    
    return null;
}

// Fetch functions
function fetchJson(url) {
    return new Promise((resolve, reject) => {
        https.get(url, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                if (res.statusCode === 200) {
                    resolve(JSON.parse(data));
                } else {
                    reject(new Error(`Failed: ${res.statusCode}`));
                }
            });
        }).on('error', reject);
    });
}

async function fetchPokemonData(id) {
    const [pokemon, species] = await Promise.all([
        fetchJson(`https://pokeapi.co/api/v2/pokemon/${id}`),
        fetchJson(`https://pokeapi.co/api/v2/pokemon-species/${id}`)
    ]);
    return { pokemon, species };
}

async function fetchEvolutionChain(url) {
    return fetchJson(url);
}

// Convert PokeAPI data to our format
async function convertToOurFormat(data, id) {
    const { pokemon, species } = data;
    const paddedId = id.toString().padStart(3, '0');
    
    // Types
    const types = pokemon.types
        .sort((a, b) => a.slot - b.slot)
        .map(t => t.type.name.charAt(0).toUpperCase() + t.type.name.slice(1));
    
    // Base stats
    const stats = {};
    pokemon.stats.forEach(s => {
        const statName = s.stat.name;
        if (statName === 'hp') stats.hp = s.base_stat;
        else if (statName === 'attack') stats.attack = s.base_stat;
        else if (statName === 'defense') stats.defense = s.base_stat;
        else if (statName === 'special-attack') stats.spAttack = s.base_stat;
        else if (statName === 'special-defense') stats.spDefense = s.base_stat;
        else if (statName === 'speed') stats.speed = s.base_stat;
    });
    
    // Learnset - level-up moves only (Gen 2: Gold/Silver/Crystal)
    const learnset = [];
    const levelUpMoves = pokemon.moves.filter(m => 
        m.version_group_details.some(v => v.move_learn_method.name === 'level-up')
    );
    
    for (const move of levelUpMoves) {
        const versionDetail = move.version_group_details.find(v => 
            v.move_learn_method.name === 'level-up' && 
            (v.version_group.name === 'gold-silver' || v.version_group.name === 'crystal')
        );
        
        if (versionDetail) {
            const moveId = mapMoveName(move.move.name);
            if (moveId) {
                learnset.push({
                    level: versionDetail.level_learned_at,
                    moveId: moveId
                });
            }
        }
    }
    
    // Sort by level
    learnset.sort((a, b) => a.level - b.level);
    
    // Evolution data
    const evolution = {};
    
    // Get pre-evolution
    if (species.evolves_from_species) {
        const preEvoId = species.evolves_from_species.url.split('/').filter(Boolean).pop();
        evolution.preEvolutionId = preEvoId;
    }
    
    // Get evolution chain for next evolution
    try {
        const chainData = await fetchEvolutionChain(species.evolution_chain.url);
        const findEvolution = (chain, currentId) => {
            if (chain.species.url.includes(`/${currentId}/`)) {
                if (chain.evolves_to.length > 0) {
                    const evolutions = [];
                    
                    for (const nextEvo of chain.evolves_to) {
                        const targetId = nextEvo.species.url.split('/').filter(Boolean).pop();
                        const evoDetail = nextEvo.evolution_details[0];
                        
                        const evoData = {
                            targetSpeciesId: targetId
                        };
                        
                        if (evoDetail.min_level) {
                            evoData.level = evoDetail.min_level;
                        }
                        if (evoDetail.item) {
                            evoData.item = evoDetail.item.name.split('-').map(w => 
                                w.charAt(0).toUpperCase() + w.slice(1)
                            ).join(' ');
                        }
                        if (evoDetail.trigger.name === 'trade') {
                            evoData.trade = true;
                        }
                        if (evoDetail.min_happiness) {
                            evoData.friendship = evoDetail.min_happiness;
                        }
                        if (evoDetail.time_of_day) {
                            evoData.timeOfDay = evoDetail.time_of_day;
                        }
                        if (evoDetail.known_move) {
                            evoData.knownMove = evoDetail.known_move.name;
                        }
                        
                        evolutions.push(evoData);
                    }
                    
                    return evolutions;
                }
            }
            
            for (const evo of chain.evolves_to) {
                const result = findEvolution(evo, currentId);
                if (result) return result;
            }
            
            return null;
        };
        
        const nextEvos = findEvolution(chainData.chain, id);
        if (nextEvos && nextEvos.length > 0) {
            evolution.next = nextEvos;
        }
    } catch (error) {
        console.log(`  Warning: Could not fetch evolution chain for #${id}`);
    }
    
    // Abilities
    const abilities = pokemon.abilities
        .filter(a => !a.is_hidden)
        .map(a => a.ability.name.split('-').map(w => 
            w.charAt(0).toUpperCase() + w.slice(1)
        ).join(' '));
    
    // Name
    const name = pokemon.name.charAt(0).toUpperCase() + pokemon.name.slice(1);
    
    return {
        id: id.toString(),
        name: name,
        types: types,
        baseStats: stats,
        learnset: learnset,
        evolution: evolution,
        possibleAbilities: abilities.length > 0 ? abilities : ['None'],
        catchRate: species.capture_rate || 255,
        expYield: pokemon.base_experience || 50,
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

async function importGen2Pokemon() {
    console.log('Starting Gen 2 Pokemon data import...\n');
    
    const gen2Pokedex = {};
    
    // Process Pokemon 152-251
    for (let id = 152; id <= 251; id++) {
        try {
            console.log(`Fetching Pokemon #${id}...`);
            const data = await fetchPokemonData(id);
            const converted = await convertToOurFormat(data, id);
            
            gen2Pokedex[id.toString()] = converted;
            console.log(`✓ ${converted.name} - ${converted.learnset.length} moves`);
            
            // Be polite to the API
            await new Promise(r => setTimeout(r, 250));
        } catch (error) {
            console.error(`✗ Failed Pokemon #${id}: ${error.message}`);
        }
    }
    
    // Merge with existing Gen 1 Pokemon (1-151)
    console.log('\nMerging with existing Gen 1 Pokemon...');
    const completePokedex = { ...existingPokedex, ...gen2Pokedex };
    
    // Sort by ID
    const sortedPokedex = {};
    Object.keys(completePokedex)
        .map(id => parseInt(id))
        .sort((a, b) => a - b)
        .forEach(id => {
            sortedPokedex[id.toString()] = completePokedex[id.toString()];
        });
    
    // Write to file
    fs.writeFileSync(pokedexPath, JSON.stringify(sortedPokedex, null, 2));
    
    console.log(`\n✓ Complete! Pokedex now has ${Object.keys(sortedPokedex).length} Pokemon (Gen 1 + Gen 2).`);
}

importGen2Pokemon().catch(console.error);
