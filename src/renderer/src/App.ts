
import { Game } from './core/Game';
import { Editor } from './editor/Editor';

export class App {
  private game: Game;
  private editor: Editor;
  
  // UI Elements
  private appContainer: HTMLElement;
  private toolbar: HTMLElement;
  private mainContainer: HTMLElement;
  private gameContainer: HTMLElement;
  
  private isEditorMode: boolean = false; // Default to Game Mode for Title Screen

  constructor() {
    const appEl = document.getElementById('app');
    if (!appEl) throw new Error('Root #app element not found');
    this.appContainer = appEl;
    this.appContainer.innerHTML = ''; // Clear loading text
    
    // 1. Toolbar
    this.toolbar = document.createElement('div');
    this.toolbar.id = 'toolbar';
    this.toolbar.innerHTML = `
        <span style="flex:1; font-weight:bold;">Pokemon Engine</span>
        <button id="mode-toggle" class="mode-toggle">Play Game</button>
    `;
    this.appContainer.appendChild(this.toolbar);
    
    // 2. Main Container (Flex Row)
    this.mainContainer = document.createElement('div');
    this.mainContainer.style.display = 'flex';
    this.mainContainer.style.flex = '1';
    this.mainContainer.style.position = 'relative';
    this.appContainer.appendChild(this.mainContainer);
    
    // 3. Editor Sidebar (Visible by default)
    const sidebarContainer = document.createElement('div');
    sidebarContainer.id = 'editor-sidebar-container';
    sidebarContainer.style.width = '300px'; 
    sidebarContainer.style.minWidth = '300px'; // Force width
    sidebarContainer.style.height = '100%';
    sidebarContainer.style.display = 'block'; 
    sidebarContainer.style.background = '#333'; // Ensure background
    sidebarContainer.style.borderRight = '1px solid #444';
    sidebarContainer.style.boxSizing = 'border-box';
    sidebarContainer.style.flexShrink = '0'; // Prevent shrinking
    this.mainContainer.appendChild(sidebarContainer);
    
    this.editor = new Editor(sidebarContainer);
    
    // 4. Game Container
    this.gameContainer = document.createElement('div');
    this.gameContainer.id = 'game-container';
    this.gameContainer.style.flex = '1';
    this.gameContainer.style.display = 'flex';
    this.gameContainer.style.justifyContent = 'center';
    this.gameContainer.style.alignItems = 'center';
    this.gameContainer.style.background = '#000';
    this.gameContainer.style.overflow = 'hidden'; // Clip canvas
    this.mainContainer.appendChild(this.gameContainer);
    
    // Initialize Game
    // Note: Game creates a new Canvas in 'game-container'
    this.game = new Game('game-container');
    this.editor.attachGame(this.game);
    
    // Events
    document.getElementById('mode-toggle')?.addEventListener('click', () => this.toggleMode());
    
    // Start Game
    this.game.start();
    
    // Initial State Sync
    this.game.setEditorMode(this.isEditorMode);
    if(this.isEditorMode) this.editor.onOpen();
  }
  
  private toggleMode(): void {
      this.isEditorMode = !this.isEditorMode;
      const btn = document.getElementById('mode-toggle');
      const sidebar = document.getElementById('editor-sidebar-container');
      
      // Update Game State
      this.game.setEditorMode(this.isEditorMode);

      if (this.isEditorMode) {
          // EDITOR MODE
          if (sidebar) sidebar.style.display = 'block';
          if (btn) btn.innerText = 'Play Game';
          this.editor.onOpen();
      } else {
          // GAME MODE
          if (sidebar) sidebar.style.display = 'none';
          if (btn) btn.innerText = 'Switch to Editor';
          this.editor.onClose();
          
          // Force Focus to Game
          // this.game.input.focus(); 
      }
  }
}
