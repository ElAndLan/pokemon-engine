using System.Collections.ObjectModel;
using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum ProtectionScope { Personal, Side }
public enum ProtectionFilter { All, Priority, MultiTarget }
public enum ProtectionChainMode { Shared, ClassicOnly, None }

public abstract record ProtectionContactEffect;
public sealed record ProtectionContactDamage(Fraction Fraction) : ProtectionContactEffect;
public sealed record ProtectionContactStatus(PersistentStatus Status) : ProtectionContactEffect;
public sealed record ProtectionContactStage(StatKind Stat, int Delta) : ProtectionContactEffect;

public sealed record ProtectionProfile(
    BattleConditionId Id,
    ProtectionScope Scope,
    ProtectionFilter Filter,
    ProtectionChainMode Chain,
    bool DrawGuaranteed,
    IReadOnlyList<ProtectionContactEffect> ContactEffects);

public static class ProtectionConditions
{
    public static ProtectionProfile LegacyPersonal { get; } = Personal(
        "personal", ProtectionChainMode.Shared, drawGuaranteed: true, []);

    public static ProtectionProfile Personal(string key, ProtectionChainMode chain,
        bool drawGuaranteed, IReadOnlyList<ProtectionContactEffect> contactEffects) => Normalize(new(
            Id(key), ProtectionScope.Personal, ProtectionFilter.All, chain, drawGuaranteed, contactEffects));

    public static ProtectionProfile Side(string key, ProtectionFilter filter,
        ProtectionChainMode chain, bool drawGuaranteed) => Normalize(new(
            Id(key), ProtectionScope.Side, filter, chain, drawGuaranteed, []));

    public static BattleConditionDefinition Definition(ProtectionProfile profile)
    {
        profile = Normalize(profile);
        if (profile.Scope != ProtectionScope.Personal)
            throw new ArgumentException("Only personal protection owns a dynamic creature condition.", nameof(profile));
        return new BattleConditionDefinition
        {
            Id = profile.Id,
            Scope = BattleConditionScope.Creature,
            Hooks = [BattleConditionHook.TryHit],
            DefaultDuration = 1,
            DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
            Tags = ["personal_protection", "protection"],
            StackingKey = "personal_protection",
            StackingPolicy = BattleConditionStackingPolicy.Reject,
            SwitchPolicy = BattleConditionSwitchPolicy.Remove,
            FaintPolicy = BattleConditionFaintPolicy.Remove,
            Protection = profile,
        };
    }

    public static BattleConditionInstance? Active(IEnumerable<BattleConditionInstance> conditions,
        BattleConditionOwner owner)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(owner);
        return conditions.SingleOrDefault(instance => instance.Owner == owner
            && instance.Definition.Protection is not null);
    }

    public static BattleConditionInstance? Active(IEnumerable<BattleConditionInstance> conditions,
        BattleSide side, int partyIndex)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(side) || partyIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(partyIndex));
        return conditions.SingleOrDefault(instance => instance.Owner.Scope == BattleConditionScope.Creature
            && instance.Owner.Side == side && instance.Owner.PartyIndex == partyIndex
            && instance.Definition.Protection is not null);
    }

    public static bool Protects(ProtectionProfile profile, BattleSlot source, BattleSlot target,
        BattleMove move)
    {
        profile = Validate(profile);
        ArgumentNullException.ThrowIfNull(move);
        if (profile.Scope != ProtectionScope.Personal || source == target || !IsActiveCreatureTarget(move.Target))
            return false;
        return profile.Filter switch
        {
            ProtectionFilter.All => true,
            ProtectionFilter.Priority => move.Priority > 0,
            ProtectionFilter.MultiTarget => move.Target is MoveTarget.AllOpponents
                or MoveTarget.AllOtherPokemon or MoveTarget.AllPokemon,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.Filter, "Unknown protection filter."),
        };
    }

    public static BattleHookDispatchSnapshot CollectHooks(
        IEnumerable<BattleConditionInstance> conditions,
        BattleConditionOwner owner,
        BattleSlot source,
        BattleSlot target,
        BattleMove move,
        bool bypass,
        int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(move);
        BattleConditionInstance? instance = Active(conditions, owner);
        BattleHookRegistration[] registrations = bypass
            || instance?.Definition.Protection is not { } profile
            || !Protects(profile, source, target, move)
                ? []
                : [BattleHookRegistration.ForCondition(instance, BattleConditionHook.TryHit, 0, 0,
                    new BattleHookFilter(new BattleHookFilterId("personal_protection"),
                        BattleHookFilterDecision.Deny))];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.TryHit), registrations);
    }

    public static bool UsesChain(ProtectionProfile profile, string ruleset)
    {
        profile = Validate(profile);
        if (!BattleRulesets.IsSupported(ruleset))
            throw new ArgumentException($"Unknown battle ruleset '{ruleset}'.", nameof(ruleset));
        return profile.Chain == ProtectionChainMode.Shared
            || profile.Chain == ProtectionChainMode.ClassicOnly && ruleset == BattleRulesets.Gen4Like;
    }

    public static double SuccessChance(ProtectionProfile profile, int chain, string ruleset)
    {
        if (chain < 0)
            throw new ArgumentOutOfRangeException(nameof(chain));
        if (!UsesChain(profile, ruleset))
            return 1;
        int factor = ruleset == BattleRulesets.Gen4Like ? 2 : 3;
        return 1 / Math.Pow(factor, Math.Min(chain, 20));
    }

    public static bool Succeeds(ProtectionProfile profile, int chain, string ruleset, IRng rng,
        out double? draw)
    {
        ArgumentNullException.ThrowIfNull(rng);
        double chance = SuccessChance(profile, chain, ruleset);
        if (chance == 1 && !profile.DrawGuaranteed)
        {
            draw = null;
            return true;
        }
        draw = rng.NextDouble();
        return draw < chance;
    }

    public static ProtectionProfile Validate(ProtectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Id.Value) || profile.Id.Value.Split(':')[0] != "protection")
            throw new ArgumentException("Protection condition IDs must use the protection family.", nameof(profile));
        if (!Enum.IsDefined(profile.Scope) || !Enum.IsDefined(profile.Filter) || !Enum.IsDefined(profile.Chain))
            throw new ArgumentException("Protection profile enums must be defined.", nameof(profile));
        if (profile.ContactEffects is null)
            throw new ArgumentException("Protection contact effects cannot be null.", nameof(profile));
        if (profile.Scope == ProtectionScope.Personal && profile.Filter != ProtectionFilter.All
            || profile.Scope == ProtectionScope.Side && profile.Filter == ProtectionFilter.All
            || profile.Scope == ProtectionScope.Side && profile.ContactEffects.Count > 0)
            throw new ArgumentException("Protection scope, filter, and contact payload are incompatible.", nameof(profile));
        foreach (ProtectionContactEffect effect in profile.ContactEffects)
        {
            bool valid = effect switch
            {
                ProtectionContactDamage(var fraction) => fraction.Num > 0 && fraction.Den > 0
                    && fraction.Num <= fraction.Den,
                ProtectionContactStatus(var status) => Enum.IsDefined(status),
                ProtectionContactStage(var stat, var delta) => Enum.IsDefined(stat) && stat != StatKind.Hp
                    && delta is >= -6 and < 0,
                _ => false,
            };
            if (!valid)
                throw new ArgumentException("Protection contact payload is invalid.", nameof(profile));
        }
        return profile;
    }

    public static bool Equivalent(ProtectionProfile left, ProtectionProfile right) =>
        left.Id == right.Id && left.Scope == right.Scope && left.Filter == right.Filter
        && left.Chain == right.Chain && left.DrawGuaranteed == right.DrawGuaranteed
        && left.ContactEffects.SequenceEqual(right.ContactEffects);

    internal static ProtectionProfile Normalize(ProtectionProfile profile)
    {
        profile = Validate(profile);
        return profile with
        {
            ContactEffects = new ReadOnlyCollection<ProtectionContactEffect>(profile.ContactEffects.ToArray()),
        };
    }

    private static BattleConditionId Id(string key)
    {
        if (!BattleConditionId.ValidToken(key))
            throw new ArgumentException("Protection keys must be lowercase tokens.", nameof(key));
        return new BattleConditionId($"protection:{key}");
    }

    private static bool IsActiveCreatureTarget(MoveTarget target) => target is not
        (MoveTarget.UsersField or MoveTarget.OpponentsField or MoveTarget.EntireField
            or MoveTarget.FaintingPokemon or MoveTarget.SpecificMove);
}
