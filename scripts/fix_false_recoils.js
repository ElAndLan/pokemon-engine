
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

const falsePositives = [
    'Draco Meteor', 
    'Leaf Storm', 
    'Overheat', 
    'Superpower', 
    'Psycho Boost', 
    'V-create', 
    'Close Combat', 
    'Hammer Arm'
];

let fixedCount = 0;

Object.values(moves).forEach(move => {
    if (falsePositives.includes(move.name)) {
        if (move.recoil) {
            delete move.recoil;
            console.log(`[FIX] Removed false recoil from ${move.name}`);
            fixedCount++;
        }
    }
});

if (fixedCount > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Fixed ${fixedCount} false positives.`);
} else {
    console.log('No false positives found (or already fixed).');
}
