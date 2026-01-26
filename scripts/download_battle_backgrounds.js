const https = require('https');
const fs = require('fs');
const path = require('path');

const backgroundsDir = path.join(__dirname, '..', 'data', 'battle_backgrounds');

if (!fs.existsSync(backgroundsDir)) {
  fs.mkdirSync(backgroundsDir, { recursive: true });
}

const backgrounds = [
  {
    name: 'grass',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-grass.png'
  },
  {
    name: 'water',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-water.png'
  },
  {
    name: 'cave',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-cave.png'
  },
  {
    name: 'building',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-building.png'
  },
  {
    name: 'forest',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-forest.png'
  },
  {
    name: 'mountain',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-mountain.png'
  },
  {
    name: 'sandy',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-sandy.png'
  },
  {
    name: 'snowy',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-snowy.png'
  },
  {
    name: 'volcanic',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-volcanic.png'
  },
  {
    name: 'swamp',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-swamp.png'
  },
  {
    name: 'sea',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-sea.png'
  },
  {
    name: 'underwater',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-underwater.png'
  },
  {
    name: 'electric-terrain',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-electric-terrain.png'
  },
  {
    name: 'grassy-terrain',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-grassy-terrain.png'
  },
  {
    name: 'misty-terrain',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-misty-terrain.png'
  },
  {
    name: 'psychic-terrain',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-psychic-terrain.png'
  },
  {
    name: 'indoor',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-indoor.png'
  },
  {
    name: 'gym',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-gym.png'
  },
  {
    name: 'elite-four',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-elite-four.png'
  },
  {
    name: 'champion',
    url: 'https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/items/battle-bg-champion.png'
  }
];

function downloadImage(url, filepath) {
  return new Promise((resolve, reject) => {
    https.get(url, (response) => {
      if (response.statusCode === 200) {
        response.pipe(fs.createWriteStream(filepath))
          .on('finish', () => resolve())
          .on('error', (err) => reject(err));
      } else {
        reject(new Error(`Failed to download ${url}: ${response.statusCode}`));
      }
    }).on('error', (err) => reject(err));
  });
}

async function downloadAllBackgrounds() {
  console.log(`Downloading ${backgrounds.length} battle backgrounds...`);
  
  for (const bg of backgrounds) {
    const filepath = path.join(backgroundsDir, `${bg.name}.png`);
    
    try {
      await downloadImage(bg.url, filepath);
      console.log(`✓ Downloaded: ${bg.name}`);
    } catch (error) {
      console.error(`✗ Failed to download ${bg.name}:`, error.message);
    }
  }
  
  console.log('\nDownload complete!');
  console.log(`Backgrounds saved to: ${backgroundsDir}`);
}

downloadAllBackgrounds().catch(console.error);
