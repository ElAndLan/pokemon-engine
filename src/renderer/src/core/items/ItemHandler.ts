import type { PokemonInstance } from '../data/DataTypes';
import type { ItemData } from '../data/ItemData';
import { CaptureCalculator, CaptureContext } from '../battle/CaptureCalculator';
import { ExperienceCalculator } from '../battle/ExperienceCalculator';
import type { Game } from '../Game';
import { StatCalculator } from '../stat/StatCalculator';
import { MoveLearningManager } from '../battle/MoveLearningManager';

export type ItemUseContext = 'battle' | 'overworld';

export interface MoveToReplace {
    moveId: string;
    moveName: string;
}

export interface ItemUseResult {
  success: boolean;
  message: string;
  consumed: boolean; // Whether the item was consumed
  effects?: {
    hpRestored?: number;
    ppRestored?: number;
    statusCured?: string[];
    revived?: boolean;
    statChanges?: Record<string, number>;
    learnedMoves?: string[];
    movesToReplace?: MoveToReplace[];
    pokemonInstanceId?: string;
  };
  capture?: {
      caught: boolean;
      shakes: number;
      critical: boolean;
  };
}

/**
 * Handles item usage for both battle and overworld contexts
 */
export class ItemHandler {
  private game: Game;

  constructor(game: Game) {
    this.game = game;
  }

  /**
   * Get Pokemon display name (nickname or species name)
   */
  private getPokemonName(pokemon: PokemonInstance): string {
    if (pokemon.nickname) return pokemon.nickname;
    const species = this.game.dataManager.getPokemonSpecies(pokemon.speciesId);
    return species?.name || 'Pokemon';
  }

  /**
   * Use an item on a Pokemon
   */
  public useItem(
    itemId: string,
    targetPokemon: PokemonInstance,
    context: ItemUseContext
  ): ItemUseResult {
    const itemData = this.game.dataManager.getItem(itemId);
    
    if (!itemData) {
      return {
        success: false,
        message: 'Item not found',
        consumed: false
      };
    }

    // Check if item can be used in this context
    if (context === 'battle' && !itemData.canUseInBattle) {
      return {
        success: false,
        message: `${itemData.name} cannot be used in battle`,
        consumed: false
      };
    }

    if (context === 'overworld' && !itemData.canUseInOverworld) {
      return {
        success: false,
        message: `${itemData.name} cannot be used outside of battle`,
        consumed: false
      };
    }

    console.log(`[ItemHandler] Using item: ${itemData.name} (${itemData.id}) Category: ${itemData.category} Context: ${context}`);
    
    // Route to appropriate handler based on item category
    switch (itemData.category) {
      case 'medicine':
        return this.useMedicine(itemData, targetPokemon);
      case 'pokeballs':
        return this.usePokeball(itemData, targetPokemon, context);
      case 'battle':
        return this.useBattleItem(itemData, targetPokemon, context);
      case 'berries':
        return this.useBerry(itemData, targetPokemon);
      case 'tms':
        return this.useTM(itemData, targetPokemon);
      default:
        console.warn(`[ItemHandler] No handler for category: ${itemData.category}`);
        return {
          success: false,
          message: `${itemData.name} cannot be used this way`,
          consumed: false
        };
    }
  }

  /**
   * Use medicine items (potions, status heals, etc.)
   */
  private useMedicine(itemData: ItemData, pokemon: PokemonInstance): ItemUseResult {
    const effects: ItemUseResult['effects'] = {};
    let message = '';
    const pokemonName = this.getPokemonName(pokemon);

    // Parse effect from description - ensure it's a string
    const effectTextRaw = typeof itemData.effect === 'string' ? itemData.effect : itemData.description;
    const effectText = effectTextRaw.toLowerCase();
    
    console.log(`[ItemHandler] Processing Medicine: ${itemData.name} (${itemData.id}). Text: "${effectText}"`);

    // Redirect miscategorized berries
    if (itemData.id.endsWith('berry') || itemData.category === 'berries') {
        return this.useBerry(itemData, pokemon);
    }
    
    // --- SPECIAL CASES ---
    
    // Rare Candy / Level Up
    if (itemData.id === 'rare-candy' || effectText.includes('level up') || effectText.includes('raises the level')) {
        if (pokemon.level >= 100) {
            return {
                success: false,
                message: `You cannot raise a Pokemon's level above the max level.`,
                consumed: false
            };
        }
        
        const oldLevel = pokemon.level;
        const oldStats = { ...pokemon.currentStats };
        
        pokemon.level++;
        pokemon.experience = ExperienceCalculator.getExpForLevel(pokemon.level);
        
        const speciesData = this.game.dataManager.getPokemonSpecies(pokemon.speciesId);
        if (!speciesData) {
            return {
                success: false,
                message: 'Species data not found',
                consumed: false
            };
        }
        
        const newStats = ExperienceCalculator.recalculateStats(pokemon, speciesData);
        
        pokemon.currentStats = newStats;
        const hpDiff = newStats.hp - oldStats.hp;
        if (hpDiff > 0) pokemon.currentHp += hpDiff;
        
        const moves = this.game.dataManager.getAllMoves();
        const moveLearningManager = new MoveLearningManager(moves);
        
        const learnableMoves = moveLearningManager.getMovesLearnableAtLevel(speciesData, pokemon.level, pokemon);
        const learnedMoves: string[] = [];
        const movesToReplace: MoveToReplace[] = [];
        
        for (const learnableMove of learnableMoves) {
            const moveData = this.game.dataManager.getMove(learnableMove.moveId);
            if (moveData) {
                const result = moveLearningManager.learnMove(pokemon, learnableMove.moveId);
                if (result.learned) {
                    learnedMoves.push(moveData.name);
                } else if (result.reason === 'slots_full') {
                    movesToReplace.push({
                        moveId: learnableMove.moveId,
                        moveName: moveData.name
                    });
                }
            }
        }
        
        const statChanges: Record<string, number> = {
            hp: newStats.hp - oldStats.hp,
            attack: newStats.attack - oldStats.attack,
            defense: newStats.defense - oldStats.defense,
            spAttack: newStats.spAttack - oldStats.spAttack,
            spDefense: newStats.spDefense - oldStats.spDefense,
            speed: newStats.speed - oldStats.speed
        };
        
        const effects: ItemUseResult['effects'] = { 
            statChanges,
            learnedMoves: learnedMoves.length > 0 ? learnedMoves : undefined,
            pokemonInstanceId: pokemon.uuid
        };

        if (movesToReplace.length > 0) {
            effects.movesToReplace = movesToReplace;
        }

        return {
            success: true,
            message: `${pokemonName}'s level is raised by 1.`,
            consumed: true,
            effects
        };
    }

    // Revive
    if (effectText.includes('revive') || itemData.id.includes('revive')) {
      if (pokemon.currentHp > 0) {
        return {
          success: false,
          message: `Item would have no effect.`,
          consumed: false
        };
      }

      effects.revived = true;
      const maxHp = pokemon.currentStats.hp;
      
      // Max Revive or Revival Herb
      if (effectText.includes('full') || effectText.includes('all') || itemData.id === 'max-revive' || itemData.id === 'revival-herb') {
        pokemon.currentHp = maxHp;
        effects.hpRestored = maxHp;
      } else {
        // Standard Revive - Half HP
        pokemon.currentHp = Math.floor(maxHp / 2);
        effects.hpRestored = pokemon.currentHp;
      }
      
      pokemon.status = 'None';
      return {
          success: true,
          message: `${pokemonName} was revived!`,
          consumed: true,
          effects
      };
    }

    // --- STANDARD MEDICINE ---

    // HP Restoration
    // Check for "Restores 20 HP", "Restores the HP ... by 20 points", etc.
    let hpRestored = 0;
    
    if (itemData.id === 'full-restore' || itemData.id === 'max-potion' || effectText.includes('fully restores') || effectText.includes('restores hp to full')) {
        hpRestored = pokemon.currentStats.hp - pokemon.currentHp;
    } else if (effectText.includes('restores') || effectText.includes('heals') || effectText.includes('recovers')) {
        // Extract number ONLY if the text implies healing
        // "Restores 20 HP"
        const numbers = effectText.match(/(\d+)/);
        if (numbers) {
            hpRestored = parseInt(numbers[0]);
        }
    }

    if (hpRestored > 0) {
      if (pokemon.currentHp === 0) {
        return {
          success: false,
          message: `${pokemonName} has fainted. Use a Revive instead.`,
          consumed: false
        };
      }

      if (pokemon.currentHp === pokemon.currentStats.hp) {
         // Only block if it's purely an HP item. Full Restore cures status too.
         if (!effectText.includes('status') && !effectText.includes('heal all')) {
             return {
                success: false,
                message: `Item would have no effect.`,
                consumed: false
             };
         }
      } else {
          // Heal
          const actualRestored = Math.min(hpRestored, pokemon.currentStats.hp - pokemon.currentHp);
          pokemon.currentHp += actualRestored;
          effects.hpRestored = actualRestored;
          
          if (itemData.id === 'full-restore' || itemData.id === 'max-potion') {
              message = `${pokemonName}'s HP was fully restored!`;
          } else {
              message = `${pokemonName}'s health is restored by ${actualRestored} points!`;
          }
      }
    }

    // PP Restoration (Ether, Elixir)
    let ppRestored = 0;
    const isAllMoves = effectText.includes('all moves') || itemData.id.includes('elixir');
    
    if (itemData.id.includes('ether') || itemData.id.includes('elixir') || (effectText.includes('restore') && effectText.includes('pp'))) {
       if (itemData.id.includes('max') || effectText.includes('fully')) {
           ppRestored = 999;
       } else {
           ppRestored = 10;
       }
       
       // Note: Actual PP restore logic requires move selection for Ether/MaxEther
       // usage context 'overworld' usually implies checking moves.
       // For now, implementing Elixir (All moves) is easier.
       if (isAllMoves) {
           let totalRestored = 0;
           pokemon.moves.forEach(m => {
               const missing = m.maxPp - m.pp;
               if (missing > 0) {
                   const restore = Math.min(missing, ppRestored);
                   m.pp += restore;
                   totalRestored += restore;
               }
           });
           
           if (totalRestored > 0) {
               if (message) message += ' ';
               message += `Restored PP for all moves!`;
               effects.ppRestored = totalRestored;
           }
       } else {
           // TODO: Ether needs move selection context
           if (!message) {
               // Fallback if we can't select move yet - Try to restore first available move
               const move = pokemon.moves.find(m => m.pp < m.maxPp);
               if (move) {
                   const restore = Math.min(move.maxPp - move.pp, ppRestored);
                   move.pp += restore;
                   effects.ppRestored = restore;
                   message = `Restored ${restore} PP to ${move.moveId}!`;
               } else {
                   message = "PP is already full.";
               }
           }
       }
    }

    // PP Up / PP Max
    if (itemData.id === 'pp-up' || itemData.id === 'pp-max') {
        // Without move selection, try to boost the first move that isn't maxed
        // Limit is 1.6x base PP (3 stages of +20%)
        // We need base PP from registry
        let applied = false;
        
        for (const move of pokemon.moves) {
            const moveData = this.game.dataManager.getMove(move.moveId);
            if (!moveData) continue;
            
            const basePp = moveData.pp;
            const limit = Math.floor(basePp * 1.6);
            
            if (move.maxPp < limit) {
                let newMax = 0;
                if (itemData.id === 'pp-max') {
                    newMax = limit;
                } else {
                    // PP Up adds 20%
                    const boost = Math.max(1, Math.floor(basePp * 0.2));
                    newMax = Math.min(limit, move.maxPp + boost);
                }
                
                if (newMax > move.maxPp) {
                    const diff = newMax - move.maxPp;
                    move.maxPp = newMax;
                    move.pp += diff; // Usually current PP increases too
                    applied = true;
                    message = `${pokemonName}'s ${moveData.name} PP increased!`;
                    // consumed = true; // Handled by generic return
                    effects.statChanges = { ...effects.statChanges, pp: 1 }; // Fake stat change to signal success
                    break; // Only one move
                }
            }
        }
        
        if (!applied) {
             return {
                success: false,
                message: `It won't have any effect.`, // Already maxed or no moves
                consumed: false
             };
        }
    }
    
    // Status Healing
    const statusCured: string[] = [];
    
    const hasStatus = pokemon.status !== 'None';
    const hasConfusion = pokemon.volatile && pokemon.volatile['Confusion'] > 0;

    if (hasStatus || hasConfusion) {
      const curesAll = itemData.id === 'full-heal' || itemData.id === 'full-restore' || itemData.id === 'heal-powder' || itemData.id === 'lava-cookie' || itemData.id === 'old-gateau' || itemData.id === 'casteliacone' || itemData.id === 'lumiose-galette' || itemData.id === 'shalour-sable' || itemData.id === 'big-malasada' || itemData.id === 'pewter-crunchies' || itemData.id === 'rage-candy-bar' || effectText.includes('all status') || effectText.includes('any status');
      
      const curesPoison = itemData.id === 'antidote' || effectText.includes('poison');
      const curesBurn = itemData.id === 'burn-heal' || effectText.includes('burn');
      const curesFreeze = itemData.id === 'ice-heal' || effectText.includes('defrosts') || effectText.includes('thaws');
      const curesSleep = itemData.id === 'awakening' || effectText.includes('wakes') || effectText.includes('sleep');
      const curesParalysis = itemData.id === 'paralyze-heal' || effectText.includes('paralysis') || effectText.includes('paralyze');
      const curesConfusion = curesAll || effectText.includes('confusion'); 

      if (curesAll ||
          (curesPoison && pokemon.status === 'Poison') ||
          (curesBurn && pokemon.status === 'Burn') ||
          (curesFreeze && pokemon.status === 'Freeze') ||
          (curesSleep && pokemon.status === 'Sleep') ||
          (curesParalysis && pokemon.status === 'Paralysis')) {
        
        if (pokemon.status !== 'None') {
            statusCured.push(pokemon.status);
            const oldStatus = pokemon.status;
            pokemon.status = 'None';
            const suffix = message ? ` and was cured of ${oldStatus}!` : `${pokemonName} was cured of ${oldStatus}!`;
            message += suffix;
        }
      }
      
      // Separate check for confusion (can exist with other status or alone)
      if (curesConfusion && hasConfusion) {
          delete pokemon.volatile['Confusion'];
          statusCured.push('Confusion');
          const suffix = message ? " and snapped out of confusion!" : `${pokemonName} snapped out of confusion!`;
          message += suffix;
      }
      
      if (statusCured.length === 0 && hpRestored === 0) {
         // Only fail if it did nothing else (didn't heal HP)
        return {
          success: false,
          message: `Item would have no effect.`,
          consumed: false
        };
      }
    }

    // Vitamins (EVs)
    // Match "effort", "base points", or specific names + "raises"/description fallback
    const isVitamin = effectText.includes('effort') || 
                      effectText.includes('base points') || 
                      (itemData.description && itemData.description.includes('base points')) ||
                      ['hp-up', 'protein', 'iron', 'calcium', 'zinc', 'carbos'].includes(itemData.id);

    if (isVitamin) {
        console.log(`[ItemHandler] Vitamin Logic: ${itemData.name} ID:${itemData.id}`);
        const evCapPerStat = 255; 
        const evCapTotal = 510; // Standard Gen 3+
        
        // Ensure EVs exist
        if (!pokemon.evs) pokemon.evs = { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 };

        let evArgs: { stat: string, value: number, name: string } | null = null;

        if (itemData.id === 'hp-up' || effectText.includes('hp effort')) evArgs = { stat: 'hp', value: 10, name: 'HP' };
        else if (itemData.id === 'protein' || effectText.includes('attack effort')) evArgs = { stat: 'attack', value: 10, name: 'Attack' };
        else if (itemData.id === 'iron' || effectText.includes('defense effort')) evArgs = { stat: 'defense', value: 10, name: 'Defense' };
        else if (itemData.id === 'calcium' || effectText.includes('special attack effort')) evArgs = { stat: 'spAttack', value: 10, name: 'Sp. Atk' };
        else if (itemData.id === 'zinc' || effectText.includes('special defense effort')) evArgs = { stat: 'spDefense', value: 10, name: 'Sp. Def' };
        else if (itemData.id === 'carbos' || effectText.includes('speed effort')) evArgs = { stat: 'speed', value: 10, name: 'Speed' };

        if (evArgs) {
            const statKey = evArgs.stat as keyof typeof pokemon.evs;
            const currentEv = pokemon.evs[statKey] || 0;
            // Calculate Total EVs (Safely)
            const totalEvs = Object.values(pokemon.evs).reduce((a, b) => Number(a) + Number(b), 0);
            
            console.log(`[ItemHandler] EVs - Current: ${currentEv}/${evCapPerStat}, Total: ${totalEvs}/${evCapTotal}`);

            if (currentEv >= evCapPerStat || totalEvs >= evCapTotal) {
                 return {
                    success: false,
                    message: `Item had no effect. (EV Cap Reached: ${currentEv}/${evCapPerStat}, Total: ${totalEvs}/${evCapTotal})`,
                    consumed: false
                 };
            }
            
            // Check how much we can actually add without exceeding total cap
            let amountToAdd = evArgs.value;
            if (totalEvs + amountToAdd > evCapTotal) {
                amountToAdd = evCapTotal - totalEvs;
            }
            // And per stat cap (less relevant if per-stat cap is 255 and we only add 10, but good practice)
            if (currentEv + amountToAdd > evCapPerStat) {
                amountToAdd = evCapPerStat - currentEv;
            }
            
            if (amountToAdd <= 0) {
                 return {
                    success: false,
                    message: `Item had no effect. (EV Cap Calculation Resulted in 0 add)`,
                    consumed: false
                 };
            }
            
            pokemon.evs[statKey] = currentEv + amountToAdd;
            
            const species = this.game.dataManager.getPokemonSpecies(pokemon.speciesId);
            if (species) {
                const newStats = StatCalculator.calculateAllStats(
                    species.baseStats,
                    pokemon.ivs,
                    pokemon.evs,
                    pokemon.level,
                    pokemon.nature
                );
                
                const diff = newStats.hp - pokemon.currentStats.hp;
                if (diff > 0) pokemon.currentHp += diff;
                
                pokemon.currentStats = newStats;
            }
            
            return {
                success: true,
                message: `${pokemonName}'s ${evArgs.name} rose!`,
                consumed: true,
                effects: { statChanges: { [evArgs.stat]: amountToAdd } }
            };
        }
    }

    if (hpRestored === 0 && !effects.revived && !message && !effects.ppRestored) {
        
        // Handle "No Effect Information" or known fallthroughs
        if (effectText.includes('no effect information')) {
             return { success: false, message: `It won't have any effect.`, consumed: false };
        }

        // Quietly fail for Medicine items that didn't meet conditions (e.g. Full Heal on healthy)
        return {
            success: false,
            message: `It won't have any effect.`,
            consumed: false
        };
    }

    return {
      success: true,
      message,
      consumed: true,
      effects
    };
  }

  /**
   * Use Pokeball
   */
  private usePokeball(itemData: ItemData, pokemon: PokemonInstance, context: ItemUseContext): ItemUseResult {
    if (context !== 'battle') {
      return {
        success: false,
        message: 'Pokéballs can only be used in battle.',
        consumed: false
      };
    }

    // This would integrate with the battle system
    // We need to fetch species
    const species = this.game.dataManager.getPokemonSpecies(pokemon.speciesId);
    if (!species) {
         return {
            success: false,
            message: 'Error: Pokemon Species not found.',
            consumed: false
         };
    }
    
    // For now, assume basic context or mock it.
    // In a real integration, 'context' argument should probably carry battle state (turns, etc)
    // or we query the BattleSystem.
    // Assuming context strings are 'battle' or 'overworld' mostly. 
    // We might need to cast or expand ItemUseContext if we want to pass rich data.
    // But CaptureCalculator accepts optional context.
    
    // Construct simplified context
    const captureContext: CaptureContext = {
       // Mock values or derived from Game state if possible
       turnCount: 1, // Default
       isDark: false,
       isCave: false
    };
    
    const result = CaptureCalculator.calculateCapture(pokemon, species, itemData, captureContext);
    
    let message = `You threw a ${itemData.name}!`;
    if (result.caught) {
        message += ` Gotcha! ${pokemon.nickname || species.name} was caught!`;
    } else {
        message += ` Oh no! The Pokemon broke free!`;
    }

    return {
      success: true,
      message,
      consumed: true,
      capture: result
    };
  }

  /**
   * Use battle items (X Attack, Guard Spec, etc.)
   */
  private useBattleItem(itemData: ItemData, pokemon: PokemonInstance, context: ItemUseContext): ItemUseResult {
    if (context !== 'battle') {
      return {
        success: false,
        message: `${itemData.name} can only be used in battle.`,
        consumed: false
      };
    }

    const pokemonName = this.getPokemonName(pokemon);
    const effectText = (typeof itemData.effect === 'string' ? itemData.effect : itemData.description).toLowerCase();
    const effects: ItemUseResult['effects'] = { statChanges: {} };
    let message = '';

    // Apply changes
    const applyStage = (stat: string, amount: number) => {
        if (!pokemon.statStages) pokemon.statStages = {};
        const statKey = stat as keyof typeof pokemon.statStages;
        const current = pokemon.statStages[statKey] || 0; 
        if (current >= 6) return false;
        pokemon.statStages[statKey] = Math.min(current + amount, 6);
        // Track effect
        if (!effects.statChanges) effects.statChanges = {};
        effects.statChanges[stat] = amount;
        return true;
    };

    if (effectText.includes('attack') && !effectText.includes('special')) {
      if (applyStage('attack', 2)) message = `${pokemonName}'s Attack rose sharply!`; 
      else if (applyStage('attack', 1)) message = `${pokemonName}'s Attack rose!`;
      else return { success: false, message: `${pokemonName}'s Attack won't go higher!`, consumed: false };
    }
    
    // Parse stat boosts more generically
    let stat: string | null = null;
    let amount = 1;
    if (effectText.includes('sharply') || effectText.includes('drastically')) amount = 2; // X Items often +2 in new gens, but old descriptions might allow +1. 
    // Actually, simple detection:
    
    if (effectText.includes('defense') && !effectText.includes('special')) stat = 'defense';
    if (effectText.includes('speed')) stat = 'speed';
    if (effectText.includes('sp. atk') || effectText.includes('special attack')) stat = 'spAttack';
    if (effectText.includes('sp. def') || effectText.includes('special defense')) stat = 'spDefense';
    if (effectText.includes('accuracy')) stat = 'accuracy';
    
    if (itemData.id === 'dire-hit' || effectText.includes('critical')) {
       // Crit is special
       stat = 'crit'; 
    }

    if (stat === 'crit') {
        // Handle Crit
        if (!pokemon.volatile) pokemon.volatile = {};
        if (pokemon.volatile['FocusEnergy']) {
             return { success: false, message: `It won't have any effect.`, consumed: false };
        }
        pokemon.volatile['FocusEnergy'] = 1; // Flag
        message = `${pokemonName} is getting pumped!`;
        
    } else if (itemData.id === 'guard-spec' || effectText.includes('prevents stat reduction')) {
        // Guard Spec
        if (!pokemon.volatile) pokemon.volatile = {};
        if (pokemon.volatile['Mist']) {
             return { success: false, message: `It won't have any effect.`, consumed: false };
        }
        pokemon.volatile['Mist'] = 5;
        message = `${pokemonName} is protected from stat reduction!`;
        
    } else if (stat) {
        // We know stat is explicitly set to valid keys above or null
        // Force cast to keyof statStages which is Partial<Record<StatName, number>>
        // Actually pokemon.statStages is { [key in StatName]?: number }
        // We can just cast to string which is allowed for indexing into it if it was Record<string, number>
        // But it's restricted. So we need 'as keyof typeof pokemon.statStages' or 'as any' (since line 584 uses any in previous fix attempt which failed).
        // Let's use 'as any' again but be cleaner or check previous error.
        // Previous error: "expression of type 'any' can't be used to index type". 
        // This is weird. 'any' SHOULD be usable.
        // Unless strict settings prevent it.
        // Let's try casting to 'keyof typeof pokemon.statStages'.
        // But pokemon.statStages is optional. 
        // Let's use `(stat as keyof PokemonInstance['currentStats'])` (assuming Stats interface matches).
        // Or just suppress lint if it works in runtime. 
        // The error suggests type 'any' cannot index the type. So the KEY cannot be any.
        // The key MUST be 'hp' | 'attack' etc.
        // So we must cast stat to that union.
        // `const statKey = stat as 'attack' | 'defense' | 'speed' | 'spAttack' | 'spDefense' | 'accuracy' | 'evasion';`
        // That's verbose. 
        // Use `stat as keyof typeof pokemon.currentStats` (assuming Stats keys match).
        
        const statKey = stat as keyof typeof pokemon.currentStats; 
        const current = pokemon.statStages?.[statKey] || 0;
        if (current < 6) {
            applyStage(stat, amount); // Use helper to update effects
            message = `${pokemonName}'s ${stat} rose!`;
        } else {
             return { success: false, message: `${pokemonName}'s ${stat} won't go higher!`, consumed: false };
        }
    }

    if (!message) {
      return {
        success: false,
        message: `${itemData.name} had no effect.`,
        consumed: false
      };
    }

    return {
      success: true,
      message,
      consumed: true,
      effects
    };
  }

  /**
   * Use berry
   */
  private useBerry(itemData: ItemData, pokemon: PokemonInstance): ItemUseResult {
    const pokemonName = this.getPokemonName(pokemon);
    // Parse effect from description - ensure it's a string
    const effectText = (typeof itemData.effect === 'string' ? itemData.effect : itemData.description).toLowerCase();
    
    // HP Restoration
    if (effectText.includes('hp') || effectText.includes('restore health')) {
      const maxHp = pokemon.currentStats.hp;
      if (pokemon.currentHp === maxHp) {
        return {
          success: false,
          message: `${pokemonName}'s HP is already full.`,
          consumed: false
        };
      }

      const hpRestored = Math.floor(maxHp / 4); // Most berries restore 1/4 HP
      pokemon.currentHp = Math.min(pokemon.currentHp + hpRestored, maxHp);
      
      return {
        success: true,
        message: `${pokemonName} restored ${hpRestored} HP!`,
        consumed: true,
        effects: { hpRestored }
      };
    }

    // Status Cure
    if (pokemon.status !== 'None') {
        let cured = false;
        if ((itemData.id === 'cheri-berry' || effectText.includes('paralysis')) && pokemon.status === 'Paralysis') cured = true;
        if ((itemData.id === 'chesto-berry' || effectText.includes('sleep')) && pokemon.status === 'Sleep') cured = true;
        if ((itemData.id === 'pecha-berry' || effectText.includes('poison')) && pokemon.status === 'Poison') cured = true;
        if ((itemData.id === 'rawst-berry' || effectText.includes('burn')) && pokemon.status === 'Burn') cured = true;
        if ((itemData.id === 'aspear-berry' || effectText.includes('freeze')) && pokemon.status === 'Freeze') cured = true;
        if ((itemData.id === 'lum-berry' || effectText.includes('all status') || effectText.includes('any status'))) cured = true;

        if (cured) {
            const oldStatus = pokemon.status;
            pokemon.status = 'None';
            return {
                success: true,
                message: `${pokemonName} was cured of ${oldStatus}!`,
                consumed: true,
                effects: { statusCured: [oldStatus] }
            };
        }
    }
    
    // Confusion (Persim, etc)
    const hasConfusion = pokemon.volatile && pokemon.volatile['Confusion'] > 0;
    if (hasConfusion) {
        if (itemData.id === 'persim-berry' || itemData.id === 'lum-berry' || effectText.includes('confusion')) {
            delete pokemon.volatile['Confusion'];
            return {
                success: true,
                message: `${pokemonName} snapped out of confusion!`,
                consumed: true,
                effects: { statusCured: ['Confusion'] }
            };
        }
    }

    // PP Restoration (Leppa Berry)
    if (itemData.id === 'leppa-berry' || (effectText.includes('restore') && effectText.includes('pp'))) {
        // Restore 10 PP to a move
        // In battle, this restores the move at 0. In overworld, it selects?
        // Basic logic: Restore first move with missing PP
        let restored = false;
        for (const move of pokemon.moves) {
            if (move.pp < move.maxPp) {
                const amount = 10;
                const actual = Math.min(move.maxPp - move.pp, amount);
                move.pp += actual;
                restored = true;
                return {
                    success: true,
                    message: `${pokemonName} restored ${actual} PP to ${move.moveId}!`, // Ideally simplify name
                    consumed: true,
                    effects: { ppRestored: actual }
                };
            }
        }
        if (!restored) {
             return {
                success: false,
                message: `${pokemonName}'s PP is fully charged.`,
                consumed: false
            };
        }
    }
    


    return {
      success: true,
      message: `${pokemonName} ate the ${itemData.name}!`,
      consumed: true
    };
  }

  /**
   * Use TM/HM
   */
  private useTM(itemData: ItemData, pokemon: PokemonInstance): ItemUseResult {
    // TMs would teach moves - this requires move learning system
    return {
      success: false,
      message: 'TM/HM usage not yet implemented.',
      consumed: false
    };
  }

  /**
   * Check if an item can be used on a Pokemon in the current context
   */
  public canUseItem(itemId: string, pokemon: PokemonInstance, context: ItemUseContext): boolean {
    const itemData = this.game.dataManager.getItem(itemId);
    
    if (!itemData) return false;

    if (context === 'battle' && !itemData.canUseInBattle) return false;
    if (context === 'overworld' && !itemData.canUseInOverworld) return false;

    // Additional checks based on item type
    if (itemData.category === 'medicine') {
      const effectText = typeof itemData.effect === 'string' ? itemData.effect : itemData.description;
      const maxHp = pokemon.currentStats.hp;
      
      // Can't use HP items on fainted Pokemon (except Revive)
      if (effectText.includes('HP') && !effectText.includes('Revive') && pokemon.currentHp === 0) {
        return false;
      }

      // Can't use Revive on non-fainted Pokemon
      if (effectText.includes('Revive') && pokemon.currentHp > 0) {
        return false;
      }

      // Can't use if HP is already full
      if (effectText.includes('HP') && pokemon.currentHp === maxHp) {
        return false;
      }
    }

    return true;
  }
}
