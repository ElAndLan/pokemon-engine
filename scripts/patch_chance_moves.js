
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

let patchedCount = 0;

Object.values(moves).forEach(move => {
    if (!move.description) return;
    const desc = move.description.toLowerCase();
    
    // Only target moves that describe a probability ("may")
    if (desc.includes('may ')) {
        if (!move.effects) return;

        let needsSave = false;
        
        // Define default chances based on keywords
        let defaultChance = 10;
        if (desc.includes('flinch')) defaultChance = 30; // Most flinch moves are 30% (Bite, Headbutt, Rock Slide)
        // Some paralysis are 30% (Body Slam), some 10% (Thunderbolt). 
        // Heuristic: If it's a "Beam" or "Bolt" -> 10%. If "Slam" or "Body" -> 30%. Default to 10% for safety (better than 100%).

        move.effects.forEach(e => {
            const isStatus = e.type === 'Status' || e.type === 'StatChange' || e.type === 'Unique';
            
            // If effect has NO chance defined, it defaults to 100% in engine. Fix this.
            if (isStatus && e.chance === undefined && e.volatileChance === undefined) {
                // Determine chance to assign
                let chance = defaultChance;
                
                // Fine tuning
                if (e.status === 'Paralysis' && (move.name === 'Body Slam' || move.name === 'Discharge' || move.name === 'Glare')) chance = 30;
                if (e.status === 'Burn' && (move.name === 'Scald' || move.name === 'Lava Plume')) chance = 30;
                if (e.status === 'Poison' && (move.name === 'Poison Jab' || move.name === 'Sludge Bomb')) chance = 30;
                
                // Apply Fix
                if (e.volatileStatus) {
                     e.volatileChance = chance;
                } else {
                     e.chance = chance;
                }
                
                needsSave = true;
                console.log(`[PATCH] ${move.name}: Added chance ${chance}% to ${e.type} (${e.status || e.volatileStatus})`);
            }
        });

        if (needsSave) patchedCount++;
    }
});

if (patchedCount > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Successfully patched ${patchedCount} moves.`);
} else {
    console.log('No moves needed chance patching.');
}
