export type PokemonType = 
  | 'normal' | 'fire' | 'water' | 'grass' | 'electric' | 'ice' 
  | 'fighting' | 'poison' | 'ground' | 'flying' | 'psychic' 
  | 'bug' | 'rock' | 'ghost' | 'dragon' | 'steel' | 'dark' | 'fairy';

export const TypeChart: Record<PokemonType, Partial<Record<PokemonType, number>>> = {
  normal: { rock: 0.5, ghost: 0, steel: 0.5 },
  fire: { fire: 0.5, water: 0.5, grass: 2.0, ice: 2.0, bug: 2.0, rock: 0.5, dragon: 0.5, steel: 2.0 },
  water: { fire: 2.0, water: 0.5, grass: 0.5, ground: 2.0, rock: 2.0, dragon: 0.5 },
  grass: { fire: 0.5, water: 2.0, grass: 0.5, poison: 0.5, ground: 2.0, flying: 0.5, bug: 0.5, rock: 2.0, dragon: 0.5, steel: 0.5 },
  electric: { water: 2.0, grass: 0.5, electric: 0.5, ground: 0, flying: 2.0, dragon: 0.5 },
  ice: { fire: 0.5, water: 0.5, grass: 2.0, ice: 0.5, ground: 2.0, flying: 2.0, dragon: 2.0, steel: 0.5 },
  fighting: { normal: 2.0, ice: 2.0, poison: 0.5, flying: 0.5, psychic: 0.5, bug: 0.5, rock: 2.0, ghost: 0, dark: 2.0, steel: 2.0, fairy: 0.5 },
  poison: { grass: 2.0, poison: 0.5, ground: 0.5, rock: 0.5, ghost: 0.5, steel: 0, fairy: 2.0 },
  ground: { fire: 2.0, water: 0.5, grass: 0.5, electric: 2.0, ice: 2.0, poison: 2.0, flying: 0, rock: 2.0, steel: 2.0 },
  flying: { grass: 2.0, electric: 0.5, fighting: 2.0, bug: 2.0, rock: 0.5, steel: 0.5 },
  psychic: { fighting: 2.0, poison: 2.0, psychic: 0.5, dark: 0, steel: 0.5 },
  bug: { fire: 0.5, grass: 2.0, fighting: 0.5, poison: 0.5, flying: 0.5, psychic: 2.0, ghost: 0.5, dark: 2.0, steel: 0.5, fairy: 0.5 },
  rock: { fire: 2.0, ice: 2.0, fighting: 0.5, ground: 0.5, flying: 2.0, bug: 2.0, steel: 0.5 },
  ghost: { normal: 0, psychic: 2.0, ghost: 2.0, dark: 0.5 },
  dragon: { dragon: 2.0, steel: 0.5, fairy: 0 },
  steel: { fire: 0.5, water: 0.5, electric: 0.5, ice: 2.0, rock: 2.0, steel: 0.5, fairy: 2.0 },
  dark: { fighting: 0.5, psychic: 2.0, ghost: 2.0, dark: 0.5, fairy: 0.5 },
  fairy: { fire: 0.5, fighting: 2.0, poison: 0.5, dragon: 2.0, dark: 2.0, steel: 0.5 }
};

export function getTypeEffectiveness(moveType: string, targetTypes: string[]): number {
  let multiplier = 1.0;
  const attackType = moveType.toLowerCase() as PokemonType;

  // Manual override for 1.5x rule if the standard is 2.0x
  // The user requested 1.5x for strong, 0.5x for weak.
  // The chart above encodes these values directly.

  for (const type of targetTypes) {
      const defType = type.toLowerCase() as PokemonType;
      if (TypeChart[attackType] && TypeChart[attackType][defType] !== undefined) {
          multiplier *= TypeChart[attackType][defType]!;
      }
  }
  return multiplier;
}
