import type { PokemonInstance, PokemonSpecies, MoveInstance } from '../data/DataTypes';
import type { MoveData } from '../data/DataTypes';

export interface LearnableMove {
    level: number;
    moveId: string;
    moveName: string;
}

export interface MoveLearningResult {
    learned: boolean;
    moveId?: string;
    moveName?: string;
    reason?: 'new_move' | 'slots_full' | 'already_known';
    replacementOptions?: {
        newMove: LearnableMove;
        currentMoves: MoveInstance[];
    };
}

export class MoveLearningManager {
    private moveCache: Map<string, MoveData> = new Map();

    constructor(moves: { [id: string]: MoveData }) {
        Object.entries(moves).forEach(([id, move]) => {
            this.moveCache.set(id, move);
        });
    }

    /**
     * Check if a Pokemon can learn any new moves at its current level
     */
    public getMovesLearnableAtLevel(species: PokemonSpecies, level: number, pokemon?: PokemonInstance): LearnableMove[] {
        if (!species.learnset) return [];

        const moveIdsAlreadyKnown = new Set<string>();
        if (pokemon) {
            pokemon.moves.forEach(move => {
                moveIdsAlreadyKnown.add(move.moveId);
            });
        }

        const learnableMoves: LearnableMove[] = [];

        species.learnset.forEach(learnMove => {
            if (learnMove.level === level) {
                const moveData = this.moveCache.get(learnMove.moveId);
                if (moveData) {
                    learnableMoves.push({
                        level: learnMove.level,
                        moveId: learnMove.moveId,
                        moveName: moveData.name
                    });
                }
            }
        });

        return learnableMoves.filter(move => !moveIdsAlreadyKnown.has(move.moveId));
    }

    /**
     * Attempt to teach a move to a Pokemon
     * @returns Result of the move learning attempt
     */
    public learnMove(pokemon: PokemonInstance, moveId: string): MoveLearningResult {
        const moveData = this.moveCache.get(moveId);
        if (!moveData) {
            return { learned: false, reason: 'already_known' };
        }

        const alreadyKnowsMove = pokemon.moves.some(m => m.moveId === moveId);
        if (alreadyKnowsMove) {
            return { learned: false, reason: 'already_known' };
        }

        if (pokemon.moves.length < 4) {
            const newMoveInstance = this.createMoveInstance(moveData);
            pokemon.moves.push(newMoveInstance);
            return {
                learned: true,
                moveId: moveData.id,
                moveName: moveData.name,
                reason: 'new_move'
            };
        }

        return {
            learned: false,
            reason: 'slots_full',
            replacementOptions: {
                newMove: {
                    level: pokemon.level,
                    moveId: moveData.id,
                    moveName: moveData.name
                },
                currentMoves: pokemon.moves
            }
        };
    }

    /**
     * Replace a move in the Pokemon's moveset
     */
    public replaceMove(pokemon: PokemonInstance, oldMoveIndex: number, newMoveId: string): MoveLearningResult {
        if (oldMoveIndex < 0 || oldMoveIndex >= pokemon.moves.length) {
            return { learned: false, reason: 'already_known' };
        }

        const moveData = this.moveCache.get(newMoveId);
        if (!moveData) {
            return { learned: false, reason: 'already_known' };
        }

        const newMoveInstance = this.createMoveInstance(moveData);
        const oldMove = pokemon.moves[oldMoveIndex];

        pokemon.moves[oldMoveIndex] = newMoveInstance;

        return {
            learned: true,
            moveId: moveData.id,
            moveName: moveData.name,
            reason: 'new_move'
        };
    }

    /**
     * Get all moves a Pokemon should know at a given level
     * Used for initial Pokemon creation
     */
    public getMovesForLevel(species: PokemonSpecies, level: number): MoveInstance[] {
        if (!species.learnset) return [];

        const learnableMoves = species.learnset
            .filter(m => m.level <= level)
            .map(m => {
                const moveData = this.moveCache.get(m.moveId);
                return {
                    moveId: m.moveId,
                    learnLevel: m.level,
                    power: moveData?.power || 0
                };
            })
            .filter(m => m.moveId !== undefined);

        if (learnableMoves.length === 0) return [];

        learnableMoves.sort((a, b) => {
            if (b.learnLevel !== a.learnLevel) {
                return b.learnLevel - a.learnLevel;
            }
            return b.power - a.power;
        });

        return learnableMoves.slice(0, 4).map(m => this.createMoveInstanceFromId(m.moveId));
    }

    /**
     * Create a MoveInstance from MoveData
     */
    private createMoveInstance(moveData: MoveData): MoveInstance {
        return {
            moveId: moveData.id,
            pp: moveData.pp,
            maxPp: moveData.pp
        };
    }

    /**
     * Create a MoveInstance from move ID
     */
    private createMoveInstanceFromId(moveId: string): MoveInstance {
        const moveData = this.moveCache.get(moveId);
        return {
            moveId: moveId,
            pp: moveData?.pp || 10,
            maxPp: moveData?.pp || 10
        };
    }
}
