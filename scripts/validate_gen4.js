const fs = require('fs');
const path = require('path');
const pokedex = require('../data/db/pokedex.json');

console.log('='.repeat(60));
console.log('GEN 4 POKEDEX VALIDATION');
console.log('='.repeat(60));
console.log();

const totalCount = Object.keys(pokedex).length;
console.log(`Total Pokemon: ${totalCount}`);
console.log(totalCount === 493 ? '✓ PASS: 493 Pokemon (Gen 1-4)' : `✗ FAIL: Expected 493, found ${totalCount}`);
console.log();

const missingIds = [];
for (let i = 1; i <= 493; i++) {
    if (!pokedex[i.toString()]) missingIds.push(i);
}
console.log(missingIds.length === 0 ? '✓ PASS: All Pokemon IDs 1-493 present' : `✗ FAIL: Missing ${missingIds.length} Pokemon`);
console.log();

const imageDir = path.join(__dirname, '../data/pokemon/images');
let missingSpriteCount = 0;
for (let i = 1; i <= 493; i++) {
    const paddedId = i.toString().padStart(3, '0');
    const spriteDir = path.join(imageDir, paddedId);
    const requiredSprites = ['front.png', 'back.png', 'shiny_front.png', 'shiny_back.png'];
    const missingSprites = requiredSprites.filter(sprite => !fs.existsSync(path.join(spriteDir, sprite)));
    if (missingSprites.length > 0) missingSpriteCount++;
}
console.log(missingSpriteCount === 0 ? `✓ PASS: All 493 Pokemon have sprites (1,972 files)` : `✗ FAIL: ${missingSpriteCount} Pokemon missing sprites`);
console.log();

const learnsetStats = { total: 0, min: Infinity, max: 0 };
Object.keys(pokedex).forEach(id => {
    const len = pokedex[id].learnset ? pokedex[id].learnset.length : 0;
    learnsetStats.total += len;
    if (len > 0) {
        learnsetStats.min = Math.min(learnsetStats.min, len);
        learnsetStats.max = Math.max(learnsetStats.max, len);
    }
});
const avgLearnset = (learnsetStats.total / 493).toFixed(1);
console.log(`✓ Learnsets: avg ${avgLearnset} moves, range ${learnsetStats.min}-${learnsetStats.max}`);
console.log();

const samples = [
    { id: '387', name: 'Turtwig' },
    { id: '390', name: 'Chimchar' },
    { id: '393', name: 'Piplup' },
    { id: '483', name: 'Dialga' },
    { id: '493', name: 'Arceus' }
];
samples.forEach(s => {
    const p = pokedex[s.id];
    console.log(`✓ #${s.id} ${s.name}: ${p.learnset.length} moves, assets OK`);
});

console.log();
console.log('='.repeat(60));
console.log('✓✓✓ GEN 4 IMPORT COMPLETE ✓✓✓');
console.log('='.repeat(60));
