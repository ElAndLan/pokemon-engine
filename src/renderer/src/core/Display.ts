export class Display {
  public canvas: HTMLCanvasElement;
  public ctx: CanvasRenderingContext2D;
  
  // Resolution: 960x640 (Larger for better editor visibility)
  public readonly width: number = 960;
  public readonly height: number = 640;
  public readonly scale: number = 2; 

  constructor(parentId: string = 'app') {
    this.canvas = document.createElement('canvas');
    this.canvas.width = this.width * this.scale;
    this.canvas.height = this.height * this.scale;
    
    // Disable anti-aliasing for pixel art look
    this.canvas.style.imageRendering = 'pixelated';
    this.canvas.style.width = '100%';
    this.canvas.style.height = '100%';
    this.canvas.style.objectFit = 'contain';
    
    const parent = document.getElementById(parentId);
    if (parent) {
      parent.appendChild(this.canvas);
    } else {
      document.body.appendChild(this.canvas);
    }

    const context = this.canvas.getContext('2d');
    if (!context) throw new Error('Failed to get 2D context');
    this.ctx = context;
    
    // Scale the context so drawing at 240x160 automatically scales up
    this.ctx.scale(this.scale, this.scale);
    this.ctx.imageSmoothingEnabled = false;
  }

  public clear(): void {
    this.ctx.fillStyle = '#000000';
    this.ctx.fillRect(0, 0, this.width, this.height);
  }
}
