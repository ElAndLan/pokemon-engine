import { Menu } from './MenuSystem';
import { Game } from '../Game';
import type { PokemonInstance } from '../data/DataTypes';

/**
 * Menu for selecting a Pokemon from the party
 */
export class PokemonSelectionMenu implements Menu {
  private game: Game;
  private selectedIndex: number = 0;
  private onPokemonSelected: ((pokemon: PokemonInstance, index: number) => void) | null = null;
  private title: string;

  private loadedSprites: Map<number, HTMLImageElement> = new Map();

  constructor(game: Game, title: string = 'Choose a Pokemon') {
    this.game = game;
    this.title = title;
  }

  /**
   * Set callback for when a Pokemon is selected
   */
  public setOnPokemonSelected(callback: (pokemon: PokemonInstance, index: number) => void): void {
    this.onPokemonSelected = callback;
  }

  public async onOpen(): Promise<void> {
    // Reset selection
    this.selectedIndex = 0;
    this.loadedSprites.clear();
    await this.loadPartySprites();
  }

  private async loadPartySprites(): Promise<void> {
    const party = this.game.party;
    if (!party) return;

    for (let i = 0; i < party.length; i++) {
        const pokemon = party[i];
        const species = this.game.dataManager.getPokemonSpecies(pokemon.speciesId);
        if (species && species.assets && species.assets.front) {
            try {
                const response = await (window as any).fs.readImage(species.assets.front);
                if (response.success) {
                    const img = new Image();
                    img.src = `data:image/png;base64,${response.data}`;
                    this.loadedSprites.set(i, img);
                }
            } catch (e) {
                console.error('Failed to load menu sprite', e);
            }
        }
    }
  }

  public onClose(): void {
    // Cleanup
    this.loadedSprites.clear();
  }

  public update(dt: number): void {
    const input = this.game.input;
    const party = this.game.party;

    if (!party || party.length === 0) {
      this.game.menuSystem.pop();
      return;
    }

    const colHeight = 3;

    // Up
    if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
        // If not at top of column
        if (this.selectedIndex % colHeight !== 0) {
            this.selectedIndex--;
        }
    } 
    // Down
    else if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
         // If not at bottom of column AND next index exists
         if (this.selectedIndex % colHeight !== colHeight - 1 && this.selectedIndex + 1 < party.length) {
             this.selectedIndex++;
         }
    }
    // Left
    else if (input.isJustPressed('ArrowLeft') || input.isJustPressed('KeyA')) {
        // If in right column (index >= 3), go left
        if (this.selectedIndex >= colHeight) {
            this.selectedIndex -= colHeight;
        }
    }
    // Right
    else if (input.isJustPressed('ArrowRight') || input.isJustPressed('KeyD')) {
        // If in left column and corresponding slot exists on right
        if (this.selectedIndex < colHeight && this.selectedIndex + colHeight < party.length) {
            this.selectedIndex += colHeight;
        }
    }

    // Select Pokemon
    if (input.isJustPressed('Space') || input.isJustPressed('Enter') || input.isJustPressed('KeyZ')) {
      const selectedPokemon = party[this.selectedIndex];
      if (this.onPokemonSelected) {
        this.onPokemonSelected(selectedPokemon, this.selectedIndex);
      }
      this.game.menuSystem.pop();
    }

    // Cancel
    if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
      this.game.menuSystem.pop();
    }
  }

  public render(ctx: CanvasRenderingContext2D): void {
    const w = this.game.display.width;
    const h = this.game.display.height;
    const party = this.game.party;

    if (!party || party.length === 0) {
      return;
    }

    // Semi-transparent overlay
    ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
    ctx.fillRect(0, 0, w, h);

    // Menu layout constants
    const menuWidth = 800;
    const menuHeight = 400; // fit 3 rows of ~120
    const menuX = (w - menuWidth) / 2;
    const menuY = (h - menuHeight) / 2;
    
    // Panel Background
    ctx.fillStyle = '#2a2a2a';
    ctx.fillRect(menuX, menuY, menuWidth, menuHeight);
    ctx.strokeStyle = '#fff';
    ctx.lineWidth = 4;
    ctx.strokeRect(menuX, menuY, menuWidth, menuHeight);

    // Title
    ctx.fillStyle = '#fff';
    ctx.font = 'bold 24px monospace';
    ctx.textAlign = 'left';
    ctx.fillText(this.title, menuX + 20, menuY + 40);

    // Items
    const colWidth = (menuWidth - 40) / 2; // 2 columns with padding
    const rowHeight = 100;
    const startY = menuY + 60;

    party.forEach((pokemon, index) => {
      // Determine Grid Pos
      const col = index < 3 ? 0 : 1;
      const row = index % 3;
      
      const x = menuX + 20 + col * colWidth;
      const y = startY + row * rowHeight;
      const w = colWidth - 10;
      const h = rowHeight - 10;
      
      const isSelected = index === this.selectedIndex;

      // Item Box
      ctx.fillStyle = isSelected ? '#444' : '#333';
      ctx.fillRect(x, y, w, h);
      
      if (isSelected) {
          ctx.strokeStyle = '#f1c40f'; // Gold highlight
          ctx.lineWidth = 3;
          ctx.strokeRect(x, y, w, h);
      }

      // 1. Sprite
      const sprite = this.loadedSprites.get(index);
      const spriteSize = 80;
      if (sprite) {
          // Centered vertically in box, left aligned
          ctx.drawImage(sprite, x + 5, y + (h - spriteSize) / 2, spriteSize, spriteSize);
      } else {
          // Placeholder
          ctx.fillStyle = '#111';
          ctx.fillRect(x + 5, y + (h - spriteSize) / 2, spriteSize, spriteSize);
      }

      const textX = x + spriteSize + 20;

      // 2. Name & Level
      const pokemonName = pokemon.nickname || this.game.dataManager.getPokemonSpecies(pokemon.speciesId)?.name || 'Unknown';
      ctx.fillStyle = '#fff';
      ctx.font = 'bold 18px monospace';
      ctx.textAlign = 'left';
      ctx.fillText(pokemonName, textX, y + 30);
      
      ctx.font = '16px monospace';
      ctx.fillStyle = '#f1c40f';
      ctx.fillText(`Lv${pokemon.level}`, x + w - 60, y + 30);


      // 3. HP Bar
      const hpBarWidth = 180;
      const hpBarHeight = 10;
      const hpBarX = textX;
      const hpBarY = y + 50;
      const hpPercent = pokemon.currentHp / pokemon.currentStats.hp;
      
      // HP Background
      ctx.fillStyle = '#111';
      ctx.fillRect(hpBarX, hpBarY, hpBarWidth, hpBarHeight);
      
      // HP Fill
      let hpColor = '#2ecc71';
      if (hpPercent <= 0.2) hpColor = '#e74c3c';
      else if (hpPercent <= 0.5) hpColor = '#f1c40f';
      
      ctx.fillStyle = hpColor;
      ctx.fillRect(hpBarX, hpBarY, hpBarWidth * hpPercent, hpBarHeight);
      
      // HP Text
      ctx.fillStyle = '#ccc';
      ctx.font = '14px monospace';
      ctx.fillText(`${Math.ceil(pokemon.currentHp)}/${pokemon.currentStats.hp}`, hpBarX, hpBarY + 25);
      
      // Status
      if (pokemon.status !== 'None') {
          ctx.fillStyle = '#e74c3c'; // Red-ish
          ctx.fillText(pokemon.status.toUpperCase(), x + w - 50, hpBarY + 25);
      }
    });

    // Hints
    ctx.fillStyle = '#aaa';
    ctx.font = '14px monospace';
    ctx.textAlign = 'center';
    ctx.fillText('Arrows: Select   Space/Z: Confirm   X/Esc: Cancel', this.game.display.width / 2, menuY + menuHeight + 25);
  }
}
