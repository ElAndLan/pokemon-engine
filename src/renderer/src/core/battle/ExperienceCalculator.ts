import { PokemonInstance, PokemonSpecies, Stats } from '../data/DataTypes';

export class ExperienceCalculator {
  
  // Growth Rate: Medium Fast (Cubic)
  // XP = Level^3
  public static getExpForLevel(level: number): number {
    return Math.pow(level, 3);
  }

  public static getExpyield(species: PokemonSpecies): number {
    return species.expYield || 64; // Default
  }

  // Gen 7 Formula for Flat XP gain (Simplified)
  // XP = (BaseExp * Level) / 7
  public static calculateExpGain(defeated: PokemonInstance, species: PokemonSpecies): number {
    const a = 1; // Trainer bonus (1 = wild, 1.5 = trainer)
    const b = species.expYield;
    const L = defeated.level;
    const s = 1; // Exp Share (1 = participant)
    
    // Simplified Gen 5-like formula
    return Math.floor((b * L) / 7 * a * s);
  }

  public static getStat(base: number, iv: number, ev: number, level: number, natureMult: number = 1.0): number {
      // ((2 * Base + IV + (EV/4)) * Level / 100) + 5
      // Note: HP is different, but for simplicity/universality using this for now except HP
      return Math.floor((Math.floor((2 * base + iv + Math.floor(ev / 4)) * level / 100) + 5) * natureMult);
  }
  
  public static getHp(base: number, iv: number, ev: number, level: number): number {
      // ((2 * Base + IV + (EV/4)) * Level / 100) + Level + 10
      return Math.floor((2 * base + iv + Math.floor(ev / 4)) * level / 100) + level + 10;
  }

  public static recalculateStats(inst: PokemonInstance, stringSpecies: any): Stats {
      // Need the actual Species data passed in since Instance only has ID
      // We will assume 'stringSpecies' is the resolved PokemonSpecies object
      const s: PokemonSpecies = stringSpecies;
      
      return {
          hp: this.getHp(s.baseStats.hp, inst.ivs.hp, inst.evs.hp, inst.level),
          attack: this.getStat(s.baseStats.attack, inst.ivs.attack, inst.evs.attack, inst.level),
          defense: this.getStat(s.baseStats.defense, inst.ivs.defense, inst.evs.defense, inst.level),
          spAttack: this.getStat(s.baseStats.spAttack, inst.ivs.spAttack, inst.evs.spAttack, inst.level),
          spDefense: this.getStat(s.baseStats.spDefense, inst.ivs.spDefense, inst.evs.spDefense, inst.level),
          speed: this.getStat(s.baseStats.speed, inst.ivs.speed, inst.evs.speed, inst.level),
      };
  }
}
