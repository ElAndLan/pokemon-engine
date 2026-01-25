
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

let patchedCount = 0;

Object.values(moves).forEach(move => {
    let modified = false;
    const desc = move.description ? move.description.toLowerCase() : '';
    
    // --- 1. DRAIN ---
    if (desc.includes('restored by half') || desc.includes('absorbs half')) {
        if (!move.drainPercent) {
            move.drainPercent = 50;
            // Remove incorrect 'Heal' effect if present (which heals based on Max HP)
            if (move.effects) {
                move.effects = move.effects.filter(e => e.type !== 'Heal');
            }
            modified = true;
            console.log(`[PATCH] ${move.name}: Added Drain 50%`);
        }
    }

    // --- 2. RECOIL ---
    if (desc.includes('recoil')) {
        if (!move.recoil) {
            move.recoil = { type: 'Damage', percent: 25 }; // Standard default
            modified = true;
            console.log(`[PATCH] ${move.name}: Added Recoil 25%`);
        }
    }

    // --- 3. MULTI HIT ---
    if (desc.includes('2 to 5 times')) {
        if (!move.multiHit) {
            move.multiHit = { min: 2, max: 5 };
            modified = true;
            console.log(`[PATCH] ${move.name}: Added MultiHit 2-5`);
        }
    } else if (desc.includes('hits twice') || desc.includes('kick twice') || desc.includes('strike twice')) {
        if (!move.multiHit) {
            move.multiHit = { min: 2, max: 2 };
            modified = true;
            console.log(`[PATCH] ${move.name}: Added MultiHit 2`);
        }
    }

    // --- 4. CRIT RATE ---
    if (desc.includes('high critical')) {
        if (!move.critRate) {
            move.critRate = 1;
            modified = true;
            console.log(`[PATCH] ${move.name}: Added High Crit`);
        }
    }

    // --- 5. MISSING STATUS EFFECTS ---
    // Helper to check and add status
    const ensureStatus = (keyword, statusName, chance) => {
        if (desc.includes(keyword) && !desc.includes('cures') && !desc.includes('heals')) {
             const hasStatus = move.effects && move.effects.some(e => e.type === 'Status' && e.status === statusName);
             if (!hasStatus && move.category !== 'Status') {
                 // Add the effect
                 if (!move.effects) move.effects = [];
                 move.effects.push({
                     type: 'Status',
                     status: statusName,
                     chance: chance
                 });
                 modified = true;
                 console.log(`[PATCH] ${move.name}: Added missing ${statusName} effect (${chance}%)`);
             }
        }
    };

    ensureStatus('may freeze', 'Freeze', 10);
    ensureStatus('may burn', 'Burn', 10);
    ensureStatus('may poison', 'Poison', 10); // Standard poison
    ensureStatus('may paralyze', 'Paralysis', 10);

    if (modified) patchedCount++;
});

if (patchedCount > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Successfully patched ${patchedCount} moves.`);
} else {
    console.log('No moves needed patching.');
}
