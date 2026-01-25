
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

if (!moves['solar_beam']) {
    moves['solar_beam'] = {
        id: 'solar_beam',
        name: 'Solar Beam',
        type: 'Grass',
        category: 'Special',
        power: 120,
        accuracy: 100,
        pp: 10,
        priority: 0,
        target: 'SelectedEnemy',
        flags: {
            charge: true
        },
        effects: [
            {
                type: 'Damage'
            }
        ],
        description: 'Absorbs light in one turn, then attacks next turn.'
    };
    console.log('[ADD] Added Solar Beam');
    fs.writeFileSync(movesPath, JSON.stringify(moves, null, 2));
} else {
    console.log('Solar Beam already exists.');
}
