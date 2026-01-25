
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

const effectTypes = new Set();
const movesWithoutEffects = [];
const highCritMoves = [];
const multiHitMoves = [];


const targetMoves = ['Ice Beam', 'Blizzard', 'Take Down', 'Double Slap', 'Absorb', 'Mega Drain'];
const interestingMoves = [];

Object.values(moves).forEach(move => {
    // Check specific targets
    if (targetMoves.includes(move.name)) {
        interestingMoves.push(move);
    }
    
    // Check for Mismatches
    // Freeze
    if (move.description && move.description.toLowerCase().includes('freeze')) {
        const hasStatus = move.effects && move.effects.some(e => e.type === 'Status' && e.status === 'Freeze');
        if (!hasStatus && move.category !== 'Status') {
             // console.log(`[WARNING] ${move.name} mentions 'freeze' but has no Freeze status effect.`);
        }
    }
});

console.log(JSON.stringify(interestingMoves, null, 2));

