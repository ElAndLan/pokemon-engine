const fs = require('fs');
const path = require('path');
const { Worker } = require('worker_threads');

const outputPath = path.join(__dirname, '../data/db/pokemon_moves_and_evolutions.json');

const MAX_WORKERS = 10;
const BATCH_SIZE = 5;

async function fetchAllMovesAndEvolutions() {
    console.log('Starting fetch of level-up moves and evolution data for Gen 1-5...\n');
    console.log(`Using ${MAX_WORKERS} workers for parallel processing\n`);
    console.log('Pokemon ranges:');
    console.log('  Gen 1: 1-151');
    console.log('  Gen 2: 152-251');
    console.log('  Gen 3: 252-386');
    console.log('  Gen 4: 387-493');
    console.log('  Gen 5: 494-649');
    console.log();
    
    const generationRanges = [
        { gen: 1, start: 1, end: 151 },
        { gen: 2, start: 152, end: 251 },
        { gen: 3, start: 252, end: 386 },
        { gen: 4, start: 387, end: 493 },
        { gen: 5, start: 494, end: 649 }
    ];
    
    const result = {};
    let successCount = 0;
    let failCount = 0;
    
    for (const { gen, start, end } of generationRanges) {
        console.log(`\n=== Fetching Gen ${gen} (IDs ${start}-${end}) ===`);
        
        const pokemonIds = [];
        for (let id = start; id <= end; id++) {
            pokemonIds.push({ id, generation: gen });
        }
        
        const totalPokemon = pokemonIds.length;
        let completedPokemon = 0;
        
        const batches = [];
        for (let i = 0; i < pokemonIds.length; i += BATCH_SIZE) {
            batches.push(pokemonIds.slice(i, i + BATCH_SIZE));
        }
        
        console.log(`Processing ${batches.length} batches of ${BATCH_SIZE} Pokemon each...`);
        console.log(`Progress: [${' '.repeat(50)}] 0%`);
        
        for (const batch of batches) {
            const workerPromises = batch.map(({ id, generation }) => {
                return new Promise((resolve) => {
                    const worker = new Worker(path.join(__dirname, 'fetch_worker.js'));
                    
                    worker.on('message', (message) => {
                        if (message.type === 'success') {
                            result[message.data.id] = message.data;
                            successCount++;
                            process.stdout.write('.');
                        } else if (message.type === 'error') {
                            console.error(`\n  ✗ Failed Pokemon #${message.data.id}: ${message.data.error}`);
                            failCount++;
                            process.stdout.write('X');
                        }
                        completedPokemon++;
                        
                        const progress = Math.floor((completedPokemon / totalPokemon) * 100);
                        const barLength = Math.ceil(progress / 2);
                        const progressBar = '='.repeat(barLength) + ' '.repeat(50 - barLength);
                        process.stdout.write(`\rProgress: [${progressBar}] ${progress}%`);
                        
                        worker.terminate();
                        resolve();
                    });
                    
                    worker.on('error', (error) => {
                        console.error(`\n  ✗ Worker error: ${error.message}`);
                        failCount++;
                        completedPokemon++;
                        
                        const progress = Math.floor((completedPokemon / totalPokemon) * 100);
                        const barLength = Math.ceil(progress / 2);
                        const progressBar = '='.repeat(barLength) + ' '.repeat(50 - barLength);
                        process.stdout.write(`\rProgress: [${progressBar}] ${progress}%`);
                        
                        worker.terminate();
                        resolve();
                    });
                    
                    worker.postMessage({ type: 'process', id, generation });
                });
            });
            
            await Promise.all(workerPromises);
            
            await new Promise(r => setTimeout(r, 200));
        }
        
        console.log();
        console.log(`  Gen ${gen} complete!`);
    }
    
    const sortedResult = {};
    Object.keys(result).map(id => parseInt(id)).sort((a, b) => a - b).forEach(id => {
        sortedResult[id.toString()] = result[id.toString()];
    });
    
    fs.writeFileSync(outputPath, JSON.stringify(sortedResult, null, 2));
    
    console.log(`\n=== Complete! ===`);
    console.log(`Success: ${successCount}`);
    console.log(`Failed: ${failCount}`);
    console.log(`Total Pokemon: ${Object.keys(sortedResult).length}`);
    console.log(`Output: ${outputPath}`);
}

fetchAllMovesAndEvolutions().catch(console.error);
