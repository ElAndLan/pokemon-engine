"""
Sprite Sheet Analyzer for Pokemon Bag Inventory
Analyzes the inventory sprite sheet and outputs coordinate mappings
"""

from PIL import Image
import json

def analyze_sprite_sheet(image_path):
    """Analyze the sprite sheet and detect different regions"""
    img = Image.open(image_path)
    width, height = img.size
    
    print(f"Sprite Sheet Dimensions: {width}x{height}")
    print(f"Color Mode: {img.mode}")
    
    # Based on typical Pokemon sprite sheets, let's analyze the structure
    # The sheet is 704x560
    
    # Common sprite sizes in Pokemon games:
    # - Bag icons: typically 64x64
    # - Item icons: typically 24x24 or 32x32
    # - Backgrounds: variable sizes
    
    sprite_map = {
        "metadata": {
            "width": width,
            "height": height,
            "mode": img.mode
        },
        "bags": {},
        "items": {},
        "backgrounds": {}
    }
    
    # Analyze top row for bag icons (assuming 64x64)
    bag_categories = ['medicine', 'pokeballs', 'tms', 'berries', 'battle', 'key']
    for i, category in enumerate(bag_categories):
        x = i * 64
        if x + 64 <= width:
            sprite_map["bags"][category] = {
                "x": x,
                "y": 0,
                "width": 64,
                "height": 64
            }
    
    # Sample pixels to detect background regions
    # Check for large uniform or patterned areas
    
    # Analyze for item icons (starting after bag icons)
    # Typically 24x24 or 32x32
    item_y_start = 64
    item_size = 24  # Common size
    
    # Save the analysis
    return sprite_map

if __name__ == "__main__":
    result = analyze_sprite_sheet("data/item_inventory_images/Inventory_spritesheet.png")
    print(json.dumps(result, indent=2))
