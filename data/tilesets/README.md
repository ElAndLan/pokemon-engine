# Tilesets Directory

## Structure
```
data/tilesets/
├── tilesets.json          # Tileset configuration
├── images/                # Store your tileset PNG files here
│   ├── terrain.png
│   ├── buildings.png
│   └── decorations.png
└── README.md             # This file
```

## How to Add Tilesets

1. **Place your tileset image** in the `images/` folder
   - Supported formats: PNG
   - Recommended: Organized grid layout
   - Common sizes: 256x256, 512x512, etc.

2. **Import via Editor**
   - Open the game in Editor mode
   - Go to "Tilesets" tab
   - Click "Import Tileset"
   - Select your image
   - Configure tile size (16x16, 32x32, etc.)

3. **Use in Map Editor**
   - Switch to "Map" tab
   - Select tiles from the palette
   - Paint on the canvas

## Tileset Format

Your tileset images should be organized in a grid:
```
[Tile 0][Tile 1][Tile 2][Tile 3]
[Tile 4][Tile 5][Tile 6][Tile 7]
[Tile 8][Tile 9][Tile 10][Tile 11]
```

## Example Pokemon-Style Tilesets

You can find free Pokemon-style tilesets at:
- https://www.spriters-resource.com/
- https://reliccastle.com/resources/
- https://pokemonworkshop.com/

## Configuration

The `tilesets.json` file stores:
- Tileset name
- Image path
- Tile width/height
- Grid columns/rows
- Tile metadata (collision, terrain type, etc.)
