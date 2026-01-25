const fs = require('fs');
const path = require('path');

const MOVES_PATH = path.join(__dirname, '../data/db/moves.json');

try {
    const raw = fs.readFileSync(MOVES_PATH, 'utf8');
    // JSON.parse will automatically handle duplicates by keeping the LAST occurrence of a key
    const json = JSON.parse(raw);
    const fixed = JSON.stringify(json, null, 2);
    fs.writeFileSync(MOVES_PATH, fixed);
    console.log("Fixed duplicates in moves.json");
} catch (e) {
    console.error("Error fixing moves.json:", e);
}
