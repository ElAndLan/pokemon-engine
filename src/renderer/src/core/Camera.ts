export class Camera {
  public x: number = 0;
  public y: number = 0;
  public width: number;
  public height: number;
  public mapWidth: number = 0;
  public mapHeight: number = 0;

  constructor(width: number, height: number) {
    this.width = width;
    this.height = height;
  }

  public setMapSize(width: number, height: number): void {
    this.mapWidth = width;
    this.mapHeight = height;
  }

  public follow(target: { x: number, y: number }): void {
    // Center the camera on the target
    // We assume the target x/y is the center of the target, 
    // or we might want to pass target width/height to center perfectly.
    // For a 16x16 player, passing x,y as top-left means we should offset slightly.
    // Let's assume target.x/y is the top-left for now.
    
    // Target Center
    const centerX = target.x + 8; // Half of 16 (default tile/player size)
    const centerY = target.y + 8;

    // Desired Camera Top-Left
    let desiredX = centerX - (this.width / 2);
    let desiredY = centerY - (this.height / 2);

    // Clamp to Map Bounds
    // 0 <= camera.x <= mapWidth - cameraWidth
    this.x = Math.max(0, Math.min(desiredX, this.mapWidth - this.width));
    this.y = Math.max(0, Math.min(desiredY, this.mapHeight - this.height));
    
    // Floor to prevent sub-pixel rendering artifacts
    this.x = Math.floor(this.x);
    this.y = Math.floor(this.y);
  }
}
