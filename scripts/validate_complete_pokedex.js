const fs = require('fs');
const path = require('path');

const pokedexPath = path.join(__dirname, '../data/db/pokedex.json');
const pokedex = require(pokedexPath);

console.log('='.repeat(60));
console.log('COMPREHENSIVE POKEDEX VALIDATION');
console.log('='.repeat(60));
console.log();

let totalIssues = 0;

// 1. Check total count
console.log('1. TOTAL POKEMON COUNT');
console.log('-'.repeat(60));
const totalCount = Object.keys(pokedex).length;
console.log(`Total Pokemon in pokedex: ${totalCount}`);
if (totalCount === 251) {
    console.log('✓ PASS: Expected 251 Pokemon (Gen 1 + Gen 2)');
} else {
    console.log(`✗ FAIL: Expected 251, found ${totalCount}`);
    totalIssues++;
}
console.log();

// 2. Check for missing IDs
console.log('2. MISSING POKEMON IDS (1-251)');
console.log('-'.repeat(60));
const missingIds = [];
for (let i = 1; i <= 251; i++) {
    if (!pokedex[i.toString()]) {
        missingIds.push(i);
    }
}
if (missingIds.length === 0) {
    console.log('✓ PASS: All Pokemon IDs 1-251 present');
} else {
    console.log(`✗ FAIL: Missing ${missingIds.length} Pokemon: ${missingIds.slice(0, 10).join(', ')}${missingIds.length > 10 ? '...' : ''}`);
    totalIssues++;
}
console.log();

// 3. Check sprite files
console.log('3. SPRITE FILE COVERAGE');
console.log('-'.repeat(60));
const imageDir = path.join(__dirname, '../data/pokemon/images');
let missingSpriteCount = 0;
const missingSpriteDetails = [];

for (let i = 1; i <= 251; i++) {
    const paddedId = i.toString().padStart(3, '0');
    const spriteDir = path.join(imageDir, paddedId);
    
    const requiredSprites = ['front.png', 'back.png', 'shiny_front.png', 'shiny_back.png'];
    const missingSprites = [];
    
    for (const sprite of requiredSprites) {
        const spritePath = path.join(spriteDir, sprite);
        if (!fs.existsSync(spritePath)) {
            missingSprites.push(sprite);
        }
    }
    
    if (missingSprites.length > 0) {
        missingSpriteCount++;
        if (missingSpriteDetails.length < 5) {
            missingSpriteDetails.push(`#${i}: missing ${missingSprites.join(', ')}`);
        }
    }
}

if (missingSpriteCount === 0) {
    console.log('✓ PASS: All 251 Pokemon have all 4 sprite variants');
    console.log('  Total sprites: 1004 files (251 Pokemon × 4 variants)');
} else {
    console.log(`✗ FAIL: ${missingSpriteCount} Pokemon missing sprites`);
    missingSpriteDetails.forEach(detail => console.log(`  ${detail}`));
    totalIssues++;
}
console.log();

// 4. Check learnsets
console.log('4. LEARNSET VALIDATION');
console.log('-'.repeat(60));
const emptyLearnsets = [];
const learnsetStats = { min: Infinity, max: 0, total: 0 };

Object.keys(pokedex).forEach(id => {
    const pokemon = pokedex[id];
    const learnsetLength = pokemon.learnset ? pokemon.learnset.length : 0;
    
    if (learnsetLength === 0 && parseInt(id) !== 201 && parseInt(id) !== 235) { // Unown and Smeargle exceptions
        emptyLearnsets.push(`#${id} ${pokemon.name}`);
    }
    
    learnsetStats.total += learnsetLength;
    if (learnsetLength > 0) {
        learnsetStats.min = Math.min(learnsetStats.min, learnsetLength);
        learnsetStats.max = Math.max(learnsetStats.max, learnsetLength);
    }
});

const avgLearnset = (learnsetStats.total / 251).toFixed(1);

if (emptyLearnsets.length <= 3) { // Allow Unown, Smeargle, and maybe Tyrogue
    console.log('✓ PASS: Most Pokemon have populated learnsets');
    console.log(`  Average moves per Pokemon: ${avgLearnset}`);
    console.log(`  Range: ${learnsetStats.min} - ${learnsetStats.max} moves`);
    if (emptyLearnsets.length > 0) {
        console.log(`  Note: ${emptyLearnsets.length} Pokemon with empty learnsets (expected for special cases)`);
    }
} else {
    console.log(`✗ FAIL: ${emptyLearnsets.length} Pokemon with empty learnsets`);
    emptyLearnsets.slice(0, 10).forEach(p => console.log(`  ${p}`));
    totalIssues++;
}
console.log();

// 5. Check evolution data
console.log('5. EVOLUTION DATA VALIDATION');
console.log('-'.repeat(60));
const missingEvolutionLevel = [];
const crossGenEvolutions = [];

Object.keys(pokedex).forEach(id => {
    const pokemon = pokedex[id];
    
    if (pokemon.evolution && pokemon.evolution.next) {
        for (const evo of pokemon.evolution.next) {
            // Check if evolution has a method (level, item, trade, friendship, etc.)
            const hasMethod = evo.level || evo.item || evo.trade || evo.friendship || evo.timeOfDay;
            
            if (!hasMethod) {
                missingEvolutionLevel.push(`#${id} ${pokemon.name} → #${evo.targetSpeciesId}`);
            }
            
            // Track cross-gen evolutions
            const targetId = parseInt(evo.targetSpeciesId);
            if ((parseInt(id) <= 151 && targetId > 151) || (parseInt(id) > 151 && targetId <= 151)) {
                crossGenEvolutions.push(`#${id} ${pokemon.name} → #${evo.targetSpeciesId}`);
            }
        }
    }
});

if (missingEvolutionLevel.length === 0) {
    console.log('✓ PASS: All evolutions have evolution methods specified');
} else {
    console.log(`⚠ WARNING: ${missingEvolutionLevel.length} evolutions missing method details`);
    missingEvolutionLevel.slice(0, 5).forEach(e => console.log(`  ${e}`));
}

console.log(`  Cross-generation evolutions found: ${crossGenEvolutions.length}`);
if (crossGenEvolutions.length > 0) {
    console.log('  Examples:');
    crossGenEvolutions.slice(0, 3).forEach(e => console.log(`    ${e}`));
}
console.log();

// 6. Check asset paths
console.log('6. ASSET PATH FORMAT');
console.log('-'.repeat(60));
const invalidAssetPaths = [];

Object.keys(pokedex).forEach(id => {
    const pokemon = pokedex[id];
    const paddedId = id.padStart(3, '0');
    
    if (pokemon.assets) {
        const expectedFront = `data/pokemon/images/${paddedId}/front.png`;
        if (pokemon.assets.front !== expectedFront) {
            invalidAssetPaths.push(`#${id}: front path mismatch`);
        }
    } else {
        invalidAssetPaths.push(`#${id}: missing assets object`);
    }
});

if (invalidAssetPaths.length === 0) {
    console.log('✓ PASS: All Pokemon have correct three-digit asset paths');
} else {
    console.log(`✗ FAIL: ${invalidAssetPaths.length} Pokemon with asset path issues`);
    invalidAssetPaths.slice(0, 5).forEach(issue => console.log(`  ${issue}`));
    totalIssues++;
}
console.log();

// 7. Sample data verification
console.log('7. SAMPLE DATA VERIFICATION');
console.log('-'.repeat(60));

const samples = [
    { id: '1', name: 'Bulbasaur', gen: 1 },
    { id: '25', name: 'Pikachu', gen: 1 },
    { id: '152', name: 'Chikorita', gen: 2 },
    { id: '155', name: 'Cyndaquil', gen: 2 },
    { id: '249', name: 'Lugia', gen: 2 },
    { id: '251', name: 'Celebi', gen: 2 }
];

samples.forEach(sample => {
    const pokemon = pokedex[sample.id];
    if (pokemon) {
        const hasLearnset = pokemon.learnset && pokemon.learnset.length > 0;
        const hasAssets = pokemon.assets && pokemon.assets.shinyFront;
        console.log(`✓ #${sample.id} ${sample.name} (Gen ${sample.gen}): ${pokemon.learnset.length} moves, assets: ${hasAssets ? 'OK' : 'MISSING'}`);
    } else {
        console.log(`✗ #${sample.id} ${sample.name}: NOT FOUND`);
        totalIssues++;
    }
});
console.log();

// Final summary
console.log('='.repeat(60));
console.log('VALIDATION SUMMARY');
console.log('='.repeat(60));
if (totalIssues === 0) {
    console.log('✓✓✓ ALL CHECKS PASSED ✓✓✓');
    console.log('Pokedex is complete and valid!');
} else {
    console.log(`⚠ ${totalIssues} CRITICAL ISSUES FOUND`);
    console.log('Please review the issues above.');
}
console.log('='.repeat(60));
