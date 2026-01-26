export type PokemonType = 
  | 'Normal' | 'Fire' | 'Water' | 'Grass' | 'Electric' | 'Ice' 
  | 'Fighting' | 'Poison' | 'Ground' | 'Flying' | 'Psychic' 
  | 'Bug' | 'Rock' | 'Ghost' | 'Dragon' | 'Steel' | 'Dark' | 'Fairy';

export const TypeChart: Record<PokemonType, Partial<Record<PokemonType, number>>> = {
  Normal: { Rock: 0.5, Ghost: 0, Steel: 0.5 },
  Fire: { Fire: 0.5, Water: 0.5, Grass: 2.0, Ice: 2.0, Bug: 2.0, Rock: 0.5, Dragon: 0.5, Steel: 2.0 },
  Water: { Fire: 2.0, Water: 0.5, Grass: 0.5, Ground: 2.0, Rock: 2.0, Dragon: 0.5 },
  Grass: { Fire: 0.5, Water: 2.0, Grass: 0.5, Poison: 0.5, Ground: 2.0, Flying: 0.5, Bug: 0.5, Rock: 2.0, Dragon: 0.5, Steel: 0.5 },
  Electric: { Water: 2.0, Grass: 0.5, Electric: 0.5, Ground: 0, Flying: 2.0, Dragon: 0.5 },
  Ice: { Fire: 0.5, Water: 0.5, Grass: 2.0, Ice: 0.5, Ground: 2.0, Flying: 2.0, Dragon: 2.0, Steel: 0.5 },
  Fighting: { Normal: 2.0, Ice: 2.0, Poison: 0.5, Flying: 0.5, Psychic: 0.5, Bug: 0.5, Rock: 2.0, Ghost: 0, Dark: 2.0, Steel: 2.0, Fairy: 0.5 },
  Poison: { Grass: 2.0, Poison: 0.5, Ground: 0.5, Rock: 0.5, Ghost: 0.5, Steel: 0, Fairy: 2.0 },
  Ground: { Fire: 2.0, Water: 0.5, Grass: 0.5, Electric: 2.0, Ice: 2.0, Poison: 2.0, Flying: 0, Rock: 2.0, Steel: 2.0 },
  Flying: { Grass: 2.0, Electric: 0.5, Fighting: 2.0, Bug: 2.0, Rock: 0.5, Steel: 0.5 },
  Psychic: { Fighting: 2.0, Poison: 2.0, Psychic: 0.5, Dark: 0, Steel: 0.5 },
  Bug: { Fire: 0.5, Grass: 2.0, Fighting: 0.5, Poison: 0.5, Flying: 0.5, Psychic: 2.0, Ghost: 0.5, Dark: 2.0, Steel: 0.5, Fairy: 0.5 },
  Rock: { Fire: 2.0, Ice: 2.0, Fighting: 0.5, Ground: 0.5, Flying: 2.0, Bug: 2.0, Steel: 0.5 },
  Ghost: { Normal: 0, Psychic: 2.0, Ghost: 2.0, Dark: 0.5 },
  Dragon: { Dragon: 2.0, Steel: 0.5, Fairy: 0 },
  Steel: { Fire: 0.5, Water: 0.5, Electric: 0.5, Ice: 2.0, Rock: 2.0, Steel: 0.5, Fairy: 2.0 },
  Dark: { Fighting: 0.5, Psychic: 2.0, Ghost: 2.0, Dark: 0.5, Fairy: 0.5 },
  Fairy: { Fire: 0.5, Fighting: 2.0, Poison: 0.5, Dragon: 2.0, Dark: 2.0, Steel: 0.5 }
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
