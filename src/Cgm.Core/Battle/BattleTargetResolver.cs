using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleTargetScopeKind { ActiveSlots, Side, Field, FaintedParty, MoveReference }

public enum BattleTargetSelection { Fixed, RandomOpponent }

public readonly record struct BattleSlot(BattleSide Side, int Position);

/// <summary>Immutable active-slot layout. Slot order is Player then Enemy, with each side ordered by position.</summary>
public sealed class BattleTopology
{
    public static BattleTopology Singles { get; } = new(1);
    public static BattleTopology Doubles { get; } = new(2);

    public BattleTopology(int activeSlotsPerSide)
    {
        if (activeSlotsPerSide is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(activeSlotsPerSide), "Only singles and doubles are supported.");

        ActiveSlotsPerSide = activeSlotsPerSide;
        Slots = Array.AsReadOnly(Enum.GetValues<BattleSide>()
            .SelectMany(side => Enumerable.Range(0, activeSlotsPerSide).Select(position => new BattleSlot(side, position)))
            .ToArray());
    }

    public int ActiveSlotsPerSide { get; }
    public IReadOnlyList<BattleSlot> Slots { get; }

    public bool Contains(BattleSlot slot) =>
        Enum.IsDefined(slot.Side) && slot.Position >= 0 && slot.Position < ActiveSlotsPerSide;

    public IReadOnlyList<BattleSlot> SlotsFor(BattleSide side) =>
        Slots.Where(slot => slot.Side == side).ToArray();
}

public readonly record struct BattleTargetScope(
    BattleTargetScopeKind Kind,
    IReadOnlyList<BattleSlot> Slots,
    BattleSide? Side = null,
    BattleTargetSelection Selection = BattleTargetSelection.Fixed);

public static class BattleTargetResolver
{
    private static readonly IReadOnlyList<BattleSlot> EmptySlots = Array.Empty<BattleSlot>();

    /// <summary>Resolves an authored target shape without inspecting mutable battle state or drawing RNG.</summary>
    public static BattleTargetScope ResolveScope(
        MoveTarget target,
        BattleTopology topology,
        BattleSlot source,
        BattleSlot? selected = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (!topology.Contains(source))
            throw new ArgumentOutOfRangeException(nameof(source), source, "Source slot is outside the battle topology.");
        if (selected is { } selection && !topology.Contains(selection))
            throw new ArgumentOutOfRangeException(nameof(selected), selected, "Selected slot is outside the battle topology.");

        IReadOnlyList<BattleSlot> ownSlots = topology.SlotsFor(source.Side);
        IReadOnlyList<BattleSlot> opponentSlots = topology.SlotsFor(Opponent(source.Side));
        IReadOnlyList<BattleSlot> allies = ownSlots.Where(slot => slot != source).ToArray();

        return target switch
        {
            MoveTarget.User => Active([source]),
            MoveTarget.AllOpponents => Active(opponentSlots),
            MoveTarget.AllOtherPokemon => Active(topology.Slots.Where(slot => slot != source).ToArray()),
            MoveTarget.AllPokemon => Active(topology.Slots),
            MoveTarget.AllAllies => Active(allies),
            MoveTarget.UserAndAllies => Active(ownSlots),
            MoveTarget.RandomOpponent => new BattleTargetScope(
                BattleTargetScopeKind.ActiveSlots, opponentSlots, Selection: BattleTargetSelection.RandomOpponent),
            MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst => Active(RequiredSelected(selected, target)),
            MoveTarget.Ally => Active(RequiredAlly(selected, source)),
            MoveTarget.UserOrAlly => Active(SelectedUserOrAlly(selected, source, ownSlots)),
            MoveTarget.UsersField => new BattleTargetScope(BattleTargetScopeKind.Side, EmptySlots, source.Side),
            MoveTarget.OpponentsField => new BattleTargetScope(BattleTargetScopeKind.Side, EmptySlots, Opponent(source.Side)),
            MoveTarget.EntireField => new BattleTargetScope(BattleTargetScopeKind.Field, EmptySlots),
            MoveTarget.FaintingPokemon => new BattleTargetScope(BattleTargetScopeKind.FaintedParty, EmptySlots, source.Side),
            MoveTarget.SpecificMove => new BattleTargetScope(BattleTargetScopeKind.MoveReference, EmptySlots),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move target."),
        };
    }

    /// <summary>Compatibility adapter for existing one-active-per-side battle resolution.</summary>
    public static BattleTargetScope ResolveSinglesScope(MoveTarget target, BattleSide sourceSide) =>
        ResolveScope(target, BattleTopology.Singles, new BattleSlot(sourceSide, 0),
            target is MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst
                ? new BattleSlot(Opponent(sourceSide), 0)
                : null);

    public static bool IsSinglesActiveCreatureTarget(MoveTarget target) => target switch
    {
        MoveTarget.User or MoveTarget.Selected or MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon => true,
        MoveTarget.UsersField or MoveTarget.EntireField => false,
        _ when Enum.IsDefined(target) => throw new InvalidOperationException(
            $"Move target '{target}' requires the Phase 15B topology-aware action resolver."),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move target."),
    };

    public static BattleSide ResolveSinglesActiveCreatureSide(MoveTarget target, BattleSide sourceSide) => target switch
    {
        MoveTarget.User => sourceSide,
        MoveTarget.Selected or MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon => Opponent(sourceSide),
        MoveTarget.UsersField or MoveTarget.EntireField =>
            throw new InvalidOperationException($"Move target '{target}' does not resolve to an active creature."),
        _ when Enum.IsDefined(target) => throw new InvalidOperationException(
            $"Move target '{target}' requires the Phase 15B topology-aware action resolver."),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move target."),
    };

    private static BattleTargetScope Active(IReadOnlyList<BattleSlot> slots) =>
        new(BattleTargetScopeKind.ActiveSlots, slots);

    private static IReadOnlyList<BattleSlot> RequiredSelected(BattleSlot? selected, MoveTarget target) =>
        selected is { } slot
            ? [slot]
            : throw new ArgumentException($"Move target '{target}' requires a selected active slot.", nameof(selected));

    private static IReadOnlyList<BattleSlot> RequiredAlly(BattleSlot? selected, BattleSlot source)
    {
        BattleSlot slot = RequiredSelected(selected, MoveTarget.Ally)[0];
        if (slot.Side != source.Side || slot == source)
            throw new ArgumentException("An ally target must be a different active slot on the source side.", nameof(selected));
        return [slot];
    }

    private static IReadOnlyList<BattleSlot> SelectedUserOrAlly(
        BattleSlot? selected,
        BattleSlot source,
        IReadOnlyList<BattleSlot> ownSlots)
    {
        if (ownSlots.Count == 1)
            return [source];

        BattleSlot slot = RequiredSelected(selected, MoveTarget.UserOrAlly)[0];
        if (slot.Side != source.Side)
            throw new ArgumentException("A user-or-ally target must be on the source side.", nameof(selected));
        return [slot];
    }

    private static BattleSide Opponent(BattleSide side) =>
        side == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
}
