
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

const riskyMoves = [];

Object.values(moves).forEach(move => {
    if (!move.description) return;
    const desc = move.description.toLowerCase();
    
    if (desc.includes('may ')) {
        // This move SHOULD have a chance property in at least one effect
        // Ignore High Crit 'may' (usually "may execute high crit" or irrelevant text? 
        // Actually "High critical hit ratio" doesn't usually use "may").
        
        let hasChance = false;
        let hasEffect = false;

        if (move.effects) {
            move.effects.forEach(e => {
                if (e.type === 'Status' || e.type === 'StatChange' || e.type === 'Unique') {
                    hasEffect = true;
                    if (e.chance !== undefined && e.chance < 100) {
                        hasChance = true;
                    }
                }
            });
        }
        
        // If it says "may" but all relevant effects lack 'chance' (implying 100%) or there are no effects
        if (hasEffect && !hasChance) {
            riskyMoves.push({
                name: move.name,
                effects: move.effects,
                desc: move.description
            });
        }
    }
});

console.log(`Found ${riskyMoves.length} moves with "may" description but no explicit chance < 100%`);
if (riskyMoves.length > 0) {
    console.log(JSON.stringify(riskyMoves.slice(0, 10), null, 2));
}
