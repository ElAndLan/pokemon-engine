export type Listener<T = any> = (data: T) => void;

export class EventBus {
  private listeners: Map<string, Listener[]> = new Map();

  public on<T>(event: string, fn: Listener<T>): void {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, []);
    }
    this.listeners.get(event)?.push(fn);
  }

  public off<T>(event: string, fn: Listener<T>): void {
    const callbacks = this.listeners.get(event);
    if (!callbacks) return;
    const index = callbacks.indexOf(fn);
    if (index !== -1) {
      callbacks.splice(index, 1);
    }
  }

  public emit<T>(event: string, data?: T): void {
    const callbacks = this.listeners.get(event);
    if (!callbacks) return;
    callbacks.forEach((fn) => fn(data));
  }

  public clear(): void {
    this.listeners.clear();
  }
}
