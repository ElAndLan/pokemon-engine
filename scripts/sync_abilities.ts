// Script to properly sync by converting display names to kebab-case
import * as fs from 'fs';
import * as path from 'path';

const abilitiesPath = path.join(__dirname, '../src/renderer/src/core/battle/Abilities.ts');
const abilitiesJsonPath = path.join(__dirname, '../data/db/abilities.json');

const content = fs.readFileSync(abilitiesPath, 'utf-8');
const jsonContent = fs.readFileSync(abilitiesJsonPath, 'utf-8');

// Helper to convert display name to kebab-case
function toKebabCase(str: string): string {
    return str
        .toLowerCase()
        .replace(/\s+/g, '-')
        .replace(/[^a-z0-9-]/g, '');
}

// Extract all ability IDs from Abilities.ts
const registerPattern = /const\s+(\w+):\s*Ability\s*=\s*{[^}]*id:\s*['"]([^'"]+)['"]/g;
const matches = [...content.matchAll(registerPattern)];

const implementedAbilities = new Map<string, string>();
for (const match of matches) {
    const varName = match[1];
    const displayName = match[2];
    const kebabId = toKebabCase(displayName);
    implementedAbilities.set(kebabId, displayName);
}

const abilitiesData = JSON.parse(jsonContent);

console.log('\n=== SYNCING ABILITIES ===\n');

let updatedCount = 0;
let alreadyMarked = 0;
let notFound = [];

for (const [kebabId, displayName] of implementedAbilities) {
    if (abilitiesData[kebabId]) {
        if (abilitiesData[kebabId].implemented === true) {
            alreadyMarked++;
        } else {
            abilitiesData[kebabId].implemented = true;
            updatedCount++;
            console.log(`✓ ${displayName} (${kebabId})`);
        }
    } else {
        notFound.push({ kebabId, displayName });
    }
}

console.log(`\n=== SUMMARY ===`);
console.log(`Total in Abilities.ts: ${implementedAbilities.size}`);
console.log(`Already marked true: ${alreadyMarked}`);
console.log(`Newly marked true: ${updatedCount}`);
console.log(`Not found in JSON: ${notFound.length}`);

if (notFound.length > 0) {
    console.log(`\nNot found in abilities.json:`);
    notFound.forEach(({ kebabId, displayName }) => {
        console.log(`  "${displayName}" -> ${kebabId}`);
    });
}

// Write back
if (updatedCount > 0) {
    fs.writeFileSync(abilitiesJsonPath, JSON.stringify(abilitiesData, null, 2), 'utf-8');
    console.log(`\n✓ Updated abilities.json with ${updatedCount} new implementations\n`);
} else {
    console.log(`\n✓ No updates needed - all abilities already marked\n`);
}
