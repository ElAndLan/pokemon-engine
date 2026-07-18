using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleIntentQueueTests
{
    private static readonly EntityId Move = EntityId.Parse("move:neutral_intent");
    private static readonly BattleSlot Player0 = new(BattleSide.Player, 0);
    private static readonly BattleSlot Player1 = new(BattleSide.Player, 1);

    [Fact]
    public void PreviewOrdersDueCheckpointSequenceWithoutMutation()
    {
        var queue = new BattleIntentQueue();
        BattleIntent later = queue.Enqueue(Request(2, BattleIntentCheckpoint.PreAction));
        BattleIntent first = queue.Enqueue(Request(1, BattleIntentCheckpoint.PreAction));
        BattleIntent second = queue.Enqueue(Request(1, BattleIntentCheckpoint.PreAction));
        queue.Enqueue(Request(1, BattleIntentCheckpoint.TurnEnd));

        BattleIntentPreview preview = queue.Preview(2, BattleIntentCheckpoint.PreAction);

        Assert.Equal([first.Sequence, second.Sequence, later.Sequence], preview.Entries.Select(intent => intent.Sequence));
        Assert.Equal(4, queue.Count);
        Assert.Equal([first.Sequence, second.Sequence, 3, later.Sequence],
            queue.DebugSnapshot().Select(intent => intent.Sequence));
    }

    [Fact]
    public void ConsumeIsAtomicAndDefersSameCheckpointInsertionsPastPreviewBoundary()
    {
        var queue = new BattleIntentQueue();
        BattleIntent due = queue.Enqueue(Request(0));
        BattleIntentPreview preview = queue.Preview(2, BattleIntentCheckpoint.PreAction);
        BattleIntent laterDeferred = queue.Enqueue(Request(2));
        IReadOnlyList<BattleIntent> consumed = queue.Consume(preview);
        BattleIntent earlierDeferred = queue.Enqueue(Request(0)); // inserted during capped checkpoint execution

        IReadOnlyList<BattleIntent> deferred = queue.Complete(preview);

        Assert.Equal([due], consumed);
        Assert.Equal([earlierDeferred, laterDeferred], deferred);
        Assert.Equal([earlierDeferred.Sequence, laterDeferred.Sequence], queue.DebugSnapshot().Select(intent => intent.Sequence));
        Assert.Throws<ArgumentException>(() => queue.Consume(preview));
        Assert.Throws<ArgumentException>(() => queue.Complete(preview));
        BattleIntentPreview next = queue.Preview(2, BattleIntentCheckpoint.PreAction);
        Assert.Equal([earlierDeferred, laterDeferred], queue.Consume(next));
        Assert.Empty(queue.Complete(next));
    }

    [Fact]
    public void ConsumeRejectsForeignAndChangedPreviews()
    {
        var first = new BattleIntentQueue();
        var second = new BattleIntentQueue();
        first.Enqueue(Request(0));
        BattleIntentPreview preview = first.Preview(0, BattleIntentCheckpoint.PreAction);

        Assert.Throws<ArgumentException>(() => second.Consume(preview));
        first.EndBattle();
        Assert.Throws<ArgumentException>(() => first.Consume(preview));

        var transferred = new BattleIntentQueue();
        transferred.Enqueue(Request(0,
            owner: CreatureOwner(1, Player0, BattleIntentSwitchPolicy.FollowOwner, BattleIntentFaintPolicy.Persist)));
        BattleIntentPreview beforeTransfer = transferred.Preview(0, BattleIntentCheckpoint.PreAction);
        transferred.OwnerSwitched(BattleSide.Player, 1, Player1);
        Assert.Throws<ArgumentException>(() => transferred.Consume(beforeTransfer));
    }

    [Fact]
    public void TargetPoliciesResolveSnapshotLiveSourceSideAndField()
    {
        var queue = new BattleIntentQueue();
        var occupants = new Dictionary<BattleSlot, int> { [Player0] = 3, [Player1] = 4 };
        int? ActiveParty(BattleSlot slot) => occupants.TryGetValue(slot, out int party) ? party : null;
        BattleSlot? ActiveSlot(BattleSide side, int party) => occupants
            .Where(pair => pair.Key.Side == side && pair.Value == party)
            .Select(pair => (BattleSlot?)pair.Key)
            .SingleOrDefault();

        BattleIntent snapshot = queue.Enqueue(Request(0, target: new(BattleIntentTargetPolicy.SnapshotSlot, Player0, 3, BattleSide.Player)));
        BattleIntent live = queue.Enqueue(Request(0, target: new(BattleIntentTargetPolicy.LiveSlot, Player1, null, BattleSide.Player)));
        BattleIntent creatureSource = queue.Enqueue(Request(0,
            owner: CreatureOwner(4, Player1, BattleIntentSwitchPolicy.FollowOwner, BattleIntentFaintPolicy.Persist),
            target: new(BattleIntentTargetPolicy.Source)));
        BattleIntent side = queue.Enqueue(Request(0, target: new(BattleIntentTargetPolicy.Side, Side: BattleSide.Enemy)));
        BattleIntent field = queue.Enqueue(Request(0, target: new(BattleIntentTargetPolicy.Field)));

        Assert.Equal(Player0, queue.ResolveTarget(snapshot, ActiveParty, ActiveSlot)!.Slot);
        Assert.Equal(Player1, queue.ResolveTarget(live, ActiveParty, ActiveSlot)!.Slot);
        Assert.Equal(Player1, queue.ResolveTarget(creatureSource, ActiveParty, ActiveSlot)!.Slot);
        Assert.Equal(BattleSide.Enemy, queue.ResolveTarget(side, ActiveParty, ActiveSlot)!.Side);
        Assert.True(queue.ResolveTarget(field, ActiveParty, ActiveSlot)!.IsField);

        occupants[Player0] = 9;
        occupants.Remove(Player1);
        Assert.Null(queue.ResolveTarget(snapshot, ActiveParty, ActiveSlot));
        Assert.Null(queue.ResolveTarget(live, ActiveParty, ActiveSlot));
        Assert.Null(queue.ResolveTarget(creatureSource, ActiveParty, ActiveSlot));
    }

    [Fact]
    public void OwnerCleanupAppliesSwitchFaintAndBattleEndPolicies()
    {
        var queue = new BattleIntentQueue();
        BattleIntent cancelOnSwitch = queue.Enqueue(Request(3,
            owner: CreatureOwner(1, Player0, BattleIntentSwitchPolicy.Cancel, BattleIntentFaintPolicy.Persist)));
        BattleIntent follow = queue.Enqueue(Request(3,
            owner: CreatureOwner(2, Player0, BattleIntentSwitchPolicy.FollowOwner, BattleIntentFaintPolicy.Persist)));
        BattleIntent cancelOnFaint = queue.Enqueue(Request(3,
            owner: CreatureOwner(3, Player0, BattleIntentSwitchPolicy.FollowOwner, BattleIntentFaintPolicy.Cancel)));
        BattleIntent slotOwned = queue.Enqueue(Request(3));

        BattleIntentCleanupResult switchedAway = queue.OwnerSwitched(BattleSide.Player, 1, null);
        BattleIntentCleanupResult transferred = queue.OwnerSwitched(BattleSide.Player, 2, Player1);
        IReadOnlyList<BattleIntent> fainted = queue.OwnerFainted(BattleSide.Player, 3);

        Assert.Equal([cancelOnSwitch], switchedAway.Cancelled);
        Assert.Empty(switchedAway.Transferred);
        Assert.Empty(transferred.Cancelled);
        Assert.Equal(Player1, Assert.Single(transferred.Transferred).Owner.LastKnownSlot);
        Assert.Equal([cancelOnFaint], fainted);
        Assert.Equal([follow.Sequence, slotOwned.Sequence], queue.EndBattle().Select(intent => intent.Sequence));
        Assert.Empty(queue.DebugSnapshot());
    }

    [Fact]
    public void EnqueueRejectsInvalidTypedRecords()
    {
        var queue = new BattleIntentQueue();

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Enqueue(Request(-1)));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(Request(0, owner: new(
            BattleIntentOwnerScope.Creature, BattleSide.Player, Player0, null,
            BattleIntentSwitchPolicy.FollowOwner, BattleIntentFaintPolicy.Persist))));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(Request(0,
            target: new(BattleIntentTargetPolicy.SnapshotSlot, Player0, null, BattleSide.Player))));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(Request(0,
            target: new(BattleIntentTargetPolicy.Side, Side: (BattleSide)99))));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(Request(0,
            owner: SlotOwner() with { LastKnownSlot = new BattleSlot(BattleSide.Player, -1) })));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(Request(0) with { SourceMove = EntityId.Parse("item:not_a_move") }));
        Assert.Throws<ArgumentException>(() => queue.Enqueue(Request(0) with { Ruleset = " " }));
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Enqueue(Request(0) with { SourceActionSequence = -1 }));
    }

    [Fact]
    public void EnqueueRangeValidatesWholeBatchBeforeMutation()
    {
        var queue = new BattleIntentQueue();

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.EnqueueRange(
            [Request(0), Request(-1), Request(1)]));

        Assert.Equal(0, queue.Count);
        Assert.Empty(queue.DebugSnapshot());
        Assert.Equal([0L, 1L], queue.EnqueueRange([Request(0), Request(1)])
            .Select(intent => intent.Sequence).ToArray());
    }

    [Fact]
    public void DebugSnapshotIsStableAndJsonSerializable()
    {
        var left = new BattleIntentQueue();
        var right = new BattleIntentQueue();
        left.Enqueue(Request(2));
        left.Enqueue(Request(1, BattleIntentCheckpoint.TurnEnd,
            CreatureOwner(7, Player1, BattleIntentSwitchPolicy.FollowOwner, BattleIntentFaintPolicy.Cancel),
            new(BattleIntentTargetPolicy.LiveSlot, Player0, null, BattleSide.Player)));
        right.Enqueue(Request(2));
        right.Enqueue(Request(1, BattleIntentCheckpoint.TurnEnd,
            CreatureOwner(7, Player1, BattleIntentSwitchPolicy.FollowOwner, BattleIntentFaintPolicy.Cancel),
            new(BattleIntentTargetPolicy.LiveSlot, Player0, null, BattleSide.Player)));

        string first = JsonSerializer.Serialize(left.DebugSnapshot());
        string replay = JsonSerializer.Serialize(right.DebugSnapshot());

        Assert.Equal(first, replay);
        Assert.Contains("neutral_intent", first);
        Assert.DoesNotContain("DisplayName", first);
    }

    [Fact]
    public void DelayedPayloads_AreValidatedAndExposeDeterministicDebugFields()
    {
        var queue = new BattleIntentQueue();
        var snapshot = new DelayedDamageSnapshot(50, 80, 120, EntityId.Parse("type:neutral"),
            DamageClass.Special, true, false, true, []);
        queue.Enqueue(Request(2, BattleIntentCheckpoint.TurnEnd) with
        {
            Payload = new DelayedDamageIntent(snapshot, Player0, 0, false),
        });
        queue.Enqueue(Request(1, BattleIntentCheckpoint.TurnEnd) with
        {
            Payload = new DelayedHealIntent(50, Player0, 0, true, []),
        });
        queue.Enqueue(Request(1, BattleIntentCheckpoint.TurnEnd) with
        {
            Payload = new DelayedStatusIntent(PersistentStatus.Sleep, Player0, 0, false),
        });
        queue.Enqueue(Request(0, BattleIntentCheckpoint.SwitchIn) with
        {
            Payload = new ReplacementRestoreIntent(true, true, true),
        });

        IReadOnlyList<BattleIntentDebugEntry> debug = queue.DebugSnapshot();
        string json = JsonSerializer.Serialize(debug);

        Assert.Equal([
            BattleIntentPayloadKind.ReplacementRestore,
            BattleIntentPayloadKind.DelayedHeal,
            BattleIntentPayloadKind.DelayedStatus,
            BattleIntentPayloadKind.DelayedDamage,
        ], debug.Select(entry => entry.Payload));
        Assert.Contains("RestorePp", json);
        Assert.Throws<ArgumentException>(() => queue.Enqueue(Request(1) with
        {
            Payload = new DelayedHealIntent(0, Player0, 0, false, []),
        }));
    }

    private static BattleIntentRequest Request(int dueTurn,
        BattleIntentCheckpoint checkpoint = BattleIntentCheckpoint.PreAction,
        BattleIntentOwner? owner = null,
        BattleIntentTarget? target = null) =>
        new(dueTurn, checkpoint, owner ?? SlotOwner(), target ?? new(BattleIntentTargetPolicy.Source),
            new SkipActionIntent(), Move, 0);

    private static BattleIntentOwner SlotOwner() => new(
        BattleIntentOwnerScope.Slot, BattleSide.Player, Player0, null,
        BattleIntentSwitchPolicy.StaySlot, BattleIntentFaintPolicy.Persist);

    private static BattleIntentOwner CreatureOwner(int partyIndex, BattleSlot slot,
        BattleIntentSwitchPolicy switchPolicy, BattleIntentFaintPolicy faintPolicy) => new(
        BattleIntentOwnerScope.Creature, slot.Side, slot, partyIndex, switchPolicy, faintPolicy);
}
