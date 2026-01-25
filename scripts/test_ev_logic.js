
const evs = { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 };
const total = Object.values(evs).reduce((a, b) => a + b, 0);
console.log('Zero EVs Total:', total);

const evs2 = { hp: 100, attack: 100, defense: 55, spAttack: 0, spDefense: 0, speed: 0 };
const total2 = Object.values(evs2).reduce((a, b) => a + b, 0);
console.log('255 EVs Total:', total2);

const evs3 = { hp: 255, attack: 255, defense: 255, spAttack: 255, spDefense: 255, speed: 255 };
const total3 = Object.values(evs3).reduce((a, b) => a + b, 0);
console.log('Max EVs Total:', total3);

// Test matching
const text = "Increases Special Attack effort by 10";
console.log('Has effort:', text.includes('effort'));

// Verify JSON content presence (simple grep simulation)
const fs = require('fs');
try {
    const data = fs.readFileSync('data/db/items.json', 'utf8');
    const json = JSON.parse(data);
    console.log('Protein Exists:', !!json['protein']);
    console.log('Carbos Exists:', !!json['carbos']);
    if (json['protein']) console.log('Protein Effect:', json['protein'].effect);
    if (json['carbos']) console.log('Carbos Effect:', json['carbos'].effect);
} catch (e) {
    console.error('Error reading items.json', e.message);
}
