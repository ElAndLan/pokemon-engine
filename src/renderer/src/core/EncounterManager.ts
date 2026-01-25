import type {
  PokemonSpecies,
  PokemonInstance,
  PokemonStats,
  PokemonIVs,
  EncountersData,
  EncounterEntry,
  MovesData
} from '../types/PokemonTypes';

/**
 * Manages wild Pokemon encounters with proper IV generation, movesets, and shiny determination
 */
export class EncounterManager {
  private encounters: EncountersData = {};
  private pokedex: { [id: string]: PokemonSpecies } = {};
  private moves: MovesData = {};
  private shinyChance: number = 8192; // 1/8192 base shiny chance

  constructor() {
    this.loadData();
  }

  /**
   * Load all required data (encounters, pokedex, moves)
   */
  private async loadData(): Promise<void> {
    try {
      // Load encounters
      const encountersResult = await (window as any).fs.readFile('data/db/encounters.json');
      if (encountersResult.success) {
        this.encounters = JSON.parse(encountersResult.data);
        console.log('[EncounterManager] Loaded encounters');
      }

      // Load pokedex
      const pokedexResult = await (window as any).fs.readFile('data/db/pokedex.json');
      if (pokedexResult.success) {
        this.pokedex = JSON.parse(pokedexResult.data);
        console.log('[EncounterManager] Loaded pokedex');
      }

      // Load moves
      const movesResult = await (window as any).fs.readFile('data/db/moves.json');
      if (movesResult.success) {
        this.moves = JSON.parse(movesResult.data);
        console.log('[EncounterManager] Loaded moves');
      }
    } catch (error) {
      console.error('[EncounterManager] Error loading data:', error);
    }
  }

  /**
   * Generate a wild encounter for a specific route and encounter type
   */
  /**
   * Generate a wild encounter for a specific zone
   */
  public generateEncounter(zoneId: string): PokemonInstance | null {
    // Direct lookup: encounters.json keys are the zone IDs (e.g. "route_1_grass")
    const zoneData = this.encounters[zoneId] as any;
    
    if (!zoneData || !zoneData.encounters || zoneData.encounters.length === 0) {
      console.warn(`[EncounterManager] No encounters found for zone: ${zoneId}`);
      return null;
    }

    const encounterTable = zoneData.encounters as EncounterEntry[];
    const selectedEncounter = this.weightedRandomSelection(encounterTable);
    if (!selectedEncounter) return null;

    return this.createWildPokemon(selectedEncounter);
  }

  /**
   * Weighted random selection from encounter table
   */
  private weightedRandomSelection(encounterTable: EncounterEntry[]): EncounterEntry | null {
    const totalRate = encounterTable.reduce((sum, enc) => sum + (enc.rate || enc.weight || 0), 0);
    const roll = Math.random() * totalRate;

    let cumulative = 0;
    for (const encounter of encounterTable) {
      cumulative += (encounter.rate || encounter.weight || 0);
      if (roll < cumulative) {
        return encounter;
      }
    }

    return encounterTable[encounterTable.length - 1];
  }

  /**
   * Generate a random level within the specified range
   */
  private randomLevel(levelRange: [number, number]): number {
    const [min, max] = levelRange;
    return Math.floor(Math.random() * (max - min + 1)) + min;
  }

  /**
   * Generate random IVs (0-31 for each stat)
   */
  private generateIVs(): PokemonIVs {
    return {
      hp: Math.floor(Math.random() * 32),
      attack: Math.floor(Math.random() * 32),
      defense: Math.floor(Math.random() * 32),
      spAttack: Math.floor(Math.random() * 32),
      spDefense: Math.floor(Math.random() * 32),
      speed: Math.floor(Math.random() * 32)
    };
  }

  /**
   * Determine if Pokemon should be shiny
   */
  private isShiny(): boolean {
    return Math.floor(Math.random() * this.shinyChance) === 0;
  }

  /**
   * Get the 4 strongest moves a Pokemon should know at a given level
   */
  private getMovesForLevel(pokemonId: string, level: number): string[] {
    const pokemon = this.pokedex[pokemonId];
    if (!pokemon || !pokemon.learnset) return [];

    const learnableMoves = pokemon.learnset
      .filter(move => move.level <= level)
      .map(move => ({
        moveId: move.moveId,
        learnLevel: move.level,
        power: this.moves[move.moveId]?.power || 0
      }));

    if (learnableMoves.length === 0) return [];

    learnableMoves.sort((a, b) => {
      if (b.learnLevel !== a.learnLevel) {
        return b.learnLevel - a.learnLevel;
      }
      return b.power - a.power;
    });

    return learnableMoves.slice(0, 4).map(m => m.moveId);
  }

  /**
   * Generate a random nature
   */
  private randomNature(): string {
    const natures = [
      'Hardy', 'Lonely', 'Brave', 'Adamant', 'Naughty',
      'Bold', 'Docile', 'Relaxed', 'Impish', 'Lax',
      'Timid', 'Hasty', 'Serious', 'Jolly', 'Naive',
      'Modest', 'Mild', 'Quiet', 'Bashful', 'Rash',
      'Calm', 'Gentle', 'Sassy', 'Careful', 'Quirky'
    ];
    return natures[Math.floor(Math.random() * natures.length)];
  }

  /**
   * Create a complete wild Pokemon instance
   */
  private createWildPokemon(encounter: EncounterEntry): PokemonInstance | null {
    const pokemonId = encounter.pokemonId;
    
    // Robust Lookup: Try original, then integer, then padded
    let pokemonData = this.pokedex[pokemonId];
    if (!pokemonData) {
        // Try removing leading zeros
        const intId = parseInt(pokemonId).toString();
        pokemonData = this.pokedex[intId];
        
        // Try padding to 3 digits (if not found yet)
        if (!pokemonData) {
            const paddedId = pokemonId.toString().padStart(3, '0');
            pokemonData = this.pokedex[paddedId];
        }
    }

    if (!pokemonData) {
      console.error(`[EncounterManager] Pokemon ${pokemonId} not found in pokedex (tried variants)`);
      return null;
    }



    let minLevel = 1;
    let maxLevel = 1;

    if (encounter.minLevel !== undefined && encounter.maxLevel !== undefined) {
        minLevel = encounter.minLevel;
        maxLevel = encounter.maxLevel;
    } else if (encounter.levelMin !== undefined && encounter.levelMax !== undefined) {
        minLevel = encounter.levelMin;
        maxLevel = encounter.levelMax;
    } else if (encounter.level) {
        [minLevel, maxLevel] = encounter.level;
    }

    const level = Math.floor(Math.random() * (maxLevel - minLevel + 1)) + minLevel;
    const ivs = this.generateIVs();
    const isShiny = this.isShiny();
    const nature = this.randomNature();
    const moveset = this.getMovesForLevel(pokemonId, level);

    const stats = this.calculateStats(pokemonData.baseStats, ivs, level, nature);

    return {
      id: crypto.randomUUID(), // Valid UUID for the instance
      speciesId: pokemonId, // The species ID (e.g. "16")
      name: pokemonData.name,
      nickname: pokemonData.name, // Default nickname to species name
      level: level,
      types: pokemonData.types,
      baseStats: pokemonData.baseStats,
      currentStats: stats,
      ivs: ivs,
      evs: { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 },
      nature: nature,
      moves: moveset,
      currentHp: stats.hp,
      maxHp: stats.hp,
      isShiny: isShiny,
      ability: this.randomAbility(pokemonData.possibleAbilities),
      experience: this.experienceForLevel(level),
      friendship: pokemonData.catchRate || 70,
      status: null,
      isWild: true
    };
  }

  /**
   * Calculate actual stats from base stats, IVs, level, and nature
   */
  private calculateStats(baseStats: PokemonStats, ivs: PokemonIVs, level: number, nature: string): PokemonStats {
    const evs = { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 };

    // HP calculation
    const hp = Math.floor(((2 * baseStats.hp + ivs.hp + Math.floor(evs.hp / 4)) * level) / 100) + level + 10;

    // Other stats calculation
    const calcStat = (base: number, iv: number, ev: number, statName: keyof PokemonStats): number => {
      const baseStat = Math.floor(((2 * base + iv + Math.floor(ev / 4)) * level) / 100) + 5;
      return Math.floor(baseStat * this.getNatureModifier(nature, statName));
    };

    return {
      hp: hp,
      attack: calcStat(baseStats.attack, ivs.attack, evs.attack, 'attack'),
      defense: calcStat(baseStats.defense, ivs.defense, evs.defense, 'defense'),
      spAttack: calcStat(baseStats.spAttack, ivs.spAttack, evs.spAttack, 'spAttack'),
      spDefense: calcStat(baseStats.spDefense, ivs.spDefense, evs.spDefense, 'spDefense'),
      speed: calcStat(baseStats.speed, ivs.speed, evs.speed, 'speed')
    };
  }

  /**
   * Get nature modifier for a stat
   */
  private getNatureModifier(nature: string, stat: keyof PokemonStats): number {
    const natureEffects: { [key: string]: Partial<Record<keyof PokemonStats, number>> } = {
      'Lonely': { attack: 1.1, defense: 0.9 },
      'Brave': { attack: 1.1, speed: 0.9 },
      'Adamant': { attack: 1.1, spAttack: 0.9 },
      'Naughty': { attack: 1.1, spDefense: 0.9 },
      'Bold': { defense: 1.1, attack: 0.9 },
      'Relaxed': { defense: 1.1, speed: 0.9 },
      'Impish': { defense: 1.1, spAttack: 0.9 },
      'Lax': { defense: 1.1, spDefense: 0.9 },
      'Timid': { speed: 1.1, attack: 0.9 },
      'Hasty': { speed: 1.1, defense: 0.9 },
      'Jolly': { speed: 1.1, spAttack: 0.9 },
      'Naive': { speed: 1.1, spDefense: 0.9 },
      'Modest': { spAttack: 1.1, attack: 0.9 },
      'Mild': { spAttack: 1.1, defense: 0.9 },
      'Quiet': { spAttack: 1.1, speed: 0.9 },
      'Rash': { spAttack: 1.1, spDefense: 0.9 },
      'Calm': { spDefense: 1.1, attack: 0.9 },
      'Gentle': { spDefense: 1.1, defense: 0.9 },
      'Sassy': { spDefense: 1.1, speed: 0.9 },
      'Careful': { spDefense: 1.1, spAttack: 0.9 }
    };

    const effects = natureEffects[nature];
    return effects?.[stat] || 1.0;
  }

  /**
   * Select random ability from possible abilities
   */
  private randomAbility(abilities: string[]): string {
    if (!abilities || abilities.length === 0) return 'None';
    return abilities[Math.floor(Math.random() * abilities.length)];
  }

  /**
   * Calculate experience points for a given level
   */
  private experienceForLevel(level: number): number {
    // Medium Fast growth rate formula
    return Math.floor(Math.pow(level, 3));
  }

  /**
   * Set custom shiny chance (for shiny charm, etc.)
   */
  public setShinyChance(chance: number): void {
    this.shinyChance = chance;
  }
}
