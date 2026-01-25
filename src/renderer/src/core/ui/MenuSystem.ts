export interface Menu {
  update(dt: number): void;
  render(ctx: CanvasRenderingContext2D): void;
  onOpen?(): void;
  onClose?(): void;
}

export class MenuSystem {
  private stack: Menu[] = [];

  constructor() {}

  public push(menu: Menu): void {
    if (menu.onOpen) menu.onOpen();
    this.stack.push(menu);
  }

  public pop(): void {
    const menu = this.stack.pop();
    if (menu && menu.onClose) menu.onClose();
  }

  public clear(): void {
    while (this.stack.length > 0) {
      this.pop();
    }
  }

  public get activeMenu(): Menu | undefined {
    return this.stack[this.stack.length - 1];
  }

  public get isOpen(): boolean {
    return this.stack.length > 0;
  }

  public update(dt: number): void {
    if (this.activeMenu) {
      this.activeMenu.update(dt);
    }
  }

  public render(ctx: CanvasRenderingContext2D): void {
    // Render all menus in stack? Or just top?
    // Usually we want to render all from bottom up so backgrounds stack
    // But for now, let's just render the top one to keep it simple, 
    // or render all if we want transparency.
    // Let's render all.
    for (const menu of this.stack) {
        menu.render(ctx);
    }
  }
}
