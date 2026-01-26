const fs = require('fs');
const path = require('path');

const itemsPath = path.join(__dirname, '../data/db/items.json');
const items = JSON.parse(fs.readFileSync(itemsPath, 'utf8'));

const POKEBALLS = [
  'master-ball',
  'ultra-ball',
  'great-ball',
  'poke-ball',
  'safari-ball',
  'net-ball',
  'dive-ball',
  'nest-ball',
  'repeat-ball',
  'timer-ball',
  'luxury-ball',
  'premier-ball',
  'dusk-ball',
  'heal-ball',
  'quick-ball',
  'cherish-ball',
  'lure-ball',
  'level-ball',
  'moon-ball',
  'heavy-ball',
  'fast-ball',
  'friend-ball',
  'love-ball',
  'park-ball',
  'sport-ball',
  'dream-ball',
  'beast-ball'
];

let updatedCount = 0;

for (const ballId of POKEBALLS) {
  if (items[ballId] && items[ballId].sprite) {
    const oldSprite = items[ballId].sprite;
    items[ballId].sprite = `data/items/pokeballs/${ballId}.png`;
    console.log(`Updated ${ballId}: ${oldSprite} -> ${items[ballId].sprite}`);
    updatedCount++;
  }
}

fs.writeFileSync(itemsPath, JSON.stringify(items, null, 2));
console.log(`\n✓ Updated ${updatedCount} pokeball sprite paths`);
