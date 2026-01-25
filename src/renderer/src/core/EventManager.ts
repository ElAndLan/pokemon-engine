
import { Game, GameState } from './Game';

export interface ScriptCommand {
  type: 'dialog' | 'giveItem' | 'heal' | 'playSound' | 'npcAction' | 'npcWalk' | 'battle' | 'wait';
  [key: string]: any;
}

export class EventManager {
  private game: Game;
  private scripts: { [id: string]: ScriptCommand[] } = {};
  
 // Execution State
  private currentScript: ScriptCommand[] | null = null;
  private currentStepIndex: number = 0;
  private isRunning: boolean = false;
  private currentContext: { targetNpcId?: string } | null = null;

  constructor(game: Game) {
      this.game = game;
      this.loadScripts();
  }

  private async loadScripts() {
      try {
          // In a real app, load from file. For now, hardcode or load a simple JSON.
          const res = await (window as any).fs.readFile('data/db/scripts.json');
          if (res.success) {
              this.scripts = JSON.parse(res.data);
              console.log(`[EventManager] Loaded ${Object.keys(this.scripts).length} scripts.`);
          } else {
              console.warn('[EventManager] scripts.json not found, using empty registry.');
              this.scripts = {};
          }
      } catch (e) {
          console.error('[EventManager] Failed to load scripts', e);
      }
  }

  public async runScript(id: string, context?: { targetNpcId?: string }): Promise<void> {
      if (this.isRunning) return; // Prevent overlapping scripts for now
      
      const script = this.scripts[id];
      if (script) {
          console.log(`[EventManager] Starting script: ${id} with context:`, context);
          this.currentScript = script;
          this.currentStepIndex = 0;
          this.currentContext = context || null;
          this.isRunning = true;
          this.game.state = GameState.Script; // Pause Overworld input
          
          await this.executeScriptLoop();
      } else {
          console.warn(`[EventManager] Script not found: ${id}`);
      }
  }

  public resume(): void {
      // Used for non-promise based waits (standard dialog callback)
      // We can use a resolver if we are awaiting a promise.
      if (this.resumeResolver) {
          this.resumeResolver();
          this.resumeResolver = null;
      }
  }

  private resumeResolver: (() => void) | null = null;
  
  private waitForResume(): Promise<void> {
      return new Promise(resolve => {
          this.resumeResolver = resolve;
      });
  }

  private async executeScriptLoop(): Promise<void> {
      while (this.isRunning && this.currentScript && this.currentStepIndex < this.currentScript.length) {
          const cmd = this.currentScript[this.currentStepIndex];
          console.log(`[EventManager] Executing Step ${this.currentStepIndex}: ${cmd.type}`, cmd);
          
          await this.executeCommand(cmd);
          
          this.currentStepIndex++;
      }
      this.finishScript();
  }

  private async executeCommand(cmd: ScriptCommand): Promise<void> {
      switch (cmd.type) {
          case 'dialog':
              console.log(`[EventManager] Showing dialog: "${cmd.text}"`);
              this.game.dialogBox.show(cmd.text);
              console.log(`[EventManager] DialogBox.isVisible: ${this.game.dialogBox.isVisible}`);
              await this.waitForResume(); // Wait for DialogBox to call resume()
              console.log(`[EventManager] Dialog closed, continuing script`);
              break;
              
          case 'heal':
              console.log('[EventManager] Healing Party...');
              // TODO: Implement party heal
              await new Promise(r => setTimeout(r, 500)); // Fake visual wait
              break;
              
          case 'npcAction':
              await this.handleNpcAction(cmd);
              break;
              
          case 'npcWalk':
              await this.handleNpcWalk(cmd);
              break;

           case 'battle':
              await this.handleBattleTrigger(cmd);
              break;

          case 'giveItem':
               console.log(`[EventManager] Giving Item: ${cmd.itemId} x${cmd.count || 1}`);
               this.game.dialogBox.show(`You found ${cmd.itemId}!`);
               await this.waitForResume();
               break;

          case 'playSound':
              // Play Sound
              break;

          case 'wait':
              const ms = cmd.ms || 1000;
              await new Promise(r => setTimeout(r, ms));
              break;

          default:
              console.warn(`[EventManager] Unknown command: ${cmd.type}`);
              break;
      }
  }
  
  private async handleNpcAction(cmd: any): Promise<void> {
      const npc = this.resolveNpc(cmd.targetId);
      if (npc) {
          if (cmd.action === 'face') {
              npc.face(cmd.direction);
          } else if (cmd.action === 'hop') {
              npc.hop();
              // Hop is visual, maybe wait specific time?
              if (cmd.wait) await new Promise(r => setTimeout(r, 500));
          } else if (cmd.action === 'emote') {
              // TODO
          }
      }
  }

  private async handleNpcWalk(cmd: any): Promise<void> {
      const npc = this.resolveNpc(cmd.targetId);
      if (!npc) return;

      // Get movement deltas
      const dx = parseInt(cmd.x) || 0;
      const dy = parseInt(cmd.y) || 0;
      
      console.log(`[EventManager] Moving NPC ${cmd.targetId} by (${dx}, ${dy})`);

      // Walk tile by tile for smooth animation
      const stepsX = Math.abs(dx);
      const stepsY = Math.abs(dy);
      const dirX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
      const dirY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);

      // Walk horizontally first - ignore collision during scripted events
      for (let i = 0; i < stepsX; i++) {
          await npc.walk(dirX, 0, this.game.map, true);
      }

      // Then walk vertically - ignore collision during scripted events
      for (let i = 0; i < stepsY; i++) {
          await npc.walk(0, dirY, this.game.map, true);
      }

      console.log(`[EventManager] NPC movement complete`);
  }
  
  private async handleBattleTrigger(cmd: any): Promise<void> {
      console.log('[EventManager] Starting Battle...');
      // this.game.startBattle(cmd.enemyId);
      // await this.waitForResume(); // Game needs to call resume() after battle
      // Placeholder
      this.game.dialogBox.show(`Battle against ${cmd.enemyId}!`);
      await this.waitForResume();
  }

  private resolveNpc(targetId?: string): any {
      if (!targetId || targetId === 'Player') return this.game.player; // Maybe handle player?
      
      // Fallback to Context ID
      if ((!targetId || targetId === 'this') && this.currentContext?.targetNpcId) {
          targetId = this.currentContext.targetNpcId;
      }
      
      return this.game.npcList.find(n => n.uniqueId === targetId);
  }

  private finishScript(): void {
      console.log('[EventManager] Script Finished.');
      this.isRunning = false;
      this.currentScript = null;
      this.game.state = GameState.Overworld;
  }
}
