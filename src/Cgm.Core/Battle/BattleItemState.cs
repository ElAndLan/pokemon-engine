using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleItemOperation { Consume, Give, Steal, Swap, Remove, Destroy, Restore, Suppress }
public enum BattleItemSubject { User, Target }
public enum BattleItemRequirement { Held, Empty, Consumed }
public enum BattleItemMutationFailure
{
    None,
    Fainted,
    MissingItem,
    Occupied,
    SameItem,
    UnknownItem,
    NotHoldable,
    Protected,
    NoConsumptionHistory,
}

public sealed record BattleItemConsumption(
    EntityId Item,
    BattleSide Side,
    int PartyIndex,
    int Turn,
    string Cause);

public sealed record BattleItemMutationResult(
    BattleItemOperation Operation,
    BattleItemMutationFailure Failure,
    IReadOnlyList<(BattleOverlayOwner Owner, EntityId? Before, EntityId? After)> Changes,
    BattleItemConsumption? Consumption = null)
{
    public bool Succeeded => Failure == BattleItemMutationFailure.None;
}

/// <summary>Atomic runtime-only held-item ownership and consumption history.</summary>
public sealed class BattleItemState(
    BattleOverlayStore overlays,
    IReadOnlyDictionary<EntityId, Item> catalog)
{
    private readonly Dictionary<(BattleSide Side, int PartyIndex), BattleItemConsumption> _consumed = [];

    public BattleItemConsumption? LastConsumed(BattleOverlayOwner owner) =>
        _consumed.GetValueOrDefault((owner.Side, owner.PartyIndex));

    public bool Meets(BattleOverlayOwner owner, BattleEffectiveValues baseValues,
        BattleItemRequirement requirement) => requirement switch
    {
        BattleItemRequirement.Held => Effective(owner, baseValues) is not null,
        BattleItemRequirement.Empty => Effective(owner, baseValues) is null,
        BattleItemRequirement.Consumed => LastConsumed(owner) is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unknown held-item requirement."),
    };

    public BattleItemMutationResult Mutate(
        BattleItemOperation operation,
        BattleOverlayOwner user,
        BattleEffectiveValues userBase,
        bool userFainted,
        BattleOverlayOwner target,
        BattleEffectiveValues targetBase,
        bool targetFainted,
        int turn,
        int actionSequence,
        string cause,
        int? suppressionDuration = null,
        Func<BattleOverlayOwner, BattleItemOperation, bool>? stickyProtection = null)
    {
        ValidateRequest(operation, user, target, turn, actionSequence, cause, suppressionDuration);
        BattleOverlayOwner subject = operation is BattleItemOperation.Steal or BattleItemOperation.Remove
            or BattleItemOperation.Destroy ? target : user;
        BattleEffectiveValues subjectBase = subject == user ? userBase : targetBase;
        bool subjectFainted = subject == user ? userFainted : targetFainted;
        EntityId? userItem = Effective(user, userBase);
        EntityId? targetItem = Effective(target, targetBase);

        bool transfer = operation is BattleItemOperation.Give or BattleItemOperation.Steal or BattleItemOperation.Swap;
        if (subjectFainted || transfer && (userFainted || targetFainted))
            return Fail(operation, BattleItemMutationFailure.Fainted);

        if (operation == BattleItemOperation.Suppress)
        {
            if (subjectFainted)
                return Fail(operation, BattleItemMutationFailure.Fainted);
            Apply(subject, new SuppressionOverlay(BattleEffectiveValueKind.HeldItem),
                BattleOverlayLayer.Suppression, turn, actionSequence, suppressionDuration,
                BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd);
            return new(operation, BattleItemMutationFailure.None, [(subject, Effective(subject, subjectBase), null)]);
        }

        EntityId? origin = operation switch
        {
            BattleItemOperation.Give or BattleItemOperation.Swap or BattleItemOperation.Consume => userItem,
            BattleItemOperation.Steal or BattleItemOperation.Remove or BattleItemOperation.Destroy => targetItem,
            BattleItemOperation.Restore => LastConsumed(user)?.Item,
            _ => null,
        };
        if (operation == BattleItemOperation.Restore && origin is null)
            return Fail(operation, BattleItemMutationFailure.NoConsumptionHistory);
        if (operation is not BattleItemOperation.Restore && origin is null)
            return Fail(operation, BattleItemMutationFailure.MissingItem);
        Item? definition = null;
        if (origin is { } item && !catalog.TryGetValue(item, out definition))
            return Fail(operation, BattleItemMutationFailure.UnknownItem);
        if (definition is { Holdable: false })
            return Fail(operation, BattleItemMutationFailure.NotHoldable);
        if (operation is BattleItemOperation.Give or BattleItemOperation.Steal or BattleItemOperation.Restore
            && (operation == BattleItemOperation.Give ? targetItem : userItem) is not null)
            return Fail(operation, BattleItemMutationFailure.Occupied);
        if (operation == BattleItemOperation.Swap && (userItem is null || targetItem is null))
            return Fail(operation, BattleItemMutationFailure.MissingItem);
        if (operation == BattleItemOperation.Swap && userItem == targetItem)
            return Fail(operation, BattleItemMutationFailure.SameItem);

        Item? swapTarget = null;
        if (operation == BattleItemOperation.Swap
            && (!catalog.TryGetValue(targetItem!.Value, out swapTarget) || swapTarget is null))
            return Fail(operation, BattleItemMutationFailure.UnknownItem);
        if (swapTarget is { Holdable: false })
            return Fail(operation, BattleItemMutationFailure.NotHoldable);

        bool protectedItem = origin is { } protectedId && Protected(protectedId, operation)
            || operation == BattleItemOperation.Swap && Protected(targetItem!.Value, operation);
        bool sticky = operation is BattleItemOperation.Steal or BattleItemOperation.Swap
            or BattleItemOperation.Remove or BattleItemOperation.Destroy
            && stickyProtection?.Invoke(target, operation) == true;
        if (protectedItem || sticky)
            return Fail(operation, BattleItemMutationFailure.Protected);

        var changes = new List<(BattleOverlayOwner, EntityId?, EntityId?)>();
        BattleItemConsumption? consumption = null;
        switch (operation)
        {
            case BattleItemOperation.Consume:
                Apply(user, new HeldItemOverlay(null), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                consumption = new(origin!.Value, user.Side, user.PartyIndex, turn, cause);
                _consumed[(user.Side, user.PartyIndex)] = consumption;
                changes.Add((user, userItem, null));
                break;
            case BattleItemOperation.Give:
                Apply(user, new HeldItemOverlay(null), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                Apply(target, new HeldItemOverlay(userItem), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                changes.Add((user, userItem, null));
                changes.Add((target, null, userItem));
                break;
            case BattleItemOperation.Steal:
                Apply(target, new HeldItemOverlay(null), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                Apply(user, new HeldItemOverlay(targetItem), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                changes.Add((user, null, targetItem));
                changes.Add((target, targetItem, null));
                break;
            case BattleItemOperation.Swap:
                Apply(user, new HeldItemOverlay(targetItem), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                Apply(target, new HeldItemOverlay(userItem), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                changes.Add((user, userItem, targetItem));
                changes.Add((target, targetItem, userItem));
                break;
            case BattleItemOperation.Remove:
            case BattleItemOperation.Destroy:
                Apply(target, new HeldItemOverlay(null), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                changes.Add((target, targetItem, null));
                break;
            case BattleItemOperation.Restore:
                Apply(user, new HeldItemOverlay(origin), BattleOverlayLayer.PermanentInstance, turn, actionSequence);
                _consumed.Remove((user.Side, user.PartyIndex));
                changes.Add((user, null, origin));
                break;
        }
        return new(operation, BattleItemMutationFailure.None,
            changes.OrderBy(change => change.Item1.Side).ThenBy(change => change.Item1.PartyIndex).ToArray(), consumption);
    }

    public void EndBattle() => _consumed.Clear();

    private EntityId? Effective(BattleOverlayOwner owner, BattleEffectiveValues baseValues) =>
        overlays.Resolve(owner, baseValues).Values.HeldItem;

    private bool Protected(EntityId item, BattleItemOperation operation)
    {
        Item definition = catalog[item];
        if (definition.KeyItem)
            return true;
        return definition.BattleEffects.Any(effect => effect.Op == "itemMutationGuard"
            && Operations(effect).Contains(operation));
    }

    internal static IReadOnlySet<BattleItemOperation> Operations(Effect effect)
    {
        if (effect.Params is null || !effect.Params.TryGetValue("operations", out var value)
            || value.ValueKind != System.Text.Json.JsonValueKind.String)
            return new HashSet<BattleItemOperation>();
        return value.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Enum.Parse<BattleItemOperation>(value, true)).ToHashSet();
    }

    private void Apply(BattleOverlayOwner owner, BattleOverlayPayload payload, BattleOverlayLayer layer,
        int turn, int actionSequence, int? duration = null,
        BattleOverlayCleanup cleanup = BattleOverlayCleanup.BattleEnd) =>
        overlays.Apply(new BattleOverlayApplication(owner, new BattleOverlaySource(), layer, payload,
            turn, actionSequence, duration,
            duration is null ? null : BattleIntentCheckpoint.TurnEnd, cleanup));

    private static BattleItemMutationResult Fail(BattleItemOperation operation, BattleItemMutationFailure failure) =>
        new(operation, failure, []);

    private static void ValidateRequest(BattleItemOperation operation, BattleOverlayOwner user,
        BattleOverlayOwner target, int turn, int actionSequence, string cause, int? suppressionDuration)
    {
        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation));
        if (turn < 0 || actionSequence < 0 || string.IsNullOrWhiteSpace(cause)
            || !BattleConditionId.ValidToken(cause))
            throw new ArgumentException("Item mutation requires nonnegative time and a lowercase token cause.");
        if (operation == BattleItemOperation.Suppress != suppressionDuration.HasValue
            || suppressionDuration is <= 0 or > 16)
            throw new ArgumentException("Only suppression requires a duration in 1..16.");
        if (user.Side == target.Side && user.PartyIndex == target.PartyIndex
            && operation is BattleItemOperation.Give or BattleItemOperation.Steal or BattleItemOperation.Swap)
            throw new ArgumentException("Transfer operations require two distinct creatures.");
    }
}
