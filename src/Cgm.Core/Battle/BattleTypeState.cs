using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleTypeMutationFailure
{
    None,
    Fainted,
    MissingType,
    UnknownType,
    SameTypes,
    TypeLimit,
    MissingSource,
}

public sealed record BattleTypeMutationResult(
    BattleTypeOperation Operation,
    BattleTypeMutationFailure Failure,
    IReadOnlyList<(BattleOverlayOwner Owner, IReadOnlyList<EntityId> Before,
        IReadOnlyList<EntityId> After)> Changes,
    int? Draw = null,
    int? DrawBound = null)
{
    public bool Succeeded => Failure == BattleTypeMutationFailure.None;
}

public sealed record BattleMoveTypeOverrideResult(
    IReadOnlyList<(BattleOverlayOwner Owner, EntityId Type, EntityId? MatchType)> Changes);

/// <summary>Atomic runtime-only effective-type mutation over immutable creature and move definitions.</summary>
public sealed class BattleTypeState(BattleOverlayStore overlays, TypeChart chart)
{
    public const int MaximumTypes = 3;

    public IReadOnlyList<EntityId> Effective(BattleOverlayOwner owner, BattleEffectiveValues baseValues) =>
        overlays.Resolve(owner, baseValues).Values.CreatureTypes;

    public bool Requires(BattleTypeSubject subject, EntityId type,
        BattleOverlayOwner user, BattleEffectiveValues userBase,
        BattleOverlayOwner target, BattleEffectiveValues targetBase) =>
        Effective(subject == BattleTypeSubject.User ? user : target,
            subject == BattleTypeSubject.User ? userBase : targetBase).Contains(type);

    public BattleTypeMutationResult Mutate(TypeMutationEffect effect,
        BattleOverlayOwner user, BattleEffectiveValues userBase, bool userFainted,
        BattleOverlayOwner target, BattleEffectiveValues targetBase, bool targetFainted,
        BattleEnvironment environment, BattleActionHistory history, IRng rng,
        int turn, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(rng);
        (BattleOverlayOwner Owner, BattleEffectiveValues Base, bool Fainted) recipient =
            effect.Subject == BattleTypeSubject.User
                ? (user, userBase, userFainted) : (target, targetBase, targetFainted);
        if (recipient.Fainted)
            return Fail(effect.Operation, BattleTypeMutationFailure.Fainted);

        IReadOnlyList<EntityId> before = Effective(recipient.Owner, recipient.Base);
        IReadOnlyList<EntityId>? source = null;
        int? draw = null;
        int? drawBound = null;
        switch (effect.Source)
        {
            case BattleTypeSource.Fixed:
                source = effect.Type is { } fixedType ? [fixedType] : null;
                break;
            case BattleTypeSource.User:
                source = Effective(user, userBase);
                break;
            case BattleTypeSource.Target:
                source = Effective(target, targetBase);
                break;
            case BattleTypeSource.FirstMove:
                source = overlays.Resolve(user, userBase).Values.Moves.FirstOrDefault() is { } first
                    ? [first.Type] : null;
                break;
            case BattleTypeSource.Environment:
                source = effect.Environment?.GetValueOrDefault(environment) is { } environmentType
                    ? [environmentType] : null;
                break;
            case BattleTypeSource.ResistantToLastDamage:
                BattleDamageRecord? last = history.DamageSnapshot().LastOrDefault(record => record.Connected
                    && record.Target.Side == user.Side && record.Target.PartyIndex == user.PartyIndex);
                if (last is not null)
                {
                    EntityId[] eligible = chart.TypeIds.Where(type => !before.Contains(type)
                            && chart.SingleValue(last.DamageType, type).Numerator
                                < chart.SingleValue(last.DamageType, type).Denominator)
                        .ToArray();
                    if (eligible.Length > 0)
                    {
                        drawBound = eligible.Length > 1 ? eligible.Length : null;
                        draw = drawBound is { } bound ? rng.Next(bound) : null;
                        source = [eligible[draw ?? 0]];
                    }
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect.Source, "Unknown type source.");
        }
        if (source is null || source.Count == 0)
            return Fail(effect.Operation, BattleTypeMutationFailure.MissingSource);
        if (source.Any(type => !chart.Contains(type))
            || effect.FallbackType is { } fallback && !chart.Contains(fallback))
            return Fail(effect.Operation, BattleTypeMutationFailure.UnknownType);

        IReadOnlyList<EntityId> after;
        switch (effect.Operation)
        {
            case BattleTypeOperation.Replace:
            case BattleTypeOperation.Copy:
                after = Unique(source);
                if (after.Count > MaximumTypes)
                    return Fail(effect.Operation, BattleTypeMutationFailure.TypeLimit);
                if (before.SequenceEqual(after))
                    return Fail(effect.Operation, BattleTypeMutationFailure.SameTypes);
                break;
            case BattleTypeOperation.Add:
                EntityId added = source.Single();
                if (before.Contains(added))
                    return Fail(effect.Operation, BattleTypeMutationFailure.SameTypes);
                if (before.Count >= MaximumTypes)
                    return Fail(effect.Operation, BattleTypeMutationFailure.TypeLimit);
                after = [.. before, added];
                break;
            case BattleTypeOperation.Remove:
                EntityId removed = source.Single();
                if (!before.Contains(removed))
                    return effect.Required ? Fail(effect.Operation, BattleTypeMutationFailure.MissingType)
                        : new(effect.Operation, BattleTypeMutationFailure.None, [], draw, drawBound);
                EntityId[] remaining = before.Where(type => type != removed).ToArray();
                after = remaining.Length > 0 ? remaining : [effect.FallbackType!.Value];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect.Operation, "Unknown type operation.");
        }

        BattleOverlayPayload payload = effect.Operation == BattleTypeOperation.Add
            ? new TypeAdditionOverlay($"type_{source.Single().Slug}", [source.Single()])
            : new CreatureTypesOverlay(after);
        BattleOverlayLayer layer = effect.Operation == BattleTypeOperation.Add
            ? BattleOverlayLayer.Additive : BattleOverlayLayer.PermanentInstance;
        if (effect.Operation != BattleTypeOperation.Add)
            overlays.RemoveTypeAdditions(recipient.Owner, turn, actionSequence);
        overlays.Apply(new BattleOverlayApplication(recipient.Owner,
            new BattleOverlaySource(user.Slot, user.PartyIndex), layer, payload, turn, actionSequence,
            effect.Duration, effect.Duration is null ? null : BattleIntentCheckpoint.TurnEnd,
            BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));
        return new(effect.Operation, BattleTypeMutationFailure.None,
            [(recipient.Owner, before.ToArray(), after.ToArray())], draw, drawBound);
    }

    public BattleMoveTypeOverrideResult ApplyOverride(MoveTypeOverrideEffect effect,
        IEnumerable<BattleOverlayOwner> owners, BattleOverlaySource source, int turn, int actionSequence)
    {
        var changes = new List<(BattleOverlayOwner Owner, EntityId Type, EntityId? MatchType)>();
        foreach (BattleOverlayOwner owner in owners.OrderBy(owner => owner.Side).ThenBy(owner => owner.PartyIndex))
        {
            overlays.Apply(new BattleOverlayApplication(owner, source, BattleOverlayLayer.Additive,
                new MoveTypeRuleOverlay($"move_type_{actionSequence}",
                    new BattleMoveTypeRule(effect.Type, effect.MatchType)), turn, actionSequence,
                effect.Duration, BattleIntentCheckpoint.TurnEnd,
                BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));
            changes.Add((owner, effect.Type, effect.MatchType));
        }
        return new(changes);
    }

    private static IReadOnlyList<EntityId> Unique(IEnumerable<EntityId> types) =>
        types.Distinct().ToArray();

    private static BattleTypeMutationResult Fail(BattleTypeOperation operation,
        BattleTypeMutationFailure failure) => new(operation, failure, []);
}
