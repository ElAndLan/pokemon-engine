/**
 * LayerManager - Manages visibility, opacity, and locking for map layers
 */

export interface LayerState {
  name: string;
  visible: boolean;
  opacity: number;
  locked: boolean;
}

export class LayerManager {
  private layers: Map<string, LayerState> = new Map();
  
  constructor() {
    // Initialize default layers
    this.addLayer('Ground', true, 1.0, false);
    this.addLayer('Decoration', true, 1.0, false);
    this.addLayer('Collision', true, 0.5, false);
    this.addLayer('Encounters', true, 0.7, false);
  }
  
  /**
   * Add a new layer to the manager
   */
  public addLayer(name: string, visible: boolean, opacity: number, locked: boolean): void {
    this.layers.set(name, { name, visible, opacity, locked });
  }
  
  /**
   * Toggle the visibility of a layer
   */
  public toggleVisibility(layerName: string): void {
    const layer = this.layers.get(layerName);
    if (layer) {
      layer.visible = !layer.visible;
      console.log(`[LayerManager] Layer ${layerName} visibility: ${layer.visible}`);
    }
  }
  
  /**
   * Set the opacity of a layer (0.0 to 1.0)
   */
  public setOpacity(layerName: string, opacity: number): void {
    const layer = this.layers.get(layerName);
    if (layer) {
      layer.opacity = Math.max(0, Math.min(1, opacity));
    }
  }
  
  /**
   * Toggle the locked state of a layer
   */
  public toggleLock(layerName: string): void {
    const layer = this.layers.get(layerName);
    if (layer) {
      layer.locked = !layer.locked;
    }
  }
  
  /**
   * Check if a layer is visible
   */
  public isVisible(layerName: string): boolean {
    return this.layers.get(layerName)?.visible ?? true;
  }
  
  /**
   * Get the opacity of a layer
   */
  public getOpacity(layerName: string): number {
    return this.layers.get(layerName)?.opacity ?? 1.0;
  }
  
  /**
   * Check if a layer is locked
   */
  public isLocked(layerName: string): boolean {
    return this.layers.get(layerName)?.locked ?? false;
  }
  
  /**
   * Get all managed layers
   */
  public getAllLayers(): LayerState[] {
    return Array.from(this.layers.values());
  }
}
