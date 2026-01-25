import { Stats, StatName } from '../data/DataTypes';

export interface NatureModifiers {
  boosted?: StatName;
  hindered?: StatName;
}

export class StatCalculator {
  private static readonly NATURE_MODIFIERS: Record<string, NatureModifiers> = {
    'Lonely': { boosted: 'attack', hindered: 'defense' },
    'Brave': { boosted: 'attack', hindered: 'speed' },
    'Adamant': { boosted: 'attack', hindered: 'spAttack' },
    'Naughty': { boosted: 'attack', hindered: 'spDefense' },
    'Bold': { boosted: 'defense', hindered: 'attack' },
    'Relaxed': { boosted: 'defense', hindered: 'speed' },
    'Impish': { boosted: 'defense', hindered: 'spAttack' },
    'Lax': { boosted: 'defense', hindered: 'spDefense' },
    'Timid': { boosted: 'speed', hindered: 'attack' },
    'Hasty': { boosted: 'speed', hindered: 'defense' },
    'Jolly': { boosted: 'speed', hindered: 'spAttack' },
    'Naive': { boosted: 'speed', hindered: 'spDefense' },
    'Modest': { boosted: 'spAttack', hindered: 'attack' },
    'Mild': { boosted: 'spAttack', hindered: 'defense' },
    'Quiet': { boosted: 'spAttack', hindered: 'speed' },
    'Rash': { boosted: 'spAttack', hindered: 'spDefense' },
    'Calm': { boosted: 'spDefense', hindered: 'attack' },
    'Gentle': { boosted: 'spDefense', hindered: 'defense' },
    'Sassy': { boosted: 'spDefense', hindered: 'speed' },
    'Careful': { boosted: 'spDefense', hindered: 'spAttack' }
  };

  private static readonly NATURE_BOOST_MULTIPLIER = 1.1;
  private static readonly NATURE_HINDER_MULTIPLIER = 0.9;
  private static readonly NATURE_NEUTRAL_MULTIPLIER = 1.0;

  public static calculateHp(base: number, iv: number, ev: number, level: number): number {
    const evContribution = Math.floor(ev / 4);
    const baseValue = 2 * base + iv + evContribution;
    return Math.floor((baseValue * level) / 100) + level + 10;
  }

  public static calculateStat(base: number, iv: number, ev: number, level: number): number {
    const evContribution = Math.floor(ev / 4);
    const baseValue = 2 * base + iv + evContribution;
    return Math.floor((baseValue * level) / 100) + 5;
  }

  public static calculateStatWithNature(
    base: number,
    iv: number,
    ev: number,
    level: number,
    nature: string,
    statName: StatName
  ): number {
    const statValue = this.calculateStat(base, iv, ev, level);
    return Math.floor(statValue * this.getNatureMultiplier(nature, statName));
  }

  public static calculateAllStats(
    baseStats: Stats,
    ivs: Stats,
    evs: Stats,
    level: number,
    nature: string
  ): Stats {
    return {
      hp: this.calculateHp(baseStats.hp, ivs.hp, evs.hp, level),
      attack: this.calculateStatWithNature(baseStats.attack, ivs.attack, evs.attack, level, nature, 'attack'),
      defense: this.calculateStatWithNature(baseStats.defense, ivs.defense, evs.defense, level, nature, 'defense'),
      spAttack: this.calculateStatWithNature(baseStats.spAttack, ivs.spAttack, evs.spAttack, level, nature, 'spAttack'),
      spDefense: this.calculateStatWithNature(baseStats.spDefense, ivs.spDefense, evs.spDefense, level, nature, 'spDefense'),
      speed: this.calculateStatWithNature(baseStats.speed, ivs.speed, evs.speed, level, nature, 'speed')
    };
  }

  public static getNatureMultiplier(nature: string, statName: StatName): number {
    if (statName === 'hp' || statName === 'accuracy' || statName === 'evasion') {
      return this.NATURE_NEUTRAL_MULTIPLIER;
    }

    const modifiers = this.NATURE_MODIFIERS[nature];
    if (!modifiers) {
      return this.NATURE_NEUTRAL_MULTIPLIER;
    }

    if (modifiers.boosted === statName) {
      return this.NATURE_BOOST_MULTIPLIER;
    }

    if (modifiers.hindered === statName) {
      return this.NATURE_HINDER_MULTIPLIER;
    }

    return this.NATURE_NEUTRAL_MULTIPLIER;
  }

  public static getNatureModifiers(nature: string): NatureModifiers {
    return this.NATURE_MODIFIERS[nature] || {};
  }

  public static calculateStatIncreasePerLevel(
    base: number,
    iv: number,
    ev: number,
    currentLevel: number,
    isHp: boolean
  ): number {
    const currentStat = isHp
      ? this.calculateHp(base, iv, ev, currentLevel)
      : this.calculateStat(base, iv, ev, currentLevel);

    const nextLevel = currentLevel + 1;
    const nextStat = isHp
      ? this.calculateHp(base, iv, ev, nextLevel)
      : this.calculateStat(base, iv, ev, nextLevel);

    return nextStat - currentStat;
  }

  public static calculateStatIncreasePerLevelWithNature(
    base: number,
    iv: number,
    ev: number,
    currentLevel: number,
    nature: string,
    statName: StatName
  ): number {
    const currentStat = this.calculateStatWithNature(base, iv, ev, currentLevel, nature, statName);
    const nextLevel = currentLevel + 1;
    const nextStat = this.calculateStatWithNature(base, iv, ev, nextLevel, nature, statName);

    return nextStat - currentStat;
  }

  public static calculateAllStatIncreases(
    baseStats: Stats,
    ivs: Stats,
    evs: Stats,
    currentLevel: number,
    nature: string
  ): Partial<Stats> {
    return {
      hp: this.calculateStatIncreasePerLevel(baseStats.hp, ivs.hp, evs.hp, currentLevel, true),
      attack: this.calculateStatIncreasePerLevelWithNature(baseStats.attack, ivs.attack, evs.attack, currentLevel, nature, 'attack'),
      defense: this.calculateStatIncreasePerLevelWithNature(baseStats.defense, ivs.defense, evs.defense, currentLevel, nature, 'defense'),
      spAttack: this.calculateStatIncreasePerLevelWithNature(baseStats.spAttack, ivs.spAttack, evs.spAttack, currentLevel, nature, 'spAttack'),
      spDefense: this.calculateStatIncreasePerLevelWithNature(baseStats.spDefense, ivs.spDefense, evs.spDefense, currentLevel, nature, 'spDefense'),
      speed: this.calculateStatIncreasePerLevelWithNature(baseStats.speed, ivs.speed, evs.speed, currentLevel, nature, 'speed')
    };
  }
}
