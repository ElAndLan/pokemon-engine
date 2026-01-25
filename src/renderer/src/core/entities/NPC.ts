
import { Display } from '../Display';
import { Tilemap } from '../Tilemap';
import { TILE_SIZE } from '../consts';

export enum Direction {
  Down = 0,
  Left = 1,
  Right = 2,
  Up = 3
}

export class NPC {
  public id: number;
  public uniqueId: string; // The "name" from Tiled (e.g. "mom", "rival")
  
  // Grid Position
  public gridX: number = 0;
  public gridY: number = 0;

  // Pixel Position
  public x: number = 0;
  public y: number = 0;
  
  public width: number = TILE_SIZE;
  public height: number = TILE_SIZE;

  // Movement State
  private moveSpeed: number = 1; // Slower than player usually
  private isMoving: boolean = false;
  private targetX: number = 0;
  private targetY: number = 0;
  
  private isHopping: boolean = false;
  private hopHeight: number = 0;
  private hopTimer: number = 0;

  // Visuals
  private sprite: HTMLImageElement | null = null;
  private isLoaded: boolean = false;
  private direction: Direction = Direction.Down;
  private frame: number = 0;
  private frameTimer: number = 0;
  private FRAME_DELAY: number = 10; // Faster animation
  private idleTimer: number = 0;
  private idleBob: number = 0;
  private spriteWidth: number = 16;
  private spriteHeight: number = 20; // Default

  public triggerId: string | null = null;
  public scale: number = 1.0;

  constructor(id: number, uniqueId: string, gridX: number, gridY: number, spritePath: string = 'data/player/pokemon_main_trainer.png', triggerId: string | null = null, scale: number = 1.0) {
      this.id = id;
      this.uniqueId = uniqueId;
      this.gridX = gridX;
      this.gridY = gridY;
      this.x = gridX * TILE_SIZE;
      this.y = gridY * TILE_SIZE;
      this.targetX = this.x;
      this.targetY = this.y;
      this.triggerId = triggerId;
      this.scale = scale;
      
      this.loadSprite(spritePath);
  }

  private async loadSprite(path: string): Promise<void> {
      try {
          // Default fallback for now
          // In real engine, we'd load specific NPC sprites
          if (!path) path = 'data/player/pokemon_main_trainer.png';
          
          
          const response = await (window as any).fs.readImage(path);
          console.log(`[NPC] Sprite Load Response for ${path}:`, response);

          if (response && response.success) {
              this.sprite = new Image();
              this.sprite.src = `data:image/png;base64,${response.data}`;
              this.sprite.onload = () => {
                  this.isLoaded = true;
                  // Strictly follow Player.ts logic: Assume 4x4 sheet
                  this.spriteWidth = this.sprite!.width / 4;
                  this.spriteHeight = this.sprite!.height / 4;
                  
                  console.log(`[NPC] Sprite Loaded: ${this.spriteWidth}x${this.spriteHeight} (Source Frame Size)`);
              };
              this.sprite.onerror = (e) => console.error('[NPC] Sprite Image Error:', e);
          } else {
              console.error('[NPC] Failed to read image:', response?.error);
          }
      } catch (e) {
          console.error('[NPC] Failed to load sprite', e);
      }
  }

  // API for Event Manager
  
  public face(dir: Direction) {
      if (!this.isMoving) {
          this.direction = dir;
      }
  }


  
  public hop() {
      if (!this.isHopping && !this.isMoving) {
          this.isHopping = true;
          this.hopTimer = 0;
      }
  }

  private moveResolve: (() => void) | null = null;
  
  public update(): void {
      // Hopping Logic
      if (this.isHopping) {
          this.hopTimer++;
          // Simple sine wave hop
          this.hopHeight = Math.sin((this.hopTimer / 20) * Math.PI) * 8;
          if (this.hopTimer >= 20) {
              this.isHopping = false;
              this.hopHeight = 0;
          }
      }

      // Movement Logic
      if (this.isMoving) {
          const speed = this.moveSpeed;
          
          // Animate walking frames
          this.frameTimer++;
          if (this.frameTimer > this.FRAME_DELAY) {
              this.frame = (this.frame + 1) % 4;
              this.frameTimer = 0;
          }

          if (this.x < this.targetX) this.x = Math.min(this.x + speed, this.targetX);
          if (this.x > this.targetX) this.x = Math.max(this.x - speed, this.targetX);
          if (this.y < this.targetY) this.y = Math.min(this.y + speed, this.targetY);
          if (this.y > this.targetY) this.y = Math.max(this.y - speed, this.targetY);

          if (this.x === this.targetX && this.y === this.targetY) {
              this.isMoving = false;
              this.frame = 0; // Reset to idle frame
              this.frameTimer = 0;
              
              if (this.moveResolve) {
                  this.moveResolve();
                  this.moveResolve = null;
              }
          }
      } else {
          // Idle animation - subtle bobbing
          this.idleTimer++;
          this.idleBob = Math.sin(this.idleTimer / 60) * 0.5; // Very subtle
      }
  }
  
  public async walk(dx: number, dy: number, map: Tilemap, ignoreCollision: boolean = false): Promise<void> {
      console.log(`[NPC] walk() called: dx=${dx}, dy=${dy}, isMoving=${this.isMoving}, ignoreCollision=${ignoreCollision}`);
      if (this.isMoving) return;

      const targetGridX = this.gridX + dx;
      const targetGridY = this.gridY + dy;

      console.log(`[NPC] Checking walkability: (${targetGridX}, ${targetGridY})`);
      
      // During scripted events, ignore collision
      const canWalk = ignoreCollision || map.isWalkable(targetGridX, targetGridY);
      
      if (canWalk) {
          this.direction = dx > 0 ? 2 : (dx < 0 ? 1 : (dy > 0 ? 0 : 3));
          
          this.gridX = targetGridX;
          this.gridY = targetGridY;
          this.targetX = targetGridX * TILE_SIZE;
          this.targetY = targetGridY * TILE_SIZE;
          this.isMoving = true;
          
          console.log(`[NPC] Starting walk to (${targetGridX}, ${targetGridY}), direction=${this.direction}`);
          
          return new Promise(resolve => {
              this.moveResolve = resolve;
          });
      } else {
          console.log(`[NPC] Target position not walkable!`);
      }
      return Promise.resolve();
  }

  public render(display: Display, cameraX: number, cameraY: number): void {
      const drawY = this.y - this.hopHeight - this.idleBob;
      
      if (this.isLoaded && this.sprite) {
          const srcX = this.frame * this.spriteWidth;
          const srcY = this.direction * this.spriteHeight;
          
          // Calculate Alignment Offset
          // Center Horizontally: (TileWidth - (SpriteWidth * Scale)) / 2
          // Align Bottom: TileHeight - (SpriteHeight * Scale)
          const destWidth = this.spriteWidth * this.scale;
          const destHeight = this.spriteHeight * this.scale;
          
          const offX = (this.width - destWidth) / 2;
          const offY = (this.height - destHeight);

          // Draw at Scaled Size (Aligned)
          display.ctx.drawImage(
              this.sprite,
              srcX, srcY, this.spriteWidth, this.spriteHeight, // Source
              Math.floor(this.x - cameraX + offX), Math.floor(drawY - cameraY + offY), destWidth, destHeight // Dest
          );
      } else {
          // Fallback
          display.ctx.fillStyle = '#ffaaaa';
          display.ctx.fillRect(this.x - cameraX, drawY - cameraY, this.width, this.height);
      }
  }
}
