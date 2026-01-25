import type { BagItem, BagData, ItemCategory, ItemData } from './data/ItemData';

export class BagSystem {
  private items: Map<string, number> = new Map(); // itemId -> quantity

  constructor() {
    console.log('[BagSystem] Initialized');
  }

  /**
   * Add an item to the bag
   */
  public addItem(itemId: string, quantity: number = 1): void {
    const current = this.items.get(itemId) || 0;
    this.items.set(itemId, current + quantity);
    console.log(`[BagSystem] Added ${quantity}x ${itemId}. Total: ${current + quantity}`);
  }

  /**
   * Remove an item from the bag
   */
  public removeItem(itemId: string, quantity: number = 1): boolean {
    const current = this.items.get(itemId) || 0;
    if (current < quantity) {
      console.warn(`[BagSystem] Cannot remove ${quantity}x ${itemId}. Only have ${current}`);
      return false;
    }
    
    const newQuantity = current - quantity;
    if (newQuantity <= 0) {
      this.items.delete(itemId);
    } else {
      this.items.set(itemId, newQuantity);
    }
    
    console.log(`[BagSystem] Removed ${quantity}x ${itemId}. Remaining: ${newQuantity}`);
    return true;
  }

  /**
   * Check if player has an item
   */
  public hasItem(itemId: string, quantity: number = 1): boolean {
    const current = this.items.get(itemId) || 0;
    return current >= quantity;
  }

  /**
   * Get quantity of an item
   */
  public getQuantity(itemId: string): number {
    return this.items.get(itemId) || 0;
  }

  /**
   * Get all items as array
   */
  public getAllItems(): BagItem[] {
    const result: BagItem[] = [];
    this.items.forEach((quantity, itemId) => {
      result.push({ itemId, quantity });
    });
    return result;
  }

  /**
   * Get items by category (requires DataManager to filter)
   */
  public getItemsByCategory(category: ItemCategory, dataManager: any): BagItem[] {
    const allItems = this.getAllItems();
    return allItems.filter(bagItem => {
      const itemData = dataManager.getItem(bagItem.itemId);
      return itemData && itemData.category === category;
    });
  }

  /**
   * Clear all items (for testing)
   */
  public clear(): void {
    this.items.clear();
    console.log('[BagSystem] Cleared all items');
  }

  /**
   * Serialize for save system
   */
  public serialize(): BagData {
    return {
      items: this.getAllItems()
    };
  }

  /**
   * Deserialize from save system
   */
  public deserialize(data: BagData): void {
    this.items.clear();
    if (data && data.items) {
      data.items.forEach(item => {
        this.items.set(item.itemId, item.quantity);
      });
    }
    console.log(`[BagSystem] Loaded ${this.items.size} item types`);
  }

  /**
   * Debug: Add starter items
   */
  public debugAddStarterItems(): void {
    this.addItem('potion', 5);
    this.addItem('super_potion', 2);
    this.addItem('pokeball', 10);
    this.addItem('great_ball', 5);
    this.addItem('antidote', 3);
    this.addItem('paralyze_heal', 2);
    console.log('[BagSystem] Added debug starter items');
  }

  /**
   * Debug: Add 99 of every item
   */
  public debugAddAllItems(items: ItemData[]): void {
      for (const item of items) {
          this.addItem(item.id, 99);
      }
      console.log(`[BagSystem] Added 99x of ${items.length} items`);
  }
}
