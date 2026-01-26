import { PokemonInstance, PokemonSpecies, Stats } from '../data/DataTypes';
import { ExperienceCalculator } from './ExperienceCalculator';
import { StatCalculator } from '../stat/StatCalculator';

export interface EvolutionCondition {
  level?: number;
  item?: string;
  trade?: boolean;
  friendship?: number;
  timeOfDay?: 'day' | 'night';
  knownMove?: string;
  heldItem?: string;
  beauty?: number;
}

export interface EvolutionData {
  targetSpeciesId: string;
  condition: EvolutionCondition;
}

export interface EvolutionResult {
  canEvolve: boolean;
  evolutionData?: EvolutionData;
  reason?: string;
}

export class EvolutionManager {
  private pokemonData: Map<string, PokemonSpecies>;

  constructor(pokemonData: Map<string, PokemonSpecies>) {
    this.pokemonData = pokemonData;
  }

  public checkEvolution(pokemon: PokemonInstance, context?: {
    timeOfDay?: 'day' | 'night';
    heldItem?: string;
    usedItem?: string;
    isTrading?: boolean;
    knownMove?: string;
  }): EvolutionResult {
    const species = this.pokemonData.get(pokemon.speciesId);
    
    if (!species || !species.evolution || !species.evolution.next) {
      return { canEvolve: false, reason: 'No evolution available' };
    }

    const possibleEvolutions = species.evolution.next;

    for (const evo of possibleEvolutions) {
      const condition: EvolutionCondition = {};
      
      if (evo.level !== undefined) condition.level = evo.level;
      if (evo.item !== undefined) condition.item = evo.item;
      if (evo.trade !== undefined) condition.trade = evo.trade;
      if (evo.friendship !== undefined) condition.friendship = evo.friendship;
      if (evo.timeOfDay !== undefined) condition.timeOfDay = evo.timeOfDay;
      if (evo.knownMove !== undefined) condition.knownMove = evo.knownMove;
      if (evo.heldItem !== undefined) condition.heldItem = evo.heldItem;
      if (evo.beauty !== undefined) condition.beauty = evo.beauty;

      if (this.checkCondition(pokemon, condition, context)) {
        return {
          canEvolve: true,
          evolutionData: {
            targetSpeciesId: evo.targetSpeciesId,
            condition
          }
        };
      }
    }

    return { canEvolve: false, reason: 'Conditions not met' };
  }

  private checkCondition(pokemon: PokemonInstance, condition: EvolutionCondition, context?: any): boolean {
    if (condition.level !== undefined) {
      if (pokemon.level < condition.level) {
        return false;
      }
    }

    if (condition.item !== undefined) {
      if (!context || context.usedItem !== condition.item) {
        return false;
      }
    }

    if (condition.trade) {
      if (!context || !context.isTrading) {
        return false;
      }
    }

    if (condition.friendship !== undefined) {
      const friendship = this.getFriendship(pokemon);
      if (friendship < condition.friendship) {
        return false;
      }
    }

    if (condition.timeOfDay !== undefined) {
      if (!context || context.timeOfDay !== condition.timeOfDay) {
        return false;
      }
    }

    if (condition.knownMove !== undefined) {
      const hasMove = pokemon.moves.some(m => m.moveId === condition.knownMove);
      if (!hasMove) {
        return false;
      }
    }

    if (condition.heldItem !== undefined) {
      if (pokemon.heldItem !== condition.heldItem) {
        return false;
      }
    }

    if (condition.beauty !== undefined) {
      const beauty = this.getBeauty(pokemon);
      if (beauty < condition.beauty) {
        return false;
      }
    }

    return true;
  }

  public evolvePokemon(pokemon: PokemonInstance, targetSpeciesId: string): PokemonInstance {
    const targetSpecies = this.pokemonData.get(targetSpeciesId);
    
    if (!targetSpecies) {
      throw new Error(`Species ${targetSpeciesId} not found`);
    }

    const oldSpecies = this.pokemonData.get(pokemon.speciesId);
    if (!oldSpecies) {
      throw new Error(`Current species ${pokemon.speciesId} not found`);
    }

    pokemon.speciesId = targetSpeciesId;
    pokemon.types = targetSpecies.types;

    const newStats = ExperienceCalculator.recalculateStats(pokemon, targetSpecies);
    pokemon.currentStats = newStats;

    const hpRatio = pokemon.currentHp / (oldSpecies.baseStats.hp + 100);
    pokemon.currentHp = Math.floor(targetSpecies.baseStats.hp + 100 * hpRatio);
    if (pokemon.currentHp > newStats.hp) {
      pokemon.currentHp = newStats.hp;
    }

    const possibleAbilities = targetSpecies.possibleAbilities;
    const currentAbilityIndex = oldSpecies.possibleAbilities.indexOf(pokemon.ability);
    
    if (currentAbilityIndex >= 0 && currentAbilityIndex < possibleAbilities.length) {
      pokemon.ability = possibleAbilities[currentAbilityIndex];
    } else {
      pokemon.ability = possibleAbilities[0];
    }

    pokemon.moves = this.recalculateMoves(pokemon, targetSpecies);

    console.log(`[EvolutionManager] ${pokemon.nickname || oldSpecies.name} evolved into ${targetSpecies.name}!`);

    return pokemon;
  }

  private recalculateMoves(pokemon: PokemonInstance, species: PokemonSpecies): any[] {
    const learnset = species.learnset;
    const availableMoves = learnset.filter(move => move.level <= pokemon.level);
    
    const moves: any[] = [...pokemon.moves];
    
    for (const learnMove of availableMoves) {
      const alreadyHas = moves.some(m => m.moveId === learnMove.moveId);
      if (!alreadyHas) {
        if (moves.length < 4) {
          moves.push({
            moveId: learnMove.moveId,
            pp: this.getBasePP(learnMove.moveId),
            maxPp: this.getBasePP(learnMove.moveId)
          });
        }
      }
    }

    if (moves.length > 4) {
      moves.sort((a, b) => b.maxPp - a.maxPp);
      return moves.slice(0, 4);
    }

    return moves;
  }

  private getBasePP(moveId: string): number {
    return 15;
  }

  private getFriendship(pokemon: PokemonInstance): number {
    return 70;
  }

  private getBeauty(pokemon: PokemonInstance): number {
    return 0;
  }

  public getPossibleEvolutions(pokemon: PokemonInstance): EvolutionData[] {
    const species = this.pokemonData.get(pokemon.speciesId);
    
    if (!species || !species.evolution || !species.evolution.next) {
      return [];
    }

    return species.evolution.next.map(evo => {
      const condition: EvolutionCondition = {};
      
      if (evo.level !== undefined) condition.level = evo.level;
      if (evo.item !== undefined) condition.item = evo.item;
      if (evo.trade !== undefined) condition.trade = evo.trade;
      if (evo.friendship !== undefined) condition.friendship = evo.friendship;
      if (evo.timeOfDay !== undefined) condition.timeOfDay = evo.timeOfDay;
      if (evo.knownMove !== undefined) condition.knownMove = evo.knownMove;
      if (evo.heldItem !== undefined) condition.heldItem = evo.heldItem;
      if (evo.beauty !== undefined) condition.beauty = evo.beauty;

      return {
        targetSpeciesId: evo.targetSpeciesId,
        condition
      };
    });
  }
}
