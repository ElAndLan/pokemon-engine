const fs = require('fs');
const path = require('path');
const https = require('https');

const DEST_DIR = path.join(__dirname, '../data/pokemon/images');

// Ensure destination exists
if (!fs.existsSync(DEST_DIR)) {
    fs.mkdirSync(DEST_DIR, { recursive: true });
}

// Missing Pokemon IDs and their names
const missingPokemon = {
    '21': 'spearow',
    '22': 'fearow',
    '23': 'ekans',
    '24': 'arbok'
};

async function downloadImage(url, destPath) {
    return new Promise((resolve, reject) => {
        const file = fs.createWriteStream(destPath);
        https.get(url, (response) => {
            if (response.statusCode === 200) {
                response.pipe(file);
                file.on('finish', () => {
                    file.close(() => resolve(true));
                });
            } else {
                file.close();
                fs.unlink(destPath, () => {});
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

async function processMissingPokemon() {
    console.log('Downloading sprites for missing Pokemon...');

    for (const [id, name] of Object.entries(missingPokemon)) {
        console.log(`Processing ID: ${id} (${name})`);
        const folder = path.join(DEST_DIR, id);
        
        if (!fs.existsSync(folder)) {
            fs.mkdirSync(folder, { recursive: true });
        }

        const variants = [
            { type: 'front', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/normal/${name}.png` },
            { type: 'back', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/back-normal/${name}.png` },
            { type: 'shiny_front', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/shiny/${name}.png` },
            { type: 'shiny_back', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/back-shiny/${name}.png` }
        ];

        for (const variant of variants) {
            const dest = path.join(folder, `${variant.type}.png`);
            
            const success = await downloadImage(variant.url, dest);
            if (success) {
                console.log(`  ✓ Downloaded ${variant.type}`);
            } else {
                console.log(`  ✗ Failed ${variant.type}`);
            }
            
            // Be polite
            await new Promise(r => setTimeout(r, 100));
        }

        console.log(`Completed ${id}: ${name}`);
    }
    console.log('\nDownload Complete!');
}

processMissingPokemon();
