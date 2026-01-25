import { PokemonInstance, PokemonSpecies, Stats } from '../data/DataTypes';
import { StatCalculator } from '../stat/StatCalculator';

export class ExperienceCalculator {
  
  // Growth Rate: Medium Fast (Cubic)
  // XP = Level^3
  public static getExpForLevel(level: number): number {
    return Math.pow(level, 3);
  }

  public static getExpyield(species: PokemonSpecies): number {
    return species.expYield || 64;
  }

  // Gen 7 Formula for Flat XP gain (Simplified)
  // XP = (BaseExp * Level) / 7
  public static calculateExpGain(defeated: PokemonInstance, species: PokemonSpecies): number {
    const a = 1;
    const b = species.expYield;
    const L = defeated.level;
    const s = 1;
    
    return Math.floor((b * L) / 7 * a * s);
  }

  public static recalculateStats(inst: PokemonInstance, stringSpecies: any): Stats {
      const s: PokemonSpecies = stringSpecies;
      return StatCalculator.calculateAllStats(s.baseStats, inst.ivs, inst.evs, inst.level, inst.nature);
  }
}
