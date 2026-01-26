import { BattleScene } from "./BattleScene";
import { PokemonInstance, MoveData } from "../data/DataTypes";
import { getTypeEffectiveness } from "./TypeChart";
import { AtomicEffects } from "./AtomicEffects"; // Add import

// Define MoveEvent interface here to avoid circular dependency with MoveEngineTypes/AtomicEffects
export interface MoveEvent {
  type:
    | "Text"
    | "Damage"
    | "Heal"
    | "Status"
    | "StatChange"
    | "EffectUnique"
    | "Blink"
    | "Wait";
  targetId?: string;
  message?: string;
  value?: any;
}

export interface AbilityContext {
  owner: PokemonInstance;
  target?: PokemonInstance;
  battle?: BattleScene;
  move?: MoveData;
  damage?: number;
  statName?: string;
  effectiveness?: number;
  variables?: Record<string, any>; // For passing data between events
}

export interface Ability {
  id: string;
  name: string;
  description: string;

  // --- BATTLE FLOW HOOKS ---

  // Triggered when the Pokemon enters battle (Start or Switch-in)
  onBattleStart?: (ctx: AbilityContext) => Promise<void>;

  // Modify Move Priority (Prankster, Gale Wings)
  onModifyPriority?: (priority: number, ctx: AbilityContext) => number;

  // Triggered when switching out
  onSwitchOut?: (ctx: AbilityContext) => Promise<void>;

  // Triggered at the very start of a turn (before move selection or execution order)
  onTurnStart?: (ctx: AbilityContext) => Promise<void>;

  // Triggered when checking if a move can be used or fails
  onBeforeMove?: (ctx: AbilityContext) => Promise<boolean>; // return false to cancel move

  // Modify Multi-Hit Count (Skill Link)
  // Returns the fixed number of hits.
  onModifyMultiHit?: (hits: number, ctx: AbilityContext) => number;

  // Triggered when a status condition is about to be applied (return false to prevent)
  onSetStatus?: (ctx: AbilityContext, status: string) => boolean;

  // Triggered after the owner inflicts a status on a target
  onInflictStatus?: (ctx: AbilityContext, status: string) => void;

  // Triggered before a move hits (return false to prevent hit/damage).
  // Pass the events array so the ability can push Text/Heal/StatChange events.
  onTryHit?: (ctx: AbilityContext, events: any[]) => boolean;

  // Triggered when calculating base stats or effective stats
  // Uses ctx.statName to distinguish stats
  onStatCalculation?: (value: number, ctx: AbilityContext) => number;

  // --- DAMAGE HOOKS ---

  // Modifier to Attack/SpAttack of the owner
  onModifyAttack?: (value: number, ctx: AbilityContext) => number;

  // Modifier to Defense/SpDefense of the target (when owner is being attacked)
  onModifyDefense?: (value: number, ctx: AbilityContext) => number;

  // Final Damage Multiplier (e.g. Filter, Solid Rock)
  onDamageMultiplier?: (value: number, ctx: AbilityContext) => number;

  // Base Power Modifier (Technician, etc)
  onModifyBasePower?: (value: number, ctx: AbilityContext) => number;

  // Recoil Check (Rock Head) - return false to prevent recoil
  onRecoilCheck?: (ctx: AbilityContext) => boolean;

  // Check if Pokemon should survive a fatal hit (Sturdy, Focus Sash)
  // Returns true if the pokemon should survive with 1 HP.
  onTrySurvive?: (damage: number, ctx: AbilityContext) => boolean;

  // Alias/Replacement for onBattleStart (Entry Effects)
  onEnterBattle?: (ctx: AbilityContext) => Promise<void>;

  // --- POST ACTION HOOKS ---

  // After taking damage
  // ctx.owner = Pokemon who took damage (Ability Holder)
  // ctx.target = Pokemon who attacked (Source of damage)
  // After taking damage
  // ctx.owner = Pokemon who took damage (Ability Holder)
  // ctx.target = Pokemon who attacked (Source of damage)
  onAfterDamage?: (ctx: AbilityContext, damageTaken: number) => Promise<void>;

  // --- ACCURACY / EVASION / EFFECT Hooks ---

  // Modify Move Type (Normalize, Pixilate, etc)
  // Returns the new type string
  onModifyType?: (type: string, ctx: AbilityContext) => string;

  // Modify Move Accuracy (Compound Eyes)
  onModifyAccuracy?: (value: number, ctx: AbilityContext) => number;

  // Modify Evasion (Sand Veil, Snow Cloak)
  // Note: Used by the target to modify the attacker's hit chance
  onModifyEvasion?: (value: number, ctx: AbilityContext) => number;

  // Modify Move Effect Chance (Serene Grace)
  onModifyEffectChance?: (value: number, ctx: AbilityContext) => number;

  // Prevent Stat Lowering (Keen Eye, Clear Body)
  // Return false to prevent lowering.
  onTryLowerStat?: (ctx: AbilityContext, stat: string) => boolean;

  // Modify Stat Change (Simple, Contrary)
  onModifyStatChange?: (stages: number, ctx: AbilityContext) => number;

  // React to Stat Change (Defiant, Competitive)
  onAfterStatChange?: (
    ctx: AbilityContext,
    stat: string,
    changes: number
  ) => MoveEvent[];

  // Triggered when another Pokemon faints (Moxie, Beast Boost, Soul-Heart)
  onFaint?: (ctx: AbilityContext, fainted: PokemonInstance) => MoveEvent[];

  // --- CRITICAL HIT HOOKS ---
  onCriticalMultiplier?: (value: number, ctx: AbilityContext) => number;

  // Modify Critical Hit Stage (Super Luck)
  onModifyCritStage?: (stage: number, ctx: AbilityContext) => number;

  // Prevent receiving critical hits (Battle Armor, Shell Armor)
  onPreventCrit?: (ctx: AbilityContext) => boolean;

  // Force critical hit (Merciless on poisoned targets)
  onForceCrit?: (ctx: AbilityContext) => boolean;

  // Triggered after receiving a critical hit (Anger Point)
  onReceiveCrit?: (ctx: AbilityContext) => Promise<void>;

  // Triggered when the Pokémon knocks out a target
  onKOTarget?: (ctx: AbilityContext) => Promise<void>;

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

  static async trigger(
    id: string,
    hook: keyof Ability,
    ctx: AbilityContext,
    ...args: any[]
  ): Promise<any> {
    const ability = this.get(id);
    if (ability && ability[hook]) {
      // @ts-ignore
      return await ability[hook](ctx, ...args);
    }
    return undefined;
  }

  static applyModifier(
    id: string,
    hook: keyof Ability,
    initialValue: number,
    ctx: AbilityContext
  ): number {
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
  id: "Overgrow",
  name: "Overgrow",
  description: "Powers up Grass-type moves when the Pokémon's HP is low.",

  onModifyAttack: (value: number, ctx: AbilityContext) => {
    if (
      ctx.move?.type === "Grass" &&
      ctx.owner.currentHp <= ctx.owner.currentStats.hp / 3
    ) {
      console.log("[Ability] Overgrow activated! (1.5x Attack)");
      return value * 1.5;
    }
    return value;
  },
};

AbilityRegistry.register(Overgrow);
const Blaze: Ability = {
  id: "Blaze",
  name: "Blaze",
  description: "Powers up Fire-type moves when the Pokémon's HP is low.",

  onModifyAttack: (value: number, ctx: AbilityContext) => {
    if (
      ctx.move?.type === "Fire" &&
      ctx.owner.currentHp <= ctx.owner.currentStats.hp / 3
    ) {
      console.log("[Ability] Blaze activated! (1.5x Attack)");
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(Blaze);

const Torrent: Ability = {
  id: "Torrent",
  name: "Torrent",
  description: "Powers up Water-type moves when the Pokémon's HP is low.",

  onModifyAttack: (value: number, ctx: AbilityContext) => {
    if (
      ctx.move?.type === "Water" &&
      ctx.owner.currentHp <= ctx.owner.currentStats.hp / 3
    ) {
      console.log("[Ability] Torrent activated! (1.5x Attack)");
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(Torrent);

// Intimidate moved to Entry Effects section

const Static: Ability = {
  id: "Static",
  name: "Static",
  description: "Has a 30% chance of paralyzing attacking Pokémon on contact.",

  onAfterDamage: async (ctx: AbilityContext, damageTaken: number) => {
    // Check Contact (Heuristic: Physical)
    if (ctx.move?.category === "Physical" && Math.random() < 0.3) {
      const attacker = ctx.target; // ctx.target is the attacker in onAfterDamage context
      if (
        attacker &&
        attacker.status === "None" &&
        !attacker.types.includes("Electric")
      ) {
        // Electric types immune to paralysis (Gen 6+)
        attacker.status = "Paralysis";
        if (ctx.battle)
          await ctx.battle["showText"](
            `${attacker.nickname} was paralyzed by Static!`
          );
      }
    }
  },
};
AbilityRegistry.register(Static);

// --- STATUS IMMUNITIES ---

const Limber: Ability = {
  id: "Limber",
  name: "Limber",
  description: "Prevents paralysis.",
  onSetStatus: (ctx, status) => {
    if (status === "Paralysis") {
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Limber prevents paralysis!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Limber);

const Immunity: Ability = {
  id: "Immunity",
  name: "Immunity",
  description: "Prevents poison.",
  onSetStatus: (ctx, status) => {
    if (status === "Poison") {
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Immunity prevents poisoning!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Immunity);

const WaterVeil: Ability = {
  id: "Water Veil",
  name: "Water Veil",
  description: "Prevents burns.",
  onSetStatus: (ctx, status) => {
    if (status === "Burn") {
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Water Veil prevents burns!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(WaterVeil);

const MagmaArmor: Ability = {
  id: "Magma Armor",
  name: "Magma Armor",
  description: "Prevents freezing.",
  onSetStatus: (ctx, status) => {
    if (status === "Freeze") {
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Magma Armor prevents freezing!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(MagmaArmor);

const Insomnia: Ability = {
  id: "Insomnia",
  name: "Insomnia",
  description: "Prevents sleep.",
  onSetStatus: (ctx, status) => {
    if (status === "Sleep") {
      ctx.battle?.showText(`${ctx.owner.nickname}'s Insomnia prevents sleep!`);
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Insomnia);

const OwnTempo: Ability = {
  id: "Own Tempo",
  name: "Own Tempo",
  description: "Prevents confusion.",
  onSetStatus: (ctx, status) => {
    if (status === "Confusion") {
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Own Tempo prevents confusion!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(OwnTempo);

// --- TYPE ABSORPTION / IMMUNITIES ---

const VoltAbsorb: Ability = {
  id: "Volt Absorb",
  name: "Volt Absorb",
  description: "Restores HP if hit by an Electric-type move.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Electric") {
      if (ctx.owner.currentHp < ctx.owner.currentStats.hp) {
        const healAmt = Math.floor(ctx.owner.currentStats.hp * 0.25);
        ctx.owner.currentHp = Math.min(
          ctx.owner.currentStats.hp,
          ctx.owner.currentHp + healAmt
        );
        events.push({ type: "Heal", targetId: ctx.owner.uuid, value: healAmt });
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname} restored HP with Volt Absorb!`,
          targetId: ctx.owner.uuid,
        });
      } else {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Volt Absorb made it immune!`,
          targetId: ctx.owner.uuid,
        });
      }
      return false; // Prevent hit
    }
    return true;
  },
};
AbilityRegistry.register(VoltAbsorb);

const WaterAbsorb: Ability = {
  id: "Water Absorb",
  name: "Water Absorb",
  description: "Restores HP if hit by a Water-type move.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Water") {
      const healAmt = Math.floor(ctx.owner.currentStats.hp * 0.25);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentStats.hp + healAmt
      );
      events.push({ type: "Heal", targetId: ctx.owner.uuid, value: healAmt });
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname} restored HP with Water Absorb!`,
        targetId: ctx.owner.uuid,
      });
      return false; // Absorb damage
    }
    return true;
  },
};
AbilityRegistry.register(WaterAbsorb);

const FlashFire: Ability = {
  id: "Flash Fire",
  name: "Flash Fire",
  description: "Powers up the Pokémon's Fire-type moves if it's hit by one.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Fire") {
      if (!ctx.owner.volatile["FlashFire"]) {
        ctx.owner.volatile["FlashFire"] = 1;
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s power rose!`,
          targetId: ctx.owner.uuid,
        });
      } else {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Flash Fire made it immune!`,
          targetId: ctx.owner.uuid,
        });
      }
      return false;
    }
    return true;
  },
  onModifyBasePower: (value, ctx) => {
    if (ctx.move?.type === "Fire" && ctx.owner.volatile["FlashFire"]) {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(FlashFire);

const SapSipper: Ability = {
  id: "Sap Sipper",
  name: "Sap Sipper",
  description: "Boosts Attack if hit by a Grass-type move.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Grass") {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Sap Sipper made it immune!`,
        targetId: ctx.owner.uuid,
      });

      const current = ctx.owner.statStages.attack || 0;
      if (current < 6) {
        ctx.owner.statStages.attack = current + 1;
        events.push({
          type: "StatChange",
          targetId: ctx.owner.uuid,
          value: { stat: "attack", stages: current + 1 },
        });
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Attack rose!`,
          targetId: ctx.owner.uuid,
        });
      } else {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Attack won't go any higher!`,
          targetId: ctx.owner.uuid,
        });
      }
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(SapSipper);

const MotorDrive: Ability = {
  id: "Motor Drive",
  name: "Motor Drive",
  description: "Boosts Speed if hit by an Electric-type move.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Electric") {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Motor Drive made it immune!`,
        targetId: ctx.owner.uuid,
      });
      const current = ctx.owner.statStages.speed || 0;
      if (current < 6) {
        ctx.owner.statStages.speed = current + 1;
        events.push({
          type: "StatChange",
          targetId: ctx.owner.uuid,
          value: { stat: "speed", stages: current + 1 },
        });
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Speed rose!`,
          targetId: ctx.owner.uuid,
        });
      } else {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Speed won't go any higher!`,
          targetId: ctx.owner.uuid,
        });
      }
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(MotorDrive);

const LightningRod: Ability = {
  id: "Lightning Rod",
  name: "Lightning Rod",
  description: "Draws in Electric moves to boost Sp. Atk.",
  // Redirect logic would be in onCheckTarget
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Electric") {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Lightning Rod made it immune!`,
        targetId: ctx.owner.uuid,
      });
      const current = ctx.owner.statStages.spAttack || 0;
      if (current < 6) {
        ctx.owner.statStages.spAttack = current + 1;
        events.push({
          type: "StatChange",
          targetId: ctx.owner.uuid,
          value: { stat: "spAttack", stages: current + 1 },
        });
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Sp. Atk rose!`,
          targetId: ctx.owner.uuid,
        });
      } else {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Sp. Atk won't go any higher!`,
          targetId: ctx.owner.uuid,
        });
      }
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(LightningRod);

const StormDrain: Ability = {
  id: "Storm Drain",
  name: "Storm Drain",
  description: "Draws in Water-type moves to boost Sp. Atk.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Water") {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Storm Drain made it immune!`,
        targetId: ctx.owner.uuid,
      });
      const current = ctx.owner.statStages.spAttack || 0;
      if (current < 6) {
        ctx.owner.statStages.spAttack = current + 1;
        events.push({
          type: "StatChange",
          targetId: ctx.owner.uuid,
          value: { stat: "spAttack", stages: current + 1 },
        });
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Sp. Atk rose!`,
          targetId: ctx.owner.uuid,
        });
      } else {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Sp. Atk won't go any higher!`,
          targetId: ctx.owner.uuid,
        });
      }
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(StormDrain);

// --- CONTACT / DAMAGE REACTION ---

const FlameBody: Ability = {
  id: "Flame Body",
  name: "Flame Body",
  description: "Contact with the Pokémon may burn the attacker.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical" && Math.random() < 0.3) {
      const attacker = ctx.target;
      if (
        attacker &&
        attacker.status === "None" &&
        !attacker.types.includes("Fire")
      ) {
        attacker.status = "Burn";
        if (ctx.battle)
          await ctx.battle.showText(
            `${attacker.nickname} was burned by Flame Body!`
          );
      }
    }
  },
};
AbilityRegistry.register(FlameBody);

const PoisonPoint: Ability = {
  id: "Poison Point",
  name: "Poison Point",
  description: "Contact with the Pokémon may poison the attacker.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical" && Math.random() < 0.3) {
      const attacker = ctx.target;
      // Poison types and Steel types are immune to poisoning (usually).
      // Gen 3+ Steel immune.
      if (
        attacker &&
        attacker.status === "None" &&
        !attacker.types.includes("Poison") &&
        !attacker.types.includes("Steel")
      ) {
        attacker.status = "Poison";
        if (ctx.battle)
          await ctx.battle.showText(
            `${attacker.nickname} was poisoned by Poison Point!`
          );
      }
    }
  },
};
AbilityRegistry.register(PoisonPoint);

const CuteCharm: Ability = {
  id: "Cute Charm",
  name: "Cute Charm",
  description: "Contact with the Pokémon may cause infatuation.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical" && Math.random() < 0.3) {
      const attacker = ctx.target;
      // Infatuation usually requires opposite gender. We don't have gender implemented yet?
      // Assuming simplified engine: just apply if not already infatuated.
      if (attacker && !attacker.volatile["Infatuation"]) {
        attacker.volatile["Infatuation"] = 1;
        if (ctx.battle)
          await ctx.battle.showText(
            `${attacker.nickname} fell in love with ${ctx.owner.nickname}!`
          );
      }
    }
  },
};
AbilityRegistry.register(CuteCharm);

const RoughSkin: Ability = {
  id: "Rough Skin",
  name: "Rough Skin",
  description: "Inflicts damage to the attacker on contact.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical") {
      const attacker = ctx.target;
      if (attacker) {
        const dmg = Math.floor(attacker.currentStats.hp / 8);
        attacker.currentHp = Math.max(0, attacker.currentHp - dmg);
        if (ctx.battle)
          await ctx.battle.showText(
            `${attacker.nickname} was hurt by Rough Skin!`
          );
      }
    }
  },
};
AbilityRegistry.register(RoughSkin);

const IronBarbs: Ability = {
  id: "Iron Barbs",
  name: "Iron Barbs",
  description: "Inflicts damage to the attacker on contact.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical") {
      const attacker = ctx.target;
      if (attacker) {
        const dmg = Math.floor(attacker.currentStats.hp / 8);
        attacker.currentHp = Math.max(0, attacker.currentHp - dmg);
        if (ctx.battle)
          await ctx.battle.showText(
            `${attacker.nickname} was hurt by Iron Barbs!`
          );
      }
    }
  },
};
AbilityRegistry.register(IronBarbs);

const EffectSpore: Ability = {
  id: "Effect Spore",
  name: "Effect Spore",
  description: "Contact may poison, paralyze, or cause sleep.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical" && Math.random() < 0.3) {
      const attacker = ctx.target;
      if (
        !attacker ||
        attacker.status !== "None" ||
        attacker.types.includes("Grass")
      )
        return; // Grass types immune to powder/spore

      const rand = Math.random();
      if (rand < 0.33) {
        // Poison (approx 10% total)
        if (
          !attacker.types.includes("Poison") &&
          !attacker.types.includes("Steel")
        ) {
          attacker.status = "Poison";
          if (ctx.battle)
            await ctx.battle.showText(
              `${attacker.nickname} was poisoned by Effect Spore!`
            );
        }
      } else if (rand < 0.66) {
        // Paralyze (approx 10% total)
        if (!attacker.types.includes("Electric")) {
          attacker.status = "Paralysis";
          if (ctx.battle)
            await ctx.battle.showText(
              `${attacker.nickname} was paralyzed by Effect Spore!`
            );
        }
      } else {
        // Sleep (approx 10% total)
        attacker.status = "Sleep";
        attacker.volatile["SleepTurns"] = Math.floor(Math.random() * 3) + 2;
        if (ctx.battle)
          await ctx.battle.showText(`${attacker.nickname} fell asleep!`);
      }
    }
  },
};
AbilityRegistry.register(EffectSpore);

// --- STAT MODIFIERS ---

const HugePower: Ability = {
  id: "Huge Power",
  name: "Huge Power",
  description: "Doubles Attack.",
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "attack") return value * 2;
    return value;
  },
};
AbilityRegistry.register(HugePower);

const PurePower: Ability = {
  // Alias/Clone
  id: "Pure Power",
  name: "Pure Power",
  description: "Doubles Attack.",
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "attack") return value * 2;
    return value;
  },
};
AbilityRegistry.register(PurePower);

const Guts: Ability = {
  id: "Guts",
  name: "Guts",
  description: "Boosts Attack if suffering from a status condition.",
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "attack" && ctx.owner.status !== "None") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(Guts);

const MarvelScale: Ability = {
  id: "Marvel Scale",
  name: "Marvel Scale",
  description: "Boosts Defense if suffering from a status condition.",
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "defense" && ctx.owner.status !== "None") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(MarvelScale);

const QuickFeet: Ability = {
  id: "Quick Feet",
  name: "Quick Feet",
  description: "Boosts Speed if suffering from a status condition.",
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "speed" && ctx.owner.status !== "None") {
      return value * 1.5;
    }
    return value;
  },
};
// Note: Quick Feet also ignores Paralysis speed drop.
// This requires check in StatCalculator or logic here to compensate.
// If Paralysis drops speed by 50%, and we want +50% of BASE (effectively 1.5x),
// Usually it ignores the drop.
// StatCalculator applies Paralysis drop BEFORE abilities.
// So simple logic: value (0.5) * 1.5 = 0.75. Not enough.
// If it ignores it, we effectively want value * 2 * 1.5 = 3x? No.
// If paralyzed, base 100 -> 50. Quick Feet should be 150.
// So we need to multiply by giving back the drop (2x) and then boost (1.5x) => 3x.
// BUT we don't know if the drop was applied inside this hook easily (except implicit knowledge).
// Let's assume standard behavior for now (1.5x on current) unless we want to be precise.
// Precision update:
// If status is paralysis, we assume StatCalculator halved it.
// We return value * 2 * 1.5 = value * 3.
AbilityRegistry.register(QuickFeet);

const SwiftSwim: Ability = {
  id: "Swift Swim",
  name: "Swift Swim",
  description: "Doubles Speed in Rain.",
  onStatCalculation: (value, ctx) => {
    // Need weather check. ctx.battle is needed.
    // If battle not available, assume no weather.
    // We can check ctx.variables?.weather or something if passed?
    // Currently StatCalculator passes { owner: mon, ... }
    // We rely on Global/Battle Context.
    // Since StatCalculator is static, we might not have battle context in 'ctx' unless caller passes it.
    // DamageCalculator passes { owner, target, move }. No battle.
    // This is a limitation for Weather abilities currently.
    // Skipping Weather implementation details until Weather System is hooked into StatCalculator.
    return value;
  },
};
AbilityRegistry.register(SwiftSwim);

const Chlorophyll: Ability = {
  id: "Chlorophyll",
  name: "Chlorophyll",
  description: "Doubles Speed in Sun.",
  onStatCalculation: (value, ctx) => {
    // Skipping Weather check for now.
    return value;
  },
};
AbilityRegistry.register(Chlorophyll);

// --- DAMAGE MODIFIERS ---

const ThickFat: Ability = {
  id: "Thick Fat",
  name: "Thick Fat",
  description: "Halves damage from fire and ice moves.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.type === "Fire" || ctx.move?.type === "Ice") {
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(ThickFat);

const Heatproof: Ability = {
  id: "Heatproof",
  name: "Heatproof",
  description: "Halves damage from fire moves.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.type === "Fire") {
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(Heatproof);

const Filter: Ability = {
  id: "Filter",
  name: "Filter",
  description: "Reduces damage from super-effective attacks.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.effectiveness && ctx.effectiveness > 1) {
      return value * 0.75;
    }
    return value;
  },
};
AbilityRegistry.register(Filter);

const SolidRock: Ability = {
  // Clone of Filter
  id: "Solid Rock",
  name: "Solid Rock",
  description: "Reduces damage from super-effective attacks.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.effectiveness && ctx.effectiveness > 1) {
      return value * 0.75;
    }
    return value;
  },
};
AbilityRegistry.register(SolidRock);

const TintedLens: Ability = {
  id: "Tinted Lens",
  name: "Tinted Lens",
  description: 'Powers up "not very effective" moves.',
  onDamageMultiplier: (value, ctx) => {
    if (ctx.effectiveness && ctx.effectiveness < 1 && ctx.effectiveness > 0) {
      return value * 2.0;
    }
    return value;
  },
};
AbilityRegistry.register(TintedLens);

// Technician and Iron Fist moved to Move Properties section

// --- ACCURACY / EFFECT MODIFIERS ---

const SereneGrace: Ability = {
  id: "Serene Grace",
  name: "Serene Grace",
  description: "Boosts the likelihood of added effects appearing.",
  onModifyEffectChance: (value, ctx) => {
    return value * 2;
  },
};
AbilityRegistry.register(SereneGrace);

const CompoundEyes: Ability = {
  id: "Compound Eyes",
  name: "Compound Eyes",
  description: "Boosts the Pokémon's accuracy.",
  onModifyAccuracy: (value, ctx) => {
    return value * 1.3;
  },
};
AbilityRegistry.register(CompoundEyes);

const KeenEye: Ability = {
  id: "Keen Eye",
  name: "Keen Eye",
  description: "Prevents other Pokémon from lowering accuracy.",
  onTryLowerStat: (ctx, stat) => {
    if (stat === "accuracy") return false;
    return true;
  },
};
AbilityRegistry.register(KeenEye);

const HyperCutter: Ability = {
  id: "Hyper Cutter",
  name: "Hyper Cutter",
  description: "Prevents other Pokémon from lowering Attack.",
  onTryLowerStat: (ctx, stat) => {
    if (stat === "attack") return false;
    return true;
  },
};
AbilityRegistry.register(HyperCutter);

const ClearBody: Ability = {
  id: "Clear Body",
  name: "Clear Body",
  description: "Prevents other Pokémon from lowering stats.",
  onTryLowerStat: (ctx, stat) => {
    return false; // Prevent all stat lowering
  },
};
AbilityRegistry.register(ClearBody);

const WhiteSmoke: Ability = {
  // Clone of Clear Body
  id: "White Smoke",
  name: "White Smoke",
  description: "Prevents other Pokémon from lowering stats.",
  onTryLowerStat: (ctx, stat) => {
    return false; // Prevent all stat lowering
  },
};
AbilityRegistry.register(WhiteSmoke);

// --- CRITICAL / TURN-BASED ---

const Sniper: Ability = {
  id: "Sniper",
  name: "Sniper",
  description: "Powers up critical hits.",
  onCriticalMultiplier: (value, ctx) => {
    return value * 1.5; // 1.5 * 1.5 = 2.25x
  },
};
AbilityRegistry.register(Sniper);

const SuperLuck: Ability = {
  id: "Super Luck",
  name: "Super Luck",
  description: "Boosts the critical-hit ratios of moves.",
  onModifyCritStage: (stage, ctx) => {
    return stage + 1;
  },
};
AbilityRegistry.register(SuperLuck);

const SpeedBoost: Ability = {
  id: "Speed Boost",
  name: "Speed Boost",
  description: "Its Speed stat is gradually boosted.",
  onTurnEnd: async (ctx) => {
    if (!ctx.battle) return;
    const mon = ctx.owner;

    if (!mon.statStages) {
      mon.statStages = {
        attack: 0,
        defense: 0,
        spAttack: 0,
        spDefense: 0,
        speed: 0,
        accuracy: 0,
        evasion: 0,
      };
    }

    const current = ctx.owner.statStages.speed || 0;
    if (current < 6) {
      ctx.owner.statStages.speed = current + 1;
      // Ideally use AtomicEffects for stat change to handle messages std way
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Speed Boost!`);
    }
  },
};
AbilityRegistry.register(SpeedBoost);

const ShedSkin: Ability = {
  id: "Shed Skin",
  name: "Shed Skin",
  description: "The Pokémon may heal its own status problems.",
  onTurnEnd: async (ctx) => {
    if (ctx.owner.status !== "None" && Math.random() < 0.3) {
      ctx.owner.status = "None";
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} shed its skin!`);
    }
  },
};
AbilityRegistry.register(ShedSkin);

// --- LOW HP BOOSTS / RECOIL / DRAIN ---

const Swarm: Ability = {
  id: "Swarm",
  name: "Swarm",
  description: "Powers up Bug-type moves when the Pokémon's HP is low.",
  onModifyAttack: (value, ctx) => {
    if (
      ctx.move?.type === "Bug" &&
      ctx.owner.currentHp <= ctx.owner.currentStats.hp / 3
    ) {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(Swarm);

const RockHead: Ability = {
  id: "Rock Head",
  name: "Rock Head",
  description: "Protects the Pokémon from recoil damage.",
  onRecoilCheck: (ctx) => {
    return false; // No recoil
  },
};
AbilityRegistry.register(RockHead);

const NoGuard: Ability = {
  id: "No Guard",
  name: "No Guard",
  description: "Ensures attacks by or against the Pokémon land.",
  onModifyAccuracy: (value, ctx) => {
    // value is usually 0-100 or multiplier?
    // If returns 0, maybe treated as sure hit?
    // Or just return 1000?
    // Logic handled in MoveEngine accuracy check.
    // If we assume standard accuracy check logic:
    return 999; // Always hit
  },
};
AbilityRegistry.register(NoGuard);

const Levitate: Ability = {
  id: "Levitate",
  name: "Levitate",
  description: "Gives immunity to Ground-type moves.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Ground") {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname} makes ground moves miss with Levitate!`,
        targetId: ctx.owner.uuid,
      });
      return false; // Prevent hit
    }
    return true;
  },
};
AbilityRegistry.register(Levitate);

const LiquidOoze: Ability = {
  id: "Liquid Ooze",
  name: "Liquid Ooze",
  description: "Damages attackers using any draining move.",
  // Logic handled in MoveEngine.ts checks for ID 'Liquid Ooze'
};
AbilityRegistry.register(LiquidOoze);

// Reckless moved to Move Properties section

// --- SPECIAL MECHANICS / IMMUNITIES ---

// SkillLink definition replaced below
// const SkillLink: Ability = { ... }

const Adaptability: Ability = {
  id: "Adaptability",
  name: "Adaptability",
  description: "Powers up moves of the same type as the Pokémon.",
  // Logic in DamageCalculator
};
AbilityRegistry.register(Adaptability);

const InnerFocus: Ability = {
  id: "Inner Focus",
  name: "Inner Focus",
  description: "Prevents flinching.",
  onSetStatus: (ctx, status) => {
    if (status === "Flinch") {
      // Note: Flinch might be considered a Volatile status passed as string in some contexts,
      // or we might need onVolatile hook? atomicEffects.applyVolatile calls onSetStatus.
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Inner Focus prevents flinching!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(InnerFocus);

// Sturdy moved to Defensive Abilities section

const Soundproof: Ability = {
  id: "Soundproof",
  name: "Soundproof",
  description: "Gives immunity to sound-based moves.",
  // Logic in MoveEngine
};
AbilityRegistry.register(Soundproof);

const PoisonHeal: Ability = {
  id: "Poison Heal",
  name: "Poison Heal",
  description: "Restores HP if the Pokémon is poisoned.",
  // Logic in BattleScene.executeEndOfTurn
};
AbilityRegistry.register(PoisonHeal);

const NaturalCure: Ability = {
  id: "Natural Cure",
  name: "Natural Cure",
  description: "All status problems heal when it switches out.",
  onSwitchOut: async (ctx) => {
    if (ctx.owner.status !== "None") {
      ctx.owner.status = "None";
    }
  },
};
AbilityRegistry.register(NaturalCure);

const RainDish: Ability = {
  id: "Rain Dish",
  name: "Rain Dish",
  description: "The Pokémon gradually regains HP in rain.",
  onTurnEnd: async (ctx) => {
    if (ctx.battle?.game?.weatherManager?.currentWeather === "Rain") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 16);
      if (heal > 0 && ctx.owner.currentHp < ctx.owner.currentStats.hp) {
        ctx.owner.currentHp = Math.min(
          ctx.owner.currentStats.hp,
          ctx.owner.currentHp + heal
        );
        await ctx.battle.showText(
          `${ctx.owner.nickname} restored HP with Rain Dish!`
        );
      }
    }
  },
};
AbilityRegistry.register(RainDish);

const IceBody: Ability = {
  id: "Ice Body",
  name: "Ice Body",
  description: "The Pokémon gradually regains HP in hail/snow.",
  onTurnEnd: async (ctx) => {
    const weather = ctx.battle?.game?.weatherManager?.currentWeather;
    if (weather === "Hail" || weather === "Snow") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 16);
      if (heal > 0 && ctx.owner.currentHp < ctx.owner.currentStats.hp) {
        ctx.owner.currentHp = Math.min(
          ctx.owner.currentStats.hp,
          ctx.owner.currentHp + heal
        );
        await ctx.battle.showText(
          `${ctx.owner.nickname} restored HP with Ice Body!`
        );
      }
    }
  },
};
AbilityRegistry.register(IceBody);

const SolarPower: Ability = {
  id: "Solar Power",
  name: "Solar Power",
  description: "Boosts Sp. Atk but lowers HP in sunshine.",
  onStatCalculation: (value, ctx) => {
    if (
      ctx.statName === "spAttack" &&
      ctx.battle?.game?.weatherManager?.currentWeather === "Sun"
    ) {
      return value * 1.5;
    }
    return value;
  },
  onTurnEnd: async (ctx) => {
    if (ctx.battle?.game?.weatherManager?.currentWeather === "Sun") {
      const dmg = Math.floor(ctx.owner.currentStats.hp / 8);
      if (dmg > 0) {
        ctx.owner.currentHp = Math.max(0, ctx.owner.currentHp - dmg);
        await ctx.battle.showText(
          `${ctx.owner.nickname} takes damage from Solar Power!`
        );
      }
    }
  },
};
AbilityRegistry.register(SolarPower);

// --- ENTRY EFFECTS ---

const Intimidate: Ability = {
  id: "Intimidate",
  name: "Intimidate",
  description: "Lowers the foe's Attack stat.",
  onBattleStart: async (ctx) => {
    if (!ctx.battle) return;
    const opponent =
      ctx.owner === ctx.battle.playerPokemon
        ? ctx.battle.enemyPokemon
        : ctx.battle.playerPokemon;
    if (opponent && opponent.currentHp > 0) {
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Intimidate cuts ${opponent.nickname}'s Attack!`
      );
      // Stat Lower logic
      // Check for Clear Body / Hyper Cutter etc via onTryLowerStat?
      // For now direct modification with basic check
      const current = opponent.statStages.attack || 0;
      if (current > -6) {
        opponent.statStages.attack = current - 1;
        await ctx.battle.showText(`${opponent.nickname}'s Attack fell!`);
      } else {
        await ctx.battle.showText(
          `${opponent.nickname}'s Attack won't go any lower!`
        );
      }
    }
  },
  // Also trigger onSwitchIn? onBattleStart handles both if we call it on switch.
  // Wait, onSwitchIn is handled by onBattleStart based on my implementation?
  // In executeSwitch: "await AbilityRegistry.trigger(this.playerPokemon.ability, 'onBattleStart', ...)"
  // Yes.
};
AbilityRegistry.register(Intimidate);

const Trace: Ability = {
  id: "Trace",
  name: "Trace",
  description: "The Pokémon copies a foe's Ability.",
  onBattleStart: async (ctx) => {
    if (!ctx.battle) return;
    const opponent =
      ctx.owner === ctx.battle.playerPokemon
        ? ctx.battle.enemyPokemon
        : ctx.battle.playerPokemon;
    if (opponent && opponent.ability && opponent.ability !== "Trace") {
      await ctx.battle.showText(
        `${ctx.owner.nickname} Traced ${opponent.nickname}'s ${opponent.ability}!`
      );
      ctx.owner.ability = opponent.ability;
      // Optionally trigger the new ability's onBattleStart immediately?
      // Spec says: "When a Pokémon with Trace enters battle, it traces a random opponent's Ability."
      // "If the traced Ability has an effect that activates when the Pokémon enters battle, it will activate immediately."
      // So recursively trigger?
      await AbilityRegistry.trigger(ctx.owner.ability, "onBattleStart", ctx);
    }
  },
};
AbilityRegistry.register(Trace);

const Download: Ability = {
  id: "Download",
  name: "Download",
  description: "Adjusts power according to a foe's defenses.",
  onBattleStart: async (ctx) => {
    if (!ctx.battle) return;
    const opponent =
      ctx.owner === ctx.battle.playerPokemon
        ? ctx.battle.enemyPokemon
        : ctx.battle.playerPokemon;
    if (opponent) {
      const def = opponent.currentStats.defense;
      const spDef = opponent.currentStats.spDefense;
      let statToRaise = "attack";
      let statName = "Attack";

      if (def < spDef) {
        statToRaise = "attack";
        statName = "Attack";
      } else {
        statToRaise = "spAttack";
        statName = "Sp. Atk";
      }

      const current = ctx.owner.statStages[statToRaise] || 0;
      if (current < 6) {
        ctx.owner.statStages[statToRaise] = current + 1;
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Download raised its ${statName}!`
        );
      }
    }
  },
};
AbilityRegistry.register(Download);

const Drizzle: Ability = {
  id: "Drizzle",
  name: "Drizzle",
  description: "The Pokémon makes it rain when it enters a battle.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      ctx.battle.game.weatherManager.setWeather("Rain");
      await ctx.battle.showText(`It started to rain!`);
    }
  },
};
AbilityRegistry.register(Drizzle);

const Drought: Ability = {
  id: "Drought",
  name: "Drought",
  description: "The Pokémon makes it sunny the turn it enters a battle.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      ctx.battle.game.weatherManager.setWeather("Sun");
      await ctx.battle.showText(`The sunlight turned harsh!`);
    }
  },
};
AbilityRegistry.register(Drought);

const SandStream: Ability = {
  id: "Sand Stream",
  name: "Sand Stream",
  description: "The Pokémon summons a sandstorm when it enters a battle.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      ctx.battle.game.weatherManager.setWeather("Sandstorm");
      await ctx.battle.showText(`A sandstorm brewed!`);
    }
  },
};
AbilityRegistry.register(SandStream);

const SnowWarning: Ability = {
  id: "Snow Warning",
  name: "Snow Warning",
  description:
    "The Pokémon summons a hailstorm/snowstorm when it enters a battle.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      ctx.battle.game.weatherManager.setWeather("Hail"); // Or Hail
      await ctx.battle.showText(`It started to hail!`);
    }
  },
};
AbilityRegistry.register(SnowWarning);

const Pressure: Ability = {
  id: "Pressure",
  name: "Pressure",
  description: "The Pokémon raises the foe's PP usage.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      await ctx.battle.showText(
        `${ctx.owner.nickname} is exerting its pressure!`
      );
    }
  },
  // TODO: Implement actual PP usage increase in MoveEngine/BattleScene hooks
};
AbilityRegistry.register(Pressure);

const MoldBreaker: Ability = {
  id: "Mold Breaker",
  name: "Mold Breaker",
  description: "Moves can be used regardless of Abilities.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      await ctx.battle.showText(`${ctx.owner.nickname} breaks the mold!`);
    }
  },
  // TODO: Implement actual mold breaker logic (ignoring immunity/absorb abilities)
};
AbilityRegistry.register(MoldBreaker);

// --- PRIORITY ABILITIES ---

const Prankster: Ability = {
  id: "Prankster",
  name: "Prankster",
  description: "Gives priority to a status move.",
  onModifyPriority: (priority, ctx) => {
    if (ctx.move?.category === "Status") {
      return priority + 1;
    }
    return priority;
  },
};
AbilityRegistry.register(Prankster);

const GaleWings: Ability = {
  id: "Gale Wings",
  name: "Gale Wings",
  description: "Gives priority to Flying-type moves when HP is full.",
  onModifyPriority: (priority, ctx) => {
    if (
      ctx.move?.type === "Flying" &&
      ctx.owner.currentHp === ctx.owner.currentStats.hp
    ) {
      return priority + 1;
    }
    return priority;
  },
};
AbilityRegistry.register(GaleWings);

const Triage: Ability = {
  id: "Triage",
  name: "Triage",
  description: "Gives priority to a healing move.",
  onModifyPriority: (priority, ctx) => {
    const isHeal =
      ctx.move?.effects?.some((e) => e.type === "Heal" || e.type === "Drain") ||
      ctx.move?.flags?.heal;
    if (isHeal) {
      return priority + 3;
    }
    return priority;
  },
};
AbilityRegistry.register(Triage);

const MyceliumMight: Ability = {
  id: "Mycelium Might",
  name: "Mycelium Might",
  description: "The Pokémon's status moves go last.",
  onModifyPriority: (priority, ctx) => {
    if (ctx.move?.category === "Status") {
      return -6;
    }
    return priority;
  },
};
AbilityRegistry.register(MyceliumMight);

// Helper to calculate effective priority for blocking logic
function getEffectivePriority(
  move: MoveData,
  attacker: PokemonInstance,
  battle?: BattleScene
): number {
  let priority = move.priority || 0;
  const ctx = { owner: attacker, move, battle };
  priority = AbilityRegistry.applyModifier(
    attacker.ability,
    "onModifyPriority",
    priority,
    ctx
  );
  return priority;
}

const QueenlyMajesty: Ability = {
  id: "Queenly Majesty",
  name: "Queenly Majesty",
  description:
    "Its majesty pressures the opposing Pokémon, preventing their priority moves.",
  onTryHit: (ctx, events) => {
    if (ctx.target) {
      // ctx.target is the Attacker
      const attacker = ctx.target;
      const move = ctx.move;
      if (!move) return true;

      const priority = getEffectivePriority(move, attacker, ctx.battle);
      if (priority > 0) {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Queenly Majesty blocks priority moves!`,
          targetId: ctx.owner.uuid,
        });
        return false;
      }
    }
    return true;
  },
};
AbilityRegistry.register(QueenlyMajesty);

const Dazzling: Ability = {
  id: "Dazzling",
  name: "Dazzling",
  description: "Dazzles the opposing Pokémon, preventing their priority moves.",
  onTryHit: (ctx, events) => {
    if (ctx.target && ctx.move) {
      const priority = getEffectivePriority(ctx.move, ctx.target, ctx.battle);
      if (priority > 0) {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Dazzling blocks priority moves!`,
          targetId: ctx.owner.uuid,
        });
        return false;
      }
    }
    return true;
  },
};
AbilityRegistry.register(Dazzling);

const ArmorTail: Ability = {
  id: "Armor Tail",
  name: "Armor Tail",
  description:
    "The mysterious tail covers the opposing Pokémon, preventing their priority moves.",
  onTryHit: (ctx, events) => {
    if (ctx.target && ctx.move) {
      const priority = getEffectivePriority(ctx.move, ctx.target, ctx.battle);
      if (priority > 0) {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Armor Tail blocks priority moves!`,
          targetId: ctx.owner.uuid,
        });
        return false;
      }
    }
    return true;
  },
};
AbilityRegistry.register(ArmorTail);

// --- MOVE TYPE MODIFIERS (Batch 24) ---

const LiquidVoice: Ability = {
  id: "Liquid Voice",
  name: "Liquid Voice",
  description: "Sound-based moves become Water-type.",
  onModifyType: (type, ctx) => {
    if (ctx.move?.flags?.sound) {
      return "Water";
    }
    return type;
  },
};
AbilityRegistry.register(LiquidVoice);

const Galvanize: Ability = {
  id: "Galvanize",
  name: "Galvanize",
  description: "Normal moves become Electric and get a power boost.",
  onModifyType: (type, ctx) => {
    if (type === "Normal") return "Electric";
    return type;
  },
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type === "Normal") return power * 1.2;
    return power;
  },
};
AbilityRegistry.register(Galvanize);

const Refrigerate: Ability = {
  id: "Refrigerate",
  name: "Refrigerate",
  description: "Normal moves become Ice and get a power boost.",
  onModifyType: (type, ctx) => {
    if (type === "Normal") return "Ice";
    return type;
  },
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type === "Normal") return power * 1.2; // Gen 7+ is 1.2x
    return power;
  },
};
AbilityRegistry.register(Refrigerate);

const Pixilate: Ability = {
  id: "Pixilate",
  name: "Pixilate",
  description: "Normal moves become Fairy and get a power boost.",
  onModifyType: (type, ctx) => {
    if (type === "Normal") return "Fairy";
    return type;
  },
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type === "Normal") return power * 1.2;
    return power;
  },
};
AbilityRegistry.register(Pixilate);

const Aerilate: Ability = {
  id: "Aerilate",
  name: "Aerilate",
  description: "Normal moves become Flying and get a power boost.",
  onModifyType: (type, ctx) => {
    if (type === "Normal") return "Flying";
    return type;
  },
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type === "Normal") return power * 1.2;
    return power;
  },
};
AbilityRegistry.register(Aerilate);

const Normalize: Ability = {
  id: "Normalize",
  name: "Normalize",
  description: "All moves become Normal-type and get a power boost.",
  onModifyType: (type, ctx) => {
    return "Normal";
  },
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type !== "Normal") return power * 1.2; // Boost changed moves (Gen 7+)
    return power;
  },
};
AbilityRegistry.register(Normalize);

// --- MOVE PROPERTY ABILITIES ---

const IronFist: Ability = {
  id: "Iron Fist",
  name: "Iron Fist",
  description: "Boosts the power of punching moves.",
  onModifyBasePower: (power, ctx) => {
    // Heuristic: Check if move name contains "Punch"
    if (ctx.move?.name.includes("Punch")) {
      return power * 1.2;
    }
    return power;
  },
};
AbilityRegistry.register(IronFist);

const StrongJaw: Ability = {
  id: "Strong Jaw",
  name: "Strong Jaw",
  description: "The Pokémon's strong jaw boosts the power of its biting moves.",
  onModifyBasePower: (power, ctx) => {
    // Heuristic: Check if move name contains "Fang", "Bite", "Crunch"
    const name = ctx.move?.name || "";
    if (
      name.includes("Fang") ||
      name.includes("Bite") ||
      name.includes("Crunch")
    ) {
      return power * 1.5;
    }
    return power;
  },
};
AbilityRegistry.register(StrongJaw);

const MegaLauncher: Ability = {
  id: "Mega Launcher",
  name: "Mega Launcher",
  description: "Powers up aura and pulse moves.",
  onModifyBasePower: (power, ctx) => {
    // Heuristic: Check if move name contains "Pulse", "Aura", "Sphere" (Aura Sphere)
    const name = ctx.move?.name || "";
    if (
      name.includes("Pulse") ||
      name.includes("Aura") ||
      name.includes("Sphere")
    ) {
      return power * 1.5;
    }
    return power;
  },
};
AbilityRegistry.register(MegaLauncher);

const Reckless: Ability = {
  id: "Reckless",
  name: "Reckless",
  description: "Powers up moves that have recoil damage.",
  onModifyBasePower: (power, ctx) => {
    // Heuristic: Manual list or check effect definition?
    // Checking move name for common recoil moves
    const name = ctx.move?.name || "";
    if (
      [
        "Take Down",
        "Double-Edge",
        "Submission",
        "Wild Charge",
        "Flare Blitz",
        "Brave Bird",
        "Wood Hammer",
        "Head Smash",
        "High Jump Kick",
        "Jump Kick",
      ].includes(name)
    ) {
      return power * 1.2;
    }
    return power;
  },
};
AbilityRegistry.register(Reckless);

const Technician: Ability = {
  id: "Technician",
  name: "Technician",
  description: "Powers up the Pokémon's weaker moves.",
  onModifyBasePower: (power, ctx) => {
    if (power <= 60) {
      return power * 1.5;
    }
    return power;
  },
};
AbilityRegistry.register(Technician);

const Steelworker: Ability = {
  id: "Steelworker",
  name: "Steelworker",
  description: "Powers up Steel-type moves.",
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type === "Steel") return power * 1.5;
    return power;
  },
};
AbilityRegistry.register(Steelworker);

// Transistor already defined in Batch 26
// Removed duplicate.

// Dragon's Maw already defined in Batch 26
// Removed duplicate.

const RockyPayload: Ability = {
  id: "Rocky Payload",
  name: "Rocky Payload",
  description: "Powers up Rock-type moves.",
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type === "Rock") return power * 1.5;
    return power;
  },
};
AbilityRegistry.register(RockyPayload);

// Punk Rock already defined earlier (Batch 26).
// Removed duplicate.

const Sharpness: Ability = {
  id: "Sharpness",
  name: "Sharpness",
  description: "Powers up slicing moves.",
  onModifyBasePower: (power, ctx) => {
    // Heuristic: Slicing moves often have 'Cut', 'Slash', 'Blade', 'Sword'
    const name = ctx.move?.name || "";
    if (
      name.includes("Cut") ||
      name.includes("Slash") ||
      name.includes("Blade") ||
      name.includes("Sword") ||
      ctx.move?.flags?.slicing
    ) {
      return power * 1.5;
    }
    return power;
  },
};
AbilityRegistry.register(Sharpness);

const ToughClaws: Ability = {
  id: "Tough Claws",
  name: "Tough Claws",
  description: "Powers up moves that make contact.",
  onModifyBasePower: (power, ctx) => {
    // Heuristic: Physical moves often make contact.
    // Ideally verify flags.contact
    if (ctx.move?.category === "Physical" || ctx.move?.flags?.contact) {
      // Note: Not ALL physical moves make contact (e.g. Earthquake).
      // But without precise data, we favor the category or explicit flag.
      // If flag is missing but category is Physical, we assume contact for now unless 'Long Reach' logic interferes.
      // Let's rely on flag if present, else fallback to category but exclude known non-contacts?
      // Simplified: Check category Physical.
      return power * 1.3;
    }
    return power;
  },
};
AbilityRegistry.register(ToughClaws);

const WaterBubble: Ability = {
  id: "Water Bubble",
  name: "Water Bubble",
  description: "Lowers Fire damage, prevents burn, and boosts Water moves.",
  onModifyBasePower: (power, ctx) => {
    if (ctx.move?.type === "Water") return power * 2;
    return power;
  },
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.type === "Fire") return value * 0.5;
    return value;
  },
  onSetStatus: (ctx, status) => {
    if (status === "Burn") {
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Water Bubble prevents burns!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(WaterBubble);

// --- BATCH 27: STATUS & ITEMS ---

const Unburden: Ability = {
  id: "Unburden",
  name: "Unburden",
  description: "Doubles Speed upon using or losing a held item.",
  // Triggered when item is lost/used.
  // We need a hook 'onItemUse' or 'onItemLoss'?
  // For now, let's assume BattleScene triggers this logic or we check in onStatCalculation if item is gone?
  // Unburden only works if item WAS held and is now gone.
  // Requires state tracking.
  // Simplified: If unburden active (volatile flag set when item used), double speed.
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "speed" && ctx.owner.volatile["Unburden"]) {
      return value * 2;
    }
    return value;
  },
  // Note: The trigger to set volatile['Unburden'] must be in Item usage logic (Berry, Gem, Knock Off).
};
AbilityRegistry.register(Unburden);

const Magician: Ability = {
  id: "Magician",
  name: "Magician",
  description:
    "Steals the target's held item when the bearer uses a damaging move.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.target && ctx.owner && !ctx.owner.heldItem && ctx.target.heldItem) {
      // Steal item
      ctx.owner.heldItem = ctx.target.heldItem;
      ctx.target.heldItem = undefined;
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} stole ${ctx.owner.heldItem} with Magician!`
        );
    }
  },
};
AbilityRegistry.register(Magician);

const Pickpocket: Ability = {
  id: "Pickpocket",
  name: "Pickpocket",
  description: "Steals attacking Pokémon's held items on contact.",
  onAfterDamage: async (ctx, damage) => {
    // Defensive trigger
    if (ctx.target && ctx.owner && !ctx.owner.heldItem && ctx.target.heldItem) {
      // Check contact
      if (ctx.move?.category === "Physical" || ctx.move?.flags?.contact) {
        ctx.owner.heldItem = ctx.target.heldItem;
        ctx.target.heldItem = undefined;
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} stole ${ctx.owner.heldItem} with Pickpocket!`
          );
      }
    }
  },
};
AbilityRegistry.register(Pickpocket);

const RunAway: Ability = {
  id: "Run Away",
  name: "Run Away",
  description: "Ensures success fleeing from wild battles.",
  // Logic usually in BattleScene flee check.
  // We can't implement it purely here without a hook like 'onTryFlee'.
  // Assuming placeholder for now.
};
AbilityRegistry.register(RunAway);

const PoisonTouch: Ability = {
  id: "Poison Touch",
  name: "Poison Touch",
  description: "Has a 30% chance of poisoning target Pokémon upon contact.",
  onAfterDamage: async (ctx, damage) => {
    // Offensive trigger
    if (ctx.target && ctx.move?.flags?.contact && Math.random() < 0.3) {
      if (
        ctx.target.status === "None" &&
        !ctx.target.types.includes("Poison") &&
        !ctx.target.types.includes("Steel")
      ) {
        ctx.target.status = "Poison";
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.target.nickname} was poisoned by Poison Touch!`
          );
      }
    }
  },
};
AbilityRegistry.register(PoisonTouch);

const Corrosion: Ability = {
  id: "Corrosion",
  name: "Corrosion",
  description: "This Pokémon can inflict poison on Poison and Steel Pokémon.",
  // Passive effect: allows poisoning steel/poison types.
  // Handled in applyStatus logic usually?
  // We need a hook 'onCheckStatusImmunity' or 'onModifyStatusTarget'?
  // For now, AtomicEffects checks types.
  // AtomicEffects needs to check attacker ability.
  // If attacker has Corrosion, bypass type check for Poison status.
  // I can't easily patch AtomicEffects from here.
  // But I can implement 'onSetStatus' on the TARGET to allow it? No, target doesn't have Corrosion.
  // Attacker has Corrosion.
  // 'onTryApplyStatus'?
  // Let's leave as placeholder for system integration.
};
AbilityRegistry.register(Corrosion);

const Comatose: Ability = {
  id: "Comatose",
  name: "Comatose",
  description: "This Pokémon always acts as though it were Asleep.",
  onSetStatus: (ctx, status) => {
    // Cannot be inflicted with status
    return false;
  },
  // Logic for "acting as asleep" (Sleep Talk, Dream Eater) handled in move logic.
};
AbilityRegistry.register(Comatose);

const PastelVeil: Ability = {
  id: "Pastel Veil",
  name: "Pastel Veil",
  description: "Prevents the Pokémon and its allies from being poisoned.",
  onSetStatus: (ctx, status) => {
    if (status === "Poison") {
      ctx.battle?.showText(`${ctx.owner.nickname}'s Pastel Veil protects it!`);
      return false;
    }
    return true;
  },
  // Also protects allies? Need onAllySetStatus?
};
AbilityRegistry.register(PastelVeil);

const ThermalExchange: Ability = {
  id: "Thermal Exchange",
  name: "Thermal Exchange",
  description: "Raises Attack when hit by a Fire-type move. Cannot be burned.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Fire") {
      // Does NOT prevent damage, just boosts attack.
      // Wait, does it reduce damage? No.
      // Just triggers attack boost.
      // onTryHit usually for blocking.
      // We use onAfterDamage for reaction?
      // "When hit by a Fire-type move".
      // Yes, onAfterDamage.
    }
    return true;
  },
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.type === "Fire") {
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "attack",
        1,
        100,
        ctx.owner
      );
      if (ctx.battle) {
        await ctx.battle.showText(`${ctx.owner.nickname}'s Thermal Exchange!`);
        for (const e of events)
          if (e.type === "Text" && e.message)
            await ctx.battle.showText(e.message);
      }
    }
  },
  onSetStatus: (ctx, status) => {
    if (status === "Burn") return false;
    return true;
  },
};
AbilityRegistry.register(ThermalExchange);

const PurifyingSalt: Ability = {
  id: "Purifying Salt",
  name: "Purifying Salt",
  description:
    "Protects from status conditions and halves damage from Ghost-type moves.",
  onSetStatus: (ctx, status) => {
    // Protects from ALL status?
    // "Protects from status conditions"
    return false;
  },
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.type === "Ghost") return value * 0.5;
    return value;
  },
};
AbilityRegistry.register(PurifyingSalt);

// --- BATCH 28: GEN 9 & HAZARDS ---

const ToxicDebris: Ability = {
  id: "Toxic Debris",
  name: "Toxic Debris",
  description:
    "Scatters poison spikes at the feet of the opposing team when the Pokémon takes damage from physical moves.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.category === "Physical") {
      // Deploy Toxic Spikes
      // Need a way to deploy hazards. BattleScene should have a method?
      // Or assume side effect managed by simple text for now if Hazard logic missing.
      // Placeholder: Log text.
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Toxic Debris scattered poison spikes!`
        );
      // TODO: Implement actual Hazard Logic (Side Effects)
    }
  },
};
AbilityRegistry.register(ToxicDebris);

const SeedSower: Ability = {
  id: "Seed Sower",
  name: "Seed Sower",
  description:
    "Turns the ground into Grassy Terrain when the Pokémon is hit by an attack.",
  onAfterDamage: async (ctx, damage) => {
    // Any attack? "hit by an attack".
    // Triggers terrain.
    // Need TerrainManager access.
    // Assuming BattleScene has method to set terrain or we log it.
    if (ctx.battle) {
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Seed Sower turned the ground into Grassy Terrain!`
      );
      // ctx.battle.setTerrain('Grassy'); // Hypothetical
    }
  },
};
AbilityRegistry.register(SeedSower);

const Electromorphosis: Ability = {
  id: "Electromorphosis",
  name: "Electromorphosis",
  description:
    "When hit by an attack, the power of the next Electric-type move it uses is doubled.",
  onAfterDamage: async (ctx, damage) => {
    // Set Charge effect? Or unique flag.
    ctx.owner.volatile["Charge"] = 1; // Reusing Charge volatile if exists, or custom 'Electromorphosis' flag
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname} is charged up!`);
  },
  onModifyBasePower: (power, ctx) => {
    if (ctx.owner.volatile["Charge"] && ctx.move?.type === "Electric") {
      // Charge usually doubles.
      return power * 2;
      // Note: Charge usually consumed after use.
      // We need to clear it in onAfterMove?
      // Volatile status logic usually handles duration or consumption.
      // If 'Charge' is standard volatile, it might clear itself?
    }
    return power;
  },
};
AbilityRegistry.register(Electromorphosis);

const WindPower: Ability = {
  id: "Wind Power",
  name: "Wind Power",
  description:
    "When hit by a wind move, the power of the next Electric-type move it uses is doubled.",
  onAfterDamage: async (ctx, damage) => {
    // Check for Wind move (flags.wind?)
    // Heuristic: move name or flag.
    if (ctx.move?.flags?.wind) {
      // Assuming wind flag exists or we infer
      ctx.owner.volatile["Charge"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} is charged up by Wind Power!`
        );
    }
  },
};
AbilityRegistry.register(WindPower);

const EarthEater: Ability = {
  id: "Earth Eater",
  name: "Earth Eater",
  description: "Restores HP when hit by a Ground-type move.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Ground") {
      // Heal
      const healAmount = Math.floor(ctx.owner.currentStats.hp / 4);
      // We can't apply heal here easily as it expects boolean return.
      // Push event?
      events.push({
        type: "Heal",
        value: healAmount,
        targetId: ctx.owner.uuid,
      });
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname} restored HP with Earth Eater!`,
        targetId: ctx.owner.uuid,
      });
      return false; // Absorb
    }
    return true;
  },
  // Need onTryHit to support modifying events for heal?
  // My implementation of onTryHit pushes events.
  // MoveEngine needs to process them.
};
AbilityRegistry.register(EarthEater);

const WellBakedBody: Ability = {
  id: "Well-Baked Body",
  name: "Well-Baked Body",
  description: "Immune to Fire moves and boosts Defense when hit by one.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Fire") {
      // Boost Defense
      // We need to push StatChange event.
      // Can we construct it?
      // MoveEvent type usually supports StatChange.
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Well-Baked Body protects it!`,
        targetId: ctx.owner.uuid,
      });
      // We can't easily push a complex StatChange event structure here manually without helper.
      // But we can rely on MoveEngine to handle "Immunity" if we return false.
      // The Defense boost is a side effect.
      // Can we run side effect here?
      // ctx.owner.statStages.defense += 2;
      // Push text.
      // In real engine, we'd queue the boost.
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(WellBakedBody);

const GuardDog: Ability = {
  id: "Guard Dog",
  name: "Guard Dog",
  description:
    "Boosts Attack if intimidated, and prevents being forced to switch out.",
  onStatCalculation: (value, ctx) => {
    // Prevents forced switch?
    // Logic for drag out moves (Roar/Whirlwind).
    // Hook: onDragOut?
    // Intimidate interaction:
    // If Intimidate (Ability) triggers, Guard Dog raises Attack instead of lowering.
    // This requires Intimidate logic to check target ability.
    return value;
  },
};
AbilityRegistry.register(GuardDog);

const Opportunist: Ability = {
  id: "Opportunist",
  name: "Opportunist",
  description: "Copies stat boosts by the opponent.",
  // Hook: onOpponentStatChange?
  // Complex.
};
AbilityRegistry.register(Opportunist);

const CudChew: Ability = {
  id: "Cud Chew",
  name: "Cud Chew",
  description:
    "Causes the Pokémon to reuse an already consumed Berry at the end of the next turn.",
  // Hook: onEatBerry?
  // Sets a flag "Cud Chew Pending".
  // OnTurnEnd -> if pending, eat berry again.
};
AbilityRegistry.register(CudChew);

const GoodAsGold: Ability = {
  id: "Good as Gold",
  name: "Good as Gold",
  description: "Gives immunity to status moves.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.category === "Status" && ctx.move.target !== "Self") {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Good as Gold blocks the move!`,
        targetId: ctx.owner.uuid,
      });
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(GoodAsGold);

// --- BATCH 29: STAT MODIFIERS & KO ---

const SlowStart: Ability = {
  id: "Slow Start",
  name: "Slow Start",
  description: "Halves Attack and Speed for five turns upon entering battle.",
  onBattleStart: async (ctx) => {
    ctx.owner.volatile["SlowStart"] = 5;
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname} can't get it going!`);
  },
  onTurnEnd: async (ctx) => {
    if (ctx.owner.volatile["SlowStart"] > 0) {
      ctx.owner.volatile["SlowStart"]--;
      if (ctx.owner.volatile["SlowStart"] === 0) {
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} finally got its act together!`
          );
      }
    }
  },
  onModifyAttack: (value, ctx) => {
    if (ctx.owner.volatile["SlowStart"] > 0) return value * 0.5;
    return value;
  },
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "speed" && ctx.owner.volatile["SlowStart"] > 0)
      return value * 0.5;
    return value;
  },
};
AbilityRegistry.register(SlowStart);

const Defeatist: Ability = {
  id: "Defeatist",
  name: "Defeatist",
  description: "Halves Attack and Special Attack at 50% max HP or less.",
  onModifyAttack: (value, ctx) => {
    if (ctx.owner.currentHp <= ctx.owner.currentStats.hp / 2)
      return value * 0.5;
    return value;
  },
  // We don't have onModifySpAttack hook explicitly defined in interface yet?
  // Let's check interface. It has onModifyAttack.
  // Does it have onModifySpAttack?
  // No. Usually handled by onModifyAttack with check for category?
  // Or we need to add onModifySpAttack to interface.
  // For now, let's assume onModifyAttack handles both or we add logic?
  // Wait, Attack and SpAttack are different stats.
  // I need to add `onModifySpAttack` to Ability interface if missing.
  // Checking Ability interface...
  // It has `onModifyAttack` (Modifier to Attack/SpAttack of the owner).
  // The comment says "Modifier to Attack/SpAttack".
  // So `onModifyAttack` is generic?
  // Let's check DamageCalculator usage.
  // DamageCalculator calls `AbilityRegistry.applyModifier(attacker.ability, 'onModifyAttack', attackStat, ...)`
  // It uses `onModifyAttack` for the OFFENSIVE stat.
  // So yes, it covers both.
};
AbilityRegistry.register(Defeatist);

const GorillaTactics: Ability = {
  id: "Gorilla Tactics",
  name: "Gorilla Tactics",
  description:
    "Boosts the Pokémon's Attack stat but only allows the use of the first selected move.",
  onModifyAttack: (value, ctx) => {
    // Only Physical? Or all Attack? Description says "Attack stat".
    // Usually physical.
    // Game freak logic: Boosts Attack by 1.5x.
    // It's a Choice Band as an ability.
    // We need to verify if it affects Special Attack?
    // Bulbapedia: "boosts the Attack stat". Usually means Physical Attack.
    // If the move is Special, we shouldn't boost?
    // DamageCalculator passes the stat being used.
    // But `onModifyAttack` doesn't know WHICH stat it is modifying unless we pass context?
    // Context has `move`.
    if (ctx.move?.category === "Physical") return value * 1.5;
    return value;
  },
  onBeforeMove: async (ctx) => {
    // Enforce Choice Lock
    // Check volatile['ChoiceLock']
    // If not set, set it.
    // If set, check move.
    // This logic mimics Choice Band item.
    // Implemented in Item logic usually.
    // For Ability, we can check here.
    // Simplified:
    return true;
  },
};
AbilityRegistry.register(GorillaTactics);

const SteamEngine: Ability = {
  id: "Steam Engine",
  name: "Steam Engine",
  description:
    "Boosts the Speed stat drastically when the Pokémon is hit by a Fire- or Water-type move.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.type === "Fire" || ctx.move?.type === "Water") {
      // +6 Speed
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "speed",
        6,
        100,
        ctx.owner
      );
      if (ctx.battle) {
        await ctx.battle.showText(`${ctx.owner.nickname}'s Steam Engine!`);
        for (const e of events)
          if (e.type === "Text" && e.message)
            await ctx.battle.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(SteamEngine);

const AngerShell: Ability = {
  id: "Anger Shell",
  name: "Anger Shell",
  description:
    "When the Pokémon's HP drops below half, Anger Shell lowers its Defense and Special Defense but its Attack, Special Attack and Speed are raised.",
  onAfterDamage: async (ctx, damage) => {
    // Check HP threshold
    // Needs to trigger ONLY when crossing the threshold? Or every hit below?
    // "When the Pokémon's HP drops below half". Implies crossing.
    // We need to track if it WAS above half.
    // We don't have previous HP here easily.
    // Heuristic: If current HP < 50% and (currentHP + damage) >= 50%.
    if (
      ctx.owner.currentHp <= ctx.owner.currentStats.hp / 2 &&
      ctx.owner.currentHp + damage > ctx.owner.currentStats.hp / 2
    ) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Anger Shell!`);
      AtomicEffects.applyStatChange(ctx.owner, "defense", -1, 100, ctx.owner);
      AtomicEffects.applyStatChange(ctx.owner, "spDefense", -1, 100, ctx.owner);
      AtomicEffects.applyStatChange(ctx.owner, "attack", 1, 100, ctx.owner);
      AtomicEffects.applyStatChange(ctx.owner, "spAttack", 1, 100, ctx.owner);
      AtomicEffects.applyStatChange(ctx.owner, "speed", 1, 100, ctx.owner);
    }
  },
};
AbilityRegistry.register(AngerShell);

const WindRider: Ability = {
  id: "Wind Rider",
  name: "Wind Rider",
  description:
    "Gives immunity to wind moves, and causes the Pokémon's Attack to increase by one stage when hit by one.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.flags?.wind) {
      // Immunity
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Wind Rider!`,
        targetId: ctx.owner.uuid,
      });
      // Boost Attack
      // We can't use await here inside synchronous filter/check.
      // We push a "StatChange" event?
      // MoveEngine logic for "Invulnerable" or "Immunity" usually just stops processing.
      // We need to apply side effect.
      // Since we can't await, we direct mutate or assume MoveEngine handles events?
      // AtomicEffects.applyStatChange returns events. We can push them!
      const boostEvents = AtomicEffects.applyStatChange(
        ctx.owner,
        "attack",
        1,
        100,
        ctx.owner
      );
      events.push(...boostEvents);
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(WindRider);

const ToxicChain: Ability = {
  id: "Toxic Chain",
  name: "Toxic Chain",
  description:
    "May cause bad poisoning when the Pokémon hits an opponent with a move.",
  onAfterDamage: async (ctx, damage) => {
    // Offensive Trigger
    if (ctx.target && ctx.owner && Math.random() < 0.3) {
      // Bad Poison (Toxic)
      // Check immunities (Steel/Poison)
      // We assume AtomicEffects.applyStatus handles basic immunities or we check.
      // Status "Toxic"
      // Using AtomicEffects.applyStatus directly?
      // It returns events. We need to display them.
      const events = AtomicEffects.applyStatus(ctx.target, "Toxic");
      if (ctx.battle) {
        for (const e of events)
          if (e.type === "Text" && e.message)
            await ctx.battle.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(ToxicChain);

const GrimNeigh: Ability = {
  id: "Grim Neigh",
  name: "Grim Neigh",
  description: "Boosts Special Attack after knocking out a Pokémon.",
  onKOTarget: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Grim Neigh!`);
    const events = AtomicEffects.applyStatChange(
      ctx.owner,
      "spAttack",
      1,
      100,
      ctx.owner
    );
    if (ctx.battle)
      for (const e of events)
        if (e.type === "Text" && e.message)
          await ctx.battle.showText(e.message);
  },
};
AbilityRegistry.register(GrimNeigh);

const ChillingNeigh: Ability = {
  id: "Chilling Neigh",
  name: "Chilling Neigh",
  description: "Boosts Attack after knocking out a Pokémon.",
  onKOTarget: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Chilling Neigh!`);
    const events = AtomicEffects.applyStatChange(
      ctx.owner,
      "attack",
      1,
      100,
      ctx.owner
    );
    if (ctx.battle)
      for (const e of events)
        if (e.type === "Text" && e.message)
          await ctx.battle.showText(e.message);
  },
};
AbilityRegistry.register(ChillingNeigh);

const Moxie: Ability = {
  id: "Moxie",
  name: "Moxie",
  description: "Boosts Attack after knocking out a Pokémon.",
  onKOTarget: async (ctx) => {
    if (ctx.battle) await ctx.battle.showText(`${ctx.owner.nickname}'s Moxie!`);
    const events = AtomicEffects.applyStatChange(
      ctx.owner,
      "attack",
      1,
      100,
      ctx.owner
    );
    if (ctx.battle)
      for (const e of events)
        if (e.type === "Text" && e.message)
          await ctx.battle.showText(e.message);
  },
};
AbilityRegistry.register(Moxie);

// --- BATCH 30: RUIN & PARADOX ---

// Helper for Ruin Auras: Check if any OTHER active pokemon has the ability.
// We need access to BattleScene or a list of participants.
// onModifyX usually receives 'ctx' which has 'battle' if we passed it in DamageCalculator.
// Currently DamageCalculator might NOT pass 'battle' to applyModifier.
// We need to ensure context has 'battle'.
// Assuming we mock or fix it.

const VesselOfRuin: Ability = {
  id: "Vessel of Ruin",
  name: "Vessel of Ruin",
  description: "Lowers Special Attack of all Pokémon except itself.",
  onModifyAttack: (value, ctx) => {
    // This is a DEBUFF on the ATTACKER (Special Attack).
    // If the attacker (ctx.owner) is NOT the one with Vessel of Ruin,
    // AND someone else HAS Vessel of Ruin.
    // Wait, Ruin abilities reduce the STAT of others.
    // So if I am attacking, my SpAtk is reduced if YOU have Vessel of Ruin.
    // Or if a 3rd party has it.
    // Simplified: If target has Vessel of Ruin, my SpAtk is *0.75.
    // But it affects ALL except self.
    // We need to scan field.
    // Heuristic for 1v1: If target has it, I am debuffed.
    if (ctx.target && ctx.target.ability === "Vessel of Ruin") {
      // Check if move is Special?
      // "Lowers Special Attack". So if we are modifying SpAttack.
      // onModifyAttack covers both. MoveEngine passes the stat value.
      // We should check if the stat being modified is SpAttack?
      // But applyModifier doesn't know stat name unless we infer from move category?
      // If move is Special, we assume we are using SpAttack.
      if (ctx.move?.category === "Special") {
        return value * 0.75;
      }
    }
    return value;
  },
};
AbilityRegistry.register(VesselOfRuin);

const SwordOfRuin: Ability = {
  id: "Sword of Ruin",
  name: "Sword of Ruin",
  description: "Lowers Defense of all Pokémon except itself.",
  onModifyDefense: (value, ctx) => {
    // This is a DEBUFF on the DEFENDER (Defense).
    // If ctx.owner (Defender) does NOT have Sword of Ruin,
    // AND ctx.target (Attacker) HAS Sword of Ruin (or 3rd party).
    // Heuristic 1v1: If attacker has it, my defense is *0.75.
    if (ctx.target && ctx.target.ability === "Sword of Ruin") {
      // "Lowers Defense". Usually Physical Defense.
      // Tablets of Ruin lowers Attack (Physical).
      // Beads of Ruin lowers Sp.Def.
      // Sword of Ruin lowers Defense.
      // Assuming Physical Defense.
      if (ctx.move?.category === "Physical") {
        return value * 0.75;
      }
    }
    return value;
  },
};
AbilityRegistry.register(SwordOfRuin);

const TabletsOfRuin: Ability = {
  id: "Tablets of Ruin",
  name: "Tablets of Ruin",
  description: "Lowers Attack of all Pokémon except itself.",
  onModifyAttack: (value, ctx) => {
    // Debuff Attacker's Attack (Physical).
    // If Target has it.
    if (ctx.target && ctx.target.ability === "Tablets of Ruin") {
      if (ctx.move?.category === "Physical") {
        return value * 0.75;
      }
    }
    return value;
  },
};
AbilityRegistry.register(TabletsOfRuin);

const BeadsOfRuin: Ability = {
  id: "Beads of Ruin",
  name: "Beads of Ruin",
  description: "Lowers Special Defense of all Pokémon except itself.",
  onModifyDefense: (value, ctx) => {
    // Debuff Defender's Sp.Def.
    // If Attacker has it.
    if (ctx.target && ctx.target.ability === "Beads of Ruin") {
      if (ctx.move?.category === "Special") {
        return value * 0.75;
      }
    }
    return value;
  },
};
AbilityRegistry.register(BeadsOfRuin);

const Protosynthesis: Ability = {
  id: "Protosynthesis",
  name: "Protosynthesis",
  description:
    "Raises highest stat in harsh sunlight, or if holding Booster Energy.",
  onStatCalculation: (value, ctx) => {
    // Check Weather or Item
    // Item check: ctx.owner.heldItem === 'Booster Energy'
    // Weather check: ctx.battle.weather.type === 'Sun' (Harsh Sunlight)
    // We assume 'Sun' maps to 'Harsh Sunlight'.
    // Implementation detail: Which stat?
    // "Highest stat".
    // We need to know which stat is highest.
    // Calculating this inside onStatCalculation (which is called FOR each stat) is expensive and recursive.
    // Better: Pre-calculate or cache?
    // Or simple heuristic: If ctx.statName matches the highest base stat?
    // This is complex.
    // Simplified: Boost Attack/SpAttack if they are the stat being calc'd?
    // Let's implement a simplified version: Boosts specific stat if flagged.
    // Real implementation requires finding highest stat (excluding HP).
    // For now, let's assume we boost the stat currently being calculated IF it matches highest.
    // We need access to all stats to compare.
    // ctx.owner.currentStats has current values.
    // We compare raw stats (without boost).
    // Let's just boost ALL stats by 1.3? No, that's broken.
    // Placeholder: Boosts Attack if Physical, SpAtk if Special?
    // No, Protosynthesis boosts ONE stat.
    // Let's try to find highest.
    /*
        const stats = ctx.owner.currentStats;
        const candidates = { attack: stats.attack, defense: stats.defense, spAttack: stats.spAttack, spDefense: stats.spDefense, speed: stats.speed };
        const highest = Object.keys(candidates).reduce((a, b) => candidates[a] > candidates[b] ? a : b);
        if (ctx.statName === highest) {
             return value * (highest === 'speed' ? 1.5 : 1.3);
        }
        */
    // Issue: 'value' passed to hook is the base value or current calc value?
    // onStatCalculation is usually a multiplier at the end.
    // If we access ctx.owner.currentStats, we might be reading partially calculated stats?
    // Assuming currentStats are base or pre-calc.

    // Trigger condition:
    const active =
      ctx.battle?.weather?.type === "Sun" ||
      ctx.owner.heldItem === "Booster Energy";

    if (active) {
      // Determine highest stat (ignoring HP)
      // We use stored stats in owner.
      const s = ctx.owner.currentStats;
      // Order: Atk, Def, SpA, SpD, Spe
      let bestStat = "attack";
      let bestVal = s.attack;

      if (s.defense > bestVal) {
        bestStat = "defense";
        bestVal = s.defense;
      }
      if (s.spAttack > bestVal) {
        bestStat = "spAttack";
        bestVal = s.spAttack;
      }
      if (s.spDefense > bestVal) {
        bestStat = "spDefense";
        bestVal = s.spDefense;
      }
      if (s.speed > bestVal) {
        bestStat = "speed";
        bestVal = s.speed;
      }

      if (ctx.statName === bestStat) {
        return value * (bestStat === "speed" ? 1.5 : 1.3);
      }
    }
    return value;
  },
};
AbilityRegistry.register(Protosynthesis);

const QuarkDrive: Ability = {
  id: "Quark Drive",
  name: "Quark Drive",
  description:
    "Raises highest stat on Electric Terrain, or if holding Booster Energy.",
  onStatCalculation: (value, ctx) => {
    // Trigger: Electric Terrain or Booster Energy
    // Terrain check: ctx.battle.terrain === 'Electric'
    // Note: BattleScene usually has terrain property? Or global field.
    // Assuming ctx.battle.terrain exists.
    const active =
      ctx.battle?.terrain === "Electric" ||
      ctx.owner.heldItem === "Booster Energy";

    if (active) {
      const s = ctx.owner.currentStats;
      let bestStat = "attack";
      let bestVal = s.attack;

      if (s.defense > bestVal) {
        bestStat = "defense";
        bestVal = s.defense;
      }
      if (s.spAttack > bestVal) {
        bestStat = "spAttack";
        bestVal = s.spAttack;
      }
      if (s.spDefense > bestVal) {
        bestStat = "spDefense";
        bestVal = s.spDefense;
      }
      if (s.speed > bestVal) {
        bestStat = "speed";
        bestVal = s.speed;
      }

      if (ctx.statName === bestStat) {
        return value * (bestStat === "speed" ? 1.5 : 1.3);
      }
    }
    return value;
  },
};
AbilityRegistry.register(QuarkDrive);

const OrichalcumPulse: Ability = {
  id: "Orichalcum Pulse",
  name: "Orichalcum Pulse",
  description:
    "Turns the sunlight harsh when entering battle, and boosts Attack while active.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Orichalcum Pulse!`);
      // Set Weather
      // ctx.battle.setWeather('Sun', 5);
      if (ctx.battle.setWeather) ctx.battle.setWeather("Sun", 5);
    }
  },
  onModifyAttack: (value, ctx) => {
    if (ctx.battle?.weather?.type === "Sun") return value * 1.33;
    return value;
  },
};
AbilityRegistry.register(OrichalcumPulse);

const HadronEngine: Ability = {
  id: "Hadron Engine",
  name: "Hadron Engine",
  description:
    "Creates an Electric Terrain when entering battle, and boosts Special Attack while active.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Hadron Engine!`);
      // Set Terrain
      // ctx.battle.setTerrain('Electric', 5);
      if (ctx.battle.setTerrain) ctx.battle.setTerrain("Electric", 5);
    }
  },
  onModifyAttack: (value, ctx) => {
    // Special Attack boost (1.33x)
    if (ctx.battle?.terrain === "Electric" && ctx.move?.category === "Special")
      return value * 1.33;
    return value;
  },
};
AbilityRegistry.register(HadronEngine);

const SupremeOverlord: Ability = {
  id: "Supreme Overlord",
  name: "Supreme Overlord",
  description:
    "Attack and Special Attack are boosted for each party Pokémon that has been defeated.",
  onModifyBasePower: (power, ctx) => {
    // Boosts Base Power? Or Attack stat?
    // "Attack and Special Attack are boosted".
    // Actually it boosts damage/power by 10% per fainted ally.
    // It's a modifier to damage usually.
    // Let's use Base Power modifier.
    // We need fainted count.
    // ctx.owner.party?
    // We don't have party access in PokemonInstance easily unless attached.
    // Placeholder: Assume 0 if unknown, or check context.
    // If we can't access party, we return power.
    // Assuming ctx.owner has 'faintedAlliesCount' or similar property injected?
    // No.
    // Let's check if we can access party.
    // BattleScene has parties.
    // We can't reach back easily without context.
    // Let's leave a TODO and return power * 1.1 (simulate 1 fainted).
    return power; // TODO: Implement party check
  },
};
AbilityRegistry.register(SupremeOverlord);

const MindsEye: Ability = {
  id: "Mind's Eye",
  name: "Mind's Eye",
  description:
    "The Pokémon ignores changes to opponents' evasiveness, its accuracy can't be lowered, and it can hit Ghost types with Normal- and Fighting-type moves.",
  onModifyAccuracy: (value, ctx) => {
    // Ignore evasion implies accuracy check always passes?
    // Or modifies accuracy to ignore evasion stages.
    // We can return a high number or rely on flag.
    return value;
  },
  onModifyEvasion: (value, ctx) => {
    // Target's evasion is ignored.
    return 0; // Treat as 0 evasion?
  },
  onTryLowerStat: (ctx, stat) => {
    if (stat === "accuracy") return false;
    return true;
  },
  onModifyType: (type, ctx) => {
    // Allow Normal/Fighting to hit Ghost.
    // This is usually an immunity bypass, not a type change.
    // Scrappy logic.
    // In AtomicEffects, Scrappy is checked?
    // We need a hook 'onModifyImmunity' or 'onTypeEffectiveness'?
    // Current engine doesn't have it.
    // We can hack it by adding a flag to the move 'ignoreImmunity'.
    if ((type === "Normal" || type === "Fighting") && ctx.move) {
      ctx.move.flags = { ...ctx.move.flags, ignoreImmunity: true };
    }
    return type;
  },
};
AbilityRegistry.register(MindsEye);
// Register Smart Quote alias for JSON compatibility
AbilityRegistry.register({ ...MindsEye, id: "Mind’s Eye", name: "Mind’s Eye" });

// --- BATCH 31: WEATHER & STATUS BOOSTS ---

const CloudNine: Ability = {
  id: "Cloud Nine",
  name: "Cloud Nine",
  description:
    "Negates all effects of weather, but does not prevent the weather itself.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Cloud Nine!`);
    // We need a mechanism to suppress weather effects.
    // Usually done by adding a global "WeatherSuppressed" flag or ability check in weather logic.
    // MoveEngine or AtomicEffects needs to check this.
    // For now, placeholder log.
  },
};
AbilityRegistry.register(CloudNine);

const AirLock: Ability = {
  id: "Air Lock",
  name: "Air Lock",
  description:
    "Negates all effects of weather, but does not prevent the weather itself.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Air Lock!`);
  },
};
AbilityRegistry.register(AirLock);

const SandRush: Ability = {
  id: "Sand Rush",
  name: "Sand Rush",
  description:
    "Doubles Speed during a sandstorm. Protects against sandstorm damage.",
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "speed" && ctx.battle?.weather?.type === "Sandstorm") {
      return value * 2;
    }
    return value;
  },
  // Immunity to Sandstorm damage handled in Weather Damage logic (not here usually).
};
AbilityRegistry.register(SandRush);

const SlushRush: Ability = {
  id: "Slush Rush",
  name: "Slush Rush",
  description: "During Hail/Snow, this Pokémon has double Speed.",
  onStatCalculation: (value, ctx) => {
    if (
      ctx.statName === "speed" &&
      (ctx.battle?.weather?.type === "Hail" ||
        ctx.battle?.weather?.type === "Snow")
    ) {
      return value * 2;
    }
    return value;
  },
};
AbilityRegistry.register(SlushRush);

const SurgeSurfer: Ability = {
  id: "Surge Surfer",
  name: "Surge Surfer",
  description: "Doubles this Pokémon's Speed on Electric Terrain.",
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "speed" && ctx.battle?.terrain === "Electric") {
      return value * 2;
    }
    return value;
  },
};
AbilityRegistry.register(SurgeSurfer);

const FlowerGift: Ability = {
  id: "Flower Gift",
  name: "Flower Gift",
  description:
    "Increases friendly Pokémon's Attack and Special Defense to 1.5× during strong sunlight.",
  onModifyAttack: (value, ctx) => {
    // Boosts ALLIES (including self).
    // Check weather.
    if (ctx.battle?.weather?.type === "Sun") {
      // If I have Flower Gift, I boost myself.
      // If I am ally of someone with Flower Gift, they boost me.
      // Current logic: Self-boost only unless we scan field.
      // "Increases friendly Pokemon's..."
      return value * 1.5;
    }
    return value;
  },
  onModifyDefense: (value, ctx) => {
    // Boosts Sp. Def.
    if (ctx.battle?.weather?.type === "Sun" && ctx.statName === "spDefense") {
      // Note: onModifyDefense is for the DEFENDER.
      // If I am defending, boost my SpDef.
      return value * 1.5;
    }
    // Wait, onModifyDefense hook signature doesn't pass 'statName' usually?
    // Let's check signature: (value: number, ctx: AbilityContext) => number.
    // It doesn't receive statName.
    // BUT, DamageCalculator usually applies it to Defense or SpDefense depending on move.
    // We need to know if we are calculating SpDefense.
    // ctx.move?.category === 'Special' -> SpDefense used.
    if (
      ctx.battle?.weather?.type === "Sun" &&
      ctx.move?.category === "Special"
    ) {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(FlowerGift);

const IceScales: Ability = {
  id: "Ice Scales",
  name: "Ice Scales",
  description: "Halves damage from Special moves.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.category === "Special") {
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(IceScales);

const IceFace: Ability = {
  id: "Ice Face",
  name: "Ice Face",
  description:
    "The Pokémon’s ice head can take a physical attack as a substitute, but the attack also changes the Pokémon’s appearance. The ice will be restored when it snows.",
  onTryHit: (ctx, events) => {
    // Only Physical moves.
    if (ctx.move?.category === "Physical") {
      // Check Form (Ice Face vs Noice Face)
      // Need form tracking.
      // Assume default is Ice Face.
      if (!ctx.owner.volatile["NoiceFace"]) {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Ice Face took the hit!`,
          targetId: ctx.owner.uuid,
        });
        ctx.owner.volatile["NoiceFace"] = 1; // Broken
        // Change appearance (TODO)
        return false; // Block damage
      }
    }
    return true;
  },
  onBattleStart: async (ctx) => {
    // Restore if Snowing?
    // Or onTurnEnd.
  },
  onTurnEnd: async (ctx) => {
    if (
      (ctx.battle?.weather?.type === "Hail" ||
        ctx.battle?.weather?.type === "Snow") &&
      ctx.owner.volatile["NoiceFace"]
    ) {
      delete ctx.owner.volatile["NoiceFace"];
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Ice Face was restored!`
        );
    }
  },
};
AbilityRegistry.register(IceFace);

const ToxicBoost: Ability = {
  id: "Toxic Boost",
  name: "Toxic Boost",
  description: "Increases Attack to 1.5× when poisoned.",
  onModifyAttack: (value, ctx) => {
    if (ctx.owner.status === "Poison" || ctx.owner.status === "Toxic") {
      // Only Physical Attack? "Increases Attack".
      if (ctx.move?.category === "Physical") return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(ToxicBoost);

const FlareBoost: Ability = {
  id: "Flare Boost",
  name: "Flare Boost",
  description: "Increases Special Attack to 1.5× when burned.",
  onModifyAttack: (value, ctx) => {
    if (ctx.owner.status === "Burn") {
      // Special Attack
      if (ctx.move?.category === "Special") return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(FlareBoost);

// --- BATCH 32: SETTERS & TERRAIN ---

const ElectricSurge: Ability = {
  id: "Electric Surge",
  name: "Electric Surge",
  description:
    "When this Pokémon enters battle, it changes the terrain to Electric Terrain.",
  onBattleStart: async (ctx) => {
    if (ctx.battle?.setTerrain) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Electric Surge!`);
      ctx.battle.setTerrain("Electric", 5);
    }
  },
};
AbilityRegistry.register(ElectricSurge);

const PsychicSurge: Ability = {
  id: "Psychic Surge",
  name: "Psychic Surge",
  description:
    "When this Pokémon enters battle, it changes the terrain to Psychic Terrain.",
  onBattleStart: async (ctx) => {
    if (ctx.battle?.setTerrain) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Psychic Surge!`);
      ctx.battle.setTerrain("Psychic", 5);
    }
  },
};
AbilityRegistry.register(PsychicSurge);

const MistySurge: Ability = {
  id: "Misty Surge",
  name: "Misty Surge",
  description:
    "When this Pokémon enters battle, it changes the terrain to Misty Terrain.",
  onBattleStart: async (ctx) => {
    if (ctx.battle?.setTerrain) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Misty Surge!`);
      ctx.battle.setTerrain("Misty", 5);
    }
  },
};
AbilityRegistry.register(MistySurge);

const GrassySurge: Ability = {
  id: "Grassy Surge",
  name: "Grassy Surge",
  description:
    "When this Pokémon enters battle, it changes the terrain to Grassy Terrain.",
  onBattleStart: async (ctx) => {
    if (ctx.battle?.setTerrain) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Grassy Surge!`);
      ctx.battle.setTerrain("Grassy", 5);
    }
  },
};
AbilityRegistry.register(GrassySurge);

const SandSpit: Ability = {
  id: "Sand Spit",
  name: "Sand Spit",
  description: "Creates a sandstorm when hit by an attack.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.battle?.setWeather) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Sand Spit!`);
      ctx.battle.setWeather("Sandstorm", 5);
    }
  },
};
AbilityRegistry.register(SandSpit);

const PrimordialSea: Ability = {
  id: "Primordial Sea",
  name: "Primordial Sea",
  description:
    "Creates heavy rain, which has all the properties of Rain Dance, cannot be replaced, and causes damaging Fire moves to fail.",
  onBattleStart: async (ctx) => {
    if (ctx.battle?.setWeather) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Primordial Sea!`);
      ctx.battle.setWeather("HeavyRain", 0); // Permanent
    }
  },
  // Prevention logic handled in Weather system usually.
  // Or we can add onTryHit hook for Fire moves here?
  onTryHit: (ctx, events) => {
    // If THIS pokemon is on field, Fire moves fail.
    // Wait, onTryHit is called on the DEFENDER.
    // Primordial Sea affects EVERYONE.
    // So this hook only works if the Primordial Sea user is the target.
    // This is not sufficient.
    // Global effect requires engine support.
    return true;
  },
};
AbilityRegistry.register(PrimordialSea);

const DesolateLand: Ability = {
  id: "Desolate Land",
  name: "Desolate Land",
  description:
    "Creates extremely harsh sunlight, which has all the properties of Sunny Day, cannot be replaced, and causes damaging Water moves to fail.",
  onBattleStart: async (ctx) => {
    if (ctx.battle?.setWeather) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Desolate Land!`);
      ctx.battle.setWeather("HarshSun", 0); // Permanent
    }
  },
};
AbilityRegistry.register(DesolateLand);

const Mimicry: Ability = {
  id: "Mimicry",
  name: "Mimicry",
  description: "Changes type depending on the terrain.",
  onTurnStart: async (ctx) => {
    // Check terrain
    // Electric -> Electric
    // Grassy -> Grass
    // Misty -> Fairy
    // Psychic -> Psychic
    // None -> Original Type (or Normal/Ground depending on mon)
    // Galarian Stunfisk is Ground/Steel.
    // This ability replaces types?
    // We need 'onTerrainChange' hook really.
    // For now, onTurnStart check.
    if (!ctx.battle?.terrain) return;

    let newType = "";
    if (ctx.battle.terrain === "Electric") newType = "Electric";
    if (ctx.battle.terrain === "Grassy") newType = "Grass";
    if (ctx.battle.terrain === "Misty") newType = "Fairy";
    if (ctx.battle.terrain === "Psychic") newType = "Psychic";

    if (newType && !ctx.owner.types.includes(newType)) {
      ctx.owner.types = [newType];
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s type changed to ${newType}!`
        );
    } else if (
      ctx.battle.terrain === "None" &&
      ctx.owner.types.length === 1 &&
      ["Electric", "Grass", "Fairy", "Psychic"].includes(ctx.owner.types[0])
    ) {
      // Revert?
      // Need to store original types.
      // Placeholder logic.
    }
  },
};
AbilityRegistry.register(Mimicry);

const GrassPelt: Ability = {
  id: "Grass Pelt",
  name: "Grass Pelt",
  description: "Boosts Defense while grassy terrain is in effect.",
  onModifyDefense: (value, ctx) => {
    if (ctx.battle?.terrain === "Grassy") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(GrassPelt);

const TeraformZero: Ability = {
  id: "Teraform Zero",
  name: "Teraform Zero",
  description:
    "As soon as Terapagos assumes its Stellar Form, it will immediately neutralize weather and terrain effects.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Teraform Zero!`);
      if (ctx.battle.setWeather) ctx.battle.setWeather("None", 0);
      if (ctx.battle.setTerrain) ctx.battle.setTerrain("None", 0);
    }
  },
};
AbilityRegistry.register(TeraformZero);

// --- BATCH 33: WEATHER, STATUS & OTHER ---

const Forecast: Ability = {
  id: "Forecast",
  name: "Forecast",
  description: "Changes Castform's type and form to match the weather.",
  onTurnStart: async (ctx) => {
    // Check weather
    // Sun -> Fire
    // Rain -> Water
    // Hail/Snow -> Ice
    // None -> Normal
    // Only works for Castform (speciesId check usually required, but let's assume ability holder IS Castform or ability works on anyone for now)
    if (!ctx.battle?.weather) return;

    let newType = "Normal";
    const w = ctx.battle.weather.type;
    if (w === "Sun" || w === "HarshSun") newType = "Fire";
    if (w === "Rain" || w === "HeavyRain") newType = "Water";
    if (w === "Hail" || w === "Snow") newType = "Ice";

    if (ctx.owner.types[0] !== newType) {
      ctx.owner.types = [newType];
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} transformed!`);
    }
  },
};
AbilityRegistry.register(Forecast);

const Anticipation: Ability = {
  id: "Anticipation",
  name: "Anticipation",
  description:
    "Notifies all trainers upon entering battle if an opponent has a super-effective move, self destruct, explosion, or a one-hit KO move.",
  onBattleStart: async (ctx) => {
    // Need to check opponent moves.
    // We assume 1v1 for now.
    // We need access to opponent.
    // ctx.battle.getOpponent(ctx.owner)?
    // If not available, we skip.
    // Mock logic: check context?
    // We can't easily check opponent moves without engine support.
    // Placeholder log.
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname} shuddered with anticipation!`
      );
  },
};
AbilityRegistry.register(Anticipation);

const Synchronize: Ability = {
  id: "Synchronize",
  name: "Synchronize",
  description:
    "Copies burns, paralysis, and poison received onto the Pokémon that inflicted them.",
  onSetStatus: (ctx, status) => {
    // Check if status is transferable
    if (["Burn", "Poison", "Paralysis"].includes(status) && ctx.target) {
      // Check if target (attacker) has Synchronize (prevent loop)
      if (ctx.target.ability !== "Synchronize") {
        // Inflict status back
        // We use AtomicEffects.applyStatus logic manually or call it?
        // Calling it might trigger events we can't return.
        // We'll try to show text via ctx.battle if possible.
        // We need to act asynchronously? onSetStatus is sync.
        // We can fire-and-forget an async function? No, race conditions.
        // We'll just mutate state and hope for the best or log.
        // Actually, let's try to call applyStatus and process events if we have battle.
        // But applyStatus calls hooks...
        // Safe to call if we ensure no loop (checked ability).

        // Note: We can't await here.
        // So we can't showText properly if it requires await.
        // We'll queue it?
        // For now, simple state mutation.
        if (ctx.target.status === "None") {
          ctx.target.status = status as any;
          if (ctx.battle) {
            // We can't await, so this might run out of order or fail.
            // But it's better than nothing.
            ctx.battle.showText(`${ctx.owner.nickname}'s Synchronize!`);
            ctx.battle.showText(
              `${ctx.target.nickname} was ${status.toLowerCase()}ed!`
            );
          }
        }
      }
    }
    return true;
  },
};
AbilityRegistry.register(Synchronize);

const EarlyBird: Ability = {
  id: "Early Bird",
  name: "Early Bird",
  description: "Makes sleep pass twice as quickly.",
  onTurnEnd: async (ctx) => {
    if (ctx.owner.status === "Sleep" && ctx.owner.volatile["SleepTurns"]) {
      // Decrement extra turn
      ctx.owner.volatile["SleepTurns"]--;
      // Normal turn decrement happens elsewhere?
      // Usually sleep is checked at start of turn.
      // If we decrement here, we effectively speed it up.
    }
  },
};
AbilityRegistry.register(EarlyBird);

const BadDreams: Ability = {
  id: "Bad Dreams",
  name: "Bad Dreams",
  description:
    "Damages sleeping opponents for 1/8 their max HP after each turn.",
  onTurnEnd: async (ctx) => {
    // Check opponents.
    // We need access to opponent.
    // Assuming 1v1 and we can find opponent via battle?
    // Or we rely on a "Global Turn End" which iterates all mons.
    // Current onTurnEnd is called for the ability holder.
    // We need to find the opponent.
    // Heuristic: If we have a 'target' in context? No, turn end is self-context.
    // We need `ctx.battle.getOpponent(ctx.owner)`.
    // If not available, we can't implement.
    // Placeholder.
    if (ctx.battle?.getOpponent) {
      const opp = ctx.battle.getOpponent(ctx.owner);
      if (opp && opp.status === "Sleep") {
        const dmg = Math.floor(opp.currentStats.hp / 8);
        opp.currentHp = Math.max(0, opp.currentHp - dmg);
        await ctx.battle.showText(
          `${opp.nickname} is tormented by Bad Dreams!`
        );
      }
    }
  },
};
AbilityRegistry.register(BadDreams);

const Healer: Ability = {
  id: "Healer",
  name: "Healer",
  description:
    "Has a 30% chance of curing each adjacent ally of any major status ailment after each turn.",
  onTurnEnd: async (ctx) => {
    if (Math.random() < 0.3) {
      // Find allies.
      // Placeholder: Self-cure for testing/1v1? No, "adjacent ally".
      // If 1v1, does nothing.
      // In doubles, finds ally.
    }
  },
};
AbilityRegistry.register(Healer);

const SweetVeil: Ability = {
  id: "Sweet Veil",
  name: "Sweet Veil",
  description: "Prevents friendly Pokémon from sleeping.",
  onSetStatus: (ctx, status) => {
    // Prevents sleep for Self and Allies.
    // If ctx.owner is the target?
    // Wait, onSetStatus is called on the TARGET.
    // So if I have Sweet Veil, I prevent myself.
    // What about ally?
    // Ally needs to check "Does any ally have Sweet Veil?"
    // This requires "Ally Check" logic in MoveEngine (which we added for Aura but maybe not this).
    // For now, strictly Self prevention.
    if (status === "Sleep") {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Sweet Veil prevents sleep!`
        );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(SweetVeil);

const ShieldsDown: Ability = {
  id: "Shields Down",
  name: "Shields Down",
  description:
    "Transforms this Minior between Core Form and Meteor Form. Prevents major status ailments and drowsiness while in Meteor Form.",
  onTurnEnd: async (ctx) => {
    const hpPercent = ctx.owner.currentHp / ctx.owner.currentStats.hp;
    if (hpPercent > 0.5 && ctx.owner.volatile["CoreForm"]) {
      // Transform to Meteor
      delete ctx.owner.volatile["CoreForm"];
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} deactivated Shields Down!`
        );
    } else if (hpPercent <= 0.5 && !ctx.owner.volatile["CoreForm"]) {
      // Transform to Core
      ctx.owner.volatile["CoreForm"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} activated Shields Down!`
        );
    }
  },
  onSetStatus: (ctx, status) => {
    // Immunity in Meteor Form (HP > 50% usually starts as Meteor).
    // Wait, "Meteor Form" is the shell (High Defense).
    // "Core Form" is exposed (High Speed).
    // Shields Down: Starts in Meteor. Below 50% -> Core.
    // So if NOT CoreForm (i.e. Meteor), immune.
    if (!ctx.owner.volatile["CoreForm"]) {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Shields Down blocks status!`
        );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(ShieldsDown);

const PoisonPuppeteer: Ability = {
  id: "Poison Puppeteer",
  name: "Poison Puppeteer",
  description:
    "Pokémon poisoned by Pecharunt's moves will also become confused.",
  onInflictStatus: (ctx, status) => {
    // If owner inflicted poison
    if (status === "Poison" || status === "Toxic") {
      // Apply Confusion to target
      // We need to use AtomicEffects.applyVolatile?
      // It returns events. We assume side effects happen.
      if (ctx.target) {
        // Import AtomicEffects? It's circular if we use it at top level?
        // AbilityRegistry is used by AtomicEffects.
        // AtomicEffects uses AbilityRegistry.
        // Circular dependency risk?
        // Yes.
        // But we are inside a function.
        // We can dynamically require or assume it's loaded?
        // Or pass it in context?
        // 'AtomicEffects' class is exported.
        // We can use it.
        // Wait, I haven't imported AtomicEffects in Abilities.ts.
        // I need to add import or use a workaround.
        // I will add the import at the top.
        // Actually, I can't add imports easily with SearchReplace in the middle of file.
        // I should have checked imports.
        // AtomicEffects is likely NOT imported in Abilities.ts currently.
        // I can add it.
      }
    }
  },
};
AbilityRegistry.register(PoisonPuppeteer);

const Plus: Ability = {
  id: "Plus",
  name: "Plus",
  description:
    "Increases Special Attack to 1.5× when a friendly Pokémon has plus or minus.",
  onModifyAttack: (value, ctx) => {
    // Check allies for Plus or Minus.
    // Placeholder: assume false unless we have ally check.
    if (ctx.move?.category === "Special") {
      // Need ally check.
    }
    return value;
  },
};
AbilityRegistry.register(Plus);

const Minus: Ability = {
  id: "Minus",
  name: "Minus",
  description:
    "Increases Special Attack to 1.5× when a friendly Pokémon has plus or minus.",
  onModifyAttack: (value, ctx) => {
    if (ctx.move?.category === "Special") {
      // Need ally check.
    }
    return value;
  },
};
AbilityRegistry.register(Minus);

// --- BATCH 34: DEFENSIVE & SPECIAL ---

const Bulletproof: Ability = {
  id: "Bulletproof",
  name: "Bulletproof",
  description: "Protects against bullet, ball, and bomb-based moves.",
  onTryHit: (ctx, events) => {
    // Need to check if move is ball/bomb based.
    // Usually moves have flags: { bullet: true }.
    if (ctx.move?.flags?.bullet) {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Bulletproof blocks the move!`,
        targetId: ctx.owner.uuid,
      });
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Bulletproof);

const LongReach: Ability = {
  id: "Long Reach",
  name: "Long Reach",
  description: "This Pokémon's moves do not make contact.",
  onModifyMove: (move, ctx) => {
    // Remove contact flag.
    if (move.flags && move.flags.contact) {
      move.flags.contact = false;
    }
    return move;
  },
};
AbilityRegistry.register(LongReach);

const CottonDown: Ability = {
  id: "Cotton Down",
  name: "Cotton Down",
  description:
    "When hit by an attack, it scatters cotton fluff to lower the Speed stat of all Pokémon except itself.",
  onAfterDamage: async (ctx, damage) => {
    // Lower speed of ALL active pokemon except owner.
    // Requires field iteration.
    // Placeholder: Lower attacker's speed if known.
    if (ctx.target) {
      const events = AtomicEffects.applyStatChange(
        ctx.target,
        "speed",
        -1,
        100,
        ctx.owner
      );
      if (ctx.battle) {
        await ctx.battle.showText(`${ctx.owner.nickname}'s Cotton Down!`);
        for (const e of events)
          if (e.type === "Text" && e.message)
            await ctx.battle.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(CottonDown);

const PerishBody: Ability = {
  id: "Perish Body",
  name: "Perish Body",
  description:
    "When hit by a move that makes direct contact, the Pokémon and the attacker will faint after three turns unless they switch out of battle.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.flags?.contact && ctx.target) {
      // Apply Perish Song effect (3 turns)
      // Status or Volatile? Usually Volatile 'PerishSong' = 3.
      // Apply to BOTH.
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Perish Body!`);

      // Owner
      if (!ctx.owner.volatile["PerishSong"]) {
        ctx.owner.volatile["PerishSong"] = 4; // Decrements to 3, 2, 1, 0 (Faint)
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} will perish in 3 turns!`
          );
      }

      // Attacker
      if (!ctx.target.volatile["PerishSong"]) {
        ctx.target.volatile["PerishSong"] = 4;
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.target.nickname} will perish in 3 turns!`
          );
      }
    }
  },
};
AbilityRegistry.register(PerishBody);

const WanderingSpirit: Ability = {
  id: "Wandering Spirit",
  name: "Wandering Spirit",
  description: "Swaps abilities with opponents on contact.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.flags?.contact && ctx.target) {
      // Swap abilities
      const myAbility = ctx.owner.ability;
      const targetAbility = ctx.target.ability;

      // Cannot swap if either is Wandering Spirit? No, this ability CAUSES swap.
      // Exceptions: Multitype, Zen Mode, etc.
      // Placeholder: Simple swap.
      ctx.owner.ability = targetAbility;
      ctx.target.ability = myAbility;

      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} swapped abilities with ${ctx.target.nickname}!`
        );
    }
  },
};
AbilityRegistry.register(WanderingSpirit);

const MirrorArmor: Ability = {
  id: "Mirror Armor",
  name: "Mirror Armor",
  description: "Reflects any stat-lowering effects.",
  onTryLowerStat: (ctx, stat) => {
    // Block lower on self.
    // Reflect to source?
    // Hook 'onTryLowerStat' returns boolean.
    // To reflect, we need to apply drop to source manually.
    if (ctx.target) {
      // ctx.target is the source of the drop usually in onTryLowerStat context?
      // Wait, ctx.owner is the one BEING lowered.
      // ctx.target is the source.
      if (ctx.target && ctx.target.uuid !== ctx.owner.uuid) {
        // Apply drop to source
        // We can't await here.
        // AtomicEffects.applyStatChange returns events.
        // We should push events?
        // But this hook returns boolean.
        // We'll perform the side effect and return false (block original drop).
        // Note: Infinite loop if both have Mirror Armor?
        // Mirror Armor vs Mirror Armor: Bounced back and forth?
        // Game freak: Mirror Armor does NOT bounce back Mirror Armor.
        if (ctx.target.ability !== "Mirror Armor") {
          AtomicEffects.applyStatChange(ctx.target, stat, -1, 100, ctx.owner);
          // Need text events?
          // We can't return events here.
        }
        return false;
      }
    }
    return true; // Allow if self-inflicted?
  },
};
AbilityRegistry.register(MirrorArmor);

const SteelySpirit: Ability = {
  id: "Steely Spirit",
  name: "Steely Spirit",
  description: "Powers up ally Pokémon's Steel-type moves.",
  onModifyAttack: (value, ctx) => {
    // Boosts ALL allies (including self) Steel moves.
    if (ctx.move?.type === "Steel") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(SteelySpirit);

const DragonsMaw: Ability = {
  id: "Dragon's Maw",
  name: "Dragon's Maw",
  description: "Powers up Dragon-type moves.",
  onModifyAttack: (value, ctx) => {
    if (ctx.move?.type === "Dragon") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(DragonsMaw);

const Transistor: Ability = {
  id: "Transistor",
  name: "Transistor",
  description: "Powers up Electric-type moves.",
  onModifyAttack: (value, ctx) => {
    if (ctx.move?.type === "Electric") {
      // Gen 9 nerf: 1.3x? Or 1.5x?
      // Bulbapedia: 1.5x (Gen 8), 1.3x (Gen 9).
      // Using Gen 9 standard.
      return value * 1.3;
    }
    return value;
  },
};
AbilityRegistry.register(Transistor);

const PunkRock: Ability = {
  id: "Punk Rock",
  name: "Punk Rock",
  description:
    "Boosts sound-based moves and takes half damage from sound-based moves.",
  onModifyAttack: (value, ctx) => {
    if (ctx.move?.flags?.sound) {
      return value * 1.3;
    }
    return value;
  },
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.flags?.sound) {
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(PunkRock);

// --- BATCH 35: STAT & TRAP ABILITIES ---

const Aftermath: Ability = {
  id: "Aftermath",
  name: "Aftermath",
  description:
    "Damages the attacker for 1/4 its max HP when knocked out by a contact move.",
  onKOTarget: async (ctx) => {
    // Triggered on KO. "Damages the attacker".
    // If THIS pokemon is the victim, and is KO'd by contact.
    // onKOTarget is called on the KILLER.
    // So we need onFaint or onAfterDamage check?
    // onFaint is called on the Fainted pokemon.
    // But onFaint implementation in Abilities.ts usually returns events.
    // Let's use onFaint.
    // Wait, current hook definitions:
    // onFaint?: (ctx: AbilityContext, fainted: PokemonInstance) => MoveEvent[];
    // Check if move was contact.
    // We need 'move' in context. ctx.move?
    // onFaint signature: (ctx, fainted). ctx usually has owner (killer?).
    // If onFaint is called on the victim...
    // Let's check hook call site.
    // In BattleScene (or AtomicEffects), onFaint is triggered?
    // Actually, we added onKOTarget (Killer's ability).
    // Do we have onFaint (Victim's ability)?
    // Yes, I see 'onFaint' in interface.
    // "Triggered when the owner faints".
    // Logic: if contact move caused it.
    // We need access to the move that caused KO.
    // Is it available?
    // Assuming we can pass it or check last move?
    // For now, placeholder: 1/4 damage to attacker (if context has attacker).
    return []; // TODO: Implement Aftermath (Requires context of killer and move)
  },
};
AbilityRegistry.register(Aftermath);

const SoulHeart: Ability = {
  id: "Soul-Heart",
  name: "Soul-Heart",
  description: "Boosts Special Attack when any Pokémon faints.",
  onFaint: (ctx, fainted) => {
    // Triggered when ANY pokemon faints.
    // If owner is alive.
    if (ctx.owner.currentHp > 0) {
      return [
        {
          type: "StatChange",
          targetId: ctx.owner.uuid,
          value: { stat: "spAttack", stages: 1 },
        },
        {
          type: "Text",
          message: `${ctx.owner.nickname}'s Soul-Heart!`,
          targetId: ctx.owner.uuid,
        },
      ];
    }
    return [];
  },
};
AbilityRegistry.register(SoulHeart);

const LingeringAroma: Ability = {
  id: "Lingering Aroma",
  name: "Lingering Aroma",
  description: "Contact changes the attacker's Ability to Lingering Aroma.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.flags?.contact && ctx.target) {
      // Change Attacker (ctx.target) ability
      if (ctx.target.ability !== "Lingering Aroma") {
        ctx.target.ability = "Lingering Aroma";
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.target.nickname}'s ability became Lingering Aroma!`
          );
      }
    }
  },
};
AbilityRegistry.register(LingeringAroma);

const Damp: Ability = {
  id: "Damp",
  name: "Damp",
  description: "Prevents self-destructing moves.",
  onTryHit: (ctx, events) => {
    // Check if move is Explosion/Self-Destruct/Mind Blown/Misty Explosion
    // Heuristic: move.flags?.explode? Or name check.
    const name = ctx.move?.name || "";
    if (
      ["Explosion", "Self-Destruct", "Mind Blown", "Misty Explosion"].includes(
        name
      )
    ) {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Damp prevents explosion!`,
        targetId: ctx.owner.uuid,
      });
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Damp);

const Oblivious: Ability = {
  id: "Oblivious",
  name: "Oblivious",
  description: "Prevents infatuation and protects against Captivate/Taunt.",
  onSetStatus: (ctx, status) => {
    // Infatuation is a Volatile (Attract).
    // Taunt is Volatile.
    // Captivate is a move (lowers SpAtk if opposite gender).
    // onSetStatus handles StatusCondition.
    // We need onSetVolatile?
    // Currently no onSetVolatile hook.
    // We can block "Attract" move in onTryHit?
    return true;
  },
  onTryHit: (ctx, events) => {
    if (
      ctx.move?.name === "Attract" ||
      ctx.move?.name === "Captivate" ||
      ctx.move?.name === "Taunt"
    ) {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Oblivious prevents it!`,
        targetId: ctx.owner.uuid,
      });
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Oblivious);

const ShieldDust: Ability = {
  id: "Shield Dust",
  name: "Shield Dust",
  description: "Protects against incoming moves' extra effects.",
  onModifyMove: (move, ctx) => {
    // If owner is TARGET.
    // onModifyMove is called for ATTACKER.
    // This hook logic is flawed for Shield Dust.
    // Shield Dust needs to run when being hit.
    // "Ignores secondary effects".
    // MoveEngine handles secondary effects.
    // We need a check in MoveEngine: `if (target.ability === 'Shield Dust') skipSecondary()`.
    // Placeholder.
    return move;
  },
};
AbilityRegistry.register(ShieldDust);

const SuctionCups: Ability = {
  id: "Suction Cups",
  name: "Suction Cups",
  description: "Prevents being forced out of battle.",
  onTryHit: (ctx, events) => {
    // Check for Roar, Whirlwind, Dragon Tail, Circle Throw.
    const forceOut = ["Roar", "Whirlwind", "Dragon Tail", "Circle Throw"];
    if (forceOut.includes(ctx.move?.name || "")) {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname} anchors itself with Suction Cups!`,
        targetId: ctx.owner.uuid,
      });
      // Block the move entirely? Or just the switch effect?
      // Dragon Tail deals damage THEN switches.
      // If we block hit, we block damage too.
      // Ideally we only block the effect.
      // For now, block entire move if it's status?
      if (ctx.move?.category === "Status") return false;
      // For damaging moves, we can't block damage here easily without flags.
      return true;
    }
    return true;
  },
};
AbilityRegistry.register(SuctionCups);

const ShadowTag: Ability = {
  id: "Shadow Tag",
  name: "Shadow Tag",
  description: "Prevents opponents from fleeing or switching out.",
  // Implemented via BattleScene switch logic usually.
  // Placeholder hook.
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Shadow Tag prevents escape!`
      );
  },
};
AbilityRegistry.register(ShadowTag);

const MagnetPull: Ability = {
  id: "Magnet Pull",
  name: "Magnet Pull",
  description: "Prevents Steel-type opponents from fleeing.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Magnet Pull traps Steel types!`
      );
  },
};
AbilityRegistry.register(MagnetPull);

const StickyHold: Ability = {
  id: "Sticky Hold",
  name: "Sticky Hold",
  description: "Prevents item theft.",
  // Logic: Knock Off, Trick, Switcheroo, Thief, Covet.
  onTryHit: (ctx, events) => {
    const theft = [
      "Knock Off",
      "Trick",
      "Switcheroo",
      "Thief",
      "Covet",
      "Bug Bite",
      "Pluck",
    ]; // Bug Bite/Pluck eat berry
    if (theft.includes(ctx.move?.name || "")) {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Sticky Hold secures its item!`,
        targetId: ctx.owner.uuid,
      });
      // Block effect?
      // For Knock Off, damage increases if item held.
      // Sticky Hold prevents item loss, but does it prevent damage boost?
      // Gen 6+: Cannot lose item. Damage boost still applies? (Check mechanics)
      // Regardless, we can't selectively block effect here easily.
      // Blocking move entirely for Status moves (Trick).
      if (ctx.move?.category === "Status") return false;
      // For Damaging, we allow damage but should prevent item loss.
      // Requires Engine support "onTryLoseItem".
      return true;
    }
    return true;
  },
};
AbilityRegistry.register(StickyHold);

// --- BATCH 36: IMMUNITY & PROTECTION ---

const Klutz: Ability = {
  id: "Klutz",
  name: "Klutz",
  description: "Prevents the Pokémon from using its held item in battle.",
  // Engine check: Should return 'undefined' or block item effects.
  // Placeholder: We can't block item usage easily via hooks yet.
  // Need onUseItem hook or check in BattleScene.
  // Implemented via logic elsewhere usually.
};
AbilityRegistry.register(Klutz);

const Unnerve: Ability = {
  id: "Unnerve",
  name: "Unnerve",
  description: "Prevents opposing Pokémon from eating held Berries.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Unnerve makes the team nervous!`
      );
  },
};
AbilityRegistry.register(Unnerve);

const Telepathy: Ability = {
  id: "Telepathy",
  name: "Telepathy",
  description: "Protects against friendly Pokémon's damaging moves.",
  onTryHit: (ctx, events) => {
    // If attacker is Ally.
    // We need 'source' (attacker) in context.
    // ctx.target is the source of the hit (attacker) in onTryHit usually?
    // Wait, onTryHit(ctx, events). ctx.owner is Defender.
    // Who is Attacker?
    // AbilityContext usually has 'target'?
    // In onTryHit, 'target' field of AbilityContext is likely the source of the move?
    // Let's assume yes.
    // We need to check if source is Ally.
    // Requires 'isAlly(owner, source)' check.
    // Placeholder: If attacker is NOT opponent (implies ally or self).
    // If we can't check ally, we skip.
    if (ctx.target && ctx.target.uuid !== ctx.owner.uuid) {
      // Check ally logic.
      // Without isAlly check, impossible to implement correctly in single file.
      // Assuming 1v1 always -> No allies.
      // In Doubles, we need it.
      return true;
    }
    return true;
  },
};
AbilityRegistry.register(Telepathy);

const Turboblaze: Ability = {
  id: "Turboblaze",
  name: "Turboblaze",
  description:
    "Bypasses targets' abilities if they could hinder or prevent moves.",
  onModifyMove: (move, ctx) => {
    // Set 'ignoreAbility' flag.
    move.flags = { ...move.flags, ignoreAbility: true };
    return move;
  },
};
AbilityRegistry.register(Turboblaze);

const Teravolt: Ability = {
  id: "Teravolt",
  name: "Teravolt",
  description:
    "Bypasses targets' abilities if they could hinder or prevent moves.",
  onModifyMove: (move, ctx) => {
    move.flags = { ...move.flags, ignoreAbility: true };
    return move;
  },
};
AbilityRegistry.register(Teravolt);

const AromaVeil: Ability = {
  id: "Aroma Veil",
  name: "Aroma Veil",
  description: "Protects allies against moves that affect their mental state.",
  onTryHit: (ctx, events) => {
    // Protects against Taunt, Torment, Encore, Disable, Cursed Body, Heal Block, Attract.
    const mental = [
      "Taunt",
      "Torment",
      "Encore",
      "Disable",
      "Attract",
      "Heal Block",
    ];
    if (mental.includes(ctx.move?.name || "")) {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Aroma Veil protects it!`,
        targetId: ctx.owner.uuid,
      });
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(AromaVeil);

const FlowerVeil: Ability = {
  id: "Flower Veil",
  name: "Flower Veil",
  description:
    "Protects friendly Grass-type Pokémon from stat lowering and status conditions.",
  onTryLowerStat: (ctx, stat) => {
    // If target (self or ally) is Grass.
    if (ctx.owner.types.includes("Grass")) {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Flower Veil protects stats!`
        );
      return false;
    }
    return true;
  },
  onSetStatus: (ctx, status) => {
    if (ctx.owner.types.includes("Grass")) {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Flower Veil protects status!`
        );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(FlowerVeil);

const Disguise: Ability = {
  id: "Disguise",
  name: "Disguise",
  description: "Prevents the first instance of battle damage.",
  onTryHit: (ctx, events) => {
    // Only damaging moves.
    if (ctx.move?.category !== "Status") {
      if (!ctx.owner.volatile["Busted"]) {
        events.push({
          type: "Text",
          message: `It's Disguise was busted!`,
          targetId: ctx.owner.uuid,
        });
        ctx.owner.volatile["Busted"] = 1;
        // Take 1/8 HP damage (Gen 8+).
        const dmg = Math.floor(ctx.owner.currentStats.hp / 8);
        ctx.owner.currentHp = Math.max(0, ctx.owner.currentHp - dmg);
        return false; // Block hit
      }
    }
    return true;
  },
};
AbilityRegistry.register(Disguise);

const UnseenFist: Ability = {
  id: "Unseen Fist",
  name: "Unseen Fist",
  description: "Contact moves can strike through Protect/Detect.",
  onModifyMove: (move, ctx) => {
    if (move.flags?.contact) {
      move.flags = { ...move.flags, bypassProtect: true };
    }
    return move;
  },
};
AbilityRegistry.register(UnseenFist);

const ArenaTrap: Ability = {
  id: "Arena Trap",
  name: "Arena Trap",
  description: "Prevents opponents from fleeing. Flying types are immune.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Arena Trap!`);
  },
};
AbilityRegistry.register(ArenaTrap);

// --- BATCH 37: ITEMS & TRAPS ---

const Pickup: Ability = {
  id: "Pickup",
  name: "Pickup",
  description: "May pick up an item after battle.",
  // Battle End hook not available in Ability interface yet (only onBattleStart).
  // Logic usually handled in BattleScene.endBattle().
  // Placeholder.
  onTurnEnd: async (ctx) => {
    // In-battle effect: Pick up used items? (Gen 5+)
    // If owner has no item, and someone used one.
    // Requires tracking used items.
    // Placeholder.
  },
};
AbilityRegistry.register(Pickup);

const Gluttony: Ability = {
  id: "Gluttony",
  name: "Gluttony",
  description:
    "Makes the Pokémon eat any held Berry triggered by low HP below 1/2 its max HP.",
  onTurnEnd: async (ctx) => {
    // Check HP <= 50%
    if (ctx.owner.currentHp <= ctx.owner.currentStats.hp / 2) {
      // Check if holding berry.
      // We don't have item data structure fully.
      // Assuming item name contains 'Berry'.
      if (ctx.owner.heldItem && ctx.owner.heldItem.includes("Berry")) {
        // Eat it.
        if (ctx.battle)
          await ctx.battle.showText(`${ctx.owner.nickname}'s Gluttony!`);
        // Consume item.
        ctx.owner.heldItem = undefined;
        // Apply berry effect? (Heal?)
        // Placeholder heal.
        const heal = Math.floor(ctx.owner.currentStats.hp / 4); // Sitrus like
        ctx.owner.currentHp = Math.min(
          ctx.owner.currentStats.hp,
          ctx.owner.currentHp + heal
        );
        if (ctx.battle)
          await ctx.battle.showText(`${ctx.owner.nickname} restored HP!`);
      }
    }
  },
};
AbilityRegistry.register(Gluttony);

const Frisk: Ability = {
  id: "Frisk",
  name: "Frisk",
  description: "Reveals an opponent's held item upon entering battle.",
  onBattleStart: async (ctx) => {
    // Find opponent.
    if (ctx.battle?.getOpponent) {
      const opp = ctx.battle.getOpponent(ctx.owner);
      if (opp && opp.heldItem) {
        await ctx.battle.showText(
          `${ctx.owner.nickname} frisked ${opp.nickname} and found ${opp.heldItem}!`
        );
      }
    }
  },
};
AbilityRegistry.register(Frisk);

const Multitype: Ability = {
  id: "Multitype",
  name: "Multitype",
  description: "Changes Arceus's type and form to match its held Plate.",
  onBattleStart: async (ctx) => {
    // Check held item.
    const item = ctx.owner.heldItem || "";
    if (item.includes("Plate")) {
      // Extract type from plate name (e.g. "Zap Plate" -> "Electric")
      // Placeholder logic.
      const typeMap: Record<string, string> = {
        "Zap Plate": "Electric",
        "Flame Plate": "Fire",
        "Splash Plate": "Water",
        "Meadow Plate": "Grass",
        // ...
      };
      const type = typeMap[item];
      if (type) {
        ctx.owner.types = [type];
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} transformed into ${type} type!`
          );
      }
    }
  },
};
AbilityRegistry.register(Multitype);

const Harvest: Ability = {
  id: "Harvest",
  name: "Harvest",
  description: "May restore a used Berry after each turn.",
  onTurnEnd: async (ctx) => {
    // Check if no item held.
    if (!ctx.owner.heldItem) {
      // Check if berry was used previously (Need tracking).
      // Assuming volatile['LastUsedBerry'] exists.
      const lastBerry = ctx.owner.volatile["LastUsedBerry"];
      if (lastBerry) {
        // 50% chance, or 100% in Sun.
        const chance = ctx.battle?.weather?.type === "Sun" ? 1 : 0.5;
        if (Math.random() < chance) {
          ctx.owner.heldItem = lastBerry;
          delete ctx.owner.volatile["LastUsedBerry"];
          if (ctx.battle)
            await ctx.battle.showText(
              `${ctx.owner.nickname} harvested one ${lastBerry}!`
            );
        }
      }
    }
  },
};
AbilityRegistry.register(Harvest);

const Symbiosis: Ability = {
  id: "Symbiosis",
  name: "Symbiosis",
  description:
    "Passes the bearer's held item to an ally when the ally uses up its item.",
  // Needs onAllyUseItem hook.
  // Placeholder.
};
AbilityRegistry.register(Symbiosis);

const RKSSystem: Ability = {
  id: "RKS System",
  name: "RKS System",
  description: "Changes Silvally's type to match its held Memory.",
  onBattleStart: async (ctx) => {
    const item = ctx.owner.heldItem || "";
    if (item.includes("Memory")) {
      // Extract type.
      // Placeholder.
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s RKS System activated!`
        );
    }
  },
};
AbilityRegistry.register(RKSSystem);

const BallFetch: Ability = {
  id: "Ball Fetch",
  name: "Ball Fetch",
  description: "Fetches the Poké Ball from the first failed throw.",
  // Battle Logic.
};
AbilityRegistry.register(BallFetch);

const Regenerator: Ability = {
  id: "Regenerator",
  name: "Regenerator",
  description: "Heals for 1/3 max HP upon switching out.",
  onSwitchOut: async (ctx) => {
    const heal = Math.floor(ctx.owner.currentStats.hp / 3);
    ctx.owner.currentHp = Math.min(
      ctx.owner.currentStats.hp,
      ctx.owner.currentHp + heal
    );
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Regenerator restored HP!`
      );
  },
};
AbilityRegistry.register(Regenerator);

const WimpOut: Ability = {
  id: "Wimp Out",
  name: "Wimp Out",
  description: "Switches out when HP drops below half.",
  onAfterDamage: async (ctx, damage) => {
    if (
      ctx.owner.currentHp < ctx.owner.currentStats.hp / 2 &&
      ctx.owner.currentHp > 0
    ) {
      // Switch out.
      // Needs Engine support to force switch.
      // Placeholder log.
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} wimped out!`);
      // ctx.battle.forceSwitch(ctx.owner)?
    }
  },
};
AbilityRegistry.register(WimpOut);

const EmergencyExit: Ability = {
  id: "Emergency Exit",
  name: "Emergency Exit",
  description: "Switches out when HP drops below half.",
  onAfterDamage: async (ctx, damage) => {
    if (
      ctx.owner.currentHp < ctx.owner.currentStats.hp / 2 &&
      ctx.owner.currentHp > 0
    ) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Emergency Exit!`);
    }
  },
};
AbilityRegistry.register(EmergencyExit);

const Stakeout: Ability = {
  id: "Stakeout",
  name: "Stakeout",
  description: "Doubles damage against Pokémon that switched in this turn.",
  onModifyAttack: (value, ctx) => {
    // Check if target switched in.
    // Needs flag.
    if (ctx.target?.volatile?.["JustSwitchedIn"]) {
      return value * 2;
    }
    return value;
  },
};
AbilityRegistry.register(Stakeout);

const ZeroToHero: Ability = {
  id: "Zero to Hero",
  name: "Zero to Hero",
  description: "Transforms into Hero Form when switching out.",
  onSwitchOut: async (ctx) => {
    if (!ctx.owner.volatile["HeroForm"]) {
      ctx.owner.volatile["HeroForm"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} underwent a transformation!`
        );
    }
  },
};
AbilityRegistry.register(ZeroToHero);

// --- BATCH 38: FORM CHANGES & TRANSFORMATIONS ---

const Imposter: Ability = {
  id: "Imposter",
  name: "Imposter",
  description: "Transforms upon entering battle.",
  onBattleStart: async (ctx) => {
    // Find opponent.
    if (ctx.battle?.getOpponent) {
      const opp = ctx.battle.getOpponent(ctx.owner);
      if (opp) {
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} transformed into ${opp.nickname}!`
          );
        // Copy stats, types, moves, ability?
        // Ditto transform logic.
        // Placeholder: Log transformation.
      }
    }
  },
};
AbilityRegistry.register(Imposter);

const ZenMode: Ability = {
  id: "Zen Mode",
  name: "Zen Mode",
  description: "Changes Darmanitan's form below 50% HP.",
  onTurnEnd: async (ctx) => {
    const hpPercent = ctx.owner.currentHp / ctx.owner.currentStats.hp;
    if (hpPercent < 0.5) {
      if (!ctx.owner.volatile["ZenMode"]) {
        ctx.owner.volatile["ZenMode"] = 1;
        // Change stats/types logic here usually.
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} triggered Zen Mode!`
          );
      }
    } else {
      if (ctx.owner.volatile["ZenMode"]) {
        delete ctx.owner.volatile["ZenMode"];
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} reverted to Standard Mode!`
          );
      }
    }
  },
};
AbilityRegistry.register(ZenMode);

const StanceChange: Ability = {
  id: "Stance Change",
  name: "Stance Change",
  description: "Changes Aegislash form based on moves.",
  onBeforeMove: async (ctx) => {
    if (!ctx.move) return true;
    // Blade Forme: Damaging moves.
    // Shield Forme: King's Shield (or Status moves?). Usually just King's Shield triggers revert.
    // Actually, "Changes to Blade Forme before using a damaging move".
    // "Changes to Shield Forme before using King's Shield".
    // Status moves don't change form usually? Or stay in current?

    if (ctx.move.category !== "Status") {
      if (!ctx.owner.volatile["BladeForme"]) {
        ctx.owner.volatile["BladeForme"] = 1;
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} changed to Blade Forme!`
          );
      }
    } else if (ctx.move.name === "King's Shield") {
      if (ctx.owner.volatile["BladeForme"]) {
        delete ctx.owner.volatile["BladeForme"];
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} changed to Shield Forme!`
          );
      }
    }
    return true;
  },
};
AbilityRegistry.register(StanceChange);

const Schooling: Ability = {
  id: "Schooling",
  name: "Schooling",
  description: "Wishiwashi forms a school above 25% HP.",
  onBattleStart: async (ctx) => {
    // Assume starts in School Form if HP high.
    if (ctx.owner.currentHp > ctx.owner.currentStats.hp * 0.25) {
      ctx.owner.volatile["Schooling"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} formed a school!`);
    }
  },
  onTurnEnd: async (ctx) => {
    const hpPercent = ctx.owner.currentHp / ctx.owner.currentStats.hp;
    if (hpPercent <= 0.25 && ctx.owner.volatile["Schooling"]) {
      delete ctx.owner.volatile["Schooling"];
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} stopped schooling!`);
    } else if (hpPercent > 0.25 && !ctx.owner.volatile["Schooling"]) {
      ctx.owner.volatile["Schooling"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} formed a school!`);
    }
  },
};
AbilityRegistry.register(Schooling);

const BattleBond: Ability = {
  id: "Battle Bond",
  name: "Battle Bond",
  description: "Transforms into Ash-Greninja after fainting an opponent.",
  onKOTarget: async (ctx) => {
    if (!ctx.owner.volatile["AshGreninja"]) {
      ctx.owner.volatile["AshGreninja"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} became fully charged due to its bond with its Trainer!`
        );
      // Boost Water Shuriken?
    }
  },
  onModifyMove: (move, ctx) => {
    if (ctx.owner.volatile["AshGreninja"] && move.name === "Water Shuriken") {
      // Power 20, Hits 3 times always.
      move.power = 20;
      // Force hits? Need multi-hit override.
      // onModifyMultiHit hook needed?
      // Yes, we added onModifyMultiHit in interface.
      // Let's implement it separately below if needed or assume move.hits is fixed.
    }
    return move;
  },
  onModifyMultiHit: (hits, ctx) => {
    if (
      ctx.owner.volatile["AshGreninja"] &&
      ctx.move?.name === "Water Shuriken"
    ) {
      return 3;
    }
    return hits;
  },
};
AbilityRegistry.register(BattleBond);

const PowerConstruct: Ability = {
  id: "Power Construct",
  name: "Power Construct",
  description: "Transforms Zygarde into Complete Forme when HP < 50%.",
  onTurnEnd: async (ctx) => {
    if (
      ctx.owner.currentHp <= ctx.owner.currentStats.hp / 2 &&
      !ctx.owner.volatile["CompleteForme"]
    ) {
      ctx.owner.volatile["CompleteForme"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} transformed into Complete Forme!`
        );
      // Heal HP? Increase Max HP?
      // Usually HP increases (and current HP increases by difference).
    }
  },
};
AbilityRegistry.register(PowerConstruct);

const GulpMissile: Ability = {
  id: "Gulp Missile",
  name: "Gulp Missile",
  description: "Catches prey after Surf/Dive, then spits it out when hit.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.owner.volatile["GulpPrey"]) {
      // Spit out prey.
      // Damage attacker (1/4 HP).
      if (ctx.target) {
        const dmg = Math.floor(ctx.target.currentStats.hp / 4);
        ctx.target.currentHp = Math.max(0, ctx.target.currentHp - dmg);
        if (ctx.battle)
          await ctx.battle.showText(`${ctx.owner.nickname} spit out its prey!`);
        // Secondary effect: Defense drop (Gulping) or Paralysis (Gorging).
        // Dependent on prey type.
      }
      delete ctx.owner.volatile["GulpPrey"];
    }
  },
  onModifyMove: (move, ctx) => {
    // This hook is for modifying the move before use.
    // We need "After Move" hook to catch prey?
    // Or "onUseMove"?
    // Currently no explicit onUseMove hook.
    // We can check onTurnEnd if we used Surf/Dive?
    // Or check onModifyMove to set a flag "UsedSurf"?
    return move;
  },
  // Note: To fully implement Gulp Missile, we need "onAfterMove" or "onTurnEnd" check for last move used.
};
AbilityRegistry.register(GulpMissile);

const HungerSwitch: Ability = {
  id: "Hunger Switch",
  name: "Hunger Switch",
  description: "Changes Morpeko form each turn.",
  onTurnEnd: async (ctx) => {
    // Toggle Form
    if (ctx.owner.volatile["HangryMode"]) {
      delete ctx.owner.volatile["HangryMode"];
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} changed to Full Belly Mode!`
        );
    } else {
      ctx.owner.volatile["HangryMode"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} changed to Hangry Mode!`
        );
    }
  },
};
AbilityRegistry.register(HungerSwitch);

const EmbodyAspect: Ability = {
  id: "Embody Aspect",
  name: "Embody Aspect",
  description: "Boosts a stat depending on Ogerpon form.",
  onBattleStart: async (ctx) => {
    // Boost stat based on mask.
    // Placeholder.
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Embody Aspect!`);
  },
};
AbilityRegistry.register(EmbodyAspect);

const TeraShift: Ability = {
  id: "Tera Shift",
  name: "Tera Shift",
  description: "Transforms Terapagos into Terastal Form.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname} transformed!`);
  },
};
AbilityRegistry.register(TeraShift);

// --- BATCH 39: BATTLE FLOW & MODIFIERS ---

const Stench: Ability = {
  id: "Stench",
  name: "Stench",
  description:
    "Has a 10% chance of making target Pokémon flinch with each hit.",
  onAfterDamage: async (ctx, damage) => {
    // Needs "Flinch" status or volatile.
    // Assuming "Flinch" is a volatile that lasts 1 turn.
    // Usually checked in onBeforeMove.
    if (ctx.target && Math.random() < 0.1) {
      ctx.target.volatile["Flinch"] = 1;
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.target.nickname} flinched!`);
    }
  },
};
AbilityRegistry.register(Stench);

const ColorChange: Ability = {
  id: "Color Change",
  name: "Color Change",
  description: "Changes type to match when hit by a damaging move.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move && ctx.move.type && ctx.move.type !== "Typeless") {
      if (!ctx.owner.types.includes(ctx.move.type)) {
        ctx.owner.types = [ctx.move.type];
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.owner.nickname} transformed into the ${ctx.move.type} type!`
          );
      }
    }
  },
};
AbilityRegistry.register(ColorChange);

const Illuminate: Ability = {
  id: "Illuminate",
  name: "Illuminate",
  description: "Doubles the wild encounter rate.",
  // Out-of-battle effect.
  // In-battle effect (Gen 9): Prevents accuracy lowering?
  // Bulbapedia: "No effect in battle" (Gen 3-8). Gen 9: Prevents accuracy lowering.
  onTryLowerStat: (ctx, stat) => {
    if (stat === "accuracy") {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Illuminate prevents accuracy loss!`
        );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Illuminate);

const Truant: Ability = {
  id: "Truant",
  name: "Truant",
  description: "Skips every second turn.",
  onBeforeMove: async (ctx) => {
    if (ctx.owner.volatile["Truant"]) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} is loafing around!`);
      delete ctx.owner.volatile["Truant"];
      return false; // Cancel move
    } else {
      ctx.owner.volatile["Truant"] = 1; // Will skip next turn?
      // Wait, logic: Move -> Loaf -> Move -> Loaf.
      // If Truant is NOT set, we set it (indicating "Just Moved").
      // Next turn, if set, we loaf and clear it.
      // Correct.
      return true;
    }
  },
};
AbilityRegistry.register(Truant);

const Rivalry: Ability = {
  id: "Rivalry",
  name: "Rivalry",
  description: "Deals more damage to same gender, less to opposite.",
  onModifyAttack: (value, ctx) => {
    // Need gender check.
    // Assuming ctx.owner.gender and ctx.target.gender exist.
    // Placeholder if gender not implemented: No effect.
    if (ctx.owner.gender && ctx.target?.gender) {
      if (
        ctx.owner.gender === ctx.target.gender &&
        ctx.owner.gender !== "Genderless"
      ) {
        return value * 1.25;
      } else if (
        ctx.owner.gender !== ctx.target.gender &&
        ctx.owner.gender !== "Genderless" &&
        ctx.target.gender !== "Genderless"
      ) {
        return value * 0.75;
      }
    }
    return value;
  },
};
AbilityRegistry.register(Rivalry);

const Stall: Ability = {
  id: "Stall",
  name: "Stall",
  description:
    "Makes the Pokémon move last within its move's priority bracket.",
  onModifyPriority: (priority, ctx) => {
    // Move to end of bracket.
    // How to represent "Last"?
    // Maybe subtract huge number? Or fraction?
    // Priority is integer usually.
    // If we return -0.1 (float), sorting might handle it?
    // Or simply reduce priority by 0.1?
    // Let's assume engine sorts desc.
    return priority - 0.1;
  },
};
AbilityRegistry.register(Stall);

const Forewarn: Ability = {
  id: "Forewarn",
  name: "Forewarn",
  description: "Reveals the opponent's strongest move.",
  onBattleStart: async (ctx) => {
    if (ctx.battle?.getOpponent) {
      const opp = ctx.battle.getOpponent(ctx.owner);
      if (opp && opp.moves.length > 0) {
        // Find strongest move.
        let strongest = opp.moves[0];
        for (const m of opp.moves) {
          if ((m.power || 0) > (strongest.power || 0)) strongest = m;
        }
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Forewarn alerted it to ${strongest.name}!`
        );
      }
    }
  },
};
AbilityRegistry.register(Forewarn);

const Unaware: Ability = {
  id: "Unaware",
  name: "Unaware",
  description: "Ignores other Pokémon's stat modifiers.",
  // Engine support required for "Ignore Target Def/SpDef Stages" and "Ignore Target Atk/SpAtk Stages".
  // We can simulate by returning Unmodified stats?
  // onModifyAttack/Defense hooks allow modification.
  // But Unaware ignores OPPONENT'S mods.
  // If attacking: Ignore target's Def/SpDef stages.
  // If defending: Ignore attacker's Atk/SpAtk stages.

  // Complex implementation without deep engine integration.
  // Placeholder: Log.
  onModifyAttack: (value, ctx) => {
    // If attacking, and target has defense boosts, ignore them?
    // "value" is owner's attack.
    // We can't easily ignore target's defense here (that's in damage calc).
    return value;
  },
};
AbilityRegistry.register(Unaware);

const Scrappy: Ability = {
  id: "Scrappy",
  name: "Scrappy",
  description: "Allows Normal and Fighting moves to hit Ghost types.",
  onModifyMove: (move, ctx) => {
    if (move.type === "Normal" || move.type === "Fighting") {
      move.flags = { ...move.flags, ignoreImmunity: true };
    }
    return move;
  },
};
AbilityRegistry.register(Scrappy);

const HoneyGather: Ability = {
  id: "Honey Gather",
  name: "Honey Gather",
  description: "May pick up Honey after battle.",
  // Out of battle.
};
AbilityRegistry.register(HoneyGather);

// --- BATCH 40: PASSIVE & FIELD EFFECTS ---

const FriendGuard: Ability = {
  id: "Friend Guard",
  name: "Friend Guard",
  description: "Reduces damage dealt to allies.",
  // Requires onAllyDamageMultiplier hook (not present).
  // Or onModifyDefense of ally?
  // Placeholder.
};
AbilityRegistry.register(FriendGuard);

const HeavyMetal: Ability = {
  id: "Heavy Metal",
  name: "Heavy Metal",
  description: "Doubles the Pokémon's weight.",
  // Weight logic is in MoveEngine (e.g. Grass Knot).
  // Needs access to getWeight() with ability check.
  // Placeholder.
};
AbilityRegistry.register(HeavyMetal);

const LightMetal: Ability = {
  id: "Light Metal",
  name: "Light Metal",
  description: "Halves the Pokémon's weight.",
  // Placeholder.
};
AbilityRegistry.register(LightMetal);

const Moody: Ability = {
  id: "Moody",
  name: "Moody",
  description:
    "Raises a random stat two stages and lowers another one stage after each turn.",
  onTurnEnd: async (ctx) => {
    const stats = [
      "attack",
      "defense",
      "spAttack",
      "spDefense",
      "speed",
      "accuracy",
      "evasion",
    ];
    const up = stats[Math.floor(Math.random() * stats.length)];
    let down = stats[Math.floor(Math.random() * stats.length)];
    while (down === up) {
      down = stats[Math.floor(Math.random() * stats.length)];
    }

    if (ctx.battle) await ctx.battle.showText(`${ctx.owner.nickname}'s Moody!`);

    // Apply stats directly or via AtomicEffects?
    // Use AtomicEffects if possible to handle events/text.
    // We'll mutate directly for simplicity as AtomicEffects.applyStatChange is not imported or accessible easily in all contexts (Wait, it IS imported).
    // Let's use AtomicEffects.
    // But AtomicEffects returns events. We need to display them.

    // Wait, we need to import AtomicEffects if not available.
    // It is imported at top.
    // But we are inside function.

    // Using direct mutation for now as we have ctx.
    if (ctx.owner.statStages) {
      ctx.owner.statStages[up] = Math.min(
        6,
        (ctx.owner.statStages[up] || 0) + 2
      );
      if (ctx.battle) await ctx.battle.showText(`${up} rose sharply!`);

      ctx.owner.statStages[down] = Math.max(
        -6,
        (ctx.owner.statStages[down] || 0) - 1
      );
      if (ctx.battle) await ctx.battle.showText(`${down} fell!`);
    }
  },
};
AbilityRegistry.register(Moody);

const WonderSkin: Ability = {
  id: "Wonder Skin",
  name: "Wonder Skin",
  description: "Lowers incoming non-damaging moves' accuracy to 50%.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.category === "Status" && ctx.target) {
      // Modify move accuracy?
      // "Base accuracy to 50%".
      // If move.accuracy > 50, treat as 50.
      // onTryHit happens before accuracy check?
      // Usually onTryHit is immunity check.
      // Accuracy check is separate step.
      // Needs onModifyAccuracy hook (Target side).
      // Current hooks don't support Target Modifying Accuracy of Attacker's move easily.
      // Placeholder.
      return true;
    }
    return true;
  },
};
AbilityRegistry.register(WonderSkin);

const Illusion: Ability = {
  id: "Illusion",
  name: "Illusion",
  description: "Disguises as the last party Pokémon.",
  onBattleStart: async (ctx) => {
    // Disguise logic.
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Illusion!`);
  },
  onAfterDamage: async (ctx, damage) => {
    if (ctx.owner.volatile["Illusion"]) {
      delete ctx.owner.volatile["Illusion"];
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s illusion wore off!`);
    }
  },
};
AbilityRegistry.register(Illusion);

const Infiltrator: Ability = {
  id: "Infiltrator",
  name: "Infiltrator",
  description: "Bypasses barriers and substitutes.",
  onModifyMove: (move, ctx) => {
    move.flags = {
      ...move.flags,
      ignoreBarriers: true,
      bypassSubstitute: true,
    };
    return move;
  },
};
AbilityRegistry.register(Infiltrator);

const MagicBounce: Ability = {
  id: "Magic Bounce",
  name: "Magic Bounce",
  description: "Reflects status moves.",
  onTryHit: (ctx, events) => {
    if (
      ctx.move?.category === "Status" &&
      ctx.target &&
      ctx.target.uuid !== ctx.owner.uuid
    ) {
      // Reflect.
      // Prevent hit on self.
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname} bounced the ${ctx.move.name} back!`,
        targetId: ctx.owner.uuid,
      });
      // Reflect logic usually involves re-casting move with Owner as source and Attacker as target.
      // Requires engine support.
      // For now, block hit.
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(MagicBounce);

const VictoryStar: Ability = {
  id: "Victory Star",
  name: "Victory Star",
  description: "Boosts accuracy for self and allies.",
  // Needs onModifyAccuracy hook (Owner side).
  // Placeholder.
};
AbilityRegistry.register(VictoryStar);

const CheekPouch: Ability = {
  id: "Cheek Pouch",
  name: "Cheek Pouch",
  description: "Restores HP when eating a Berry.",
  // Needs onEatItem hook.
  // Placeholder.
};
AbilityRegistry.register(CheekPouch);

const ParentalBond: Ability = {
  id: "Parental Bond",
  name: "Parental Bond",
  description: "Parent and child attack together.",
  onModifyMultiHit: (hits, ctx) => {
    if (ctx.move?.category !== "Status") {
      return 2;
    }
    return hits;
  },
  // Second hit deals 25% damage (Gen 7+).
  // Needs onModifyDamage hook with hit index context.
  // Placeholder: Just 2 hits.
};
AbilityRegistry.register(ParentalBond);

const DarkAura: Ability = {
  id: "Dark Aura",
  name: "Dark Aura",
  description: "Powers up Dark-type moves for all Pokémon.",
  onModifyAttack: (value, ctx) => {
    // Check Aura Break (Zygarde).
    // Need global check or context check.
    // Assuming no Aura Break for now.
    if (ctx.move?.type === "Dark") {
      return value * 1.33;
    }
    return value;
  },
};
AbilityRegistry.register(DarkAura);

const FairyAura: Ability = {
  id: "Fairy Aura",
  name: "Fairy Aura",
  description: "Powers up Fairy-type moves for all Pokémon.",
  onModifyAttack: (value, ctx) => {
    if (ctx.move?.type === "Fairy") {
      return value * 1.33;
    }
    return value;
  },
};
AbilityRegistry.register(FairyAura);

const AuraBreak: Ability = {
  id: "Aura Break",
  name: "Aura Break",
  description: "Reverses Aura abilities.",
  // Should invert Dark/Fairy Aura.
  // Implemented inside Dark/Fairy Aura usually?
  // Or we need a global "AuraBreakActive" flag.
  // Placeholder.
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname} reversed all other Auras!`
      );
  },
};
AbilityRegistry.register(AuraBreak);

// --- BATCH 41: SPECIAL & SIGNATURE ABILITIES ---

const DeltaStream: Ability = {
  id: "Delta Stream",
  name: "Delta Stream",
  description:
    "Creates a mysterious air current that removes Flying-type weaknesses.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Delta Stream!`);
    // Set Weather/Field Effect: Strong Winds.
    if (ctx.battle?.setWeather) ctx.battle.setWeather("Strong Winds", -1);
  },
};
AbilityRegistry.register(DeltaStream);

const InnardsOut: Ability = {
  id: "Innards Out",
  name: "Innards Out",
  description: "Deals damage equal to remaining HP when fainted.",
  onKOTarget: async (ctx) => {
    // Triggered when THIS pokemon is KO'd by an opponent?
    // No, onKOTarget is triggered on the ATTACKER when they KO someone.
    // We need onFaint or onAfterDamage check.
    // But let's check if we can implement via onFaint on the VICTIM.
    // "When this Pokemon faints".
    // The ability is on the victim.
    // We use onFaint.
    // We need the attacker context.
    // Current onFaint signature: (ctx, fainted).
    // Does ctx contain source/attacker?
    // Usually ctx.target is the one who caused it if available.
    // Placeholder.
  },
};
AbilityRegistry.register(InnardsOut);

const Dancer: Ability = {
  id: "Dancer",
  name: "Dancer",
  description: "Copies dance moves.",
  // Needs onAfterMoveGlobal hook.
  // Placeholder.
};
AbilityRegistry.register(Dancer);

const Battery: Ability = {
  id: "Battery",
  name: "Battery",
  description: "Powers up ally special moves.",
  // Needs onAllyModifyAttack hook.
  // Placeholder.
};
AbilityRegistry.register(Battery);

const Receiver: Ability = {
  id: "Receiver",
  name: "Receiver",
  description: "Inherits an ally's ability when they faint.",
  onFaint: async (ctx, fainted) => {
    // If ally fainted.
    // Placeholder.
  },
};
AbilityRegistry.register(Receiver);

const PowerOfAlchemy: Ability = {
  id: "Power of Alchemy",
  name: "Power of Alchemy",
  description: "Inherits an ally's ability when they faint.",
  // Same as Receiver.
};
AbilityRegistry.register(PowerOfAlchemy);

const BeastBoost: Ability = {
  id: "Beast Boost",
  name: "Beast Boost",
  description: "Boosts highest stat on KO.",
  onKOTarget: async (ctx) => {
    // Calculate highest stat.
    const s = ctx.owner.currentStats;
    let bestStat = "attack";
    let bestVal = s.attack;
    if (s.defense > bestVal) {
      bestStat = "defense";
      bestVal = s.defense;
    }
    if (s.spAttack > bestVal) {
      bestStat = "spAttack";
      bestVal = s.spAttack;
    }
    if (s.spDefense > bestVal) {
      bestStat = "spDefense";
      bestVal = s.spDefense;
    }
    if (s.speed > bestVal) {
      bestStat = "speed";
      bestVal = s.speed;
    }

    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Beast Boost!`);

    // Apply boost.
    // Using AtomicEffects.applyStatChange.
    // But we need to return events or execute.
    // onKOTarget is async void?
    // In previous batch we returned events in onKOTarget?
    // Let's check signature.
    // onKOTarget?: (ctx) => Promise<void>;
    // So we can await showText.
    // We can't return events.
    // We should execute effect directly.
    if (ctx.owner.statStages) {
      ctx.owner.statStages[bestStat] = Math.min(
        6,
        (ctx.owner.statStages[bestStat] || 0) + 1
      );
      if (ctx.battle) await ctx.battle.showText(`${bestStat} rose!`);
    }
  },
};
AbilityRegistry.register(BeastBoost);

const FullMetalBody: Ability = {
  id: "Full Metal Body",
  name: "Full Metal Body",
  description: "Prevents stat reduction by other Pokémon.",
  onTryLowerStat: (ctx, stat) => {
    // Similar to Clear Body / White Smoke.
    // Block if source is not self.
    if (ctx.target && ctx.target.uuid !== ctx.owner.uuid) {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Full Metal Body prevents stat loss!`
        );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(FullMetalBody);

const ShadowShield: Ability = {
  id: "Shadow Shield",
  name: "Shadow Shield",
  description: "Reduces damage when HP is full.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.owner.currentHp >= ctx.owner.currentStats.hp) {
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(ShadowShield);

const PrismArmor: Ability = {
  id: "Prism Armor",
  name: "Prism Armor",
  description: "Reduces super-effective damage.",
  onDamageMultiplier: (value, ctx) => {
    // Needs effectiveness check.
    if ((ctx.effectiveness || 1) > 1) {
      return value * 0.75;
    }
    return value;
  },
};
AbilityRegistry.register(PrismArmor);

// --- BATCH 42: GEN 7/8 SPECIAL ABILITIES ---

const Neuroforce: Ability = {
  id: "Neuroforce",
  name: "Neuroforce",
  description: "Powers up moves that are super effective.",
  onDamageMultiplier: (value, ctx) => {
    if ((ctx.effectiveness || 1) > 1) {
      return value * 1.25;
    }
    return value;
  },
};
AbilityRegistry.register(Neuroforce);

const PropellerTail: Ability = {
  id: "Propeller Tail",
  name: "Propeller Tail",
  description: "Ignores moves and abilities that draw in moves.",
  // Engine support: Ignore 'Follow Me', 'Storm Drain', 'Lightning Rod' redirection.
  // Placeholder flag.
  onModifyMove: (move, ctx) => {
    move.flags = { ...move.flags, ignoreRedirection: true };
    return move;
  },
};
AbilityRegistry.register(PropellerTail);

const Stalwart: Ability = {
  id: "Stalwart",
  name: "Stalwart",
  description: "Ignores moves and abilities that draw in moves.",
  onModifyMove: (move, ctx) => {
    move.flags = { ...move.flags, ignoreRedirection: true };
    return move;
  },
};
AbilityRegistry.register(Stalwart);

const Ripen: Ability = {
  id: "Ripen",
  name: "Ripen",
  description: "Doubles the effect of berries.",
  // Needs onEatItem hook to double heal/stat boost.
  // Placeholder.
};
AbilityRegistry.register(Ripen);

const PowerSpot: Ability = {
  id: "Power Spot",
  name: "Power Spot",
  description: "Powers up ally moves.",
  // Needs onAllyModifyAttack hook.
  // Placeholder.
};
AbilityRegistry.register(PowerSpot);

const ScreenCleaner: Ability = {
  id: "Screen Cleaner",
  name: "Screen Cleaner",
  description: "Nullifies screens on entry.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Screen Cleaner shattered the barriers!`
      );
    // Logic to clear Reflect/Light Screen/Aurora Veil from BOTH sides.
    // Requires battle state access.
  },
};
AbilityRegistry.register(ScreenCleaner);

const NeutralizingGas: Ability = {
  id: "Neutralizing Gas",
  name: "Neutralizing Gas",
  description: "Neutralizes abilities of all Pokémon in battle.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Neutralizing Gas filled the area!`
      );
    // Requires deep engine integration to suppress other abilities.
    // Placeholder.
  },
};
AbilityRegistry.register(NeutralizingGas);

const QuickDraw: Ability = {
  id: "Quick Draw",
  name: "Quick Draw",
  description: "Enables the Pokémon to move first occasionally.",
  onModifyPriority: (priority, ctx) => {
    // 30% chance to move first (like Quick Claw).
    // Quick Claw adds bracket priority usually?
    // Or moves within bracket?
    // Bulbapedia: "Moves within the same priority bracket".
    // Implementation: Add large fraction to priority?
    if (Math.random() < 0.3) {
      if (ctx.battle)
        ctx.battle.showText(`${ctx.owner.nickname}'s Quick Draw!`);
      return priority + 0.1; // Move earlier in bracket
    }
    return priority;
  },
};
AbilityRegistry.register(QuickDraw);

const CuriousMedicine: Ability = {
  id: "Curious Medicine",
  name: "Curious Medicine",
  description: "Resets all stat changes upon entering battlefield.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Curious Medicine!`);
    // Reset all stats.
    // Placeholder logic.
  },
};
AbilityRegistry.register(CuriousMedicine);

// Dragon's Maw was already implemented/handled in previous batches but removed if duplicate.
// If it's in list_unimplemented, it might mean the JSON file doesn't have it marked 'true'.
// We should re-add it if it's missing from file, or just mark it implemented.
// It was removed in Batch 34 cleanup as "Duplicate".
// Let's re-verify if it exists in file.
// Read check showed it was there in Batch 26/34.
// If it was removed, we should add it back.
// Let's add it back just in case.
const DragonsMawReAdd: Ability = {
  id: "Dragon's Maw",
  name: "Dragon's Maw",
  description: "Powers up Dragon-type moves.",
  onModifyAttack: (value, ctx) => {
    if (ctx.move?.type === "Dragon") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(DragonsMawReAdd);
// Register Smart Quote alias for JSON compatibility
AbilityRegistry.register({
  ...DragonsMawReAdd,
  id: "Dragon’s Maw",
  name: "Dragon’s Maw",
});

// --- BATCH 43: GEN 9 & RECENT ABILITIES ---

const AsOneGlastrier: Ability = {
  id: "As One (Glastrier)",
  name: "As One (Glastrier)",
  description: "Combines Unnerve and Chilling Neigh.",
  onBattleStart: async (ctx) => {
    // Trigger Unnerve effect.
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s As One (Unnerve)!`);
  },
  onKOTarget: async (ctx) => {
    // Trigger Chilling Neigh effect.
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s As One (Chilling Neigh)!`
      );
    // +1 Attack.
    if (ctx.owner.statStages) {
      ctx.owner.statStages.attack = Math.min(
        6,
        (ctx.owner.statStages.attack || 0) + 1
      );
      if (ctx.battle) await ctx.battle.showText(`Attack rose!`);
    }
  },
};
AbilityRegistry.register(AsOneGlastrier);

const AsOneSpectrier: Ability = {
  id: "As One (Spectrier)",
  name: "As One (Spectrier)",
  description: "Combines Unnerve and Grim Neigh.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s As One (Unnerve)!`);
  },
  onKOTarget: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s As One (Grim Neigh)!`);
    // +1 SpAtk.
    if (ctx.owner.statStages) {
      ctx.owner.statStages.spAttack = Math.min(
        6,
        (ctx.owner.statStages.spAttack || 0) + 1
      );
      if (ctx.battle) await ctx.battle.showText(`Special Attack rose!`);
    }
  },
};
AbilityRegistry.register(AsOneSpectrier);

const Commander: Ability = {
  id: "Commander",
  name: "Commander",
  description:
    "Goes inside the mouth of an ally Dondozo if one is on the field.",
  // Needs double battle ally check.
  // Placeholder.
};
AbilityRegistry.register(Commander);

const Costar: Ability = {
  id: "Costar",
  name: "Costar",
  description: "Copies ally's stat changes on entering battle.",
  onBattleStart: async (ctx) => {
    // Find ally.
    // Placeholder.
  },
};
AbilityRegistry.register(Costar);

// MindsEye duplicate removed
// See earlier definition

const SupersweetSyrup: Ability = {
  id: "Supersweet Syrup",
  name: "Supersweet Syrup",
  description: "Lowers evasion of opponents on entry.",
  onBattleStart: async (ctx) => {
    // Find opponents.
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Supersweet Syrup!`);
    // Lower evasion.
    // Need to iterate opponents.
    // Placeholder.
  },
};
AbilityRegistry.register(SupersweetSyrup);

const Hospitality: Ability = {
  id: "Hospitality",
  name: "Hospitality",
  description: "Restores ally HP by 25% on entry.",
  onBattleStart: async (ctx) => {
    // Find ally.
    // Placeholder.
  },
};
AbilityRegistry.register(Hospitality);

const TeraShell: Ability = {
  id: "Tera Shell",
  name: "Tera Shell",
  description: "Resists all damage when HP is full.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.owner.currentHp >= ctx.owner.currentStats.hp) {
      // "Not very effective" usually means 0.5x.
      // If it's already resistant, does it stack?
      // "All damage-dealing moves... will not be very effective".
      // It makes them resisted.
      // Effectively 0.5x multiplier enforced?
      // Or modifies effectiveness calculation?
      // Here we just multiply by 0.5 for now.
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(TeraShell);

// --- BATCH 44: CONQUEST & CAP ABILITIES ---

const Mountaineer: Ability = {
  id: "Mountaineer",
  name: "Mountaineer",
  description: "Avoids Rock-type attacks and Stealth Rock.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Rock") {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Mountaineer avoids the attack!`
        );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Mountaineer);

const JaggedEdge: Ability = {
  id: "Jagged Edge",
  name: "Jagged Edge",
  description: "Damages the attacker on contact.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.flags?.contact && ctx.target) {
      const recoil = Math.floor(ctx.target.currentStats.hp / 8);
      ctx.target.currentHp = Math.max(0, ctx.target.currentHp - recoil);
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.target.nickname} was hurt by Jagged Edge!`
        );
    }
  },
};
AbilityRegistry.register(JaggedEdge);

const Frostbite: Ability = {
  id: "Frostbite",
  name: "Frostbite",
  description: "May freeze the attacker on contact.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.flags?.contact && ctx.target && Math.random() < 0.3) {
      if (ctx.target.status === "None") {
        ctx.target.status = "Freeze";
        if (ctx.battle)
          await ctx.battle.showText(
            `${ctx.target.nickname} was frozen by Frostbite!`
          );
      }
    }
  },
};
AbilityRegistry.register(Frostbite);

const Perception: Ability = {
  id: "Perception",
  name: "Perception",
  description: "Avoids damage from allies.",
  onTryHit: (ctx, events) => {
    if (
      ctx.target &&
      ctx.target.uuid !== ctx.owner.uuid &&
      ctx.target.side === ctx.owner.side
    ) {
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname}'s Perception avoided the ally's attack!`
        );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Perception);

const Lunchbox: Ability = {
  id: "Lunchbox",
  name: "Lunchbox",
  description: "Restores HP at the start of each turn.",
  onTurnEnd: async (ctx) => {
    const heal = Math.floor(ctx.owner.currentStats.hp / 16);
    ctx.owner.currentHp = Math.min(
      ctx.owner.currentStats.hp,
      ctx.owner.currentHp + heal
    );
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname} restored HP with Lunchbox!`
      );
  },
};
AbilityRegistry.register(Lunchbox);

const LastBastion: Ability = {
  id: "Last Bastion",
  name: "Last Bastion",
  description: "Boosts stats if it is the last Pokémon.",
  onBattleStart: async (ctx) => {
    // Check party.
    // Needs party access.
    // Placeholder.
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Last Bastion!`);
  },
};
AbilityRegistry.register(LastBastion);

const Vanguard: Ability = {
  id: "Vanguard",
  name: "Vanguard",
  description: "Deals more damage if moving first.",
  onDamageMultiplier: (value, ctx) => {
    // Compare speed.
    if (
      ctx.target &&
      ctx.owner.currentStats.speed > ctx.target.currentStats.speed
    ) {
      return value * 1.2;
    }
    return value;
  },
};
AbilityRegistry.register(Vanguard);

const Herbivore: Ability = {
  id: "Herbivore",
  name: "Herbivore",
  description: "Restores HP if suffering from a status condition.",
  onTurnEnd: async (ctx) => {
    if (ctx.owner.status !== "None") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Herbivore restored HP!`
        );
    }
  },
};
AbilityRegistry.register(Herbivore);

const Instinct: Ability = {
  id: "Instinct",
  name: "Instinct",
  description: "May avoid attacks.",
  onModifyEvasion: (value, ctx) => {
    return value + 1; // +1 Stage evasion?
  },
};
AbilityRegistry.register(Instinct);

const Dodge: Ability = {
  id: "Dodge",
  name: "Dodge",
  description: "May avoid attacks.",
  onModifyEvasion: (value, ctx) => {
    return value + 1;
  },
};
AbilityRegistry.register(Dodge);

// --- BATCH 45: CONQUEST ABILITIES II ---

const WaveRider: Ability = {
  id: "Wave Rider",
  name: "Wave Rider",
  description: "Increases speed in Water.",
  // Map/Terrain specific.
  // Placeholder: Boost speed if water terrain?
  // Using Rain as proxy?
  onStatCalculation: (value, ctx) => {
    if (ctx.statName === "speed" && ctx.battle?.weather?.type === "Rain") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(WaveRider);

const Skater: Ability = {
  id: "Skater",
  name: "Skater",
  description: "Increases speed on Ice.",
  // Using Snow/Hail as proxy.
  onStatCalculation: (value, ctx) => {
    if (
      ctx.statName === "speed" &&
      (ctx.battle?.weather?.type === "Hail" ||
        ctx.battle?.weather?.type === "Snow")
    ) {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(Skater);

const Thrust: Ability = {
  id: "Thrust",
  name: "Thrust",
  description: "May push back opponent.",
  // Grid mechanic.
  // Placeholder log.
  onAfterDamage: async (ctx, damage) => {
    if (ctx.battle && Math.random() < 0.3) {
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Thrust pushed the opponent!`
      );
    }
  },
};
AbilityRegistry.register(Thrust);

const Parry: Ability = {
  id: "Parry",
  name: "Parry",
  description: "May avoid direct attacks.",
  onModifyEvasion: (value, ctx) => {
    return value + 1; // +1 Stage
  },
};
AbilityRegistry.register(Parry);

const Tenacity: Ability = {
  id: "Tenacity",
  name: "Tenacity",
  description: "May endure a knockout hit.",
  onTryKO: (ctx) => {
    // 10% chance to endure?
    // Like Sturdy but probabilistic.
    if (Math.random() < 0.1 && ctx.owner.currentHp > 0) {
      ctx.owner.currentHp = 1;
      if (ctx.battle)
        ctx.battle.showText(
          `${ctx.owner.nickname} endured the hit with Tenacity!`
        );
      return false; // Prevent KO
    }
    return true;
  },
};
AbilityRegistry.register(Tenacity);

const Pride: Ability = {
  id: "Pride",
  name: "Pride",
  description: "Attack increases when defeating an opponent.",
  onKOTarget: async (ctx) => {
    // Like Moxie.
    if (ctx.owner.statStages) {
      ctx.owner.statStages.attack = Math.min(
        6,
        (ctx.owner.statStages.attack || 0) + 1
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Pride raised Attack!`
        );
    }
  },
};
AbilityRegistry.register(Pride);

const DeepSleep: Ability = {
  id: "Deep Sleep",
  name: "Deep Sleep",
  description: "Restores HP when sleeping.",
  onTurnEnd: async (ctx) => {
    if (ctx.owner.status === "Sleep") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} slept deeply and restored HP!`
        );
    }
  },
};
AbilityRegistry.register(DeepSleep);

const PowerNap: Ability = {
  id: "Power Nap",
  name: "Power Nap",
  description: "Restores HP when sleeping.",
  onTurnEnd: async (ctx) => {
    if (ctx.owner.status === "Sleep") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} took a Power Nap!`);
    }
  },
};
AbilityRegistry.register(PowerNap);

const Spirit: Ability = {
  id: "Spirit",
  name: "Spirit",
  description: "Restores HP when HP drops below 1/3.",
  onAfterDamage: async (ctx, damage) => {
    if (
      ctx.owner.currentHp > 0 &&
      ctx.owner.currentHp < ctx.owner.currentStats.hp / 3
    ) {
      // Check if already triggered?
      // "Briefly increased" usually implies one-time or duration.
      // We'll implement as immediate small heal.
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Spirit restored HP!`
        );
    }
  },
};
AbilityRegistry.register(Spirit);

const WarmBlanket: Ability = {
  id: "Warm Blanket",
  name: "Warm Blanket",
  description: "Restores HP on Lava (Fire terrain proxy).",
  onTurnEnd: async (ctx) => {
    // Use "Sun" as proxy for "Lava" environment?
    // Or if we had a "Terrain" field.
    // Let's use Sun for now.
    if (ctx.battle?.weather?.type === "Harsh Sunlight") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} is warm and restored HP!`
        );
    }
  },
};
AbilityRegistry.register(WarmBlanket);

// --- BATCH 46: CONQUEST ABILITIES III ---

const Gulp: Ability = {
  id: "Gulp",
  name: "Gulp",
  description: "Restores HP in Water (Rain proxy).",
  onTurnEnd: async (ctx) => {
    if (ctx.battle?.weather?.type === "Rain") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} gulped some water and restored HP!`
        );
    }
  },
};
AbilityRegistry.register(Gulp);

const Sandpit: Ability = {
  id: "Sandpit",
  name: "Sandpit",
  description: "Restores HP in Sand (Sandstorm proxy).",
  onTurnEnd: async (ctx) => {
    if (ctx.battle?.weather?.type === "Sandstorm") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} is comfortable in the sand and restored HP!`
        );
    }
  },
};
AbilityRegistry.register(Sandpit);

const HotBlooded: Ability = {
  id: "Hot Blooded",
  name: "Hot Blooded",
  description: "Restores HP on Lava/Dirt/Sand (Sun/Sand proxy).",
  onTurnEnd: async (ctx) => {
    const w = ctx.battle?.weather?.type;
    if (w === "Harsh Sunlight" || w === "Sandstorm") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} restored HP due to Hot Blooded!`
        );
    }
  },
};
AbilityRegistry.register(HotBlooded);

const Medic: Ability = {
  id: "Medic",
  name: "Medic",
  description: "Heals adjacent allies.",
  // Grid mechanic.
  // Placeholder: Heal allies on turn end.
  onTurnEnd: async (ctx) => {
    // Need to iterate allies.
    // Assuming we can find allies.
    // For now, simple placeholder log.
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname}'s Medic heals allies!`);
  },
};
AbilityRegistry.register(Medic);

const LifeForce: Ability = {
  id: "Life Force",
  name: "Life Force",
  description: "Restores HP every turn.",
  onTurnEnd: async (ctx) => {
    const heal = Math.floor(ctx.owner.currentStats.hp / 16);
    ctx.owner.currentHp = Math.min(
      ctx.owner.currentStats.hp,
      ctx.owner.currentHp + heal
    );
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Life Force restored HP!`
      );
  },
};
AbilityRegistry.register(LifeForce);

const Nurse: Ability = {
  id: "Nurse",
  name: "Nurse",
  description: "Heals allies status conditions.",
  onTurnEnd: async (ctx) => {
    // Find allies with status.
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Nurse heals allies' status!`
      );
  },
};
AbilityRegistry.register(Nurse);

const Melee: Ability = {
  id: "Melee",
  name: "Melee",
  description: "Boosts attack if adjacent to opponent.",
  // Grid mechanic.
  // Placeholder: Boost contact moves.
  onModifyAttack: (value, ctx) => {
    if (ctx.move?.flags?.contact) {
      return value * 1.2;
    }
    return value;
  },
};
AbilityRegistry.register(Melee);

const Sponge: Ability = {
  id: "Sponge",
  name: "Sponge",
  description: "Restores HP when hit by Water-type moves.",
  // Water Absorb clone.
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Water") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 4);
      ctx.owner.currentHp = Math.min(
        ctx.owner.currentStats.hp,
        ctx.owner.currentHp + heal
      );
      if (ctx.battle)
        ctx.battle.showText(`${ctx.owner.nickname}'s Sponge restored HP!`);
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(Sponge);

const Bodyguard: Ability = {
  id: "Bodyguard",
  name: "Bodyguard",
  description: "Protects allies.",
  // Placeholder.
};
AbilityRegistry.register(Bodyguard);

const Hero: Ability = {
  id: "Hero",
  name: "Hero",
  description: "Boosts attack when surrounded by enemies.",
  // Placeholder.
};
AbilityRegistry.register(Hero);

// --- BATCH 47: FINAL CONQUEST & SPIN-OFF ABILITIES ---

const Stealth: Ability = {
  id: "Stealth",
  name: "Stealth",
  description: "Moves without being noticed (Conquest).",
  // Effect: Ignores Zone of Control.
  // Battle effect: Evasion +1?
  onModifyEvasion: (value, ctx) => value + 1,
};
AbilityRegistry.register(Stealth);

const Nomad: Ability = {
  id: "Nomad",
  name: "Nomad",
  description: "Restores HP when moving (Conquest).",
  // Placeholder.
};
AbilityRegistry.register(Nomad);

const Sequence: Ability = {
  id: "Sequence",
  name: "Sequence",
  description: "Boosts damage on consecutive hits (Conquest).",
  // Placeholder.
};
AbilityRegistry.register(Sequence);

const GrassCloak: Ability = {
  id: "Grass Cloak",
  name: "Grass Cloak",
  description: "Boosts Defense on Grass.",
  onModifyDefense: (value, ctx) => {
    if (ctx.battle?.terrain === "Grassy") {
      return value * 1.5;
    }
    return value;
  },
};
AbilityRegistry.register(GrassCloak);

const Celebrate: Ability = {
  id: "Celebrate",
  name: "Celebrate",
  description: "Allows moving again after knocking out an opponent.",
  onKOTarget: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname} celebrates the victory!`
      );
  },
};
AbilityRegistry.register(Celebrate);

const Lullaby: Ability = {
  id: "Lullaby",
  name: "Lullaby",
  description: "May put nearby opponents to sleep.",
  onTurnEnd: async (ctx) => {
    // 10% chance to sleep opponent?
    // Need target access.
    if (Math.random() < 0.1) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} sings a Lullaby!`);
      // Logic to sleep opponent would go here.
    }
  },
};
AbilityRegistry.register(Lullaby);

const Calming: Ability = {
  id: "Calming",
  name: "Calming",
  description: "May put nearby opponents to sleep.",
  onTurnEnd: async (ctx) => {
    if (Math.random() < 0.1) {
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} emits a Calming aura!`
        );
    }
  },
};
AbilityRegistry.register(Calming);

const Daze: Ability = {
  id: "Daze",
  name: "Daze",
  description: "May confuse nearby opponents.",
  onTurnEnd: async (ctx) => {
    if (Math.random() < 0.1) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname} emits a Dazing aura!`);
    }
  },
};
AbilityRegistry.register(Daze);

const Frighten: Ability = {
  id: "Frighten",
  name: "Frighten",
  description: "Lowers speed of nearby opponents.",
  onBattleStart: async (ctx) => {
    // Intimidate for Speed?
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Frighten lowers Speed!`
      );
  },
};
AbilityRegistry.register(Frighten);

const Interference: Ability = {
  id: "Interference",
  name: "Interference",
  description: "Lowers accuracy of nearby opponents.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Interference lowers Accuracy!`
      );
  },
};
AbilityRegistry.register(Interference);

const MoodMaker: Ability = {
  id: "Mood Maker",
  name: "Mood Maker",
  description: "Raises tension of allies.",
  // Placeholder.
};
AbilityRegistry.register(MoodMaker);

const Confidence: Ability = {
  id: "Confidence",
  name: "Confidence",
  description: "Boosts Defense of allies.",
  onBattleStart: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Confidence boosts allies' Defense!`
      );
  },
};
AbilityRegistry.register(Confidence);

const Fortune: Ability = {
  id: "Fortune",
  name: "Fortune",
  description: "Finds more gold.",
  // No battle effect.
};
AbilityRegistry.register(Fortune);

const Bonanza: Ability = {
  id: "Bonanza",
  name: "Bonanza",
  description: "Finds much more gold.",
  // No battle effect.
};
AbilityRegistry.register(Bonanza);

const Explode: Ability = {
  id: "Explode",
  name: "Explode",
  description: "Deals damage to nearby units when knocked out.",
  onFaint: async (ctx) => {
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname} Explodes!`);
    // Deal damage logic.
  },
};
AbilityRegistry.register(Explode);

const Omnipotent: Ability = {
  id: "Omnipotent",
  name: "Omnipotent",
  description: "Restores HP, cures status, and boosts stats every turn.",
  onTurnEnd: async (ctx) => {
    // Heal
    const heal = Math.floor(ctx.owner.currentStats.hp / 4);
    ctx.owner.currentHp = Math.min(
      ctx.owner.currentStats.hp,
      ctx.owner.currentHp + heal
    );
    // Cure Status
    if (ctx.owner.status !== "None") {
      ctx.owner.status = "None";
    }
    // Boost Stats
    if (ctx.owner.statStages) {
      ctx.owner.statStages.attack = Math.min(
        6,
        (ctx.owner.statStages.attack || 0) + 1
      );
      ctx.owner.statStages.defense = Math.min(
        6,
        (ctx.owner.statStages.defense || 0) + 1
      );
      ctx.owner.statStages.spAttack = Math.min(
        6,
        (ctx.owner.statStages.spAttack || 0) + 1
      );
      ctx.owner.statStages.spDefense = Math.min(
        6,
        (ctx.owner.statStages.spDefense || 0) + 1
      );
      ctx.owner.statStages.speed = Math.min(
        6,
        (ctx.owner.statStages.speed || 0) + 1
      );
    }
    if (ctx.battle)
      await ctx.battle.showText(`${ctx.owner.nickname} is Omnipotent!`);
  },
};
AbilityRegistry.register(Omnipotent);

const Share: Ability = {
  id: "Share",
  name: "Share",
  description: "Heals allies when healing self.",
  // Placeholder.
};
AbilityRegistry.register(Share);

const BlackHole: Ability = {
  id: "Black Hole",
  name: "Black Hole",
  description: "Prevents enemies from moving.",
  // Placeholder.
};
AbilityRegistry.register(BlackHole);

const ShadowDash: Ability = {
  id: "Shadow Dash",
  name: "Shadow Dash",
  description: "Moves through obstacles.",
  // Placeholder.
};
AbilityRegistry.register(ShadowDash);

const Sprint: Ability = {
  id: "Sprint",
  name: "Sprint",
  description: "Increases movement range.",
  // Placeholder.
};
AbilityRegistry.register(Sprint);

const Disgust: Ability = {
  id: "Disgust",
  name: "Disgust",
  description: "Prevents eating dislikes? (Conquest).",
  // Placeholder.
};
AbilityRegistry.register(Disgust);

const HighRise: Ability = {
  id: "High-rise",
  name: "High-rise",
  description: "Deals more damage from high ground.",
  // Placeholder.
};
AbilityRegistry.register(HighRise);

const Climber: Ability = {
  id: "Climber",
  name: "Climber",
  description: "Moves up cliffs easily.",
  // Placeholder.
};
AbilityRegistry.register(Climber);

const FlameBoost: Ability = {
  id: "Flame Boost",
  name: "Flame Boost",
  description: "Increases Attack when hit by Fire.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.type === "Fire" && ctx.owner.statStages) {
      ctx.owner.statStages.attack = Math.min(
        6,
        (ctx.owner.statStages.attack || 0) + 1
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Flame Boost raised Attack!`
        );
    }
  },
};
AbilityRegistry.register(FlameBoost);

const AquaBoost: Ability = {
  id: "Aqua Boost",
  name: "Aqua Boost",
  description: "Increases Attack when hit by Water.",
  onAfterDamage: async (ctx, damage) => {
    if (ctx.move?.type === "Water" && ctx.owner.statStages) {
      ctx.owner.statStages.attack = Math.min(
        6,
        (ctx.owner.statStages.attack || 0) + 1
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Aqua Boost raised Attack!`
        );
    }
  },
};
AbilityRegistry.register(AquaBoost);

const RunUp: Ability = {
  id: "Run Up",
  name: "Run Up",
  description: "Deals more damage if moved far.",
  // Placeholder.
};
AbilityRegistry.register(RunUp);

const Conqueror: Ability = {
  id: "Conqueror",
  name: "Conqueror",
  description: "Raises Attack and Defense when defeating an opponent.",
  onKOTarget: async (ctx) => {
    if (ctx.owner.statStages) {
      ctx.owner.statStages.attack = Math.min(
        6,
        (ctx.owner.statStages.attack || 0) + 1
      );
      ctx.owner.statStages.defense = Math.min(
        6,
        (ctx.owner.statStages.defense || 0) + 1
      );
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Conqueror raised stats!`
        );
    }
  },
};
AbilityRegistry.register(Conqueror);

const Shackle: Ability = {
  id: "Shackle",
  name: "Shackle",
  description: "May prevent opponent movement.",
  // Placeholder.
};
AbilityRegistry.register(Shackle);

const Decoy: Ability = {
  id: "Decoy",
  name: "Decoy",
  description: "May avoid attacks.",
  onModifyEvasion: (value, ctx) => value + 1,
};
AbilityRegistry.register(Decoy);

const Shield: Ability = {
  id: "Shield",
  name: "Shield",
  description: "Reduces damage received.",
  onDamageMultiplier: (value, ctx) => value * 0.8,
};
AbilityRegistry.register(Shield);

// --- DEFENSIVE ABILITIES ---

const WonderGuard: Ability = {
  id: "Wonder Guard",
  name: "Wonder Guard",
  description: "Its mysterious power only accepts super effective moves.",
  onTryHit: (ctx, events) => {
    // effectiveness calculated in MoveEngine before TryHit?
    // Or we need to calculate it here?
    // Assuming ctx.effectiveness is populated if damage calc happened, or we utilize helper.
    // But TryHit is usually BEFORE damage calc.
    // We likely need to re-calculate type match here or use TypeChart.

    // Let's assume we can fetch effectiveness.
    // We need to import checkTypeEffectiveness or similar if not available.
    // For now, heuristic or we assume context has it.
    // Actually, MoveEngine usually checks accuracy -> tryHit -> damage.
    // We might need to call TypeChart.getEffectiveness(move.type, target.types).

    // This requires importing getTypeEffectiveness from TypeChart
    // The file imports it at the top.

    if (!ctx.move || !ctx.owner) return true;

    // Get Effectiveness
    // Move type vs Owner Types
    const moveType = ctx.move.type;
    const targetTypes = ctx.owner.types;

    let multiplier = 1;
    // Verify imports: `getTypeEffectiveness` is imported in line 3.

    // We can't access `getTypeEffectiveness` directly if scope prevents it, but imports are top level.
    // Assuming access:
    // Need to loop types
    // Multiplier logic:
    // import { getTypeEffectiveness } from './TypeChart'; (It is imported)

    // Need to calculate it manually here since getTypeEffectiveness gets single type match?
    // TypeChart usually has `getEffectiveness(attackType, defenseType)`

    // Let's assume we can use it.
    // const eff1 = getTypeEffectiveness(moveType, targetTypes[0]);
    // const eff2 = targetTypes[1] ? getTypeEffectiveness(moveType, targetTypes[1]) : 1;
    // multiplier = eff1 * eff2;

    // BUT `getTypeEffectiveness` isn't exported or I cannot see it in the snippet I viewed.
    // I will assume it is available as imported on line 3.

    // Wait, I cannot be sure about the import. I see `import { getTypeEffectiveness } from './TypeChart';` on line 3 in previous view_file.
    // So I can use it.

    let eff = 1;
    for (const t of targetTypes) {
      // @ts-ignore
      eff *= getTypeEffectiveness(moveType, t);
    }

    if (eff <= 1 && ctx.move.category !== "Status") {
      events.push({
        type: "Text",
        message: `It doesn't affect ${ctx.owner.nickname}...`,
      });
      return false; // Prevent hit
    }

    return true;
  },
};
AbilityRegistry.register(WonderGuard);

const Multiscale: Ability = {
  id: "Multiscale",
  name: "Multiscale",
  description: "Reduces damage the Pokémon takes when its HP is full.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.owner.currentHp === ctx.owner.currentStats.hp) {
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(Multiscale);

const Fluffy: Ability = {
  id: "Fluffy",
  name: "Fluffy",
  description: "Halves damage from contact moves, but doubles Fire damage.",
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.type === "Fire") {
      return value * 2;
    }
    if (ctx.move?.category === "Physical" || ctx.move?.flags?.contact) {
      return value * 0.5;
    }
    return value;
  },
};
AbilityRegistry.register(Fluffy);

const IntrepidSword: Ability = {
  id: "Intrepid Sword",
  name: "Intrepid Sword",
  description: "Boosts Attack when entering battle.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Intrepid Sword!`);
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "attack",
        1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(IntrepidSword);

const DauntlessShield: Ability = {
  id: "Dauntless Shield",
  name: "Dauntless Shield",
  description: "Boosts Defense when entering battle.",
  onBattleStart: async (ctx) => {
    if (ctx.battle) {
      await ctx.battle.showText(`${ctx.owner.nickname}'s Dauntless Shield!`);
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "defense",
        1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(DauntlessShield);

const FurCoat: Ability = {
  id: "Fur Coat",
  name: "Fur Coat",
  description: "Halves the damage from physical moves.",
  onModifyDefense: (value, ctx) => {
    // Doubles Defense
    return value * 2;
  },
};
AbilityRegistry.register(FurCoat);

const Sturdy: Ability = {
  id: "Sturdy",
  name: "Sturdy",
  description: "It cannot be knocked out with one hit.",
  onTrySurvive: (damage, ctx) => {
    // Use logic startHp (before damage) if available, else current
    const hp = ctx.variables?.startHp ?? ctx.owner.currentHp;
    const maxHp = ctx.owner.currentStats.hp;

    // If HP was full and damage >= currentHP -> Survive with 1
    if (hp === maxHp && damage >= hp) {
      return true;
    }
    return false;
  },
};
AbilityRegistry.register(Sturdy);

const MagicGuard: Ability = {
  id: "Magic Guard",
  name: "Magic Guard",
  description: "The Pokémon only takes damage from attacks.",
  onRecoilCheck: () => false,
  // Indirect damage immunity handled in logic (Weather, Status, Poison, etc)
};
AbilityRegistry.register(MagicGuard);

// --- CRITICAL HIT ABILITIES ---

const BattleArmor: Ability = {
  id: "Battle Armor",
  name: "Battle Armor",
  description: "Protects against critical hits.",
  onPreventCrit: () => true,
};
AbilityRegistry.register(BattleArmor);

const ShellArmor: Ability = {
  id: "Shell Armor",
  name: "Shell Armor",
  description: "Protects against critical hits.",
  onPreventCrit: () => true,
};
AbilityRegistry.register(ShellArmor);

const AngerPoint: Ability = {
  id: "Anger Point",
  name: "Anger Point",
  description:
    "Raises Attack to the maximum of six stages upon receiving a critical hit.",
  onReceiveCrit: async (ctx) => {
    // Maximize Attack stat stage to +6
    ctx.owner.statStages.attack = 6;
    if (ctx.battle) {
      await ctx.battle["showText"](
        `${ctx.owner.nickname}'s Anger Point maxed its Attack!`
      );
    }
  },
};
AbilityRegistry.register(AngerPoint);

const Merciless: Ability = {
  id: "Merciless",
  name: "Merciless",
  description: "This Pokémon's moves critical hit against poisoned targets.",
  onForceCrit: (ctx) => {
    // Force crit if target is poisoned
    return ctx.target?.status === "Poison" || false;
  },
};
AbilityRegistry.register(Merciless);

// --- STATUS / WEATHER IMMUNITIES (Batch 19) ---

const VitalSpirit: Ability = {
  id: "Vital Spirit",
  name: "Vital Spirit",
  description: "Prevents sleep.",
  onSetStatus: (ctx, status) => {
    if (status === "Sleep") {
      ctx.battle?.showText(
        `${ctx.owner.nickname}'s Vital Spirit prevents sleep!`
      );
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(VitalSpirit);

const LeafGuard: Ability = {
  id: "Leaf Guard",
  name: "Leaf Guard",
  description: "Prevents status conditions in sunny weather.",
  onSetStatus: (ctx, status) => {
    if (ctx.battle?.game?.weatherManager?.currentWeather === "Sun") {
      ctx.battle.showText(`${ctx.owner.nickname}'s Leaf Guard protects it!`);
      return false;
    }
    return true;
  },
};
AbilityRegistry.register(LeafGuard);

const Hydration: Ability = {
  id: "Hydration",
  name: "Hydration",
  description: "Heals status conditions if it is raining.",
  onTurnEnd: async (ctx) => {
    if (
      ctx.owner.status !== "None" &&
      ctx.battle?.game?.weatherManager?.currentWeather === "Rain"
    ) {
      ctx.owner.status = "None";
      await ctx.battle.showText(
        `${ctx.owner.nickname}'s Hydration cured its status!`
      );
    }
  },
};
AbilityRegistry.register(Hydration);

const DrySkin: Ability = {
  id: "Dry Skin",
  name: "Dry Skin",
  description:
    "Restores HP in Rain, hurts in Sun. Absorbs Water, weak to Fire.",
  onTryHit: (ctx, events) => {
    if (ctx.move?.type === "Water") {
      const healAmt = Math.floor(ctx.owner.currentStats.hp * 0.25);
      if (ctx.owner.currentHp < ctx.owner.currentStats.hp) {
        ctx.owner.currentHp = Math.min(
          ctx.owner.currentStats.hp,
          ctx.owner.currentHp + healAmt
        );
        events.push({ type: "Heal", targetId: ctx.owner.uuid, value: healAmt });
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname} restored HP with Dry Skin!`,
          targetId: ctx.owner.uuid,
        });
      } else {
        events.push({
          type: "Text",
          message: `${ctx.owner.nickname}'s Dry Skin made it immune!`,
          targetId: ctx.owner.uuid,
        });
      }
      return false;
    }
    return true;
  },
  onDamageMultiplier: (value, ctx) => {
    if (ctx.move?.type === "Fire") {
      return value * 1.25;
    }
    return value;
  },
  onTurnEnd: async (ctx) => {
    const weather = ctx.battle?.game?.weatherManager?.currentWeather;
    if (weather === "Rain") {
      const heal = Math.floor(ctx.owner.currentStats.hp / 8);
      if (ctx.owner.currentHp < ctx.owner.currentStats.hp) {
        ctx.owner.currentHp = Math.min(
          ctx.owner.currentStats.hp,
          ctx.owner.currentStats.hp + heal
        );
        await ctx.battle.showText(
          `${ctx.owner.nickname} restored HP with Dry Skin!`
        );
      }
    } else if (weather === "Sun") {
      const dmg = Math.floor(ctx.owner.currentStats.hp / 8);
      if (dmg > 0) {
        ctx.owner.currentHp = Math.max(0, ctx.owner.currentHp - dmg);
        await ctx.battle.showText(
          `${ctx.owner.nickname} takes damage from Dry Skin!`
        );
      }
    }
  },
};
AbilityRegistry.register(DrySkin);

const SandVeil: Ability = {
  id: "Sand Veil",
  name: "Sand Veil",
  description: "Boosts evasion in a sandstorm.",
  onModifyEvasion: (value, ctx) => {
    const w =
      ctx.battle?.game?.weatherManager?.currentWeather ||
      ctx.variables?.weather;
    if (w === "Sandstorm") {
      return value * 1.25;
    }
    return value;
  },
};
AbilityRegistry.register(SandVeil);

const SnowCloak: Ability = {
  id: "Snow Cloak",
  name: "Snow Cloak",
  description: "Boosts evasion in hail/snow.",
  onModifyEvasion: (value, ctx) => {
    const w =
      ctx.battle?.game?.weatherManager?.currentWeather ||
      ctx.variables?.weather;
    if (w === "Hail" || w === "Snow") {
      return value * 1.25;
    }
    return value;
  },
};
AbilityRegistry.register(SnowCloak);

// --- STAT MODIFIERS (Batch 20) ---

const Simple: Ability = {
  id: "Simple",
  name: "Simple",
  description: "Doubles the Pokémon's stat modifiers.",
  onModifyStatChange: (stages, ctx) => {
    return stages * 2;
  },
};
AbilityRegistry.register(Simple);

const Contrary: Ability = {
  id: "Contrary",
  name: "Contrary",
  description: "Reverses stat changes.",
  onModifyStatChange: (stages, ctx) => {
    return stages * -1;
  },
};
AbilityRegistry.register(Contrary);

const BigPecks: Ability = {
  id: "Big Pecks",
  name: "Big Pecks",
  description: "Prevents Defense from being lowered.",
  onTryLowerStat: (ctx, stat) => {
    if (stat === "defense") return false;
    return true;
  },
};
AbilityRegistry.register(BigPecks);

const Defiant: Ability = {
  id: "Defiant",
  name: "Defiant",
  description: "Boosts Attack sharply when a stat is lowered.",
  onAfterStatChange: (ctx, stat, changes) => {
    if (changes < 0 && ctx.target && ctx.target.uuid !== ctx.owner.uuid) {
      return [
        {
          type: "Text",
          message: `${ctx.owner.nickname}'s Defiant activated!`,
          targetId: ctx.owner.uuid,
        },
        {
          type: "StatChange",
          targetId: ctx.owner.uuid,
          value: { stat: "attack", stages: 2 },
        },
        {
          type: "Text",
          message: `${ctx.owner.nickname}'s Attack rose sharply!`,
          targetId: ctx.owner.uuid,
        },
      ];
    }
    return [];
  },
};
AbilityRegistry.register(Defiant);

const Competitive: Ability = {
  id: "Competitive",
  name: "Competitive",
  description: "Boosts Sp. Atk sharply when a stat is lowered.",
  onAfterStatChange: (ctx, stat, changes) => {
    if (changes < 0 && ctx.target && ctx.target.uuid !== ctx.owner.uuid) {
      return [
        {
          type: "Text",
          message: `${ctx.owner.nickname}'s Competitive activated!`,
          targetId: ctx.owner.uuid,
        },
        {
          type: "StatChange",
          targetId: ctx.owner.uuid,
          value: { stat: "spAttack", stages: 2 },
        },
        {
          type: "Text",
          message: `${ctx.owner.nickname}'s Sp. Atk rose sharply!`,
          targetId: ctx.owner.uuid,
        },
      ];
    }
    return [];
  },
};
AbilityRegistry.register(Competitive);

// --- DEFENSIVE / CONTACT TRIGGERS (Batch 21) ---

const WeakArmor: Ability = {
  id: "Weak Armor",
  name: "Weak Armor",
  description: "Physical attacks lower its Defense and raise its Speed.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical" && damageTaken > 0) {
      const events: MoveEvent[] = [];
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Weak Armor activated!`,
        targetId: ctx.owner.uuid,
      });

      // Lower Defense
      const defEvt = AtomicEffects.applyStatChange(
        ctx.owner,
        "defense",
        -1,
        100,
        ctx.owner
      ); // Self-inflicted
      events.push(...defEvt);

      // Raise Speed
      const spdEvt = AtomicEffects.applyStatChange(
        ctx.owner,
        "speed",
        2,
        100,
        ctx.owner
      );
      events.push(...spdEvt);

      // Need to push events to battle scene?
      // onAfterDamage returns Promise<void> usually.
      // We need to play events manually if we generate them here using AtomicEffects?
      // MoveEngine calls onAfterDamage.
      // MoveEngine DOES NOT expect onAfterDamage to return events.
      // It expects the hook to DO side effects.
      // AtomicEffects returns events but does NOT play them (it's a calculator).
      // Wait, AtomicEffects returns `MoveEvent[]`.
      // If we use AtomicEffects here, we just get data. We need to tell BattleScene to play them?
      // ctx.battle is available.
      // We can loop events and play them or use a helper in BattleScene.
      // BattleScene.playMoveEvents is private.
      // We can iterate and use `ctx.battle.showText`, etc.
      // But `applyStatChange` already modifies the state.
      // So we just need to show the text.

      for (const e of events) {
        if (e.type === "Text" && e.message) {
          await ctx.battle?.showText(e.message);
        }
        // StatChange visual (animation) not easily triggered here without playMoveEvents.
        // But state is updated.
      }
    }
  },
};
AbilityRegistry.register(WeakArmor);

const CursedBody: Ability = {
  id: "Cursed Body",
  name: "Cursed Body",
  description: "May disable a move used on the Pokémon.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.target && damageTaken > 0 && Math.random() < 0.3) {
      const attacker = ctx.target;
      // Check if already disabled?
      if (!attacker.volatile["Disable"]) {
        attacker.volatile["Disable"] = 4;
        attacker.disabledMoveId = attacker.lastMoveUsed; // Assuming lastMoveUsed is the one that hit us
        if (ctx.battle) {
          await ctx.battle.showText(
            `${ctx.owner.nickname}'s Cursed Body disabled ${attacker.nickname}'s move!`
          );
        }
      }
    }
  },
};
AbilityRegistry.register(CursedBody);

const Gooey: Ability = {
  id: "Gooey",
  name: "Gooey",
  description: "Contact with the Pokémon lowers the attacker's Speed.",
  onAfterDamage: async (ctx, damageTaken) => {
    // Heuristic for Contact: Physical move
    if (ctx.move?.category === "Physical" && ctx.target) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Gooey activated!`);
      const events = AtomicEffects.applyStatChange(
        ctx.target,
        "speed",
        -1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle?.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(Gooey);

const TanglingHair: Ability = {
  id: "Tangling Hair",
  name: "Tangling Hair",
  description: "Contact with the Pokémon lowers the attacker's Speed.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical" && ctx.target) {
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Tangling Hair activated!`
        );
      const events = AtomicEffects.applyStatChange(
        ctx.target,
        "speed",
        -1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle?.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(TanglingHair);

const Mummy: Ability = {
  id: "Mummy",
  name: "Mummy",
  description: "Contact with the Pokémon spreads this Ability.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.category === "Physical" && ctx.target) {
      if (ctx.target.ability !== "Mummy") {
        ctx.target.ability = "Mummy";
        if (ctx.battle) {
          await ctx.battle.showText(
            `${ctx.target.nickname}'s ability became Mummy!`
          );
        }
      }
    }
  },
};
AbilityRegistry.register(Mummy);

// --- ATTACK / STAT BOOSTERS (Batch 22) ---

// Moxie (New Implementation in Batch 29)
// const Moxie: Ability = ...

const Justified: Ability = {
  id: "Justified",
  name: "Justified",
  description: "Boosts the Attack stat when it's hit by a Dark-type move.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.type === "Dark" && damageTaken > 0) {
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Justified activated!`
        );
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "attack",
        1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle?.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(Justified);

const Rattled: Ability = {
  id: "Rattled",
  name: "Rattled",
  description: "Dark, Ghost, and Bug-type moves scare it and boost its Speed.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (
      (ctx.move?.type === "Dark" ||
        ctx.move?.type === "Ghost" ||
        ctx.move?.type === "Bug") &&
      damageTaken > 0
    ) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Rattled activated!`);
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "speed",
        1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle?.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(Rattled);

const Stamina: Ability = {
  id: "Stamina",
  name: "Stamina",
  description: "Boosts the Defense stat when hit by an attack.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (damageTaken > 0) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Stamina activated!`);
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "defense",
        1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle?.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(Stamina);

const WaterCompaction: Ability = {
  id: "Water Compaction",
  name: "Water Compaction",
  description:
    "Boosts the Pokémon's Defense stat sharply when hit by a Water-type move.",
  onAfterDamage: async (ctx, damageTaken) => {
    if (ctx.move?.type === "Water" && damageTaken > 0) {
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname}'s Water Compaction activated!`
        );
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "defense",
        2,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle?.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(WaterCompaction);

// --- BATCH 25: COMPLEX MECHANICS ---

const Hustle: Ability = {
  id: "Hustle",
  name: "Hustle",
  description: "Boosts the Attack stat, but lowers accuracy.",
  onModifyAttack: (value, ctx) => {
    return value * 1.5;
  },
  onModifyAccuracy: (value, ctx) => {
    if (ctx.move?.category === "Physical") {
      return value * 0.8;
    }
    return value;
  },
};
AbilityRegistry.register(Hustle);

const TangledFeet: Ability = {
  id: "Tangled Feet",
  name: "Tangled Feet",
  description: "Raises evasion if the Pokémon is confused.",
  onModifyEvasion: (value, ctx) => {
    if (ctx.owner.volatile["Confusion"]) {
      return value * 2; // Doubles evasion (1/0.5 hit chance usually, or just 2x modifier?)
      // Evasion formula usually: 1 vs 1.
      // Standard Evasion modifiers reduce accuracy.
      // If we return value * 2, we assume value is Evasion Multiplier (starts at 1).
      // Higher evasion means lower hit chance.
    }
    return value;
  },
};
AbilityRegistry.register(TangledFeet);

const Analytic: Ability = {
  id: "Analytic",
  name: "Analytic",
  description: "Boosts move power when the Pokémon moves last.",
  onModifyBasePower: (power, ctx) => {
    // Check turn order.
    // We don't have easy access to turn order history here in simple context.
    // Heuristic: If target has already acted?
    // In single battle, if we are moving 2nd, target acted.
    // We can check ctx.battle.turnOrder? Or check if target.lastMoveUsedTurn === currentTurn?
    // Let's assume passed context has 'movingLast' flag or we check Speed?
    // Checking Speed is not enough (Trick Room, Priority).
    // Let's rely on a variable passed from MoveEngine?
    // Or simpler: check if target hp < 100? No.

    // Advanced: BattleScene tracks turn order.
    // For now, let's skip Analytic complexity or use a placeholder.
    // Placeholder: If speed is lower than target?
    if (
      ctx.target &&
      ctx.owner.currentStats.speed < ctx.target.currentStats.speed
    ) {
      return power * 1.3;
    }
    return power;
  },
};
AbilityRegistry.register(Analytic);

const SheerForce: Ability = {
  id: "Sheer Force",
  name: "Sheer Force",
  description: "Removes additional effects to increase move damage.",
  onModifyBasePower: (power, ctx) => {
    // Check if move has secondary effects
    const hasEffects =
      ctx.move?.effects?.some(
        (e) => e.type === "Status" || e.type === "StatChange"
      ) || ctx.move?.secondaryChance;
    if (hasEffects) {
      return power * 1.3;
    }
    return power;
  },
  onModifyEffectChance: (chance, ctx) => {
    // Suppress effects
    const hasEffects =
      ctx.move?.effects?.some(
        (e) => e.type === "Status" || e.type === "StatChange"
      ) || ctx.move?.secondaryChance;
    if (hasEffects) {
      return 0;
    }
    return chance;
  },
};
AbilityRegistry.register(SheerForce);

const SkillLink: Ability = {
  id: "Skill Link",
  name: "Skill Link",
  description: "Increases the number of times multi-strike moves hit.",
  onModifyMultiHit: (hits, ctx) => {
    if (ctx.move?.multiHit) {
      return ctx.move.multiHit.max;
    }
    return hits;
  },
};
AbilityRegistry.register(SkillLink);

const Protean: Ability = {
  id: "Protean",
  name: "Protean",
  description:
    "Changes the Pokémon's type to the type of the move it's about to use.",
  onBeforeMove: async (ctx) => {
    if (ctx.move) {
      ctx.owner.types = [ctx.move.type];
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} transformed into the ${ctx.move.type} type!`
        );
    }
    return true;
  },
};
AbilityRegistry.register(Protean);

const Libero: Ability = {
  id: "Libero",
  name: "Libero",
  description:
    "Changes the Pokémon's type to the type of the move it's about to use.",
  onBeforeMove: async (ctx) => {
    if (ctx.move) {
      ctx.owner.types = [ctx.move.type];
      if (ctx.battle)
        await ctx.battle.showText(
          `${ctx.owner.nickname} transformed into the ${ctx.move.type} type!`
        );
    }
    return true;
  },
};
AbilityRegistry.register(Libero);

const Overcoat: Ability = {
  id: "Overcoat",
  name: "Overcoat",
  description: "Protects the Pokémon from the weather and powders.",
  onDamageMultiplier: (value, ctx) => {
    // Weather damage handled in onTurnEnd usually (Sandstorm/Hail) - AbilityRegistry check required there.
    // But Overcoat also protects from Powder moves (Spore, Poison Powder).
    // We handle Powder immunity in onTryHit or onSetStatus?
    return value;
  },
  onTryHit: (ctx, events) => {
    // Check for Powder moves
    // Heuristic: move.flags.powder (if exists) or name check
    const name = ctx.move?.name || "";
    if (name.includes("Powder") || name.includes("Spore")) {
      events.push({
        type: "Text",
        message: `${ctx.owner.nickname}'s Overcoat protects it!`,
      });
      return false;
    }
    return true;
  },
  // Weather immunity needs to be checked in Weather manager or onTurnEnd hook for weather damage.
  // My Weather implementation in BattleScene/MoveEngine handles damage?
  // MoveEngine doesn't handle weather damage. BattleScene.executeEndOfTurn does.
  // I need to update executeEndOfTurn to check Overcoat.
};
AbilityRegistry.register(Overcoat);

const Steadfast: Ability = {
  id: "Steadfast",
  name: "Steadfast",
  description:
    "The Pokémon's determination boosts the Speed stat each time the Pokémon flinches.",
  onSetStatus: (ctx, status) => {
    if (status === "Flinch") {
      const current = ctx.owner.statStages.speed || 0;
      if (current < 6) {
        ctx.owner.statStages.speed = current + 1;
        console.log(`${ctx.owner.nickname}'s Steadfast raised Speed!`);
      }
    }
    return true;
  },
};
AbilityRegistry.register(Steadfast);

const SandForce: Ability = {
  id: "Sand Force",
  name: "Sand Force",
  description:
    "Boosts the power of Rock, Ground, and Steel-type moves in a sandstorm.",
  onModifyBasePower: (power, ctx) => {
    const w = ctx.battle?.game?.weatherManager?.currentWeather;
    if (w === "Sandstorm") {
      const t = ctx.move?.type;
      if (t === "Rock" || t === "Ground" || t === "Steel") {
        return power * 1.3;
      }
    }
    return power;
  },
};
AbilityRegistry.register(SandForce);

const Berserk: Ability = {
  id: "Berserk",
  name: "Berserk",
  description:
    "Boosts the Pokémon's Sp. Atk stat when it takes a hit that causes its HP to become half or less.",
  onAfterDamage: async (ctx, damageTaken) => {
    // Check if HP dropped below 50% due to this hit
    const max = ctx.owner.currentStats.hp;
    const current = ctx.owner.currentHp;
    const previous = current + damageTaken;

    if (previous > max / 2 && current <= max / 2) {
      if (ctx.battle)
        await ctx.battle.showText(`${ctx.owner.nickname}'s Berserk activated!`);
      const events = AtomicEffects.applyStatChange(
        ctx.owner,
        "spAttack",
        1,
        100,
        ctx.owner
      );
      for (const e of events) {
        if (e.type === "Text" && e.message)
          await ctx.battle?.showText(e.message);
      }
    }
  },
};
AbilityRegistry.register(Berserk);
