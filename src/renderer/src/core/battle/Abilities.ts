import { BattleScene } from './BattleScene';
import { PokemonInstance, MoveData } from '../data/DataTypes';

export interface AbilityContext {
    owner: PokemonInstance;
    target?: PokemonInstance;
    battle?: BattleScene;
    move?: MoveData;
    damage?: number;
    variables?: Record<string, any>; // For passing data between events
}

export interface Ability {
    id: string;
    name: string;
    description: string;

    // --- BATTLE FLOW HOOKS ---
    
    // Triggered when the Pokemon enters battle (Start or Switch-in)
    onBattleStart?: (ctx: AbilityContext) => Promise<void>;
    
    // Triggered at the very start of a turn (before move selection or execution order)
    onTurnStart?: (ctx: AbilityContext) => Promise<void>;

    // Triggered when checking if a move can be used or fails
    onBeforeMove?: (ctx: AbilityContext) => Promise<boolean>; // return false to cancel move

    // Triggered when calculating base stats or effective stats
    onStatCalculation?: (stat: string, value: number, ctx: AbilityContext) => number;

    // --- DAMAGE HOOKS ---

    // Modifier to Attack/SpAttack of the owner
    onModifyAttack?: (value: number, ctx: AbilityContext) => number;
    
    // Modifier to Defense/SpDefense of the target (when owner is being attacked)
    onModifyDefense?: (value: number, ctx: AbilityContext) => number;

    // Final Damage Multiplier (e.g. Filter, Solid Rock)
    onDamageMultiplier?: (value: number, ctx: AbilityContext) => number;

    // --- POST ACTION HOOKS ---
    
    // After taking damage
    onAfterDamage?: (damageTaken: number, ctx: AbilityContext) => Promise<void>;

    // End of turn
    onTurnEnd?: (ctx: AbilityContext) => Promise<void>;
}

export class AbilityRegistry {
    private static abilities: Map<string, Ability> = new Map();

    static register(ability: Ability) {
        this.abilities.set(ability.id, ability);
    }

    static get(id: string): Ability | undefined {
        return this.abilities.get(id);
    }

    static has(id: string): boolean {
        return this.abilities.has(id);
    }

    // --- HELPER WRAPPERS ---
    
    static async trigger(id: string, hook: keyof Ability, ctx: AbilityContext, ...args: any[]): Promise<any> {
        const ability = this.get(id);
        if (ability && ability[hook]) {
            // @ts-ignore
            return await ability[hook](ctx, ...args);
        }
        return undefined;
    }

    static applyModifier(id: string, hook: keyof Ability, initialValue: number, ctx: AbilityContext): number {
        const ability = this.get(id);
        if (ability && ability[hook]) {
            // @ts-ignore
            return ability[hook](initialValue, ctx);
        }
        return initialValue;
    }
}

// --- EXAMPLE IMPLEMENTATION (Ideally move to separate files) ---
const Overgrow: Ability = {
    id: 'Overgrow',
    name: 'Overgrow',
    description: 'Powers up Grass-type moves when the Pokémon\'s HP is low.',
    
    onModifyAttack: (value: number, ctx: AbilityContext) => {
        if (ctx.move?.type === 'Grass' && ctx.owner.currentHp <= (ctx.owner.currentStats.hp / 3)) {
            console.log('[Ability] Overgrow activated! (1.5x Attack)');
            return value * 1.5;
        }
        return value;
    }
};

AbilityRegistry.register(Overgrow);
const Blaze: Ability = {
    id: 'Blaze',
    name: 'Blaze',
    description: 'Powers up Fire-type moves when the Pokémon\'s HP is low.',
    
    onModifyAttack: (value: number, ctx: AbilityContext) => {
        if (ctx.move?.type === 'Fire' && ctx.owner.currentHp <= (ctx.owner.currentStats.hp / 3)) {
            console.log('[Ability] Blaze activated! (1.5x Attack)');
            return value * 1.5;
        }
        return value;
    }
};
AbilityRegistry.register(Blaze);

const Torrent: Ability = {
    id: 'Torrent',
    name: 'Torrent',
    description: 'Powers up Water-type moves when the Pokémon\'s HP is low.',
    
    onModifyAttack: (value: number, ctx: AbilityContext) => {
        if (ctx.move?.type === 'Water' && ctx.owner.currentHp <= (ctx.owner.currentStats.hp / 3)) {
            console.log('[Ability] Torrent activated! (1.5x Attack)');
            return value * 1.5;
        }
        return value;
    }
};
AbilityRegistry.register(Torrent);
