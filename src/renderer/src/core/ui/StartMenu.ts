import { Menu } from './MenuSystem';
import { Game } from '../Game';
import { PCMenu } from './PCMenu'; // Import
import { PartyScreen } from './PartyScreen';
import { BagMenu } from './BagMenu';

export class StartMenu implements Menu {
  private game: Game;
  private options: string[] = ['Pokedex', 'Pokemon', 'Bag', 'PC', 'Save', 'Options', 'Exit'];
  private selection: number = 0;
  private width: number = 120;
  private height: number = 0;
  
  constructor(game: Game) {
    this.game = game;
    this.height = this.options.length * 30 + 20; // Dynamic height
  }

  public onOpen(): void {
    this.selection = 0;
    console.log("Start Menu Opened");
  }

  public onClose(): void {
      console.log("Start Menu Closed");
  }

  public update(dt: number): void {
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

    if (input.isJustPressed('Escape') || input.isJustPressed('KeyX')) {
        this.game.menuSystem.pop();
    }
  }

  private selectOption(): void {
      const option = this.options[this.selection];
      console.log(`Selected: ${option}`);
      
      switch (option) {
          case 'Exit':
              this.game.menuSystem.pop();
              break;
          case 'Save':
              console.log("Saving game...");
              this.game.save(0); // Slot 0
              break;
          case 'Pokemon':
              this.game.menuSystem.push(new PartyScreen(this.game));
              break;
          case 'Bag':
              this.game.menuSystem.push(new BagMenu(this.game, 'OVERWORLD'));
              break;
          case 'PC':
              this.game.menuSystem.push(new PCMenu(this.game));
              break;
          default:
              console.log(`${option} is not implemented yet.`);
              break;
      }
  }

  public render(ctx: CanvasRenderingContext2D): void {
      // Use logical width from Display class, as ctx.canvas.width gives physical size (scaled)
      const screenW = this.game.display.width;
      const screenH = this.game.display.height;
      
      const margin = 120; // Increased from 20 to be clearly safe 
      const x = screenW - this.width - margin;
      const y = 30; // Slightly improved y-margin too

      // Draw Box
      ctx.fillStyle = '#2d3436';
      ctx.strokeStyle = '#fff';
      ctx.lineWidth = 2;
      
      ctx.fillRect(x, y, this.width, this.height);
      ctx.strokeRect(x, y, this.width, this.height);

      // Draw Text
      ctx.fillStyle = '#fff';
      ctx.font = '16px monospace';
      ctx.textAlign = 'left';

      for (let i = 0; i < this.options.length; i++) {
          const optY = y + 25 + (i * 30);
          
          if (i === this.selection) {
              ctx.fillText('>', x + 10, optY);
              ctx.fillStyle = '#f1c40f'; // Highlight color
          } else {
              ctx.fillStyle = '#fff';
          }
          
          ctx.fillText(this.options[i], x + 25, optY);
      }
  }
}
