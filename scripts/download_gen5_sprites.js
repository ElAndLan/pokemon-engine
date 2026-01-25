const fs = require('fs');
const path = require('path');
const https = require('https');

const DEST_DIR = path.join(__dirname, '../data/pokemon/images');

// Gen 5 Pokemon (494-649) - Using Black/White sprites
const gen5Pokemon = {
    494: 'victini', 495: 'snivy', 496: 'servine', 497: 'serperior',
    498: 'tepig', 499: 'pignite', 500: 'emboar',
    501: 'oshawott', 502: 'dewott', 503: 'samurott',
    504: 'patrat', 505: 'watchog', 506: 'lillipup', 507: 'herdier', 508: 'stoutland',
    509: 'purrloin', 510: 'liepard', 511: 'pansage', 512: 'simisage',
    513: 'pansear', 514: 'simisear', 515: 'panpour', 516: 'simipour',
    517: 'munna', 518: 'musharna', 519: 'pidove', 520: 'tranquill', 521: 'unfezant',
    522: 'blitzle', 523: 'zebstrika', 524: 'roggenrola', 525: 'boldore', 526: 'gigalith',
    527: 'woobat', 528: 'swoobat', 529: 'drilbur', 530: 'excadrill',
    531: 'audino', 532: 'timburr', 533: 'gurdurr', 534: 'conkeldurr',
    535: 'tympole', 536: 'palpitoad', 537: 'seismitoad',
    538: 'throh', 539: 'sawk', 540: 'sewaddle', 541: 'swadloon', 542: 'leavanny',
    543: 'venipede', 544: 'whirlipede', 545: 'scolipede',
    546: 'cottonee', 547: 'whimsicott', 548: 'petilil', 549: 'lilligant',
    550: 'basculin', 551: 'sandile', 552: 'krokorok', 553: 'krookodile',
    554: 'darumaka', 555: 'darmanitan', 556: 'maractus', 557: 'dwebble', 558: 'crustle',
    559: 'scraggy', 560: 'scrafty', 561: 'sigilyph', 562: 'yamask', 563: 'cofagrigus',
    564: 'tirtouga', 565: 'carracosta', 566: 'archen', 567: 'archeops',
    568: 'trubbish', 569: 'garbodor', 570: 'zorua', 571: 'zoroark',
    572: 'minccino', 573: 'cinccino', 574: 'gothita', 575: 'gothorita', 576: 'gothitelle',
    577: 'solosis', 578: 'duosion', 579: 'reuniclus',
    580: 'ducklett', 581: 'swanna', 582: 'vanillite', 583: 'vanillish', 584: 'vanilluxe',
    585: 'deerling', 586: 'sawsbuck', 587: 'emolga', 588: 'karrablast', 589: 'escavalier',
    590: 'foongus', 591: 'amoonguss', 592: 'frillish', 593: 'jellicent',
    594: 'alomomola', 595: 'joltik', 596: 'galvantula', 597: 'ferroseed', 598: 'ferrothorn',
    599: 'klink', 600: 'klang', 601: 'klinklang', 602: 'tynamo', 603: 'eelektrik', 604: 'eelektross',
    605: 'elgyem', 606: 'beheeyem', 607: 'litwick', 608: 'lampent', 609: 'chandelure',
    610: 'axew', 611: 'fraxure', 612: 'haxorus', 613: 'cubchoo', 614: 'beartic',
    615: 'cryogonal', 616: 'shelmet', 617: 'accelgor', 618: 'stunfisk',
    619: 'mienfoo', 620: 'mienshao', 621: 'druddigon', 622: 'golett', 623: 'golurk',
    624: 'pawniard', 625: 'bisharp', 626: 'bouffalant', 627: 'rufflet', 628: 'braviary',
    629: 'vullaby', 630: 'mandibuzz', 631: 'heatmor', 632: 'durant',
    633: 'deino', 634: 'zweilous', 635: 'hydreigon',
    636: 'larvesta', 637: 'volcarona', 638: 'cobalion', 639: 'terrakion', 640: 'virizion',
    641: 'tornadus', 642: 'thundurus', 643: 'reshiram', 644: 'zekrom',
    645: 'landorus', 646: 'kyurem', 647: 'keldeo', 648: 'meloetta', 649: 'genesect'
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

async function downloadGen5Sprites() {
    console.log('Starting Gen 5 sprite download (494-649)...');
    console.log('Using Black/White sprites\n');
    let successCount = 0;

    for (const [id, name] of Object.entries(gen5Pokemon)) {
        const paddedId = id.padStart(3, '0');
        const folder = path.join(DEST_DIR, paddedId);
        if (!fs.existsSync(folder)) fs.mkdirSync(folder, { recursive: true });

        const variants = [
            { type: 'front', url: `https://img.pokemondb.net/sprites/black-white/normal/${name}.png` },
            { type: 'back', url: `https://img.pokemondb.net/sprites/black-white/back-normal/${name}.png` },
            { type: 'shiny_front', url: `https://img.pokemondb.net/sprites/black-white/shiny/${name}.png` },
            { type: 'shiny_back', url: `https://img.pokemondb.net/sprites/black-white/back-shiny/${name}.png` }
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

    console.log(`\n✓ Complete! ${successCount}/156 Pokemon, ${successCount * 4} sprite files`);
}

downloadGen5Sprites().catch(console.error);
