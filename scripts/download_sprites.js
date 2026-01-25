const fs = require('fs');
const path = require('path');
const https = require('https');
const pokedex = require('../data/db/pokedex.json');

const DEST_DIR = path.join(__dirname, '../data/pokemon/images');

// Ensure destination exists
if (!fs.existsSync(DEST_DIR)) {
    fs.mkdirSync(DEST_DIR, { recursive: true });
}

// Normalize names for PokemonDB URLs
function normalizeName(name) {
    let lower = name.toLowerCase();
    
    // Special cases
    if (lower === 'nidoran♀') return 'nidoran-f';
    if (lower === 'nidoran♂') return 'nidoran-m';
    if (lower === 'mr. mime') return 'mr-mime';
    if (lower === "farfetch'd") return 'farfetchd';
    
    // Default normalization
    return lower.replace(/ /g, '-').replace(/[^a-z0-9-]/g, '');
}

async function downloadImage(url, destPath) {
    return new Promise((resolve, reject) => {
        // REMOVED existence check to force overwrite
        
        const file = fs.createWriteStream(destPath);
        https.get(url, (response) => {
            if (response.statusCode === 200) {
                response.pipe(file);
                file.on('finish', () => {
                    file.close(() => resolve(true));
                });
            } else {
                file.close();
                fs.unlink(destPath, () => {}); // Verify deletion
                console.error(`Failed to download ${url}: Status ${response.statusCode}`);
                resolve(false);
            }
        }).on('error', (err) => {
            fs.unlink(destPath, () => {});
            console.error(`Error downloading ${url}: ${err.message}`);
            resolve(false);
        });
    });
}

async function processPokemon() {
    // Only Gen 1 (1-151)
    console.log('Reading Pokedex keys...');
    const gen1Ids = Object.keys(pokedex).filter(id => parseInt(id) <= 151);
    
    console.log(`Starting download for ${gen1Ids.length} Pokemon...`);

    for (const id of gen1Ids) {
        console.log(`Processing ID: ${id}`); // Verbose log
        const entry = pokedex[id];
        const name = normalizeName(entry.name);
        const folder = path.join(DEST_DIR, id);
        
        if (!fs.existsSync(folder)) {
            fs.mkdirSync(folder, { recursive: true });
        }

        const variants = [
            { type: 'front', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/normal/${name}.png` },
            { type: 'back', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/back-normal/${name}.png` }
            // Skipping Shiny as requested by user
        ];

        for (const variant of variants) {
            const dest = path.join(folder, `${variant.type}.png`);
            
            // FORCE OVERWRITE: Do not check if file exists
            await downloadImage(variant.url, dest);
            
            // Be polite
            await new Promise(r => setTimeout(r, 100));
        }

        process.stdout.write(`\rProcessed ${id}: ${entry.name}    `);
    }
    console.log('\nDownload Complete!');
}

processPokemon();
