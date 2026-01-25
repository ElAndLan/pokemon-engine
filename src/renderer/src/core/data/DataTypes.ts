export type PokemonType = 'Normal' | 'Fire' | 'Water' | 'Grass' | 'Electric' | 'Ice' | 'Fighting' | 'Poison' | 'Ground' | 'Flying' | 'Psychic' | 'Bug' | 'Rock' | 'Ghost' | 'Dragon' | 'Steel' | 'Dark' | 'Fairy';

export type StatusCondition = 'None' | 'Poison' | 'Burn' | 'Sleep' | 'Paralysis' | 'Freeze';

export interface Stats {
  hp: number;
  attack: number;
  defense: number;
  spAttack: number;
  spDefense: number;
  speed: number;
  accuracy?: number; // Treat as stage 0 default
  evasion?: number;
}

export type StatName = keyof Stats;

// 4. Learnset Item
export interface LearnMove {
  level: number;
  moveId: string;
}

// 3. Evolution Definition
export interface Evolution {
  targetSpeciesId: string;
  level?: number;
  item?: string;
  condition?: string; // e.g. "Trade", "Friendship"
}

// Static Data (The Species Definition)
// JSON file: "data/pokemon/{id}.json"
export interface PokemonSpecies {
  id: string; // e.g. "bulbasaur"
  name: string; // "Bulbasaur"
  types: PokemonType[];
  baseStats: Stats;
  evolution?: {
      preEvolutionId?: string;
      next?: Evolution[];
  };
  learnset: LearnMove[]; // All potential moves
  possibleAbilities: string[]; // e.g. ["Overgrow", "Chlorophyll"]
  catchRate: number;
  expYield: number;
  assets: {
      front: string; // Battle Front (Static or GIF)
      back: string; // Battle Back
      shinyFront?: string; 
      shinyBack?: string;
      icon: string; // Menu Icon
      overworld: string; // Path to 4-Directional Sprite Sheet (e.g. 4x4 grid)
  };
}

// 5. Instance Move (The move a Pokemon actually has)
export interface MoveInstance {
  moveId: string;
  pp: number; // Current PP
  maxPp: number; // Max PP (can be boosted)
}

// Dynamic Data (The Actual Caught Pokemon)
export interface PokemonInstance {
  uuid: string; // Unique ID for this specific pokemon
  speciesId: string; // Link to Species Data
  nickname?: string;
  types: PokemonType[]; // Cache types for battle comparison (STAB, Weakness)
  
  // 9. Original Trainer
  originalTrainer: string;
  
  // Growth
  level: number;
  experience: number;
  
  // 7. Data
  ivs: Stats;
  evs: Stats;
  nature: string; // e.g. "Adamant" (+Atk, -SpAtk)
  ability: string;
  gender: 'Male' | 'Female' | 'Genderless';
  shiny: boolean;
  
  // 5. Moves
  moves: MoveInstance[]; // Max 4
  
  // 6. Current Combat Stats (Calculated)
  currentHp: number;
  currentStats: Stats; // Result of Base + IV + EV + Level + Nature
  statStages: { [key in StatName]?: number }; // -6 to +6
  
  // 8. Status
  status: StatusCondition;
  // Volatile Status (Confusion, Flinch, etc) - Key: StatusName, Value: Turns Remaining (or 1 for indefinite/flag)
  volatile: Record<string, number>;
  heldItem?: string;
  
  // Battle Memory
  lastMoveUsed?: string;
  disabledMoveId?: string;
}

// Move Definition (Static)
export type MoveCategory = 'Physical' | 'Special' | 'Status';
export type MoveTarget = 'Self' | 'SelectedEnemy' | 'AllEnemies' | 'RandomEnemy' | 'Field';

export interface MoveEffect {
  type: 'Damage' | 'Status' | 'StatChange' | 'Heal' | 'Unique';
  // Status
  status?: StatusCondition;
  volatileStatus?: 'Confusion' | 'Flinch' | 'Bound' | 'LeechSeed' | 'Nightmare' | 'Curse';
  volatileChance?: number; 
  chance?: number; // 0-100
  // StatChange
  stat?: keyof Stats | 'all' | 'accuracy' | 'evasion'; 
  stages?: number; // -6 to +6
  // Heal
  healPercent?: number;
}

export interface MoveData {
  id: string;
  name: string;
  type: PokemonType; 
  category: MoveCategory;
  power: number;
  accuracy: number; 
  pp: number;
  priority: number;
  target: MoveTarget;
  effects: MoveEffect[];
  // Requirements
  reqTargetStatus?: StatusCondition; // e.g. Dream Eater needs 'Sleep'
  
  // Mechanics (Missing from original spec)
  drainPercent?: number; // % of damage dealt to heal
  recoil?: { type: 'Damage' | 'MaxHP', percent: number }; // 25% of Damage or 50% of MaxHP (Struggle/Mind Blown)
  multiHit?: { min: number, max: number }; // e.g. 2-5
  critRate?: number; // 0 (default), 1 (high), etc

  flags?: {
      charge?: boolean; // 1-turn charge, 2nd turn attack
      recharge?: boolean; // Attack first, skip next turn
      invulnerable?: 'Fly' | 'Dig' | 'Dive' | 'Bounce' | 'ShadowForce' | 'SkyDrop' | 'PhantomForce'; // Charge + Invulnerable
  };

  description: string;
}

export interface PokedexRegistry {
    [id: string]: PokemonSpecies;
}

export interface MoveRegistry {
    [id: string]: MoveData;
}

// Helper to get multiplier
export function getStatStageMultiplier(stage: number): number {
    const s = Math.max(-6, Math.min(6, stage));
    if (s === -6) return 0.25;
    if (s === -5) return 0.28;
    if (s === -4) return 0.33;
    if (s === -3) return 0.40;
    if (s === -2) return 0.50;
    if (s === -1) return 0.66;
    if (s === 0) return 1.0;
    if (s === 1) return 1.5;
    if (s === 2) return 2.0;
    if (s === 3) return 2.5;
    if (s === 4) return 3.0;
    if (s === 5) return 3.5;
    if (s === 6) return 4.0;
    return 1.0;
}

export function getEffectiveStat(mon: PokemonInstance, stat: StatName): number {
    let value = mon.currentStats[stat] || 0;
    
    // Apply Stage Multiplier
    const stage = mon.statStages?.[stat] || 0;
    value *= getStatStageMultiplier(stage);
    
    // Status Modifiers (Spec 4.2)
    if (stat === 'attack' && mon.status === 'Burn') {
        value *= 0.5;
    }
    
    if (stat === 'speed' && mon.status === 'Paralysis') {
        value *= 0.5;
    }

    return Math.floor(value);
}
