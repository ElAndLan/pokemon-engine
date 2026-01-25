
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

let patchedCount = 0;

// Helper to determine chance
// User said "10% to 30%".
// Heuristics:
// - "high chance" -> 30% or 50%
// - "may" default -> 10%
// - Specific moves known for 30% (Scald, Discharge, Lava Plume, Body Slam, Glare, Headbutt, Rock Slide, Bite, etc)
const getChance = (moveName, rawChance = 10) => {
    const n = moveName.toLowerCase();
    const thirtyPercenters = [
        'body slam', 'discharge', 'lava plume', 'scald', 'poison jab', 'sludge bomb', 
        'force palm', 'spark', 'dragon breath', 'lick', 'iron head', 'zen headbutt',
        'headbutt', 'rock slide', 'bite', 'water pulse', 'confuse ray', 'glare', 'stun spore' 
    ];
    if (thirtyPercenters.includes(n)) return 30;
    return rawChance;
};

Object.values(moves).forEach(move => {
    if (!move.description) return;
    const desc = move.description.toLowerCase();
    
    // Skip if not "may"
    if (!desc.includes('may ')) return;

    let modified = false;

    // 1. STATUS CONDITIONS
    const statusPatterns = [
        { regex: /may\s+.*burn/i, status: 'Burn' },
        { regex: /may\s+.*freeze/i, status: 'Freeze' },
        { regex: /may\s+.*paralyz/i, status: 'Paralysis' }, // paralyze, paralysis
        { regex: /may\s+.*poison/i, status: 'Poison' },
        { regex: /may\s+.*confus/i, volatile: 'Confusion' },
        { regex: /may\s+.*flinch/i, volatile: 'Flinch' }
    ];

    statusPatterns.forEach(pattern => {
        if (pattern.regex.test(desc)) {
            // Check if effect exists
            let effectFound = false;
            
            if (!move.effects) move.effects = [];

            move.effects.forEach(e => {
                if (pattern.status && e.type === 'Status' && e.status === pattern.status) {
                    effectFound = true;
                    if (e.chance === undefined) {
                        e.chance = getChance(move.name);
                        modified = true;
                        console.log(`[PATCH] ${move.name}: Added chance ${e.chance}% to existing ${pattern.status}`);
                    }
                }
                if (pattern.volatile && e.volatileStatus === pattern.volatile) {
                    effectFound = true;
                    if (e.volatileChance === undefined) {
                        e.volatileChance = getChance(move.name);
                        modified = true;
                        console.log(`[PATCH] ${move.name}: Added chance ${e.volatileChance}% to existing ${pattern.volatile}`);
                    }
                }
            });

            // If not found, add it
            if (!effectFound && move.category !== 'Status') {
                const chance = getChance(move.name);
                const newEffect = {
                    type: pattern.volatile ? 'Unique' : 'Status',
                    chance: pattern.volatile ? undefined : chance
                };
                
                if (pattern.status) newEffect.status = pattern.status;
                if (pattern.volatile) {
                    newEffect.volatileStatus = pattern.volatile;
                    newEffect.volatileChance = chance;
                }

                move.effects.push(newEffect);
                modified = true;
                console.log(`[PATCH] ${move.name}: INSERTED missing ${pattern.status || pattern.volatile} effect (${chance}%)`);
            }
        }
    });

    // 2. STAT CHANGES ("May lower", "May raise")
    // This is harder to parse automatically for WHICH stat.
    // Use simple regex for "lower ... [stat]"
    const statMap = {
        'attack': 'attack', 'defense': 'defense', 'speed': 'speed', 
        'accuracy': 'accuracy', 'evasion': 'evasion', 
        'sp. atk': 'spAttack', 'sp. def': 'spDefense'
    };

    if (desc.includes('may lower') || desc.includes('may raise')) {
        // Simple check if StatChange effect exists without chance
        move.effects.forEach(e => {
            if (e.type === 'StatChange' && e.chance === undefined) {
                e.chance = getChance(move.name);
                modified = true;
                console.log(`[PATCH] ${move.name}: Added chance ${e.chance}% to StatChange`);
            }
        });
    }

    if (modified) patchedCount++;
});

if (patchedCount > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Successfully patched ${patchedCount} moves.`);
} else {
    console.log('No moves needed patching.');
}
