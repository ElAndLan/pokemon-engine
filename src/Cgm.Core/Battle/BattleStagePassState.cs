namespace Cgm.Core.Battle;

public enum BattleStagePassFailure { None, Missing, SameCreature, SourceFainted, TargetFainted }

public sealed record BattleStagePassChange(StatKind Stat, int Before, int After);

public sealed record BattleStagePassResult(
    BattleStagePassFailure Failure,
    IReadOnlyList<BattleStagePassChange> Changes)
{
    public bool Succeeded => Failure == BattleStagePassFailure.None;
}

public sealed class BattleStagePassState
{
    private static readonly StatKind[] Slots =
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe, StatKind.Accuracy, StatKind.Evasion];
    private readonly Dictionary<(BattleSide Side, int PartyIndex), IReadOnlyList<int>> _pending = [];

    public BattleStagePassFailure Capture(BattleOverlayOwner owner, BattleCreature source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var key = Key(owner);
        if (source.IsFainted)
        {
            _pending.Remove(key);
            return BattleStagePassFailure.SourceFainted;
        }

        _pending[key] = Array.AsReadOnly(Slots.Select(source.Stage).ToArray());
        return BattleStagePassFailure.None;
    }

    public BattleStagePassResult Consume(
        BattleOverlayOwner sourceOwner,
        BattleOverlayOwner targetOwner,
        BattleCreature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var sourceKey = Key(sourceOwner);
        if (!_pending.Remove(sourceKey, out IReadOnlyList<int>? stages))
            return new BattleStagePassResult(BattleStagePassFailure.Missing, []);
        if (sourceKey == Key(targetOwner))
            return new BattleStagePassResult(BattleStagePassFailure.SameCreature, []);
        if (target.IsFainted)
            return new BattleStagePassResult(BattleStagePassFailure.TargetFainted, []);

        var changes = new List<BattleStagePassChange>();
        for (int i = 0; i < Slots.Length; i++)
        {
            int before = target.Stage(Slots[i]);
            target.SetStage(Slots[i], stages[i]);
            int after = target.Stage(Slots[i]);
            if (after != before)
                changes.Add(new BattleStagePassChange(Slots[i], before, after));
        }
        return new BattleStagePassResult(BattleStagePassFailure.None, changes);
    }

    public bool Discard(BattleOverlayOwner owner) => _pending.Remove(Key(owner));

    public bool OwnerFainted(BattleSide side, int partyIndex) => _pending.Remove(Key(side, partyIndex));

    public int EndBattle()
    {
        int count = _pending.Count;
        _pending.Clear();
        return count;
    }

    public bool HasPending(BattleOverlayOwner owner) => _pending.ContainsKey(Key(owner));

    private static (BattleSide Side, int PartyIndex) Key(BattleOverlayOwner owner) =>
        Key(owner.Side, owner.PartyIndex);

    private static (BattleSide Side, int PartyIndex) Key(BattleSide side, int partyIndex)
    {
        if (!Enum.IsDefined(side))
            throw new ArgumentOutOfRangeException(nameof(side));
        if (partyIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(partyIndex));
        return (side, partyIndex);
    }
}
