export class SaveManager {
  private readonly saveDirectory: string = './saves';

  constructor() {
    console.log('Save Manager Initialized');
  }

  public async checkSave(slot: number): Promise<boolean> {
      const fileName = `${this.saveDirectory}/save_input_${slot}.json`;
      const result = await window.fs.readFile(fileName);
      return result.success;
  }

  public async saveGame(slot: number, data: object): Promise<boolean> {
    const fileName = `${this.saveDirectory}/save_input_${slot}.json`;
    const json = JSON.stringify(data, null, 2);
    
    // In a real app, we'd ensure the directory exists first or use a known user data path.
    // For this prototype, we rely on the main process handling the writes to the working dir.
    const result = await window.fs.writeFile(fileName, json);
    if (!result.success) {
      console.error('Failed to save game:', result.error);
      return false;
    }
    console.log('Game Saved to', fileName);
    return true;
  }

  public async loadGame(slot: number): Promise<any | null> {
    const fileName = `${this.saveDirectory}/save_input_${slot}.json`;
    const result = await window.fs.readFile(fileName);
    
    if (!result.success || !result.data) {
      console.error('Failed to load game:', result.error);
      return null;
    }

    try {
      return JSON.parse(result.data);
    } catch (e) {
      console.error('Failed to parse save file:', e);
      return null;
    }
  }
}
