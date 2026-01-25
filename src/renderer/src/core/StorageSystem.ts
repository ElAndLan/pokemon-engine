import { PokemonInstance } from './data/DataTypes';
import { DataManager } from './data/DataManager';

export class StorageSystem {
    public boxes: PokemonInstance[][] = [];
    public currentBox: number = 0;
    private readonly MAX_BOXES = 32;
    private readonly BOX_CAPACITY = 30;

    constructor() {
        // Init empty boxes
        for(let i=0; i<this.MAX_BOXES; i++) {
            this.boxes.push([]);
        }
    }

    public addPokemon(pokemon: PokemonInstance, boxIndex?: number): boolean {
        // If specific box requested, try that first
        if (boxIndex !== undefined && boxIndex >= 0 && boxIndex < this.MAX_BOXES) {
            if (this.boxes[boxIndex].length < this.BOX_CAPACITY) {
                this.boxes[boxIndex].push(pokemon);
                return true;
            }
        }

        // Fallback: Find first free slot
        for (let i = 0; i < this.boxes.length; i++) {
            if (this.boxes[i].length < this.BOX_CAPACITY) {
                this.boxes[i].push(pokemon);
                return true;
            }
        }
        return false; // Full
    }

    public getBox(index: number): PokemonInstance[] {
        return this.boxes[index];
    }

    public debugPopulate(dataManager: DataManager): void {
        console.log('[Storage] Debug Populating PC...');
        const allSpecies = dataManager.getAllSpecies();
        // Sort by ID
        allSpecies.sort((a, b) => parseInt(a.id) - parseInt(b.id));

        for (const species of allSpecies) {
             const mon = dataManager.createPokemonInstance(species, 20);
             // Ensure moves are max PP? 
             mon.moves.forEach(m => {
                 const moveData = dataManager.getMove(m.moveId);
                 if (moveData) {
                     m.maxPp = moveData.pp;
                     m.pp = moveData.pp;
                 }
             });
             this.addPokemon(mon);
        }
        console.log(`[Storage] Populated with ${allSpecies.length} Pokemon.`);
    }
}
