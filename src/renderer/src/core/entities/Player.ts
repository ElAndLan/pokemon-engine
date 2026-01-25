import { Display } from '../Display';
import { InputManager } from '../InputManager';
import { Tilemap } from '../Tilemap';
import { TILE_SIZE } from '../consts';
import { NPC } from './NPC';

export enum Direction {
  Down = 0,
  Left = 1,
  Right = 2,
  Up = 3
}

export class Player {
  // Grid Position (Logical)
  public gridX: number = 0;
  public gridY: number = 0;

  // Pixel Position (Visual)
  public x: number = 0;
  public y: number = 0;
  
  public width: number = TILE_SIZE;
  public height: number = TILE_SIZE;

  private moveSpeed: number = 2; // Pixels per frame
  private isMoving: boolean = false;
  private targetX: number = 0;
  private targetY: number = 0;
  private TILE_SIZE: number = TILE_SIZE;

  // Visuals
  private sprite: HTMLImageElement | null = null;
  private isLoaded: boolean = false;
  public direction: Direction = Direction.Down;
  private frame: number = 0; // 0, 1, 2, 3
  private frameTimer: number = 0;
  private FRAME_DELAY: number = 10; // Frames to wait before switching
  private spriteWidth: number = 0; // Calculated on load
  private spriteHeight: number = 0;

  constructor() {
      this.loadSprite();
  }

  private async loadSprite(): Promise<void> {
      try {
          const path = 'data/player/pokemon_main_trainer.png'; // Updated to PNG
          console.log(`[Player] Loading sprite from: ${path}`);
          
          const response = await (window as any).fs.readImage(path);
          
          if (response && response.success) {
              this.sprite = new Image();
              // Base64 Prefix for PNG
              this.sprite.src = `data:image/png;base64,${response.data}`;
              
              this.sprite.onload = () => {
                  this.isLoaded = true;
                  this.spriteWidth = this.sprite!.width / 4; 
                  this.spriteHeight = this.sprite!.height / 4; 
                  console.log(`[Player] Sprite Loaded. Size: ${this.sprite!.width}x${this.sprite!.height} Frame: ${this.spriteWidth}x${this.spriteHeight}`);
              };
              
              this.sprite.onerror = (e) => {
                  console.error('[Player] Start loading failed', e);
              }
          } else {
              console.error(`[Player] Failed to read image file: ${response.error}`);
          }
      } catch (e) {
          console.error('[Player] Exception loading sprite', e);
      }
  }

  public setPosition(gridX: number, gridY: number, tileSize: number = TILE_SIZE): void {
    this.gridX = gridX;
    this.gridY = gridY;
    this.TILE_SIZE = tileSize;
    
    // Update visual size to match grid (or sprite size?)
    // Typically sprite might be taller.
    // For now, keep hit-box as tile size.
    this.width = tileSize;
    this.height = tileSize;

    this.x = gridX * tileSize;
    this.y = gridY * tileSize;
    this.targetX = this.x;
    this.targetY = this.y;
  }

  public update(input: InputManager, map: Tilemap, npcs: NPC[] = []): boolean {
    if (this.isMoving) {
      return this.continueMovement();
    } else {
      this.handleInput(input, map, npcs);
      return false;
    }
  }

  private handleInput(input: InputManager, map: Tilemap, npcs: NPC[]): void {
    let dx = 0;
    let dy = 0;

    if (input.isDown('ArrowUp') || input.isDown('KeyW')) {
        dy = -1; 
        this.direction = Direction.Up;
    }
    else if (input.isDown('ArrowDown') || input.isDown('KeyS')) {
        dy = 1; 
        this.direction = Direction.Down;
    }
    else if (input.isDown('ArrowLeft') || input.isDown('KeyA')) {
        dx = -1; 
        this.direction = Direction.Left;
    }
    else if (input.isDown('ArrowRight') || input.isDown('KeyD')) {
        dx = 1; 
        this.direction = Direction.Right;
    }

    if (dx !== 0 || dy !== 0) {
      this.attemptMovement(dx, dy, map, npcs);
    } else {
        // Idle
        this.frame = 0;
    }
  }

  private attemptMovement(dx: number, dy: number, map: Tilemap, npcs: NPC[]): void {
    const targetGridX = this.gridX + dx;
    const targetGridY = this.gridY + dy;

    // 1. Check NPC Collision
    const isBlockedByNPC = npcs.some(npc => npc.gridX === targetGridX && npc.gridY === targetGridY);
    if (isBlockedByNPC) {
        return; // Blocked
    }

    // 2. Check Map Collision
    if (map.isWalkable(targetGridX, targetGridY)) {
        this.gridX = targetGridX;
        this.gridY = targetGridY;
        
        this.targetX = this.gridX * this.TILE_SIZE;
        this.targetY = this.gridY * this.TILE_SIZE;
        this.isMoving = true;
    }
  }

  private continueMovement(): boolean {
    const speed = this.moveSpeed;
    
    // Animate
    this.frameTimer++;
    if (this.frameTimer > this.FRAME_DELAY) {
        this.frame = (this.frame + 1) % 4; // 0, 1, 2, 3
        this.frameTimer = 0;
    }
    
    // Move towards target
    if (this.x < this.targetX) this.x = Math.min(this.x + speed, this.targetX);
    if (this.x > this.targetX) this.x = Math.max(this.x - speed, this.targetX);
    if (this.y < this.targetY) this.y = Math.min(this.y + speed, this.targetY);
    if (this.y > this.targetY) this.y = Math.max(this.y - speed, this.targetY);

    // Check if reached
    if (this.x === this.targetX && this.y === this.targetY) {
      this.isMoving = false;
      this.frame = 0; // Return to idle frame? Or keep animating?
      return true; // Step Finished
    }
    
    return false;
  }

  public render(display: Display): void {
      // Deprecated, use renderWithCamera
  }

  // Helper to get render position relative to camera
  public renderWithCamera(display: Display, cameraX: number, cameraY: number): void {
      if (this.isLoaded && this.sprite) {
          // Source Rect
          const srcX = this.frame * this.spriteWidth;
          const srcY = this.direction * this.spriteHeight;

          // Draw
          display.ctx.drawImage(
              this.sprite,
              srcX, srcY, this.spriteWidth, this.spriteHeight, // Source
              this.x - cameraX, this.y - cameraY, this.width, this.height // Dest (Stretched to Tile Size?)
          );
      } else {
          // Fallback Red Box
          display.ctx.fillStyle = '#ff0000'; // Player is Red
          display.ctx.fillRect(
              this.x - cameraX, 
              this.y - cameraY, 
              this.width, 
              this.height
          );
      }
  }
}
