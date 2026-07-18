using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record BattleMoveIdentityQueryResult(
    EntityId AuthoredType,
    EntityId EffectiveType,
    DamageClass AuthoredClass,
    DamageClass EffectiveClass,
    BattleEnvironment NaturalEnvironment,
    BattleEnvironment EffectiveEnvironment);

public sealed record BattleDamageQueryResult(
    BattleMoveIdentityQueryResult Identity,
    DamageStatSelector Offensive,
    DamageStatSelector Defensive,
    BattleQueryValue Stab,
    BattleQueryResult Effectiveness,
    bool Spread);

public sealed record BattleDamageQueryTraceEntry(
    int Turn,
    int ActionSequence,
    BattleSlot SourceSlot,
    BattleSlot TargetSlot,
    BattleDamageQueryResult Result);

public static class BattleDamageQueries
{
    public static BattleMoveIdentityQueryResult Identity(BattleMove move, int moveSlot,
        BattleCreature source, BattleEffectiveValues sourceValues, BattleEnvironmentState environment,
        EntityId? conditionType = null)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceValues);
        ValidateType(move.Type, nameof(move));
        if (!Enum.IsDefined(move.DamageClass))
            throw new ArgumentOutOfRangeException(nameof(move), move.DamageClass, "Unknown authored damage class.");
        if (moveSlot < 0 || moveSlot >= sourceValues.Moves.Count)
            throw new ArgumentOutOfRangeException(nameof(moveSlot), "Move slot is outside the effective move list.");

        BattleEffectiveMove effectiveMove = sourceValues.Moves[moveSlot];
        ValidateType(effectiveMove.Type, nameof(sourceValues));
        if (!Enum.IsDefined(effectiveMove.DamageClass))
            throw new ArgumentOutOfRangeException(nameof(sourceValues), effectiveMove.DamageClass,
                "Unknown effective damage class.");
        if (conditionType is { } replacementType)
            ValidateType(replacementType, nameof(conditionType));
        EntityId type = conditionType ?? effectiveMove.Type;
        DamageClass damageClass = effectiveMove.DamageClass;
        if (move.SecondaryEffects.OfType<DamageClassQueryEffect>().SingleOrDefault() is { } classQuery)
        {
            damageClass = classQuery.Mode switch
            {
                DamageClassQueryMode.Physical => DamageClass.Physical,
                DamageClassQueryMode.Special => DamageClass.Special,
                DamageClassQueryMode.HigherOffense => HigherOffense(source, sourceValues.Stats),
                _ => throw new ArgumentOutOfRangeException(nameof(classQuery), classQuery.Mode,
                    "Unknown damage-class query mode."),
            };
        }
        return new BattleMoveIdentityQueryResult(move.Type, type, move.DamageClass, damageClass,
            environment.Natural, environment.Effective);
    }

    public static BattleDamageQueryResult Resolve(BattleMove move,
        BattleMoveIdentityQueryResult identity, BattleCreature source, BattleCreature target,
        BattleEffectiveValues sourceValues, BattleEffectiveValues targetValues, TypeChart chart,
        int snapshottedLiveTargets, BattleQueryContext context,
        IEnumerable<BattleQueryModifier>? effectivenessModifiers = null)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceValues);
        ArgumentNullException.ThrowIfNull(targetValues);
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(context);
        ValidateType(identity.EffectiveType, nameof(identity));
        if (!Enum.IsDefined(identity.EffectiveClass))
            throw new ArgumentOutOfRangeException(nameof(identity), identity.EffectiveClass,
                "Unknown effective damage class.");
        if (snapshottedLiveTargets <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshottedLiveTargets));

        DamageStatQueryEffect? statQuery = move.SecondaryEffects.OfType<DamageStatQueryEffect>().SingleOrDefault();
        DamageStatSelector offensive = statQuery?.Offensive
            ?? (move.OffensiveStatOverride is { } offensiveStat
                ? new DamageStatSelector(DamageQueryOwner.User, offensiveStat) : null)
            ?? new DamageStatSelector(DamageQueryOwner.User,
                identity.EffectiveClass == DamageClass.Physical ? StatKind.Atk : StatKind.Spa);
        DamageStatSelector defensive = statQuery?.Defensive
            ?? (move.DefensiveStatOverride is { } defensiveStat
                ? new DamageStatSelector(DamageQueryOwner.Target, defensiveStat) : null)
            ?? new DamageStatSelector(DamageQueryOwner.Target,
                identity.EffectiveClass == DamageClass.Physical ? StatKind.Def : StatKind.Spd);
        EffectivenessQueryEffect profile = move.SecondaryEffects.OfType<EffectivenessQueryEffect>()
            .SingleOrDefault() ?? new EffectivenessQueryEffect(EffectivenessQueryMode.Standard, null, null, null);
        ValidateSelector(offensive, nameof(move), offensive: true);
        ValidateSelector(defensive, nameof(move), offensive: false);
        ValidateProfile(profile);
        foreach (EntityId defenderType in targetValues.CreatureTypes)
            ValidateType(defenderType, nameof(targetValues));
        foreach (EntityId stabType in sourceValues.CreatureTypes)
            ValidateType(stabType, nameof(sourceValues));
        BattleQueryValue authoredEffectiveness = Effectiveness(chart, identity.EffectiveType,
            targetValues.CreatureTypes, profile);
        BattleQueryResult effectiveness = BattleQuery.Evaluate(BattleQueryId.Effectiveness,
            authoredEffectiveness, effectivenessModifiers, context);
        IReadOnlyList<EntityId> stabTypes = profile.StabSource switch
        {
            StabQuerySource.User => sourceValues.CreatureTypes,
            StabQuerySource.Target => targetValues.CreatureTypes,
            StabQuerySource.None => [],
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.StabSource,
                "Unknown STAB query source."),
        };
        BattleQueryValue stab = profile.StabSource != StabQuerySource.None
            && stabTypes.Contains(identity.EffectiveType) ? new BattleQueryValue(3, 2) : new BattleQueryValue(1);
        return new BattleDamageQueryResult(identity, offensive, defensive, stab, effectiveness,
            snapshottedLiveTargets > 1);
    }

    public static BattleCreature Owner(DamageStatSelector selector, BattleCreature source,
        BattleCreature target) => selector.Owner switch
    {
        DamageQueryOwner.User => source,
        DamageQueryOwner.Target => target,
        _ => throw new ArgumentOutOfRangeException(nameof(selector), selector.Owner, "Unknown damage-stat owner."),
    };

    public static BattleEffectiveValues Owner(DamageStatSelector selector, BattleEffectiveValues source,
        BattleEffectiveValues target) => selector.Owner switch
    {
        DamageQueryOwner.User => source,
        DamageQueryOwner.Target => target,
        _ => throw new ArgumentOutOfRangeException(nameof(selector), selector.Owner, "Unknown damage-stat owner."),
    };

    public static BattleSlot Owner(DamageStatSelector selector, BattleSlot source, BattleSlot target) =>
        selector.Owner switch
        {
            DamageQueryOwner.User => source,
            DamageQueryOwner.Target => target,
            _ => throw new ArgumentOutOfRangeException(nameof(selector), selector.Owner,
                "Unknown damage-stat owner."),
        };

    private static void ValidateSelector(DamageStatSelector selector, string paramName, bool offensive)
    {
        if (!Enum.IsDefined(selector.Owner))
            throw new ArgumentOutOfRangeException(paramName, selector.Owner, "Unknown damage-stat owner.");
        bool allowed = offensive
            ? selector.Stat is StatKind.Atk or StatKind.Def or StatKind.Spa or StatKind.Spd
            : selector.Stat is StatKind.Def or StatKind.Spd;
        if (!allowed)
            throw new ArgumentOutOfRangeException(paramName, selector.Stat,
                offensive ? "Unknown offensive damage stat." : "Unknown defensive damage stat.");
    }

    private static void ValidateProfile(EffectivenessQueryEffect profile)
    {
        if (!Enum.IsDefined(profile.Mode))
            throw new ArgumentOutOfRangeException(nameof(profile), profile.Mode,
                "Unknown effectiveness query mode.");
        if (!Enum.IsDefined(profile.StabSource))
            throw new ArgumentOutOfRangeException(nameof(profile), profile.StabSource,
                "Unknown STAB query source.");
        if (profile.AdditionalType is { } additionalType)
            ValidateType(additionalType, nameof(profile));
        if (profile.DefendingType is { } defendingType)
            ValidateType(defendingType, nameof(profile));
        if (profile.DefendingType.HasValue != profile.DefendingTypeMultiplier.HasValue)
            throw new ArgumentException("A defending-type query requires both its type and multiplier.",
                nameof(profile));
        if (profile.DefendingTypeMultiplier is { Numerator: <= 0 })
            throw new ArgumentOutOfRangeException(nameof(profile),
                "Effectiveness override multipliers must be positive.");
    }

    private static void ValidateType(EntityId type, string paramName)
    {
        if (type.Category != EntityCategory.Type)
            throw new ArgumentException($"'{type}' is not a type EntityId.", paramName);
    }

    private static BattleQueryValue Effectiveness(TypeChart chart, EntityId moveType,
        IReadOnlyList<EntityId> defenderTypes, EffectivenessQueryEffect profile)
    {
        BattleQueryValue result = new(1);
        foreach (EntityId defenderType in defenderTypes)
        {
            BattleQueryValue primary = profile.DefendingType == defenderType
                ? profile.DefendingTypeMultiplier!.Value : chart.SingleValue(moveType, defenderType);
            result = TypeChart.Multiply(result, Profile(primary, profile.Mode));
            if (profile.AdditionalType is { } additional)
                result = TypeChart.Multiply(result, Profile(chart.SingleValue(additional, defenderType), profile.Mode));
        }
        return result;
    }

    private static BattleQueryValue Profile(BattleQueryValue value, EffectivenessQueryMode mode) => mode switch
    {
        EffectivenessQueryMode.Standard => value,
        EffectivenessQueryMode.Neutral => new BattleQueryValue(1),
        EffectivenessQueryMode.Inverse when value.Numerator == 0 => new BattleQueryValue(2),
        EffectivenessQueryMode.Inverse when Compare(value, new BattleQueryValue(1)) < 0 => new BattleQueryValue(2),
        EffectivenessQueryMode.Inverse when Compare(value, new BattleQueryValue(1)) > 0 => new BattleQueryValue(1, 2),
        EffectivenessQueryMode.Inverse => new BattleQueryValue(1),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown effectiveness query mode."),
    };

    private static int Compare(BattleQueryValue left, BattleQueryValue right) =>
        checked(left.Numerator * right.Denominator).CompareTo(checked(right.Numerator * left.Denominator));

    private static DamageClass HigherOffense(BattleCreature source, Stats stats)
    {
        int attack = BattleQuery.ResolveInteger(BattleQueryId.OffensiveStat, stats.Atk,
            [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.StatStageMultiplier(source.Stage(StatKind.Atk)), InsertionOrder: 0)]);
        int specialAttack = BattleQuery.ResolveInteger(BattleQueryId.OffensiveStat, stats.Spa,
            [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.StatStageMultiplier(source.Stage(StatKind.Spa)), InsertionOrder: 0)]);
        return attack > specialAttack ? DamageClass.Physical : DamageClass.Special;
    }
}
