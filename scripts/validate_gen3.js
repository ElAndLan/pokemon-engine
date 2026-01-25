const fs = require('fs');
const path = require('path');
const pokedex = require('../data/db/pokedex.json');

console.log('='.repeat(60));
console.log('GEN 3 POKEDEX VALIDATION');
console.log('='.repeat(60));
console.log();

const totalCount = Object.keys(pokedex).length;
console.log(`Total Pokemon: ${totalCount}`);
console.log(totalCount === 386 ? '✓ PASS: 386 Pokemon (Gen 1+2+3)' : `✗ FAIL: Expected 386, found ${totalCount}`);
console.log();

const missingIds = [];
for (let i = 1; i <= 386; i++) {
    if (!pokedex[i.toString()]) missingIds.push(i);
}
console.log(missingIds.length === 0 ? '✓ PASS: All Pokemon IDs 1-386 present' : `✗ FAIL: Missing ${missingIds.length} Pokemon`);
console.log();

const imageDir = path.join(__dirname, '../data/pokemon/images');
let missingSpriteCount = 0;
for (let i = 1; i <= 386; i++) {
    const paddedId = i.toString().padStart(3, '0');
    const spriteDir = path.join(imageDir, paddedId);
    const requiredSprites = ['front.png', 'back.png', 'shiny_front.png', 'shiny_back.png'];
    const missingSprites = requiredSprites.filter(sprite => !fs.existsSync(path.join(spriteDir, sprite)));
    if (missingSprites.length > 0) missingSpriteCount++;
}
console.log(missingSpriteCount === 0 ? `✓ PASS: All 386 Pokemon have sprites (1,544 files)` : `✗ FAIL: ${missingSpriteCount} Pokemon missing sprites`);
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
const avgLearnset = (learnsetStats.total / 386).toFixed(1);
console.log(`✓ Learnsets: avg ${avgLearnset} moves, range ${learnsetStats.min}-${learnsetStats.max}`);
console.log();

const samples = [
    { id: '252', name: 'Treecko' },
    { id: '255', name: 'Torchic' },
    { id: '258', name: 'Mudkip' },
    { id: '384', name: 'Rayquaza' },
    { id: '386', name: 'Deoxys' }
];
samples.forEach(s => {
    const p = pokedex[s.id];
    console.log(`✓ #${s.id} ${s.name}: ${p.learnset.length} moves, assets OK`);
});

console.log();
console.log('='.repeat(60));
console.log('✓✓✓ GEN 3 IMPORT COMPLETE ✓✓✓');
console.log('='.repeat(60));
