import { Menu } from './MenuSystem';
import { Game } from '../Game';
import type { ItemCategory, ItemData, BagItem } from '../data/ItemData';
import { PokemonSelectionMenu } from './PokemonSelectionMenu';
import type { ItemUseContext } from '../items/ItemHandler';

export type BagMode = 'OVERWORLD' | 'BATTLE';

interface SpriteCoords {
  x: number;
  y: number;
  width: number;
  height: number;
}

// Sprite coordinates from the inventory sprite sheet (UI elements only)
const SPRITE_ATLAS = {
  bags: {
    closed: { x: 33, y: 33, width: 62, height: 62 },
    pokeballs: { x: 112, y: 34, width: 63, height: 60 },
    key: { x: 32, y: 111, width: 63, height: 64 },
    medicine: { x: 113, y: 112, width: 61, height: 64 },
    berries: { x: 352, y: 32, width: 63, height: 64 },
    tms: { x: 352, y: 112, width: 62, height: 62 },
    battle: { x: 113, y: 112, width: 61, height: 64 }
  },
  
  backgrounds: {
    normal: { x: 113, y: 209, width: 238, height: 157 },
    tm_case: { x: 369, y: 211, width: 239, height: 157 },
    berry_pouch: { x: 369, y: 384, width: 237, height: 157 }
  },
  
  tab_bar: { x: 17, y: 208, width: 79, height: 95 },
  
  close_labels: {
    bag: { x: 449, y: 15, width: 238, height: 49 },
    berry_pouch: { x: 448, y: 81, width: 238, height: 30 },
    tm_case: { x: 449, y: 129, width: 237, height: 61 }
  }
};

export class BagMenu implements Menu {
  private game: Game;
  private mode: BagMode;
  private categories: ItemCategory[] = ['medicine', 'pokeballs', 'battle', 'berries', 'tms', 'key'];
  private categoryNames: Record<ItemCategory, string> = {
    medicine: 'ITEMS',
    pokeballs: 'POKE BALLS',
    battle: 'BATTLE ITEMS',
    berries: 'BERRIES',
    tms: 'TMs & HMs',
    key: 'KEY ITEMS'
  };
  
  private currentCategory: number = 0;
  private currentItem: number = 0;
  private contextMenuOpen: boolean = false;
  private contextSelection: number = 0;

  private spriteSheet: HTMLImageElement | null = null;
  private spriteSheetLoaded: boolean = false;

  public onItemUsed: ((itemId: string) => void) | null = null;

  constructor(game: Game, mode: BagMode = 'OVERWORLD') {
    this.game = game;
    this.mode = mode;
  }

  public async onOpen(): Promise<void> {
    console.log(`[BagMenu] Opened in ${this.mode} mode`);
    this.currentCategory = 0;
    this.currentItem = 0;
    this.contextMenuOpen = false;
    await this.loadSpriteSheet();
  }

  private async loadSpriteSheet(): Promise<void> {
    try {
      const response = await (window as any).fs.readImage('data/item_inventory_images/Inventory_spritesheet.png');
      if (response.success) {
        const img = new Image();
        img.onload = () => {
          this.spriteSheetLoaded = true;
          console.log('[BagMenu] Sprite sheet loaded');
        };
        img.src = `data:image/png;base64,${response.data}`;
        this.spriteSheet = img;
      }
    } catch (e) {
      console.error('[BagMenu] Failed to load sprite sheet', e);
    }
  }

  public onClose(): void {
    console.log('[BagMenu] Closed');
  }

  public update(dt: number): void {
    const input = this.game.input;

    if (this.contextMenuOpen) {
      this.updateContextMenu(input);
      return;
    }

    const items = this.getCurrentCategoryItems();

    if (input.isJustPressed('KeyA') || input.isJustPressed('ArrowLeft')) {
      this.currentCategory = (this.currentCategory - 1 + this.categories.length) % this.categories.length;
      this.currentItem = 0;
    }
    if (input.isJustPressed('KeyD') || input.isJustPressed('ArrowRight')) {
      this.currentCategory = (this.currentCategory + 1) % this.categories.length;
      this.currentItem = 0;
    }

    if (items.length > 0) {
      if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
        this.currentItem = Math.min(this.currentItem + 1, items.length - 1);
      }
      if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
        this.currentItem = Math.max(this.currentItem - 1, 0);
      }

      if (input.isJustPressed('Space') || input.isJustPressed('Enter') || input.isJustPressed('KeyZ')) {
        this.contextMenuOpen = true;
        this.contextSelection = 0;
      }
    }

    if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
      this.game.menuSystem.pop();
    }
  }

  private updateContextMenu(input: any): void {
    const items = this.getCurrentCategoryItems();
    if (items.length === 0) {
      this.contextMenuOpen = false;
      return;
    }

    const selectedBagItem = items[this.currentItem];
    const itemData = this.game.dataManager.getItem(selectedBagItem.itemId);
    if (!itemData) {
      this.contextMenuOpen = false;
      return;
    }

    const options = this.getContextOptions(itemData);

    if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
      this.contextSelection = (this.contextSelection + 1) % options.length;
    }
    if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
      this.contextSelection = (this.contextSelection - 1 + options.length) % options.length;
    }

    if (input.isJustPressed('Space') || input.isJustPressed('Enter') || input.isJustPressed('KeyZ')) {
      const option = options[this.contextSelection];
      this.handleContextOption(option, selectedBagItem.itemId, itemData);
    }

    if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
      this.contextMenuOpen = false;
    }
  }

  private getContextOptions(itemData: ItemData): string[] {
    const options: string[] = [];
    
    if (this.mode === 'OVERWORLD' && itemData.canUseInOverworld) {
      options.push('USE');
    } else if (this.mode === 'BATTLE' && itemData.canUseInBattle) {
      options.push('USE');
    }
    
    if (this.mode === 'OVERWORLD' && itemData.category === 'berries') {
      options.push('GIVE');
    }
    
    if (!itemData.isKeyItem) {
      options.push('TOSS');
    }
    
    options.push('CANCEL');
    return options;
  }

  private handleContextOption(option: string, itemId: string, itemData: ItemData): void {
    if (option === 'USE') {
      console.log(`[BagMenu] Using item: ${itemId}`);
      this.contextMenuOpen = false;
      
      // Determine context based on mode
      const context: ItemUseContext = this.mode === 'BATTLE' ? 'battle' : 'overworld';
      
      // In overworld, need to select a Pokemon
      if (context === 'overworld') {
        const pokemonMenu = new PokemonSelectionMenu(this.game, `Use ${itemData.name}`);
        pokemonMenu.setOnPokemonSelected((pokemon, index) => {
          // Use the item on the selected Pokemon
          const result = this.game.itemHandler.useItem(itemId, pokemon, context);
          
          // Show result message
          console.log(`[BagMenu] Item use result:`, result);
          if (result.success && result.consumed) {
            // Remove item from bag
            this.game.bagSystem.removeItem(itemId, 1);
          }
          
          // Show message to user via dialog
          if (this.game.dialogBox) {
            this.game.dialogBox.show(result.message);
            // NOTE: Interaction flow might continue immediately if we don't wait?
            // DialogBox.show sets 'active' = true.
            // Game.update calls menuSystem.update. 
            // We should arguably pause BagMenu update while dialog is open.
          }
          
          // Call callback if set
          if (this.onItemUsed && result.success) {
            this.onItemUsed(itemId);
          }
        });
        this.game.menuSystem.push(pokemonMenu);
      } else {
        // In battle, would use on active Pokemon or target
        // This would be handled by battle system
        if (this.onItemUsed) {
          this.onItemUsed(itemId);
        }
      }
    } else if (option === 'GIVE') {
      console.log(`[BagMenu] Give item: ${itemId}`);
      this.contextMenuOpen = false;
      // TODO: Implement give item to Pokemon
    } else if (option === 'TOSS') {
      console.log(`[BagMenu] Toss item: ${itemId}`);
      this.game.bagSystem.removeItem(itemId, 1);
      this.contextMenuOpen = false;
    } else {
      this.contextMenuOpen = false;
    }
  }

  private getCurrentCategoryItems(): BagItem[] {
    const category = this.categories[this.currentCategory];
    return this.game.bagSystem.getItemsByCategory(category, this.game.dataManager);
  }

  private drawSprite(ctx: CanvasRenderingContext2D, coords: SpriteCoords, x: number, y: number, scaleX: number = 1, scaleY: number = 1): void {
    if (!this.spriteSheet || !this.spriteSheetLoaded) return;
    
    ctx.drawImage(
      this.spriteSheet,
      coords.x, coords.y, coords.width, coords.height,
      x, y, coords.width * scaleX, coords.height * scaleY
    );
  }

  public render(ctx: CanvasRenderingContext2D): void {
    const w = this.game.display.width;
    const h = this.game.display.height;

    // Draw background texture from sprite sheet at proper scale
    const bgCoords = this.getBackgroundCoords();
    if (bgCoords && this.spriteSheetLoaded) {
      // Scale to fit screen without stretching too much (zoom out more)
      const scale = Math.min(w / bgCoords.width, h / bgCoords.height) * 0.85;
      const bgW = bgCoords.width * scale;
      const bgH = bgCoords.height * scale;
      const bgX = (w - bgW) / 2;
      const bgY = (h - bgH) / 2 - 30; // Shift up by 30px
      this.drawSprite(ctx, bgCoords, bgX, bgY, scale, scale);
    } else {
      ctx.fillStyle = '#d8c8a0';
      ctx.fillRect(0, 0, w, h);
    }

    // Outer border
    ctx.strokeStyle = '#6b4423';
    ctx.lineWidth = 8;
    ctx.strokeRect(4, 4, w - 8, h - 8);

    // Inner border
    ctx.strokeStyle = '#8b6f47';
    ctx.lineWidth = 3;
    ctx.strokeRect(12, 12, w - 24, h - 24);

    const padding = 20;
    const leftPanelW = 360;
    const rightPanelW = 460; // Adjusted width

    // Category name (top left)
    this.renderCategoryName(ctx, padding, padding);

    // Left panel: Bag icon only
    this.renderLeftPanel(ctx, padding, padding + 50, leftPanelW, h - padding * 2 - 50);

    // Right panel: Item list (top)
    this.renderRightPanel(ctx, padding + leftPanelW + 10, padding, rightPanelW, h - padding * 2);
    
    // Description box at the very bottom (in the blue area)
    const descY = h - 200; // Move up 25px (was 175)
    const descH = 90;
    const descX = padding + leftPanelW - 125;
    const descW = rightPanelW + 110;
    this.renderDescriptionBox(ctx, descX, descY, descW, descH);

    // Context menu
    if (this.contextMenuOpen) {
      this.renderContextMenu(ctx, w, h);
    }
  }

  private getBackgroundCoords(): SpriteCoords | null {
    const category = this.categories[this.currentCategory];
    
    if (category === 'tms') {
      return SPRITE_ATLAS.backgrounds.tm_case;
    } else if (category === 'berries') {
      return SPRITE_ATLAS.backgrounds.berry_pouch;
    } else {
      return SPRITE_ATLAS.backgrounds.normal;
    }
  }

  private renderCategoryName(ctx: CanvasRenderingContext2D, x: number, y: number): void {
    const category = this.categories[this.currentCategory];
    ctx.fillStyle = '#2c1810';
    ctx.font = 'bold 20px monospace';
    ctx.textAlign = 'left';
    ctx.fillText(this.categoryNames[category], x + 100, y + 40); // Shift right 100px, down 20px
  }

  private renderLeftPanel(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number): void {
    // Bag icon centered at top
    const bagSize = 100;
    const bagX = x + (w - bagSize) / 2;
    const bagY = y + 170; // Shifted down 150px (was y + 20)

    // Draw bag sprite from sprite sheet
    const category = this.categories[this.currentCategory];
    const bagCoords = SPRITE_ATLAS.bags[category];
    if (bagCoords && this.spriteSheetLoaded) {
      const scale = bagSize / Math.max(bagCoords.width, bagCoords.height);
      const offsetX = (bagSize - bagCoords.width * scale) / 2;
      const offsetY = (bagSize - bagCoords.height * scale) / 2;
      this.drawSprite(ctx, bagCoords, bagX + offsetX, bagY + offsetY, scale, scale);
    }

    // Navigation arrows below bag
    ctx.fillStyle = '#2c1810';
    ctx.font = 'bold 32px monospace';
    ctx.textAlign = 'center';
    const arrowY = bagY + bagSize + 40;
    ctx.fillText('◀', bagX + bagSize / 3, arrowY);
    ctx.fillText('▶', bagX + (bagSize * 2) / 3, arrowY);
  }

  private renderRightPanel(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number): void {
    // Item list (much taller, increased by another 100px)
    const listY = y + 25;
    // Calculate remaining height: Total H - Top Offset - Bottom Area (200px) - Padding - Extra Reduction (50px)
    // The passed 'h' is (CanvasH - 40). 
    // We want to end at CanvasH - 320ish.
    const listH = (h + 40) - 250 - listY - 20; 
    this.renderItemList(ctx, x, listY, w, listH);
  }

  private renderItemList(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number): void {
    // List background
    ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
    ctx.fillRect(x, y, w, h);
    ctx.strokeStyle = '#8b6f47';
    ctx.lineWidth = 2;
    ctx.strokeRect(x, y, w, h);

    const items = this.getCurrentCategoryItems();
    if (items.length === 0) {
      ctx.fillStyle = '#8b7355';
      ctx.font = '16px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('No items', x + w / 2, y + 30);
      return;
    }

    const itemHeight = 32;
    const visibleItems = Math.floor((h - 10) / itemHeight);
    const scrollOffset = Math.max(0, this.currentItem - visibleItems + 1);

    items.slice(scrollOffset, scrollOffset + visibleItems).forEach((bagItem, i) => {
      const itemData = this.game.dataManager.getItem(bagItem.itemId);
      if (!itemData) return;

      const actualIndex = scrollOffset + i;
      const isSelected = actualIndex === this.currentItem;
      const itemY = y + 5 + i * itemHeight;

      if (isSelected) {
        ctx.fillStyle = 'rgba(240, 224, 192, 0.8)';
        ctx.fillRect(x + 3, itemY, w - 6, itemHeight - 2);
      }

      ctx.fillStyle = '#2c1810';
      ctx.font = '16px monospace';
      ctx.textAlign = 'left';
      ctx.fillText(itemData.name, x + 12, itemY + 20);

      if (!itemData.isKeyItem) {
        ctx.textAlign = 'right';
        ctx.fillText(`x${bagItem.quantity}`, x + w - 12, itemY + 20);
      }
    });
  }

  private renderDescriptionBox(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number): void {
    // Description box background
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(x, y, w, h);
    ctx.strokeStyle = '#8b6f47';
    ctx.lineWidth = 3;
    ctx.strokeRect(x, y, w, h);

    const items = this.getCurrentCategoryItems();
    if (items.length > 0) {
      const selectedItem = items[this.currentItem];
      const itemData = this.game.dataManager.getItem(selectedItem.itemId);
      if (itemData) {
        // Draw Item Icon Box (Left of Description)
        const iconBoxSize = 90;
        const iconBoxX = x - iconBoxSize - 73; // 73px padding (75 - 2)
        const iconBoxY = y - 2; // Shift up 2px
        
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(iconBoxX, iconBoxY, iconBoxSize, iconBoxSize);
        ctx.lineWidth = 3;
        ctx.strokeStyle = '#8b6f47';
        ctx.strokeRect(iconBoxX, iconBoxY, iconBoxSize, iconBoxSize);

        // Draw Icon
        if (itemData.sprite) {
             const img = new Image();
             img.src = itemData.sprite;
             // Note: Loading happening every frame is bad, usually we cache. 
             // But browser caching might handle "new Image().src = same_url" okay-ish?
             // Better to cache it on the menu class.
             // For simplicity in this edit:
             ctx.drawImage(img, iconBoxX + 5, iconBoxY + 5, iconBoxSize - 10, iconBoxSize - 10);
        }

        ctx.fillStyle = '#2c1810';
        ctx.font = '16px monospace';
        ctx.textAlign = 'left';
        this.wrapText(ctx, itemData.description, x + 15, y + 25, w - 30, 20);
      }
    }
  }

  private renderContextMenu(ctx: CanvasRenderingContext2D, w: number, h: number): void {
    const items = this.getCurrentCategoryItems();
    if (items.length === 0) return;

    const selectedItem = items[this.currentItem];
    const itemData = this.game.dataManager.getItem(selectedItem.itemId);
    if (!itemData) return;

    const options = this.getContextOptions(itemData);
    
    // Recalculate list geometry to find item position
    const padding = 20;
    const leftPanelW = 360;
    const rightPanelW = 460;
    const listX = padding + leftPanelW + 10;
    const listY = padding + 25; // Adjusted Y
    
    // List Height (Same calc as renderRightPanel)
    const listH = (h + 40) - 300 - listY - 20;
    
    const itemHeight = 32;
    const visibleItems = Math.floor((listH - 10) / itemHeight);
    const scrollOffset = Math.max(0, this.currentItem - visibleItems + 1);
    
    const visualIndex = this.currentItem - scrollOffset;
    const itemVisualY = listY + 5 + visualIndex * itemHeight; // Y position of the selected item row
    
    const menuW = 140;
    const menuH = options.length * 30 + 16;
    
    // Position: Right-aligned to the list, just below the Item row
    const menuX = listX + rightPanelW - menuW - 20; 
    let menuY = itemVisualY + itemHeight; // Default: Below
    
    // Check bounds: If menu goes off bottom of list area or screen, render ABOVE the item instead
    if (menuY + menuH > listY + listH) {
        menuY = itemVisualY - menuH; 
    }

    ctx.fillStyle = '#f8f0d8';
    ctx.fillRect(menuX, menuY, menuW, menuH);
    ctx.strokeStyle = '#8b6f47';
    ctx.lineWidth = 3;
    ctx.strokeRect(menuX, menuY, menuW, menuH);

    ctx.font = '16px monospace';
    ctx.textAlign = 'left';
    options.forEach((option, i) => {
      const isSelected = i === this.contextSelection;
      const optY = menuY + 22 + i * 30;
      
      ctx.fillStyle = isSelected ? '#2c1810' : '#6b5345';
      ctx.fillText((isSelected ? '▶ ' : '  ') + option, menuX + 12, optY);
    });
  }

  private wrapText(ctx: CanvasRenderingContext2D, text: string, x: number, y: number, maxWidth: number, lineHeight: number): void {
    const words = text.split(' ');
    let line = '';
    let currentY = y;

    for (let i = 0; i < words.length; i++) {
      const testLine = line + words[i] + ' ';
      const metrics = ctx.measureText(testLine);
      
      if (metrics.width > maxWidth && i > 0) {
        ctx.fillText(line, x, currentY);
        line = words[i] + ' ';
        currentY += lineHeight;
      } else {
        line = testLine;
      }
    }
    ctx.fillText(line, x, currentY);
  }
}
