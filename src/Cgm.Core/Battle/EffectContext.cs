namespace Cgm.Core.Battle;

/// <summary>
/// Per-effect resolution context (EFFECT_TYPES_CATALOG §2.3), passed to every primitive so they stay
/// small. Bundles who is acting on whom; the controller owns state mutation, the RNG, and the event
/// log. Only the fields resolution needs today are here — item/ability/ruleset/trace grow in later
/// catalog layers (§12), per the promotion rule (don't add speculative fields).
/// </summary>
internal readonly record struct EffectContext(
    BattleMove Move,
    BattleCreature Source,
    BattleSide SourceSide,
    BattleCreature Target,
    BattleSide TargetSide,
    int DamageDealt);
