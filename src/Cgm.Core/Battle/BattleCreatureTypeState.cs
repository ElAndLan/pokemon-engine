using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleTypeOperation { Replace, Add, Remove, Copy }
public enum BattleTypeSubject { User, Target }
public enum BattleTypeMutationFailure { None, Fainted, WouldEmptyTypes, ExceedsMax, NoChange }

public sealed record BattleTypeMutationResult(
    BattleTypeOperation Operation,
    BattleTypeMutationFailure Failure,
    IReadOnlyList<EntityId> Before,
    IReadOnlyList<EntityId> After)
{
    public bool Succeeded => Failure == BattleTypeMutationFailure.None;
}

/// <summary>Atomic runtime-only effective-creature-type mutation over immutable species/form definitions.</summary>
public sealed class BattleCreatureTypeState
{
    private readonly BattleOverlayStore _overlays;
    private readonly int _maxTypes;
    private readonly EntityId? _emptyFallback;

    public BattleCreatureTypeState(BattleOverlayStore overlays, int maxTypes = 3, EntityId? emptyFallback = null)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        if (maxTypes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTypes), "Maximum effective type count must be positive.");
        if (emptyFallback is { } fallback && (fallback == default || fallback.Category != EntityCategory.Type))
            throw new ArgumentException("Empty-type fallback must be a valid type ID.", nameof(emptyFallback));
        _overlays = overlays;
        _maxTypes = maxTypes;
        _emptyFallback = emptyFallback;
    }

    public IReadOnlyList<EntityId> Effective(BattleOverlayOwner owner, BattleEffectiveValues baseValues) =>
        _overlays.Resolve(owner, baseValues).Values.CreatureTypes;

    public BattleTypeMutationResult Mutate(
        BattleTypeOperation operation,
        IReadOnlyList<EntityId>? types,
        (BattleOverlayOwner Owner, BattleEffectiveValues Base, bool Fainted) subject,
        (BattleOverlayOwner Owner, BattleEffectiveValues Base, bool Fainted)? source,
        int turn,
        int actionSequence)
    {
        ValidateRequest(operation, types, subject, source, turn, actionSequence);
        if (subject.Fainted || (operation == BattleTypeOperation.Copy && source!.Value.Fainted))
            return Fail(operation, BattleTypeMutationFailure.Fainted);

        IReadOnlyList<EntityId> current = Effective(subject.Owner, subject.Base);
        IEnumerable<EntityId> computed = operation switch
        {
            BattleTypeOperation.Replace => types!,
            BattleTypeOperation.Add => current.Concat(types!),
            BattleTypeOperation.Remove => current.Where(type => !types!.Contains(type)),
            BattleTypeOperation.Copy => Effective(source!.Value.Owner, source.Value.Base),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        var result = new List<EntityId>();
        foreach (EntityId type in computed)
            if (!result.Contains(type))
                result.Add(type);
        if (result.Count == 0)
        {
            if (_emptyFallback is not { } fallback)
                return Fail(operation, BattleTypeMutationFailure.WouldEmptyTypes);
            result.Add(fallback);
        }
        if (result.Count > _maxTypes)
            return Fail(operation, BattleTypeMutationFailure.ExceedsMax);
        if (result.SequenceEqual(current))
            return Fail(operation, BattleTypeMutationFailure.NoChange);

        _overlays.Apply(new BattleOverlayApplication(subject.Owner, new BattleOverlaySource(),
            BattleOverlayLayer.PermanentInstance, new CreatureTypesOverlay(result), turn, actionSequence,
            Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));
        return new(operation, BattleTypeMutationFailure.None, current, result);
    }

    private static BattleTypeMutationResult Fail(BattleTypeOperation operation, BattleTypeMutationFailure failure) =>
        new(operation, failure, [], []);

    private static void ValidateRequest(BattleTypeOperation operation, IReadOnlyList<EntityId>? types,
        (BattleOverlayOwner Owner, BattleEffectiveValues Base, bool Fainted) subject,
        (BattleOverlayOwner Owner, BattleEffectiveValues Base, bool Fainted)? source, int turn, int actionSequence)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "Type mutation operation is invalid.");
        if (turn < 0 || actionSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(turn), "Type mutation time cannot be negative.");
        if (operation == BattleTypeOperation.Copy)
        {
            if (types is { Count: > 0 } || source is null)
                throw new ArgumentException("Type copy requires a source creature and no authored types.");
            if (source.Value.Owner.Side == subject.Owner.Side && source.Value.Owner.PartyIndex == subject.Owner.PartyIndex)
                throw new ArgumentException("Type copy source and subject must be distinct creatures.");
            return;
        }
        if (source is not null || types is not { Count: > 0 } || types.Distinct().Count() != types.Count
            || types.Any(type => type == default || type.Category != EntityCategory.Type))
            throw new ArgumentException("Type replace/add/remove requires a unique nonempty valid type list and no source.");
    }
}
