const fs = require('fs');
const path = require('path');
const https = require('https');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const existingMoves = require(movesPath);

console.log(`Current moves in database: ${Object.keys(existingMoves).length}\n`);

function fetchJson(url) {
    return new Promise((resolve, reject) => {
        https.get(url, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => res.statusCode === 200 ? resolve(JSON.parse(data)) : reject(new Error(`Failed: ${res.statusCode}`)));
        }).on('error', reject);
    });
}

async function fetchAllMoves() {
    console.log('Fetching move list from PokeAPI...\n');
    
    // Fetch all moves up to Gen 5 (moves 1-559)
    const moveList = await fetchJson('https://pokeapi.co/api/v2/move?limit=559');
    console.log(`Total moves available in Gen 1-5: ${moveList.results.length}\n`);
    
    const newMoves = {};
    let addedCount = 0;
    let skippedCount = 0;
    
    for (const moveEntry of moveList.results) {
        const moveName = moveEntry.name;
        const moveId = moveName.replace(/-/g, '_');
        
        // Skip if already exists
        if (existingMoves[moveId]) {
            skippedCount++;
            continue;
        }
        
        try {
            console.log(`Fetching: ${moveName}...`);
            const moveData = await fetchJson(moveEntry.url);
            
            // Only add moves from Gen 1-5
            if (moveData.generation.name.match(/generation-(i|ii|iii|iv|v)$/)) {
                const type = moveData.type.name.charAt(0).toUpperCase() + moveData.type.name.slice(1);
                const category = moveData.damage_class.name === 'physical' ? 'Physical' :
                                moveData.damage_class.name === 'special' ? 'Special' : 'Status';
                
                // Get English description
                const flavorText = moveData.flavor_text_entries.find(e => e.language.name === 'en');
                const description = flavorText ? flavorText.flavor_text.replace(/\n/g, ' ') : 'No description available.';
                
                newMoves[moveId] = {
                    id: moveId,
                    name: moveData.name.split('-').map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' '),
                    type: type,
                    category: category,
                    power: moveData.power || 0,
                    accuracy: moveData.accuracy || 100,
                    pp: moveData.pp || 0,
                    priority: moveData.priority || 0,
                    target: 'SelectedEnemy',
                    effects: [{ type: category === 'Status' ? 'Status' : 'Damage' }],
                    description: description
                };
                
                addedCount++;
                console.log(`  ✓ Added: ${newMoves[moveId].name} (${type}, ${category})`);
            }
            
            await new Promise(r => setTimeout(r, 100));
        } catch (error) {
            console.error(`  ✗ Failed to fetch ${moveName}: ${error.message}`);
        }
    }
    
    console.log(`\n✓ Fetched ${addedCount} new moves`);
    console.log(`  Skipped ${skippedCount} existing moves\n`);
    
    // Merge with existing moves
    const allMoves = { ...existingMoves, ...newMoves };
    
    // Sort alphabetically by ID
    const sortedMoves = {};
    Object.keys(allMoves).sort().forEach(id => {
        sortedMoves[id] = allMoves[id];
    });
    
    // Write to file
    fs.writeFileSync(movesPath, JSON.stringify(sortedMoves, null, 2));
    
    console.log(`✓ Complete! Total moves in database: ${Object.keys(sortedMoves).length}`);
    console.log(`  Previous: ${Object.keys(existingMoves).length}`);
    console.log(`  Added: ${addedCount}`);
}

fetchAllMoves().catch(console.error);
