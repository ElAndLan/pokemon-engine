const fs = require('fs');
const path = require('path');

const movesPath = path.join(process.cwd(), 'data/db/moves.json');
const movesData = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

const SUPPORTED_EFFECTS = ['Damage', 'Status', 'StatChange', 'Heal', 'Unique', 'Drain'];
const SUPPORTED_VOLATILE = ['Confusion', 'Flinch', 'Bound', 'LeechSeed', 'Nightmare', 'Curse'];

let passed = 0;
let failed = 0;
const issues = [];

for (const id in movesData) {
    const move = movesData[id];
    let moveIssues = [];

    // 1. Basic Structure
    if (!move.name) moveIssues.push('Target lacks name');
    if (!move.type) moveIssues.push('Target lacks type');

    // 2. Effects Audit
    if (!move.effects) {
        moveIssues.push('No effects array defined');
    } else {
        for (const effect of move.effects) {
            if (!SUPPORTED_EFFECTS.includes(effect.type)) {
                moveIssues.push(`Unsupported effect type: ${effect.type}`);
            }

            if (effect.type === 'Status' && !effect.status) {
                moveIssues.push('Status effect missing "status" property');
            }

            if (effect.type === 'StatChange' && (!effect.stat || effect.stages === undefined)) {
                moveIssues.push('StatChange missing "stat" or "stages"');
            }

            if (effect.type === 'Unique' && effect.volatileStatus && !SUPPORTED_VOLATILE.includes(effect.volatileStatus)) {
                 // Not an error, but a warning for future impl
                 // issues.push(`${move.name}: Unknown volatile status ${effect.volatileStatus}`);
            }
        }
    }

    if (moveIssues.length > 0) {
        failed++;
        issues.push({ name: move.name, errors: moveIssues });
    } else {
        passed++;
    }
}

console.log('--- GLOBAL MOVE COMPATIBILITY REPORT ---');
console.log(`Passed: ${passed}`);
console.log(`Failed: ${failed}`);
console.log(`Coverage: ${((passed / (passed + failed)) * 100).toFixed(2)}%`);

if (issues.length > 0) {
    console.log('\nTop Issues:');
    issues.slice(0, 10).forEach(iss => {
        console.log(`- ${iss.name}: ${iss.errors.join(', ')}`);
    });
}

// Generate full report file
fs.writeFileSync('move_engine_compatibility.json', JSON.stringify(issues, null, 2));
