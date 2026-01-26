
import { Menu } from './MenuSystem';
import { Game } from '../Game';
import { PokemonInstance } from '../data/DataTypes';
import { SummaryScreen } from './SummaryScreen';

export type PartyScreenMode = 'VIEW' | 'BATTLE_SWITCH';

export class PartyScreen implements Menu {
  private game: Game;
  private mode: PartyScreenMode;
  private selection: number = 0;
  private contextMenuOpen: boolean = false;
  private contextSelection: number = 0;
  
  // Callback for Battle Mode
  public onResult: ((pokemon: PokemonInstance | null) => void) | null = null;

  constructor(game: Game, mode: PartyScreenMode = 'VIEW') {
    this.game = game;
    this.mode = mode;
  }

  private imageCache: Map<string, HTMLImageElement> = new Map();
  private switchSource: number | null = null; // New state for swapping

  public async onOpen(): Promise<void> {
      console.log(`[PartyScreen] Opened in ${this.mode} mode.`);
      this.selection = 0;
      this.contextMenuOpen = false;
      this.switchSource = null;
      
      // Preload Images
      console.log('[PartyScreen] Preloading sprites...');
      for (const mon of this.game.party) {
          await this.game.dataManager.loadPokemonSpecies(mon.speciesId);
          const species = this.game.dataManager.getPokemonSpecies(mon.speciesId);
          if (species) {
              // Load Front Sprite (View Mode wants detail) or Icon
              // User asked for "Front Sprite".
              const path = species.assets.front; 
              if (!this.imageCache.has(path)) {
                   const res = await (window as any).fs.readImage(path);
                   if (res.success) {
                       const img = new Image();
                       img.onload = () => {
                           console.log(`[PartyScreen] Loaded sprite: ${path}`);
                       };
                       img.src = `data:image/png;base64,${res.data}`;
                       this.imageCache.set(path, img);
                   }
              }
          }
      }
  }

  private getPokemonDisplayName(mon: PokemonInstance): string {
      if (mon.nickname) {
          return mon.nickname;
      }
      const species = this.game.dataManager.getPokemonSpecies(mon.speciesId);
      return species?.name || mon.speciesId || 'Unknown';
  }

  public onClose(): void {
      console.log("[PartyScreen] Closed");
      this.imageCache.clear(); // Clear cache to free memory?? Or keep it? keeping it might be better for performance but lets clear for now.
  }

  public update(dt: number): void {
      const input = this.game.input;

      if (this.contextMenuOpen) {
          this.updateContextMenu(input);
          return;
      }

      // Grid/List Navigation
      if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
          this.selection = Math.min(this.selection + 1, this.game.party.length - 1);
      }
      if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
          this.selection = Math.max(this.selection - 1, 0);
      }

      // Confirm (Open Context Menu or Select)
      if (input.isJustPressed('Space') || input.isJustPressed('Enter') || input.isJustPressed('KeyZ')) {
          if (this.mode === 'VIEW') {
              if (this.switchSource !== null) {
                  // Complete the Swap
                  this.swapPokemon(this.switchSource, this.selection);
                  this.switchSource = null;
              } else {
                  this.contextMenuOpen = true;
                  this.contextSelection = 0;
              }
          } else if (this.mode === 'BATTLE_SWITCH') {
              // Select for Switch
              const selectedMon = this.game.party[this.selection];
              if (this.onResult) {
                  this.onResult(selectedMon);
              }
              this.game.menuSystem.pop();
          }
      }

      // Cancel / Back
      if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
          if (this.switchSource !== null) {
              // Cancel Swap mode
              this.switchSource = null;
              return;
          }
          
          if (this.mode === 'BATTLE_SWITCH') {
              if (this.onResult) this.onResult(null); // Cancel
          }
          this.game.menuSystem.pop();
      }
  }
  
  private swapPokemon(indexA: number, indexB: number): void {
      if (indexA === indexB) return;
      
      const temp = this.game.party[indexA];
      this.game.party[indexA] = this.game.party[indexB];
      this.game.party[indexB] = temp;
      console.log(`[PartyScreen] Swapped slot ${indexA} with ${indexB}`);
  }

  private updateContextMenu(input: any): void {
      const options = ['Summary', 'Switch', 'Item', 'Cancel'];
      
      if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
          this.contextSelection = (this.contextSelection + 1) % options.length;
      }
      if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
          this.contextSelection = (this.contextSelection - 1 + options.length) % options.length;
      }

      if (input.isJustPressed('Space') || input.isJustPressed('Enter') || input.isJustPressed('KeyZ')) {
          const opt = options[this.contextSelection];
          if (opt === 'Cancel') {
              this.contextMenuOpen = false;
          } else if (opt === 'Switch') {
               // Activate Switch Mode
               this.switchSource = this.selection;
               this.contextMenuOpen = false;
          } else if (opt === 'Summary') {
               // Open Summary Screen
               const selectedMon = this.game.party[this.selection];
               const summaryScreen = new SummaryScreen(this.game, selectedMon);
               summaryScreen.onClose = () => {
                   this.game.menuSystem.pop();
               };
               this.game.menuSystem.push(summaryScreen);
               this.contextMenuOpen = false;
          } else {
              this.contextMenuOpen = false;
          }
      }
      
      if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
          this.contextMenuOpen = false;
      }
  }

  public render(ctx: CanvasRenderingContext2D): void {
      const w = this.game.display.width;
      const h = this.game.display.height;

      // Background
      ctx.fillStyle = '#2c3e50';
      ctx.fillRect(0, 0, w, h);

      // Title
      ctx.fillStyle = '#fff';
      ctx.font = '20px serif';
      ctx.textAlign = 'left';
      
      let title = this.mode === 'VIEW' ? 'Pokemon Party' : 'Choose a Pokemon';
      if (this.switchSource !== null) title = `Move to where?`;
      
      ctx.fillText(title, 20, 30);

      // Render Party List
      this.game.party.forEach((mon, i) => {
           const y = 50 + (i * 80); // Increased Height per row (50 -> 80) for Sprites
           
           const isSelected = i === this.selection;
           const isSwitchSource = i === this.switchSource;
           
           // Box
           if (isSwitchSource) {
                ctx.fillStyle = '#f39c12'; // Orange for Source
           } else if (isSelected) {
                ctx.fillStyle = '#34495e'; // Dark Blue for Cursor
           } else {
                ctx.fillStyle = 'rgba(0,0,0,0.2)';
           }
           
           ctx.fillRect(10, y, w - 20, 75); // Taller box
           ctx.strokeStyle = isSelected ? '#f1c40f' : '#7f8c8d';
           ctx.lineWidth = isSelected ? 2 : 1;
           ctx.strokeRect(10, y, w - 20, 75);

           const species = this.game.dataManager.getPokemonSpecies(mon.speciesId);
           if (species) {
               const path = species.assets.front;
               const img = this.imageCache.get(path);
               if (img && img.complete && img.naturalWidth > 0) {
                   // Draw Sprite (Scaled down approx 64x64)
                   ctx.drawImage(img, 20, y + 5, 64, 64);
               }
           }

           // Name & Level
           const textX = 100; // Shifted right for sprite
           ctx.fillStyle = '#ecf0f1';
           ctx.font = 'bold 20px monospace';
           ctx.fillText(`${this.getPokemonDisplayName(mon)}`, textX, y + 30);
           
           // Gender/Level Line
           ctx.font = '16px monospace';
           ctx.fillText(`Lv.${mon.level}`, textX, y + 55);

           // HP Bar
           const hpPercent = mon.currentHp / mon.currentStats.hp;
           const barX = 300;
           const barY = y + 35;
           const barW = 150;
           const barH = 15;
           
           // Bar BG
           ctx.fillStyle = '#555';
           ctx.fillRect(barX, barY, barW, barH);
           
           // Bar FG
           ctx.fillStyle = hpPercent > 0.5 ? '#2ecc71' : hpPercent > 0.2 ? '#f1c40f' : '#e74c3c';
           ctx.fillRect(barX, barY, barW * hpPercent, barH);
           
           // Text
           ctx.fillStyle = '#fff';
           ctx.font = '14px monospace';
           ctx.fillText(`${mon.currentHp}/${mon.currentStats.hp}`, barX + barW + 10, barY + 12);

           // Status
           if (mon.status !== 'None') {
               ctx.fillStyle = '#e74c3c'; // Red
               ctx.font = 'bold 14px monospace';
               ctx.fillText(mon.status.toUpperCase(), barX, y + 25);
           }
      });

      // Context Menu Overlay
      if (this.contextMenuOpen) {
           const cx = w - 150;
           const cy = h - 150;
           
           // Menu Box
           ctx.fillStyle = '#2d3436';
           ctx.strokeStyle = '#fff';
           ctx.fillRect(cx, cy, 140, 110);
           ctx.strokeRect(cx, cy, 140, 110);

           const options = ['Summary', 'Switch', 'Item', 'Cancel'];
           ctx.font = '16px monospace';
           options.forEach((opt, i) => {
               ctx.fillStyle = i === this.contextSelection ? '#f1c40f' : '#fff';
               
               // Cursor Arrow
               if (i === this.contextSelection) {
                   ctx.fillText('>', cx + 10, cy + 25 + (i * 25));
               }
               
               ctx.fillText(opt, cx + 25, cy + 25 + (i * 25));
           });
      }
  }
}
