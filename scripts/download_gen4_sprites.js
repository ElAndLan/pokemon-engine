const fs = require('fs');
const path = require('path');
const https = require('https');

const DEST_DIR = path.join(__dirname, '../data/pokemon/images');

const gen4Pokemon = {
    387: 'turtwig', 388: 'grotle', 389: 'torterra',
    390: 'chimchar', 391: 'monferno', 392: 'infernape',
    393: 'piplup', 394: 'prinplup', 395: 'empoleon',
    396: 'starly', 397: 'staravia', 398: 'staraptor',
    399: 'bidoof', 400: 'bibarel', 401: 'kricketot', 402: 'kricketune',
    403: 'shinx', 404: 'luxio', 405: 'luxray',
    406: 'budew', 407: 'roserade', 408: 'cranidos', 409: 'rampardos',
    410: 'shieldon', 411: 'bastiodon', 412: 'burmy', 413: 'wormadam', 414: 'mothim',
    415: 'combee', 416: 'vespiquen', 417: 'pachirisu', 418: 'buizel', 419: 'floatzel',
    420: 'cherubi', 421: 'cherrim', 422: 'shellos', 423: 'gastrodon',
    424: 'ambipom', 425: 'drifloon', 426: 'drifblim', 427: 'buneary', 428: 'lopunny',
    429: 'mismagius', 430: 'honchkrow', 431: 'glameow', 432: 'purugly',
    433: 'chingling', 434: 'stunky', 435: 'skuntank', 436: 'bronzor', 437: 'bronzong',
    438: 'bonsly', 439: 'mime-jr', 440: 'happiny', 441: 'chatot', 442: 'spiritomb',
    443: 'gible', 444: 'gabite', 445: 'garchomp',
    446: 'munchlax', 447: 'riolu', 448: 'lucario',
    449: 'hippopotas', 450: 'hippowdon', 451: 'skorupi', 452: 'drapion',
    453: 'croagunk', 454: 'toxicroak', 455: 'carnivine',
    456: 'finneon', 457: 'lumineon', 458: 'mantyke',
    459: 'snover', 460: 'abomasnow', 461: 'weavile', 462: 'magnezone',
    463: 'lickilicky', 464: 'rhyperior', 465: 'tangrowth', 466: 'electivire',
    467: 'magmortar', 468: 'togekiss', 469: 'yanmega', 470: 'leafeon', 471: 'glaceon',
    472: 'gliscor', 473: 'mamoswine', 474: 'porygon-z', 475: 'gallade', 476: 'probopass',
    477: 'dusknoir', 478: 'froslass', 479: 'rotom',
    480: 'uxie', 481: 'mesprit', 482: 'azelf',
    483: 'dialga', 484: 'palkia', 485: 'heatran', 486: 'regigigas',
    487: 'giratina', 488: 'cresselia', 489: 'phione', 490: 'manaphy',
    491: 'darkrai', 492: 'shaymin', 493: 'arceus'
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

async function downloadGen4Sprites() {
    console.log('Starting Gen 4 sprite download (387-493)...\n');
    let successCount = 0;

    for (const [id, name] of Object.entries(gen4Pokemon)) {
        const paddedId = id.padStart(3, '0');
        const folder = path.join(DEST_DIR, paddedId);
        if (!fs.existsSync(folder)) fs.mkdirSync(folder, { recursive: true });

        const variants = [
            { type: 'front', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/normal/${name}.png` },
            { type: 'back', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/back-normal/${name}.png` },
            { type: 'shiny_front', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/shiny/${name}.png` },
            { type: 'shiny_back', url: `https://img.pokemondb.net/sprites/heartgold-soulsilver/back-shiny/${name}.png` }
        ];

        let pokemonSuccess = true;
        for (const variant of variants) {
            const success = await downloadImage(variant.url, path.join(folder, `${variant.type}.png`));
            if (!success) pokemonSuccess = false;
            await new Promise(r => setTimeout(r, 100));
        }

        if (pokemonSuccess) {
            successCount++;
            console.log(`✓ ${name} (#${id})`);
        }
    }

    console.log(`\n✓ Complete! ${successCount}/107 Pokemon, ${successCount * 4} sprite files`);
}

downloadGen4Sprites().catch(console.error);
