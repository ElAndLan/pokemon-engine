/**
 * Manages tileset loading, configuration, and tile data
 */
export interface TilesetConfig {
  id: string;
  name: string;
  imagePath: string;
  tileWidth: number;
  tileHeight: number;
  columns: number;
  rows: number;
  tileCount: number;
  imageWidth: number;
  imageHeight: number;
}

export interface Tileset extends TilesetConfig {
  image: HTMLImageElement;
  loaded: boolean;
}

export class TilesetManager {
  private tilesets: Map<string, Tileset> = new Map();
  private activeTilesetId: string | null = null;
  private configPath: string = 'data/tilesets/tilesets.json';

  constructor() {
    this.loadConfig();
  }

  /**
   * Load tileset configuration from disk
   */
  private async loadConfig(): Promise<void> {
    try {
      const result = await (window as any).fs.readFile(this.configPath);
      if (result.success) {
        const config = JSON.parse(result.data);
        console.log('[TilesetManager] Loaded config:', config);
        
        // Load each tileset
        if (config.tilesets && Array.isArray(config.tilesets)) {
          for (const tilesetConfig of config.tilesets) {
            await this.loadTileset(tilesetConfig);
          }
          this.recalculateGids();
        }
      }

    } catch (error) {
      console.error('[TilesetManager] Error loading config:', error);
    } finally {
      if (this.onLoad) this.onLoad();
    }
  }

  public onLoad: (() => void) | null = null;

  /**
   * Save tileset configuration to disk
   */
  private async saveConfig(): Promise<void> {
    const config = {
      tilesets: Array.from(this.tilesets.values()).map(ts => ({
        id: ts.id,
        name: ts.name,
        imagePath: ts.imagePath,
        tileWidth: ts.tileWidth,
        tileHeight: ts.tileHeight,
        columns: ts.columns,
        rows: ts.rows,
        tileCount: ts.tileCount,
        imageWidth: ts.imageWidth,
        imageHeight: ts.imageHeight
      }))
    };

    try {
      const result = await (window as any).fs.writeFile(
        this.configPath,
        JSON.stringify(config, null, 2)
      );
      if (result.success) {
        console.log('[TilesetManager] Config saved');
      }
    } catch (error) {
      console.error('[TilesetManager] Error saving config:', error);
    }
  }

  /**
   * Import a new tileset from an image file
   */
  public async importTileset(
    name: string,
    imagePath: string,
    tileWidth: number,
    tileHeight: number
  ): Promise<string> {
    const id = `tileset_${Date.now()}`;
    
    // Load image to get dimensions
    const imageResult = await (window as any).fs.readImage(imagePath);
    if (!imageResult.success) {
      throw new Error('Failed to load image: ' + imageResult.error);
    }

    // Create temporary image to get dimensions
    const img = new Image();
    await new Promise<void>((resolve, reject) => {
      img.onload = () => resolve();
      img.onerror = () => reject(new Error('Failed to decode image'));
      img.src = `data:image/png;base64,${imageResult.data}`;
    });

    const columns = Math.floor(img.width / tileWidth);
    const rows = Math.floor(img.height / tileHeight);
    const tileCount = columns * rows;

    const config: TilesetConfig = {
      id,
      name,
      imagePath,
      tileWidth,
      tileHeight,
      columns,
      rows,
      tileCount,
      imageWidth: img.width,
      imageHeight: img.height
    };

    await this.loadTileset(config);
    await this.saveConfig();

    return id;
  }

  /**
   * Load a tileset from configuration
   */
  private async loadTileset(config: TilesetConfig): Promise<void> {
    const img = new Image();
    
    try {
      const imageResult = await (window as any).fs.readImage(config.imagePath);
      if (!imageResult.success) {
        console.error(`[TilesetManager] Failed to load ${config.name}:`, imageResult.error);
        return;
      }

      await new Promise<void>((resolve, reject) => {
        img.onload = () => resolve();
        img.onerror = () => reject(new Error('Failed to decode image'));
        img.src = `data:image/png;base64,${imageResult.data}`;
      });

      const tileset: Tileset = {
        ...config,
        image: img,
        loaded: true
      };

      this.tilesets.set(config.id, tileset);
      console.log(`[TilesetManager] Loaded tileset: ${config.name} (${config.tileCount} tiles)`);

      // Set as active if first tileset
      if (!this.activeTilesetId) {
        this.activeTilesetId = config.id;
      }
    } catch (error) {
      console.error(`[TilesetManager] Error loading tileset ${config.name}:`, error);
    }
  }

  /**
   * Get the active tileset
   */
  public getActiveTileset(): Tileset | null {
    if (!this.activeTilesetId) return null;
    return this.tilesets.get(this.activeTilesetId) || null;
  }

  /**
   * Set the active tileset
   */
  public setActiveTileset(id: string): void {
    if (this.tilesets.has(id)) {
      this.activeTilesetId = id;
    }
  }

  /**
   * Get all tilesets
   */
  public getAllTilesets(): Tileset[] {
    return Array.from(this.tilesets.values());
  }

  /**
   * Get tileset by ID
   */
  public getTileset(id: string): Tileset | null {
    return this.tilesets.get(id) || null;
  }

  /**
   * Delete a tileset
   */
  public async deleteTileset(id: string): Promise<void> {
    this.tilesets.delete(id);
    if (this.activeTilesetId === id) {
      this.activeTilesetId = this.tilesets.size > 0 
        ? this.tilesets.keys().next().value ?? null
        : null;
    }
    await this.saveConfig();
  }

  /**
   * Calculate firstgid for all tilesets based on load order
   */
  private recalculateGids(): void {
      let currentGid = 1;
      for (const tileset of this.tilesets.values()) {
          (tileset as any).firstgid = currentGid;
          currentGid += tileset.tileCount;
      }
      console.log('[TilesetManager] Recalculated GIDs');
  }

  /**
   * Get Global ID for a local tile index in a specific tileset
   */
  public getGlobalId(tilesetId: string, localId: number): number {
      const tileset = this.tilesets.get(tilesetId);
      if (!tileset) return 0;
      return ((tileset as any).firstgid || 1) + localId;
  }

  /**
   * Draw a tile using its Global ID (GID)
   */
  public drawGid(
    ctx: CanvasRenderingContext2D,
    gid: number,
    x: number,
    y: number,
    width?: number,
    height?: number
  ): void {
      if (gid === 0) return;

      // Find which tileset this GID belongs to
      // Iterate in reverse (or find the one with largest firstgid <= gid)
      let targetTileset: Tileset | null = null;
      let targetFirstGid = 1;

      for (const tileset of this.tilesets.values()) {
          const firstgid = (tileset as any).firstgid || 1;
          if (gid >= firstgid && gid < firstgid + tileset.tileCount) {
              targetTileset = tileset;
              targetFirstGid = firstgid;
              break;
          }
      }

      if (!targetTileset || !targetTileset.loaded) return;

      const localId = gid - targetFirstGid;
      
      // Calculate source position
      const col = localId % targetTileset.columns;
      const row = Math.floor(localId / targetTileset.columns);

      const sx = col * targetTileset.tileWidth;
      const sy = row * targetTileset.tileHeight;

      const dw = width || targetTileset.tileWidth;
      const dh = height || targetTileset.tileHeight;

      try {
        ctx.drawImage(
          targetTileset.image,
          sx, sy, targetTileset.tileWidth, targetTileset.tileHeight,
          x, y, dw, dh
        );
      } catch (error) {
        // console.error('[TilesetManager] Error drawing tile:', error);
      }
  }

  /**
   * Resolve a GID to a tileset and local tile ID
   */
  public resolveGid(gid: number): { tilesetId: string, localId: number } | null {
      if (gid === 0) return null;

      for (const tileset of this.tilesets.values()) {
          const firstgid = (tileset as any).firstgid || 1;
          if (gid >= firstgid && gid < firstgid + tileset.tileCount) {
              return {
                  tilesetId: tileset.id,
                  localId: gid - firstgid
              };
          }
      }

      return null;
  }

  // Legacy drawTile (local ID) - now delegating to drawGid if possible or warning
  public drawTile(
    ctx: CanvasRenderingContext2D,
    tileId: number,
    x: number,
    y: number,
    width?: number,
    height?: number
  ): void {
      // If called with what looks like a GID (large number), likely from Tilemap render
      // But Tilemap currently passes straight data.
      // We will phase this out or map it to active tileset for UI only.
      
      // Assume "tileId" here is LOCAL id of ACTIVE tileset (Legacy behavior for UI Palette)
      const tileset = this.getActiveTileset();
      if (!tileset || !tileset.loaded || tileId === 0) return;

      // Calculate source position in tileset
      const col = tileId % tileset.columns;
      const row = Math.floor(tileId / tileset.columns);

      const sx = col * tileset.tileWidth;
      const sy = row * tileset.tileHeight;

      const dw = width || tileset.tileWidth;
      const dh = height || tileset.tileHeight;

      try {
        ctx.drawImage(
          tileset.image,
          sx, sy, tileset.tileWidth, tileset.tileHeight,
          x, y, dw, dh
        );
      } catch (error) {
        console.error('[TilesetManager] Error drawing tile:', error);
      }
  }

  /**
   * Get tile count for active tileset
   */
  public getActiveTileCount(): number {
    const tileset = this.getActiveTileset();
    return tileset ? tileset.tileCount : 0;
  }
}
