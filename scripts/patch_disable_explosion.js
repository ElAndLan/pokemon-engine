
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

let patched = 0;

// 1. Patch Disable
if (moves['disable']) {
    const move = moves['disable'];
    // Ensure effects exist
    if (!move.effects) move.effects = [];
    
    // Check if correct effect exists
    const hasDisable = move.effects.some(e => e.type === 'Unique' && e.volatileStatus === 'Disable');
    
    if (!hasDisable) {
        // Clear old dummy effects
        move.effects = [{
            type: 'Unique',
            volatileStatus: 'Disable',
            chance: 100
        }];
        console.log('[PATCH] fixed Disable effect.');
        patched++;
    }
}

// 2. Patch Explosion & Self-Destruct
const suicideMoves = ['explosion', 'self_destruct', 'misty_explosion'];
suicideMoves.forEach(id => {
    if (moves[id]) {
        const move = moves[id];
        // Ensure MaxHP recoil
        if (!move.recoil || move.recoil.type !== 'MaxHP') {
            move.recoil = {
                type: 'MaxHP',
                percent: 100
            };
            console.log(`[PATCH] fixed ${move.name} recoil (MaxHP).`);
            patched++;
        }
    }
});

if (patched > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Successfully patched ${patched} moves.`);
} else {
    console.log('No patches needed.');
}
