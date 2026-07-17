using System.Collections.Frozen;
using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum EntryHazardKind { Damage, Status, Stage }

public sealed record EntryHazardProfile(
    BattleConditionId Id,
    EntryHazardKind Kind,
    int MaximumLayers,
    bool GroundedOnly,
    IReadOnlyList<Fraction> Fractions,
    EntityId? DamageType,
    IReadOnlyList<PersistentStatus> Statuses,
    StatKind? Stat,
    int StageDelta,
    IReadOnlySet<EntityId> AbsorbTypes);

public static class EntryHazardConditions
{
    private static readonly EntityId Rock = EntityId.Parse("type:rock");

    public static EntryHazardProfile LayeredDamage(string key, IReadOnlyList<Fraction> fractions,
        bool groundedOnly = true) => Normalize(new EntryHazardProfile(
            Id(key), EntryHazardKind.Damage, fractions.Count, groundedOnly, fractions,
            null, [], null, 0, new HashSet<EntityId>()));

    public static EntryHazardProfile TypeScaledDamage(string key, EntityId type, Fraction fraction,
        bool groundedOnly = false) => Normalize(new EntryHazardProfile(
            Id(key), EntryHazardKind.Damage, 1, groundedOnly, [fraction],
            type, [], null, 0, new HashSet<EntityId>()));

    public static EntryHazardProfile Status(string key, IReadOnlyList<PersistentStatus> statuses,
        IReadOnlySet<EntityId> absorbTypes, bool groundedOnly = true) => Normalize(new EntryHazardProfile(
            Id(key), EntryHazardKind.Status, statuses.Count, groundedOnly, [],
            null, statuses, null, 0, absorbTypes));

    public static EntryHazardProfile Stage(string key, StatKind stat, int delta,
        bool groundedOnly = true) => Normalize(new EntryHazardProfile(
            Id(key), EntryHazardKind.Stage, 1, groundedOnly, [],
            null, [], stat, delta, new HashSet<EntityId>()));

    public static EntryHazardProfile LegacyLayeredDamage { get; } = LayeredDamage(
        "layered_damage", [new Fraction(1, 8), new Fraction(1, 6), new Fraction(1, 4)]);

    public static EntryHazardProfile LegacyTypeScaledDamage { get; } = TypeScaledDamage(
        "type_scaled_rock", Rock, new Fraction(1, 8));

    public static BattleConditionDefinition Definition(EntryHazardProfile profile)
    {
        profile = Normalize(profile);
        return new BattleConditionDefinition
        {
            Id = profile.Id,
            Scope = BattleConditionScope.Side,
            Hooks = [BattleConditionHook.SwitchIn],
            Tags = ["entry_hazard", "hazard", $"hazard_{profile.Kind.ToString().ToLowerInvariant()}"],
            StackingKey = profile.Id.Value.Replace(':', '_'),
            StackingPolicy = profile.MaximumLayers == 1
                ? BattleConditionStackingPolicy.Reject
                : BattleConditionStackingPolicy.Stack,
            MaximumStacks = profile.MaximumLayers,
            SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
            FaintPolicy = BattleConditionFaintPolicy.Persist,
            EntryHazard = profile,
        };
    }

    public static IReadOnlyList<BattleConditionInstance> Active(
        IEnumerable<BattleConditionInstance> conditions, BattleSide side)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(side))
            throw new ArgumentOutOfRangeException(nameof(side));
        return conditions.Where(instance => instance.Owner == SideConditions.Owner(side)
                && instance.Definition.EntryHazard is not null)
            .OrderBy(instance => instance.Sequence)
            .ToArray();
    }

    public static int Damage(EntryHazardProfile profile, int layers, int maxHp, double effectiveness = 1)
    {
        profile = Validate(profile);
        if (profile.Kind != EntryHazardKind.Damage || layers <= 0)
            return 0;
        Fraction fraction = profile.Fractions[Math.Min(layers, profile.MaximumLayers) - 1];
        return profile.DamageType is null
            ? EffectMath.FractionOfMaxHp(maxHp, fraction)
            : EffectMath.TypeScaledHazardDamage(maxHp, fraction, effectiveness);
    }

    public static PersistentStatus StatusFor(EntryHazardProfile profile, int layers)
    {
        profile = Validate(profile);
        if (profile.Kind != EntryHazardKind.Status || layers <= 0)
            throw new ArgumentException("A positive status-hazard layer is required.", nameof(layers));
        return profile.Statuses[Math.Min(layers, profile.MaximumLayers) - 1];
    }

    public static EntryHazardProfile Validate(EntryHazardProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Id.Value) || profile.Id.Value.Split(':')[0] != "hazard")
            throw new ArgumentException("Entry-hazard condition IDs must use the hazard family.", nameof(profile));
        if (!Enum.IsDefined(profile.Kind) || profile.MaximumLayers is < 1 or > 8)
            throw new ArgumentException("Entry hazards require a defined kind and 1..8 layers.", nameof(profile));
        if (profile.Fractions is null || profile.Statuses is null || profile.AbsorbTypes is null)
            throw new ArgumentException("Entry-hazard payload collections cannot be null.", nameof(profile));
        if (profile.Fractions.Any(fraction => fraction.Num <= 0 || fraction.Den <= 0 || fraction.Num > fraction.Den))
            throw new ArgumentException("Entry-hazard fractions must be in (0,1].", nameof(profile));
        if (profile.DamageType is { Category: not EntityCategory.Type }
            || profile.AbsorbTypes.Any(type => type.Category != EntityCategory.Type))
            throw new ArgumentException("Entry-hazard type references must be type IDs.", nameof(profile));
        if (profile.Statuses.Any(status => !Enum.IsDefined(status))
            || profile.Stat is { } stat && (!Enum.IsDefined(stat) || stat == StatKind.Hp))
            throw new ArgumentException("Entry-hazard status and stat values must be defined battle values.", nameof(profile));

        bool valid = profile.Kind switch
        {
            EntryHazardKind.Damage => profile.Fractions.Count == profile.MaximumLayers
                && profile.Statuses.Count == 0 && profile.Stat is null && profile.StageDelta == 0
                && profile.AbsorbTypes.Count == 0
                && (profile.DamageType is null || profile.MaximumLayers == 1),
            EntryHazardKind.Status => profile.Statuses.Count == profile.MaximumLayers
                && profile.Fractions.Count == 0 && profile.DamageType is null
                && profile.Stat is null && profile.StageDelta == 0,
            EntryHazardKind.Stage => profile.MaximumLayers == 1 && profile.Stat is not null
                && profile.StageDelta is >= -6 and <= 6 and not 0
                && profile.Fractions.Count == 0 && profile.DamageType is null
                && profile.Statuses.Count == 0 && profile.AbsorbTypes.Count == 0,
            _ => false,
        };
        return valid ? profile : throw new ArgumentException("Entry-hazard payload does not match its kind.", nameof(profile));
    }

    public static bool Equivalent(EntryHazardProfile left, EntryHazardProfile right) =>
        left.Id == right.Id && left.Kind == right.Kind && left.MaximumLayers == right.MaximumLayers
        && left.GroundedOnly == right.GroundedOnly && left.DamageType == right.DamageType
        && left.Stat == right.Stat && left.StageDelta == right.StageDelta
        && left.Fractions.SequenceEqual(right.Fractions) && left.Statuses.SequenceEqual(right.Statuses)
        && left.AbsorbTypes.SetEquals(right.AbsorbTypes);

    internal static EntryHazardProfile Normalize(EntryHazardProfile profile)
    {
        profile = Validate(profile);
        return profile with
        {
            Fractions = Array.AsReadOnly(profile.Fractions.ToArray()),
            Statuses = Array.AsReadOnly(profile.Statuses.ToArray()),
            AbsorbTypes = profile.AbsorbTypes.ToFrozenSet(),
        };
    }

    private static BattleConditionId Id(string key)
    {
        if (!BattleConditionId.ValidToken(key))
            throw new ArgumentException("Entry-hazard keys must be lowercase tokens.", nameof(key));
        return new BattleConditionId($"hazard:{key}");
    }
}
