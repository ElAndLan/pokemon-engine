const fs = require('fs');
const path = require('path');
const https = require('https');

const DEST_DIR = path.join(__dirname, '../data/pokemon/images');

// Ensure destination exists
if (!fs.existsSync(DEST_DIR)) {
    fs.mkdirSync(DEST_DIR, { recursive: true });
}

// Gen 2 Pokemon names (152-251)
const gen2Pokemon = {
    152: 'chikorita', 153: 'bayleef', 154: 'meganium',
    155: 'cyndaquil', 156: 'quilava', 157: 'typhlosion',
    158: 'totodile', 159: 'croconaw', 160: 'feraligatr',
    161: 'sentret', 162: 'furret', 163: 'hoothoot', 164: 'noctowl',
    165: 'ledyba', 166: 'ledian', 167: 'spinarak', 168: 'ariados',
    169: 'crobat', 170: 'chinchou', 171: 'lanturn',
    172: 'pichu', 173: 'cleffa', 174: 'igglybuff',
    175: 'togepi', 176: 'togetic', 177: 'natu', 178: 'xatu',
    179: 'mareep', 180: 'flaaffy', 181: 'ampharos',
    182: 'bellossom', 183: 'marill', 184: 'azumarill',
    185: 'sudowoodo', 186: 'politoed', 187: 'hoppip', 188: 'skiploom', 189: 'jumpluff',
    190: 'aipom', 191: 'sunkern', 192: 'sunflora',
    193: 'yanma', 194: 'wooper', 195: 'quagsire',
    196: 'espeon', 197: 'umbreon', 198: 'murkrow', 199: 'slowking',
    200: 'misdreavus', 201: 'unown', 202: 'wobbuffet',
    203: 'girafarig', 204: 'pineco', 205: 'forretress',
    206: 'dunsparce', 207: 'gligar', 208: 'steelix', 209: 'snubbull', 210: 'granbull',
    211: 'qwilfish', 212: 'scizor', 213: 'shuckle',
    214: 'heracross', 215: 'sneasel', 216: 'teddiursa', 217: 'ursaring',
    218: 'slugma', 219: 'magcargo', 220: 'swinub', 221: 'piloswine',
    222: 'corsola', 223: 'remoraid', 224: 'octillery',
    225: 'delibird', 226: 'mantine', 227: 'skarmory',
    228: 'houndour', 229: 'houndoom', 230: 'kingdra',
    231: 'phanpy', 232: 'donphan', 233: 'porygon2',
    234: 'stantler', 235: 'smeargle', 236: 'tyrogue',
    237: 'hitmontop', 238: 'smoochum', 239: 'elekid', 240: 'magby',
    241: 'miltank', 242: 'blissey', 243: 'raikou', 244: 'entei', 245: 'suicune',
    246: 'larvitar', 247: 'pupitar', 248: 'tyranitar',
    249: 'lugia', 250: 'ho-oh', 251: 'celebi'
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

async function downloadGen2Sprites() {
    console.log('Starting Gen 2 sprite download...\n');
    console.log(`Downloading sprites for ${Object.keys(gen2Pokemon).length} Pokemon (152-251)\n`);

    let successCount = 0;
    let failCount = 0;

    for (const [id, name] of Object.entries(gen2Pokemon)) {
        console.log(`Processing #${id}: ${name}`);
        const paddedId = id.padStart(3, '0');
        const folder = path.join(DEST_DIR, paddedId);
        
        if (!fs.existsSync(folder)) {
            fs.mkdirSync(folder, { recursive: true });
        }

        const variants = [
            { type: 'front', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/normal/${name}.png` },
            { type: 'back', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/back-normal/${name}.png` },
            { type: 'shiny_front', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/shiny/${name}.png` },
            { type: 'shiny_back', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/back-shiny/${name}.png` }
        ];

        let pokemonSuccess = true;
        for (const variant of variants) {
            const dest = path.join(folder, `${variant.type}.png`);
            const success = await downloadImage(variant.url, dest);
            
            if (!success) {
                pokemonSuccess = false;
            }
            
            // Be polite to the server
            await new Promise(r => setTimeout(r, 100));
        }

        if (pokemonSuccess) {
            successCount++;
            console.log(`✓ ${name} (#${id}) - All 4 sprites downloaded`);
        } else {
            failCount++;
            console.log(`✗ ${name} (#${id}) - Some sprites failed`);
        }
    }

    console.log(`\n✓ Download Complete!`);
    console.log(`Success: ${successCount} Pokemon`);
    console.log(`Failed: ${failCount} Pokemon`);
    console.log(`Total sprites: ${successCount * 4} files`);
}

downloadGen2Sprites().catch(console.error);
