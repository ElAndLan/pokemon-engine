export type ItemCategory = 'medicine' | 'pokeballs' | 'battle' | 'berries' | 'tms' | 'key';

export interface ItemEffect {
  type: 'heal_hp' | 'heal_pp' | 'heal_status' | 'revive' | 'stat_boost' | 'capture' | 'teach_move' | 'evolve';
  value?: number;       // HP amount, PP amount, stat stages, etc.
  target?: 'pokemon' | 'party' | 'wild';
  status?: string;      // For status healing (poison, burn, etc.)
  statType?: string;    // For stat boosts (attack, defense, etc.)
}

export interface ItemData {
  id: string;           // "potion", "pokeball", "tm01"
  name: string;         // "Potion"
  category: ItemCategory;
  description: string;
  buyPrice: number;
  sellPrice: number;
  effect?: string | ItemEffect;  // Can be a string description or structured object
  icon?: string;        // Path to icon image
  sprite?: string;      // URL to sprite image
  isKeyItem?: boolean;
  isTM?: boolean;
  tmMove?: string;      // Move ID if TM/HM
  isHM?: boolean;
  canUseInBattle?: boolean;
  canUseInOverworld?: boolean;
}

export interface BagItem {
  itemId: string;
  quantity: number;
}

export interface BagData {
  items: BagItem[];
}
