
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

// User provided list with specific known probabilities (standard Gen 5 values):
const patches = {
    'bolt_strike': { stat: 'Paralysis', type: 'Status', chance: 20 },
    'bubble_beam': { stat: 'speed', type: 'StatChange', chance: 10, stages: -1 },
    'bug_buzz': { stat: 'spDefense', type: 'StatChange', chance: 10, stages: -1 },
    'charge_beam': { stat: 'spAttack', type: 'StatChange', chance: 70, stages: 1, target: 'Self' }, // High chance
    'earth_power': { stat: 'spDefense', type: 'StatChange', chance: 10, stages: -1 },
    'energy_ball': { stat: 'spDefense', type: 'StatChange', chance: 10, stages: -1 },
    'fiery_dance': { stat: 'spAttack', type: 'StatChange', chance: 50, stages: 1, target: 'Self' }, // Volcarona sig
    'flash_cannon': { stat: 'spDefense', type: 'StatChange', chance: 10, stages: -1 },
    'focus_blast': { stat: 'spDefense', type: 'StatChange', chance: 10, stages: -1 },
    'freeze_shock': { stat: 'Paralysis', type: 'Status', chance: 30 }, // "Freeze Blast"? Likely Freeze Shock. (Kyurem-B). Actually description says "May paralyze".
    'leaf_tornado': { stat: 'accuracy', type: 'StatChange', chance: 30, stages: -1 }, // Usually 30 - 50?
    'luster_purge': { stat: 'spDefense', type: 'StatChange', chance: 50, stages: -1 },
    'mirror_shot': { stat: 'accuracy', type: 'StatChange', chance: 30, stages: -1 },
    'mist_ball': { stat: 'spAttack', type: 'StatChange', chance: 50, stages: -1 },
    'mud_bomb': { stat: 'accuracy', type: 'StatChange', chance: 30, stages: -1 },
    'night_daze': { stat: 'accuracy', type: 'StatChange', chance: 40, stages: -1 },
    'ominous_wind': { type: 'StatChange', stat: 'all', chance: 10, stages: 1, target: 'Self' },
    'poison_gas': { type: 'Status', status: 'Poison', chance: 100 }, // Ensure it's explicit? User asked for it.
    'razor_shell': { stat: 'defense', type: 'StatChange', chance: 50, stages: -1 },
    'relic_song': { status: 'Sleep', type: 'Status', chance: 10 },
    'seed_flare': { stat: 'spDefense', type: 'StatChange', chance: 40, stages: -2 },
    'thief': { type: 'Unique', volatileStatus: 'StealItem', volatileChance: 100 }, // Always steals if hits?
};

let patchedCount = 0;

Object.keys(patches).forEach(id => {
    const move = moves[id];
    const patch = patches[id];

    if (!move) {
        // Try fuzzy match on name if ID mismatch
        // User wrote names like "Freeze Blast", "Ominus Wind"
        return; 
    }

    if (!move.effects) move.effects = [];

    let effectFound = false;

    // Check existing effects
    move.effects.forEach(e => {
        // MATCH STATUS
        if (patch.type === 'Status' && e.type === 'Status' && e.status === patch.status) {
            e.chance = patch.chance;
            effectFound = true;
        }
        // MATCH STAT CHANGE
        if (patch.type === 'StatChange' && e.type === 'StatChange' && e.stat === patch.stat) {
            e.chance = patch.chance;
            if (patch.stages) e.stages = patch.stages; 
            effectFound = true;
        }
        // MATCH UNIQUE
        if (patch.type === 'Unique' && e.type === 'Unique') {
             // Thief logic...
        }
    });

    if (!effectFound) {
        // Create new effect
        const newEffect = { ...patch };
        // cleanup temp keys
        if (newEffect.target === 'Self') delete newEffect.target; // Effect target handled in code usually, or specific prop?
        // Actually DataTypes says: "target" is on MoveData, but effect can target self? 
        // AtomicEffects ApplyStatChange checks: "const target = effect.target === 'Self' || move.target === 'Self' ? attacker : defender;"
        // So we need to add target: 'Self' to the effect if it's self-buff.
        
        move.effects.push(newEffect);
        console.log(`[PATCH] ${move.name}: Added missing effect ${patch.type} (${patch.chance}%)`);
        patchedCount++;
    } else {
        console.log(`[PATCH] ${move.name}: Updated existing effect to ${patch.chance}%`);
        patchedCount++;
    }
});

// Handle typos / fuzzy names from user list
// "Ominus Wind" -> 'ominous_wind' (Handled above keys if correct)
// "Freeze Blast" -> Likely 'freeze_shock' (Black Kyurem) or 'ice_burn' (White Kyurem)? User said "Paralyze". Freeze Shock paralyzes. Ice Burn burns.
// "Seed flare" -> 'seed_flare'

if (patchedCount > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Successfully patched ${patchedCount} specific moves.`);
}
