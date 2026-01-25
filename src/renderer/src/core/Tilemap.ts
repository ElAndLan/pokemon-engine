import { Display } from './Display';
import { TiledMap, TiledTileLayer, TiledObjectLayer, TiledProperty, TiledObject } from './world/TiledTypes';
import { Camera } from './Camera';
import { TilesetManager } from '../editor/TilesetManager';
import { LayerManager } from '../editor/LayerManager';
import { TILE_SIZE } from './consts';

export class Tilemap {
  public width: number = 0;
  public height: number = 0;
  public tileWidth: number = TILE_SIZE;
  public tileHeight: number = TILE_SIZE;
  // Store all layers (tiles + objects)
  private layers: (TiledTileLayer | TiledObjectLayer)[] = [];
  private mapProperties: TiledProperty[] = [];
  public path: string = '';
  public readonly instanceId = Math.random().toString(36).slice(2, 7);
  public tilesetManager: TilesetManager | null = null;
  public layerManager: LayerManager | null = null;
  
  // Mapping of Integer ID (on Encounters layer) -> Zone Key (string)
  // e.g., { 1: "route_1_grass", 2: "route_1_water" }
  public zoneMapping: { [id: number]: string } = {};

  constructor() {
      console.log(`[Tilemap] Created Instance: ${this.instanceId}`);
  }

  public loadFromTiled(map: TiledMap): void {
    // CLAMP Dimensions to prevent crashes
    const MAX_DIM = 4096;
    let targetW = map.width;
    let targetH = map.height;

    // Safety Check: Detect "Bad Header" (e.g. 30000x300000) vs "Real Data" (30x20)
    if (map.layers.length > 0 && map.layers[0].type === 'tilelayer') {
        const l = map.layers[0] as any;
        if (l.width > 0 && l.height > 0) {
             if (targetW > l.width * 2 || targetH > l.height * 2) {
                 console.warn(`[Tilemap] Map Header Dimensions (${targetW}x${targetH}) disagree with Layer 0 (${l.width}x${l.height}). Using Layer dimensions.`);
                 targetW = l.width;
                 targetH = l.height;
             }
        }
    }

    if (targetW > MAX_DIM || targetH > MAX_DIM) {
        console.warn(`[Tilemap] Map dimensions (${targetW}x${targetH}) exceed safety limit. Clamping to ${MAX_DIM}x${MAX_DIM}.`);
        targetW = Math.min(targetW, MAX_DIM);
        targetH = Math.min(targetH, MAX_DIM);
    }

    this.width = targetW;
    this.height = targetH;
    this.tileWidth = map.tilewidth;
    this.tileHeight = map.tileheight;
    
    // Store all layers
    this.layers = map.layers;
    this.mapProperties = map.properties || [];
    
    // Load Zone Mapping from properties if exists (custom property "zoneMapping")
    const mappingProp = this.getMapProperty('zoneMapping');
    if (mappingProp) {
        try {
            this.zoneMapping = typeof mappingProp === 'string' ? JSON.parse(mappingProp) : mappingProp;
        } catch (e) {
            console.warn('[Tilemap] Failed to parse zoneMapping property', e);
            this.zoneMapping = {};
        }
    } else {
        this.zoneMapping = {};
    }

    // Force-Sync Layer Dimensions
    // This fixes cases where layer data doesn't match map metadata
    this.resize(this.width, this.height);
  }

  public getMapProperty(name: string): any {
      const prop = this.mapProperties.find(p => p.name === name);
      return prop ? prop.value : null;
  }
  
  public setTile(gridX: number, gridY: number, tileId: number, layerName: string = 'Ground'): void {
      let layer = this.layers.find(l => l.name === layerName && l.type === 'tilelayer') as TiledTileLayer;
      
      if (!layer) {
          // Create Layer if not exists
          console.log(`[Tilemap] Creating new layer: ${layerName}`);
          layer = {
              id: Date.now(),
              name: layerName,
              type: 'tilelayer',
              visible: true,
              opacity: (layerName === 'Collision' || layerName === 'Encounters') ? 0.5 : 1,
              x: 0,
              y: 0,
              width: this.width,
              height: this.height,
              data: new Array(this.width * this.height).fill(0)
          };
          this.layers.push(layer);
      }

      if (gridX >= 0 && gridX < layer.width && gridY >= 0 && gridY < layer.height) {
          layer.data[gridY * layer.width + gridX] = tileId;
      } else {
          console.warn(`[Tilemap] setTile OUT OF BOUNDS: ${gridX},${gridY} vs ${layer.width}x${layer.height}`);
      }
  }

  public getTile(gridX: number, gridY: number, layerName: string = 'Ground'): number {
      const layer = this.layers.find(l => l.name === layerName && l.type === 'tilelayer') as TiledTileLayer;
      if (!layer) return 0;
      
      if (gridX >= 0 && gridX < layer.width && gridY >= 0 && gridY < layer.height) {
          return layer.data[gridY * layer.width + gridX];
      }
      return 0;
  }
  
  public resize(newWidth: number, newHeight: number): void {
      console.log(`[Tilemap] Resizing map to ${newWidth}x${newHeight}`);
      
      // Update dimensions
      this.width = newWidth;
      this.height = newHeight;
      
      // Resize Layers
      for (const layer of this.layers) {
          if (layer.type === 'tilelayer') {
              console.log(`[Tilemap] Resizing layer '${layer.name}' from ${layer.width}x${layer.height} to ${newWidth}x${newHeight}`);
              const tileLayer = layer as TiledTileLayer;
              const oldData = tileLayer.data;
              const oldWidth = tileLayer.width;
              const oldHeight = tileLayer.height;
              
              // Create new data array
              const newData = new Array(newWidth * newHeight).fill(0);
              
              // Copy existing data
              for (let y = 0; y < Math.min(oldHeight, newHeight); y++) {
                  for (let x = 0; x < Math.min(oldWidth, newWidth); x++) {
                      newData[y * newWidth + x] = oldData[y * oldWidth + x];
                  }
              }
              
              tileLayer.data = newData;
              tileLayer.width = newWidth;
              tileLayer.height = newHeight;
          }
      }
      
      // No need to resize Object Dimensions, but boundaries change for them
      
      
      // Update Internal Map Properties if they exist
      if (this.mapProperties) {
        let widthProp = this.mapProperties.find(p => p.name === 'width');
        if (widthProp) widthProp.value = newWidth;
        else this.mapProperties.push({ name: 'width', type: 'int', value: newWidth });

        let heightProp = this.mapProperties.find(p => p.name === 'height');
        if (heightProp) heightProp.value = newHeight;
        else this.mapProperties.push({ name: 'height', type: 'int', value: newHeight });
      }
      
      console.log(`[Tilemap] Resize Complete. New Size: ${this.width}x${this.height}`);
  }
  
  public getObjectAt(layerName: string, gridX: number, gridY: number): TiledObject | null {
       const layer = this.layers.find(l => (layerName === '*' ? l.type === 'objectgroup' : l.name === layerName));
       if (layer && layer.type === 'objectgroup') {
           const objectLayer = layer as TiledObjectLayer;
           const x = gridX * TILE_SIZE;
           const y = gridY * TILE_SIZE;
           
           return objectLayer.objects.find(o => 
               Math.abs(o.x - x) < 0.1 && Math.abs(o.y - y) < 0.1
           ) || null;
       }
       return null;
  }

  public getObjectById(id: number): TiledObject | null {
      for (const layer of this.layers) {
          if (layer.type === 'objectgroup') {
              const obj = (layer as TiledObjectLayer).objects.find(o => o.id === id);
              if (obj) return obj;
          }
      }
      return null;
  }

  public removeObjectAt(layerName: string | null, gridX: number, gridY: number): boolean {
      const log = (msg: string) => { console.log(msg); if ((window as any).fs && (window as any).fs.log) (window as any).fs.log(msg); };
      
      const pixelX = gridX * this.tileWidth;
      const pixelY = gridY * this.tileHeight;
      const checkX = pixelX + (this.tileWidth / 2);
      const checkY = pixelY + (this.tileHeight / 2);

      let totalRemoved = 0;

      for (const layer of this.layers) {
          // If layerName is null or '*', check ALL object layers. Otherwise check specific name.
          const matchesLayer = (layerName === null || layerName === '*') || layer.name === layerName;

          if (matchesLayer && layer.type === 'objectgroup') {
              const objLayer = layer as TiledObjectLayer;
              const initialCount = objLayer.objects.length;
              
              log(`[Tilemap] [${this.instanceId}] Deleting at ${gridX},${gridY} (Pixel ${checkX},${checkY}). Layer '${layer.name}' has ${initialCount} objects.`);

              // NEW LOGIC: Iterate backwards and remove all matches by index to prevent skip issues
              let removedInLayer = 0;
              for (let i = objLayer.objects.length - 1; i >= 0; i--) {
                  const obj = objLayer.objects[i];
                  const isAtLocation = 
                      checkX >= obj.x && checkX < obj.x + obj.width &&
                      checkY >= obj.y && checkY < obj.y + obj.height;
                  
                  if (!isAtLocation && i < 10) { // Log first few failures to avoid spam
                      log(`[Tilemap] Skip Obj ${obj.id} (${obj.type}): Pos ${obj.x},${obj.y} Size ${obj.width}x${obj.height} vs Check ${checkX},${checkY}`);
                  }
                      
                  if (isAtLocation) {
                      objLayer.objects.splice(i, 1);
                      removedInLayer++;
                  }
              }

              if (removedInLayer > 0) {
                  totalRemoved += removedInLayer;
                  log(`[Tilemap] Removed ${removedInLayer} objects from layer '${layer.name}'`);
              }
          }
      }

      return totalRemoved > 0;
  }

  public clearAllObjectsOfType(typeName: string): number {
      const log = (msg: string) => { console.log(msg); if ((window as any).fs && (window as any).fs.log) (window as any).fs.log(msg); };
      let totalRemoved = 0;
      
      for (const layer of this.layers) {
          if (layer.type === 'objectgroup') {
              const objLayer = layer as TiledObjectLayer;
              const initialCount = objLayer.objects.length;
              
              objLayer.objects = objLayer.objects.filter(obj => obj.type !== typeName);
              
              const removed = initialCount - objLayer.objects.length;
              if (removed > 0) {
                   log(`[Tilemap] Nuclear Purge: Removed ${removed} '${typeName}' objects from layer '${layer.name}'`);
                   totalRemoved += removed;
              }
          }
      }
      return totalRemoved;
  }
  public getPixelWidth(): number {
      return this.width * this.tileWidth;
  }
  
  public getPixelHeight(): number {
      return this.height * this.tileHeight;
  }

  public addObject(layerName: string, obj: any): void {
      let layer = this.layers.find(l => l.name === layerName && l.type === 'objectgroup') as TiledObjectLayer;
      if (!layer) {
           layer = {
              id: Date.now(),
              name: layerName,
              type: 'objectgroup',
              visible: true,
              opacity: 1,
              x: 0,
              y: 0,
              objects: []
          };
          this.layers.push(layer);
      }
      
      const maxId = layer.objects.reduce((max, o) => o.id > max ? o.id : max, 0);
      obj.id = maxId + 1;
      layer.objects.push(obj);
  }

  public isWalkable(gridX: number, gridY: number): boolean {
    // Check Bounds
    if (gridX < 0 || gridX >= this.width || gridY < 0 || gridY >= this.height) {
        return false;
    }

    // 1. Check Collision Layer
    // If Collision Layer exists, any non-zero tile there blocks movement.
    const collisionLayer = this.layers.find(l => l.name === 'Collision') as TiledTileLayer;
    if (collisionLayer) {
        const tileId = collisionLayer.data[gridY * collisionLayer.width + gridX];
        if (tileId !== 0) return false; // Blocked
    }

    return true;
  }





  public getEncounterZoneAt(pixelX: number, pixelY: number): string | null {
      // 1. Check Tile Layer (New System)
      const gridX = Math.floor(pixelX / this.tileWidth);
      const gridY = Math.floor(pixelY / this.tileHeight);
      
      const layer = this.layers.find(l => l.name === 'Encounters' && l.type === 'tilelayer') as TiledTileLayer;
      if (layer) {
          if (gridX >= 0 && gridX < layer.width && gridY >= 0 && gridY < layer.height) {
              const tileId = layer.data[gridY * layer.width + gridX];
              if (tileId !== 0 && this.zoneMapping[tileId]) {
                  return this.zoneMapping[tileId];
              }
          }
      }

      // 2. Check Objects (Legacy System)
      for (const layer of this.layers) {
          if (layer.type === 'objectgroup' && layer.visible) {
              const objLayer = layer as TiledObjectLayer;
              for (const obj of objLayer.objects) {
                  if (obj.type === 'EncounterZone') {
                      if (pixelX >= obj.x && pixelX < obj.x + obj.width &&
                          pixelY >= obj.y && pixelY < obj.y + obj.height) {
                          
                          // Check properties for 'tableId' OR use the object name as the ID (New behavior)
                          if (obj.properties) {
                              const prop = obj.properties.find(p => p.name === 'tableId');
                              if (prop) return prop.value;
                          }
                          // Fallback to Name if no property
                          if (obj.name) return obj.name;
                      }
                  }
              }
          }
      }
      return null;
  }

  public getWarpAt(pixelX: number, pixelY: number): any | null {
      for (const layer of this.layers) {
          if (layer.type === 'objectgroup' && (layer.visible === true || layer.visible === undefined)) {
              const objLayer = layer as TiledObjectLayer;
              for (const obj of objLayer.objects) {
                  if (obj.type === 'Warp') {
                      if (pixelX >= obj.x && pixelX < obj.x + obj.width &&
                          pixelY >= obj.y && pixelY < obj.y + obj.height) {
                          
                          const targetMap = obj.properties?.find(p => p.name === 'targetMap')?.value;
                          const targetX = obj.properties?.find(p => p.name === 'targetX')?.value;
                          const targetY = obj.properties?.find(p => p.name === 'targetY')?.value;

                          if (targetMap && targetX !== undefined && targetY !== undefined) {
                              return { targetMap, targetX, targetY };
                          }
                      }
                  }
              }
          }
      }
      return null;
  }

  public getTriggerAt(pixelX: number, pixelY: number): any | null {
      for (const layer of this.layers) {
          if (layer.type === 'objectgroup' && layer.visible) {
              const objLayer = layer as TiledObjectLayer;
              for (const obj of objLayer.objects) {
                  if (obj.type === 'Trigger') {
                      if (pixelX >= obj.x && pixelX < obj.x + obj.width &&
                          pixelY >= obj.y && pixelY < obj.y + obj.height) {
                          
                          const triggerId = obj.properties?.find(p => p.name === 'triggerId')?.value;
                          const repeatable = obj.properties?.find(p => p.name === 'repeatable')?.value;

                          if (triggerId) {
                              return { 
                                  triggerId, 
                                  repeatable: repeatable === true || repeatable === 'true' 
                              };
                          }
                      }
                  }
              }
          }
      }
      return null;
  }

  public serialize(): TiledMap {
      return {
          width: this.width,
          height: this.height,
          tilewidth: this.tileWidth,
          tileheight: this.tileHeight,
          orientation: "orthogonal",
          layers: this.layers,
          tilesets: [
              {
                  firstgid: 1,
                  name: "main",
                  image: "tileset.png",
                  imagewidth: 256,
                  imageheight: 256,
                  tilewidth: this.tileWidth,
                  tileheight: this.tileHeight,
                  tilecount: 64,
                  columns: 256 / this.tileWidth
              }
          ],
           properties: [
               ...this.mapProperties.filter(p => p.name !== 'zoneMapping'),
               { name: 'zoneMapping', type: 'string', value: JSON.stringify(this.zoneMapping) }
           ]
       };
   }

   public render(display: Display, camera?: Camera, zoom: number = 1): void {
     if (this.layers.length === 0) return;

     for (const layer of this.layers) {
       // Check LayerManager visibility if available
       if (this.layerManager && !this.layerManager.isVisible(layer.name)) continue;
       
       // Fallback to Tiled visibility if no LayerManager
       if (!this.layerManager && !layer.visible) continue;
       
       if (layer.type === 'tilelayer') {
           this.renderLayer(display, layer as TiledTileLayer, camera, zoom);
       }
     }
     
     // Render objects on top
     this.renderObjects(display, camera, zoom);
   }

   private renderLayer(display: Display, layer: TiledTileLayer, camera?: Camera, zoom: number = 1): void {
     const { data, width, height } = layer;
     const camX = camera ? camera.x : 0;
     const camY = camera ? camera.y : 0;
     
     // Apply Layer Opacity if needed
     const prevAlpha = display.ctx.globalAlpha;
     
     let layerOpacity = layer.opacity !== undefined ? layer.opacity : 1.0;
     if (this.layerManager) {
         layerOpacity = this.layerManager.getOpacity(layer.name);
     }
     
     display.ctx.globalAlpha = layerOpacity;

     const startX = Math.floor(camX / this.tileWidth);
     const startY = Math.floor(camY / this.tileHeight);
     
     // ADJUST CULLING FOR ZOOM
     // If zoom is 0.5 (zoomed out), we see 2x the area.
     const effectiveWidth = display.width / zoom;
     const effectiveHeight = display.height / zoom;

     // Add buffer of 1 tile to prevent clipping
     const endX = startX + Math.ceil(effectiveWidth / this.tileWidth) + 1;
     const endY = startY + Math.ceil(effectiveHeight / this.tileHeight) + 1;

     // Clamp to map bounds
     const loopStartX = Math.max(0, startX);
     const loopStartY = Math.max(0, startY);
     const loopEndX = Math.min(width, endX);
     const loopEndY = Math.min(height, endY);

     for (let y = loopStartY; y < loopEndY; y++) {
       for (let x = loopStartX; x < loopEndX; x++) {
         const tileId = data[y * width + x];
         
         if (tileId !== 0 && tileId !== undefined) { 
            const posX = (x * this.tileWidth) - camX;
            const posY = (y * this.tileHeight) - camY;

            // Use tileset if available, otherwise fallback to colored squares
            if (this.tilesetManager && layer.name !== 'Collision' && layer.name !== 'Encounters') {
                // Fix: Use drawGid directly because tileId is already a GID (Global ID)
                // Calling drawTile would incorrectly treat it as a local ID relative to the active tileset
                this.tilesetManager.drawGid(display.ctx, tileId, posX, posY, this.tileWidth, this.tileHeight);
            } else {
                // Fallback rendering for collision layer or when no tileset
                if (layer.name === 'Collision') {
                    display.ctx.fillStyle = '#ff0000';
                    display.ctx.fillRect(posX, posY, this.tileWidth, this.tileHeight);
                } else if (layer.name === 'Encounters') {
                    // NEW: Color Mesh for Encounter Zones
                    // Generate color from ID
                    const hue = (tileId * 137.508) % 360; // Golden Angle hack for distinct colors
                    display.ctx.fillStyle = `hsla(${hue}, 70%, 50%, 0.4)`;
                    display.ctx.fillRect(posX, posY, this.tileWidth, this.tileHeight);
                } else {
                    display.ctx.fillStyle = tileId === 1 ? '#70c070' : '#888888';
                    display.ctx.fillRect(posX, posY, this.tileWidth, this.tileHeight);
                }
            }
         }
       }
     }
    // Restore Alpha
    display.ctx.globalAlpha = prevAlpha;
  }

  // Debug: Render objects
  public renderObjects(display: Display, camera?: Camera, zoom: number = 1): void {
      const camX = camera ? camera.x : 0;
      const camY = camera ? camera.y : 0;
      const ctx = display.ctx;
      
      const effectiveW = display.width / zoom;
      const effectiveH = display.height / zoom;

      for (const layer of this.layers) {
          if (layer.type === 'objectgroup') {
              // Check LayerManager visibility if available
              if (this.layerManager && !this.layerManager.isVisible(layer.name)) continue;
              
              // Fallback to Tiled visibility
              if (!this.layerManager && !layer.visible) continue;

             const objectLayer = layer as TiledObjectLayer;
             for (const obj of objectLayer.objects) {
                 const x = obj.x - camX;
                 const y = obj.y - camY;
                 
                 // Skip if off screen (Zoom Adjusted)
                 if (x < -obj.width || y < -obj.height || x > effectiveW || y > effectiveH) continue;

                 ctx.save();
                 if (obj.type === 'Spawn') {
                     ctx.fillStyle = 'rgba(255, 255, 0, 0.5)'; // Yellow
                     ctx.fillRect(x, y, obj.width, obj.height);
                     this.drawLabel(ctx, 'Spawn', x, y);
                 } else if (obj.type === 'EncounterZone') {
                     ctx.fillStyle = 'rgba(0, 0, 255, 0.3)'; // Blue
                     ctx.fillRect(x, y, obj.width, obj.height);
                     
                     // Smart Labeling: Only draw if Left and Top neighbors don't exist
                     const hasLeft = objectLayer.objects.some(o => 
                         o.type === 'EncounterZone' && o.name === obj.name &&
                         Math.abs(o.x - (obj.x - obj.width)) < 1 && Math.abs(o.y - obj.y) < 1
                     );
                     const hasTop = objectLayer.objects.some(o => 
                         o.type === 'EncounterZone' && o.name === obj.name &&
                         Math.abs(o.x - obj.x) < 1 && Math.abs(o.y - (obj.y - obj.height)) < 1
                     );
                     
                     if (!hasLeft && !hasTop) {
                         this.drawLabel(ctx, obj.name || 'Zone', x, y);
                     }
                 } else if (obj.type === 'Warp') {
                     ctx.fillStyle = 'rgba(128, 0, 128, 0.5)'; // Purple
                     ctx.fillRect(x, y, obj.width, obj.height);
                     this.drawLabel(ctx, 'Warp', x, y);
                 } else if (obj.type === 'Item') {
                     ctx.fillStyle = 'rgba(0, 255, 255, 0.6)'; // Cyan
                     ctx.beginPath();
                     ctx.arc(x + obj.width/2, y + obj.height/2, 8, 0, Math.PI * 2);
                     ctx.fill();
                     this.drawLabel(ctx, 'Item', x, y);
                 } else if (obj.type === 'NPC') {
                    ctx.strokeStyle = 'rgba(255, 100, 100, 1.0)'; // Red Border
                    ctx.lineWidth = 2;
                    ctx.strokeRect(x, y, obj.width, obj.height);
                    this.drawLabel(ctx, 'NPC', x, y);
                 } else if (obj.type === 'Trigger') {
                     ctx.fillStyle = 'rgba(255, 0, 0, 0.3)'; // Red semi-transparent
                     ctx.fillRect(x, y, obj.width, obj.height);
                     ctx.strokeStyle = 'rgba(255, 0, 0, 0.8)'; // Red border
                     ctx.lineWidth = 2;
                     ctx.strokeRect(x, y, obj.width, obj.height);
                     this.drawLabel(ctx, 'Trigger', x, y);
                  }
                 ctx.restore();
             }
          }
      }
  }
  private drawLabel(ctx: CanvasRenderingContext2D, text: string, x: number, y: number): void {
      ctx.fillStyle = 'white';
      ctx.font = '10px sans-serif';
      ctx.strokeStyle = 'black';
      ctx.lineWidth = 2;
      ctx.strokeText(text, x, y - 2);
      ctx.fillText(text, x, y - 2);
  }

  /**
   * Get all trigger objects from the map
   */
  public getAllTriggers(): any[] {
      const objectLayer = this.layers.find(l => l.name === 'Objects' && l.type === 'objectgroup') as TiledObjectLayer;
      if (!objectLayer) return [];
      return objectLayer.objects.filter(obj => obj.type === 'Trigger');
  }

  /**
   * Delete a trigger by its ID
   */
  public deleteTriggerById(id: number): boolean {
      const objectLayer = this.layers.find(l => l.name === 'Objects' && l.type === 'objectgroup') as TiledObjectLayer;
      if (!objectLayer) return false;
      
      const initialLength = objectLayer.objects.length;
      objectLayer.objects = objectLayer.objects.filter(obj => obj.id !== id);
      return objectLayer.objects.length < initialLength;
  }

  /**
   * Find an object by its ID
   */
  public findObjectById(id: number): any | null {
      const objectLayer = this.layers.find(l => l.name === 'Objects' && l.type === 'objectgroup') as TiledObjectLayer;
      if (!objectLayer) return null;
      return objectLayer.objects.find(obj => obj.id === id) || null;
  }
}
