// Sprite Sheet Analyzer - Run in browser console or Node.js
// This will help us map the inventory sprite sheet

const fs = require('fs');
const { createCanvas, loadImage } = require('canvas');

async function analyzeSpriteSheet() {
    const imagePath = 'data/item_inventory_images/Inventory_spritesheet.png';
    
    try {
        // Read the image as base64
        const imageBuffer = fs.readFileSync(imagePath);
        const base64 = imageBuffer.toString('base64');
        
        console.log('Sprite Sheet Analysis');
        console.log('====================');
        console.log(`File size: ${imageBuffer.length} bytes`);
        console.log(`Expected dimensions: 704x560`);
        console.log('');
        
        // Based on Pokemon Fire Red/Leaf Green bag sprite sheets:
        // Row 1 (y=0): Bag icons (64x64 each)
        // Row 2+ (y=64+): Item icons (24x24 each)
        // Background textures: Usually larger sections
        
        const spriteMap = {
            // Bag icons - 64x64, top row
            bags: {
                medicine: { x: 0, y: 0, width: 64, height: 64 },
                pokeballs: { x: 64, y: 0, width: 64, height: 64 },
                tms: { x: 128, y: 0, width: 64, height: 64 },
                berries: { x: 192, y: 0, width: 64, height: 64 },
                battle: { x: 256, y: 0, width: 64, height: 64 },
                key: { x: 320, y: 0, width: 64, height: 64 }
            },
            
            // Item icons - 24x24, starting at y=64
            items: {
                // Medicine row 1
                potion: { x: 0, y: 64, width: 24, height: 24 },
                super_potion: { x: 24, y: 64, width: 24, height: 24 },
                hyper_potion: { x: 48, y: 64, width: 24, height: 24 },
                max_potion: { x: 72, y: 64, width: 24, height: 24 },
                full_restore: { x: 96, y: 64, width: 24, height: 24 },
                revive: { x: 120, y: 64, width: 24, height: 24 },
                max_revive: { x: 144, y: 64, width: 24, height: 24 },
                
                // Status healers row 2
                antidote: { x: 0, y: 88, width: 24, height: 24 },
                paralyze_heal: { x: 24, y: 88, width: 24, height: 24 },
                awakening: { x: 48, y: 88, width: 24, height: 24 },
                burn_heal: { x: 72, y: 88, width: 24, height: 24 },
                ice_heal: { x: 96, y: 88, width: 24, height: 24 },
                full_heal: { x: 120, y: 88, width: 24, height: 24 },
                
                // Pokeballs row 3
                pokeball: { x: 0, y: 112, width: 24, height: 24 },
                great_ball: { x: 24, y: 112, width: 24, height: 24 },
                ultra_ball: { x: 48, y: 112, width: 24, height: 24 },
                master_ball: { x: 72, y: 112, width: 24, height: 24 },
                
                // Battle items row 4
                x_attack: { x: 0, y: 136, width: 24, height: 24 },
                x_defense: { x: 24, y: 136, width: 24, height: 24 },
                x_speed: { x: 48, y: 136, width: 24, height: 24 },
                x_special: { x: 72, y: 136, width: 24, height: 24 },
                
                // Berries row 5
                oran_berry: { x: 0, y: 160, width: 24, height: 24 },
                sitrus_berry: { x: 24, y: 160, width: 24, height: 24 },
                lum_berry: { x: 48, y: 160, width: 24, height: 24 }
            },
            
            // Background textures - need to identify these
            backgrounds: {
                // These are likely larger sections
                // Common in Pokemon: 
                // - Main background texture
                // - Panel/box backgrounds
                // - Border elements
            }
        };
        
        console.log(JSON.stringify(spriteMap, null, 2));
        
    } catch (error) {
        console.error('Error:', error.message);
    }
}

// For browser usage without Node.js
if (typeof module !== 'undefined' && module.exports) {
    analyzeSpriteSheet();
} else {
    console.log('Run this in Node.js or adapt for browser');
}
