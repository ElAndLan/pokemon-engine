/**
 * UndoManager - Manages undo/redo history for the editor
 * Supports tile placement, fills, rectangles, and object operations
 */

export type ActionType = 
  | 'tile_place'
  | 'tile_fill'
  | 'tile_rect'
  | 'object_place'
  | 'object_delete'
  | 'object_move'
  | 'map_resize';

export interface TileChange {
  x: number;
  y: number;
  before: number;
  after: number;
}

export interface EditorAction {
  type: ActionType;
  timestamp: number;
  description: string;  // "Place tile", "Fill area", etc.
  data: {
    layer?: string;
    tiles?: TileChange[];  // Array of tile changes
    objects?: any[];
    before?: any;
    after?: any;
  };
}

export class UndoManager {
  private undoStack: EditorAction[] = [];
  private redoStack: EditorAction[] = [];
  private maxHistory: number = 100;
  
  constructor() {
    console.log('[UndoManager] Initialized with max history:', this.maxHistory);
  }
  
  /**
   * Record a new action to the undo stack
   */
  record(action: EditorAction): void {
    this.undoStack.push(action);
    this.redoStack = [];  // Clear redo stack on new action
    
    // Limit history size
    if (this.undoStack.length > this.maxHistory) {
      const removed = this.undoStack.shift();
      console.log('[UndoManager] History limit reached, removed oldest action:', removed?.description);
    }
    
    console.log(`[UndoManager] Recorded: ${action.description} (Stack: ${this.undoStack.length})`);
  }
  
  /**
   * Undo the last action
   */
  undo(): EditorAction | null {
    const action = this.undoStack.pop();
    if (!action) {
      console.log('[UndoManager] Nothing to undo');
      return null;
    }
    
    this.redoStack.push(action);
    console.log(`[UndoManager] Undo: ${action.description} (Undo: ${this.undoStack.length}, Redo: ${this.redoStack.length})`);
    return action;
  }
  
  /**
   * Redo the last undone action
   */
  redo(): EditorAction | null {
    const action = this.redoStack.pop();
    if (!action) {
      console.log('[UndoManager] Nothing to redo');
      return null;
    }
    
    this.undoStack.push(action);
    console.log(`[UndoManager] Redo: ${action.description} (Undo: ${this.undoStack.length}, Redo: ${this.redoStack.length})`);
    return action;
  }
  
  /**
   * Check if undo is available
   */
  canUndo(): boolean {
    return this.undoStack.length > 0;
  }
  
  /**
   * Check if redo is available
   */
  canRedo(): boolean {
    return this.redoStack.length > 0;
  }
  
  /**
   * Get description of the next undo action
   */
  getUndoDescription(): string | null {
    const action = this.undoStack[this.undoStack.length - 1];
    return action ? action.description : null;
  }
  
  /**
   * Get description of the next redo action
   */
  getRedoDescription(): string | null {
    const action = this.redoStack[this.redoStack.length - 1];
    return action ? action.description : null;
  }
  
  /**
   * Clear all history
   */
  clear(): void {
    this.undoStack = [];
    this.redoStack = [];
    console.log('[UndoManager] History cleared');
  }
  
  /**
   * Get current stack sizes (for debugging)
   */
  getStackSizes(): { undo: number; redo: number } {
    return {
      undo: this.undoStack.length,
      redo: this.redoStack.length
    };
  }
}
