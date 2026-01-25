import { Menu } from './MenuSystem';
import { Game, GameState } from '../Game';

export class TitleScreen implements Menu {
  private game: Game;
  private options: string[] = ['New Game', 'Continue'];
  private selection: number = 0;
  private hasSave: boolean = false;
  private isChecking: boolean = true;

  constructor(game: Game) {
    this.game = game;
  }

  public async onOpen(): Promise<void> {
      console.log("[Title] Checking for save file...");
      this.hasSave = await this.game.saveSystem.checkSave(0);
      this.isChecking = false;
      
      // Default to Continue if available
      this.selection = this.hasSave ? 1 : 0;
  }

  public update(dt: number): void {
    if (this.isChecking) return;

    const input = this.game.input;

    if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) {
        this.selection = (this.selection + 1) % this.options.length;
    }
    if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) {
        this.selection = (this.selection - 1 + this.options.length) % this.options.length;
    }

    if (input.isJustPressed('Space') || input.isJustPressed('Enter') || input.isJustPressed('KeyZ')) {
        this.selectOption();
    }
  }

  private selectOption(): void {
      const option = this.options[this.selection];
      
      if (option === 'New Game') {
          console.log("[Title] Starting New Game");
          this.game.menuSystem.pop(); 
          this.game.state = GameState.Overworld;
          
          // Initialize Party (Starter)
          const starter = this.game.dataManager.getPokemonSpecies("001");
          if (starter) {
               const mon = this.game.dataManager.createPokemonInstance(starter, 5);
               this.game.party = [mon];
               console.log(`[Title] Starter ${mon.nickname} added to party.`);
          }
          
          // Load Default Map
          this.game.loadLevel('maps/mom_house_start.json'); // Main Start
      } else if (option === 'Continue') {
          if (!this.hasSave) {
              // Play buzzer?
              return;
          }
          console.log("[Title] Continuing...");
          this.game.menuSystem.pop();
          this.game.state = GameState.Overworld;
          this.game.load(0);
      }
  }

  public render(ctx: CanvasRenderingContext2D): void {
      const width = this.game.display.width;
      const height = this.game.display.height;

      // Background
      ctx.fillStyle = '#000';
      ctx.fillRect(0, 0, width, height);
      
      // Title Text
      ctx.fillStyle = '#FFDD00'; // Pokémon Yellow-ish
      ctx.font = 'bold 48px monospace';
      ctx.textAlign = 'center';
      ctx.fillText('UNKNOWN ENGINE', width / 2, height / 3);
      
      ctx.fillStyle = '#fff';
      ctx.font = '16px monospace';
      ctx.fillText('Press Z or Enter', width / 2, height / 3 + 40);

      // Menu
      const menuY = height / 2 + 50;
      
      if (this.isChecking) {
          ctx.fillStyle = '#aaa';
          ctx.fillText('Loading...', width / 2, menuY);
          return;
      }

      for (let i = 0; i < this.options.length; i++) {
          const opt = this.options[i];
          const y = menuY + (i * 30);
          
          if (opt === 'Continue' && !this.hasSave) {
              ctx.fillStyle = '#555';
          } else if (i === this.selection) {
              ctx.fillStyle = '#f1c40f'; // Highlight
              ctx.fillText('>', width / 2 - 60, y);
          } else {
              ctx.fillStyle = '#fff';
          }
          
          ctx.fillText(opt, width / 2, y);
      }
      
      ctx.textAlign = 'left'; // Reset
  }
}
