const fs = require('fs');
const path = require('path');
const https = require('https');

const DEST_DIR = path.join(__dirname, '../data/items/pokeballs');

const POKEBALLS = [
  { id: 'master-ball', name: 'master-ball' },
  { id: 'ultra-ball', name: 'ultra-ball' },
  { id: 'great-ball', name: 'great-ball' },
  { id: 'poke-ball', name: 'poke-ball' },
  { id: 'safari-ball', name: 'safari-ball' },
  { id: 'net-ball', name: 'net-ball' },
  { id: 'dive-ball', name: 'dive-ball' },
  { id: 'nest-ball', name: 'nest-ball' },
  { id: 'repeat-ball', name: 'repeat-ball' },
  { id: 'timer-ball', name: 'timer-ball' },
  { id: 'luxury-ball', name: 'luxury-ball' },
  { id: 'premier-ball', name: 'premier-ball' },
  { id: 'dusk-ball', name: 'dusk-ball' },
  { id: 'heal-ball', name: 'heal-ball' },
  { id: 'quick-ball', name: 'quick-ball' },
  { id: 'cherish-ball', name: 'cherish-ball' },
  { id: 'lure-ball', name: 'lure-ball' },
  { id: 'level-ball', name: 'level-ball' },
  { id: 'moon-ball', name: 'moon-ball' },
  { id: 'heavy-ball', name: 'heavy-ball' },
  { id: 'fast-ball', name: 'fast-ball' },
  { id: 'friend-ball', name: 'friend-ball' },
  { id: 'love-ball', name: 'love-ball' },
  { id: 'park-ball', name: 'park-ball' },
  { id: 'sport-ball', name: 'sport-ball' },
  { id: 'dream-ball', name: 'dream-ball' },
  { id: 'beast-ball', name: 'beast-ball' }
];

function downloadImage(url, filepath) {
  return new Promise((resolve, reject) => {
    https.get(url, (res) => {
      if (res.statusCode === 302 || res.statusCode === 301) {
        downloadImage(res.headers.location, filepath).then(resolve).catch(reject);
        return;
      }
      
      if (res.statusCode !== 200) {
        reject(new Error(`Failed to download: ${res.statusCode}`));
        return;
      }

      const chunks = [];
      res.on('data', (chunk) => chunks.push(chunk));
      res.on('end', () => {
        const buffer = Buffer.concat(chunks);
        fs.writeFileSync(filepath, buffer);
        resolve(true);
      });
    }).on('error', reject);
  });
}

async function downloadPokeballSprites() {
  console.log('Downloading Pokeball sprites...\n');
  
  if (!fs.existsSync(DEST_DIR)) {
    fs.mkdirSync(DEST_DIR, { recursive: true });
  }

  let successCount = 0;
  let failCount = 0;

  for (const ball of POKEBALLS) {
    const filename = `${ball.id}.png`;
    const filepath = path.join(DEST_DIR, filename);
    const url = `https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/${ball.name}.png`;

    try {
      await downloadImage(url, filepath);
      successCount++;
      console.log(`✓ ${ball.name}`);
    } catch (error) {
      failCount++;
      console.log(`✗ ${ball.name} - ${error.message}`);
    }

    await new Promise(r => setTimeout(r, 100));
  }

  console.log(`\n✓ Complete! ${successCount}/${POKEBALLS.length} sprites downloaded`);
  console.log(`Failed: ${failCount}`);
  console.log(`Destination: ${DEST_DIR}`);
}

downloadPokeballSprites().catch(console.error);
