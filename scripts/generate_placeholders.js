const fs = require('fs');
const path = require('path');

const START_ID = 1;
const END_ID = 151; // Generate for full Gen 1 range as requested "rest of the pokemon"
const BASE_DIR = path.join(__dirname, '../data/pokemon');
const IMAGES_DIR = path.join(BASE_DIR, 'images');
const ICONS_DIR = path.join(BASE_DIR, 'icons');
const OVERWORLD_DIR = path.join(BASE_DIR, 'overworld');

// Source placeholder (using 001's front.png if available, else creates a dummy buffer)
const SOURCE_IMG_PATH = path.join(IMAGES_DIR, '001', 'front.png');
let PLACEHOLDER_BUFFER;

// Ensure 001 exists or create a 1x1 pixel png
if (fs.existsSync(SOURCE_IMG_PATH)) {
    PLACEHOLDER_BUFFER = fs.readFileSync(SOURCE_IMG_PATH);
    console.log("Using existing 001/front.png as placeholder base.");
} else {
    // 1x1 transparent PNG base64
    const base64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==";
    PLACEHOLDER_BUFFER = Buffer.from(base64, 'base64');
    console.log("Using generated 1x1 pixel as placeholder base.");
    
    // Ensure 001 dir exists to write the source back
    const dir001 = path.join(IMAGES_DIR, '001');
    if (!fs.existsSync(dir001)) fs.mkdirSync(dir001, { recursive: true });
    fs.writeFileSync(path.join(dir001, 'front.png'), PLACEHOLDER_BUFFER);
    fs.writeFileSync(path.join(dir001, 'back.png'), PLACEHOLDER_BUFFER);
}

// Ensure output dirs exist
if (!fs.existsSync(ICONS_DIR)) fs.mkdirSync(ICONS_DIR, { recursive: true });
if (!fs.existsSync(OVERWORLD_DIR)) fs.mkdirSync(OVERWORLD_DIR, { recursive: true });

function formatId(num) {
    return num.toString().padStart(3, '0');
}

for (let i = START_ID; i <= END_ID; i++) {
    const id = formatId(i);
    
    // 1. Images (Front/Back)
    const speciesDir = path.join(IMAGES_DIR, id);
    if (!fs.existsSync(speciesDir)) fs.mkdirSync(speciesDir, { recursive: true });
    
    const frontPath = path.join(speciesDir, 'front.png');
    const backPath = path.join(speciesDir, 'back.png');
    
    if (!fs.existsSync(frontPath)) {
        fs.writeFileSync(frontPath, PLACEHOLDER_BUFFER);
        console.log(`Created ${frontPath}`);
    }
    if (!fs.existsSync(backPath)) {
        fs.writeFileSync(backPath, PLACEHOLDER_BUFFER);
        // console.log(`Created ${backPath}`);
    }

    // 2. Icon
    const iconPath = path.join(ICONS_DIR, `${id}.png`);
    if (!fs.existsSync(iconPath)) {
        fs.writeFileSync(iconPath, PLACEHOLDER_BUFFER);
        // console.log(`Created ${iconPath}`);
    }

    // 3. Overworld
    const overworldPath = path.join(OVERWORLD_DIR, `${id}.png`);
    if (!fs.existsSync(overworldPath)) {
        fs.writeFileSync(overworldPath, PLACEHOLDER_BUFFER);
        // console.log(`Created ${overworldPath}`);
    }
}

console.log("Done generating placeholders.");
