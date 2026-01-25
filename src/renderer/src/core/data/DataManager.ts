import { PokemonSpecies, MoveData, PokemonInstance, Stats, calculateStat } from './DataTypes';
import { ItemData } from './ItemData';
import { ExperienceCalculator } from '../battle/ExperienceCalculator';

export class DataManager {
  private encounterTables: Map<string, any> = new Map();
  private pokemonCache: Map<string, PokemonSpecies> = new Map();
  private moveCache: Map<string, MoveData> = new Map();
  private itemCache: Map<string, ItemData> = new Map();

  constructor() {}

  public async loadRegistries(): Promise<void> {
    console.log('[DataManager] Loading Registries...');
    
    // 1. Pokedex
    const pokedex = await this.loadJson('data/db/pokedex.json');
    if (pokedex) {
        Object.values(pokedex).forEach((p: any) => {
            this.pokemonCache.set(p.id, p);
        });
        console.log(`[DataManager] Loaded ${this.pokemonCache.size} Pokemon Species.`);
    }

    // 2. Moves
    const moves = await this.loadJson('data/db/moves.json');
    if (moves) {
        Object.values(moves).forEach((m: any) => {
            this.moveCache.set(m.id, m);
        });
        console.log(`[DataManager] Loaded ${this.moveCache.size} Moves.`);
    }

    // 3. Items
    const items = await this.loadJson('data/db/items.json');
    if (items) {
        Object.values(items).forEach((item: any) => {
            this.itemCache.set(item.id, item);
        });
        console.log(`[DataManager] Loaded ${this.itemCache.size} Items.`);
    }
  }

  public async loadEncounterTable(id: string): Promise<void> {
    if (this.encounterTables.has(id)) return;
    const data = await this.loadJson(`data/encounters/${id}.json`);
    if (data) this.encounterTables.set(id, data);
  }

  // Deprecated: No longer needs individual loading
  public async loadPokemonSpecies(id: string): Promise<void> {
     // Registry is preloaded, do nothing.
     // Kept for compatibility if other code calls it.
  }

  // Deprecated: No longer needs individual loading
  public async loadMove(id: string): Promise<void> {
     // Registry is preloaded.
  }

  // Helper to load JSON via IPC
  private async loadJson(path: string): Promise<any | null> {
      try {
        const response = await (window as any).fs.readFile(path);
        if (response && response.success) {
            // console.log(`[DataManager] Loaded: ${path}`); // Reduced spam
            return JSON.parse(response.data);
        } else {
            console.error(`[DataManager] Failed to load ${path}: ${response.error}`);
            return null;
        }
      } catch (e) {
        console.error(`[DataManager] Error loading ${path}`, e);
        return null;
      }
  }

  public getEncounterTable(id: string): any {
      return this.encounterTables.get(id);
  }

  public getPokemonSpecies(id: string): PokemonSpecies | undefined {
      let species = this.pokemonCache.get(id);
      if (species) return species;

      // Try searching for integer version (e.g. "001" -> "1")
      const intId = parseInt(id).toString();
      species = this.pokemonCache.get(intId);
      if (species) return species;

      // Try searching for padded version (e.g. "1" -> "001")
      const paddedId = intId.padStart(3, '0');
      species = this.pokemonCache.get(paddedId);
      
      return species;
  }

  public getMove(id: string): MoveData | undefined {
      return this.moveCache.get(id);
  }

  public getItem(id: string): ItemData | undefined {
      return this.itemCache.get(id);
  }


  public getAllSpecies(): PokemonSpecies[] {
      return Array.from(this.pokemonCache.values());
  }

  public getAllItems(): ItemData[] {
      return Array.from(this.itemCache.values());
  }

  // Factory Method: Create a new Instance
  public createPokemonInstance(species: PokemonSpecies, level: number): PokemonInstance {
      // 1. IVs (0-31)
      const ivs: Stats = {
          hp: Math.floor(Math.random() * 32),
          attack: Math.floor(Math.random() * 32),
          defense: Math.floor(Math.random() * 32),
          spAttack: Math.floor(Math.random() * 32),
          spDefense: Math.floor(Math.random() * 32),
          speed: Math.floor(Math.random() * 32)
      };

      // 2. EVs (Start at 0)
      const evs: Stats = { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 };

      // 3. Nature (Random for now, placeholder)
      const nature = "Hardy"; 

      // 4. Ability (Random from possible)
      const ability = species.possibleAbilities[Math.floor(Math.random() * species.possibleAbilities.length)];

      // 5. Moves (Based on Level)
      // Filter learnset for moves <= level, take last 4
      const validMoves = species.learnset.filter(m => m.level <= level);
      const learnedMoves = validMoves.slice(-4).map(m => ({
          moveId: m.moveId,
          pp: 10, // Placeholder, need actual Move Data to set max PP
          maxPp: 10
      }));

      // 6. Calculate Stats
      // Use helper from DataTypes
      const currentStats: Stats = {
          hp: calculateStat(species.baseStats.hp, ivs.hp, evs.hp, level, true),
          attack: calculateStat(species.baseStats.attack, ivs.attack, evs.attack, level, false),
          defense: calculateStat(species.baseStats.defense, ivs.defense, evs.defense, level, false),
          spAttack: calculateStat(species.baseStats.spAttack, ivs.spAttack, evs.spAttack, level, false),
          spDefense: calculateStat(species.baseStats.spDefense, ivs.spDefense, evs.spDefense, level, false),
          speed: calculateStat(species.baseStats.speed, ivs.speed, evs.speed, level, false)
      };

      return {
          uuid: crypto.randomUUID(),
          speciesId: species.id,
          nickname: species.name,
          types: species.types, // Inherit types from species
          originalTrainer: "Player", // TODO: Get from Save Data
          level: level,
          experience: ExperienceCalculator.getExpForLevel(level),
          ivs,
          evs,
          nature,
          ability,
          gender: Math.random() > 0.5 ? 'Male' : 'Female', // TODO: Use gender ratio
          shiny: Math.random() < (1/4096),
          moves: learnedMoves,
          currentHp: currentStats.hp,
          currentStats,
          status: 'None',
          volatile: {},
          statStages: {}
      };
  }
}
