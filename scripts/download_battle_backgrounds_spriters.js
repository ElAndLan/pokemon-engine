const https = require('https');
const http = require('http');
const fs = require('fs');
const path = require('path');

const backgroundsDir = path.join(__dirname, '..', 'data', 'battle_backgrounds');

if (!fs.existsSync(backgroundsDir)) {
  fs.mkdirSync(backgroundsDir, { recursive: true });
}

const backgrounds = [
  {
    name: 'platinum_grass',
    url: 'https://www.spriters-resource.com/download/25830/',
    description: 'Platinum Grass Battle Background'
  },
  {
    name: 'platinum_indoor',
    url: 'https://www.spriters-resource.com/download/18502/',
    description: 'Platinum Indoor Battle Background'
  }
];

function downloadImage(url, filepath) {
  return new Promise((resolve, reject) => {
    const protocol = url.startsWith('https') ? https : http;
    
    const options = {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
      }
    };
    
    protocol.get(url, options, (response) => {
      if (response.statusCode === 200 || response.statusCode === 302) {
        if (response.statusCode === 302 && response.headers.location) {
          downloadImage(response.headers.location, filepath).then(resolve).catch(reject);
          return;
        }
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
  console.log(`Downloading ${backgrounds.length} battle backgrounds from Spriters Resource...`);
  
  for (const bg of backgrounds) {
    const filepath = path.join(backgroundsDir, `${bg.name}.png`);
    console.log(`\nDownloading: ${bg.description}`);
    console.log(`URL: ${bg.url}`);
    
    try {
      await downloadImage(bg.url, filepath);
      const stats = fs.statSync(filepath);
      console.log(`✓ Downloaded: ${bg.name} (${(stats.size / 1024).toFixed(2)} KB)`);
    } catch (error) {
      console.error(`✗ Failed to download ${bg.name}:`, error.message);
    }
  }
  
  console.log('\nDownload complete!');
  console.log(`Backgrounds saved to: ${backgroundsDir}`);
  
  const files = fs.readdirSync(backgroundsDir);
  console.log(`Total backgrounds: ${files.length}`);
}

downloadAllBackgrounds().catch(console.error);
