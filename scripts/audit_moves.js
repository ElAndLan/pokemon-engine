const fs = require('fs');
const path = require('path');

const movesPath = path.join(process.cwd(), 'data/db/moves.json');
const movesData = JSON.parse(fs.readFileSync(movesPath, 'utf8'));

const audit = {
    totalMoves: 0,
    categories: {},
    effectTypes: new Set(),
    statsModified: new Set(),
    statusesInflicted: new Set(),
    volatileStatuses: new Set(),
    uniqueMoves: [],
    emptyEffectMoves: [],
    accuracyOutliers: []
};

for (const id in movesData) {
    const move = movesData[id];
    audit.totalMoves++;

    // Category Distribution
    audit.categories[move.category] = (audit.categories[move.category] || 0) + 1;

    // Accuracy Check
    if (move.accuracy > 100) audit.accuracyOutliers.push(move.name);

    if (!move.effects || move.effects.length === 0) {
        audit.emptyEffectMoves.push(move.name);
        continue;
    }

    for (const effect of move.effects) {
        audit.effectTypes.add(effect.type);

        if (effect.type === 'StatChange' && effect.stat) {
            audit.statsModified.add(effect.stat);
        }

        if (effect.type === 'Status' && effect.status) {
            audit.statusesInflicted.add(effect.status);
        }

        if (effect.volatileStatus) {
            audit.volatileStatuses.add(effect.volatileStatus);
        }

        if (effect.type === 'Unique' || effect.type === 'UniqueEffect') {
            audit.uniqueMoves.push({ name: move.name, desc: move.description });
        }
    }
}

console.log('--- BATTLE MOVE AUDIT REPORT ---');
console.log(`Total Moves: ${audit.totalMoves}`);
console.log('Categories:', audit.categories);
console.log('Effect Types found:', Array.from(audit.effectTypes).join(', '));
console.log('Stats modified:', Array.from(audit.statsModified).join(', '));
console.log('Statuses inflicted:', Array.from(audit.statusesInflicted).join(', '));
console.log('Volatile Statuses:', Array.from(audit.volatileStatuses).join(', '));
console.log(`Moves with Unique logic: ${audit.uniqueMoves.length}`);
console.log(`Moves with no effects: ${audit.emptyEffectMoves.length}`);
console.log(`Moves with >100% accuracy (Never Miss): ${audit.accuracyOutliers.length}`);

// Output detailed unique moves to a log for the programmer
fs.writeFileSync('move_audit_detail.json', JSON.stringify({
    uniqueMoves: audit.uniqueMoves,
    emptyEffectMoves: audit.emptyEffectMoves,
    effectTypes: Array.from(audit.effectTypes)
}, null, 2));
