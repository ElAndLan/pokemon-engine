import { Display } from '../Display';
import { Game } from '../Game';
import { PokemonInstance, MoveData } from '../data/DataTypes';
import { InputManager } from '../InputManager';
import { DataManager } from '../data/DataManager';
import { calculateDamage } from './DamageCalculator';
import { ExperienceCalculator } from './ExperienceCalculator';
import { StatCalculator } from '../stat/StatCalculator';
import { MoveEngine } from './MoveEngine';
import { MoveExecutionResult } from './MoveEngineTypes';
import { Stats } from '../data/DataTypes';
import { BattleAI } from './BattleAI';
import { PartyScreen } from '../ui/PartyScreen';
import { BagMenu } from '../ui/BagMenu';
import { AbilityRegistry } from './Abilities'; // New Import
import { MoveReplacementMenu } from '../ui/MoveReplacementMenu';
import { MoveLearningManager, LearnableMove, MoveLearningResult } from './MoveLearningManager';
import { EvolutionManager } from './EvolutionManager';
import type { ItemUseResult } from '../items/ItemHandler';

export class BattleScene {
  private dataManager: DataManager;
  private battleAI!: BattleAI;
  private moveLearningManager: MoveLearningManager;
  private partyScreen: PartyScreen | null = null;
  private moveReplacementMenu: MoveReplacementMenu | null = null;
  private playerPokemon: PokemonInstance | null = null;
  private enemyPokemon: PokemonInstance | null = null;
  
  // ... visuals ...
  private playerSprite: HTMLImageElement | null = null;
  private enemySprite: HTMLImageElement | null = null;
  private background: HTMLImageElement | null = null;

  public isActive: boolean = false;

  // Battle State
  private state: 'INTRO' | 'SHOW_TEXT' | 'SELECT_ACTION' | 'SELECT_MOVE' | 'SELECT_POKEMON' | 'SELECT_BAG' | 'BUSY' | 'ANIMATING' | 'FAINT_ANIM' | 'CAPTURE_ANIM' | 'EXP_GAIN' | 'LEVEL_UP_STATS' | 'LEVEL_UP_STATS_2' | 'BATTLE_END_WAIT' = 'INTRO';
  private menuSelection: number = 0; 
  private moveSelection: number = 0; 
  private pokemonSelection: number = 0;
  private bagSelection: number = 0; 
  
  // Animation Queues
  private hpAnimation: { 
      pokemon: PokemonInstance; 
      startHp: number; 
      targetHp: number; 
      duration: number; 
      timer: number; 
      resolve: () => void;
  } | null = null;
  
  private flashAnimation: {
      spriteIsBack: boolean; // true = player, false = enemy
      count: number;
      timer: number;
      visible: boolean;
      resolve: () => void;
  } | null = null;

  private xpAnimation: {
      startExp: number;
      targetExp: number;
      duration: number;
      timer: number;
      resolve: () => void;
  } | null = null;

  private faintAnim: {
      yOffset: number;
      opacity: number;
      timer: number;
      resolve: () => void;
  } | null = null;
  
  private levelUpData: {
      oldStats: Stats;
      newStats: Stats;
      diff: Stats;
  } | null = null;

  private catchAnim: {
      phase: 'THROW' | 'OPEN' | 'DROP' | 'SHAKE' | 'BREAK' | 'CAUGHT';
      timer: number;
      ballId: string;
      ballSprite?: HTMLImageElement;
      shakes: number; // Max shakes from calc
      currentShake: number;
      startX: number;
      startY: number;
      targetX: number;
      targetY: number;
      enemyScale: number;
      ballX: number;
      ballY: number;
      result: { caught: boolean; shakes: number };
  } | null = null;

  // Text Box State
  private currentText: string = '';
  private textTimer: number = 0; 
  private onTextFinished: (() => void) | null = null;
  
  // Animation State
  private introTimer: number = 0;
  private constantSlideInDuration: number = 2000;

  public game: Game;

  constructor(game: Game) {
      this.game = game;
      this.dataManager = game.dataManager;
      this.battleAI = new BattleAI(this.dataManager);
      const moves = this.dataManager.getAllMoves();
      this.moveLearningManager = new MoveLearningManager(moves);
  }


  public async startBattle(player: PokemonInstance, enemy: PokemonInstance): Promise<void> {
      this.playerPokemon = player;
      this.enemyPokemon = enemy;
      this.isActive = true;
      this.state = 'INTRO';
      this.introTimer = 0;
      
      console.log('[Battle] Started!', player.nickname, 'vs', enemy.nickname);
      
      // Load Sprites
      this.loadSprite(player.speciesId, true);
      this.loadSprite(enemy.speciesId, false);
      
      // Load Background (Default Grass for now)
      this.loadBackground('data/battle_bg_grass.png'); 
      
      // Preload Moves
      for (const m of player.moves) await this.dataManager.loadMove(m.moveId);
      for (const m of enemy.moves) await this.dataManager.loadMove(m.moveId);

      // Trigger Battle Start Abilities
      await AbilityRegistry.trigger(player.ability, 'onBattleStart', { owner: player, battle: this });
      await AbilityRegistry.trigger(enemy.ability, 'onBattleStart', { owner: enemy, battle: this });
  }

  private async loadBackground(path: string): Promise<void> {
       try {
            const response = await (window as any).fs.readImage(path);
             if (response.success) {
                const img = new Image();
                img.src = `data:image/png;base64,${response.data}`;
                this.background = img;
            }
       } catch (e) {
           console.error('BG Load Failed', e);
       }
  }

  private async loadSprite(speciesId: string, isBack: boolean): Promise<void> {
      // Get species data to find the correct asset path
      const species = this.dataManager.getPokemonSpecies(speciesId);
      if (!species) {
          console.error('[Battle] Cannot load sprite: Unknown species', speciesId);
          return;
      }

      // Use the path from the Pokedex data, or fallback
      let path = isBack ? species.assets.back : species.assets.front;
      
      // Fallback if data is missing (legacy support)
      if (!path) {
           const paddedId = speciesId.toString().padStart(3, '0');
           path = `data/pokemon/images/${paddedId}/${isBack ? 'back.png' : 'front.png'}`;
      }

      try {
        const response = await (window as any).fs.readImage(path);
        if (response.success) {
            const img = new Image();
            img.src = `data:image/png;base64,${response.data}`;
            if (isBack) this.playerSprite = img;
            else this.enemySprite = img;
        } else {
            console.error('[Battle] Failed to read sprite file:', path, response.error);
        }
      } catch (e) {
          console.error('[Battle] Exception loading sprite', path, e);
      }
  }

  // ... startBattle / loadSprite ...

  // Assuming a renderVisuals method exists or will be added,
  // the following snippet would be placed inside it to adjust sprite sizes.
  // For now, it's placed here as per the instruction's structure.
  // This block is not syntactically correct outside a method, but follows the user's provided structure.
  /*
      if (this.enemySprite && showEnemy) {
          const size = 200; // Adjusted for better fit
          const targetX = width - size - 60;
          const startX = width + size; 
          const currentX = startX + (targetX - startX) * ease;
          
          ctx.globalAlpha = enemyAlpha;
          ctx.drawImage(this.enemySprite, currentX, 40 + enemyYOffset, size, size);
          ctx.globalAlpha = 1.0;
      }

      if (this.playerSprite && showPlayer) {
          const size = 200;
          const targetX = 60;
          const startX = -size - 20;
          const currentX = startX + (targetX - startX) * ease;
          ctx.drawImage(this.playerSprite, currentX, height - size - 80, size, size);
      }
  */

  public update(dt: number, input?: InputManager): void {
      if (!this.isActive) return;

      // Check for Menu Updates (e.g. Bag)
      if (this.game.menuSystem.isOpen) {
          this.game.menuSystem.update(dt);
          return;
      }
      
      if (this.state === 'INTRO') {
          this.introTimer += dt;
          if (this.introTimer >= this.constantSlideInDuration) {
              this.introTimer = this.constantSlideInDuration;
              // Transition to Text
              this.state = 'SHOW_TEXT';
              this.currentText = `Wild ${this.enemyPokemon?.nickname} appeared!`;
              this.onTextFinished = () => {
                  this.state = 'SELECT_ACTION';
              };
          }
      } else if (this.state === 'SHOW_TEXT' && input) {
          this.textTimer += dt;
          if (input.isJustPressed('KeyZ') || input.isJustPressed('Space') || input.isJustPressed('Enter')) {
              if (this.onTextFinished) {
                  this.onTextFinished();
                  this.onTextFinished = null;
              }
          }
      } else if (this.state === 'SELECT_ACTION' && input) {
          // Menu Navigation
          if (input.isJustPressed('ArrowRight') || input.isJustPressed('KeyD')) this.menuSelection = (this.menuSelection + 1) % 4;
          if (input.isJustPressed('ArrowLeft') || input.isJustPressed('KeyA')) this.menuSelection = (this.menuSelection + 3) % 4;
          if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) this.menuSelection = (this.menuSelection + 2) % 4;
          if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) this.menuSelection = (this.menuSelection + 2) % 4;
          
          if (input.isJustPressed('Space') || input.isJustPressed('Enter')) {
              if (this.menuSelection === 0) {
                   // FIGHT
                   this.state = 'SELECT_MOVE';
                   this.moveSelection = 0;
              } else if (this.menuSelection === 1) {
                  // BAG
                  const bagMenu = new BagMenu(this.game, 'BATTLE');
                  bagMenu.onItemUsed = (itemId) => {
                       this.handleBattleItemUse(itemId);
                  };
                  this.game.menuSystem.push(bagMenu);
                  // this.state = 'SELECT_BAG'; 
                  // this.bagSelection = 0;
              } else if (this.menuSelection === 2) {
                  // POKEMON
                  this.state = 'SELECT_POKEMON';
                  this.pokemonSelection = 0;
              } else if (this.menuSelection === 3) {
                   // RUN
                   this.state = 'SHOW_TEXT';
                   this.currentText = 'Got away safely!';
                   this.onTextFinished = () => {
                       this.isActive = false;
                   };
              }
          }
      } else if (this.state === 'SELECT_MOVE' && input) {
          // Move Navigation
          if (input.isJustPressed('ArrowRight') || input.isJustPressed('KeyD')) this.moveSelection = (this.moveSelection + 1) % 4;
          if (input.isJustPressed('ArrowLeft') || input.isJustPressed('KeyA')) this.moveSelection = (this.moveSelection + 3) % 4;
          if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) this.moveSelection = (this.moveSelection + 2) % 4;
          if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) this.moveSelection = (this.moveSelection + 2) % 4;
          
          // Back: Escape only. KeyZ is confirm (matches TitleScreen, DialogBox, StartMenu).
          if (input.isJustPressed('Escape')) {
              this.state = 'SELECT_ACTION';
          }
          
          // Confirm move: Z, Space, or Enter
          if (input.isJustPressed('KeyZ') || input.isJustPressed('Space') || input.isJustPressed('Enter')) {
              const moveInstance = this.playerPokemon?.moves[this.moveSelection];
              if (moveInstance) {
                  this.state = 'BUSY';
                  this.executePlayerTurn(moveInstance).catch(e => {
                      console.error('[BattleScene] TURN_CRASH:', e);
                      this.state = 'SELECT_ACTION';
                  });
              }
          }
      }
      else if (this.state === 'SELECT_POKEMON' && input) {
           if (!this.partyScreen) {
               this.partyScreen = new PartyScreen(this.game, 'BATTLE_SWITCH');
               this.partyScreen.onOpen();
               this.partyScreen.onResult = (selectedMon) => {
                   if (selectedMon) {
                       if (selectedMon === this.playerPokemon) {
                           this.currentText = `${selectedMon.nickname} is already in battle!`;
                           this.state = 'SHOW_TEXT';
                           this.onTextFinished = () => { this.state = 'SELECT_POKEMON'; this.partyScreen = null; };
                       } else if (selectedMon.currentHp <= 0) {
                           this.currentText = `${selectedMon.nickname} has no energy left!`;
                           this.state = 'SHOW_TEXT';
                           this.onTextFinished = () => { this.state = 'SELECT_POKEMON'; this.partyScreen = null; };
                       } else {
                           this.state = 'BUSY';
                           this.executeSwitch(selectedMon);
                           this.partyScreen = null;
                       }
                   } else {
                       this.state = 'SELECT_ACTION';
                       this.partyScreen = null;
                   }
               };
           }
           this.partyScreen.update(dt);
      } 
// ...
 else if (this.state === 'SELECT_BAG' && input) {
          // Vertical Navigation
          if (input.isJustPressed('ArrowDown') || input.isJustPressed('KeyS')) this.bagSelection = Math.min(this.bagSelection + 1, 3);
          if (input.isJustPressed('ArrowUp') || input.isJustPressed('KeyW')) this.bagSelection = Math.max(this.bagSelection - 1, 0);
          
          if (input.isJustPressed('KeyZ') || input.isJustPressed('Escape')) {
              this.state = 'SELECT_ACTION';
          }
          if (input.isJustPressed('KeyZ') || input.isJustPressed('Escape')) {
              this.state = 'SELECT_ACTION';
          }
      } else if (this.state === 'FAINT_ANIM' && input) {
          // Automatic, no input
      } else if (this.state === 'EXP_GAIN' && input) {
           // Skip animation on input?
           // For now, let it ride or speed up
      } else if (this.state === 'LEVEL_UP_STATS' && input) {
          if (input.isJustPressed('Space') || input.isJustPressed('Enter')) {
              this.state = 'LEVEL_UP_STATS_2';
          }
      } else if (this.state === 'LEVEL_UP_STATS_2' && input) {
          if (input.isJustPressed('Space') || input.isJustPressed('Enter')) {
               // Check for moves?
               // For now, just end or moves
               this.state = 'BATTLE_END_WAIT'; // Wait for final input
          }
      } else if (this.state === 'BATTLE_END_WAIT' && input) {
          this.textTimer += dt;
          if (input.isJustPressed('Space') || input.isJustPressed('Enter')) {
              this.isActive = false; // Actually End Battle
          }
      }
      
      // Handle Animations
      if (this.hpAnimation) {
          this.hpAnimation.timer += dt;
          const progress = Math.min(this.hpAnimation.timer / this.hpAnimation.duration, 1.0);
          
          this.hpAnimation.pokemon.currentHp = this.hpAnimation.startHp + (this.hpAnimation.targetHp - this.hpAnimation.startHp) * progress;
          
          if (progress >= 1.0) {
              this.hpAnimation.pokemon.currentHp = this.hpAnimation.targetHp; // Ensure exact
              this.hpAnimation.resolve();
              this.hpAnimation = null;
          }
      }
      
      if (this.flashAnimation) {
          this.flashAnimation.timer += dt;
          if (this.flashAnimation.timer >= 100) { // Toggle every 100ms
               this.flashAnimation.timer = 0;
               this.flashAnimation.visible = !this.flashAnimation.visible;
               this.flashAnimation.count--;
               if (this.flashAnimation.count <= 0) {
                   this.flashAnimation.visible = true; // Ensure visible at end
                   this.flashAnimation.resolve();
                   this.flashAnimation = null;
               }
          }
      }

      
      if (this.faintAnim) {
          this.faintAnim.timer += dt;
          const progress = Math.min(this.faintAnim.timer / 1000, 1.0);
          this.faintAnim.yOffset = progress * 50; // Drop 50px
          this.faintAnim.opacity = 1.0 - progress;
          
          if (progress >= 1.0) {
              this.faintAnim.resolve();
              this.faintAnim = null;
          }
      }
      
      if (this.xpAnimation) {
          this.xpAnimation.timer += dt;
          const progress = Math.min(this.xpAnimation.timer / this.xpAnimation.duration, 1.0);
          
          this.playerPokemon!.experience = Math.floor(this.xpAnimation.startExp + (this.xpAnimation.targetExp - this.xpAnimation.startExp) * progress);
          
          if (progress >= 1.0) {
              this.xpAnimation.resolve();
              this.xpAnimation = null;
          }
      }

      if (this.state === 'CAPTURE_ANIM' && this.catchAnim) {
          this.updateCaptureAnim(dt);
      }
  }

   private async playMoveEvents(result: MoveExecutionResult): Promise<void> {
       console.log(`[BattleScene] Playing ${result.events.length} events...`);
       for (const event of result.events) {
           const target = result.allParticipants.find(
               p => (p as any).uuid === event.targetId || (p as any).id === event.targetId
           );
           if (!target) {
               console.warn(`[BattleScene] Target ${event.targetId} not found in participants.`);
               continue;
           }

           console.log(`[BattleScene] Event: ${event.type}`, event.message || '');

           switch (event.type) {
               case 'Text':
                   if (event.message) await this.showText(event.message);
                   break;
               case 'Blink':
                   this.state = 'BUSY';
                   await this.blinkSprite(target === this.playerPokemon);
                   break;
               case 'Damage': {
                   this.state = 'BUSY';
                   const damage = event.value ?? 0;
                   const targetHp = target.currentHp;
                   const startHp = targetHp + damage;
                   await this.animateHealth(target, targetHp, startHp);
                   break;
               }
               case 'Heal': {
                   this.state = 'BUSY';
                   const healed = event.value ?? 0;
                   const targetHp = target.currentHp;
                   const startHp = targetHp - healed;
                   await this.animateHealth(target, targetHp, startHp);
                   break;
               }
               case 'Status':
                   break;
               case 'StatChange':
                   break;
               case 'EffectUnique':
                   break;
               default:
                   break;
           }
       }
       console.log(`[BattleScene] Finished playing events.`);
       this.state = 'BUSY';
   }

   private async executePlayerTurn(playerMoveInst: any): Promise<void> {
       if (!this.playerPokemon || !this.enemyPokemon) return;
       
       console.log(`[BattleScene] Starting player turn with move: ${playerMoveInst.moveId}`);
       this.state = 'BUSY';
       
       const playerMove = this.dataManager.getMove(playerMoveInst.moveId);
       if (!playerMove) {
           console.error('[BattleScene] Player move not found:', playerMoveInst.moveId);
           this.state = 'SELECT_ACTION';
           return;
       }
       
       // 1. Logic Execution
       const result = MoveEngine.executeMove(this.playerPokemon, this.enemyPokemon, playerMove, this.dataManager);

       // 2. Play Visuals
       await this.playMoveEvents(result);

       // 3. Post-Move Checks (Fainting, XP)
       if (this.enemyPokemon.currentHp <= 0) {
           this.enemyPokemon.currentHp = 0;
           await this.showText(`${this.getPokemonDisplayName(this.enemyPokemon)} fainted!`);
           await this.performFaintAnim();

           // XP Logic (keeping here as it's separate from move execution phase)
           await this.handleExperienceGain();

           // Only set BATTLE_END_WAIT if not showing level-up stats
           console.log('[BattleScene] After handleExperienceGain, state:', this.state);
           if (this.state !== 'LEVEL_UP_STATS' && this.state !== 'LEVEL_UP_STATS_2') {
               this.state = 'BATTLE_END_WAIT';
           }
           return;
       }
       
       // --- ENEMY ATTACK ---
       await this.executeEnemyTurn();
   }

   private async executeEnemyTurn(): Promise<void> {
       if (!this.playerPokemon || !this.enemyPokemon) return;
       
       console.log(`[BattleScene] Starting AI turn...`);
       await new Promise(r => setTimeout(r, 800));

       const aiResult = this.battleAI.getBestAction(this.playerPokemon, this.enemyPokemon);
       const moveIndex = aiResult.moveIndex >= 0 ? aiResult.moveIndex : 0;
       const enemyMoveInst = this.enemyPokemon.moves[moveIndex];
       const moveId = enemyMoveInst
           ? (typeof enemyMoveInst === 'string' ? enemyMoveInst : enemyMoveInst.moveId)
           : null;
       const enemyMove = moveId ? this.dataManager.getMove(moveId) : null;

       if (enemyMove) {
            console.log(`[BattleScene] Enemy chose move: ${enemyMove.id}`);
            const eResult = MoveEngine.executeMove(this.enemyPokemon, this.playerPokemon, enemyMove, this.dataManager);
            await this.playMoveEvents(eResult);

            if (this.playerPokemon.currentHp <= 0) {
                await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} fainted!`);
                await this.showText(`You blacked out!`);
                this.isActive = false;
                return;
            }
       } else {
            await this.showText(`${this.getPokemonDisplayName(this.enemyPokemon)} couldn't move!`);
       }

       // --- END OF TURN ---
       await this.executeEndOfTurn();

       // Check if anyone fainted during End of Turn
       if (this.playerPokemon.currentHp <= 0 && this.isActive) {
           await this.showText(`You blacked out!`);
           this.isActive = false;
           return;
       }
       if (this.enemyPokemon.currentHp <= 0 && this.isActive) {
            // Should be handled in executeEndOfTurn logic?
            return;
       }

       console.log(`[BattleScene] Turn complete. Returning to SELECT_ACTION.`);
       this.state = 'SELECT_ACTION';
   }

    private async handleBattleItemUse(itemId: string): Promise<void> {
         console.log(`[BattleScene] Item Used from Bag: ${itemId}`);
         
         // Close bag menu
         this.game.menuSystem.pop(); 
         
         // DETERMINE TARGET
         const itemData = this.dataManager.getItem(itemId);
         // Default to Player
         let target = this.playerPokemon!;
         // If Pokeball, Target Enemy
         if (itemData && itemData.category === 'pokeballs') {
             target = this.enemyPokemon!;
         }

         const itemResult = this.game.itemHandler.useItem(itemId, target, 'battle');
         
         if (itemResult.success) {
             
             // Handle Capture Specifics
             if (itemResult.capture && itemData) {
                 await this.startCaptureAnim(itemId, itemResult.capture);
                 return; // Animation handles next steps
             }

             await this.showText(itemResult.message);
             
             // Show learned moves if any
             if (itemResult.effects && itemResult.effects.learnedMoves && itemResult.effects.learnedMoves.length > 0) {
                 for (const moveName of itemResult.effects.learnedMoves) {
                     await this.showText(`${target.nickname || target.speciesId} learned ${moveName}!`);
                 }
             }

             // Handle move replacement if needed
             if (itemResult.effects && itemResult.effects.movesToReplace && itemResult.effects.movesToReplace.length > 0) {
                 await this.handleBattleMoveReplacement(target, itemResult.effects.movesToReplace, 0, itemId, itemResult.consumed);
                 return;
             }
             
             if (itemResult.consumed) {
                  this.game.bagSystem.removeItem(itemId, 1);
                  // Proceed to Enemy Turn
                  await this.executeEnemyTurn();
             } else {
                  this.state = 'SELECT_ACTION';
             }
         } else {
             await this.showText(itemResult.message);
             this.state = 'SELECT_ACTION';
         }
    }

    private async handleBattleMoveReplacement(pokemon: PokemonInstance, movesToReplace: any[], currentIndex: number, itemId: string, consumed: boolean): Promise<void> {
        if (currentIndex >= movesToReplace.length) {
            if (consumed) {
                this.game.bagSystem.removeItem(itemId, 1);
                await this.executeEnemyTurn();
            } else {
                this.state = 'SELECT_ACTION';
            }
            return;
        }

        const moveToReplace = movesToReplace[currentIndex];
        const moveData = this.dataManager.getMove(moveToReplace.moveId);
        
        if (!moveData) {
            await this.handleBattleMoveReplacement(pokemon, movesToReplace, currentIndex + 1, itemId, consumed);
            return;
        }

        await this.showText(`${pokemon.nickname || pokemon.speciesId} wants to learn ${moveData.name}!`);
        await this.showText(`But it already knows 4 moves!`);

        this.moveReplacementMenu = new MoveReplacementMenu(this.game, pokemon, moveData);
        this.moveReplacementMenu.onResult = async (replaced, oldMoveId) => {
            this.game.menuSystem.pop();
            
            if (replaced && oldMoveId) {
                const oldMoveIndex = pokemon.moves.findIndex(m => m.moveId === oldMoveId);
                if (oldMoveIndex !== -1) {
                    this.moveLearningManager.replaceMove(pokemon, oldMoveIndex, moveToReplace.moveId);
                    await this.showText(`${pokemon.nickname || pokemon.speciesId} learned ${moveData.name}!`);
                }
            }

            this.moveReplacementMenu = null;
            await this.handleBattleMoveReplacement(pokemon, movesToReplace, currentIndex + 1, itemId, consumed);
        };

        this.game.menuSystem.push(this.moveReplacementMenu);
    }

    private async startCaptureAnim(ballId: string, result: { caught: boolean, shakes: number }): Promise<void> {
        this.game.bagSystem.removeItem(ballId, 1);
        this.state = 'CAPTURE_ANIM';
        
        // Load Ball Sprite
        let ballImg: HTMLImageElement | undefined = undefined;
        try {
            const item = this.dataManager.getItem(ballId);
            // Skip external URLs - we'll use fallback rendering
            if (item && item.sprite && !item.sprite.startsWith('http')) {
                const res = await (window as any).fs.readImage(item.sprite);
                if (res.success) {
                    ballImg = new Image();
                    ballImg.src = `data:image/png;base64,${res.data}`;
                }
            }
        } catch (e) { 
            console.log('[BattleScene] Ball sprite load failed, using fallback:', e); 
        }

        this.catchAnim = {
            phase: 'THROW',
            timer: 0,
            ballId,
            ballSprite: ballImg,
            shakes: result.shakes,
            currentShake: 0,
            result,
            startX: 200, // Player position approx
            startY: 400,
            targetX: 960 - 250, // Enemy Position approx
            targetY: 150,
            ballX: 200,
            ballY: 400,
            enemyScale: 1.0
        };
    }

    private updateCaptureAnim(dt: number): void {
        if (!this.catchAnim) return;
        const anim = this.catchAnim;
        anim.timer += dt;

        if (anim.phase === 'THROW') {
            // Arc to enemy
            const duration = 600;
            const t = Math.min(anim.timer / duration, 1.0);
            
            // Simple Linear X, Parabolic Y
            anim.ballX = anim.startX + (anim.targetX - anim.startX) * t;
            // Parabola: -4 * height * (x - 0.5)^2 + height
            const height = 200;
            const arc = -4 * height * (t - 0.5) * (t - 0.5) + height;
            anim.ballY = anim.startY + (anim.targetY - anim.startY) * t - (arc > 0 ? arc : 0);

            if (t >= 1.0) {
                anim.phase = 'OPEN';
                anim.timer = 0;
            }
        } else if (anim.phase === 'OPEN') {
            // Suck in enemy
            const duration = 400;
            const t = Math.min(anim.timer / duration, 1.0);
            anim.enemyScale = 1.0 - t;
            
            if (t >= 1.0) {
                anim.enemyScale = 0;
                anim.phase = 'DROP';
                anim.timer = 0;
            }
        } else if (anim.phase === 'DROP') {
            // Drop to ground (reduced distance)
            const duration = 400;
            const t = Math.min(anim.timer / duration, 1.0);
            anim.ballY = anim.targetY + (t * 80); // Reduced from 200 to 80
            if (t >= 1.0) {
                anim.phase = 'SHAKE';
                anim.timer = 0;
            }
        } else if (anim.phase === 'SHAKE') {
            // Wait 1s, then Shake
            const shakeDuration = 1000;
            if (anim.timer >= shakeDuration) {
                // Perform Shake or End
                if (anim.currentShake < anim.shakes) {
                    anim.currentShake++;
                    anim.timer = 500; // Reset partly to loop shakes? Actually complex. 
                    // Let's just say we wait 1s per shake limit.
                    // Visual shake logic handled in render.
                    // We increment shake counter.
                    anim.timer = 0;
                } else {
                    // Done shaking
                    if (anim.result.caught) {
                        anim.phase = 'CAUGHT';
                        anim.timer = 0;
                    } else {
                        anim.phase = 'BREAK';
                        anim.timer = 0;
                    }
                }
            }
        } else if (anim.phase === 'BREAK') {
            // Break info
            const duration = 500;
            const t = Math.min(anim.timer / duration, 1.0);
            anim.enemyScale = t; // Grow back
            
            if (t >= 1.0) {
                this.finishCapture(false);
            }
        } else if (anim.phase === 'CAUGHT') {
            // Wait a moment
            if (anim.timer > 1000) {
                this.finishCapture(true);
            }
        }
    }

    private async finishCapture(success: boolean): Promise<void> {
        this.state = 'BUSY';
        // Don't clear catchAnim yet - keep enemy hidden

        if (success) {
            await this.showText(`Gotcha! ${this.enemyPokemon!.nickname} was caught!`);
            
            // Add to Party/PC
            let addedToParty = false;
            if (this.game.party.length < 6) {
                this.game.party.push(this.enemyPokemon!);
                addedToParty = true;
            } else {
                this.game.storageSystem.addPokemon(this.enemyPokemon!);
                await this.showText(`${this.enemyPokemon!.nickname} was sent to the PC.`);
            }
            
            // XP
            await this.handleExperienceGain();
            
            // Clear animation now that we're done
            this.catchAnim = null;
            
            // End
            this.isActive = false;
        } else {
            this.catchAnim = null; // Clear anim
            await this.showText(`Oh no! The Pokemon broke free!`);
            // Resume Battle (Enemy Turn)
            await this.executeEnemyTurn();
        }
    }

   private async handleExperienceGain(): Promise<void> {
        if (!this.enemyPokemon || !this.playerPokemon) return;

        console.log('[BattleScene] Loading species for:', this.enemyPokemon.speciesId);
        await this.dataManager.loadPokemonSpecies(this.enemyPokemon.speciesId);
        const species = this.dataManager.getPokemonSpecies(this.enemyPokemon.speciesId);
        
        console.log('[BattleScene] Species lookup result:', species);
        
        if (!species) {
            console.error('[BattleScene] Species not found for:', this.enemyPokemon.speciesId);
            return;
        }

        console.log('[BattleScene] Enemy Pokemon:', {
            speciesId: this.enemyPokemon.speciesId,
            level: this.enemyPokemon.level,
            expYield: species.expYield,
            hasExpYield: species.expYield !== undefined
        });

        const xpGain = ExperienceCalculator.calculateExpGain(this.enemyPokemon, species);
        const oldExp = this.playerPokemon.experience;
        const newExp = oldExp + xpGain;

        console.log('[BattleScene] XP Calculation:', {
            oldExp,
            xpGain,
            newExp,
            playerLevel: this.playerPokemon.level,
            nextLevelExp: ExperienceCalculator.getExpForLevel(this.playerPokemon.level + 1)
        });

        // APPLY XP
        this.playerPokemon.experience = newExp;

        await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} gained ${xpGain} Exp. Points!`);
        await this.animateExp(oldExp, newExp);

        const nextLevelExp = ExperienceCalculator.getExpForLevel(this.playerPokemon.level + 1);
        if (this.playerPokemon.experience >= nextLevelExp) {
            await this.handleLevelUp(newExp);
        }
   }

   private async handleLevelUp(finalExp: number): Promise<void> {
        if (!this.playerPokemon) return;
        const oldStats = { ...this.playerPokemon.currentStats };
        this.playerPokemon.level++;
        this.playerPokemon.experience = finalExp;
        
        await this.dataManager.loadPokemonSpecies(this.playerPokemon.speciesId);
        const speciesData = this.dataManager.getPokemonSpecies(this.playerPokemon.speciesId);
        
        if (speciesData) {
            const newStats = ExperienceCalculator.recalculateStats(this.playerPokemon, speciesData);
            this.playerPokemon.currentStats = newStats;
            const hpDiff = newStats.hp - oldStats.hp;
            if (hpDiff > 0) this.playerPokemon.currentHp += hpDiff;

            await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} grew to Lv. ${this.playerPokemon.level}!`);

            const learnableMoves = this.moveLearningManager.getMovesLearnableAtLevel(speciesData, this.playerPokemon.level, this.playerPokemon);
            
            if (learnableMoves.length > 0) {
                for (const learnableMove of learnableMoves) {
                    const moveData = this.dataManager.getMove(learnableMove.moveId);
                    if (moveData) {
                        const result = this.moveLearningManager.learnMove(this.playerPokemon, learnableMove.moveId);
                        
                        if (result.learned) {
                            await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} learned ${moveData.name}!`);
                        } else if (result.reason === 'slots_full') {
                            await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} wants to learn ${moveData.name}!`);
                            await this.showText(`But it already knows 4 moves!`);
                            
                            this.moveReplacementMenu = new MoveReplacementMenu(this.game, this.playerPokemon, moveData);
                              this.moveReplacementMenu.onResult = async (replaced, oldMoveId) => {
                                  if (replaced && oldMoveId) {
                                      const oldMoveIndex = this.playerPokemon!.moves.findIndex(m => m.moveId === oldMoveId);
                                      if (oldMoveIndex !== -1) {
                                          this.moveLearningManager.replaceMove(this.playerPokemon!, oldMoveIndex, learnableMove.moveId);
                                      }
                                      this.moveReplacementMenu = null;
                                      await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} learned ${moveData.name}!`);
                                      await this.checkAndTriggerEvolution(oldStats, newStats);
                                  } else {
                                      this.moveReplacementMenu = null;
                                      await this.checkAndTriggerEvolution(oldStats, newStats);
                                  }
                              };
                            
                            this.game.menuSystem.push(this.moveReplacementMenu);
                            return;
                        }
                    }
                }
            }

            await this.checkAndTriggerEvolution(oldStats, newStats);

            const statIncreases = StatCalculator.calculateAllStatIncreases(
                speciesData.baseStats,
                this.playerPokemon.ivs,
                this.playerPokemon.evs,
                this.playerPokemon.level - 1,
                this.playerPokemon.nature
            );

            this.showLevelUpStats(oldStats, newStats);
        }
   }

   private async checkAndTriggerEvolution(oldStats: Stats, newStats: Stats): Promise<void> {
      if (!this.playerPokemon) return;
      
      const evolutionManager = new EvolutionManager(this.dataManager.pokemonCache);
      const evolutionResult = evolutionManager.checkEvolution(this.playerPokemon);
      
      if (evolutionResult.canEvolve && evolutionResult.evolutionData) {
         await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} is evolving!`);
         
         const oldSpeciesId = this.playerPokemon.speciesId;
         const oldMaxHp = newStats.hp;
         
         evolutionManager.evolvePokemon(this.playerPokemon, evolutionResult.evolutionData.targetSpeciesId);
         
         const newSpeciesData = this.dataManager.getPokemonSpecies(evolutionResult.evolutionData.targetSpeciesId);
         if (newSpeciesData) {
            await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} evolved into ${newSpeciesData.name}!`);
         }
      }
      
      this.showLevelUpStats(oldStats, newStats);
   }

   private showLevelUpStats(oldStats: Stats, newStats: Stats): void {
        const speciesData = this.dataManager.getPokemonSpecies(this.playerPokemon!.speciesId);
        if (!speciesData) return;

        const statIncreases = StatCalculator.calculateAllStatIncreases(
            speciesData.baseStats,
            this.playerPokemon!.ivs,
            this.playerPokemon!.evs,
            this.playerPokemon!.level - 1,
            this.playerPokemon!.nature
        );

        this.levelUpData = {
            oldStats,
            newStats,
            diff: {
                hp: statIncreases.hp ?? 0,
                attack: statIncreases.attack ?? 0,
                defense: statIncreases.defense ?? 0,
                spAttack: statIncreases.spAttack ?? 0,
                spDefense: statIncreases.spDefense ?? 0,
                speed: statIncreases.speed ?? 0
            }
        };
        this.state = 'LEVEL_UP_STATS';
        console.log('[BattleScene] State set to LEVEL_UP_STATS:', this.state);
   }

   private async executeEndOfTurn(): Promise<void> {
       console.log('[BattleScene] Executing End of Turn...');
       // 1. Process Residual Effects for both active Pokemon
       const participants = [this.playerPokemon, this.enemyPokemon];

       for (const mon of participants) {
           if (!mon || mon.currentHp <= 0) continue;

           // --- STATUS CONDITIONS ---
           
           // Burn (1/16th Max HP)
           if (mon.status === 'Burn') {
               const dmg = Math.floor(mon.currentStats.hp / 16) || 1;
               mon.currentHp = Math.max(0, mon.currentHp - dmg);
               await this.showText(`${mon.nickname} is hurt by its burn!`);
               await this.animateHealth(mon, mon.currentHp);
           }

           // Poison (1/8th Max HP)
           if (mon.status === 'Poison') {
                const dmg = Math.floor(mon.currentStats.hp / 8) || 1;
                mon.currentHp = Math.max(0, mon.currentHp - dmg);
                await this.showText(`${mon.nickname} is hurt by poison!`);
                await this.animateHealth(mon, mon.currentHp);
           }

           // --- VOLATILE STATUSES ---

           // Leech Seed (1/8th Max HP -> Heal Opponent)
           if (mon.volatile['LeechSeed']) {
               const dmg = Math.floor(mon.currentStats.hp / 8) || 1;
               const oldHp = mon.currentHp;
               mon.currentHp = Math.max(0, mon.currentHp - dmg);
               
               await this.showText(`${mon.nickname}'s health is sapped by Leech Seed!`);
               await this.animateHealth(mon, mon.currentHp);
               
               // Heal Opponent (The one who didn't take damage)
               if (oldHp > mon.currentHp) {
                   const opponent = (mon === this.playerPokemon) ? this.enemyPokemon : this.playerPokemon;
                   if (opponent && opponent.currentHp > 0) {
                       const drainAmt = oldHp - mon.currentHp;
                       const healStart = opponent.currentHp;
                       opponent.currentHp = Math.min(opponent.currentStats.hp, opponent.currentHp + drainAmt);
                       await this.animateHealth(opponent, opponent.currentHp, healStart);
                   }
               }
           }

            // Bound / Trap (Fire Spin, Wrap, etc) - 1/16th Damage
            if (mon.volatile['Bound']) {
                mon.volatile['Bound']--;
                if (mon.volatile['Bound'] <= 0) {
                     delete mon.volatile['Bound'];
                     await this.showText(`${mon.nickname} was freed from the trap!`);
                } else {
                     const dmg = Math.floor(mon.currentStats.hp / 16) || 1;
                     mon.currentHp = Math.max(0, mon.currentHp - dmg);
                     await this.showText(`${mon.nickname} is hurt by the trap!`);
                     await this.animateHealth(mon, mon.currentHp);
                }
            }

           // Cleanup single-turn volatiles
           if (mon.volatile['Flinch']) {
               delete mon.volatile['Flinch'];
           }
       }

       // Ability Turn End
       for (const mon of participants) {
           if (mon && mon.currentHp > 0) {
              await AbilityRegistry.trigger(mon.ability, 'onTurnEnd', { owner: mon, battle: this });
           }
       }

       // 2. Check Faint after residual damage
       if (this.playerPokemon && this.playerPokemon.currentHp <= 0) {
           await this.showText(`${this.getPokemonDisplayName(this.playerPokemon)} fainted!`);
           // Handle wipe out?
       }
       if (this.enemyPokemon && this.enemyPokemon.currentHp <= 0) {
            this.enemyPokemon.currentHp = 0;
            await this.showText(`${this.getPokemonDisplayName(this.enemyPokemon)} fainted!`);
            await this.performFaintAnim();
            await this.handleExperienceGain();
            // Only set BATTLE_END_WAIT if not showing level-up stats
            console.log('[BattleScene] After handleExperienceGain in executeEndOfTurn, state:', this.state);
            if (this.state !== 'LEVEL_UP_STATS' && this.state !== 'LEVEL_UP_STATS_2') {
                this.state = 'BATTLE_END_WAIT';
            }
       };
   }

   // Animation Helpers

  private animateHealth(pokemon: PokemonInstance, targetHp: number, startHp?: number): Promise<void> {
      const from = startHp !== undefined ? startHp : pokemon.currentHp;
      return new Promise((resolve) => {
          this.hpAnimation = {
              pokemon,
              startHp: from,
              targetHp,
              duration: 800,
              timer: 0,
              resolve
          };
      });
  }
  
  private blinkSprite(isPlayer: boolean): Promise<void> {
      return new Promise((resolve) => {
          this.flashAnimation = {
              spriteIsBack: isPlayer,
              count: 6, // 3 blinks
              timer: 0,
              visible: true,
              resolve
          };
      });
  }

  // Helper to show text and wait for user input (Promisified)
  private showText(text: string): Promise<void> {
      return new Promise((resolve) => {
          this.state = 'SHOW_TEXT';
          this.currentText = text;
          this.onTextFinished = () => {
              resolve();
          };
      });
  }

  public render(display: Display): void {
      if (!this.isActive) return;
      
      const ctx = display.ctx;
      const width = display.width;   // 960
      const height = display.height; // 640
      
      // 1. Background (Stretch to fit)
      if (this.background) {
          ctx.imageSmoothingEnabled = false;
          ctx.drawImage(this.background, 0, 0, width, height);
      } else {
          ctx.fillStyle = '#f8f8f8'; 
          ctx.fillRect(0, 0, width, height);
      }
      
      // Render Visuals (pass width/height)
      this.renderVisuals(ctx, width, height);

      // Render Menus on top
      if (this.game.menuSystem.isOpen) {
          this.game.menuSystem.render(ctx);
          // If a menu is open, we might want to skip rendering the battle UI text boxes underneath?
          // But usually they layer on top.
      }

      // UI Layer
      
      // Always show Health Boxes if not INTRO
      if (this.state !== 'INTRO') {
           if (this.enemyPokemon && (this.state !== 'FAINT_ANIM' && this.state !== 'EXP_GAIN' && this.state !== 'LEVEL_UP_STATS' && this.state !== 'LEVEL_UP_STATS_2' ? this.enemyPokemon.currentHp > 0 : this.faintAnim ? true : this.enemyPokemon && this.enemyPokemon.currentHp > 0)) {
                
                const isHpAnim = this.hpAnimation && this.hpAnimation.pokemon === this.enemyPokemon;
                if (this.enemyPokemon.currentHp > 0 || isHpAnim) {
                    this.renderHealthBox(ctx, 50, 40, this.enemyPokemon, false);
                }
           }
           
           if (this.playerPokemon) this.renderHealthBox(ctx, width - 340 + 75, height - 310 + 75, this.playerPokemon, true);
      }

      if (this.state === 'INTRO') {
          // ...
      } else if (this.state === 'SHOW_TEXT' || this.state === 'BUSY' || this.state === 'EXP_GAIN' || this.state === 'BATTLE_END_WAIT') {
          this.renderTextBox(ctx, width, height, this.currentText, this.state === 'SHOW_TEXT' || this.state === 'BATTLE_END_WAIT');
      } else if (this.state === 'SELECT_POKEMON') {
          this.renderPokemonMenu(ctx, width, height);
      } else if (this.state === 'SELECT_BAG') {
          this.renderBagMenu(ctx, width, height);
      } else if (this.state === 'LEVEL_UP_STATS' || this.state === 'LEVEL_UP_STATS_2') {
          this.renderLevelUpBox(ctx, width, height);
      } else if (this.state === 'FAINT_ANIM') {
          // ...
      } else if (this.state === 'CAPTURE_ANIM') {
          // Just render, logic handled in updateCapsureAnim
      } else {
          // Main Battle Menu
          this.renderMenuBox(ctx, width, height);
      }
  }

  // ... 

  private renderMenuBox(ctx: CanvasRenderingContext2D, width: number, height: number): void {
      const boxHeight = 120; // Taller menu
      const bottomMargin = 60; // Increased Safety buffer (20 -> 60)
      
      if (this.state === 'SELECT_MOVE') {
          // DRAW MOVES
          const menuWidth = width; 
          const menuHeight = boxHeight;
          const menuX = 0;
          const menuY = height - boxHeight - bottomMargin;
          
          ctx.fillStyle = '#fff';
          ctx.fillRect(menuX, menuY, menuWidth, menuHeight);
          ctx.lineWidth = 4;
          ctx.strokeStyle = '#222';
          ctx.strokeRect(menuX + 2, menuY + 2, menuWidth - 4, menuHeight - 4);
          
          ctx.font = 'bold 20px monospace';
          
          const moves = this.playerPokemon?.moves || [];
          
          for (let i = 0; i < 4; i++) {
              const move = moves[i];
              const moveName = move ? move.moveId.toUpperCase() : '-';
              
              const ox = 60 + (i % 2) * 300;
              const oy = menuY + 40 + Math.floor(i / 2) * 40;
              
              if (i === this.moveSelection) {
                   ctx.fillStyle = '#f1c40f'; 
                   ctx.fillText('>', ox - 20, oy); 
              } else {
                  ctx.fillStyle = '#000';
              }
              
              ctx.fillText(moveName, ox, oy);
          }
          
      } else {
          this.renderTextBox(ctx, width, height, `What will ${this.getPokemonDisplayName(this.playerPokemon)} do?`, false);
          
          const menuWidth = 300; 
          const menuHeight = boxHeight;
          const menuX = width - menuWidth - 60; // More margin from right (40 -> 60)
          const menuY = height - menuHeight - bottomMargin;
          
           ctx.fillStyle = '#fff';
           ctx.fillRect(menuX, menuY, menuWidth, menuHeight);
           ctx.strokeStyle = '#222';
           ctx.strokeRect(menuX, menuY, menuWidth, menuHeight);
           
           ctx.font = 'bold 20px monospace';
           const options = ['FIGHT', 'BAG', 'POKEMON', 'RUN'];
              
           options.forEach((opt, i) => {
              const ox = menuX + 40 + (i % 2) * 130; 
              const oy = menuY + 40 + Math.floor(i / 2) * 40;
              
              if (i === this.menuSelection) {
                   ctx.fillStyle = '#f1c40f'; 
                   ctx.fillText('>', ox - 20, oy); 
               } else {
                   ctx.fillStyle = '#000';
               }
               
               ctx.fillText(opt, ox, oy);
           });
       }
       
       if (this.partyScreen) {
           this.partyScreen.render(ctx);
       }
   }

  private performFaintAnim(): Promise<void> {
      return new Promise(resolve => {
          this.state = 'FAINT_ANIM';
          this.faintAnim = {
              yOffset: 0,
              opacity: 1,
              timer: 0,
              resolve
          };
      });
  }
  
  private animateExp(start: number, target: number): Promise<void> {
      return new Promise(resolve => {
          this.state = 'EXP_GAIN';
          this.xpAnimation = {
              startExp: start,
              targetExp: target,
              duration: 1000,
              timer: 0,
              resolve
          };
      });
  }

  private renderPokemonMenu(ctx: CanvasRenderingContext2D, width: number, height: number): void {
      ctx.fillStyle = '#f8f8f8';
      ctx.fillRect(0, 0, width, height);
      
      // Header
      ctx.fillStyle = '#333';
      ctx.fillRect(0, 0, width, 50);
      ctx.fillStyle = '#fff';
      ctx.font = 'bold 20px monospace';
      ctx.fillText('Pokemon Party', 20, 32);
      
      const party = [this.playerPokemon]; 
      
      for (let i = 0; i < 6; i++) {
          const y = 60 + i * 50;
          const isSelected = this.pokemonSelection === i;
          
          if (isSelected) {
              ctx.fillStyle = '#f1c40f';
              ctx.fillRect(10, y, width - 20, 45);
          }
          
          ctx.strokeStyle = '#222';
          ctx.lineWidth = 2;
          ctx.strokeRect(10, y, width - 20, 45);
          
          const mon = party[i];
          ctx.fillStyle = '#000';
          ctx.font = '16px monospace';
          
          if (mon) {
              ctx.fillText(`${mon.nickname} (Lv${mon.level})`, 40, y + 28);
              ctx.fillText(`${Math.ceil(mon.currentHp)}/${mon.currentStats.hp}`, width - 120, y + 28);
          } else {
              ctx.fillStyle = '#aaa';
              ctx.fillText('---', 40, y + 28);
          }
          
          if (isSelected) {
              ctx.fillText('>', 20, y + 28);
          }
      }
      
      ctx.fillStyle = '#333';
      ctx.fillRect(0, height - 40, width, 40);
      ctx.fillStyle = '#fff';
      ctx.font = '12px monospace';
      ctx.fillText('Z: Back', 20, height - 15);
  }

  private renderBagMenu(ctx: CanvasRenderingContext2D, width: number, height: number): void {
      ctx.fillStyle = '#f8f8f8';
      ctx.fillRect(0, 0, width, height);
      
      ctx.fillStyle = '#333';
      ctx.fillRect(0, 0, width, 50);
      ctx.fillStyle = '#fff';
      ctx.font = 'bold 20px monospace';
      ctx.fillText('Bag', 20, 32);
      
      const categories = ['Items', 'Medicine', 'Pokeballs', 'Key Items'];
      
      for (let i = 0; i < categories.length; i++) {
          const y = 80 + i * 60;
          const isSelected = this.bagSelection === i;
          
          if (isSelected) {
               ctx.fillStyle = '#3498db';
               ctx.fillRect(20, y, 200, 50);
          } else {
               ctx.fillStyle = '#ddd';
               ctx.fillRect(20, y, 200, 50);
          }
          
          ctx.strokeStyle = '#222';
          ctx.strokeRect(20, y, 200, 50);
          
          ctx.fillStyle = isSelected ? '#fff' : '#000';
          ctx.font = '18px monospace';
          ctx.fillText(categories[i], 40, y + 32);
          
          if (isSelected) {
              ctx.fillText('>', 25, y + 32);
          }
      }
      
      ctx.strokeStyle = '#222';
      ctx.strokeRect(240, 80, width - 260, height - 140);
      ctx.fillStyle = '#000';
      ctx.font = '14px monospace';
      ctx.fillText('No items.', 260, 110);
      
      ctx.fillStyle = '#333';
      ctx.fillRect(0, height - 40, width, 40);
      ctx.fillStyle = '#fff';
      ctx.font = '12px monospace';
      ctx.fillText('Z: Back', 20, height - 15);
  }

  private renderVisuals(ctx: CanvasRenderingContext2D, width: number, height: number): void {
      const progress = Math.min(this.introTimer / this.constantSlideInDuration, 1.0);
      const ease = 1 - Math.pow(1 - progress, 3);
      
      // Faint Animation
      let enemyYOffset = 0;
      let enemyAlpha = 1;
      
      if (this.faintAnim) {
          enemyYOffset = this.faintAnim.yOffset;
          enemyAlpha = this.faintAnim.opacity;
      } else if (this.enemyPokemon && this.enemyPokemon.currentHp <= 0) {
          enemyAlpha = 0; // Force hide if dead and not animating
      }

      let showEnemy = enemyAlpha > 0;
      let showPlayer = true;

      // Capture Animation Overrides
      let enemyScale = 1.0;
      if (this.catchAnim) {
          enemyScale = this.catchAnim.enemyScale;
          // Keep enemy hidden during CAUGHT phase too
          if (enemyScale <= 0 || this.catchAnim.phase === 'CAUGHT') showEnemy = false;
      }

      // Handle Flashing
      if (this.flashAnimation && !this.flashAnimation.visible) {
          if (this.flashAnimation.spriteIsBack) showPlayer = false;
          else showEnemy = false;
      }

      if (this.enemySprite && showEnemy) {
          const size = 250; // Larger sprites for high res
          const targetX = width - size - 80;
          const startX = width + size; 
          const currentX = startX + (targetX - startX) * ease;
          
          ctx.globalAlpha = enemyAlpha;
          
          // Apply scaling for capture animation
          if (enemyScale !== 1.0) {
              ctx.save();
              const centerX = currentX + size / 2;
              const centerY = 40 + enemyYOffset + size / 2;
              ctx.translate(centerX, centerY);
              ctx.scale(enemyScale, enemyScale);
              ctx.drawImage(this.enemySprite, -size / 2, -size / 2, size, size);
              ctx.restore();
          } else {
              ctx.drawImage(this.enemySprite, currentX, 40 + enemyYOffset, size, size);
          }
          
          ctx.globalAlpha = 1.0;
      }

      if (this.playerSprite && showPlayer) {
          const size = 250;
          const targetX = 80;
          const startX = -size - 20;
          const currentX = startX + (targetX - startX) * ease;
          ctx.drawImage(this.playerSprite, currentX, height - size - 100, size, size);
      }

      // Render Pokeball during capture animation
      if (this.catchAnim) {
          const ballSize = 64;
          const ballX = this.catchAnim.ballX - ballSize / 2;
          const ballY = this.catchAnim.ballY - ballSize / 2;
          
          // Add shake effect during SHAKE phase
          if (this.catchAnim.phase === 'SHAKE') {
              const shakeAmount = Math.sin(this.catchAnim.timer * 0.02) * 10;
              
              if (this.catchAnim.ballSprite) {
                  ctx.save();
                  ctx.translate(ballX + ballSize / 2, ballY + ballSize / 2);
                  ctx.rotate(shakeAmount * Math.PI / 180);
                  ctx.drawImage(this.catchAnim.ballSprite, -ballSize / 2, -ballSize / 2, ballSize, ballSize);
                  ctx.restore();
              } else {
                  // Fallback: Draw a simple pokeball with rotation
                  ctx.save();
                  ctx.translate(ballX + ballSize / 2, ballY + ballSize / 2);
                  ctx.rotate(shakeAmount * Math.PI / 180);
                  this.drawFallbackPokeball(ctx, 0, 0, ballSize);
                  ctx.restore();
              }
          } else {
              if (this.catchAnim.ballSprite) {
                  ctx.drawImage(this.catchAnim.ballSprite, ballX, ballY, ballSize, ballSize);
              } else {
                  // Fallback: Draw a simple pokeball
                  this.drawFallbackPokeball(ctx, ballX + ballSize / 2, ballY + ballSize / 2, ballSize);
              }
          }
      }


  }
  
  private renderTextBox(ctx: CanvasRenderingContext2D, width: number, height: number, text: string, showArrow: boolean): void {
      const boxHeight = 120;
      const bottomMargin = 60; // Moved up to match menu safe zone (20->60)
      const boxY = height - boxHeight - bottomMargin;
      
      ctx.fillStyle = '#222';
      ctx.fillRect(0, boxY, width, boxHeight);
      
      ctx.lineWidth = 4;
      ctx.strokeStyle = '#d35400';
      ctx.strokeRect(2, boxY + 2, width - 4, boxHeight - 4);
      
      ctx.fillStyle = '#fff';
      ctx.font = '24px monospace';
      ctx.fillText(text, 40, boxY + 50);
      
      if (showArrow) {
          if (Math.floor(this.textTimer / 500) % 2 === 0) {
              ctx.beginPath();
              ctx.moveTo(width - 30, boxY + boxHeight - 20);
              ctx.lineTo(width - 20, boxY + boxHeight - 20);
              ctx.lineTo(width - 25, boxY + boxHeight - 10);
              ctx.fill();
          }
      }
  }

  private getPokemonDisplayName(mon: PokemonInstance): string {
      if (mon.nickname) {
          return mon.nickname;
      }
      const species = this.dataManager.getPokemonSpecies(mon.speciesId);
      return species?.name || mon.speciesId || 'Unknown';
  }

  private renderHealthBox(ctx: CanvasRenderingContext2D, x: number, y: number, mon: PokemonInstance, isPlayer: boolean): void {
      ctx.fillStyle = '#fff';
      ctx.strokeStyle = '#000';
      ctx.lineWidth = 2;
      ctx.fillRect(x, y, 140, 50);
      ctx.strokeRect(x, y, 140, 50);
      
      ctx.fillStyle = '#000';
      ctx.font = 'bold 12px monospace';
      ctx.fillText(this.getPokemonDisplayName(mon), x + 10, y + 15);
      
      ctx.font = '12px monospace';
      ctx.fillText(`Lv${mon.level}`, x + 100, y + 15);
      
      ctx.fillStyle = '#555';
      ctx.fillRect(x + 30, y + 25, 100, 6);
      
      const hpPercent = mon.currentHp / mon.currentStats.hp;
      ctx.fillStyle = hpPercent > 0.5 ? '#2ecc71' : hpPercent > 0.2 ? '#f1c40f' : '#e74c3c';
      ctx.fillRect(x + 30, y + 25, 100 * hpPercent, 6);
            if (isPlayer) {
            ctx.font = '10px monospace';
            ctx.fillText(`${Math.ceil(mon.currentHp)} / ${mon.currentStats.hp}`, x + 60, y + 42);
            // Exp Bar
            
            // XP Bar Calculation
            const currentLevelExp = ExperienceCalculator.getExpForLevel(mon.level);
            const nextLevelExp = ExperienceCalculator.getExpForLevel(mon.level + 1);
            const range = nextLevelExp - currentLevelExp;
            const current = Math.min(mon.experience - currentLevelExp, range);
            const percent = Math.max(0, current / range);

            // XP Bar Background (Tray)
            ctx.fillStyle = '#2c3e50'; // Dark Grey
            ctx.fillRect(x + 10, y + 43, 120, 4);

            ctx.fillStyle = '#40e0d0'; // Bight Turquoise for visibility
            ctx.fillRect(x + 10, y + 43, 120 * percent, 4); // Thicker bar
        }

        // --- STATUS ICONS ---
        // Position: Bottom Leftish, under HP bar.
        // HP Bar is at y+25 (height 6). So y+34 is safe.
        // For player, ExP bar is at y+43. Space is tight (y+31 to y+43 is 12px).
        // Let's overlay or fit small.
        
        let iconX = x + 30;
        const iconY = isPlayer ? y + 33 : y + 35;
        
        // 1. Primary Status
        if (mon.status !== 'None') {
            const statusMap: Record<string, { text: string, color: string }> = {
                'Sleep': { text: 'SLP', color: '#7f8c8d' }, // Gray
                'Poison': { text: 'PSN', color: '#9b59b6' }, // Purple
                'Burn': { text: 'BRN', color: '#e74c3c' }, // Red
                'Paralysis': { text: 'PAR', color: '#f1c40f' }, // Yellow
                'Freeze': { text: 'FRZ', color: '#2c3e50' }, // Dark Blue
            };
            
            // Check for Toxic (Badly Poisoned) - Assuming we might store it as status='Poison' and volatile='Toxic' or unique status.
            // For now, adhere to types.
            
            const s = statusMap[mon.status];
            if (s) {
                this.drawStatusIcon(ctx, iconX, iconY, s.text, s.color);
                iconX += 35;
            }
        }

        // 2. Volatile Statuses
         if (mon.volatile['Confusion']) {
            this.drawStatusIcon(ctx, iconX, iconY, 'CONF', '#7f8c8d'); // Gray
             iconX += 40;
         }
         if (mon.volatile['Infatuation']) {
             this.drawStatusIcon(ctx, iconX, iconY, 'INFAT', '#e84393'); // Pink
             iconX += 40;
         }
         // Drowsy / Frostbite (Future proofing)
         if (mon.volatile['Drowsy']) {
              this.drawStatusIcon(ctx, iconX, iconY, 'DRWSY', '#95a5a6'); // Gray
              iconX += 40;
         }
         if (mon.volatile['Frostbite']) {
             this.drawStatusIcon(ctx, iconX, iconY, 'FRBITE', '#74b9ff'); // Light Blue
             iconX += 45;
         }

    }

    private drawStatusIcon(ctx: CanvasRenderingContext2D, x: number, y: number, text: string, color: string): void {
        const width = ctx.measureText(text).width + 6;
        const height = 10;
        
        ctx.fillStyle = color;
        ctx.fillRect(x, y, width, height);
        
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 8px monospace';
        ctx.textAlign = 'left';
        ctx.fillText(text, x + 3, y + 8);
    }
  
  private renderLevelUpBox(ctx: CanvasRenderingContext2D, width: number, height: number): void {
      if (!this.levelUpData) return;

      const boxWidth = 180;
      const boxHeight = 180;
      const boxX = width - 460;
      const boxY = height - 320;

      ctx.save();

      ctx.fillStyle = '#f8f8f8';
      ctx.fillRect(boxX, boxY, boxWidth, boxHeight);

      ctx.strokeStyle = '#0066cc';
      ctx.lineWidth = 4;
      ctx.strokeRect(boxX, boxY, boxWidth, boxHeight);

      ctx.fillStyle = '#000';
      ctx.font = 'bold 14px monospace';

      const stats = [
          { name: 'HP', key: 'hp' },
          { name: 'Attack', key: 'attack' },
          { name: 'Defense', key: 'defense' },
          { name: 'Sp. Atk', key: 'spAttack' },
          { name: 'Sp. Def', key: 'spDefense' },
          { name: 'Speed', key: 'speed' }
      ];

      const startY = boxY + 30;
      const lineHeight = 24;

      stats.forEach((stat, index) => {
          const y = startY + index * lineHeight;
          const diff = this.levelUpData.diff[stat.key];
          const oldVal = this.levelUpData.oldStats[stat.key];
          const newVal = this.levelUpData.newStats[stat.key];

          const displayVal = this.state === 'LEVEL_UP_STATS' ? oldVal : newVal;

          ctx.font = 'bold 14px monospace';
          ctx.fillStyle = '#000';
          ctx.fillText(stat.name, boxX + 15, y);

          ctx.font = 'bold 14px monospace';
          ctx.fillStyle = '#000';
          ctx.fillText(displayVal.toString().padStart(3, ' '), boxX + 90, y);

          if (this.state === 'LEVEL_UP_STATS') {
              ctx.font = 'bold 14px monospace';
              ctx.fillStyle = diff > 0 ? '#00aa00' : '#aa0000';
              ctx.fillText(`+${diff}`, boxX + 130, y);
          }
      });

      ctx.fillStyle = '#0066cc';
      ctx.fillRect(boxX + 1, boxY + boxHeight - 1, boxWidth - 2, 4);

      ctx.restore();
  }



  private async executeSwitch(newPokemon: PokemonInstance): Promise<void> {
      if (!this.playerPokemon || !this.enemyPokemon) return;
      
      console.log(`[BattleScene] Swapping ${this.getPokemonDisplayName(this.playerPokemon)} for ${this.getPokemonDisplayName(newPokemon)}`);
      
      // 1. Text: Come back
      await this.showText(`Come back, ${this.getPokemonDisplayName(this.playerPokemon)}!`);
      
      // 2. Visuals: Withdraw
      // TODO: Add withdraw animation
      await new Promise(r => setTimeout(r, 500));
      
      // 3. Swap Data
      this.playerPokemon = newPokemon;
      
      // 4. Visuals: Send Out
      // TODO: Add send out animation / update sprite
      await this.showText(`Go! ${this.getPokemonDisplayName(this.playerPokemon)}!`);
      await new Promise(r => setTimeout(r, 500));
      
      // Trigger Switch-In Ability
      await AbilityRegistry.trigger(this.playerPokemon.ability, 'onBattleStart', { owner: this.playerPokemon, battle: this });
      
      // 5. Enemy Turn (Sacrifice)
      console.log(`[BattleScene] Enemy gets free turn due to switch.`);
      
      await this.executeEnemyTurn();
  }

  private drawFallbackPokeball(ctx: CanvasRenderingContext2D, centerX: number, centerY: number, size: number): void {
      const radius = size / 2;
      
      // Draw outer circle (red top half)
      ctx.save();
      ctx.beginPath();
      ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
      ctx.fillStyle = '#ff0000';
      ctx.fill();
      
      // Draw white bottom half
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(centerX - radius, centerY, radius * 2, radius);
      
      // Draw black middle line
      ctx.strokeStyle = '#000000';
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(centerX - radius, centerY);
      ctx.lineTo(centerX + radius, centerY);
      ctx.stroke();
      
      // Draw center circle
      ctx.beginPath();
      ctx.arc(centerX, centerY, radius * 0.3, 0, Math.PI * 2);
      ctx.fillStyle = '#ffffff';
      ctx.fill();
      ctx.strokeStyle = '#000000';
      ctx.lineWidth = 2;
      ctx.stroke();
      
      // Draw outer black border
      ctx.beginPath();
      ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
      ctx.strokeStyle = '#000000';
      ctx.lineWidth = 3;
      ctx.stroke();
      
      ctx.restore();
  }
}
