
import { Display } from '../Display';
import { EventManager } from './EventManager';
import { InputManager } from '../InputManager';

export class DialogBox {
  public isVisible: boolean = false;
  private content: string = '';
  private display: Display;
  private input: InputManager;
  private eventManager: EventManager | null = null;
  
  // Typewriter effect state
  private renderedText: string = '';
  private charIndex: number = 0;
  private timer: number = 0;
  private SPEED: number = 2; // Frames per char

  constructor(display: Display, input: InputManager) {
      this.display = display;
      this.input = input;
  }
  
  public setEventManager(em: EventManager) {
      this.eventManager = em;
  }

  public show(text: string): void {
      this.content = text;
      this.renderedText = '';
      this.charIndex = 0;
      this.timer = 0;
      this.isVisible = true;
      this.input.reset(); // Clear triggers so we don't accidentally skip
  }

  public update(): void {
      if (!this.isVisible) return;
      
      // Typewriter Effect
      if (this.charIndex < this.content.length) {
          this.timer++;
          if (this.timer >= this.SPEED) {
              this.renderedText += this.content[this.charIndex];
              this.charIndex++;
              this.timer = 0;
          }
      }

      // Input Handling
      if (this.input.isJustPressed('KeyZ') || this.input.isJustPressed('Space') || this.input.isJustPressed('Enter')) {
          if (this.charIndex < this.content.length) {
              // Instant Finish
              this.renderedText = this.content;
              this.charIndex = this.content.length;
          } else {
              // Close
              this.close();
          }
      }
  }

  private close(): void {
      this.isVisible = false;
      // Reset input so the key press doesn't carry over
      this.input.reset();
      if (this.eventManager) {
          this.eventManager.resume();
      }
  }

  public render(): void {
      if (!this.isVisible) return;

      const ctx = this.display.ctx;
      const W = this.display.width;
      const H = this.display.height;
      
      // Dialog box centered horizontally, in lower-middle area
      const h = 70; // Height of box
      const w = 600; // Fixed width
      const x = (W - w) / 2; // Center horizontally
      const y = H * 0.65; // Position at 65% down the screen

      // Draw Box Background - WHITE
      ctx.fillStyle = 'rgb(255, 255, 255)';
      ctx.fillRect(x, y, w, h);
      
      // Border - DARK BLUE
      ctx.strokeStyle = 'rgb(25, 50, 100)'; // Dark blue
      ctx.lineWidth = 4;
      ctx.strokeRect(x, y, w, h);

      // Text - BLACK
      ctx.fillStyle = 'black';
      ctx.font = 'bold 14px Arial'; // Slightly smaller font
      ctx.textBaseline = 'top';
      
      // Multi-line support (simple)
      const lines = this.wrapText(this.renderedText, 70);
      lines.forEach((line, i) => {
          ctx.fillText(line, x + 12, y + 12 + (i * 18));
      });
      
      // Cursor indicator if waiting - RED
      if (this.charIndex >= this.content.length) {
          // Blink
          if (Math.floor(Date.now() / 500) % 2 === 0) {
             ctx.fillStyle = '#ff0000';
             ctx.fillRect(x + w - 20, y + h - 20, 12, 12);
          }
      }
  }

  // Simple word wrapper
  private wrapText(text: string, maxChars: number): string[] {
      const words = text.split(' ');
      const lines: string[] = [];
      let currentLine = words[0];

      for (let i = 1; i < words.length; i++) {
          if (currentLine.length + 1 + words[i].length <= maxChars) {
              currentLine += ' ' + words[i];
          } else {
              lines.push(currentLine);
              currentLine = words[i];
          }
      }
      lines.push(currentLine);
      return lines;
  }
}
