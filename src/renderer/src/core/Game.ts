import { InputManager } from './InputManager';
import { EventBus } from './EventBus';
import { SaveManager } from './SaveManager';
import { Display } from './Display';
import { Tilemap } from './Tilemap';
import { MapLoader } from './world/MapLoader';
import { Camera } from './Camera';
import { Player } from './entities/Player';
import { TiledObjectLayer } from './world/TiledTypes';
import { DataManager } from './data/DataManager';
import type { PokemonInstance } from './data/DataTypes';
import { BattleScene } from './battle/BattleScene';
import { EncounterManager } from './EncounterManager';
import { TILE_SIZE } from './consts';
import { EventManager } from './EventManager';
import { DialogBox } from './DialogBox';
import { NPC } from './entities/NPC';
import { MenuSystem } from './ui/MenuSystem';
import { StorageSystem } from './StorageSystem'; // Import
import { BagSystem } from './BagSystem';
import { StartMenu } from './ui/StartMenu';
import { TitleScreen } from './ui/TitleScreen';
import { ItemHandler } from './items/ItemHandler';
import { WeatherManager } from './WeatherManager';
import { WeatherType } from './data/DataTypes';

export enum GameState {
  Overworld,
  Battle,
  Script,
  Menu // New State
}

export class Game {
  public input: InputManager;
  public events: EventBus;
  public saveSystem: SaveManager;
  public dataManager: DataManager;
  public encounterManager: EncounterManager;
  public storageSystem: StorageSystem; // Property
  public bagSystem: BagSystem; // Property
  public itemHandler: ItemHandler; // Item usage handler
  public display: Display;
  public map: Tilemap;
  public camera: Camera;
  public player: Player;
  public npcList: NPC[] = [];
  
  public eventManager: EventManager;
  public dialogBox: DialogBox;
  public menuSystem: MenuSystem; // New System
  public weatherManager: WeatherManager;

  public state: GameState = GameState.Overworld;
  public battleScene: BattleScene;
  
  // Transition
  private isTransitioning: boolean = false;
  private transitionTimer: number = 0;
  private transitionDuration: number = 1200;
  private pendingEncounterTableId: string | null = null;
  
  private running: boolean = false;
  public lastTime: number = 0;
  
  public onRender: ((ctx: CanvasRenderingContext2D) => void) | null = null;

  constructor(parentId: string = 'app') {
    console.log('Game Engine Initialized');
    this.display = new Display(parentId);
    this.input = new InputManager();
    this.events = new EventBus();
    this.saveSystem = new SaveManager();
    this.dataManager = new DataManager();
    this.encounterManager = new EncounterManager();
    this.storageSystem = new StorageSystem(); // Init
    this.bagSystem = new BagSystem(); // Init
    this.itemHandler = new ItemHandler(this); // Init ItemHandler
    this.eventManager = new EventManager(this);
    this.dialogBox = new DialogBox(this.display, this.input);
    this.dialogBox.setEventManager(this.eventManager);
    this.menuSystem = new MenuSystem(); // Init Menu
    this.weatherManager = new WeatherManager(this);
    
    // TEST DATA LOADING
    this.testDataLoading();
    
    // Initialize systems
    this.map = new Tilemap();
    this.camera = new Camera(this.display.width, this.display.height);
    this.player = new Player();
    this.battleScene = new BattleScene(this);
    
    // Load Registries then Title Screen
    this.dataManager.loadRegistries().then(() => {
        // Only skip title if we are hot-reloading into a specific level context
        if (this.currentLevelPath) {
            console.log(`[Game] Skipping title, level already loading: ${this.currentLevelPath}`);
            return;
        }

        // Debug Populate PC
        this.storageSystem.debugPopulate(this.dataManager);
        
        // Debug Populate Bag
        // this.bagSystem.debugAddStarterItems();
        this.bagSystem.debugAddAllItems(this.dataManager.getAllItems());
        
        // --- VERIFICATION ---
        setTimeout(() => {
             import('./debug/FullItemSuite').then(({ FullItemSuite }) => {
                 new FullItemSuite(this).run();
             });
        }, 2000); // Verify a bit after load
        
        console.log('[Game] Starting Title Screen');
        this.state = GameState.Menu;
        this.menuSystem.push(new TitleScreen(this));
    });
  }

  public currentLevelPath: string = '';

  public async loadLevel(mapPath: string, startGridX?: number, startGridY?: number): Promise<void> {
    console.log(`[Game] Loading Level: ${mapPath}`);
    this.currentLevelPath = mapPath;
    const loader = new MapLoader();
    try {
        const mapData = await loader.loadMap(mapPath);
        console.log('[Game] Map Data Loaded:', !!mapData);
        
        if (mapData) {
            // Preserve Manager references before resetting map
            const tilesetManager = this.map.tilesetManager;
            const layerManager = this.map.layerManager;
            
            this.map = new Tilemap(); // Reset Map
            this.map.tilesetManager = tilesetManager; // Restore
            this.map.layerManager = layerManager; // Restore
            
            this.map.loadFromTiled(mapData);
            this.map.path = mapPath; // Set path on Tilemap
            console.log('[Game] Tilemap initialized from Tiled data');
            
            // Update Camera Map Size
            console.log(`[Game] Map Dimensions: ${this.map.getPixelWidth()}x${this.map.getPixelHeight()}`);
            this.camera.setMapSize(this.map.getPixelWidth(), this.map.getPixelHeight());

            // --- WEATHER INTEGRATION ---
            // Check Map Properties for 'weather'
            const weatherProp = mapData.properties?.find(p => p.name === 'weather')?.value;
            if (weatherProp) {
                const weatherStr = String(weatherProp);
                // Simple mapping, assume property matches enum string or close to it
                // 'Rain', 'Sun', 'Sandstorm', 'Hail', 'Fog'
                console.log(`[Game] Map defines weather: ${weatherStr}`);
                
                // Capitalize first letter logic if needed, or exact match
                if (['Rain', 'Sun', 'Sandstorm', 'Hail', 'Fog'].includes(weatherStr)) {
                     this.weatherManager.setWeather(weatherStr as WeatherType);
                } else {
                     this.weatherManager.setWeather('None');
                }
            } else {
                this.weatherManager.setWeather('None');
            }
            
            // Spawn Logic
            console.log('[Game] Checking Spawn Logic...');
            if (startGridX !== undefined && startGridY !== undefined) {
                // Specific Warp Destination
                this.player.setPosition(startGridX, startGridY, TILE_SIZE);
                console.log(`[Game] Warped player to ${startGridX},${startGridY}`);
                // Prevent immediate re-warp if spawning on a warp tile
                this.warpCooldown = true;
                setTimeout(() => this.warpCooldown = false, 1000);
            } else {
                // Default Spawn Point
                const objectLayer = mapData.layers.find(l => l.name === 'Objects' && l.type === 'objectgroup') as TiledObjectLayer;
                console.log('[Game] Object Layer found:', !!objectLayer);
                
                if (objectLayer) {
                    console.log(`[Game] Searching path in ${objectLayer.objects.length} objects`);
                    // Find 'Spawn' type OR 'SpawnPoint' name
                    const spawn = objectLayer.objects.find(o => o.type === 'Spawn' || o.name === 'SpawnPoint');
                    if (spawn) {
                        const gridX = Math.floor(spawn.x / TILE_SIZE);
                        const gridY = Math.floor(spawn.y / TILE_SIZE);
                        this.player.setPosition(gridX, gridY, TILE_SIZE);
                        console.log(`[Game] Spawned player at ${gridX},${gridY} (Source: ${spawn.name})`);
                    } else {
                        console.warn('[Game] No Spawn Point found in Object Layer!');
                        this.player.setPosition(5, 5, TILE_SIZE); // Fallback
                    }
                } else {
                     console.warn('[Game] No Object Layer found!');
                }

                if (objectLayer) {
                     this.refreshNPCs();
                }
            }
        }
    } catch (e) {
        console.error('[Game] Error loading level:', e);
    }
  }

  public async save(slot: number = 0): Promise<void> {
      const data = {
          timestamp: Date.now(),
          player: {
              gridX: this.player.gridX,
              gridY: this.player.gridY,
              direction: this.player.direction,
              map: this.map.path || this.currentLevelPath // Ensure we save the map path!
          },
          flags: this.eventManager.flags,
          storage: this.storageSystem.boxes, // Save PC
          party: this.party, // Save Party
          bag: this.bagSystem.serialize() // Save Bag
      };
      
      console.log('[Game] Saving Data:', data);
      const success = await this.saveSystem.saveGame(slot, data);
      if (success) {
          alert('Game Saved Successfully!');
      } else {
          alert('Failed to save game.');
      }
  }

  public async load(slot: number = 0): Promise<void> {
      const data = await this.saveSystem.loadGame(slot);
      if (data) {
          console.log('[Game] Loading Data:', data);
          
          // 1. Restore Flags
          if (data.flags) {
              this.eventManager.flags = data.flags;
          }

          // 2. Restore Storage
          if (data.storage) {
              this.storageSystem.boxes = data.storage;
          }
          
          // 3. Restore Party (NEW)
          if (data.party) {
              this.party = data.party;
              // Re-link the first party member to this.player if needed? 
              // Actually Game.player doesn't hold the pokemon data, Game.party does.
          }
          
          // 4. Restore Bag
          if (data.bag) {
              this.bagSystem.deserialize(data.bag);
          }
          
          // 5. Load Map & Player
          if (data.player && data.player.map) {
              // Load Level will reset map, so we should do that first
              // We pass the start coordinates directly to loadLevel to handle the spawn cleanly
              await this.loadLevel(data.player.map, data.player.gridX, data.player.gridY);
              
              // Restore direction
              this.player.direction = data.player.direction;
          }
      } else {
          alert('No save file found.');
      }
  }
  
  public refreshNPCs(): void {
      this.npcList = [];
      const objectLayer = this.map['layers'].find(l => l.name === 'Objects');
      if (objectLayer) {
           objectLayer.objects.forEach(obj => {
                if (obj.type === 'NPC') {
                    const gridX = Math.floor(obj.x / TILE_SIZE);
                    const gridY = Math.floor(obj.y / TILE_SIZE);
                    const spriteProp = obj.properties?.find(p => p.name === 'sprite')?.value;
                    const triggerProp = obj.properties?.find(p => p.name === 'triggerId')?.value;
                    const scaleProp = obj.properties?.find(p => p.name === 'scale')?.value;
                    const scale = scaleProp !== undefined ? parseFloat(scaleProp) : 1.0;
                    
                    const npc = new NPC(obj.id, obj.name, gridX, gridY, spriteProp, triggerProp, scale); 
                    this.npcList.push(npc);
                    console.log(`[Game] Refreshed NPC: ${obj.name} Scale:${scale}`);
                }
           });
      }
  }

  public start(): void {
    if (this.running) return;
    this.running = true;
    this.lastTime = performance.now();
    requestAnimationFrame(this.loop.bind(this));
  }

  public stop(): void {
    this.running = false;
  }

  private loop(timestamp: number): void {
    if (!this.running) return;

    const deltaTime = timestamp - this.lastTime;
    this.lastTime = timestamp;

    this.update(deltaTime);
    this.render();

    // Clear input flags at END of frame, but NOT during scripts
    // (DialogBox needs to read the same key press across multiple frames)
    if (this.state !== GameState.Script) {
        this.input.update();
    }

    requestAnimationFrame(this.loop.bind(this));
  }

  // Editor Mode Flag
  public isEditorMode: boolean = false;
  private warpCooldown: boolean = false;
  private triggeredIds: Set<string> = new Set(); // To track non-repeatable triggers
  public zoom: number = 1.0;

  public setEditorMode(enabled: boolean): void {
      this.isEditorMode = enabled;
      console.log(`[Game] Editor Mode: ${enabled}`);
      // When entering editor mode, maybe reset input?
      if (enabled) {
          this.input.reset();
      }
  }

  private update(dt: number): void {
    if (this.isEditorMode) {
        // In Editor Mode, we ONLY update the camera (if we want to pan around)
        // For now, let's allow Camera panning via WASD even in editor mode?
        // Or strictly mouse panning? 
        // Let's Skip Player Update entirely.
        
        // Optional: Implement Editor Camera Panning here later.
        return;
    }

    if (this.isTransitioning) {
        this.updateTransition(dt);
        return;
    }

    // Modal Dialog: If dialog is open, it pauses everything else (script, menu, overworld)
    if (this.dialogBox.isVisible) {
        this.dialogBox.update();
        return;
    }

    if (this.state === GameState.Menu) {
        // Update Menu
        this.menuSystem.update(dt);
        // If menu closed itself, it pops stack
        if (!this.menuSystem.isOpen) {
            this.state = GameState.Overworld;
            // Provide a small cooldown or input reset so we don't immediately interact
            this.input.reset(); 
        }
        return;
    }

    if (this.state === GameState.Script) {
        // Update Dialog
        this.dialogBox.update();
        // Allow NPCs to animate even during scripts
        this.npcList.forEach(npc => npc.update());
        // Player animation could continue (idle)
        return;
    }

    if (this.state === GameState.Battle) {
        this.battleScene.update(dt, this.input);
        if (!this.battleScene.isActive) {
            this.state = GameState.Overworld;
            this.input.reset();
            return;
        }
        return;
    }

    this.npcList.forEach(npc => npc.update());

    // Menu Toggle
    if (this.input.isJustPressed('Enter') || this.input.isJustPressed('Escape')) {
         console.log('[Game] Opening Menu...');
         this.state = GameState.Menu;
         this.menuSystem.push(new StartMenu(this));
         this.input.reset(); // Clear input so we don't accidentally select first item
         return;
    }

    // Interaction Check
    if (this.input.isJustPressed('KeyZ') || this.input.isJustPressed('Space')) {
        this.checkForInteraction();
    }

    // Update Player Movement (Pass Map for collision)
    // If update returns true, a step was just finished
    const stepFinished = this.player.update(this.input, this.map, this.npcList);
    
    if (stepFinished) {
        // Priority: Warp > Trigger > Encounter
        if (this.checkForWarp()) {
            return; // Skip other checks if warping
        }
        if (this.checkForTrigger()) {
            // Trigger might handle its own priority
        }
        this.checkForEncounter();
    }
    
    // Update Camera to follow player
    this.camera.follow(this.player);

    if (this.input.isJustPressed('F5')) {
        this.saveSystem.saveGame(1, { timestamp: Date.now() });
    }
  }

  private checkForInteraction(): void {
      // 0: Down, 1: Left, 2: Right, 3: Up (matches Player.ts enum ideally, but double check)
      // Actually Player.ts: Down=0, Left=1, Right=2, Up=3
      let dx = 0; let dy = 0;
      switch (this.player.direction) {
          case 0: dy = 1; break; // Down
          case 1: dx = -1; break; // Left
          case 2: dx = 1; break; // Right
          case 3: dy = -1; break; // Up
      }
      
      const targetX = this.player.gridX + dx;
      const targetY = this.player.gridY + dy;
      
      console.log(`[Game] Checking interaction at ${targetX},${targetY}`);
      
      const npc = this.npcList.find(n => n.gridX === targetX && n.gridY === targetY);
      if (npc) {
          console.log(`[Game] Found NPC: ${npc.uniqueId}`);
          
          // Face Player (Basic)
          // npc.face(opposite...)
          
          if (npc.triggerId) {
             console.log(`[Game] Interaction Trigger: ${npc.triggerId}`);
             this.eventManager.runScript(npc.triggerId);
          }
      }
  }

  private async testDataLoading(): Promise<void> {
      console.log('[Game] Testing Data Loading...');
      await this.dataManager.loadPokemonSpecies('001');
      await this.dataManager.loadMove('tackle');
      
      const bulbasaur = this.dataManager.getPokemonSpecies('001');
      if (bulbasaur) {
          console.log('[Game] Loaded Species:', bulbasaur);
          const instance = this.dataManager.createPokemonInstance(bulbasaur, 5);
          console.log('[Game] Created Instance (Level 5):', instance);
          
          // VISUAL TEST
          console.log('[Game] Testing Sprite Loading for Battle...');
          (window as any).fs.readImage(bulbasaur.assets.front).then((res: any) => {
              if(res.success) console.log('[Game] SUCCESS: Loaded Front Sprite');
              else console.error('[Game] FAIL: Front Sprite', res.error);
          });
          (window as any).fs.readImage(bulbasaur.assets.back).then((res: any) => {
              if(res.success) console.log('[Game] SUCCESS: Loaded Back Sprite');
              else console.error('[Game] FAIL: Back Sprite', res.error);
          });
      }
  }
  
  private checkForWarp(): boolean {
      if (this.warpCooldown) return false;

      const centerX = this.player.x + 8;
      const centerY = this.player.y + 8;
      const warp = this.map.getWarpAt(centerX, centerY);
      
      if (warp) {
          console.log(`[Game] Warp Triggered! To: ${warp.targetMap} @ ${warp.targetX},${warp.targetY}`);
          this.input.reset();
          this.loadLevel(warp.targetMap, warp.targetX, warp.targetY);
          return true;
      }
      return false;
  }

  private checkForTrigger(): boolean {
      const pX = this.player.gridX;
      const pY = this.player.gridY;
      
      console.log(`[Game] checkForTrigger at player pos: (${pX}, ${pY})`);
      
      // 1. Check Step Triggers on NPCs
      const npc = this.npcList.find(n => n.gridX === pX && n.gridY === pY);
      if (npc && npc.triggerId) {
           const obj = this.map.getObjectById(npc.id);
           const type = obj?.properties?.find(p => p.name === 'triggerType')?.value;
           
           if (type === 'step') {
               console.log(`[Game] Step Trigger: ${npc.triggerId}`);
               this.eventManager.runScript(npc.triggerId, { targetNpcId: npc.uniqueId });
               return true;
           }
      }
      
      // 2. Check Generic Triggers (Object Layer)
      const objectLayer = this.map['layers']?.find(l => l.name === 'Objects');
      console.log(`[Game] Object layer found:`, !!objectLayer);
      
      if (objectLayer && objectLayer.objects) {
          console.log(`[Game] Checking ${objectLayer.objects.length} objects for triggers`);
          
          const triggerObj = objectLayer.objects.find(o => {
               const objGridX = Math.floor(o.x / TILE_SIZE);
               const objGridY = Math.floor(o.y / TILE_SIZE);
               const objGridW = Math.floor(o.width / TILE_SIZE);
               const objGridH = Math.floor(o.height / TILE_SIZE);
               
               const isInBounds = pX >= objGridX && pX < objGridX + objGridW &&
                      pY >= objGridY && pY < objGridY + objGridH;
               
               const isTrigger = o.type === 'Trigger';
               
               if (isTrigger) {
                   console.log(`[Game] Found Trigger object at (${objGridX},${objGridY}) size ${objGridW}x${objGridH}, player in bounds: ${isInBounds}`);
               }
               
               return isInBounds && isTrigger;
          });
          
          if (triggerObj) {
              console.log(`[Game] Trigger object found:`, triggerObj);
              const triggerId = triggerObj.properties?.find(p => p.name === 'triggerId')?.value;
              const type = triggerObj.properties?.find(p => p.name === 'triggerType')?.value;
              
              console.log(`[Game] Trigger ID: ${triggerId}, Type: ${type}`);
              
              if (type === 'step' && triggerId) {
                  // Debounce: Don't re-trigger if already active/triggered non-repeatable
                  const isRepeatable = triggerObj.properties.find(p => p.name === 'repeatable')?.value;
                  if (this.triggeredIds.has(triggerId) && !isRepeatable) {
                      console.log(`[Game] Trigger ${triggerId} already fired (non-repeatable)`);
                      return false;
                  }
                  
                  console.log(`[Game] Step Trigger (Obj): ${triggerId} - FIRING!`);
                  this.triggeredIds.add(triggerId);
                  this.eventManager.runScript(triggerId, { targetNpcId: triggerObj.name });
                  return true;
              } else {
                  console.log(`[Game] Trigger found but not firing - type: ${type}, triggerId: ${triggerId}`);
              }
          } else {
              console.log(`[Game] No trigger object found at player position`);
          }
      }

      // 3. Fallback: Interact Triggers checked via interaction key, not here.
      return false;
  }

  private async checkForEncounter(): Promise<void> {
    const centerX = this.player.x + 16;
    const centerY = this.player.y + 16;
    const zoneName = this.map.getEncounterZoneAt(centerX, centerY);
    
    // Debug spam (temporary)
    if (zoneName) {
        // console.log(`[Game] In Zone: ${zoneName}`);
    }

    if (zoneName) {
        // 15% chance per step as requested
        const roll = Math.random();
        // console.log(`[Game] Zone ${zoneName} Roll: ${roll}`);
        
        if (roll < 0.15) { 
            console.log(`[Game] Wild encounter triggered! Zone: ${zoneName}`);
            this.input.reset();
            
            // Start Transition
            this.isTransitioning = true;
            this.transitionTimer = 0;
            // Store zone ID for callback
            this.pendingEncounterTableId = zoneName; 
        }
    }
  }
  // ...

  private updateTransition(dt: number): void {
      this.transitionTimer += dt;
      if (this.transitionTimer >= this.transitionDuration) {
          this.isTransitioning = false;
          if (this.pendingEncounterTableId) {
             this.startWildEncounter(this.pendingEncounterTableId);
             this.pendingEncounterTableId = null;
          }
      }
  }

  public party: PokemonInstance[] = []; // Persistent Party

  private async startWildEncounter(zoneId: string): Promise<void> {
      console.log(`[Game] Generating wild encounter: ${zoneId}`);
      
      const raw = this.encounterManager.generateEncounter(zoneId);
      if (!raw) {
          console.warn('[Game] Failed to generate wild encounter. Check zone settings.');
          return;
      }

      const wildPokemon = this.normalizeWildForBattle(raw);
      console.log(`[Game] Wild ${wildPokemon.nickname} (Lv.${wildPokemon.level}) appeared!`);
      
      // Use First Pokemon in Party
      const playerMon = this.party[0];
      if (!playerMon) {
          console.error('[Game] Cannot start battle: Party is empty!');
          return;
      }
      
      if (playerMon.currentHp <= 0) {
           // Iterate to find first alive?
           const alive = this.party.find(p => p.currentHp > 0);
           if (!alive) {
               console.error('[Game] All pokemon fainted!');
               return; 
           }
           // Use the alive one? Or generic "swap" logic needed?
           // For now, just use the alive one.
           console.log(`[Game] Sending out ${alive.nickname}!`);
           this.state = GameState.Battle;
           this.battleScene.startBattle(alive, wildPokemon);
           return;
      }

      console.log('[Game] Switching to Battle State...');
      this.state = GameState.Battle;
      this.battleScene.startBattle(playerMon, wildPokemon);
  }

  /**
   * Convert EncounterManager wild (id, moves: string[]) to DataTypes.PokemonInstance
   * so BattleScene / MoveEngine / BattleAI work correctly.
   */
  private normalizeWildForBattle(raw: any): PokemonInstance {
      const uuid = raw.id ?? raw.uuid ?? crypto.randomUUID();
      const moveIds = Array.isArray(raw.moves) ? raw.moves : [];
      const moves = moveIds.map((mid: string) => {
          const m = this.dataManager.getMove(mid);
          const pp = m?.pp ?? 10;
          return { moveId: mid, pp, maxPp: pp };
      });
      const status = raw.status === null || raw.status === undefined ? 'None' : raw.status;
      const currentStats = raw.currentStats ?? raw.baseStats ?? { hp: 1, attack: 1, defense: 1, spAttack: 1, spDefense: 1, speed: 1 };
      const evs = raw.evs ?? { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 };

      return {
          uuid,
          speciesId: raw.speciesId,
          nickname: raw.nickname ?? raw.name,
          types: raw.types ?? [],
          originalTrainer: 'Wild',
          level: raw.level ?? 1,
          experience: raw.experience ?? 0,
          ivs: raw.ivs ?? { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 },
          evs,
          nature: raw.nature ?? 'Hardy',
          ability: raw.ability ?? 'None',
          gender: 'Genderless',
          shiny: !!raw.isShiny,
          moves,
          currentHp: raw.currentHp ?? currentStats.hp,
          currentStats,
          status,
          volatile: raw.volatile ?? {},
          statStages: raw.statStages ?? {}
      } as PokemonInstance;
  }


  private render(): void {
    this.display.clear();
    
    if (this.state === GameState.Battle) {
        this.battleScene.render(this.display);
        return;
    }
    
    // Zoom Support
    const zoom = this.zoom || 1;
    this.display.ctx.save();
    this.display.ctx.scale(zoom, zoom);

    // Render Map (Pass Camera)
    this.map.render(this.display, this.camera, zoom);
    
    // Render NPCs
    this.npcList.forEach(npc => npc.render(this.display, this.camera.x, this.camera.y));

    // Render Player (Pass Camera for offset)
    this.player.renderWithCamera(this.display, this.camera.x, this.camera.y);

    // Editor Overlay
    if (this.isEditorMode && this.onRender) {
        this.onRender(this.display.ctx);
    }

    this.display.ctx.restore();
    
    // Menu Overlay
    if (this.state === GameState.Menu) {
        this.menuSystem.render(this.display.ctx);
    }
    
    // UI Overlay - Render LAST to be on top of Menus
    this.dialogBox.render();
    
    // Transition Overlay
    if (this.isTransitioning) {
        const ctx = this.display.ctx;
        const progress = this.transitionTimer / this.transitionDuration;
        
        // 1. Flash White (0% - 20%)
        // 2. Flash Black (20% - 40%)
        // 3. Flash White (40% - 60%)
        // 4. Fade to Black (60% - 100%)
        
        if (progress < 0.6) {
             // Flashing Effect (Simple Strobe)
             const strobeSpeed = 100; // ms
             if (Math.floor(this.transitionTimer / strobeSpeed) % 2 === 0) {
                 ctx.fillStyle = 'rgba(255, 255, 255, 0.5)'; // Flash White
                 ctx.fillRect(0, 0, this.display.width, this.display.height);
                 // Invert?
                 // ctx.globalCompositeOperation = 'difference';
                 // ctx.fillStyle = 'white';
                 // ctx.fillRect(0, 0, this.display.width, this.display.height);
                 // ctx.globalCompositeOperation = 'source-over';
             } else {
                 // Blink Black?
                 ctx.fillStyle = 'rgba(0, 0, 0, 0.8)';
                 ctx.fillRect(0, 0, this.display.width, this.display.height);
             }
        } else {
            // Fade to Black
            const fadeProgress = (progress - 0.6) / 0.4; // 0 to 1
            ctx.fillStyle = `rgba(0, 0, 0, ${fadeProgress})`;
            ctx.fillRect(0, 0, this.display.width, this.display.height);
        }
    }
  }
}
