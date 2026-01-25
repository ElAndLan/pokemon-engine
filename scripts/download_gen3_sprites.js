const fs = require('fs');
const path = require('path');
const https = require('https');

const DEST_DIR = path.join(__dirname, '../data/pokemon/images');

// Gen 3 Pokemon names (252-386)
const gen3Pokemon = {
    252: 'treecko', 253: 'grovyle', 254: 'sceptile',
    255: 'torchic', 256: 'combusken', 257: 'blaziken',
    258: 'mudkip', 259: 'marshtomp', 260: 'swampert',
    261: 'poochyena', 262: 'mightyena', 263: 'zigzagoon', 264: 'linoone',
    265: 'wurmple', 266: 'silcoon', 267: 'beautifly', 268: 'cascoon', 269: 'dustox',
    270: 'lotad', 271: 'lombre', 272: 'ludicolo',
    273: 'seedot', 274: 'nuzleaf', 275: 'shiftry',
    276: 'taillow', 277: 'swellow', 278: 'wingull', 279: 'pelipper',
    280: 'ralts', 281: 'kirlia', 282: 'gardevoir',
    283: 'surskit', 284: 'masquerain', 285: 'shroomish', 286: 'breloom',
    287: 'slakoth', 288: 'vigoroth', 289: 'slaking',
    290: 'nincada', 291: 'ninjask', 292: 'shedinja',
    293: 'whismur', 294: 'loudred', 295: 'exploud',
    296: 'makuhita', 297: 'hariyama', 298: 'azurill',
    299: 'nosepass', 300: 'skitty', 301: 'delcatty',
    302: 'sableye', 303: 'mawile', 304: 'aron', 305: 'lairon', 306: 'aggron',
    307: 'meditite', 308: 'medicham', 309: 'electrike', 310: 'manectric',
    311: 'plusle', 312: 'minun', 313: 'volbeat', 314: 'illumise',
    315: 'roselia', 316: 'gulpin', 317: 'swalot',
    318: 'carvanha', 319: 'sharpedo', 320: 'wailmer', 321: 'wailord',
    322: 'numel', 323: 'camerupt', 324: 'torkoal',
    325: 'spoink', 326: 'grumpig', 327: 'spinda',
    328: 'trapinch', 329: 'vibrava', 330: 'flygon',
    331: 'cacnea', 332: 'cacturne', 333: 'swablu', 334: 'altaria',
    335: 'zangoose', 336: 'seviper', 337: 'lunatone', 338: 'solrock',
    339: 'barboach', 340: 'whiscash', 341: 'corphish', 342: 'crawdaunt',
    343: 'baltoy', 344: 'claydol', 345: 'lileep', 346: 'cradily',
    347: 'anorith', 348: 'armaldo', 349: 'feebas', 350: 'milotic',
    351: 'castform', 352: 'kecleon', 353: 'shuppet', 354: 'banette',
    355: 'duskull', 356: 'dusclops', 357: 'tropius',
    358: 'chimecho', 359: 'absol', 360: 'wynaut',
    361: 'snorunt', 362: 'glalie', 363: 'spheal', 364: 'sealeo', 365: 'walrein',
    366: 'clamperl', 367: 'huntail', 368: 'gorebyss',
    369: 'relicanth', 370: 'luvdisc', 371: 'bagon', 372: 'shelgon', 373: 'salamence',
    374: 'beldum', 375: 'metang', 376: 'metagross',
    377: 'regirock', 378: 'regice', 379: 'registeel',
    380: 'latias', 381: 'latios', 382: 'kyogre', 383: 'groudon',
    384: 'rayquaza', 385: 'jirachi', 386: 'deoxys'
};

async function downloadImage(url, destPath) {
    return new Promise((resolve) => {
        const file = fs.createWriteStream(destPath);
        https.get(url, (response) => {
            if (response.statusCode === 200) {
                response.pipe(file);
                file.on('finish', () => file.close(() => resolve(true)));
            } else {
                file.close();
                fs.unlink(destPath, () => {});
                resolve(false);
            }
        }).on('error', () => {
            fs.unlink(destPath, () => {});
            resolve(false);
        });
    });
}

async function downloadGen3Sprites() {
    console.log('Starting Gen 3 sprite download...\n');
    console.log(`Downloading sprites for ${Object.keys(gen3Pokemon).length} Pokemon (252-386)\n`);

    let successCount = 0;

    for (const [id, name] of Object.entries(gen3Pokemon)) {
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
            if (!success) pokemonSuccess = false;
            await new Promise(r => setTimeout(r, 100));
        }

        if (pokemonSuccess) {
            successCount++;
            console.log(`✓ ${name} (#${id})`);
        }
    }

    console.log(`\n✓ Complete! ${successCount}/135 Pokemon, ${successCount * 4} sprite files`);
}

downloadGen3Sprites().catch(console.error);
