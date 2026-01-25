export class InputManager {
  private keys: Set<string> = new Set();
  private keysPressed: Set<string> = new Set();
  private keysReleased: Set<string> = new Set();

  constructor() {
    window.addEventListener('keydown', (e) => this.onKeyDown(e));
    window.addEventListener('keyup', (e) => this.onKeyUp(e));
  }

  private onKeyDown(e: KeyboardEvent): void {
    if (!this.keys.has(e.code)) {
      this.keysPressed.add(e.code);
    }
    this.keys.add(e.code);
  }

  private onKeyUp(e: KeyboardEvent): void {
    this.keysReleased.add(e.code);
    this.keys.delete(e.code);
  }

  public update(): void {
    this.keysPressed.clear();
    this.keysReleased.clear();
  }

  public reset(): void {
    this.keys.clear();
    this.keysPressed.clear();
    this.keysReleased.clear();
  }

  public isDown(code: string): boolean {
    return this.keys.has(code);
  }

  public isJustPressed(code: string): boolean {
    return this.keysPressed.has(code);
  }
}
