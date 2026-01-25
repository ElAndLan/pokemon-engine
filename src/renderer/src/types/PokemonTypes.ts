// Type Definitions for Pokemon Engine

export interface PokemonStats {
  hp: number;
  attack: number;
  defense: number;
  spAttack: number;
  spDefense: number;
  speed: number;
}

export interface PokemonIVs extends PokemonStats {}
export interface PokemonEVs extends PokemonStats {}

export interface PokemonMove {
  level: number;
  moveId: string;
}

export interface EvolutionData {
  targetSpeciesId: string;
  level?: number;
  item?: string;
  trade?: boolean;
  friendship?: number;
  timeOfDay?: string;
  beauty?: number;
  knownMove?: string;
}

export interface PokemonEvolution {
  preEvolutionId?: string;
  next?: EvolutionData[];
}

export interface PokemonAssets {
  front: string;
  back: string;
  icon: string;
  overworld: string;
  shinyFront: string;
  shinyBack: string;
}

export interface PokemonSpecies {
  id: string;
  name: string;
  types: string[];
  baseStats: PokemonStats;
  learnset: PokemonMove[];
  evolution: PokemonEvolution;
  possibleAbilities: string[];
  catchRate: number;
  expYield: number;
  assets: PokemonAssets;
}

export interface PokemonInstance {
  id: string; // Instance UUID or Species ID (Legacy)
  speciesId: string; // Authoritative Species ID
  name: string;
  nickname?: string;
  level: number;
  types: string[];
  baseStats: PokemonStats;
  currentStats: PokemonStats;
  ivs: PokemonIVs;
  evs: PokemonEVs;
  nature: string;
  moves: string[];
  currentHp: number;
  maxHp: number;
  isShiny: boolean;
  ability: string;
  experience: number;
  friendship: number;
  status: string | null;
  isWild: boolean;
}

export interface EncounterEntry {
  pokemonId: string;
  level?: [number, number]; // [min, max]
  minLevel?: number;
  maxLevel?: number;
  levelMin?: number; // Alias
  levelMax?: number; // Alias
  rate?: number;
  weight?: number; // Alias for rate
}

export interface StaticEncounter {
  id: string;
  pokemonId: string;
  level: number;
  position: { x: number; y: number };
  respawn: boolean;
  respawnTime?: number;
  shinyLocked: boolean;
  requiresEvent?: string;
}

export interface RouteEncounters {
  name: string;
  grass?: EncounterEntry[];
  tallGrass?: EncounterEntry[];
  darkGrass?: EncounterEntry[];
  cave?: EncounterEntry[];
  surf?: EncounterEntry[];
  diving?: EncounterEntry[];
  oldRod?: EncounterEntry[];
  goodRod?: EncounterEntry[];
  superRod?: EncounterEntry[];
  safari?: EncounterEntry[];
  headbutt?: EncounterEntry[];
  rockSmash?: EncounterEntry[];
  swarm?: EncounterEntry[];
  pokeRadar?: EncounterEntry[];
  rustlingGrass?: EncounterEntry[];
  shakingGrass?: EncounterEntry[];
  sweetScent?: {
    grass?: EncounterEntry[];
    surf?: EncounterEntry[];
    darkGrass?: EncounterEntry[];
  };
  static?: StaticEncounter[];
}

export interface EncountersData {
  [routeId: string]: RouteEncounters;
}

export interface MoveData {
  id: string;
  name: string;
  type: string;
  category: string;
  power: number;
  accuracy: number;
  pp: number;
  priority: number;
  target: string;
  effects: any[];
  description: string;
}

export interface MovesData {
  [moveId: string]: MoveData;
}
