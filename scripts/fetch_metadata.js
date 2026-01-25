const fs = require('fs');
const path = require('path');
const https = require('https');

const ABILITIES_URL = 'https://pokeapi.co/api/v2/ability?limit=1000';
const OUTPUT_FILE = path.join(__dirname, '../data/db/abilities.json');

function fetchJson(url) {
    return new Promise((resolve, reject) => {
        https.get(url, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    resolve(JSON.parse(data));
                } catch (e) {
                    reject(e);
                }
            });
        }).on('error', reject);
    });
}

function normalizeName(name) {
    // Convert 'blaze' to 'Blaze'
    return name.split('-').map(word => word.charAt(0).toUpperCase() + word.slice(1)).join(' ');
}

async function main() {
    console.log(`Fetching ability list from ${ABILITIES_URL}...`);
    try {
        const listData = await fetchJson(ABILITIES_URL);
        
        const abilities = {};
        const total = listData.results.length;
        console.log(`Found ${total} abilities. Processing details...`);

        let count = 0;
        // Limit concurrency to avoid extensive rate limiting, but batching promises is faster than serial
        // We'll do serial for safety as this is a one-off script
        for (const entry of listData.results) {
            count++;
            if (count % 10 === 0) process.stdout.write(`\rProgress: ${count}/${total} (${Math.round(count/total*100)}%)`);
            
            try {
                const detail = await fetchJson(entry.url);
                const englishEntry = detail.effect_entries.find(e => e.language.name === 'en');
                // Fallback to flavor text if effect_entries is empty (common in newer gens on PokeAPI sometimes)
                let shortEffect = englishEntry ? englishEntry.short_effect : '';
                let longEffect = englishEntry ? englishEntry.effect : '';

                if (!shortEffect && detail.flavor_text_entries) {
                    const flavor = detail.flavor_text_entries.find(e => e.language.name === 'en');
                    if (flavor) shortEffect = flavor.flavor_text.replace(/\n/g, ' ');
                }

                if (!shortEffect) shortEffect = 'No description available.';

                abilities[detail.name] = {
                    id: detail.name,
                    name: detail.names.find(n => n.language.name === 'en')?.name || normalizeName(detail.name),
                    description: shortEffect,
                    longDescription: longEffect || shortEffect,
                    implemented: false
                };
            } catch (e) {
                console.error(`\nFailed to fetch ${entry.name}:`, e.message);
            }
            
            // tiny delay
            await new Promise(r => setTimeout(r, 20));
        }
        
        console.log('\nWriting to file...');
        fs.writeFileSync(OUTPUT_FILE, JSON.stringify(abilities, null, 2));
        console.log(`Done! Saved ${Object.keys(abilities).length} abilities to ${OUTPUT_FILE}`);
        
    } catch (e) {
        console.error("Fatal error:", e);
    }
}

main();
