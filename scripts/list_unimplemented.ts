// Script to list all unimplemented abilities
import * as fs from 'fs';
import * as path from 'path';

const abilitiesJsonPath = path.join(__dirname, '../data/db/abilities.json');
const jsonContent = fs.readFileSync(abilitiesJsonPath, 'utf-8');
const abilitiesData = JSON.parse(jsonContent);

const unimplemented = [];
const implemented = [];

for (const [id, data] of Object.entries(abilitiesData)) {
    if (data.implemented === false) {
        unimplemented.push({ id, name: data.name, description: data.description });
    } else if (data.implemented === true) {
        implemented.push({ id, name: data.name });
    }
}

console.log(`\n=== ABILITY STATUS ===`);
console.log(`Implemented: ${implemented.length}`);
console.log(`Unimplemented: ${unimplemented.length}`);
console.log(`Total: ${implemented.length + unimplemented.length}\n`);

console.log(`=== UNIMPLEMENTED ABILITIES (grouped by type) ===\n`);

// Group by common patterns
const groups = {
    'Critical Hit': [],
    'Weather': [],
    'Status': [],
    'Stat Boost': [],
    'Immunity': [],
    'Contact': [],
    'Item': [],
    'Trap': [],
    'Form Change': [],
    'Other': []
};

for (const ability of unimplemented) {
    const desc = ability.description.toLowerCase();
    
    if (desc.includes('critical')) {
        groups['Critical Hit'].push(ability);
    } else if (desc.includes('weather') || desc.includes('rain') || desc.includes('sun') || desc.includes('sandstorm') || desc.includes('hail')) {
        groups['Weather'].push(ability);
    } else if (desc.includes('status') || desc.includes('poison') || desc.includes('burn') || desc.includes('paralyz') || desc.includes('sleep') || desc.includes('freeze')) {
        groups['Status'].push(ability);
    } else if (desc.includes('attack') || desc.includes('defense') || desc.includes('speed') || desc.includes('special')) {
        groups['Stat Boost'].push(ability);
    } else if (desc.includes('immune') || desc.includes('prevent') || desc.includes('protect')) {
        groups['Immunity'].push(ability);
    } else if (desc.includes('contact') || desc.includes('touch')) {
        groups['Contact'].push(ability);
    } else if (desc.includes('item') || desc.includes('held')) {
        groups['Item'].push(ability);
    } else if (desc.includes('trap') || desc.includes('flee') || desc.includes('switch')) {
        groups['Trap'].push(ability);
    } else if (desc.includes('form') || desc.includes('type change')) {
        groups['Form Change'].push(ability);
    } else {
        groups['Other'].push(ability);
    }
}

for (const [groupName, abilities] of Object.entries(groups)) {
    if (abilities.length > 0) {
        console.log(`\n### ${groupName} (${abilities.length})`);
        abilities.forEach(a => {
            console.log(`  - ${a.name} (${a.id})`);
            console.log(`    ${a.description}`);
        });
    }
}
