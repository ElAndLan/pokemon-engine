import { PokemonInstance, Stats, StatName, StatusCondition, PokemonType } from '../data/DataTypes';

export class BattleSim {
    /**
     * Generates a mock Pokemon instance for testing
     */
    public static createMockMon(name: string, types: PokemonType[], stats: Partial<Stats> = {}): PokemonInstance {
        const fullStats: Stats = {
            hp: stats.hp || 100,
            attack: stats.attack || 50,
            defense: stats.defense || 50,
            spAttack: stats.spAttack || 50,
            spDefense: stats.spDefense || 50,
            speed: stats.speed || 50,
            accuracy: 100,
            evasion: 100
        };

        return {
            uuid: `mock-${name}-${Math.random().toString(36).substr(2, 9)}`,
            speciesId: name.toLowerCase(),
            nickname: name,
            types: types,
            level: 50,
            experience: 0,
            originalTrainer: 'Trainer',
            ivs: { ...fullStats },
            evs: { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 },
            nature: 'Neutral',
            ability: 'None',
            gender: 'Genderless',
            shiny: false,
            moves: [],
            currentHp: fullStats.hp,
            currentStats: { ...fullStats },
            statStages: {
                attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0, accuracy: 0, evasion: 0
            },
            status: 'None',
            volatile: {},
            heldItem: undefined
        };
    }

    /**
     * Resets a Pokemon to starting test state
     */
    public static resetMon(mon: PokemonInstance): void {
        mon.currentHp = mon.currentStats.hp;
        mon.status = 'None';
        mon.volatile = {};
        mon.statStages = {
             attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0, accuracy: 0, evasion: 0
        };
    }
}
