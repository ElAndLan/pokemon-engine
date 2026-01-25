
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

// Lists provided by user
const chargeMoves = [
    'Solar Beam', 'Solar Blade', 'Geomancy', 'Skull Bash', 'Sky Attack', 
    'Razor Wind', 'Freeze Shock', 'Ice Burn', 'Meteor Beam', 'Electro Shot'
];

const rechargeMoves = [
    'Hyper Beam', 'Giga Impact', 'Blast Burn', 'Frenzy Plant', 'Hydro Cannon',
    'Rock Wrecker', 'Roar of Time', 'Prismatic Laser', 'Meteor Assault', 'Eternabeam'
];

const invulnMoves = [
    { name: 'Fly', type: 'Fly' },
    { name: 'Dig', type: 'Dig' },
    { name: 'Dive', type: 'Dive' },
    { name: 'Bounce', type: 'Bounce' },
    { name: 'Shadow Force', type: 'ShadowForce' },
    { name: 'Phantom Force', type: 'PhantomForce' },
    { name: 'Sky Drop', type: 'SkyDrop' }
];

let patchedCount = 0;

Object.values(moves).forEach(move => {
    let modified = false;
    // Normalize Check
    const name = move.name; // Case sensitive match first? User used Title Case.

    // Normalize Check (Strip spaces for fuzzy match: "Solar Beam" == "SolarBeam")
    const normalize = (s) => s.replace(/\s+/g, '').toLowerCase();
    const cleanName = normalize(name);

    if (chargeMoves.some(n => normalize(n) === cleanName)) {
        if (!move.flags) move.flags = {};
        if (!move.flags.charge) {
            move.flags.charge = true;
            modified = true;
            console.log(`[PATCH] ${move.name}: Set Charge Flag`);
        }
    }

    if (rechargeMoves.some(n => normalize(n) === cleanName)) {
        if (!move.flags) move.flags = {};
        if (!move.flags.recharge) {
            move.flags.recharge = true;
            modified = true;
            console.log(`[PATCH] ${move.name}: Set Recharge Flag`);
        }
    }

    const invulnMatch = invulnMoves.find(i => normalize(i.name) === cleanName);
    if (invulnMatch) {
        if (!move.flags) move.flags = {};
        if (move.flags.invulnerable !== invulnMatch.type) {
            move.flags.invulnerable = invulnMatch.type;
            modified = true;
            console.log(`[PATCH] ${move.name}: Set Invulnerable (${invulnMatch.type})`);
        }
    }

    if (modified) patchedCount++;
});

if (patchedCount > 0) {
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
    console.log(`Successfully patched ${patchedCount} multi-turn moves.`);
} else {
    console.log('No moves needed patching.');
}
