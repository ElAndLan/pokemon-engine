
import { Game } from '../core/Game';
import { Tilemap } from '../core/Tilemap';
import { TilesetManager } from './TilesetManager';
import { UndoManager } from './UndoManager';
import { LayerManager } from './LayerManager';
import { TILE_SIZE } from '../core/consts';

export class Editor {
  private container: HTMLElement;
  private sidebar: HTMLElement;
  private game: Game | null = null;
  public tilesetManager: TilesetManager;
  public undoManager: UndoManager;
  public layerManager: LayerManager;
  
  private selectedTileId: number = 1; // Legacy - will remove once 2D logic is solid
  private selectedTiles: number[][] = [[1]]; // 2D array of GIDs
  private selectionRect: { startX: number, startY: number, width: number, height: number } | null = null;
  private selectionStartX: number = -1; // For drag selection on palette
  private selectionStartY: number = -1;
  private paletteZoom: number = 2.0; // Default: 32px for 16px source (2x)
  
  private currentLayer: string = 'Ground';
  
  // Object Editor State
  private selectedObjectType: string = 'NPC';
  private selectedObjectId: number | null = null;
  private objectProps: any = {
      name: 'Warp1',
      itemId: 'potion',
      amount: 1,
      sprite: 'npc_boy',
      dialog: 'Hello!',
      targetMap: 'maps/house_map.json',
      targetX: 5,
      targetY: 5,
      facing: 'South',
      isTrainer: false,
      trainerTeam: [], // Array of { species: string, level: number }
      scale: 1.0,
      triggerType: 'interact'
  };

  private isMouseDown: boolean = false;
  private currentTab: 'map' | 'objects' | 'encounters' | 'tilesets' | 'project' = 'tilesets';
  
  // Tools
  private currentTool: 'pencil' | 'rect' | 'fill' | 'picker' = 'pencil';
  private currentProjectPath: string = 'maps';
  private projectSearchTerm: string = '';
  private projectEntries: { name: string, isDirectory: boolean }[] = [];
  private homeMapPath: string | null = null;
  private isPickingWarpTarget: boolean = false;
  private isLinkingWarp: boolean = false;
  private dragStartX: number = -1;
  private dragStartY: number = -1;

  // Hover State
  private hoverGridX: number = 0;
  private hoverGridY: number = 0;

  // Undo Batching
  private pendingTileChanges: { x: number, y: number, before: number, after: number }[] = [];
  
  // Data
  private pokedex: any = {};
  private encounterData: any = null;
  private scriptsData: any = null;

  // Camera Panning
  private isPanning: boolean = false;
  private lastPanX: number = 0;
  private lastPanY: number = 0;
  
  // Script Builder State
  private isScriptBuilderOpen: boolean = false;
  private currentEditingScriptId: string = '';
  private currentScriptActions: any[] = [];

  constructor(container: HTMLElement) {
      this.container = container;
      this.container.id = 'editor-sidebar'; 
      this.tilesetManager = new TilesetManager();
      this.undoManager = new UndoManager();
      this.layerManager = new LayerManager();
      this.tilesetManager.onLoad = () => {
          console.log('[Editor] Tilesets loaded, refreshing UI');
          this.render();
      };
      
      this.sidebar = document.createElement('div');
      this.sidebar.style.height = '100%';
      this.sidebar.style.display = 'flex';
      this.sidebar.style.flexDirection = 'column';
      this.container.appendChild(this.sidebar);
      
      this.render();
  }
  
  public attachGame(game: Game): void {
      this.game = game;
      this.game.onRender = this.renderOverlay.bind(this);
      
      // Load Pokedex
      if (Object.keys(this.pokedex).length === 0) {
          (window as any).fs.readFile('data/db/pokedex.json').then((res: any) => {
              if (res.success) {
                  try {
                      this.pokedex = JSON.parse(res.data);
                      console.log('[Editor] Pokedex Loaded:', Object.keys(this.pokedex).length, 'entries');
                      if (this.currentTab === 'encounters') this.render();
                  } catch (e) {
                      console.error('[Editor] Failed to parse Pokedex:', e);
                  }
              }
          });
      }

      // Load Encounters
      this.loadEncounters();
      
      // Load Scripts
      this.loadScripts();
      
      // Connect TilesetManager to Tilemap for rendering
      this.game.map.tilesetManager = this.tilesetManager;
      this.game.map.layerManager = this.layerManager;
      
      this.setupInput();
  }

  private loadEncounters(): void {
      (window as any).fs.readFile('data/db/encounters.json').then((res: any) => {
          if (res.success) {
              try {
                  const rawData = JSON.parse(res.data);
                  
                  // MIGRATION LOGIC (Moved from render)
                  const flatData: any = {};
                  let hasMigration = false;
                  
                  for (const [key, value] of Object.entries(rawData)) {
                      const val = value as any;
                      const types = ['grass', 'surf', 'oldRod', 'goodRod', 'superRod', 'cave', 'headbutt', 'rockSmash', 'sweetScent'];
                      const foundTypes = types.filter(t => val[t] && Array.isArray(val[t]));
                      
                      if (foundTypes.length > 0) {
                          hasMigration = true;
                          foundTypes.forEach(type => {
                              const newKey = `${key}_${type}`;
                              flatData[newKey] = {
                                  encounters: val[type].map((e: any) => ({
                                      pokemonId: e.pokemonId,
                                      levelMin: Array.isArray(e.level) ? e.level[0] : (e.levelMin || e.level),
                                      levelMax: Array.isArray(e.level) ? e.level[1] : (e.levelMax || e.level),
                                      weight: e.rate || e.weight || 10
                                  }))
                              };
                          });
                      } else if (val.encounters) {
                          flatData[key] = val;
                      } else {
                          flatData[key] = { encounters: [] };
                      }
                  }
                  
                  if (hasMigration || Object.keys(flatData).length > 0) {
                     this.encounterData = flatData;
                  } else {
                     this.encounterData = rawData;
                  }
                  
                  console.log('[Editor] Encounters Loaded & Migrated');
                  if (this.currentTab === 'encounters') this.render();

              } catch(e) {
                  console.error('[Editor] Failed to parse Encounters:', e);
                  this.encounterData = {};
              }
          } else {
               console.warn('[Editor] Failed to load encounters.json, using empty.');
               this.encounterData = {};
          }
      });
  }

  private loadScripts(): void {
      (window as any).fs.readFile('data/db/scripts.json').then((res: any) => {
          if (res.success) {
              try {
                  const rawData = JSON.parse(res.data);
                  this.scriptsData = rawData;
                  console.log('[Editor] Scripts Loaded:', Object.keys(rawData).filter(k => k !== '_folders').length, 'scripts');
                  if (this.currentTab === 'scripts') this.render();
              } catch(e) {
                  console.error('[Editor] Failed to parse scripts.json:', e);
                  this.scriptsData = {};
              }
          } else {
              console.warn('[Editor] Failed to load scripts.json, using empty.');
              this.scriptsData = {};
          }
      });
  }

  private async saveScripts(): Promise<void> {
      if (!this.scriptsData) return;
      
      const path = 'data/db/scripts.json';
      const content = JSON.stringify(this.scriptsData, null, 2);
      const result = await (window as any).fs.writeFile(path, content);
      
      if (result.success) {
          console.log('[Editor] Scripts saved successfully');
      } else {
          console.error('[Editor] Failed to save scripts:', result.error);
          alert('Failed to save scripts: ' + result.error);
      }
  }

  private render(): void {
      this.sidebar.innerHTML = `
        <div style="padding: 10px; background: #333; color: white;">
            <div style="display:flex; justify-content: space-between; align-items: center; margin-bottom: 10px;">
                <h3 style="margin:0;">World Editor</h3>
                <div style="display:flex; gap:5px;">
                    <button id="editor-resize-btn" style="padding: 5px 10px; background: #2196F3; border: none; color: white; cursor: pointer; border-radius: 4px;" title="Resize Map">Resize Map</button>
                    <button id="editor-save-btn" style="padding: 5px 10px; background: #4CAF50; border: none; color: white; cursor: pointer; border-radius: 4px;">Save Map</button>
                    <button id="undo-btn" style="padding: 5px 10px; background: ${this.undoManager.canUndo() ? '#FF9800' : '#555'}; border: none; color: white; cursor: ${this.undoManager.canUndo() ? 'pointer' : 'not-allowed'}; border-radius: 4px;" ${!this.undoManager.canUndo() ? 'disabled' : ''} title="Undo (Ctrl+Z)">↶ Undo</button>
                    <button id="redo-btn" style="padding: 5px 10px; background: ${this.undoManager.canRedo() ? '#FF9800' : '#555'}; border: none; color: white; cursor: ${this.undoManager.canRedo() ? 'pointer' : 'not-allowed'}; border-radius: 4px;" ${!this.undoManager.canRedo() ? 'disabled' : ''} title="Redo (Ctrl+Y)">↷ Redo</button>
                </div>
            </div>
            
            <div style="display:flex; flex-wrap: wrap; gap: 5px; margin-bottom: 10px;">
                <button class="tab-btn" data-tab="project" style="${this.getTabStyle('project')}">Project</button>
                <button class="tab-btn" data-tab="tilesets" style="${this.getTabStyle('tilesets')}">Tilesets</button>
                <button class="tab-btn" data-tab="map" style="${this.getTabStyle('map')}">Map</button>
                <button class="tab-btn" data-tab="objects" style="${this.getTabStyle('objects')}">Objects</button>
                <button class="tab-btn" data-tab="scripts" style="${this.getTabStyle('scripts')}">Scripts</button>
                <button class="tab-btn" data-tab="encounters" style="${this.getTabStyle('encounters')}">Encounters</button>
            </div>
        </div>
        
        <div id="editor-content" style="flex: 1; padding: 10px; background: #2a2a2a; color: #ddd; overflow-y: auto;">
            ${this.renderContent()}
        </div>
      `;
      
      this.bindEvents();
  }
  
  private getTabStyle(tab: string): string {
      const active = this.currentTab === tab;
      return `flex: 1; padding: 5px; background: ${active ? '#555' : '#222'}; border: 1px solid #555; color: white; cursor: pointer;`;
  }

  private getToolStyle(tool: string): string {
      const active = this.currentTool === tool;
      return `flex: 1; padding: 5px; background: ${active ? '#2196F3' : '#333'}; border: 1px solid #555; color: white; cursor: pointer;`;
  }
  
  private renderContent(): string {
      if (this.currentTab === 'project') {
          return this.renderProjectTab();
      } else if (this.currentTab === 'tilesets') {
          return this.renderTilesetsTab();
      } else if (this.currentTab === 'map') {
          return `
            <div style="display: flex; flex-direction: column; height: 100%;">
                <h4 style="margin-top:0; flex-shrink: 0;">Layers</h4>
                <select id="layer-select" style="width: 100%; padding: 5px; background: #444; color: white; border: 1px solid #555; margin-bottom: 10px;">
                    <option value="Ground" ${this.currentLayer === 'Ground' ? 'selected' : ''}>Ground</option>
                    <option value="Decoration" ${this.currentLayer === 'Decoration' ? 'selected' : ''}>Decoration / Overhead</option>
                    <option value="Collision" ${this.currentLayer === 'Collision' ? 'selected' : ''}>Collision (Metadata)</option>
                    <option value="Encounters" ${this.currentLayer === 'Encounters' ? 'selected' : ''}>Wild Encounters (Zone)</option>
                </select>
                
                <h4 style="margin:10px 0 5px 0; flex-shrink: 0;">Tools</h4>
                <div style="display:flex; gap:5px; margin-bottom:10px; flex-shrink: 0;">
                    <button class="tool-btn" data-tool="pencil" style="${this.getToolStyle('pencil')}">✏️</button>
                    <button class="tool-btn" data-tool="rect" style="${this.getToolStyle('rect')}">⬜</button>
                    <button class="tool-btn" data-tool="fill" style="${this.getToolStyle('fill')}">🪣</button>
                    <button class="tool-btn" data-tool="picker" style="${this.getToolStyle('picker')}" title="Picker (I)">🔍</button>
                </div>

                ${this.renderLayerPanel()}

            
            <div class="tool-section" style="flex-shrink: 0;">
                <h4 style="margin-top:0;">Active Tileset</h4>
                <select id="map-tileset-select" style="width: 100%; padding: 5px; background: #444; color: white; border: 1px solid #555; margin-bottom: 5px;">
                    ${this.tilesetManager.getAllTilesets().map(ts => 
                        `<option value="${ts.id}" ${this.tilesetManager.getActiveTileset()?.id === ts.id ? 'selected' : ''}>${ts.name}</option>`
                    ).join('')}
                </select>
            </div>
            
            <div class="tool-section" style="flex: 1; display: flex; flex-direction: column; min-height: 0;">
                <h4 style="margin-top:0; flex-shrink: 0;">Tiles</h4>
                <div style="flex: 1; min-height: 0; overflow: hidden;">
                    ${this.renderTilePalette()}
                </div>
                <p style="font-size: 12px; margin-top: 5px; flex-shrink: 0;">Selected ID: ${this.selectedTileId}</p>
            </div>
            
             <div style="margin-top: 10px; font-size:11px; color:#aaa;">
                <p><strong>Right-Click</strong> to Erase/Clear Tile.</p>
             </div>
            </div>
          `;
      } else if (this.currentTab === 'objects') {
          return `
            <div class="tool-section">
                <h4 style="margin-top:0;">Object Type</h4>
                <select id="obj-type-select" style="width: 100%; padding: 5px; background: #444; color: white; border: 1px solid #555; margin-bottom: 10px;">
                    <option value="Item" ${this.selectedObjectType === 'Item' ? 'selected' : ''}>Item</option>
                    <option value="NPC" ${this.selectedObjectType === 'NPC' ? 'selected' : ''}>NPC</option>
                    <option value="Warp" ${this.selectedObjectType === 'Warp' ? 'selected' : ''}>Warp / Door</option>
                    <option value="Trigger" ${this.selectedObjectType === 'Trigger' ? 'selected' : ''}>Trigger Zone (Script)</option>
                    <option value="Spawn" ${this.selectedObjectType === 'Spawn' ? 'selected' : ''}>Spawn Point</option>
                    <option value="EncounterZone" ${this.selectedObjectType === 'EncounterZone' ? 'selected' : ''}>Encounter Zone (Named)</option>
                </select>
                
                ${this.selectedObjectType === 'Warp' ? `
                <button id="create-warp-link-btn" style="width: 100%; padding: 8px; background: ${this.isLinkingWarp ? '#FF9800' : '#673AB7'}; color: white; border: none; border-radius: 4px; cursor: pointer; display:flex; align-items:center; justify-content:center; gap:5px; margin-bottom:10px;">
                    <span>${this.isLinkingWarp ? '🖱️ Click Map to Start Link' : '🔗 Create Warp Connection'}</span>
                </button>
                <div style="font-size:10px; color:#aaa; margin-bottom:10px; display:${this.isLinkingWarp ? 'block' : 'none'};">
                    Click a tile on the map to set the Entry point (and Return point). You will then pick the destination map.
                </div>
                ` : ''}
            </div>
            
            <div class="tool-section" style="margin-top: 15px;">
                <h4 style="margin-top:0;">Properties</h4>
                ${this.renderObjectForm()}
            </div>
            
            <div class="tool-section" style="margin-top: 15px;">
                <h4 style="margin-top:0;">Triggers on Map</h4>
                <div id="trigger-list" style="max-height: 200px; overflow-y: auto; background: #222; border: 1px solid #444; border-radius: 4px;">
                    ${this.renderTriggerList()}
                </div>
            </div>
            
            <div style="margin-top: 20px; font-size: 11px; color: #aaa;">
                <p><strong>Left-Click</strong> to Place Object.</p>
                <p><strong>Right-Click</strong> on an Object to Delete it.</p>
                
                <hr style="border: 0; border-top: 1px solid #444; margin: 10px 0;">
                <button id="purge-btn" style="width: 100%; padding: 5px; background: #c62828; color: white; border: none; border-radius: 4px; cursor: pointer;">
                    PURGE ALL ENCOUNTERS
                </button>
                <p style="font-size: 10px; color: #888; text-align: center; margin-top: 5px;">Warning: Deletes ALL encounter zones</p>
            </div>
          `;
      } else if (this.currentTab === 'scripts') {
          return this.renderScriptsTab();
      } else {
          // Encounters Tab
          
          let encounterData = this.encounterData;
          if (!encounterData) {
              encounterData = {};
              this.encounterData = encounterData;
          }
          
          // Ensure at least one zone exists
          const zones = Object.keys(encounterData);
          if (zones.length === 0) zones.push('default_zone_grass');
          
          if (!this.objectProps.activeEncounterZone || !zones.includes(this.objectProps.activeEncounterZone)) {
              this.objectProps.activeEncounterZone = zones[0];
          }
          const activeZone = this.objectProps.activeEncounterZone;
          
          const currentTable = (encounterData as any)[activeZone] || { encounters: [] };
          const encounters = currentTable.encounters || [];

          return `
            <div class="tool-section">
                <h4 style="margin-top:0;">Encounter Zones</h4>
                
                <div style="background:#333; padding:10px; border-radius:4px; margin-bottom:15px; border:1px solid #555;">
                    <div style="display:flex; gap:5px; margin-bottom:10px; align-items:center;">
                        <div id="zone-color-preview" style="width:24px; height:24px; border:1px solid #888; background:black;"></div>
                        <select id="encounter-zone-select" style="flex:1; padding:5px; background:#444; color:white; border:1px solid #555;">
                            ${zones.map(z => `<option value="${z}" ${z === activeZone ? 'selected' : ''}>${z}</option>`).join('')}
                        </select>
                        <button id="add-zone-btn" style="padding:5px 10px; background:#2196F3; border:none; color:white; cursor:pointer;">+</button>
                        <button id="del-zone-btn" style="padding:5px 10px; background:#c62828; border:none; color:white; cursor:pointer;">-</button>
                    </div>
                    
                    <button id="paint-zone-btn" style="width:100%; padding:8px; background:#FF9800; border:none; color:white; cursor:pointer; font-weight:bold; display:flex; align-items:center; justify-content:center; gap:5px;">
                        <span>🖌️ Paint this Zone</span>
                    </button>
                    <p style="font-size:10px; color:#aaa; margin-top:5px; text-align:center;">Click to activate Paint Tool, then draw on map.</p>
                </div>
                
                <h4 style="margin:10px 0 5px 0;">Tools</h4>
                <div style="display:flex; gap:5px; margin-bottom:10px;">
                    <button class="tool-btn" data-tool="pencil" style="${this.getToolStyle('pencil')}">✏️</button>
                    <button class="tool-btn" data-tool="rect" style="${this.getToolStyle('rect')}">⬜</button>
                    <button class="tool-btn" data-tool="fill" style="${this.getToolStyle('fill')}">🪣</button>
                </div>

                <div style="max-height: 400px; overflow-y: auto; background: #222; border: 1px solid #444; margin-bottom: 10px;">
                    <table style="width:100%; border-collapse:collapse; font-size:11px;">
                        <thead style="background:#333; color:#aaa; text-align:left;">
                            <tr>
                                <th style="padding:5px;">Pokemon</th>
                                <th style="padding:5px;">Lvl</th>
                                <th style="padding:5px;">Rate</th>
                                <th style="padding:5px;">Action</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${encounters.map((enc: any, idx: number) => {
                                const lookupId = enc.pokemonId.toString();
                                const lookupIdInt = parseInt(lookupId).toString();
                                const pkmn = this.pokedex[lookupId] || this.pokedex[lookupIdInt];
                                const name = pkmn ? pkmn.name : enc.pokemonId;
                                return `
                                <tr style="border-bottom:1px solid #333;">
                                    <td style="padding:5px;">
                                        <div style="font-weight:bold; color:#81C784;">${name} [${enc.pokemonId}]</div>
                                    </td>
                                    <td style="padding:5px;">${enc.levelMin}-${enc.levelMax}</td>
                                    <td style="padding:5px;">
                                        <div style="display:flex; align-items:center; gap:5px;">
                                            <div style="width:30px; height:4px; background:#444; border-radius:2px; overflow:hidden;">
                                                <div style="width:${Math.min(100, enc.weight * 5)}%; height:100%; background:#4CAF50;"></div>
                                            </div>
                                            <span>${enc.weight}</span>
                                        </div>
                                    </td>
                                    <td style="padding:5px;">
                                        <button class="del-encounter-btn" data-idx="${idx}" style="color:#ff5252; background:none; border:none; cursor:pointer;">x</button>
                                    </td>
                                </tr>
                                `;
                            }).join('')}
                            ${encounters.length === 0 ? '<tr><td colspan="4" style="padding:20px; text-align:center; color:#666;">No encounters defined.</td></tr>' : ''}
                        </tbody>
                    </table>
                </div>

                <div style="background:#333; padding:10px; border-radius:4px;">
                    <h5 style="margin:0 0 10px 0; color:#eee; font-size:11px;">Add Pokemon</h5>
                    <div style="display:flex; gap:10px; margin-bottom:10px;">
                         <div style="flex:2">
                            <label style="display:block; font-size:10px; color:#aaa; margin-bottom:2px;">Species</label>
                            <select id="new-enc-id" style="width:100%; box-sizing:border-box; background:#222; color:white; border:1px solid #555; padding:4px;">
                                ${Object.values(this.pokedex).map((p: any) => `<option value="${p.id}">${p.name}</option>`).join('')}
                            </select>
                         </div>
                         <div style="flex:1">
                            <label style="display:block; font-size:10px; color:#aaa; margin-bottom:2px;">Rate</label>
                            <input type="number" id="new-enc-weight" value="10" style="width:100%; box-sizing:border-box; background:#222; color:white; border:1px solid #555; padding:4px;">
                         </div>
                    </div>
                    <div style="display:flex; gap:10px; margin-bottom:10px;">
                         <div style="flex:1">
                            <label style="display:block; font-size:10px; color:#aaa; margin-bottom:2px;">Min Lvl</label>
                            <input type="number" id="new-enc-min" value="2" style="width:100%; box-sizing:border-box; background:#222; color:white; border:1px solid #555; padding:4px;">
                         </div>
                         <div style="flex:1">
                            <label style="display:block; font-size:10px; color:#aaa; margin-bottom:2px;">Max Lvl</label>
                            <input type="number" id="new-enc-max" value="5" style="width:100%; box-sizing:border-box; background:#222; color:white; border:1px solid #555; padding:4px;">
                         </div>
                    </div>
                    <button id="add-encounter-btn" style="width:100%; background:#4CAF50; color:white; border:none; padding:6px; cursor:pointer;">Add to Zone</button>
                </div>
                
                <hr style="border:0; border-top:1px solid #444; margin:15px 0;">
                
                <!-- Hidden JSON for storage linkage -->
                <textarea id="encounter-json" style="display:none;">${this.getEncounterJson()}</textarea>
                <button id="save-encounters-btn" style="width:100%; padding:8px; background:#2196F3; border:none; color:white; cursor:pointer;">Save Changes to File</button>
            </div>
          `;
      }
  }

  private renderLayerPanel(): string {
      const layers = this.layerManager.getAllLayers();
      
      return `
        <div style="margin-top: 10px; padding: 10px; background: #333; border-radius: 4px; border: 1px solid #444;">
          <h4 style="margin-top: 0; margin-bottom: 10px; font-size: 14px;">Layer Visibility</h4>
          ${layers.map(layer => `
            <div style="display: flex; align-items: center; margin-bottom: 6px; font-size: 12px; color: ${this.currentLayer === layer.name ? '#fff' : '#ccc'};">
              <input type="checkbox" 
                     class="layer-visibility-checkbox" 
                     data-layer="${layer.name}" 
                     ${layer.visible ? 'checked' : ''} 
                     style="margin-right: 8px; cursor: pointer;">
              <span style="flex: 1; cursor: pointer;">${layer.name}</span>
              <span style="font-size: 10px; opacity: 0.5;">${Math.round(layer.opacity * 100)}%</span>
            </div>
          `).join('')}
        </div>
      `;
  }

  private getEncounterJson(): string {
      return JSON.stringify(this.encounterData || {}, null, 4);
  }

  private saveEncounters(): void {
      if (!this.encounterData) return;
      
      try {
          const json = this.encounterData;
          // Convert to flat structure for saving (already flat in memory)
          // The Editor UI works with "Active Zone" -> Encounters List
          // We need to make sure we save it exactly as that key.
          
          // Re-read current DB to preserve other zones not currently active in Editor?
          // ACTUALLY: The Editor loads the WHOLE JSON at start.
          // So 'json' variable here SHOULD be the whole DB.
          
          // However, the Editor might be using the old nested structure internally if it just parsed the file.
          // We need to ensure we are saving what we see.
          
          const flatData = {};
          
          // The text area contains the JSON we are editing. 
          // If the user used the UI, 'this.getEncounterJson()' generated the JSON string for the textarea.
          // Let's verify how getEncounterJson constructs the string.
          
           console.log('Saving Encounters:', json);
          
          // Write to disk
          const path = 'data/db/encounters.json'; // Global Encounter DB
          (window as any).fs.writeFile(path, JSON.stringify(json, null, 2)).then((res: any) => {
              // Also save map tiles to persist the zone layers
              this.saveMap().then(() => {
                  if (res.success) alert('Encounters & Map Saved!');
                  else alert('Error: ' + res.error);
              });
          });
      } catch (e) {
          alert('Invalid JSON!');
      }
  }

  private renderProjectTab(): string {
      const activeMap = (this.game as any).currentLevelPath || 'None';
      
      // Filter entries based on search term
      const filteredEntries = this.projectEntries.filter(e => 
          e.name.toLowerCase().includes(this.projectSearchTerm.toLowerCase())
      );

      // Breadcrumb logic
      const pathParts = this.currentProjectPath.split('/').filter(p => p !== 'maps' && p !== '');
      const breadcrumbs = `
        <div class="breadcrumb" style="display:flex; gap:5px; align-items:center; margin-bottom:10px; font-size:11px; color:#aaa; overflow-x:auto; white-space:nowrap;">
            <span class="path-segment" data-path="maps" style="cursor:pointer; text-decoration:underline;">maps</span>
            ${pathParts.map((part, i) => {
                const subPath = 'maps/' + pathParts.slice(0, i + 1).join('/');
                return `<span>/</span><span class="path-segment" data-path="${subPath}" style="cursor:pointer; text-decoration:underline;">${part}</span>`;
            }).join('')}
        </div>
      `;

      return `
        <div class="tool-section" style="display:flex; flex-direction:column; height:100%; gap:0;">
            <h4 style="margin-top:0; margin-bottom:10px;">Project Explorer</h4>
            
            <div style="display:flex; gap:5px; margin-bottom:10px;">
                <button id="new-map-btn" title="Create New Map" style="flex:1; padding:8px; background:#4CAF50; border:none; color:white; cursor:pointer; border-radius:4px; font-size:12px;">
                    + 📄 Map
                </button>
                <button id="new-folder-btn" title="Create New Folder" style="padding:8px; background:#2196F3; border:none; color:white; cursor:pointer; border-radius:4px; font-size:12px;">
                    + 📁 Folder
                </button>
            </div>

            <input type="text" id="project-search" placeholder="Search maps..." value="${this.projectSearchTerm}" style="
                width:100%; 
                padding:8px; 
                background:#333; 
                border:1px solid #555; 
                color:white; 
                border-radius:4px; 
                margin-bottom:10px;
                box-sizing:border-box;
                font-size:12px;
            ">

            ${breadcrumbs}
            
            <div id="project-map-list" style="flex:1; display:flex; flex-direction:column; gap:4px; overflow-y:auto; padding-right:5px; min-height:0;">
                ${this.currentProjectPath !== 'maps' ? `
                    <div class="folder-up-btn" style="padding:6px 8px; background:#2a2a2a; border:1px dashed #444; color:#888; border-radius:4px; cursor:pointer; font-size:12px;">
                        ⬅️ .. (Parent Directory)
                    </div>
                ` : ''}

                ${filteredEntries.length === 0 ? '<p style="font-size:12px; color:#666; text-align:center; margin-top:20px;">No entries found.</p>' : 
                  filteredEntries.map(entry => {
                    const fullPath = `${this.currentProjectPath}/${entry.name}`;
                    const isMap = entry.name.endsWith('.json');
                    const activeMap = this.game?.currentLevelPath || '';
                    const isCurrent = activeMap.replace(/\\/g, '/') === fullPath.replace(/\\/g, '/');
                    const isHome = this.homeMapPath && this.homeMapPath.replace(/\\/g, '/') === fullPath.replace(/\\/g, '/');
                    
                    return `
                        <div class="project-item ${entry.isDirectory ? 'dir-item' : 'file-item'}" 
                             data-name="${entry.name}" 
                             data-path="${fullPath}" 
                             style="
                                padding:6px 8px; 
                                background:${isCurrent ? '#444' : '#333'}; 
                                border:1px solid ${isCurrent ? '#2196F3' : '#555'}; 
                                border-radius:4px; 
                                cursor:pointer;
                                color:${isCurrent ? '#2196F3' : 'white'};
                                font-size:12px;
                                display:flex;
                                justify-content:space-between;
                                align-items:center;
                                transition: background 0.2s;
                        ">
                            <div style="display:flex; align-items:center; gap:8px; overflow:hidden;">
                                <span style="font-size:14px;">${entry.isDirectory ? '📁' : '📄'}</span>
                                <span style="white-space:nowrap; text-overflow:ellipsis; overflow:hidden;">${entry.name}</span>
                                ${isHome ? '<span title="Home Map" style="font-size:10px; opacity:0.8;">🏠</span>' : ''}
                            </div>
                            <div style="display:flex; gap:4px; flex-shrink:0;">
                                ${isMap ? `
                                    <button class="set-home-btn" data-path="${fullPath}" title="Set as Home Map" style="padding:2px 4px; background:none; border:none; cursor:pointer; opacity:0.6; filter:${isHome ? 'grayscale(0)' : 'grayscale(1)'};">🏠</button>
                                ` : ''}
                                <button class="rename-item-btn" data-path="${fullPath}" title="Rename" style="padding:2px 4px; background:none; border:none; cursor:pointer; opacity:0.6;">✏️</button>
                                <button class="delete-item-btn" data-path="${fullPath}" title="Delete" style="padding:2px 4px; background:none; border:none; cursor:pointer; opacity:0.6;">🗑️</button>
                            </div>
                        </div>
                    `;
                }).join('')}
            </div>
            
            <div style="padding-top:10px; border-top:1px solid #444; margin-top:10px; display:flex; gap:5px;">
                <button id="refresh-maps-btn" style="flex:1; padding:5px; background:#333; border:1px solid #555; color:#aaa; cursor:pointer; font-size:11px; border-radius:3px;">
                    Refresh
                </button>
            </div>
        </div>
      `;
  }
  
  private showNewMapDialog(): void {
      const modal = document.createElement('div');
      modal.style.cssText = `
          position: fixed;
          top: 0;
          left: 0;
          width: 100%;
          height: 100%;
          background: rgba(0,0,0,0.8);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 10000;
      `;
      
      modal.innerHTML = `
          <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:350px; color:white; font-family: sans-serif;">
              <h3 style="margin-top:0; border-bottom:1px solid #444; padding-bottom:10px;">Create New Map</h3>
              
              <div style="margin-bottom:15px;">
                  <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Map Name (e.g. house_interior)</label>
                  <input type="text" class="new-map-name-input" placeholder="map_name" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
              </div>
              
              <div style="display:flex; gap:10px; margin-bottom:20px;">
                  <div style="flex:1;">
                      <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Width (tiles)</label>
                      <input type="number" class="new-map-width-input" value="20" min="1" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
                  </div>
                  <div style="flex:1;">
                      <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Height (tiles)</label>
                      <input type="number" class="new-map-height-input" value="15" min="1" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
                  </div>
              </div>
              
              <div style="display:flex; gap:10px; justify-content:flex-end; padding-top:10px; border-top:1px solid #444;">
                  <button class="new-map-cancel-btn" style="padding:8px 16px; background:#555; border:none; color:white; cursor:pointer; border-radius:4px;">Cancel</button>
                  <button class="new-map-ok-btn" style="padding:8px 16px; background:#4CAF50; border:none; color:white; cursor:pointer; border-radius:4px; font-weight:bold;">Create Map</button>
              </div>
          </div>
      `;
      
      document.body.appendChild(modal);
      
      const nameInput = modal.querySelector('.new-map-name-input') as HTMLInputElement;
      nameInput?.focus();
      
      modal.querySelector('.new-map-cancel-btn')?.addEventListener('click', () => {
          document.body.removeChild(modal);
      });
      
      modal.querySelector('.new-map-ok-btn')?.addEventListener('click', async () => {
          const name = nameInput.value;
          const width = parseInt((modal.querySelector('.new-map-width-input') as HTMLInputElement).value);
          const height = parseInt((modal.querySelector('.new-map-height-input') as HTMLInputElement).value);
          
          console.log(`[Editor] Create Map: ${name} (${width}x${height})`);
          
          if (!name) {
              alert("Please enter a map name!");
              return;
          }
          
          if (isNaN(width) || isNaN(height) || width <= 0 || height <= 0) {
              alert("Invalid dimensions!");
              return;
          }
          
          document.body.removeChild(modal);
          await this.executeCreateNewMap(name, width, height);
      });
  }
  
  private showInputDialog(message: string, defaultValue: string, callback: (value: string | null) => void): void {
      const modal = document.createElement('div');
      modal.style.cssText = `
          position: fixed;
          top: 0;
          left: 0;
          width: 100%;
          height: 100%;
          background: rgba(0,0,0,0.8);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 10000;
      `;
      
      modal.innerHTML = `
          <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:350px; color:white; font-family: sans-serif;">
              <h3 style="margin-top:0; border-bottom:1px solid #444; padding-bottom:10px;">${message}</h3>
              
              <div style="margin-bottom:15px;">
                  <input type="text" class="input-dialog-value" value="${defaultValue}" placeholder="Enter value..." style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
              </div>
              
              <div style="display:flex; gap:10px; justify-content:flex-end;">
                  <button class="input-dialog-cancel" style="padding:8px 16px; background:#555; border:none; color:white; cursor:pointer; border-radius:4px;">Cancel</button>
                  <button class="input-dialog-ok" style="padding:8px 16px; background:#4CAF50; border:none; color:white; cursor:pointer; border-radius:4px;">OK</button>
              </div>
          </div>
      `;
      
      const input = modal.querySelector('.input-dialog-value') as HTMLInputElement;
      const okBtn = modal.querySelector('.input-dialog-ok') as HTMLButtonElement;
      const cancelBtn = modal.querySelector('.input-dialog-cancel') as HTMLButtonElement;
      
      const handleOk = () => {
          const value = input.value;
          document.body.removeChild(modal);
          callback(value);
      };
      
      const handleCancel = () => {
          document.body.removeChild(modal);
          callback(null);
      };
      
      okBtn.addEventListener('click', handleOk);
      cancelBtn.addEventListener('click', handleCancel);
      input.addEventListener('keydown', (e) => {
          if (e.key === 'Enter') handleOk();
          if (e.key === 'Escape') handleCancel();
      });
      
      document.body.appendChild(modal);
      input.focus();
      input.select();
  }

  private showScriptPickerDialog(callback: (scriptId: string) => void): void {
      if (!this.scriptsData) {
          alert('No scripts loaded!');
          return;
      }

      const modal = document.createElement('div');
      modal.style.cssText = `
          position: fixed; top: 0; left: 0; width: 100%; height: 100%;
          background: rgba(0,0,0,0.8); display: flex; align-items: center; justify-content: center; z-index: 10000;
      `;
      
      const renderScriptTree = () => {
          const scripts = this.scriptsData || {};
          const folders: { [key: string]: string[] } = { ...(scripts._folders || {}) };
          const uncategorized: string[] = [];
          
          for (const scriptId in scripts) {
              if (scriptId === '_folders') continue;
              let inFolder = false;
              for (const folderName in folders) {
                  if (folders[folderName].includes(scriptId)) {
                      inFolder = true; break;
                  }
              }
              if (!inFolder) uncategorized.push(scriptId);
          }
          
          let html = '';
          
          // Render Folders
          for (const folderName in folders) {
              const scriptIds = folders[folderName];
              if (scriptIds.length === 0) continue;
              
              html += `
                  <div style="margin-bottom: 5px;">
                      <div style="font-weight: bold; color: #81C784; padding: 4px;">📁 ${folderName}</div>
                      <div style="margin-left: 15px;">
                          ${scriptIds.map(id => `
                              <div class="script-select-item" data-id="${id}" style="padding: 4px; cursor: pointer; color: #ddd; display: flex; align-items: center; gap: 5px;">
                                  <span>📜</span> ${id}
                              </div>
                          `).join('')}
                      </div>
                  </div>
              `;
          }
          
          // Render Uncategorized
          if (uncategorized.length > 0) {
              html += `
                  <div style="margin-bottom: 5px;">
                      <div style="font-weight: bold; color: #888; padding: 4px;">📂 Uncategorized</div>
                      <div style="margin-left: 15px;">
                          ${uncategorized.map(id => `
                              <div class="script-select-item" data-id="${id}" style="padding: 4px; cursor: pointer; color: #ddd; display: flex; align-items: center; gap: 5px;">
                                  <span>📜</span> ${id}
                              </div>
                          `).join('')}
                      </div>
                  </div>
              `;
          }
          
          return html || '<div style="padding: 20px; color: #666; text-align: center;">No scripts found</div>';
      };

      modal.innerHTML = `
          <div style="background:#2a2a2a; padding:20px; border-radius:8px; width:400px; max-height: 80vh; display: flex; flex-direction: column; color:white; font-family: sans-serif;">
              <h3 style="margin-top:0; border-bottom:1px solid #444; padding-bottom:10px;">Select Script</h3>
              <div style="flex: 1; overflow-y: auto; background: #222; border: 1px solid #444; border-radius: 4px; padding: 10px; margin-bottom: 15px;">
                  ${renderScriptTree()}
              </div>
              <div style="text-align: right;">
                  <button class="picker-cancel" style="padding:8px 16px; background:#555; border:none; color:white; cursor:pointer; border-radius:4px;">Cancel</button>
              </div>
          </div>
      `;
      
      const items = modal.querySelectorAll('.script-select-item');
      items.forEach(item => {
          item.addEventListener('click', () => {
              const id = item.getAttribute('data-id');
              if (id) {
                  document.body.removeChild(modal);
                  callback(id);
              }
          });
          item.addEventListener('mouseenter', () => (item as HTMLElement).style.background = '#333');
          item.addEventListener('mouseleave', () => (item as HTMLElement).style.background = 'transparent');
      });
      
      modal.querySelector('.picker-cancel')?.addEventListener('click', () => {
          document.body.removeChild(modal);
      });
      
      document.body.appendChild(modal);
  }
  
  private showRenameDialog(oldPath: string): void {
      const fileName = oldPath.split('/').pop() || '';
      const modal = document.createElement('div');
      modal.style.cssText = `position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.8); display:flex; align-items:center; justify-content:center; z-index:10000;`;
      modal.innerHTML = `
        <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:300px; color:white; font-family:sans-serif;">
            <h4 style="margin-top:0;">Rename Item</h4>
            <div style="margin-bottom:15px; font-size:11px; color:#aaa;">Current: ${fileName}</div>
            <input type="text" class="rename-input" value="${fileName}" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; border-radius:4px; margin-bottom:15px;">
            <div style="display:flex; gap:10px; justify-content:flex-end;">
                <button class="rename-cancel" style="padding:5px 10px; background:#555; border:none; color:white; cursor:pointer; border-radius:3px;">Cancel</button>
                <button class="rename-confirm" style="padding:5px 10px; background:#4CAF50; border:none; color:white; cursor:pointer; border-radius:3px;">Rename</button>
            </div>
        </div>
      `;
      document.body.appendChild(modal);
      const input = modal.querySelector('.rename-input') as HTMLInputElement;
      input.focus();
      input.select();

      modal.querySelector('.rename-cancel')?.addEventListener('click', () => document.body.removeChild(modal));
      modal.querySelector('.rename-confirm')?.addEventListener('click', async () => {
          const newName = input.value;
          if (newName && newName !== fileName) {
              const newPath = oldPath.replace(fileName, newName);
              const res = await (window as any).fs.renameFile(oldPath, newPath);
              if (res.success) {
                  this.refreshMapList();
              } else {
                  alert("Rename failed: " + res.error);
              }
          }
          document.body.removeChild(modal);
      });
  }

  private showDeleteConfirm(path: string): void {
      const fileName = path.split('/').pop() || '';
      console.log(`[Editor] Opening Delete Confirmation for: ${path}`);
      const modal = document.createElement('div');
      modal.style.cssText = `position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.8); display:flex; align-items:center; justify-content:center; z-index:10000;`;
      modal.innerHTML = `
        <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:300px; color:white; font-family:sans-serif;">
            <h4 style="margin-top:0; color:#ff5252;">Permanently Delete?</h4>
            <p style="font-size:13px; margin-bottom:20px;">Are you sure you want to delete <strong>${fileName}</strong>?<br><br>This action cannot be undone.</p>
            <div style="display:flex; gap:10px; justify-content:flex-end;">
                <button class="delete-cancel" style="padding:5px 10px; background:#555; border:none; color:white; cursor:pointer; border-radius:3px;">Cancel</button>
                <button class="delete-confirm" style="padding:5px 10px; background:#ff5252; border:none; color:white; cursor:pointer; border-radius:3px;">Delete Forever</button>
            </div>
        </div>
      `;
      document.body.appendChild(modal);
      modal.querySelector('.delete-cancel')?.addEventListener('click', () => document.body.removeChild(modal));
      modal.querySelector('.delete-confirm')?.addEventListener('click', async () => {
          console.log(`[Editor] Triggering delete for: ${path}`);
          const res = await (window as any).fs.deleteFile(path);
          console.log(`[Editor] Delete Result:`, res);
          if (res.success) {
              this.refreshMapList();
          } else {
              alert("Delete failed: " + res.error);
          }
          document.body.removeChild(modal);
      });
  }

  private showNewFolderDialog(): void {
      const modal = document.createElement('div');
      modal.style.cssText = `position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.8); display:flex; align-items:center; justify-content:center; z-index:10000;`;
      modal.innerHTML = `
        <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:300px; color:white; font-family:sans-serif;">
            <h4 style="margin-top:0;">Create New Folder</h4>
            <input type="text" class="folder-name-input" placeholder="New Folder" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; border-radius:4px; margin-bottom:15px;">
            <div style="display:flex; gap:10px; justify-content:flex-end;">
                <button class="folder-cancel" style="padding:5px 10px; background:#555; border:none; color:white; cursor:pointer; border-radius:3px;">Cancel</button>
                <button class="folder-create" style="padding:5px 10px; background:#2196F3; border:none; color:white; cursor:pointer; border-radius:3px;">Create Folder</button>
            </div>
        </div>
      `;
      document.body.appendChild(modal);
      const input = modal.querySelector('.folder-name-input') as HTMLInputElement;
      input.focus();
      
      modal.querySelector('.folder-cancel')?.addEventListener('click', () => document.body.removeChild(modal));
      modal.querySelector('.folder-create')?.addEventListener('click', async () => {
          const name = input.value;
          if (name) {
              const fullPath = `${this.currentProjectPath}/${name}`;
              const res = await (window as any).fs.createDirectory(fullPath);
              if (res.success) {
                  this.refreshMapList();
              } else {
                  alert("Failed to create folder: " + res.error);
              }
          }
          document.body.removeChild(modal);
      });
  }

  private async executeCreateNewMap(name: string, width: number, height: number): Promise<void> {
      const fileName = name.endsWith('.json') ? name : `${name}.json`;
      const fullPath = `${this.currentProjectPath}/${fileName}`;
      
      // Basic Tiled-compatible structure
      const newMap = {
          width,
          height,
          tilewidth: TILE_SIZE,
          tileheight: TILE_SIZE,
          layers: [
              { name: 'Ground', type: 'tilelayer', x:0, y:0, width, height, visible:true, opacity:1, data: new Array(width * height).fill(0) },
              { name: 'Decoration', type: 'tilelayer', x:0, y:0, width, height, visible:true, opacity:1, data: new Array(width * height).fill(0) },
              { name: 'Collision', type: 'tilelayer', x:0, y:0, width, height, visible:true, opacity:1, data: new Array(width * height).fill(0) },
              { name: 'Encounters', type: 'tilelayer', x:0, y:0, width, height, visible:true, opacity:1, data: new Array(width * height).fill(0) },
              { name: 'Objects', type: 'objectgroup', objects: [] }
          ],
          tilesets: []
      };
      
      try {
          const res = await (window as any).fs.writeFile(fullPath, JSON.stringify(newMap, null, 2));
          if (res.success) {
              alert(`Map "${fileName}" created successfully!`);
              await this.refreshMapList();
              // Load the new map automatically
              (this.game as any).loadLevel(fullPath);
              this.render();
          } else {
              alert("Failed to create map: " + res.error);
          }
      } catch (e: any) {
          alert("Error: " + e.message);
      }
  }

  private availableMaps: string[] = [];
  private availableScripts: string[] = []; // Cache script IDs

  private async refreshMapList(): Promise<void> {
      console.log(`[Editor] Refreshing map list for: ${this.currentProjectPath}...`);
      try {
          const res = await (window as any).fs.listDir(this.currentProjectPath);
          if (res.success) {
              this.projectEntries = res.files;
              console.log('[Editor] Project entries updated:', this.projectEntries);
              this.render();
              
              // Also update global map list for Warps
              this.updateAvailableMaps();
          } else {
              console.error('[Editor] Failed to list maps:', res.error);
          }
      } catch (e) {
          console.error("[Editor] Failed to refresh map list", e);
      }
  }

  private async updateAvailableMaps(): Promise<void> {
      this.availableMaps = [];
      await this.scanMapsRecursive('maps');
      this.availableMaps.sort();
      console.log('[Editor] Available Maps for Warp:', this.availableMaps);
  }

  private async scanMapsRecursive(path: string): Promise<void> {
      try {
          const res = await (window as any).fs.listDir(path);
          if (res.success) {
              for (const file of res.files) {
                  const fullPath = path + '/' + file.name;
                  if (file.isDirectory) {
                      await this.scanMapsRecursive(fullPath);
                  } else if (file.name.endsWith('.json')) {
                      this.availableMaps.push(fullPath);
                  }
              }
          }
      } catch (e) {
          console.warn('[Editor] Error scanning maps:', e);
      }
  }

  private renderObjectForm(): string {
       // Render the property form for the selected object
       let html = '';
       
       if (this.selectedObjectId !== null) {
           html += `<div style="padding:4px; background:#444; margin-bottom:10px; border-radius:3px; display:flex; justify-content:space-between; align-items:center;">
                        <span style="font-size:11px; color:#0f0;">Editing Object #${this.selectedObjectId}</span>
                        <button id="obj-deselect-btn" style="padding:2px 6px; background:#666; color:white; border:none; border-radius:2px; font-size:10px; cursor:pointer;">Deselect</button>
                    </div>`;
       }

       if (this.selectedObjectType === 'Item') {
           html += `
            <div style="display:flex; flex-direction:column; gap:8px;">
                <label>Item ID: <input type="text" class="obj-prop" data-key="itemId" value="${this.objectProps.itemId}" style="width:100%"></label>
                <label>Amount: <input type="number" class="obj-prop" data-key="amount" value="${this.objectProps.amount}" style="width:100%"></label>
            </div>
          `;
       } else if (this.selectedObjectType === 'NPC') {
            html += `
            <div style="display:flex; flex-direction:column; gap:8px;">
                <label>Trigger Type:
                    <select class="obj-prop" data-key="triggerType" style="width:100%">
                        <option value="interact" ${this.objectProps.triggerType === 'interact' ? 'selected' : ''}>On Interact (Press Z)</option>
                        <option value="step" ${this.objectProps.triggerType === 'step' ? 'selected' : ''}>On Step (Walk Over)</option>
                        <option value="sight" ${this.objectProps.triggerType === 'sight' ? 'selected' : ''}>On Sight (Trainer)</option>
                    </select>
                </label>
                <div style="display:flex; gap:5px; align-items:flex-end; margin-bottom:5px;">
                     <label style="flex-grow:1;">Script ID:
                        <div style="display:flex; gap:3px;">
                            <input type="text" class="obj-prop" data-key="triggerId" value="${this.objectProps.triggerId || ''}" placeholder="e.g. npc_hello" style="flex-grow:1; width:auto;">
                            <button class="select-script-btn" style="padding:4px 8px; font-size:11px; cursor:pointer; background:#4CAF50; color:white; border:none; border-radius:3px; flex-shrink:0;">Select</button>
                        </div>
                     </label>
                     <button id="edit-script-btn" style="padding:4px 8px; font-size:11px; cursor:pointer; background:#2196F3; color:white; border:none; border-radius:3px;">Edit</button>
                </div>
                <label>Unique ID (Name): <input type="text" class="obj-prop" data-key="name" value="${this.objectProps.name || ''}" placeholder="e.g. rival1" style="width:100%"></label>
                <div style="display:flex; gap:5px; align-items:flex-end;">
                     <label style="flex-grow:1;">Sprite: <input type="text" class="obj-prop" data-key="sprite" value="${this.objectProps.sprite}" style="width:100%"></label>
                     <button id="npc-sprite-picker-btn" style="padding:4px; font-size:10px; cursor:pointer;">Select File</button>
                </div>
                <label>Scale: <input type="number" class="obj-prop" data-key="scale" value="${this.objectProps.scale || 1.0}" step="0.1" style="width:100%"></label>
                <!-- Sprite Preview Container -->
                <div id="npc-sprite-preview" style="width: 64px; height: 64px; border: 1px dashed #666; margin: 5px auto; display: flex; align-items: center; justify-content: center; background: #000;">
                    <span style="color:#444; font-size:10px;">Preview</span>
                </div>
                <label>Dialog: <textarea class="obj-prop" data-key="dialog" style="width:100%" rows="2">${this.objectProps.dialog}</textarea></label>
                <label>Facing: 
                    <select class="obj-prop" data-key="facing" style="width:100%">
                        <option value="South" ${this.objectProps.facing === 'South' ? 'selected' : ''}>South (Down)</option>
                        <option value="North" ${this.objectProps.facing === 'North' ? 'selected' : ''}>North (Up)</option>
                        <option value="East" ${this.objectProps.facing === 'East' ? 'selected' : ''}>East (Right)</option>
                        <option value="West" ${this.objectProps.facing === 'West' ? 'selected' : ''}>West (Left)</option>
                    </select>
                </label>
                <label style="display:flex; align-items:center; gap:5px; cursor:pointer; background:#444; padding:5px; border-radius:3px;">
                    <input type="checkbox" class="obj-prop-bool" data-key="isTrainer" ${this.objectProps.isTrainer ? 'checked' : ''}> 
                    <span>Is Trainer (Triggers Battle)</span>
                </label>

                ${this.objectProps.isTrainer ? `
                    <div style="border: 1px solid #555; padding: 8px; border-radius: 4px; background: #222;">
                        <span style="font-size:11px; color:#aaa; text-transform:uppercase;">Pokemon Team</span>
                        <div id="trainer-team-list" style="margin-top:5px; display:flex; flex-direction:column; gap:5px;">
                            ${(this.objectProps.trainerTeam || []).map((p: any, idx: number) => `
                                <div style="display:flex; gap:3px; align-items:center;">
                                    <input type="text" class="team-prop" data-idx="${idx}" data-field="species" value="${p.species}" placeholder="PID" style="width:50px; font-size:10px;">
                                    <input type="number" class="team-prop" data-idx="${idx}" data-field="level" value="${p.level}" placeholder="Lv" style="width:35px; font-size:10px;">
                                    <button class="remove-team-btn" data-idx="${idx}" style="background:#c62828; border:none; color:white; padding:2px 5px; cursor:pointer; font-size:10px;">X</button>
                                </div>
                            `).join('')}
                            <button id="add-team-btn" style="width:100%; padding:3px; background:#444; border:1px dashed #666; color:#aaa; cursor:pointer; font-size:10px; margin-top:5px;">+ Add Pokemon</button>
                        </div>
                    </div>
                ` : ''}
            </div>
          `;
       } else if (this.selectedObjectType === 'Warp') {
            html += `
            <div style="display:flex; flex-direction:column; gap:8px;">
                <label>Target Map: <div style="display:flex; gap:4px;">
                    <select class="obj-prop" data-key="targetMap" style="flex:1; background:#333; color:white; border:1px solid #555; padding:2px;">
                        <option value="">-- Select Map --</option>
                        ${this.availableMaps.map(m => `<option value="${m}" ${m === this.objectProps.targetMap ? 'selected' : ''}>${m.replace(/^maps\//, '')}</option>`).join('')}
                    </select>
                    <button id="warp-open-map-btn" title="Open Map" style="padding:2px 5px; background:#2196F3; border:none; color:white; cursor:pointer; border-radius:3px;">📂</button>
                </div></label>
                <div style="display:flex; gap:8px;">
                     <div style="flex:1;"><label>Target X: <input type="number" class="obj-prop" data-key="targetX" value="${this.objectProps.targetX}" style="width:100%"></label></div>
                     <div style="flex:1;"><label>Target Y: <input type="number" class="obj-prop" data-key="targetY" value="${this.objectProps.targetY}" style="width:100%"></label></div>
                </div>
                <button id="warp-open-visual-picker-btn" style="width:100%; padding:5px; background:#FF9800; border:1px solid #c66900; color:white; cursor:pointer; border-radius:3px; font-size:11px;">
                    🎯 Pick Destination on Map
                </button>
            </div>
          `;
       } else if (this.selectedObjectType === 'Trigger') {
            html += `
            <div style="display:flex; flex-direction:column; gap:8px;">
                <label>Unique ID (Name): <input type="text" class="obj-prop" data-key="name" value="${this.objectProps.name || ''}" placeholder="e.g. step_trigger_1" style="width:100%"></label>
                <label>Trigger Type:
                    <select class="obj-prop" data-key="triggerType" style="width:100%">
                        <option value="step" ${!this.objectProps.triggerType || this.objectProps.triggerType === 'step' ? 'selected' : ''}>On Step (Walk Over)</option>
                        <option value="interact" ${this.objectProps.triggerType === 'interact' ? 'selected' : ''}>On Interact (Press Z)</option>
                    </select>
                </label>
                <div style="display:flex; gap:5px; align-items:flex-end;">
                    <label style="flex-grow:1;">Script ID: 
                        <div style="display:flex; gap:3px;">
                            <input type="text" class="obj-prop" data-key="triggerId" value="${this.objectProps.triggerId || ''}" placeholder="e.g. rival_ambush" style="flex-grow:1; width:auto;">
                            <button class="select-script-btn" style="padding:4px 8px; font-size:11px; cursor:pointer; background:#4CAF50; color:white; border:none; border-radius:3px; flex-shrink:0;">Select</button>
                        </div>
                    </label>
                    <button id="edit-script-btn-trigger" style="padding:4px 8px; font-size:11px; cursor:pointer; background:#2196F3; color:white; border:none; border-radius:3px;">Edit</button>
                </div>
                <label style="display:flex; align-items:center; gap:5px; cursor:pointer; background:#444; padding:5px; border-radius:3px;">
                    <input type="checkbox" class="obj-prop-bool" data-key="repeatable" ${this.objectProps.repeatable ? 'checked' : ''}> 
                    <span style="font-size:12px;">Repeatable Trigger</span>
                </label>
            </div>
            `;
       } else if (this.selectedObjectType === 'Spawn') {
            html += `
            <div style="display:flex; flex-direction:column; gap:8px;">
                <label>Name: <input type="text" class="obj-prop" data-key="name" value="${this.objectProps.name}" style="width:100%"></label>
            </div>
          `;
       } else if (this.selectedObjectType === 'EncounterZone') {
            html += `
            <div style="display:flex; flex-direction:column; gap:8px;">
                <label>Zone Name (Table ID): <input type="text" class="obj-prop" data-key="name" value="${this.objectProps.name || 'route_1_grass'}" style="width:100%"></label>
            </div>
          `;
       }
       
       return html;
      return '<p style="color:#aaa">Select an object to edit properties.</p>';
  }

  private renderTriggerList(): string {
      if (!this.game || !this.game.map) {
          return '<p style="padding: 10px; color: #666; font-size: 11px; text-align: center;">No map loaded</p>';
      }
      
      const triggers = this.game.map.getAllTriggers();
      
      if (triggers.length === 0) {
          return '<p style="padding: 10px; color: #666; font-size: 11px; text-align: center;">No triggers on this map</p>';
      }
      
      return triggers.map(trigger => {
          const triggerId = trigger.properties?.find((p: any) => p.name === 'triggerId')?.value || 'unknown';
          const triggerType = trigger.properties?.find((p: any) => p.name === 'triggerType')?.value || 'step';
          const gridX = Math.floor(trigger.x / 16);
          const gridY = Math.floor(trigger.y / 16);
          
          return `
              <div class="trigger-item" style="padding: 8px; border-bottom: 1px solid #333; display: flex; justify-content: space-between; align-items: center;">
                  <div style="flex: 1;">
                      <div style="font-weight: bold; color: #ff5252; font-size: 12px;">${trigger.name || 'Trigger'}</div>
                      <div style="font-size: 10px; color: #aaa;">Script: ${triggerId} | Type: ${triggerType}</div>
                      <div style="font-size: 10px; color: #666;">Position: (${gridX}, ${gridY})</div>
                  </div>
                  <button class="delete-trigger-btn" data-id="${trigger.id}" style="padding: 4px 8px; background: #c62828; border: none; color: white; cursor: pointer; border-radius: 3px; font-size: 11px;">Delete</button>
              </div>
          `;
      }).join('');
  }

  private renderScriptsTab(): string {
      const scripts = this.scriptsData || {};
      
      // Initialize folder structure if it doesn't exist
      if (!scripts._folders) {
          scripts._folders = {};
      }
      
      // Group scripts by folder
      const folders: { [key: string]: string[] } = { ...scripts._folders };
      const uncategorized: string[] = [];
      
      // Find all scripts and categorize them
      for (const scriptId in scripts) {
          if (scriptId === '_folders') continue;
          
          // Check if script is in any folder
          let inFolder = false;
          for (const folderName in folders) {
              if (folders[folderName].includes(scriptId)) {
                  inFolder = true;
                  break;
              }
          }
          
          if (!inFolder) {
              uncategorized.push(scriptId);
          }
      }
      
      return `
          <div class="tool-section">
              <h4 style="margin-top:0;">Script Library</h4>
              
              <div style="display: flex; gap: 5px; margin-bottom: 10px;">
                  <button id="new-script-btn" style="flex: 1; padding: 8px; background: #4CAF50; border: none; color: white; cursor: pointer; border-radius: 4px;">
                      + New Script
                  </button>
                  <button id="new-folder-btn" style="flex: 1; padding: 8px; background: #2196F3; border: none; color: white; cursor: pointer; border-radius: 4px;">
                      + New Folder
                  </button>
              </div>
              
              <input type="text" id="script-search" placeholder="Search scripts..." style="width: 100%; padding: 8px; background: #333; border: 1px solid #555; color: white; border-radius: 4px; margin-bottom: 10px; box-sizing: border-box;">
              
              <div id="script-tree" style="max-height: 500px; overflow-y: auto; background: #222; border: 1px solid #444; border-radius: 4px; padding: 5px;">
                  ${Object.keys(folders).length === 0 && uncategorized.length === 0 ? 
                      '<p style="padding: 20px; text-align: center; color: #666;">No scripts yet. Click "+ New Script" to create one.</p>' :
                      this.renderScriptFolders(folders, scripts) + this.renderUncategorizedScripts(uncategorized, scripts)
                  }
              </div>
              
              <div style="margin-top: 15px; font-size: 11px; color: #aaa;">
                  <p><strong>Drag & Drop:</strong> Drag scripts to folders to organize them.</p>
                  <p><strong>Edit:</strong> Click a script to open the script builder.</p>
              </div>
          </div>
      `;
  }
  
  private renderScriptFolders(folders: { [key: string]: string[] }, scripts: any): string {
      let html = '';
      
      for (const folderName in folders) {
          const scriptIds = folders[folderName] || [];
          
          html += `
              <div class="folder-container" style="margin-bottom: 8px;">
                  <div class="folder-header" data-folder="${folderName}" style="
                      padding: 8px;
                      background: #2a2a2a;
                      border: 1px solid #444;
                      border-radius: 4px;
                      cursor: pointer;
                      display: flex;
                      justify-content: space-between;
                      align-items: center;
                      user-select: none;
                  " ondragover="event.preventDefault();" ondrop="event.preventDefault(); window.editorDropScript(event, '${folderName}');">
                      <div style="display: flex; align-items: center; gap: 8px;">
                          <span class="folder-icon">📁</span>
                          <span style="font-weight: bold; color: #81C784;">${folderName}</span>
                          <span style="font-size: 10px; color: #666;">(${scriptIds.length})</span>
                      </div>
                      <div style="display: flex; gap: 5px;">
                          <button class="delete-folder-btn" data-folder="${folderName}" style="padding: 2px 6px; background: #c62828; border: none; color: white; cursor: pointer; border-radius: 3px; font-size: 10px;">Delete Folder</button>
                      </div>
                  </div>
                  <div class="folder-contents" style="margin-left: 20px; margin-top: 5px;">
                      ${scriptIds.map(scriptId => this.renderScriptItem(scriptId, scripts[scriptId], folderName)).join('')}
                  </div>
              </div>
          `;
      }
      
      return html;
  }
  
  private renderUncategorizedScripts(uncategorized: string[], scripts: any): string {
      if (uncategorized.length === 0) return '';
      
      return `
          <div class="folder-container" style="margin-bottom: 8px;">
              <div class="folder-header" style="
                  padding: 8px;
                  background: #1a1a1a;
                  border: 1px solid #333;
                  border-radius: 4px;
                  user-select: none;
              " ondragover="event.preventDefault();" ondrop="event.preventDefault(); window.editorDropScript(event, null);">
                  <div style="display: flex; align-items: center; gap: 8px;">
                      <span>📂</span>
                      <span style="color: #888;">Uncategorized</span>
                      <span style="font-size: 10px; color: #555;">(${uncategorized.length})</span>
                  </div>
              </div>
              <div class="folder-contents" style="margin-left: 20px; margin-top: 5px;">
                  ${uncategorized.map(scriptId => this.renderScriptItem(scriptId, scripts[scriptId], null)).join('')}
              </div>
          </div>
      `;
  }
  
  private renderScriptItem(scriptId: string, scriptData: any, folderName: string | null): string {
      const actionCount = scriptData?.actions?.length || (Array.isArray(scriptData) ? scriptData.length : 0);
      
      // Truncate long names
      const displayName = scriptId.length > 25 ? scriptId.substring(0, 25) + '...' : scriptId;
      
      return `
          <div class="script-item" draggable="true" data-script-id="${scriptId}" style="
              padding: 6px 8px;
              background: #2a2a2a;
              border: 1px solid #444;
              border-radius: 3px;
              margin-bottom: 4px;
              cursor: move;
              display: flex;
              justify-content: space-between;
              align-items: center;
              transition: background 0.2s;
          " ondragstart="window.editorDragScript(event, '${scriptId}');" title="${scriptId}">
              <div style="display: flex; align-items: center; gap: 8px; flex: 1; min-width: 0;">
                  <span>📜</span>
                  <span style="font-size: 12px; color: #ddd; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;">${displayName}</span>
                  <span style="font-size: 10px; color: #666; flex-shrink: 0;">${actionCount} action${actionCount !== 1 ? 's' : ''}</span>
              </div>
              <div style="display: flex; gap: 5px; flex-shrink: 0;">
                  <button class="edit-script-btn" data-script-id="${scriptId}" style="padding: 3px 8px; background: #2196F3; border: none; color: white; cursor: pointer; border-radius: 3px; font-size: 10px;">Edit</button>
                  <button class="delete-script-btn" data-script-id="${scriptId}" style="padding: 3px 8px; background: #c62828; border: none; color: white; cursor: pointer; border-radius: 3px; font-size: 10px;">Delete</button>
              </div>
          </div>
      `;
  }

  private updateObjectProperty(obj: any, key: string, value: any): void {
    if (!obj.properties) obj.properties = [];
    const prop = obj.properties.find(p => p.name === key);
    if (prop) {
      prop.value = value;
    } else {
      const type = typeof value === 'boolean' ? 'bool' : (typeof value === 'number' ? 'int' : 'string');
      obj.properties.push({ name: key, type, value });
    }
    
    // SYNC: If updating a visual property of an NPC, refresh!
    if (obj.type === 'NPC' && (key === 'sprite' || key === 'scale')) {
        this.game?.refreshNPCs();
    }
    
    console.log(`[Editor] Updated property ${key} to ${value} on object ${obj.id}`);
  }

  private renderTilesetsTab(): string {
      const tilesets = this.tilesetManager.getAllTilesets();
      const activeTileset = this.tilesetManager.getActiveTileset();
      
      return `
        <div class="tool-section">
            <h4 style="margin-top:0;">Tileset Library</h4>
            <button id="import-tileset-btn" style="width:100%; padding:8px; background:#4CAF50; border:none; color:white; cursor:pointer; border-radius:4px; margin-bottom:10px;">
                + Import New Tileset
            </button>
            
            ${tilesets.length === 0 ? `
                <p style="color:#aaa; font-size:12px; text-align:center; padding:20px;">
                    No tilesets imported yet.<br>
                    Click "Import New Tileset" to get started!
                </p>
            ` : `
                <div style="display:flex; flex-direction:column; gap:8px;">
                    ${tilesets.map(ts => {
                        const isActive = activeTileset && activeTileset.id === ts.id;
                        const border = '2px solid #00E676'; // Bright Green
                        const bg = isActive ? '#444' : '#333';
                        return `
                        <div class="tileset-item" data-id="${ts.id}" style="
                            padding:8px; 
                            background:${bg}; 
                            border:${isActive ? border : '1px solid #555'}; 
                            border-radius:4px; 
                            cursor:pointer;
                        ">
                            <div style="display:flex; justify-content:space-between; align-items:center;">
                                <div>
                                    <div style="font-weight:bold;">${ts.name}</div>
                                    <div style="font-size:11px; color:#aaa;">
                                        ${ts.columns}x${ts.rows} (${ts.tileCount} tiles)
                                    </div>
                                </div>
                                <button class="delete-tileset-btn" data-id="${ts.id}" style="
                                    padding:4px 8px;
                                    background:#c62828;
                                    border:none;
                                    color:white;
                                    cursor:pointer;
                                    border-radius:3px;
                                    font-size:11px;
                                ">Delete</button>
                            </div>
                        </div>
                    `;}).join('')}
                </div>
            `}
        </div>
        
        ${activeTileset ? `
            <div class="tool-section" style="margin-top:15px;">
                <h4 style="margin-top:0;">Active Tileset: ${activeTileset.name}</h4>
                <p style="font-size:11px; color:#aaa;">
                    Tile Size: ${activeTileset.tileWidth}x${activeTileset.tileHeight}px<br>
                    Image: ${activeTileset.imagePath.split('/').pop()}
                </p>
            </div>
        ` : ''}
        
        <div style="margin-top:20px; font-size:11px; color:#888;">
            <p><strong>How to use:</strong></p>
            <ol style="margin:5px 0; padding-left:20px;">
                <li>Import a tileset PNG image</li>
                <li>Set tile size (16x16, 32x32, etc.)</li>
                <li>Go to "Map" tab to paint with tiles</li>
            </ol>
        </div>
        
        <!-- Hidden file input -->
        <input type="file" id="tileset-file-input" accept="image/png" style="display:none;">
      `;
  }

  private renderTilePalette(): string {
      const log = (msg: string) => { console.log(msg); if ((window as any).fs && (window as any).fs.log) (window as any).fs.log(msg); };
      const error = (msg: string) => { console.error(msg); if ((window as any).fs && (window as any).fs.log) (window as any).fs.log('ERROR: ' + msg); };

      if (this.currentLayer === 'Collision') {
          // ... (keep existing)
          return `
            <div style="display: flex; gap: 5px; flex-wrap: wrap;">
                 ${this.renderTileButton(1, 'rgba(255,0,0,0.5)', 'Blocker')}
                 ${this.renderTileButton(0, 'black', 'Eraser')}
            </div>
            <p style="font-size: 11px; color:#aaa;">Painting 'Blocker' makes the tile impassable.</p>
          `;
      } else if (this.currentLayer === 'Encounters') {
          // ... (keep existing)
          return `
             <div style="display: flex; gap: 5px; flex-wrap: wrap;">
                 ${this.renderTileButton(1, 'rgba(0,0,255,0.5)', 'Zone')}
                 ${this.renderTileButton(0, 'black', 'Eraser')}
            </div>
            <p style="font-size: 11px; color:#aaa;">Paint 'Zone' where wild Pokemon appear.</p>
          `;
      } else {
          // Use tileset if available
          const tileset = this.tilesetManager.getActiveTileset();
          log(`[Editor] Rendering Palette. Active: ${tileset ? tileset.name : 'None'}`);
          
          if (tileset && tileset.loaded) {
              const tileCount = tileset.tileCount;
              log(`[Editor] Tileset Loaded. Image: ${tileset.image.width}x${tileset.image.height}, Src Length: ${tileset.image.src.length}`);
              
              const columns = tileset.columns;
              const rows = tileset.rows;
              
              if (columns === 0 || rows === 0) {
                  error(`[Editor] Invalid dimensions! Cols: ${columns}, Rows: ${rows}`);
              }

              // Dynamic Scale: Default is 32px display (2x scale for 16px)
              const displayTileSize = 16 * this.paletteZoom;
              const displayWidth = columns * displayTileSize;
              const displayHeight = rows * displayTileSize;
              
              const src = tileset.image.src;
              // Check if src is valid
              if (!src || src.length < 100) {
                  error(`[Editor] Image src is suspiciously short or empty! ${src}`);
              }
              
              // ... existing HTML generation
              // We use an IMG tag for performance (instead of thousands of canvases)
              // We wrap it in a container to handle clicks
              let html = `
                <div style="margin-bottom: 5px; display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 5px;">
                    <div style="display: flex; gap: 2px;">
                        <button class="palette-zoom-btn" data-zoom="0.5" style="padding: 2px 5px; background: ${this.paletteZoom === 0.5 ? '#444' : '#333'}; border: 1px solid #555; color: white; cursor: pointer; font-size: 10px;">0.5x</button>
                        <button class="palette-zoom-btn" data-zoom="1" style="padding: 2px 5px; background: ${this.paletteZoom === 1 ? '#444' : '#333'}; border: 1px solid #555; color: white; cursor: pointer; font-size: 10px;">1x</button>
                        <button class="palette-zoom-btn" data-zoom="2" style="padding: 2px 5px; background: ${this.paletteZoom === 2 ? '#444' : '#333'}; border: 1px solid #555; color: white; cursor: pointer; font-size: 10px;">2x</button>
                        <button id="palette-fit-btn" style="padding: 2px 5px; background: #333; border: 1px solid #555; color: white; cursor: pointer; font-size: 10px;">Fit</button>
                    </div>
                    <button class="tile-btn" data-id="0" style="padding: 2px 8px; background: #333; border: 1px solid #555; color: white; cursor: pointer; font-size: 11px;">Eraser</button>
                </div>
                <div id="palette-scroll-container" style="max-height: 400px; overflow: auto; border: 1px solid #444; position: relative; background: #222;">
                    <div style="position: relative; width: ${displayWidth}px; height: ${displayHeight}px;">
                        <img id="active-tileset-image" src="${src}" style="width: 100%; height: 100%; image-rendering: pixelated; display: block;">
                        
                        <!-- Selection Highlight Overlay -->
                        <div id="palette-highlight" style="position: absolute; pointer-events: none; border: 2px solid white; box-sizing: border-box; display: none;"></div>
                    </div>
                </div>
                <!-- Info bar at bottom -->
                <div style="margin-top: 5px; display: flex; align-items: center; justify-content: space-between;">
                    <p style="font-size:10px; color:#aaa; margin:0;">${tileCount} tiles • ${tileset.image.width}x${tileset.image.height}px Source</p>
                    <p style="font-size:10px; color:#666; margin:0;">Click/Drag to select.</p>
                </div>
              `;
              
              return html;
          } else {
              return `
                <div style="padding:20px; text-align:center; color:#aaa; font-size:12px;">
                    <p>No tileset loaded!</p>
                    <p>Go to "Tilesets" tab to import one.</p>
                </div>
              `;
          }
      }
  }
  
  private renderTileButton(id: number, color: string, title: string): string {
      const isSelected = this.selectedTileId === id;
      const border = isSelected ? '2px solid white' : '1px solid #555';
      return `<div class="tile-btn" data-id="${id}" title="${title}" style="width:32px; height:32px; background:${color}; border:${border}; cursor:pointer;"></div>`;
  }
  
  private renderTilesetTileButton(tileId: number, title: string): string {
      const isSelected = this.selectedTileId === tileId;
      const border = isSelected ? '2px solid white' : '1px solid #555';
      const canvasId = `tile-canvas-${tileId}`;
      
      // We'll render the actual tile image via canvas after DOM is ready
      setTimeout(() => {
          const canvas = document.getElementById(canvasId) as HTMLCanvasElement;
          if (canvas) {
              const ctx = canvas.getContext('2d');
              if (ctx) {
                  if (tileId === 0) {
                      // Eraser - black square
                      ctx.fillStyle = 'black';
                      ctx.fillRect(0, 0, 32, 32);
                  } else {
                      this.tilesetManager.drawTile(ctx, tileId, 0, 0, 32, 32);
                  }
              }
          }
      }, 0);
      
      return `<canvas id="${canvasId}" class="tileset-tile-btn" data-id="${tileId}" title="${title}" width="32" height="32" style="border:${border}; cursor:pointer; image-rendering:pixelated;"></canvas>`;
  }
  
  private bindEvents(): void {
      // Tabs
      this.sidebar.querySelectorAll('.tab-btn').forEach(btn => {
          btn.addEventListener('click', (e) => {
              const tab = (e.target as HTMLElement).getAttribute('data-tab') as any;
              this.currentTab = tab;
              
              // Context-Aware Layer Switching
              if (this.currentTab === 'encounters') {
                  this.currentLayer = 'Encounters';
              } else if (this.currentTab === 'map') {
                  this.currentLayer = 'Ground'; // Default back to Ground
                  this.selectedTileId = 1; 
              } else if (this.currentTab === 'project') {
                  this.refreshMapList(); // Refresh map list when entering project tab
              }
              
              this.render();
          });
      });

      // Tools
      this.sidebar.querySelectorAll('.tool-btn').forEach(btn => {
          btn.addEventListener('click', (e) => {
              const tool = (e.target as HTMLElement).getAttribute('data-tool') as any;
              this.currentTool = tool;
              this.render();
          });
      });
      
      // Global Delegation for Script Picker (Objects Tab)
      this.sidebar.addEventListener('click', (e) => {
          const target = e.target as HTMLElement;
          if (target && target.classList.contains('select-script-btn')) {
              e.preventDefault();
              console.log('[Editor] Select Script button clicked');
              
              this.showScriptPickerDialog((scriptId) => {
                  console.log(`[Editor] Selected script: ${scriptId}`);
                  
                  // Update the input field next to the button
                  const container = target.parentElement;
                  if (container) {
                      const input = container.querySelector('input[data-key="triggerId"]') as HTMLInputElement;
                      
                      if (input) {
                          input.value = scriptId;
                          // Update property directly
                          this.objectProps.triggerId = scriptId;
                          // Trigger update
                          if (this.selectedObjectId !== null) {
                               // Find the actual object reference
                               const obj = this.game.map.findObjectById(this.selectedObjectId);
                               if (obj) {
                                   this.updateObjectProperty(obj, 'triggerId', scriptId);
                               }
                          }
                      } else {
                          console.error('[Editor] Could not find associated input for script picker');
                      }
                  }
              });
          }
      });

      // Save
      document.getElementById('editor-save-btn')?.addEventListener('click', () => this.saveMap());
      document.getElementById('editor-resize-btn')?.addEventListener('click', () => this.showResizeDialog());
      document.getElementById('save-encounters-btn')?.addEventListener('click', () => this.saveEncounters());
      document.getElementById('undo-btn')?.addEventListener('click', () => this.performUndo());
      document.getElementById('redo-btn')?.addEventListener('click', () => this.performRedo());
      
      // Purge Button
      document.getElementById('purge-btn')?.addEventListener('click', () => {
          if (confirm('Are you sure you want to delete ALL Encounter Zone objects? This cannot be undone.')) {
              if (this.game) {
                  const count = this.game.map.clearAllObjectsOfType('EncounterZone');
                  alert(`Purged ${count} Encounter Zones.`);
              }
              this.render();
          }
      });
      
      // Delete Trigger Buttons
      this.sidebar.querySelectorAll('.delete-trigger-btn').forEach(btn => {
          btn.addEventListener('click', (e) => {
              const triggerId = parseInt((e.target as HTMLElement).getAttribute('data-id') || '0');
              if (confirm('Delete this trigger?')) {
                  if (this.game && this.game.map.deleteTriggerById(triggerId)) {
                      console.log(`[Editor] Deleted trigger ${triggerId}`);
                      this.render(); // Refresh UI
                  } else {
                      alert('Failed to delete trigger');
                  }
              }
          });
      });
      
      // Layer Select
      document.getElementById('layer-select')?.addEventListener('change', (e) => {
          this.currentLayer = (e.target as HTMLSelectElement).value;
          this.selectedTileId = 1; // Reset to default tool for that layer
          this.render();
      });

      // Map Tileset Select
      document.getElementById('map-tileset-select')?.addEventListener('change', (e) => {
          const id = (e.target as HTMLSelectElement).value;
          if (id) {
              this.tilesetManager.setActiveTileset(id);
              this.render();
          }
      });
      
      // Tool Select Buttons
      this.sidebar.querySelectorAll('.tool-btn').forEach(btn => {
          btn.addEventListener('click', (e) => {
              const tool = (e.target as HTMLElement).getAttribute('data-tool');
              if (tool) {
                  this.currentTool = tool as any;
                  this.render();
              }
          });
      });

      // Scripts Tab Events - Use event delegation
      if (this.currentTab === 'scripts') {
          setTimeout(() => {
              console.log('[Editor] Setting up Scripts tab event listeners');
              
              // Use event delegation for buttons that might not exist yet
              const handleClick = (e: Event) => {
                  const target = e.target as HTMLElement;
                  
                  // New Script Button
                  if (target.id === 'new-script-btn' || target.closest('#new-script-btn')) {
                      console.log('[Editor] New Script button clicked');
                      if (!this.scriptsData) this.scriptsData = {};
                      
                      this.showInputDialog('Enter script ID (e.g., "rival_battle"):', '', (scriptId) => {
                          if (scriptId && scriptId.trim()) {
                              if (this.scriptsData[scriptId]) {
                                  alert(`Script "${scriptId}" already exists!`);
                                  return;
                              }
                              console.log(`[Editor] Creating new script: ${scriptId}`);
                              this.scriptsData[scriptId] = { actions: [] };
                              this.saveScripts();
                              this.render();
                          }
                      });
                      return;
                  }
                  
                  // New Folder Button
                  if (target.id === 'new-folder-btn' || target.closest('#new-folder-btn')) {
                      console.log('[Editor] New Folder button clicked');
                      if (!this.scriptsData) this.scriptsData = {};
                      if (!this.scriptsData._folders) this.scriptsData._folders = {};
                      
                      this.showInputDialog('Enter folder name:', '', (folderName) => {
                          if (folderName && folderName.trim()) {
                              if (this.scriptsData._folders[folderName]) {
                                  alert(`Folder "${folderName}" already exists!`);
                                  return;
                              }
                              console.log(`[Editor] Creating new folder: ${folderName}`);
                              this.scriptsData._folders[folderName] = [];
                              this.saveScripts();
                              this.render();
                          }
                      });
                      return;
                  }
                  
                  // Edit Script Button
                  if (target.classList.contains('edit-script-btn')) {
                      const scriptId = target.getAttribute('data-script-id');
                      if (scriptId) this.openScriptBuilder(scriptId);
                      return;
                  }
                  
                  // Delete Script Button
                  if (target.classList.contains('delete-script-btn')) {
                      const scriptId = target.getAttribute('data-script-id');
                      if (scriptId && confirm(`Delete "${scriptId}"?`)) {
                          delete this.scriptsData[scriptId];
                          if (this.scriptsData._folders) {
                              for (const f in this.scriptsData._folders) {
                                  const i = this.scriptsData._folders[f].indexOf(scriptId);
                                  if (i > -1) this.scriptsData._folders[f].splice(i, 1);
                              }
                          }
                          this.saveScripts();
                          this.render();
                      }
                      return;
                  }
                  
                  // Delete Folder Button
                  if (target.classList.contains('delete-folder-btn')) {
                      const folderName = target.getAttribute('data-folder');
                      if (folderName && confirm(`Delete folder "${folderName}"?`)) {
                          delete this.scriptsData._folders[folderName];
                          this.saveScripts();
                          this.render();
                      }
                      return;
                  }
              };
              
              // Add single click listener to sidebar
              this.sidebar.addEventListener('click', handleClick);
              
              // Setup drag-and-drop global functions
              (window as any).editorDragScript = (event: DragEvent, scriptId: string) => {
                  event.dataTransfer!.setData('text/plain', scriptId);
              };
              
              (window as any).editorDropScript = (event: DragEvent, folderName: string | null) => {
                  const scriptId = event.dataTransfer!.getData('text/plain');
                  if (!this.scriptsData._folders) this.scriptsData._folders = {};
                  for (const f in this.scriptsData._folders) {
                      const i = this.scriptsData._folders[f].indexOf(scriptId);
                      if (i > -1) this.scriptsData._folders[f].splice(i, 1);
                  }
                  if (folderName) {
                      if (!this.scriptsData._folders[folderName]) this.scriptsData._folders[folderName] = [];
                      if (!this.scriptsData._folders[folderName].includes(scriptId)) {
                          this.scriptsData._folders[folderName].push(scriptId);
                      }
                  }
                  this.saveScripts();
                  this.render();
              };
          }, 0);
      }

      // Project Tab Events
      if (this.currentTab === 'project') {
          setTimeout(() => {
              const newMapBtn = document.getElementById('new-map-btn');
              console.log('[Editor] New Map button found:', !!newMapBtn);
              newMapBtn?.addEventListener('click', () => {
                  console.log('[Editor] New Map button clicked');
                  this.showNewMapDialog();
              });

              document.getElementById('refresh-maps-btn')?.addEventListener('click', () => {
                  this.refreshMapList();
              });

              // Map Switching
              this.sidebar.querySelectorAll('.map-item').forEach(item => {
                  item.addEventListener('click', () => {
                      const path = item.getAttribute('data-path');
                      console.log('[Editor] Map item clicked:', path);
                      if (path && this.game) {
                          if (confirm(`Switch to map: ${path}?\n\nAny UNSAVED changes to the current map will be lost.`)) {
                              (this.game as any).loadLevel(path);
                              this.selectedObjectId = null;
                              this.render();
                          }
                      }
                  });
              });
          }, 0);
      }

      // Encounter Editor Events
      if (this.currentTab === 'encounters') {
          // Zone Change
          document.getElementById('encounter-zone-select')?.addEventListener('change', (e) => {
              this.objectProps.activeEncounterZone = (e.target as HTMLSelectElement).value;
              this.render();
          });
          
          // Render Color Preview
          const activeZone = this.objectProps.activeEncounterZone || 'default_zone';
          const preview = document.getElementById('zone-color-preview');
          if (preview && this.game) {
              // Find ID
              let id = 1;
              const mapping = this.game.map.zoneMapping;
               // Reverse lookup or find free ID
              const existingId = Object.keys(mapping).find(key => mapping[parseInt(key)] === activeZone);
              
              if (existingId) {
                  id = parseInt(existingId);
              } else {
                  // Don't auto-assign here, only on Paint. Just show gray or something? 
                  // actually let's just pick a hypothetical color
                  id = 999; 
              }
              
              const hue = (id * 137.508) % 360;
              preview.style.background = `hsla(${hue}, 70%, 50%, 1)`;
          }

          // Paint Zone Button
          document.getElementById('paint-zone-btn')?.addEventListener('click', () => {
             if (!this.game) return;
             
             const zoneName = this.objectProps.activeEncounterZone;
             if (!zoneName) {
                 alert('No zone selected!');
                 return;
             }
             
             // 1. Get or Create ID
             let id = 0;
             const mapping = this.game.map.zoneMapping;
             
             const existingId = Object.keys(mapping).find(key => mapping[parseInt(key)] === zoneName);
             if (existingId) {
                 id = parseInt(existingId);
             } else {
                 // Allocate new ID
                 const usedIds = Object.keys(mapping).map(k => parseInt(k));
                 let newId = 1;
                 while (usedIds.includes(newId)) newId++;
                 
                 mapping[newId] = zoneName;
                 id = newId;
                 console.log(`[Editor] Assigned ID ${id} to Zone '${zoneName}'`);
             }
             
             // 2. Set Tool State
             this.currentLayer = 'Encounters';
             this.currentTool = 'pencil';
             this.selectedTileId = id;
             
             alert(`Paint Tool Activated for '${zoneName}' (ID: ${id})`);
             this.render(); // Refreshes tool state visualization
          });

          // Add Zone
          document.getElementById('add-zone-btn')?.addEventListener('click', () => {
              const modal = document.createElement('div');
              modal.style.cssText = `position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.8); display:flex; align-items:center; justify-content:center; z-index:10000;`;
              modal.innerHTML = `
                <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:300px; color:white; font-family:sans-serif;">
                    <h4 style="margin-top:0;">New Encounter Zone</h4>
                    <input type="text" id="z-name" placeholder="zone_name" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; border-radius:4px; margin-bottom:15px;">
                    <div style="display:flex; gap:10px; justify-content:flex-end;">
                        <button id="z-cancel" style="padding:5px 10px; background:#555; border:none; color:white; cursor:pointer; border-radius:3px;">Cancel</button>
                        <button id="z-create" style="padding:5px 10px; background:#2196F3; border:none; color:white; cursor:pointer; border-radius:3px;">Add Zone</button>
                    </div>
                </div>
              `;
              document.body.appendChild(modal);
              const input = document.getElementById('z-name') as HTMLInputElement;
              input.focus();
              document.getElementById('z-cancel')?.addEventListener('click', () => document.body.removeChild(modal));
              document.getElementById('z-create')?.addEventListener('click', () => {
                  const name = input.value;
                  if (name) {
                      if (!this.encounterData[name]) {
                          this.encounterData[name] = { encounters: [] };
                          this.objectProps.activeEncounterZone = name;
                          this.render();
                      } else {
                          alert('Zone already exists!');
                      }
                  }
                  document.body.removeChild(modal);
              });
          });

          // Delete Zone
          document.getElementById('del-zone-btn')?.addEventListener('click', () => {
              const zones = this.objectProps.activeEncounterZone;
              if (confirm(`Delete zone '${zones}'?`)) {
                   delete this.encounterData[zones];
                   this.objectProps.activeEncounterZone = Object.keys(this.encounterData)[0] || '';
                   this.render();
              }
          });

          // Add Encounter
          document.getElementById('add-encounter-btn')?.addEventListener('click', () => {
              const id = (document.getElementById('new-enc-id') as HTMLSelectElement).value;
              const min = parseInt((document.getElementById('new-enc-min') as HTMLInputElement).value);
              const max = parseInt((document.getElementById('new-enc-max') as HTMLInputElement).value);
              const weight = parseInt((document.getElementById('new-enc-weight') as HTMLInputElement).value);
              
              if (id && !isNaN(min) && !isNaN(max) && !isNaN(weight)) {
                  const data = this.encounterData;
                  const zone = this.objectProps.activeEncounterZone || Object.keys(data)[0];
                  
                  if (!data[zone]) data[zone] = { encounters: [] }; // Safety init
                  
                  if (data[zone]) {
                      data[zone].encounters.push({
                          pokemonId: id,
                          levelMin: min,
                          levelMax: max,
                          weight: weight,
                          timeOfDay: 'Any'
                      });
                      this.render();
                  }
              }
          });

          // Delete Encounter
          this.sidebar.querySelectorAll('.del-encounter-btn').forEach(btn => {
              btn.addEventListener('click', (e) => {
                  const idx = parseInt((e.target as HTMLElement).getAttribute('data-idx') || '-1');
                  if (idx >= 0) {
                      const data = this.encounterData;
                      const zone = this.objectProps.activeEncounterZone || Object.keys(data)[0];
                      
                      if (data[zone] && data[zone].encounters[idx]) {
                          data[zone].encounters.splice(idx, 1);
                          this.render();
                      }
                  }
              });
          });
      }

      // Object Select
      document.getElementById('obj-type-select')?.addEventListener('change', (e) => {
          this.selectedObjectType = (e.target as HTMLSelectElement).value;
          this.selectedObjectId = null; // Deselect any object when changing type
          this.render(); // Re-render to show properties
      });
      
      // Object Deselect
      document.getElementById('obj-deselect-btn')?.addEventListener('click', () => {
          this.selectedObjectId = null;
          this.render();
      });

      // Script Edit Button
      const editScriptBtn = document.getElementById('edit-script-btn');
      if (editScriptBtn) {
          editScriptBtn.addEventListener('click', () => {
              const scriptId = this.objectProps.triggerId || `event_${Date.now()}`;
              this.objectProps.triggerId = scriptId;
              
              // Open Builder
              this.openScriptBuilder(scriptId);
          });
      }

      // Edit Script Button (Trigger - for placement form)
      document.getElementById('edit-script-btn-trigger')?.addEventListener('click', async () => {
          const scriptId = this.objectProps.triggerId;
          if (!scriptId || scriptId.trim() === '') {
              alert('Please enter a Script ID first (e.g. "rival_ambush")!');
              return;
          }
          await this.openScriptBuilder(scriptId);
      });

      // NPC Sprite Picker
      document.getElementById('npc-sprite-picker-btn')?.addEventListener('click', async () => {
          const res = await (window as any).fs.showOpenDialog({
              properties: ['openFile'],
              filters: [{ name: 'Images', extensions: ['png', 'jpg'] }]
          });
          
          if (!res.canceled && res.filePaths.length > 0) {
              const fullPath = res.filePaths[0];
              // Convert to relative path if possible, or use full path handling in main
              // ideally we copy it to 'data/characters', but for now let's use the path.
              // We might need to handle absolute paths in Game.ts loadSprite.
              
              // Find the input and update it
              const input = this.sidebar.querySelector('input[data-key="sprite"]') as HTMLInputElement;
              if (input) {
                  input.value = fullPath;
                  input.dispatchEvent(new Event('input')); // Trigger update
                  
                  // Show Preview
                  this.updateSpritePreview(fullPath);
              }
          }
      });
      
      // Object Property Inputs
      this.sidebar.querySelectorAll('.obj-prop').forEach(input => {
          input.addEventListener('input', (e) => {
              const target = e.target as HTMLInputElement;
              const key = target.getAttribute('data-key');
              if (key) {
                  this.objectProps[key] = target.value;
                  // If an object is selected, update its properties immediately
                  if (this.selectedObjectId !== null && this.game) {
                      const obj = this.game.map.getObjectById(this.selectedObjectId);
                      if (obj) {
                          if (!obj.properties) obj.properties = [];
                          const prop = obj.properties.find(p => p.name === key);
                          if (prop) {
                              prop.value = target.value;
                          } else {
                              // Add new property if it doesn't exist
                              obj.properties.push({ name: key, type: typeof target.value === 'number' ? 'int' : 'string', value: target.value });
                          }
                          console.log(`[Editor] Updated property ${key} to ${target.value} on object ${obj.id}`);
                      }
                  }
              }
          });
          
          input.addEventListener('keydown', (e) => {
              e.stopPropagation();
          });
      });

      // Boolean Properties (Checkboxes)
      this.sidebar.querySelectorAll('.obj-prop-bool').forEach(input => {
          input.addEventListener('change', (e) => {
              const target = e.target as HTMLInputElement;
              const key = target.getAttribute('data-key');
              if (key) {
                  this.objectProps[key] = target.checked;
                  if (this.selectedObjectId !== null && this.game) {
                      const obj = this.game.map.getObjectById(this.selectedObjectId);
                      if (obj) {
                          this.updateObjectProperty(obj, key, target.checked);
                          this.render(); // Re-render to show/hide conditional fields (like trainer team)
                      }
                  }
              }
          });
      });

      // Warp Picker Tool
      // Warp Picker Tool
      document.getElementById('warp-open-visual-picker-btn')?.addEventListener('click', () => {
          if (this.objectProps.targetMap) {
               this.showWarpDestinationPicker(this.objectProps.targetMap);
          } else {
              alert('Please select a Target Map first.');
          }
      });

      // Warp Linker Tool
      document.getElementById('create-warp-link-btn')?.addEventListener('click', () => {
          this.isLinkingWarp = !this.isLinkingWarp;
          this.render();
      });

      document.getElementById('warp-open-map-btn')?.addEventListener('click', () => {
          const mapPath = this.objectProps.targetMap;
          if (mapPath && this.game) {
              if (confirm(`Open target map: ${mapPath}?\n\nUnsaved changes will be lost.`)) {
                  (this.game as any).loadLevel(mapPath);
                  this.selectedObjectId = null;
                  this.render();
              }
          }
      });

      // Boolean Properties (Checkboxes)
      this.sidebar.querySelectorAll('.obj-prop-bool').forEach(input => {
          input.addEventListener('change', (e) => {
              const target = e.target as HTMLInputElement;
              const key = target.getAttribute('data-key');
              if (key) {
                  this.objectProps[key] = target.checked;
                  if (this.selectedObjectId !== null && this.game) {
                      const obj = this.game.map.getObjectById(this.selectedObjectId);
                      if (obj) {
                          this.updateObjectProperty(obj, key, target.checked);
                          this.render(); // Re-render to show/hide conditional fields (like trainer team)
                      }
                  }
              }
          });
      });



      // Trainer Team Events
      document.getElementById('add-team-btn')?.addEventListener('click', () => {
          if (this.selectedObjectId !== null && this.game) {
              const obj = this.game.map.getObjectById(this.selectedObjectId);
              if (obj) {
                  if (!this.objectProps.trainerTeam) this.objectProps.trainerTeam = [];
                  this.objectProps.trainerTeam.push({ species: '001', level: 5 });
                  this.updateObjectProperty(obj, 'trainerTeam', this.objectProps.trainerTeam);
                  this.render();
              }
          }
      });

      this.sidebar.querySelectorAll('.remove-team-btn').forEach(btn => {
          btn.addEventListener('click', (e) => {
              const idx = parseInt((e.target as HTMLElement).getAttribute('data-idx') || '0');
              if (this.selectedObjectId !== null && this.game) {
                  const obj = this.game.map.getObjectById(this.selectedObjectId);
                  if (obj && this.objectProps.trainerTeam) {
                      this.objectProps.trainerTeam.splice(idx, 1);
                      this.updateObjectProperty(obj, 'trainerTeam', this.objectProps.trainerTeam);
                      this.render();
                  }
              }
          });
      });

      this.sidebar.querySelectorAll('.team-prop').forEach(input => {
          input.addEventListener('change', (e) => {
              const target = e.target as HTMLInputElement;
              const idx = parseInt(target.getAttribute('data-idx') || '0');
              const field = target.getAttribute('data-field');
              if (this.selectedObjectId !== null && this.game && field) {
                  const obj = this.game.map.getObjectById(this.selectedObjectId);
                  if (obj && this.objectProps.trainerTeam && this.objectProps.trainerTeam[idx]) {
                      const val = field === 'level' ? parseInt(target.value) : target.value;
                      this.objectProps.trainerTeam[idx][field] = val;
                      this.updateObjectProperty(obj, 'trainerTeam', this.objectProps.trainerTeam);
                  }
              }
          });
      });
          this.sidebar.querySelectorAll('.tile-btn').forEach(btn => {
              btn.addEventListener('click', (e) => {
                  const id = parseInt((e.target as HTMLElement).getAttribute('data-id') || '1');
                  this.selectedTileId = id;
                  this.selectedTiles = [[id]];
                  this.selectionRect = null;
                  this.render(); 
              });
          });
          
          // Single Image Palette Picker
          const paletteImg = document.getElementById('active-tileset-image') as HTMLImageElement;
          if (paletteImg) {
              
              // Restore highlight position if tile selected
              if (this.selectedTileId > 0 && this.tilesetManager.getActiveTileset()) {
                  const tileset = this.tilesetManager.getActiveTileset();
                  if (tileset) {
                      const cols = tileset.columns;
                      const col = this.selectedTileId % cols;
                      const row = Math.floor(this.selectedTileId / cols);
                      
                      const hl = document.getElementById('palette-highlight');
                      if (hl) {
                          const ds = 16 * this.paletteZoom;
                          hl.style.display = 'block';
                          hl.style.left = `${col * ds}px`;
                          hl.style.top = `${row * ds}px`;
                          hl.style.width = `${ds}px`;
                          hl.style.height = `${ds}px`;
                          
                          // Auto scroll to selection
                          const container = document.getElementById('palette-scroll-container');
                          if (container) {
                              // Simple centering logic could go here
                          }
                      }
                  }
              }

              // Tile Palette Scaling
              const displaySize = 16 * this.paletteZoom;

              paletteImg.addEventListener('mousedown', (e) => {
                  const tileset = this.tilesetManager.getActiveTileset();
                  if (!tileset) return;
                  
                  const rect = paletteImg.getBoundingClientRect();
                  const x = e.clientX - rect.left;
                  const y = e.clientY - rect.top;
                  
                  this.selectionStartX = Math.floor(x / displaySize);
                  this.selectionStartY = Math.floor(y / displaySize);
                  
                  // Initial selection of 1x1
                  this.updatePaletteSelection(this.selectionStartX, this.selectionStartY, this.selectionStartX, this.selectionStartY);
                  
                  const onMouseMove = (moveE: MouseEvent) => {
                      const moveRect = paletteImg.getBoundingClientRect();
                      const moveX = moveE.clientX - moveRect.left;
                      const moveY = moveE.clientY - moveRect.top;
                      
                      const currentX = Math.floor(moveX / displaySize);
                      const currentY = Math.floor(moveY / displaySize);
                      
                      this.updatePaletteSelection(this.selectionStartX, this.selectionStartY, currentX, currentY);
                  };
                  
                  const onMouseUp = () => {
                      window.removeEventListener('mousemove', onMouseMove);
                      window.removeEventListener('mouseup', onMouseUp);
                      
                      // Finalize selectedTiles 2D array
                      this.finalizePaletteSelection();
                  };
                  
                  window.addEventListener('mousemove', onMouseMove);
                  window.addEventListener('mouseup', onMouseUp);
                  
                  e.preventDefault();
              });

              // Palette Zoom Buttons
              this.sidebar.querySelectorAll('.palette-zoom-btn').forEach(btn => {
                  btn.addEventListener('click', (e) => {
                      const zoom = parseFloat((e.target as HTMLElement).getAttribute('data-zoom') || '2');
                      this.paletteZoom = zoom;
                      this.render();
                  });
              });

              document.getElementById('palette-fit-btn')?.addEventListener('click', () => {
                  const container = document.getElementById('palette-scroll-container');
                  const tileset = this.tilesetManager.getActiveTileset();
                  if (container && tileset) {
                      const sidebarWidth = container.clientWidth - 10; // small buffer
                      const zoom = sidebarWidth / (tileset.columns * 16);
                      this.paletteZoom = Math.max(0.1, Math.min(zoom, 4));
                      this.render();
                  }
              });
          }
      
      // Tileset Tab Events
      if (this.currentTab === 'tilesets') {
          // Import button - use setTimeout to ensure DOM is ready
          setTimeout(() => {
              const importBtn = document.getElementById('import-tileset-btn');
              console.log('[Editor] Import button found:', !!importBtn);
              importBtn?.addEventListener('click', () => {
                  console.log('[Editor] Import button clicked!');
                  this.showTilesetImportDialog();
              });
              
              // Tileset selection
              this.sidebar.querySelectorAll('.tileset-item').forEach(item => {
                  item.addEventListener('click', (e) => {
                      const target = e.target as HTMLElement;
                      // Don't trigger if clicking delete button
                      if (target.classList.contains('delete-tileset-btn')) return;
                      
                      const id = item.getAttribute('data-id');
                      console.log('[Editor] Clicked tileset item:', id);
                      if ((window as any).fs && (window as any).fs.log) (window as any).fs.log(`[Editor] Clicked tileset item: ${id}`);

                      if (id) {
                          this.tilesetManager.setActiveTileset(id);
                          this.render();
                      }
                  });
              });
              
              // Delete buttons
              this.sidebar.querySelectorAll('.delete-tileset-btn').forEach(btn => {
                  btn.addEventListener('click', async (e) => {
                      e.stopPropagation();
                      const id = (e.target as HTMLElement).getAttribute('data-id');
                      if (id && confirm('Delete this tileset?')) {
                          await this.tilesetManager.deleteTileset(id);
                          this.render();
                      }
                  });
              });
          }, 0);
      }

      // Layer Panel Events
      this.sidebar.querySelectorAll('.layer-visibility-checkbox').forEach(cb => {
          cb.addEventListener('change', (e) => {
              const layerName = (e.target as HTMLInputElement).getAttribute('data-layer');
              if (layerName) {
                  this.layerManager.toggleVisibility(layerName);
              }
          });
      });

      this.sidebar.querySelectorAll('.layer-selector').forEach(span => {
          span.addEventListener('click', (e) => {
              const layerName = (e.target as HTMLElement).getAttribute('data-layer');
              if (layerName) {
                  this.currentLayer = layerName;
                  this.render();
              }
          });
      });

      // Project Tab Events
      if (this.currentTab === 'project') {
          setTimeout(() => {
              // Search
              const searchInput = document.getElementById('project-search') as HTMLInputElement;
              searchInput?.addEventListener('input', () => {
                  this.projectSearchTerm = searchInput.value;
                  this.render();
                  // Re-focus after render
                  document.getElementById('project-search')?.focus();
              });

              // Navigation: Folder Up
              this.sidebar.querySelector('.folder-up-btn')?.addEventListener('click', () => {
                  const parts = this.currentProjectPath.split('/');
                  if (parts.length > 1) {
                      parts.pop();
                      this.currentProjectPath = parts.join('/');
                      this.refreshMapList();
                  }
              });

              // Navigation: Breadcrumbs
              this.sidebar.querySelectorAll('.path-segment').forEach(seg => {
                  seg.addEventListener('click', () => {
                      const path = seg.getAttribute('data-path');
                      if (path) {
                          this.currentProjectPath = path;
                          this.refreshMapList();
                      }
                  });
              });

              // New Folder
              document.getElementById('new-folder-btn')?.addEventListener('click', () => {
                  this.showNewFolderDialog();
              });

              // Rename Actions
              this.sidebar.querySelectorAll('.rename-item-btn').forEach(btn => {
                  btn.addEventListener('click', (e) => {
                      e.stopPropagation();
                      const path = (e.target as HTMLElement).getAttribute('data-path');
                      if (path) this.showRenameDialog(path);
                  });
              });

              // Delete Actions
              this.sidebar.querySelectorAll('.delete-item-btn').forEach(btn => {
                  btn.addEventListener('click', (e) => {
                      e.stopPropagation();
                      const path = (e.currentTarget as HTMLElement).getAttribute('data-path');
                      if (path) this.showDeleteConfirm(path);
                  });
              });

              // Project Item Clicks (Navigate or Load)
              this.sidebar.querySelectorAll('.project-item').forEach(item => {
                  item.addEventListener('click', (e) => {
                      // Don't trigger if clicking buttons inside
                      if ((e.target as HTMLElement).classList.contains('rename-item-btn') || 
                          (e.target as HTMLElement).classList.contains('delete-item-btn')) return;

                      const path = item.getAttribute('data-path');
                      const name = item.getAttribute('data-name');
                      const isDir = item.classList.contains('dir-item');

                      if (isDir && path) {
                          this.currentProjectPath = path;
                          this.refreshMapList();
                      } else if (path && this.game) {
                          if (confirm(`Switch to map: ${name}?\n\nAny UNSAVED changes to the current map will be lost.`)) {
                              (this.game as any).loadLevel(path);
                              this.selectedObjectId = null;
                              this.render();
                          }
                      }
                  });
              });

              // New Map (already defined in previous loops, but let's ensure it's bound correctly)
              document.getElementById('new-map-btn')?.addEventListener('click', () => {
                  this.showNewMapDialog();
              });

              document.getElementById('refresh-maps-btn')?.addEventListener('click', () => {
                  this.refreshMapList();
              });

              // Set Home Map
              this.sidebar.querySelectorAll('.set-home-btn').forEach(btn => {
                  btn.addEventListener('click', (e) => {
                      e.stopPropagation();
                      const path = (e.currentTarget as HTMLElement).getAttribute('data-path');
                      if (path) this.setHomeMap(path);
                  });
              });
          }, 0);
      }
  }
  
  private async showTilesetImportDialog(): Promise<void> {
      // Create modal dialog
      const modal = document.createElement('div');
      modal.style.cssText = `
          position: fixed;
          top: 0;
          left: 0;
          width: 100%;
          height: 100%;
          background: rgba(0,0,0,0.8);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 10000;
      `;
      
      modal.innerHTML = `
          <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:450px; color:white; font-family: sans-serif;">
              <h3 style="margin-top:0; border-bottom:1px solid #444; padding-bottom:10px;">Import Tileset</h3>
              
              <div style="margin-bottom:15px;">
                  <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Tileset Name</label>
                  <input type="text" id="tileset-name-input" placeholder="e.g., Terrain" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
              </div>
              
              <div style="margin-bottom:15px;">
                  <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Tile Size (pixels)</label>
                  <input type="number" id="tileset-size-input" value="${TILE_SIZE}" min="8" max="128" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
              </div>
              
              <div style="margin-bottom:20px;">
                  <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Image Path</label>
                  <div style="display:flex; gap:8px;">
                      <input type="text" id="tileset-path-input" placeholder="Select a PNG file..." style="flex:1; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
                      <button id="tileset-browse-btn" style="padding:8px 12px; background:#2196F3; border:none; color:white; cursor:pointer; border-radius:4px; font-weight:bold;">Browse...</button>
                  </div>
                  <p style="font-size:11px; color:#aaa; margin:5px 0 0 0;">Supported formats: PNG</p>
              </div>
              
              <div style="display:flex; gap:10px; justify-content:flex-end; padding-top:10px; border-top:1px solid #444;">
                  <button id="tileset-cancel-btn" style="padding:8px 16px; background:#555; border:none; color:white; cursor:pointer; border-radius:4px;">Cancel</button>
                  <button id="tileset-import-btn-modal" style="padding:8px 16px; background:#4CAF50; border:none; color:white; cursor:pointer; border-radius:4px; font-weight:bold;">Import Tileset</button>
              </div>
          </div>
      `;
      
      document.body.appendChild(modal);
      
      // Focus first input
      setTimeout(() => {
          (document.getElementById('tileset-name-input') as HTMLInputElement)?.focus();
      }, 100);
      
      // Browse Button
      document.getElementById('tileset-browse-btn')?.addEventListener('click', async () => {
          const result = await (window as any).fs.showOpenDialog({
              title: 'Select Tileset Image',
              properties: ['openFile'],
              filters: [{ name: 'Images', extensions: ['png'] }]
          });
          
          if (result && result.success && !result.canceled && result.filePaths.length > 0) {
              const path = result.filePaths[0];
              const pathInput = document.getElementById('tileset-path-input') as HTMLInputElement;
              pathInput.value = path;
              
              // Auto-fill name if empty
              const nameInput = document.getElementById('tileset-name-input') as HTMLInputElement;
              if (!nameInput.value) {
                  // Extract filename without extension
                  const filename = path.split(/[\\/]/).pop()?.replace(/\.[^/.]+$/, "") || "";
                  // Capitalize first letter and replace underscores/dashes with spaces
                  const formattedName = filename
                      .replace(/[-_]/g, ' ')
                      .replace(/\b\w/g, (c: string) => c.toUpperCase());
                  nameInput.value = formattedName;
              }
          }
      });
      
      // Handle cancel
      document.getElementById('tileset-cancel-btn')?.addEventListener('click', () => {
          document.body.removeChild(modal);
      });
      
      // Handle import
      document.getElementById('tileset-import-btn-modal')?.addEventListener('click', async () => {
          const name = (document.getElementById('tileset-name-input') as HTMLInputElement).value;
          const sizeStr = (document.getElementById('tileset-size-input') as HTMLInputElement).value;
          const imagePath = (document.getElementById('tileset-path-input') as HTMLInputElement).value;
          
          if (!name || !sizeStr || !imagePath) {
              alert('Please fill in all fields!');
              return;
          }
          
          const tileSize = parseInt(sizeStr);
          if (isNaN(tileSize) || tileSize < 8 || tileSize > 128) {
              alert('Invalid tile size! Must be between 8 and 128.');
              return;
          }
          
          // Close modal
          document.body.removeChild(modal);
          
          try {
              await this.tilesetManager.importTileset(name, imagePath, tileSize, tileSize);
              alert('Tileset imported successfully!');
              this.render();
          } catch (error: any) {
              alert('Error importing tileset: ' + error.message);
              console.error('[Editor] Tileset import error:', error);
          }
  


      });
  }

  private floodFill(startX: number, startY: number, replacementTileId: number): void {
      if (!this.game) return;
      
      const map = this.game.map;
      const targetTileId = map.getTile(startX, startY, this.currentLayer);
      
      if (targetTileId === replacementTileId) return;

      const w = map.width;
      const h = map.height;
      const stack: {x: number, y: number}[] = [{x: startX, y: startY}];

      // Safety counter to prevent infinite loops if something goes wrong
      let iterations = 0;
      const MAX_ITERATIONS = w * h;

      const changes: {x: number, y: number, before: number, after: number}[] = [];
      const visited = new Set<string>();

      while(stack.length > 0 && iterations < MAX_ITERATIONS) {
          iterations++;
          const p = stack.pop();
          if (!p) continue;
          
          const x = p.x;
          const y = p.y;
          const key = `${x},${y}`;
          
          if (x < 0 || x >= w || y < 0 || y >= h) continue;
          if (visited.has(key)) continue;
          
          const currentTile = map.getTile(x, y, this.currentLayer);
          if (currentTile === targetTileId) {
              changes.push({ x, y, before: targetTileId, after: replacementTileId });
              visited.add(key);
              map.setTile(x, y, replacementTileId, this.currentLayer);
              
              stack.push({x: x + 1, y: y});
              stack.push({x: x - 1, y: y});
              stack.push({x: x, y: y + 1});
              stack.push({x: x, y: y - 1});
          }
      }

      if (changes.length > 0) {
          this.undoManager.record({
              type: 'tile_fill',
              timestamp: Date.now(),
              description: 'Flood Fill',
              data: {
                  layer: this.currentLayer,
                  tiles: changes
              }
          });
          this.render();
      }

      console.log(`[Editor] Flood fill complete. ${changes.length} tiles changed.`);
  }

  private applyRect(x1: number, y1: number, x2: number, y2: number): void {
      if (!this.game) return;
      
      const startX = Math.min(x1, x2);
      const endX = Math.max(x1, x2);
      const startY = Math.min(y1, y2);
      const endY = Math.max(y1, y2);
      
      const tileId = this.selectedTileId;
      // Convert to GID if needed
      let finalTileId = tileId;
      // SKIP for Encounters layer (uses raw IDs)
      if (tileId !== 0 && this.currentLayer !== 'Encounters') {
          const activeTileset = this.tilesetManager.getActiveTileset();
          if (activeTileset) {
            finalTileId = this.tilesetManager.getGlobalId(activeTileset.id, tileId);
          }
      }
      
      const changes: {x: number, y: number, before: number, after: number}[] = [];
      
      for(let y = startY; y <= endY; y++) {
          for(let x = startX; x <= endX; x++) {
              const oldTileId = this.game.map.getTile(x, y, this.currentLayer);
              if (oldTileId !== finalTileId) {
                  changes.push({ x, y, before: oldTileId, after: finalTileId });
                  this.game.map.setTile(x, y, finalTileId, this.currentLayer);
              }
          }
      }

      if (changes.length > 0) {
          this.undoManager.record({
              type: 'tile_rect',
              timestamp: Date.now(),
              description: 'Rectangle Fill',
              data: {
                  layer: this.currentLayer,
                  tiles: changes
              }
          });
          this.render();
      }
      console.log(`[Editor] Rect Fill complete. ${changes.length} tiles changed.`);
  }

  private async saveMap(): Promise<void> {
      if (!this.game) return;
      console.log('Saving Map...');
      
      const mapData = this.game.map.serialize();
      console.log('[Editor] Serializing Map. Layers found:', mapData.layers.map(l => l.name));
      const content = JSON.stringify(mapData, null, 2);
      
      const path = this.game.currentLevelPath || 'maps/sample_map.json';
      
      console.log(`Writing to ${path}...`);
      const result = await (window as any).fs.writeFile(path, content);
      
      if (result.success) {
          console.log('Map Saved Successfully!');
          alert('Map Saved Successfully!');
          // Visual feedback
          const btn = document.getElementById('editor-save-btn');
          if (btn) {
              const originalText = btn.innerText;
              btn.innerText = 'Saved!';
              btn.style.background = '#2E7D32';
              setTimeout(() => {
                  btn.innerText = originalText;
                  btn.style.background = '#4CAF50';
              }, 2000);
          }
      } else {
          console.error('Failed to save map:', result.error);
          alert('Error saving map: ' + result.error);
      }
  }
  
  public renderOverlay(ctx: CanvasRenderingContext2D): void {
      if (!this.isActive() || !this.game || !this.game.map) return;

      const camX = this.game.camera.x;
      const camY = this.game.camera.y;

      // Draw Map Boundary
      const mapPixelW = this.game.map.width * TILE_SIZE;
      const mapPixelH = this.game.map.height * TILE_SIZE;
      
      const boundX = -camX;
      const boundY = -camY;
      
      ctx.save();
      
      // Darken outside area
      ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
      
      const zoom = this.game.zoom || 1;
      const displayScale = this.game.display.scale * zoom;
      const screenW = ctx.canvas.width / displayScale;
      const screenH = ctx.canvas.height / displayScale;
      
      // Top
      if (boundY > 0) ctx.fillRect(0, 0, screenW, boundY);
      // Bottom
      if (boundY + mapPixelH < screenH) 
          ctx.fillRect(0, boundY + mapPixelH, screenW, screenH - (boundY + mapPixelH));
      // Left
      if (boundX > 0) ctx.fillRect(0, boundY, boundX, mapPixelH);
      // Right
      if (boundX + mapPixelW < screenW)
          ctx.fillRect(boundX + mapPixelW, boundY, screenW - (boundX + mapPixelW), mapPixelH);

      // Render Grid
      ctx.beginPath();
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
      ctx.lineWidth = 1;

      // Vertical Lines (Optimized to Viewport)
      const startGridX = Math.floor(camX / TILE_SIZE);
      const endGridX = Math.floor((camX + (screenW * displayScale)) / TILE_SIZE) + 1;
      
      for (let x = Math.max(0, startGridX); x <= Math.min(this.game.map.width, endGridX); x++) {
          const drawX = (x * TILE_SIZE) - camX;
          ctx.moveTo(drawX, -camY);
          ctx.lineTo(drawX, mapPixelH - camY);
      }

      // Horizontal Lines
      const startGridY = Math.floor(camY / TILE_SIZE);
      const endGridY = Math.floor((camY + (screenH * displayScale)) / TILE_SIZE) + 1;

      for (let y = Math.max(0, startGridY); y <= Math.min(this.game.map.height, endGridY); y++) {
          const drawY = (y * TILE_SIZE) - camY;
          ctx.moveTo(-camX, drawY);
          ctx.lineTo(mapPixelW - camX, drawY);
      }
      ctx.stroke();

      // Boundary Border
      ctx.strokeStyle = '#ffff00'; // Yellow border for map bounds
      ctx.lineWidth = 2;
      ctx.strokeRect(boundX, boundY, mapPixelW, mapPixelH);

      // Draw Hover Highlight
      // Grid coordinates are logical (TILE_SIZE blocks)
      let rows = this.selectedTiles.length;
      let cols = this.selectedTiles[0].length;

      if (this.currentTab === 'objects') {
          // Force 16x16 cursor for Objects (especially NPCs)
          // Unless we want larger objects later, but for now 1x1 is safe.
          rows = 1;
          cols = 1;
      }
      
      const drawX = this.hoverGridX * TILE_SIZE - camX;
      const drawY = this.hoverGridY * TILE_SIZE - camY;
      
      const totalW = cols * TILE_SIZE;
      const totalH = rows * TILE_SIZE;

      // Only highlight if inside bounds (at least partially)
      if (drawX + totalW > boundX && drawX < boundX + mapPixelW &&
          drawY + totalH > boundY && drawY < boundY + mapPixelH) {
          
          ctx.strokeStyle = 'white';
          ctx.lineWidth = 1;
          ctx.strokeRect(drawX, drawY, totalW, totalH);
          
          ctx.fillStyle = 'rgba(255, 255, 255, 0.2)';
          ctx.fillRect(drawX, drawY, totalW, totalH);

          // If multi-tile, draw internal grid for clarity
          if (cols > 1 || rows > 1) {
              ctx.beginPath();
              ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
              for (let i = 1; i < cols; i++) {
                  ctx.moveTo(drawX + i * TILE_SIZE, drawY);
                  ctx.lineTo(drawX + i * TILE_SIZE, drawY + totalH);
              }
              for (let j = 1; j < rows; j++) {
                  ctx.moveTo(drawX, drawY + j * TILE_SIZE);
                  ctx.lineTo(drawX + totalW, drawY + j * TILE_SIZE);
              }
              ctx.stroke();
          }
      } else {
           // Out of bounds highlight (Red)
          ctx.strokeStyle = 'red';
          ctx.lineWidth = 1;
          ctx.strokeRect(drawX, drawY, totalW, totalH);
      }

      // Draw Object Selection Highlight
      if (this.currentTab === 'objects' && this.selectedObjectId !== null) {
          const selectedObj = this.game.map.getObjectById(this.selectedObjectId);
          if (selectedObj) {
              const objDrawX = selectedObj.x - camX;
              const objDrawY = selectedObj.y - camY;
              ctx.strokeStyle = '#00FFFF'; // Cyan for selected object
              ctx.lineWidth = 2;
              ctx.strokeRect(objDrawX, objDrawY, selectedObj.width, selectedObj.height);
              ctx.fillStyle = 'rgba(0, 255, 255, 0.2)';
              ctx.fillRect(objDrawX, objDrawY, selectedObj.width, selectedObj.height);
          }
      }
      
      // Draw Rectangle Tool Preview
    if (this.currentTool === 'rect' && this.dragStartX !== -1 && (this.currentTab === 'map' || this.currentTab === 'encounters')) {
        const startX = Math.min(this.dragStartX, this.hoverGridX);
        const endX = Math.max(this.dragStartX, this.hoverGridX);
        const startY = Math.min(this.dragStartY, this.hoverGridY);
        const endY = Math.max(this.dragStartY, this.hoverGridY);
        
        const w = (endX - startX + 1) * TILE_SIZE;
        const h = (endY - startY + 1) * TILE_SIZE;
        
        const drawPx = startX * TILE_SIZE - camX;
        const drawPy = startY * TILE_SIZE - camY;
        
        ctx.fillStyle = 'rgba(100, 200, 255, 0.4)';
        ctx.fillRect(drawPx, drawPy, w, h);
        
        ctx.strokeStyle = 'white';
        ctx.lineWidth = 2;
        ctx.strokeRect(drawPx, drawPy, w, h);
    } else if (this.currentTab === 'objects' && (this.selectedObjectType === 'Trigger' || this.selectedObjectType === 'EncounterZone') && this.dragStartX !== -1) {
        // Object Drag Preview
        const startX = Math.min(this.dragStartX, this.hoverGridX);
        const endX = Math.max(this.dragStartX, this.hoverGridX);
        const startY = Math.min(this.dragStartY, this.hoverGridY);
        const endY = Math.max(this.dragStartY, this.hoverGridY);
        
        const w = (endX - startX + 1) * TILE_SIZE;
        const h = (endY - startY + 1) * TILE_SIZE;
        
        const drawPx = startX * TILE_SIZE - camX;
        const drawPy = startY * TILE_SIZE - camY;
        
        // Red for Trigger, Green for Zone?
        ctx.fillStyle = this.selectedObjectType === 'Trigger' ? 'rgba(255, 100, 100, 0.4)' : 'rgba(100, 255, 100, 0.4)';
        ctx.fillRect(drawPx, drawPy, w, h);
        
        ctx.strokeStyle = 'white';
        ctx.lineWidth = 2;
        ctx.strokeRect(drawPx, drawPy, w, h);
    }
    
    ctx.restore();
  }
  // ... (Rest of render methods)
// ...
  private setupInput(): void {
      if (!this.game) return;
      
      const canvas = this.game.display.canvas;
      
      // Prevent Context Menu on Canvas
      canvas.addEventListener('contextmenu', (e) => e.preventDefault());

      canvas.addEventListener('mousedown', (e) => {
          if (!this.isActive()) return;
          
          // Warp Linker Start
          if (this.isLinkingWarp) {
              const { gridX, gridY } = this.getGridFromMouse(e);
              this.isLinkingWarp = false;
              this.showWarpLinkerModal(gridX, gridY);
              this.render();
              e.preventDefault();
              return;
          }
          
          // Warp Picker Mode
          if (this.isPickingWarpTarget) {
              const { gridX, gridY } = this.getGridFromMouse(e);
              this.objectProps.targetX = gridX.toString();
              this.objectProps.targetY = gridY.toString();
              this.isPickingWarpTarget = false;
              
              // Update object properties if selected
              if (this.selectedObjectId !== null && this.game) {
                  const obj = this.game.map.getObjectById(this.selectedObjectId);
                  if (obj) {
                      this.updateObjectProperty(obj, 'targetX', gridX);
                      this.updateObjectProperty(obj, 'targetY', gridY);
                  }
              }
              
              this.render();
              e.preventDefault();
              return;
          }

          // Alt+Click = Picker Shortcut
          if (e.altKey && e.button === 0) {
              const { gridX, gridY } = this.getGridFromMouse(e);
              this.pickTile(gridX, gridY);
              e.preventDefault();
              return;
          }

          // Middle Click Panning
          if (e.button === 1) {
              this.isPanning = true;
              this.lastPanX = e.clientX;
              this.lastPanY = e.clientY;
              e.preventDefault();
              return;
          }

          this.isMouseDown = true;
          this.handleMapClick(e);
      });
      
      canvas.addEventListener('mousemove', (e) => {
          if (!this.isActive()) return;
          
          if (this.isPanning && this.game) {
              const dx = e.clientX - this.lastPanX;
              const dy = e.clientY - this.lastPanY;
              
              this.game.camera.x -= dx;
              this.game.camera.y -= dy;
              
              this.lastPanX = e.clientX;
              this.lastPanY = e.clientY;
              return;
          }

          this.updateHover(e);
          if (this.isMouseDown) {
              if (this.currentTab === 'map' || this.currentTab === 'encounters') {
                  this.handleMapClick(e);
              } else if (this.currentTab === 'objects') {
                  // Drag Logic for Triggers - DON'T place immediately, wait for mouse up
                  if (this.selectedObjectType === 'Trigger' || this.selectedObjectType === 'EncounterZone') {
                       // Just track drag start, placement happens on mouse up
                       if (this.dragStartX === -1) {
                           this.dragStartX = this.hoverGridX;
                           this.dragStartY = this.hoverGridY;
                       }
                       return; 
                  }

                  // Allow Drag-Deletion for any object if Right Click
                  const isRightClick = e.button === 2 || (e.buttons & 2) === 2;
                  if (isRightClick) {
                       this.handleMapClick(e);
                  } else {
                       // For other objects (NPCs, Items), place immediately
                       this.handleMapClick(e);
                  }
              }
          }
      });
      
      canvas.addEventListener('mouseup', (e) => {
          if (e.button === 1) {
              this.isPanning = false;
              return;
          }
          
          // Apply Rectangle Tool on Release
          if (this.currentTool === 'rect' && this.dragStartX !== -1 && (this.currentTab === 'map')) {
              this.applyRect(this.dragStartX, this.dragStartY, this.hoverGridX, this.hoverGridY);
              this.dragStartX = -1;
              this.dragStartY = -1;
          }
          else if (this.currentTab === 'objects' && (this.selectedObjectType === 'Trigger' || this.selectedObjectType === 'EncounterZone') && this.dragStartX !== -1) {
              // Calculate Rect
              const startX = Math.min(this.dragStartX, this.hoverGridX);
              const startY = Math.min(this.dragStartY, this.hoverGridY);
              const endX = Math.max(this.dragStartX, this.hoverGridX);
              const endY = Math.max(this.dragStartY, this.hoverGridY);
              
              const w = (endX - startX + 1) * TILE_SIZE;
              const h = (endY - startY + 1) * TILE_SIZE;
              
              this.placeObject(startX * TILE_SIZE, startY * TILE_SIZE, w, h);
              
              this.dragStartX = -1;
              this.dragStartY = -1;
          }
          
          
          // Commit Pencil/Eraser Stroke on Release
          if (this.pendingTileChanges.length > 0) {
              this.undoManager.record({
                  type: 'tile_place',
                  timestamp: Date.now(),
                  description: `Paint ${this.pendingTileChanges.length} tiles`,
                  data: {
                      layer: this.currentLayer,
                      tiles: [...this.pendingTileChanges]
                  }
              });
              this.pendingTileChanges = [];
              this.render(); // Ensure buttons update
          }
          
          this.isMouseDown = false;
      });
      
      canvas.addEventListener('mouseleave', () => {
          this.isMouseDown = false;
          this.isPanning = false;
          this.dragStartX = -1;
          this.dragStartY = -1;
          
          // Commit Stroke on Leave
          if (this.pendingTileChanges.length > 0) {
              this.undoManager.record({
                  type: 'tile_place',
                  timestamp: Date.now(),
                  description: `Paint ${this.pendingTileChanges.length} tiles`,
                  data: {
                      layer: this.currentLayer,
                      tiles: [...this.pendingTileChanges]
                  }
              });
              this.pendingTileChanges = [];
              this.render();
          }
      });
      canvas.addEventListener('wheel', (e) => {
          if (!this.isActive() || !this.game) return;
          
          if (e.shiftKey) {
              const delta = Math.sign(e.deltaY) * -0.1;
              const newZoom = Math.max(0.5, Math.min(4.0, this.game.zoom + delta));
              this.game.zoom = newZoom;
              e.preventDefault();
              
              // Log zoom level occasionally or update UI?
              // console.log(`[Editor] Zoom: ${newZoom.toFixed(1)}`);
          }
      });

      // Global Keydown for shortcuts
      window.addEventListener('keydown', (e) => {
          if (!this.isActive()) return;
          
          // Don't trigger if typing in an input
          if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement || e.target instanceof HTMLSelectElement) {
              return;
          }

          if (e.key.toLowerCase() === 'i') {
              this.currentTool = 'picker';
              this.render();
          }
      });
  }
  
  private isActive(): boolean {
      return this.container.style.display !== 'none';
  }
  
  private updateHover(e: MouseEvent): void {
      const { gridX, gridY } = this.getGridFromMouse(e);
      this.hoverGridX = gridX;
      this.hoverGridY = gridY;
  }

  private getGridFromMouse(e: MouseEvent): { gridX: number, gridY: number } {
      if (!this.game) return { gridX: 0, gridY: 0 };

      const rect = this.game.display.canvas.getBoundingClientRect();
      const scaleX = this.game.display.canvas.width / rect.width;
      const scaleY = this.game.display.canvas.height / rect.height;
      
      const canvasX = (e.clientX - rect.left) * scaleX;
      const canvasY = (e.clientY - rect.top) * scaleY;
      
      // Adjust for Scale (Canvas is Size * 2, but logic is Size * 1)
      const logicalScale = this.game.display.scale; // 2
      const zoom = this.game.zoom || 1;
      
      // Apply Zoom to Logical Scale
      const logicalX = (canvasX / logicalScale) / zoom;
      const logicalY = (canvasY / logicalScale) / zoom;

      // Adjust for Camera
      const worldX = logicalX + this.game.camera.x;
      const worldY = logicalY + this.game.camera.y;
      
      // Convert to Grid
      const gridX = Math.floor(worldX / TILE_SIZE); 
      const gridY = Math.floor(worldY / TILE_SIZE);
      
      return { gridX, gridY };
  }

  private handleMapClick(e: MouseEvent): void {
      const log = (msg: string) => { console.log(msg); if ((window as any).fs && (window as any).fs.log) (window as any).fs.log(msg); };

      if (!this.game) return;
      
      const { gridX, gridY } = this.getGridFromMouse(e);
      
      // Tile Picker Logic
      if (this.currentTool === 'picker') {
          this.pickTile(gridX, gridY);
          return;
      }

      const isRightClick = e.button === 2 || (e.buttons & 2) === 2;
      
      if (this.currentTab === 'map' || this.currentTab === 'encounters') {
            // Tool Logic
            if (this.currentTool === 'rect') {
                if (e.type === 'mousedown') {
                    this.dragStartX = gridX;
                    this.dragStartY = gridY;
                }
                return;
            }

            if (isRightClick) {
                // Erase Logic
                this.pendingTileChanges = [];
                const oldTileId = this.game.map.getTile(gridX, gridY, this.currentLayer);
                if (oldTileId !== 0) {
                    this.game.map.setTile(gridX, gridY, 0, this.currentLayer);
                    this.undoManager.record({
                        type: 'tile_place',
                        timestamp: Date.now(),
                        description: `Erase tile on ${this.currentLayer}`,
                        data: {
                            layer: this.currentLayer,
                            tiles: [{ x: gridX, y: gridY, before: oldTileId, after: 0 }]
                        }
                    });
                    this.render();
                }
                return;
            }

            if (this.currentTool === 'fill') {
                 // Fill uses the first tile of the selection
                 const localTileId = this.selectedTiles[0][0];
                 let gid = localTileId;
                 if (this.currentLayer !== 'Encounters' && this.currentLayer !== 'Collision') {
                    const ts = this.tilesetManager.getActiveTileset();
                    if (ts) gid = this.tilesetManager.getGlobalId(ts.id, localTileId);
                 }
                 
                 this.floodFill(gridX, gridY, gid);
                 return;
            }

            if (this.currentTool === 'pencil') {
                // Paint the entire selection
                const rows = this.selectedTiles.length;
                const cols = this.selectedTiles[0].length;
                const changes: any[] = [];
                
                for (let y = 0; y < rows; y++) {
                    for (let x = 0; x < cols; x++) {
                        const targetX = gridX + x;
                        const targetY = gridY + y;
                        const localTileId = this.selectedTiles[y][x];
                        
                        let gid = localTileId;
                        if (this.currentLayer !== 'Encounters' && this.currentLayer !== 'Collision') {
                            const ts = this.tilesetManager.getActiveTileset();
                            if (ts) gid = this.tilesetManager.getGlobalId(ts.id, localTileId);
                        }

                        const oldTileId = this.game.map.getTile(targetX, targetY, this.currentLayer);
                        if (oldTileId !== gid) {
                            this.game.map.setTile(targetX, targetY, gid, this.currentLayer);
                            changes.push({ x: targetX, y: targetY, before: oldTileId, after: gid });
                        }
                    }
                }

                if (changes.length > 0) {
                    const isSingleTile = (cols === 1 && rows === 1);
                    if (this.isMouseDown && isSingleTile) {
                        // Drag painting with single tile - group them
                        changes.forEach(c => {
                            if (!this.pendingTileChanges.some(p => p.x === c.x && p.y === c.y)) {
                                this.pendingTileChanges.push(c);
                            }
                        });
                    } else {
                        // Multi-tile stamp OR single click - record immediately
                        this.undoManager.record({
                            type: 'tile_place',
                            timestamp: Date.now(),
                            description: `Stamp ${cols}x${rows} tiles on ${this.currentLayer}`,
                            data: {
                                layer: this.currentLayer,
                                tiles: changes
                            }
                        });
                    }
                    this.render();
                }
            }
        } else if (this.currentTab === 'objects') {
           if (isRightClick) {
               // Right Click -> Delete Object
               log(`[Editor] Right-Click Delete at ${gridX},${gridY}`);
               const removed = this.game.map.removeObjectAt('*', gridX, gridY);
               this.game.map.setTile(gridX, gridY, 0, 'Encounters');
               
               if (removed) {
                   log('Object Removed');
                   this.selectedObjectId = null;
               }
           } else {
               // Left Click -> Select OR Place
               const existing = this.game.map.getObjectAt('*', gridX, gridY);
               if (existing) {
                   this.selectedObjectId = existing.id;
                   this.selectedObjectType = existing.type;
                   this.loadObjectProps(existing);
                   log(`[Editor] Selected Object ID: ${existing.id} (${existing.type})`);
                   this.render();
                   return;
               }

               // Deselect if clicking empty space? Or place?
               // Let's place a NEW one if nothing exists there.
               this.selectedObjectId = null;
               this.placeObject(gridX * TILE_SIZE, gridY * TILE_SIZE);
           }
      }
  }

  private placeObject(worldX: number, worldY: number, w?: number, h?: number): void {
      if (!this.game) return;

      // Snap to Grid (Top-Left)
      const gridX = Math.floor(worldX / TILE_SIZE) * TILE_SIZE;
      const gridY = Math.floor(worldY / TILE_SIZE) * TILE_SIZE;

      // Deduplication Check
      const layer = this.game.map['layers'].find(l => l.name === 'Objects' && l.type === 'objectgroup') as any;
      if (layer) {
          // 1. Single Spawn Point Check
          if (this.selectedObjectType === 'Spawn') {
               const existingSpawn = layer.objects.find((o: any) => o.type === 'Spawn');
               if (existingSpawn) {
                   alert("You can only have 1 Spawn Point set at a time! Delete current one to place another.");
                   return;
               }
          }
          
          // 2. Validate Encounter Zone Name
          if (this.selectedObjectType === 'EncounterZone') {
              const zoneName = this.objectProps.name;
              if (!zoneName || zoneName.trim() === '') {
                  alert("Encounter Zone must have a name (Table ID)!");
                  return;
              }
          }

          // Check if overlap existing same-type object at exact pos? 
          // For drag-areas, we might overlap significantly. 
          // Let's loosen strict overlap check or just check Top-Left for now.
          const existing = layer.objects.find((o: any) => 
              o.x === gridX && o.y === gridY && 
              o.type === this.selectedObjectType &&
              (this.selectedObjectType !== 'EncounterZone' || o.name === (this.objectProps.name || this.selectedObjectType))
          );
          if (existing && !w && !h) { // Only skip if single-tile click placement
              return;
          }
      }

      const obj: any = {
          x: gridX,
          y: gridY,
          width: w || (this.selectedObjectType === 'NPC' ? 16 : TILE_SIZE),
          height: h || (this.selectedObjectType === 'NPC' ? 16 : TILE_SIZE),
          rotation: 0,
          visible: true,
          name: this.objectProps.name || this.selectedObjectType, // Use Props Name
          type: this.selectedObjectType,
          properties: []
      };

      // Add Properties based on Type
      if (this.selectedObjectType === 'Item') {
          obj.properties.push({ name: 'itemId', type: 'string', value: this.objectProps.itemId });
          obj.properties.push({ name: 'amount', type: 'int', value: parseInt(this.objectProps.amount) });
      } else if (this.selectedObjectType === 'NPC') {
          obj.properties.push({ name: 'sprite', type: 'string', value: this.objectProps.sprite });
          obj.properties.push({ name: 'scale', type: 'float', value: parseFloat(this.objectProps.scale) || 1.0 });
          obj.properties.push({ name: 'triggerType', type: 'string', value: this.objectProps.triggerType });
          obj.properties.push({ name: 'dialog', type: 'string', value: this.objectProps.dialog });
          if (this.objectProps.triggerId) {
             obj.properties.push({ name: 'triggerId', type: 'string', value: this.objectProps.triggerId });
          }
      } else if (this.selectedObjectType === 'Warp') {
          obj.properties.push({ name: 'targetMap', type: 'string', value: this.objectProps.targetMap });
          obj.properties.push({ name: 'targetX', type: 'int', value: parseInt(this.objectProps.targetX) });
          obj.properties.push({ name: 'targetY', type: 'int', value: parseInt(this.objectProps.targetY) });
      } else if (this.selectedObjectType === 'Trigger') {
          obj.properties.push({ name: 'triggerId', type: 'string', value: this.objectProps.triggerId || 'new_trigger' });
          obj.properties.push({ name: 'triggerType', type: 'string', value: this.objectProps.triggerType || 'step' });
          obj.properties.push({ name: 'repeatable', type: 'bool', value: this.objectProps.repeatable || false });
          if (this.objectProps.targetNpcId) {
              obj.properties.push({ name: 'targetNpcId', type: 'string', value: this.objectProps.targetNpcId });
          }
      } else if (this.selectedObjectType === 'Spawn') {
      } else if (this.selectedObjectType === 'EncounterZone') {
          // Map Name to tableId
          obj.properties.push({ name: 'tableId', type: 'string', value: obj.name });
      }

      this.game.map.addObject('Objects', obj);

      // SYNC: Refresh Game NPC list so it renders immediately
      if (this.selectedObjectType === 'NPC') {
          this.game.refreshNPCs();
      }

      console.log(`Placed ${this.selectedObjectType} at ${gridX},${gridY}`);
  }
  
  
  public async onOpen(): Promise<void> {
      console.log('[Editor] Opening editor...');
      await this.loadEditorConfig();
      console.log(`[Editor] Home Map Path: ${this.homeMapPath}`);
      
      // Auto-load home map if nothing is currently active in Game
      if (this.homeMapPath && this.game) {
          const currentMap = this.game.currentLevelPath;
          console.log(`[Editor] Current Game Map: ${currentMap}`);
          
          if (this.homeMapPath && currentMap !== this.homeMapPath) {
              console.log(`[Editor] Automatically loading home map: ${this.homeMapPath}`);
              this.game.loadLevel(this.homeMapPath);
          }
      }
      
      this.refreshMapList();
  }

  private async loadEditorConfig(): Promise<void> {
      const res = await (window as any).fs.readEditorConfig();
      if (res.success && res.config) {
          this.homeMapPath = res.config.homeMapPath || null;
      }
      
      // Load Scripts for Dropdown
      const scriptRes = await (window as any).fs.readScripts();
      if (scriptRes.success) {
          try {
              const scripts = JSON.parse(scriptRes.data);
              this.availableScripts = Object.keys(scripts);
              console.log(`[Editor] Loaded ${this.availableScripts.length} scripts.`);
          } catch (e) {
              console.error('[Editor] Failed to parse scripts.json');
          }
      }
  }

  private async updateSpritePreview(path: string): Promise<void> {
      const container = document.getElementById('npc-sprite-preview');
      if (!container || !path) return;
      
      try {
          container.innerHTML = '<span style="color:#888; font-size:10px;">Loading...</span>';
          const res = await (window as any).fs.readImage(path);
          if (res.success) {
              container.innerHTML = `<img src="data:image/png;base64,${res.data}" style="max-width:100%; max-height:100%; object-fit: contain; image-rendering: pixelated;">`;
          } else {
              container.innerHTML = '<span style="color:red; font-size:10px;">Err</span>';
          }
      } catch (e) {
          console.error(e);
          container.innerHTML = '<span style="color:red; font-size:10px;">Err</span>';
      }
  }

  private async saveEditorConfig(): Promise<void> {
      const config = { homeMapPath: this.homeMapPath };
      await (window as any).fs.writeEditorConfig(config);
  }

  private async setHomeMap(path: string): Promise<void> {
      console.log(`[Editor] Setting home map to: ${path}`);
      this.homeMapPath = path;
      await this.saveEditorConfig();
      this.render();
  }
  
  public onClose(): void {
      console.log('Editor Closed');
  }

  private async showResizeDialog(): Promise<void> {
      if (!this.game || !this.game.map) return;

      const currentW = this.game.map.width;
      const currentH = this.game.map.height;

      // Create modal dialog
      const modal = document.createElement('div');
      modal.style.cssText = `
          position: fixed;
          top: 0;
          left: 0;
          width: 100%;
          height: 100%;
          background: rgba(0,0,0,0.8);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 10000;
      `;
      
      modal.innerHTML = `
          <div style="background:#2a2a2a; padding:20px; border-radius:8px; min-width:300px; color:white; font-family: sans-serif;">
              <h3 style="margin-top:0; border-bottom:1px solid #444; padding-bottom:10px;">Resize Map</h3>
              <p style="font-size:12px; color:#aaa;">Changes map dimensions in tiles. Existing tiles will be preserved.</p>
              
              <div style="margin-bottom:15px; display:flex; gap:10px;">
                  <div style="flex:1;">
                      <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Width (Tiles)</label>
                      <input type="number" id="map-width-input" value="${currentW}" min="10" max="1000" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
                  </div>
                  <div style="flex:1;">
                      <label style="display:block; margin-bottom:5px; font-size:12px; color:#aaa;">Height (Tiles)</label>
                      <input type="number" id="map-height-input" value="${currentH}" min="10" max="1000" style="width:100%; padding:8px; background:#333; border:1px solid #555; color:white; box-sizing:border-box; border-radius:4px;">
                  </div>
              </div>
              
              <div style="text-align:center; margin-bottom:20px; font-size:12px; color:#888;">
                  Pixel Size: <span id="map-pixel-size">${currentW * TILE_SIZE}x${currentH * TILE_SIZE}</span> px
              </div>
              
              <div style="display:flex; gap:10px; justify-content:flex-end; padding-top:10px; border-top:1px solid #444;">
                  <button id="resize-cancel-btn" style="padding:8px 16px; background:#555; border:none; color:white; cursor:pointer; border-radius:4px;">Cancel</button>
                  <button id="resize-confirm-btn" style="padding:8px 16px; background:#2196F3; border:none; color:white; cursor:pointer; border-radius:4px; font-weight:bold;">Resize</button>
              </div>
          </div>
      `;
      
      document.body.appendChild(modal);
      
      // Update pixel size text on input
      const updatePixelText = () => {
          const w = parseInt((document.getElementById('map-width-input') as HTMLInputElement).value) || 0;
          const h = parseInt((document.getElementById('map-height-input') as HTMLInputElement).value) || 0;
          const span = document.getElementById('map-pixel-size');
          if (span) span.innerText = `${w * TILE_SIZE}x${h * TILE_SIZE}`;
      };
      document.getElementById('map-width-input')?.addEventListener('input', updatePixelText);
      document.getElementById('map-height-input')?.addEventListener('input', updatePixelText);
      
      // Handle cancel
      document.getElementById('resize-cancel-btn')?.addEventListener('click', () => {
          document.body.removeChild(modal);
      });
      
      // Handle confirm
      document.getElementById('resize-confirm-btn')?.addEventListener('click', () => {
          const wStr = (document.getElementById('map-width-input') as HTMLInputElement).value;
          const hStr = (document.getElementById('map-height-input') as HTMLInputElement).value;
          
          const newW = parseInt(wStr);
          const newH = parseInt(hStr);
          
          if (isNaN(newW) || isNaN(newH) || newW < 1 || newH < 1) {
              alert('Invalid dimensions!');
              return;
          }
          
          // Perform Resize
          this.game!.map.resize(newW, newH);
          this.game!.camera.setMapSize(newW * TILE_SIZE, newH * TILE_SIZE);
          
          // Auto-Save to persist changes
          this.saveMap().then(() => {
             alert(`Map resized to ${newW}x${newH} and saved automatically.`);
          });
          
          document.body.removeChild(modal);
          alert(`Map resized to ${newW}x${newH} tiles.`);
      });
  }

  /**
   * Perform undo operation
   */
  private performUndo(): void {
    const action = this.undoManager.undo();
    if (!action || !this.game) return;
    
    switch (action.type) {
      case 'tile_place':
      case 'tile_fill':
      case 'tile_rect':
        // Restore all tiles
        if (action.data.tiles) {
          action.data.tiles.forEach((change: any) => {
            this.game!.map.setTile(change.x, change.y, change.before, action.data.layer!);
          });
        }
        break;
      
      case 'object_place':
        // Remove the placed object
        if (action.data.objects && action.data.objects.length > 0) {
          const obj = action.data.objects[0];
          this.game!.map.removeObjectAt('Objects', Math.floor(obj.x / 16), Math.floor(obj.y / 16));
        }
        break;
      
      case 'object_delete':
        // Restore the deleted object
        if (action.data.objects && action.data.objects.length > 0) {
          const obj = action.data.objects[0];
          this.game!.map.addObject('Objects', obj);
        }
        break;
    }
    
    this.render();
  }
  
  /**
   * Perform redo operation
   */
  private performRedo(): void {
    const action = this.undoManager.redo();
    if (!action || !this.game) return;
    
    switch (action.type) {
      case 'tile_place':
      case 'tile_fill':
      case 'tile_rect':
        // Re-apply all tiles
        if (action.data.tiles) {
          action.data.tiles.forEach((change: any) => {
            this.game!.map.setTile(change.x, change.y, change.after, action.data.layer!);
          });
        }
        break;
      
      case 'object_place':
        // Re-place the object
        if (action.data.objects && action.data.objects.length > 0) {
          const obj = action.data.objects[0];
          this.game!.map.addObject('Objects', obj);
        }
        break;
      
      case 'object_delete':
        // Re-delete the object
        if (action.data.objects && action.data.objects.length > 0) {
          const obj = action.data.objects[0];
          this.game!.map.removeObjectAt('Objects', Math.floor(obj.x / 16), Math.floor(obj.y / 16));
        }
        break;
    }
    
    this.render();
  }

  private pickTile(gridX: number, gridY: number): void {
      if (!this.game) return;
      
      const tileId = this.game.map.getTile(gridX, gridY, this.currentLayer);
      
      if (tileId > 0) {
          // If we are not on the Encounters layer, we need to resolve the GID
          if (this.currentLayer !== 'Encounters') {
              const result = this.tilesetManager.resolveGid(tileId);
              if (result) {
                  this.tilesetManager.setActiveTileset(result.tilesetId);
                  this.selectedTiles = [[result.localId]];
                  this.selectedTileId = result.localId;
                  this.selectionRect = { startX: 0, startY: 0, width: 1, height: 1 };
                  
                  console.log(`[Editor] Picked tile from ${result.tilesetId}, local ID: ${result.localId}`);
              } else {
                  // Fallback if GID resolution fails
                  this.selectedTiles = [[tileId]];
                  this.selectedTileId = tileId;
              }
          } else {
              // Encounters layer uses raw IDs
              this.selectedTiles = [[tileId]];
              this.selectedTileId = tileId;
          }

          this.currentTool = 'pencil';
          this.render();
      }
  }

  private updatePaletteSelection(x1: number, y1: number, x2: number, y2: number): void {
      const tileset = this.tilesetManager.getActiveTileset();
      if (!tileset) return;
      
      const minX = Math.max(0, Math.min(x1, x2, tileset.columns - 1));
      const minY = Math.max(0, Math.min(y1, y2, tileset.rows - 1));
      const maxX = Math.max(0, Math.min(x1, x2, tileset.columns - 1, Math.max(x1, x2)));
      const maxY = Math.max(0, Math.min(y1, y2, tileset.rows - 1, Math.max(y1, y2)));
      
      // We need to re-calc min/max properly
      const realMinX = Math.min(x1, x2);
      const realMaxX = Math.min(tileset.columns - 1, Math.max(x1, x2));
      const realMinY = Math.min(y1, y2);
      const realMaxY = Math.min(tileset.rows - 1, Math.max(y1, y2));

      this.selectionRect = {
          startX: realMinX,
          startY: realMinY,
          width: realMaxX - realMinX + 1,
          height: realMaxY - realMinY + 1
      };

      // Update UI highlight
      const highlight = document.getElementById('palette-highlight');
      if (highlight) {
          const displaySize = 16 * this.paletteZoom;
          highlight.style.display = 'block';
          highlight.style.left = `${this.selectionRect.startX * displaySize}px`;
          highlight.style.top = `${this.selectionRect.startY * displaySize}px`;
          highlight.style.width = `${this.selectionRect.width * displaySize}px`;
          highlight.style.height = `${this.selectionRect.height * displaySize}px`;
      }
  }

  private finalizePaletteSelection(): void {
      const tileset = this.tilesetManager.getActiveTileset();
      if (!tileset || !this.selectionRect) return;
      
      const { startX, startY, width, height } = this.selectionRect;
      this.selectedTiles = [];
      
      for (let y = 0; y < height; y++) {
          const row: number[] = [];
          for (let x = 0; x < width; x++) {
              const tileX = startX + x;
              const tileY = startY + y;
              const id = tileY * tileset.columns + tileX;
              row.push(id);
          }
          this.selectedTiles.push(row);
      }
      
      // Update legacy ID for single-tile tools if needed
      if (width === 1 && height === 1) {
          this.selectedTileId = this.selectedTiles[0][0];
      }
      
      console.log(`[Editor] Selected ${width}x${height} tiles`);
      this.render(); // Re-render to update metadata text etc
  }

  private syncTeamToObject(): void {
      if (this.selectedObjectId !== null && this.game) {
          const obj = this.game.map.getObjectById(this.selectedObjectId);
          if (obj) {
              if (!obj.properties) obj.properties = [];
              const teamProp = obj.properties.find(p => p.name === 'trainerTeam');
              if (teamProp) {
                  teamProp.value = JSON.stringify(this.objectProps.trainerTeam);
              } else {
                  obj.properties.push({ 
                      name: 'trainerTeam', 
                      type: 'string', // Storing as JSON string in properties
                      value: JSON.stringify(this.objectProps.trainerTeam) 
                  });
              }
          }
      }
  }

  private loadObjectProps(obj: any): void {
      // Sync properties to the form state
      this.objectProps.name = obj.name || '';
      
      // Load custom properties
      if (obj.properties) {
          obj.properties.forEach((p: any) => {
              if (p.name === 'trainerTeam') {
                  try {
                      this.objectProps.trainerTeam = JSON.parse(p.value);
                  } catch (e) {
                      this.objectProps.trainerTeam = [];
                  }
              } else {
                  this.objectProps[p.name] = p.value;
              }
          });
      }
  }

  private showWarpDestinationPicker(targetMapPath: string): void {
      console.log(`[Editor] Opening Warp Picker for: ${targetMapPath}`);
      
      // 1. Create Modal and Canvas (Styles could be moved to CSS, inline for speed)
      const overlay = document.createElement('div');
      overlay.id = 'warp-picker-overlay';
      overlay.style.position = 'fixed';
      overlay.style.top = '0';
      overlay.style.left = '0';
      overlay.style.width = '100vw';
      overlay.style.height = '100vh';
      overlay.style.backgroundColor = 'rgba(0,0,0,0.85)';
      overlay.style.zIndex = '2000';
      overlay.style.display = 'flex';
      overlay.style.justifyContent = 'center';
      overlay.style.alignItems = 'center';
      overlay.style.flexDirection = 'column';
      
      const instructions = document.createElement('div');
      instructions.innerHTML = '<h2 style="color:white; margin:0 0 10px 0;">Pick Destination</h2><p style="color:#ccc; margin:0 0 10px 0;">Click on a tile to set Warp Target.</p>';
      overlay.appendChild(instructions);

      const canvasContainer = document.createElement('div');
      canvasContainer.style.border = '2px solid white';
      canvasContainer.style.overflow = 'auto'; 
      canvasContainer.style.maxWidth = '90vw';
      canvasContainer.style.maxHeight = '80vh';
      canvasContainer.style.position = 'relative';

      const canvas = document.createElement('canvas');
      canvas.id = 'warp-picker-canvas';
      canvas.width = 800; // Will resize
      canvas.height = 600;
      
      canvasContainer.appendChild(canvas);
      overlay.appendChild(canvasContainer);
      
      const closeBtn = document.createElement('button');
      closeBtn.innerText = 'Cancel';
      closeBtn.style.marginTop = '10px';
      closeBtn.style.padding = '8px 16px';
      closeBtn.style.cursor = 'pointer';
      closeBtn.onclick = () => {
          document.body.removeChild(overlay);
      };
      overlay.appendChild(closeBtn);
      
      document.body.appendChild(overlay);

      // 2. Load Target Map
      (window as any).fs.readFile(targetMapPath).then(async (res: any) => {
          if (res.success) {
              try {
                  const mapData = JSON.parse(res.data);
                  const tempMap = new Tilemap(); // Standalone instance
                  
                  // Clean up path relative to project for tileset resolution?
                  // Tilemap logic uses globally loaded tilesets via manager usually.
                  // We inject the current manager.
                  tempMap.tilesetManager = this.tilesetManager;
                  tempMap.loadFromTiled(mapData);
                  
                  // Resize Canvas
                  canvas.width = tempMap.getPixelWidth();
                  canvas.height = tempMap.getPixelHeight();
                  
                  // Render once
                  const ctx = canvas.getContext('2d');
                  if (ctx) {
                      // Mock Display - Tilemap.render expects { ctx, width, height }
                      const mockDisplay = { ctx: ctx, width: canvas.width, height: canvas.height } as any;
                      
                      // Clear and Render
                      ctx.fillStyle = '#222';
                      ctx.fillRect(0, 0, canvas.width, canvas.height);
                      
                      tempMap.render(mockDisplay, {x:0, y:0} as any, 1);
                      
                      // Draw Grid Overlay
                      ctx.strokeStyle = 'rgba(255,255,255,0.3)';
                      ctx.lineWidth = 1;
                      for(let x=0; x<=tempMap.width; x++) {
                          ctx.beginPath(); ctx.moveTo(x*16, 0); ctx.lineTo(x*16, tempMap.height*16); ctx.stroke();
                      }
                      for(let y=0; y<=tempMap.height; y++) {
                           ctx.beginPath(); ctx.moveTo(0, y*16); ctx.lineTo(tempMap.width*16, y*16); ctx.stroke();
                      }
                  }

                  // 3. Interaction
                  canvas.onclick = (e) => {
                      const rect = canvas.getBoundingClientRect();
                      const clickX = e.clientX - rect.left; 
                      const clickY = e.clientY - rect.top;
                      
                      const gx = Math.floor(clickX / 16);
                      const gy = Math.floor(clickY / 16);
                      
                      if (gx >= 0 && gx < tempMap.width && gy >= 0 && gy < tempMap.height) {
                           console.log(`[Editor] Picked Warp Target: ${gx},${gy}`);
                           
                           this.objectProps.targetX = gx;
                           this.objectProps.targetY = gy;
                           
                           // Update Selected Object
                           if (this.selectedObjectId !== null && this.game) {
                               const obj = this.game.map.getObjectById(this.selectedObjectId);
                               if (obj) {
                                   this.updateObjectProperty(obj, 'targetX', gx);
                                   this.updateObjectProperty(obj, 'targetY', gy);
                               }
                           }
                           
                           document.body.removeChild(overlay);
                           this.render(); // Update Form UI
                      }
                  };
                  
              } catch (err) {
                  console.error('Error parsing target map:', err);
                  alert('Failed to load target map preview.');
                  if(document.body.contains(overlay)) document.body.removeChild(overlay);
              }
          } else {
              alert('Could not read target map file.');
              if(document.body.contains(overlay)) document.body.removeChild(overlay);
          }
      });
  }

  private showWarpLinkerModal(startX: number, startY: number): void {
      console.log(`[Editor] Warp Linker Started. Entry: ${startX},${startY}`);
      
      const overlay = document.createElement('div');
      overlay.style.cssText = 'position:fixed; top:0; left:0; width:100vw; height:100vh; background:rgba(0,0,0,0.9); z-index:2000; display:flex; flex-direction:column; align-items:center; justify-content:center; color:white; font-family:sans-serif;';
      
      let selectedTargetMap = '';
      let selectedTargetX = -1;
      let selectedTargetY = -1;

      overlay.innerHTML = `
        <div style="background:#222; padding:20px; border:1px solid #444; border-radius:8px; max-width:90vw; max-height:90vh; display:flex; flex-direction:column; gap:10px;">
            <h3>Create Bi-Directional Warp</h3>
            <div style="display:flex; gap:10px; align-items:center;">
                <label>Target Map:</label>
                <select id="wl-target-map" style="padding:5px; background:#333; color:white; border:1px solid #555;">
                    <option value="">-- Select Map --</option>
                    ${this.availableMaps.map(m => `<option value="${m}">${m.replace(/^maps\//, '')}</option>`).join('')}
                </select>
            </div>
            <div id="wl-canvas-container" style="border:2px solid #555; overflow:auto; width:800px; height:500px; position:relative; background:#111;">
                <p style="text-align:center; margin-top:200px; color:#666;">Select a map to preview</p>
                <canvas id="wl-canvas" style="display:none;"></canvas>
            </div>
            <div style="display:flex; justify-content:space-between; align-items:center;">
                <div id="wl-status" style="color:#FF9800; font-size:12px;">Step 1: Select Target Map</div>
                <div style="display:flex; gap:10px;">
                    <button id="wl-cancel" style="padding:8px 16px; background:#555; border:none; color:white; cursor:pointer;">Cancel</button>
                    <button id="wl-confirm" style="padding:8px 16px; background:#2E7D32; border:none; color:white; cursor:pointer; opacity:0.5; pointer-events:none;">🔗 Create Link</button>
                </div>
            </div>
        </div>
      `;
      document.body.appendChild(overlay);
      
      const mapSelect = document.getElementById('wl-target-map') as HTMLSelectElement;
      const canvas = document.getElementById('wl-canvas') as HTMLCanvasElement;
      const statusDiv = document.getElementById('wl-status') as HTMLElement;
      const confirmBtn = document.getElementById('wl-confirm') as HTMLButtonElement;
      const cancelBtn = document.getElementById('wl-cancel') as HTMLButtonElement;
      const container = document.getElementById('wl-canvas-container') as HTMLElement;

      let tempMap: Tilemap | null = null;

      // Handle Map Selection
      mapSelect.onchange = () => {
          selectedTargetMap = mapSelect.value;
          if (selectedTargetMap) {
               statusDiv.innerText = 'Step 2: Click tile to set Exit point';
               statusDiv.style.color = '#FF9800';
               selectedTargetX = -1;
               selectedTargetY = -1;
               confirmBtn.style.opacity = '0.5';
               confirmBtn.style.pointerEvents = 'none';
               
               // Load Map
               (window as any).fs.readFile(selectedTargetMap).then((res: any) => {
                   if (res.success) {
                       const data = JSON.parse(res.data);
                       tempMap = new Tilemap();
                       tempMap.tilesetManager = this.tilesetManager;
                       tempMap.loadFromTiled(data);
                       
                       canvas.width = tempMap.getPixelWidth();
                       canvas.height = tempMap.getPixelHeight();
                       canvas.style.display = 'block';
                       // Remove placeholder text
                       const p = container.querySelector('p');
                       if(p) p.style.display = 'none';

                       renderPreview();
                   }
               });
          }
      };

      // Render Loop (On Demand)
      const renderPreview = () => {
          if (!tempMap) return;
          const ctx = canvas.getContext('2d');
          if (ctx) {
               ctx.fillStyle = '#111';
               ctx.fillRect(0, 0, canvas.width, canvas.height);
               
               // Tilemap
               const mockDisplay = { ctx: ctx, width: canvas.width, height: canvas.height } as any;
               tempMap.render(mockDisplay, {x:0, y:0} as any, 1);
               
               // Grid
               ctx.strokeStyle = 'rgba(255,255,255,0.2)';
               ctx.lineWidth = 1;
               for(let x=0; x<=tempMap.width; x++) {
                   ctx.beginPath(); ctx.moveTo(x*16, 0); ctx.lineTo(x*16, tempMap.height*16); ctx.stroke();
               }
               for(let y=0; y<=tempMap.height; y++) {
                   ctx.beginPath(); ctx.moveTo(0, y*16); ctx.lineTo(tempMap.width*16, y*16); ctx.stroke();
               }
               
               // Selection Marker
               if (selectedTargetX >= 0) {
                   ctx.fillStyle = 'rgba(0, 255, 0, 0.5)';
                   ctx.fillRect(selectedTargetX * 16, selectedTargetY * 16, 16, 16);
                   ctx.strokeStyle = '#0f0';
                   ctx.lineWidth = 2;
                   ctx.strokeRect(selectedTargetX * 16, selectedTargetY * 16, 16, 16);
               }
          }
      };
      
      // Click Handler
      canvas.onclick = (e) => {
          if (!tempMap) return;
          const rect = canvas.getBoundingClientRect();
          const gx = Math.floor((e.clientX - rect.left) / 16);
          const gy = Math.floor((e.clientY - rect.top) / 16);
          
          if (gx >= 0 && gx < tempMap.width && gy >= 0 && gy < tempMap.height) {
              selectedTargetX = gx;
              selectedTargetY = gy;
              renderPreview();
              
              statusDiv.innerText = `Selected Exit: ${gx},${gy}. Ready to Link!`;
              statusDiv.style.color = '#4CAF50';
              confirmBtn.style.opacity = '1';
              confirmBtn.style.pointerEvents = 'auto';
          }
      };
      
      // Save Handler
      confirmBtn.onclick = async () => {
          confirmBtn.disabled = true;
          confirmBtn.innerText = 'Creating Link...';
          
          try {
              // 1. Create Warp on Current Map
              const warp1 = {
                  id: Date.now(),
                  name: `WarpTo_${selectedTargetMap.split('/').pop()}`,
                  type: 'Warp',
                  x: startX * 16,
                  y: startY * 16,
                  width: 16,
                  height: 16,
                  rotation: 0,
                  visible: true,
                  properties: [
                      { name: 'targetMap', type: 'string', value: selectedTargetMap },
                      { name: 'targetX', type: 'int', value: selectedTargetX },
                      { name: 'targetY', type: 'int', value: selectedTargetY }
                  ]
              };
              
              if (this.game) {
                   this.game.map.addObject('Objects', warp1);
                   await this.saveMap(); // Saves Current Map
              }

              // 2. Create Warp on Target Map
              if (selectedTargetMap === this.currentProjectPath + '/' + this.game?.map.path.split('/').pop()) {
                  // Self-link (Same map) - Already added above?
                  // No, we need a return warp at the target location.
                  const warp2 = {
                      id: Date.now() + 1,
                      name: `WarpReturn`,
                      type: 'Warp',
                      x: selectedTargetX * 16,
                      y: selectedTargetY * 16,
                      width: 16,
                      height: 16,
                      rotation: 0,
                      visible: true,
                      properties: [
                          { name: 'targetMap', type: 'string', value: this.game?.map.path || '' },
                          { name: 'targetX', type: 'int', value: startX },
                          { name: 'targetY', type: 'int', value: startY }
                      ]
                  };
                   this.game?.map.addObject('Objects', warp2);
                   await this.saveMap(); // Save again
              } else {
                  // Load Target File, Modify, Save
                  const res = await (window as any).fs.readFile(selectedTargetMap);
                  if (res.success) {
                       const targetData = JSON.parse(res.data);
                       
                       // Find Object Layer
                       let objLayer = targetData.layers.find((l:any) => l.name === 'Objects' && l.type === 'objectgroup');
                       if (!objLayer) {
                           objLayer = {
                               id: Date.now(),
                               name: 'Objects',
                               type: 'objectgroup',
                               visible: true,
                               opacity: 1,
                               x: 0, y: 0,
                               objects: []
                           };
                           targetData.layers.push(objLayer);
                       }
                       
                       // Add Warp
                       const maxId = objLayer.objects.reduce((max: number, o: any) => o.id > max ? o.id : max, 0);
                       const currentMapPath = this.game?.map.path || '';
                       
                       objLayer.objects.push({
                          id: maxId + 1,
                          name: `WarpFrom_${currentMapPath.split('/').pop()}`,
                          type: 'Warp',
                          x: selectedTargetX * 16,
                          y: selectedTargetY * 16,
                          width: 16,
                          height: 16,
                          rotation: 0,
                          visible: true,
                          properties: [
                              { name: 'targetMap', type: 'string', value: currentMapPath },
                              { name: 'targetX', type: 'int', value: startX },
                              { name: 'targetY', type: 'int', value: startY }
                          ]
                       });
                       
                       // Save File
                       await (window as any).fs.writeFile(selectedTargetMap, JSON.stringify(targetData, null, 2));
                  }
              }
              
              alert('Warp Connection Created Successfully!');
              document.body.removeChild(overlay);
              this.render();
          } catch (err: any) {
              console.error('Failed to create link:', err);
              alert('Error creating link: ' + err.message);
              confirmBtn.disabled = false;
              confirmBtn.innerText = '🔗 Create Link';
          }
      };

      cancelBtn.onclick = () => document.body.removeChild(overlay);
  }

  // --- SCRIPT BUILDER IMPLEMENTATION ---

  public async openScriptBuilder(scriptId: string): Promise<void> {
      this.isScriptBuilderOpen = true;
      this.currentEditingScriptId = scriptId;
      this.currentScriptActions = [];

      console.log(`[Editor] Opening Script Builder for ${scriptId}`);

      // Load existing script if any
      const res = await (window as any).fs.readFile('data/db/scripts.json');
      if (res.success) {
          try {
              const allScripts = JSON.parse(res.data);
              if (allScripts[scriptId]) {
                  this.currentScriptActions = allScripts[scriptId];
                  console.log('[Editor] Loaded existing actions:', this.currentScriptActions);
              }
          } catch (e) {
              console.error('[Editor] Error parsing scripts.json', e);
          }
      }

      this.renderScriptBuilderOverlay();
  }

  private renderScriptBuilderOverlay(): void {
      // Remove existing
      const existing = document.getElementById('script-builder-overlay');
      if (existing) existing.remove();

      const overlay = document.createElement('div');
      overlay.id = 'script-builder-overlay';
      overlay.style.position = 'fixed';
      overlay.style.top = '0';
      overlay.style.left = '0';
      overlay.style.width = '100%';
      overlay.style.height = '100%';
      overlay.style.backgroundColor = 'rgba(0,0,0,0.85)';
      overlay.style.zIndex = '2000';
      overlay.style.display = 'flex';
      overlay.style.flexDirection = 'column';
      overlay.style.alignItems = 'center';
      overlay.style.padding = '20px';

      const content = `
        <div style="background:#222; width:700px; height:80%; border:1px solid #444; border-radius:5px; display:flex; flex-direction:column; overflow:hidden;">
            <div style="background:#333; padding:10px; border-bottom:1px solid #444; display:flex; justify-content:space-between; align-items:center;">
                <h3 style="margin:0; color:#fff;">Event Script Builder</h3>
                <span style="color:#888;">ID: ${this.currentEditingScriptId}</span>
            </div>
            
            <div id="sb-actions-list" style="flex-grow:1; overflow-y:auto; padding:10px; display:flex; flex-direction:column; gap:8px;">
                <!-- Actions rendered here -->
            </div>

            <div style="background:#2a2a2a; padding:10px; border-top:1px solid #444; display:flex; gap:5px; flex-wrap:wrap;">
                <button id="sb-add-text" style="padding:5px 10px; cursor:pointer;">+ Text</button>
                <button id="sb-add-face" style="padding:5px 10px; cursor:pointer;">+ Face</button>
                <button id="sb-add-move" style="padding:5px 10px; cursor:pointer; background:#555; color:white;">+ Move NPC</button>
                <button id="sb-add-wait" style="padding:5px 10px; cursor:pointer; background:#555; color:white;">+ Wait</button>
                <button id="sb-add-battle" style="padding:5px 10px; cursor:pointer; background:#8b0000; color:white;">+ Battle</button>
                <button id="sb-add-emote" style="padding:5px 10px; cursor:pointer;">+ Emote</button>
                <button id="sb-add-give" style="padding:5px 10px; cursor:pointer;">+ Give Item</button>
                <button id="sb-add-heal" style="padding:5px 10px; cursor:pointer;">+ Heal</button>
            </div>

            <div style="background:#333; padding:10px; border-top:1px solid #444; display:flex; justify-content:flex-end; gap:10px;">
                <button id="sb-cancel" style="padding:8px 15px; background:#666; color:white; border:none; border-radius:3px; cursor:pointer;">Cancel</button>
                <button id="sb-save" style="padding:8px 15px; background:#4CAF50; color:white; border:none; border-radius:3px; cursor:pointer;">Save Script</button>
            </div>
        </div>
      `;

      overlay.innerHTML = content;
      document.body.appendChild(overlay);

      this.renderScriptActions();
      this.attachScriptBuilderListeners(overlay);
  }

  private renderScriptActions(): void {
      const container = document.getElementById('sb-actions-list');
      if (!container) return;
      container.innerHTML = '';

      if (this.currentScriptActions.length === 0) {
          container.innerHTML = '<div style="color:#666; text-align:center; padding:20px;">No actions yet. Add one below!</div>';
          return;
      }

      this.currentScriptActions.forEach((action, index) => {
          const item = document.createElement('div');
          item.style.background = '#444';
          item.style.padding = '8px';
          item.style.borderRadius = '3px';
          item.style.display = 'flex';
          item.style.gap = '10px';
          item.style.alignItems = 'center';

          // Type Badge (Colorized)
          let color = '#666';
          if (action.type === 'battle') color = '#8b0000';
          if (action.type === 'npcWalk') color = '#555';
          if (action.type === 'wait') color = '#4466aa';
          
          item.innerHTML = `<span style="background:${color}; font-size:10px; padding:2px 4px; border-radius:2px; min-width:40px; text-align:center; color:white;">${action.type.toUpperCase()}</span>`;

          // Inputs based on type
          if (action.type === 'dialog') {
             item.innerHTML += `<input type="text" class="sb-input" data-idx="${index}" data-field="text" value="${action.text || ''}" placeholder="Message..." style="flex-grow:1;">`;
          }
          else if (action.type === 'npcAction') {
             item.innerHTML += `
                <select class="sb-input" data-idx="${index}" data-field="action">
                    <option value="face" ${action.action==='face'?'selected':''}>Face</option>
                    <option value="emote" ${action.action==='emote'?'selected':''}>Emote</option>
                </select>
                ${action.action === 'face' ? 
                  `<select class="sb-input" data-idx="${index}" data-field="direction">
                      <option value="0" ${action.direction==0?'selected':''}>Down</option>
                      <option value="1" ${action.direction==1?'selected':''}>Left</option>
                      <option value="2" ${action.direction==2?'selected':''}>Right</option>
                      <option value="3" ${action.direction==3?'selected':''}>Up</option>
                   </select>` : ''}
                <input type="text" class="sb-input" data-idx="${index}" data-field="targetId" value="${action.targetId || 'Player'}" placeholder="Target ID" style="width:80px;">
             `;
          }
          else if (action.type === 'npcWalk') {
             item.innerHTML += `
                <input type="text" class="sb-input" data-idx="${index}" data-field="targetId" value="${action.targetId || 'this'}" placeholder="NPC ID" style="width:60px;">
                <span style="font-size:10px; color:#aaa;">X:</span>
                <input type="number" class="sb-input" data-idx="${index}" data-field="x" value="${action.x}" style="width:40px;">
                <span style="font-size:10px; color:#aaa;">Y:</span>
                <input type="number" class="sb-input" data-idx="${index}" data-field="y" value="${action.y}" style="width:40px;">
                <label style="font-size:11px; display:flex; align-items:center; gap:4px; color:#ccc;">
                    <input type="checkbox" class="sb-chk-input" data-idx="${index}" data-field="wait" ${action.wait !== false ? 'checked' : ''}> Wait
                </label>
             `;
          }
          else if (action.type === 'wait') {
             item.innerHTML += `
                <input type="number" class="sb-input" data-idx="${index}" data-field="ms" value="${action.ms || 1000}" style="width:60px;"> ms
             `;
          }
          else if (action.type === 'battle') {
             item.innerHTML += `
                <input type="text" class="sb-input" data-idx="${index}" data-field="enemyId" value="${action.enemyId || ''}" placeholder="Enemy ID / Zone" style="flex-grow:1;">
             `;
          }
           else if (action.type === 'giveItem') {
             item.innerHTML += `
               <input type="text" class="sb-input" data-idx="${index}" data-field="itemId" value="${action.itemId || ''}" placeholder="Item ID" style="width:80px;">
               <input type="number" class="sb-input" data-idx="${index}" data-field="amount" value="${action.amount || 1}" style="width:50px;">
             `;
          }

          // Delete Button
          const delBtn = document.createElement('button');
          delBtn.innerText = 'X';
          delBtn.style.color = '#ff6666';
          delBtn.style.background = 'none';
          delBtn.style.border = 'none';
          delBtn.style.cursor = 'pointer';
          delBtn.onclick = () => {
              this.currentScriptActions.splice(index, 1);
              this.renderScriptActions();
          };
          item.appendChild(delBtn);

          container.appendChild(item);
      });

      // Bind Inputs
      container.querySelectorAll('.sb-input').forEach((input: any) => {
          input.oninput = (e: any) => {
              const idx = parseInt(e.target.dataset.idx);
              const field = e.target.dataset.field;
              this.currentScriptActions[idx][field] = e.target.value;
              if (field === 'action') this.renderScriptActions();
          };
      });
      // Bind Checkboxes
      container.querySelectorAll('.sb-chk-input').forEach((input: any) => {
          input.onchange = (e: any) => {
              const idx = parseInt(e.target.dataset.idx);
              const field = e.target.dataset.field;
              this.currentScriptActions[idx][field] = e.target.checked;
          };
      });
  }

  private attachScriptBuilderListeners(overlay: HTMLElement): void {
      overlay.querySelector('#sb-cancel')?.addEventListener('click', () => {
          overlay.remove();
          this.isScriptBuilderOpen = false;
      });

      overlay.querySelector('#sb-save')?.addEventListener('click', async () => {
           // Save to scripts.json
           const res = await (window as any).fs.readFile('data/db/scripts.json');
           let scripts = {};
           if (res.success) {
               try { scripts = JSON.parse(res.data); } catch {}
           }
           scripts[this.currentEditingScriptId] = this.currentScriptActions;
           
           await (window as any).fs.writeFile('data/db/scripts.json', JSON.stringify(scripts, null, 2));
           console.log(`[Editor] Saved script ${this.currentEditingScriptId}`);
           
           // Update List
           this.refreshScriptList();

           // Close
           overlay.remove();
           this.isScriptBuilderOpen = false;
      });

      // Add Buttons
      const addAction = (action: any) => {
          this.currentScriptActions.push(action);
          this.renderScriptActions();
      };

      overlay.querySelector('#sb-add-text')?.addEventListener('click', () => addAction({ type: 'dialog', text: '' }));
      overlay.querySelector('#sb-add-face')?.addEventListener('click', () => addAction({ type: 'npcAction', action: 'face', direction: 0, targetId: 'Player' }));
      overlay.querySelector('#sb-add-move')?.addEventListener('click', () => addAction({ type: 'npcWalk', targetId: 'this', x: 0, y: 0, wait: true }));
      overlay.querySelector('#sb-add-wait')?.addEventListener('click', () => addAction({ type: 'wait', ms: 1000 }));
      overlay.querySelector('#sb-add-battle')?.addEventListener('click', () => addAction({ type: 'battle', enemyId: 'rival01' }));
      overlay.querySelector('#sb-add-emote')?.addEventListener('click', () => addAction({ type: 'npcAction', action: 'emote', emoteId: '!', targetId: 'Player' }));
      overlay.querySelector('#sb-add-give')?.addEventListener('click', () => addAction({ type: 'giveItem', itemId: 'potion', amount: 1 }));
      overlay.querySelector('#sb-add-heal')?.addEventListener('click', () => addAction({ type: 'heal' }));
  }
  private async refreshScriptList(): Promise<void> {
      try {
          const res = await (window as any).fs.readFile('data/db/scripts.json');
          if (res.success) {
               const scripts = JSON.parse(res.data);
               this.availableScripts = Object.keys(scripts);
               this.render(); // Re-render to update dropdowns if needed, or just specific areas
          }
      } catch (e) {
          console.error('[Editor] Failed to refresh script list', e);
      }
  }

}

