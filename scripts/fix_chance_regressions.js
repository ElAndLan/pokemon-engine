
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

let fixedCount = 0;

Object.values(moves).forEach(move => {
    // 1. Revert proper Status moves (should be 100% chance if they hit)
    if (move.category === 'Status') {
        if (move.effects) {
            move.effects.forEach(e => {
                // If we accidentally added a chance < 100 to a main status effect, remove it
                if ((e.type === 'Status' || e.type === 'StatChange') && e.chance !== undefined && e.chance < 100) {
                     delete e.chance;
                     console.log(`[FIX] Reverted ${move.name} chance to 100% (Status Move)`);
                     fixedCount++;
                }
            });
        }
    }

    // 2. Fix specific moves
    if (move.name === 'Bounce') {
        // Bounce has 30% paralysis chance
        move.effects.forEach(e => {
            if (e.status === 'Paralysis') {
                e.chance = 30;
                console.log(`[FIX] Updated Bounce paralysis to 30%`);
                fixedCount++;
            }
        });
    }
});

if (fixedCount > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Fixed ${fixedCount} moves.`);
} else {
    console.log('No fixes needed.');
}
