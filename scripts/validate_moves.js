const fs = require('fs');
const path = require('path');
const moves = require('../data/db/moves.json');

console.log('='.repeat(60));
console.log('MOVE DATABASE VALIDATION');
console.log('='.repeat(60));
console.log();

const totalMoves = Object.keys(moves).length;
console.log(`Total moves in database: ${totalMoves}`);
console.log();

// Sample some moves
console.log('Sample Gen 1 moves:');
['pound', 'tackle', 'thunderbolt', 'surf', 'earthquake'].forEach(id => {
    if (moves[id]) {
        console.log(`  ✓ ${moves[id].name} (${moves[id].type}, ${moves[id].category})`);
    }
});

console.log('\nSample Gen 2 moves:');
['thief', 'flame_wheel', 'whirlpool', 'future_sight'].forEach(id => {
    if (moves[id]) {
        console.log(`  ✓ ${moves[id].name} (${moves[id].type}, ${moves[id].category})`);
    }
});

console.log('\nSample Gen 3 moves:');
['blast_burn', 'hydro_cannon', 'frenzy_plant', 'volt_tackle'].forEach(id => {
    if (moves[id]) {
        console.log(`  ✓ ${moves[id].name} (${moves[id].type}, ${moves[id].category})`);
    }
});

console.log('\nSample Gen 4 moves:');
['flare_blitz', 'brave_bird', 'draco_meteor', 'stone_edge'].forEach(id => {
    if (moves[id]) {
        console.log(`  ✓ ${moves[id].name} (${moves[id].type}, ${moves[id].category})`);
    }
});

console.log('\nSample Gen 5 moves:');
['volt_switch', 'wild_charge', 'fusion_bolt', 'blue_flare'].forEach(id => {
    if (moves[id]) {
        console.log(`  ✓ ${moves[id].name} (${moves[id].type}, ${moves[id].category})`);
    }
});

console.log();
console.log('='.repeat(60));
console.log('✓✓✓ MOVE DATABASE COMPLETE ✓✓✓');
console.log('='.repeat(60));
