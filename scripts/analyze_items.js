
const fs = require('fs');
const path = require('path');

// Mock Item Data Loading
const itemsPath = path.join(__dirname, '../data/db/items.json');
const itemsRaw = fs.readFileSync(itemsPath, 'utf8');
const items = JSON.parse(itemsRaw);

// Re-implement the parsing logic from ItemHandler.ts to test coverage
function analyzeItem(item) {
    const effectTextRaw = item.effect || item.description || "";
    const effectText = effectTextRaw.toLowerCase();
    const id = item.id;
    
    const analysis = {
        id: item.id,
        name: item.name,
        category: item.category,
        effectRaw: effectTextRaw,
        handlers: []
    };

    if (item.category !== 'medicine') return null; // Only checking medicine for now as per user complaint

    // 1. Rare Candy / Level Up
    if (id === 'rare-candy' || effectText.includes('level up') || effectText.includes('raises the level')) {
        analysis.handlers.push('LEVEL_UP');
    }

    // 2. Revive
    if (effectText.includes('revive') || id.includes('revive')) {
        analysis.handlers.push('REVIVE');
    }

    // 3. HP Restoration
    let hpRestored = 0;
    if (id === 'full-restore' || id === 'max-potion' || effectText.includes('fully restores') || effectText.includes('restores hp to full')) {
        hpRestored = 9999;
        analysis.handlers.push('HEAL_FULL');
    } else {
        const numbers = effectText.match(/(\d+)/);
        if (numbers) {
            hpRestored = parseInt(numbers[0]);
            // Check context to ensure it's not "cures status" or something else with a number
            if (effectText.includes('hp') || effectText.includes('health') || effectText.includes('restores')) {
                 analysis.handlers.push(`HEAL_${hpRestored}`);
            }
        }
    }

    // 4. Status Healing
    if (id === 'full-heal' || id === 'full-restore' || id === 'heal-powder' || id === 'lava-cookie' || id === 'old-gateau' || id === 'casteliacone' || id === 'lumiose-galette' || id === 'shalour-sable' || id === 'big-malasada' || id === 'pewter-crunchies' || id === 'rage-candy-bar' || effectText.includes('all status') || effectText.includes('any status')) {
        analysis.handlers.push('CURE_ALL');
    } else if (id === 'antidote' || effectText.includes('poison')) {
        analysis.handlers.push('CURE_POISON');
    } else if (id === 'burn-heal' || effectText.includes('burn')) {
        analysis.handlers.push('CURE_BURN');
    } else if (id === 'ice-heal' || effectText.includes('defrosts') || effectText.includes('thaws') || effectText.includes('freeze')) {
        analysis.handlers.push('CURE_FREEZE');
    } else if (id === 'awakening' || effectText.includes('wakes') || effectText.includes('sleep')) {
        analysis.handlers.push('CURE_SLEEP');
    } else if (id === 'paralyze-heal' || effectText.includes('paralysis') || effectText.includes('paralyze')) {
        analysis.handlers.push('CURE_PARALYSIS');
    }

    return analysis;
}

console.log("Analyzing Medicine Items...");
const results = [];
let coveredCount = 0;
let totalCount = 0;

Object.values(items).forEach(item => {
    const result = analyzeItem(item);
    if (result) {
        totalCount++;
        if (result.handlers.length > 0) {
            coveredCount++;
        } else {
            console.log(`[UNHANDLED] ${result.name} (${result.id}) - Text: "${result.effectRaw}"`);
        }
        results.push(result);
    }
});

console.log(`Coverage: ${coveredCount}/${totalCount} (${Math.round(coveredCount/totalCount*100)}%)`);
