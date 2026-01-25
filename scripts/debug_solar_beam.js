
const fs = require('fs');
const path = require('path');

const movesPath = path.join(__dirname, '../data/db/moves.json');
const moves = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

const m = moves['solar_beam'];
if (m) {
    console.log('FOUND Solar Beam:');
    console.log(JSON.stringify(m, null, 2));
} else {
    console.log('Solar Beam NOT FOUND in JSON object.');
    // Check keys
    const keys = Object.keys(moves);
    const solarKeys = keys.filter(k => k.toLowerCase().includes('solar'));
    console.log('Keys containing "solar":', solarKeys);
}
