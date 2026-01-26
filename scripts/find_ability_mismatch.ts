// Script to find the mismatch between Abilities.ts and abilities.json
import * as fs from 'fs';
import * as path from 'path';

const abilitiesPath = path.join(__dirname, '../src/renderer/src/core/battle/Abilities.ts');
const abilitiesJsonPath = path.join(__dirname, '../data/db/abilities.json');

const content = fs.readFileSync(abilitiesPath, 'utf-8');
const jsonContent = fs.readFileSync(abilitiesJsonPath, 'utf-8');

// Extract all ability IDs from Abilities.ts
const registerPattern = /const\s+(\w+):\s*Ability\s*=\s*{[^}]*id:\s*['"]([^'"]+)['"]/g;
const matches = [...content.matchAll(registerPattern)];

const implementedAbilities = new Map<string, string>();
for (const match of matches) {
    const varName = match[1];
    const abilityId = match[2];
    implementedAbilities.set(abilityId, varName);
}

const abilitiesData = JSON.parse(jsonContent);

console.log('\n=== MISMATCH ANALYSIS ===\n');

// Find abilities in Abilities.ts but not in abilities.json
const notInJson = [];
for (const [id, varName] of implementedAbilities) {
    if (!abilitiesData[id]) {
        notInJson.push({ id, varName });
    }
}

console.log(`Abilities in Abilities.ts but NOT in abilities.json: ${notInJson.length}`);
if (notInJson.length > 0) {
    notInJson.forEach(({ id, varName }) => {
        console.log(`  ${varName} -> id: "${id}"`);
    });
}

// Find abilities in Abilities.ts that ARE in abilities.json but marked false
const inJsonButFalse = [];
for (const [id, varName] of implementedAbilities) {
    if (abilitiesData[id] && abilitiesData[id].implemented === false) {
        inJsonButFalse.push({ id, varName, name: abilitiesData[id].name });
    }
}

console.log(`\nAbilities in Abilities.ts that ARE in abilities.json but marked FALSE: ${inJsonButFalse.length}`);
if (inJsonButFalse.length > 0) {
    inJsonButFalse.forEach(({ id, varName, name }) => {
        console.log(`  ${varName} -> "${id}" (JSON name: "${name}")`);
    });
}

// Show some examples of correct matches
console.log(`\n=== SAMPLE CORRECT MATCHES ===`);
let count = 0;
for (const [id, varName] of implementedAbilities) {
    if (abilitiesData[id] && abilitiesData[id].implemented === true && count < 5) {
        console.log(`  ${varName} -> "${id}" ✓`);
        count++;
    }
}
