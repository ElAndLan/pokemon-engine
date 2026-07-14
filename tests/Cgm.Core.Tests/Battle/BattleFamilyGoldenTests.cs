using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleFamilyGoldenTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void SinglesDirectFamily_MatchesGolden()
    {
        BattleCreature player = Creature("p", 100, 100, Hit(300, MoveTarget.Selected));
        BattleCreature enemy = Creature("e", 10, 1, Wait());
        var battle = new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 15], doubles: [0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(Golden("singles-direct"), Snapshot(events, battle.Trace));
    }

    [Fact]
    public void DoublesSpreadFamily_MatchesGolden()
    {
        var battle = new BattleController(
            [Creature("p0", 200, 100, Hit(100, MoveTarget.AllOpponents)), Creature("p1", 200, 1, Wait())],
            [Creature("e0", 200, 1, Wait()), Creature("e1", 200, 1, Wait())], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new FakeRng(ints: [0, 0, 15, 15], doubles: [0.99, 0.99]));
        var actions = new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(actions);

        Assert.Equal(Golden("doubles-spread"), Snapshot(events, battle.Trace));
    }

    [Fact]
    public void ReplacementCheckpointFamily_MatchesGolden()
    {
        BattleSlot enemy0 = new(BattleSide.Enemy, 0);
        BattleSlot enemy1 = new(BattleSide.Enemy, 1);
        var battle = new BattleController(
            [Creature("p0", 200, 200, Hit(300, MoveTarget.Selected)), Creature("p1", 200, 150, Hit(300, MoveTarget.Selected))],
            [Creature("e0", 1, 1, Wait()), Creature("e1", 1, 1, Wait()), Creature("e2", 200, 1, Wait()), Creature("e3", 200, 1, Wait())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [0, 15, 0, 15], doubles: [0.99, 0.99]));
        IReadOnlyList<BattleEvent> turnEvents = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(enemy0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new UseMove(0), new ActiveSlotSelection(enemy1)),
            new BattleActionSubmission(enemy0, new Pass()),
            new BattleActionSubmission(enemy1, new Pass()),
        ]));
        IReadOnlyList<BattleEvent> replacementEvents = battle.ResolveReplacements([new(enemy1, 3), new(enemy0, 2)]);

        Assert.Equal(Golden("replacement-checkpoint"),
            Snapshot([.. turnEvents, .. replacementEvents], Array.Empty<EffectTraceEntry>()));
    }

    private static string Golden(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static string Snapshot(IEnumerable<BattleEvent> events, IEnumerable<EffectTraceEntry> trace) => string.Join('\n',
        ["events", .. events.Select(Event), "trace", .. trace.Select(Trace)]);

    private static string Event(BattleEvent item) => item switch
    {
        MoveUsed e => $"MoveUsed:{e.Slot.Side}:{e.Slot.Position}",
        DamageDealt e => $"DamageDealt:{e.Slot.Side}:{e.Slot.Position}",
        Fainted e => $"Fainted:{e.Slot.Side}:{e.Slot.Position}",
        ReplacementRequested e => $"ReplacementRequested:{e.Slot.Side}:{e.Slot.Position}",
        SwitchedIn e => $"SwitchedIn:{e.Slot.Side}:{e.Slot.Position}:{e.PartyIndex}",
        BattleEnded e => $"BattleEnded:{e.Winner}",
        _ => item.GetType().Name,
    };

    private static string Trace(EffectTraceEntry item) =>
        $"{item.Kind}:{item.SourceSlot.Side}:{item.SourceSlot.Position}:{(item.TargetSlot is { } target ? $"{target.Side}:{target.Position}" : "-")}";

    private static BattleCreature Creature(string slug, int hp, int speed, BattleMove move) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(hp, 100, 100, 100, 100, speed), [move]);

    private static BattleMove Hit(int power, MoveTarget target) =>
        new(EntityId.Parse("move:hit"), Normal, DamageClass.Special, power, 100, 10, 0, 0, target: target);

    private static BattleMove Wait() =>
        new(EntityId.Parse("move:wait"), Normal, DamageClass.Status, null, null, 10, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
}
