const pokedex = require('../../data/db/pokedex.json');
const moves = require('../../data/db/moves.json');
const encounters = require('../../data/db/encounters.json');

/**
 * Manages wild Pokemon encounters with proper IV generation, movesets, and shiny determination
 */
class EncounterManager {
    constructor() {
        this.encounters = encounters;
        this.pokedex = pokedex;
        this.moves = moves;
        this.shinyChance = 8192; // 1/8192 base shiny chance
    }

    /**
     * Generate a wild encounter for a specific route and encounter type
     * @param {string} routeId - The route identifier (e.g., 'route_1')
     * @param {string} encounterType - Type of encounter (e.g., 'grass', 'surf', 'oldRod')
     * @returns {Object|null} Generated wild Pokemon or null if no encounter
     */
    generateEncounter(routeId, encounterType = 'grass') {
        const route = this.encounters[routeId];
        if (!route || !route[encounterType]) {
            console.warn(`No ${encounterType} encounters found for ${routeId}`);
            return null;
        }

        // Select Pokemon based on weighted rates
        const selectedEncounter = this.weightedRandomSelection(route[encounterType]);
        if (!selectedEncounter) return null;

        // Generate the wild Pokemon
        return this.createWildPokemon(selectedEncounter);
    }

    /**
     * Weighted random selection from encounter table
     * @param {Array} encounterTable - Array of encounter objects with rates
     * @returns {Object|null} Selected encounter or null
     */
    weightedRandomSelection(encounterTable) {
        const totalRate = encounterTable.reduce((sum, enc) => sum + enc.rate, 0);
        const roll = Math.random() * totalRate;
        
        let cumulative = 0;
        for (const encounter of encounterTable) {
            cumulative += encounter.rate;
            if (roll < cumulative) {
                return encounter;
            }
        }
        
        // Fallback to last entry if rounding causes issues
        return encounterTable[encounterTable.length - 1];
    }

    /**
     * Generate a random level within the specified range
     * @param {Array} levelRange - [min, max] level range
     * @returns {number} Random level
     */
    randomLevel(levelRange) {
        const [min, max] = levelRange;
        return Math.floor(Math.random() * (max - min + 1)) + min;
    }

    /**
     * Generate random IVs (0-31 for each stat)
     * @returns {Object} IV object with hp, attack, defense, spAttack, spDefense, speed
     */
    generateIVs() {
        return {
            hp: Math.floor(Math.random() * 32),
            attack: Math.floor(Math.random() * 32),
            defense: Math.floor(Math.random() * 32),
            spAttack: Math.floor(Math.random() * 32),
            spDefense: Math.floor(Math.random() * 32),
            speed: Math.floor(Math.random() * 32)
        };
    }

    /**
     * Determine if Pokemon should be shiny
     * @returns {boolean} True if shiny
     */
    isShiny() {
        return Math.floor(Math.random() * this.shinyChance) === 0;
    }

    /**
     * Get the 4 strongest moves a Pokemon should know at a given level
     * @param {string} pokemonId - Pokemon ID
     * @param {number} level - Pokemon level
     * @returns {Array} Array of up to 4 move IDs
     */
    getMovesForLevel(pokemonId, level) {
        const pokemon = this.pokedex[pokemonId];
        if (!pokemon || !pokemon.learnset) return [];

        // Get all moves learnable up to this level
        const learnableMoves = pokemon.learnset
            .filter(move => move.level <= level)
            .map(move => ({
                moveId: move.moveId,
                learnLevel: move.level,
                power: this.moves[move.moveId]?.power || 0
            }));

        if (learnableMoves.length === 0) return [];

        // Sort by learn level (descending) then by power (descending)
        learnableMoves.sort((a, b) => {
            if (b.learnLevel !== a.learnLevel) {
                return b.learnLevel - a.learnLevel;
            }
            return b.power - a.power;
        });

        // Take the 4 strongest/most recent moves
        return learnableMoves.slice(0, 4).map(m => m.moveId);
    }

    /**
     * Generate a random nature
     * @returns {string} Nature name
     */
    randomNature() {
        const natures = [
            'Hardy', 'Lonely', 'Brave', 'Adamant', 'Naughty',
            'Bold', 'Docile', 'Relaxed', 'Impish', 'Lax',
            'Timid', 'Hasty', 'Serious', 'Jolly', 'Naive',
            'Modest', 'Mild', 'Quiet', 'Bashful', 'Rash',
            'Calm', 'Gentle', 'Sassy', 'Careful', 'Quirky'
        ];
        return natures[Math.floor(Math.random() * natures.length)];
    }

    /**
     * Create a complete wild Pokemon instance
     * @param {Object} encounter - Encounter data with pokemonId and level range
     * @returns {Object} Complete wild Pokemon object
     */
    createWildPokemon(encounter) {
        const pokemonId = encounter.pokemonId;
        const pokemonData = this.pokedex[pokemonId];
        
        if (!pokemonData) {
            console.error(`Pokemon ${pokemonId} not found in pokedex`);
            return null;
        }

        const level = this.randomLevel(encounter.level);
        const ivs = this.generateIVs();
        const isShiny = this.isShiny();
        const nature = this.randomNature();
        const moveset = this.getMovesForLevel(pokemonId, level);

        // Calculate stats based on base stats, IVs, and level
        const stats = this.calculateStats(pokemonData.baseStats, ivs, level, nature);

        return {
            id: pokemonId,
            name: pokemonData.name,
            level: level,
            types: pokemonData.types,
            baseStats: pokemonData.baseStats,
            stats: stats,
            ivs: ivs,
            evs: { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 },
            nature: nature,
            moves: moveset,
            currentHP: stats.hp,
            maxHP: stats.hp,
            isShiny: isShiny,
            ability: this.randomAbility(pokemonData.possibleAbilities),
            experience: this.experienceForLevel(level),
            friendship: pokemonData.catchRate || 70,
            status: null,
            isWild: true
        };
    }

    /**
     * Calculate actual stats from base stats, IVs, level, and nature
     * @param {Object} baseStats - Base stat object
     * @param {Object} ivs - IV object
     * @param {number} level - Pokemon level
     * @param {string} nature - Nature name
     * @returns {Object} Calculated stats
     */
    calculateStats(baseStats, ivs, level, nature) {
        const evs = { hp: 0, attack: 0, defense: 0, spAttack: 0, spDefense: 0, speed: 0 };
        
        // HP calculation (different formula)
        const hp = Math.floor(((2 * baseStats.hp + ivs.hp + Math.floor(evs.hp / 4)) * level) / 100) + level + 10;
        
        // Other stats calculation
        const calcStat = (base, iv, ev, statName) => {
            const baseStat = Math.floor(((2 * base + iv + Math.floor(ev / 4)) * level) / 100) + 5;
            return Math.floor(baseStat * this.getNatureModifier(nature, statName));
        };

        return {
            hp: hp,
            attack: calcStat(baseStats.attack, ivs.attack, evs.attack, 'attack'),
            defense: calcStat(baseStats.defense, ivs.defense, evs.defense, 'defense'),
            spAttack: calcStat(baseStats.spAttack, ivs.spAttack, evs.spAttack, 'spAttack'),
            spDefense: calcStat(baseStats.spDefense, ivs.spDefense, evs.spDefense, 'spDefense'),
            speed: calcStat(baseStats.speed, ivs.speed, evs.speed, 'speed')
        };
    }

    /**
     * Get nature modifier for a stat
     * @param {string} nature - Nature name
     * @param {string} stat - Stat name
     * @returns {number} Modifier (0.9, 1.0, or 1.1)
     */
    getNatureModifier(nature, stat) {
        const natureEffects = {
            'Lonely': { attack: 1.1, defense: 0.9 },
            'Brave': { attack: 1.1, speed: 0.9 },
            'Adamant': { attack: 1.1, spAttack: 0.9 },
            'Naughty': { attack: 1.1, spDefense: 0.9 },
            'Bold': { defense: 1.1, attack: 0.9 },
            'Relaxed': { defense: 1.1, speed: 0.9 },
            'Impish': { defense: 1.1, spAttack: 0.9 },
            'Lax': { defense: 1.1, spDefense: 0.9 },
            'Timid': { speed: 1.1, attack: 0.9 },
            'Hasty': { speed: 1.1, defense: 0.9 },
            'Jolly': { speed: 1.1, spAttack: 0.9 },
            'Naive': { speed: 1.1, spDefense: 0.9 },
            'Modest': { spAttack: 1.1, attack: 0.9 },
            'Mild': { spAttack: 1.1, defense: 0.9 },
            'Quiet': { spAttack: 1.1, speed: 0.9 },
            'Rash': { spAttack: 1.1, spDefense: 0.9 },
            'Calm': { spDefense: 1.1, attack: 0.9 },
            'Gentle': { spDefense: 1.1, defense: 0.9 },
            'Sassy': { spDefense: 1.1, speed: 0.9 },
            'Careful': { spDefense: 1.1, spAttack: 0.9 }
        };

        const effects = natureEffects[nature];
        return effects?.[stat] || 1.0;
    }

    /**
     * Select random ability from possible abilities
     * @param {Array} abilities - Array of ability names
     * @returns {string} Selected ability
     */
    randomAbility(abilities) {
        if (!abilities || abilities.length === 0) return 'None';
        return abilities[Math.floor(Math.random() * abilities.length)];
    }

    /**
     * Calculate experience points for a given level
     * @param {number} level - Pokemon level
     * @returns {number} Experience points
     */
    experienceForLevel(level) {
        // Medium Fast growth rate formula
        return Math.floor(Math.pow(level, 3));
    }

    /**
     * Set custom shiny chance (for shiny charm, etc.)
     * @param {number} chance - New shiny chance denominator (e.g., 4096 for 1/4096)
     */
    setShinyChance(chance) {
        this.shinyChance = chance;
    }
}

module.exports = EncounterManager;
