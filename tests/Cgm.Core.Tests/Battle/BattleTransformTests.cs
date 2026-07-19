using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15F-6 Transform value copy: the user snapshots the target's effective types, stats, and
/// ability onto itself via form/snapshot overlays, keeping its own HP.</summary>
public sealed class BattleTransformTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Ghost = EntityId.Parse("type:ghost");
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId AbilityA = EntityId.Parse("ability:first");
    private static readonly EntityId AbilityB = EntityId.Parse("ability:second");

    [Fact]
    public void TransformCopiesTargetTypesStatsAbilityKeepingOwnHp()
    {
        var battle = new BattleController(User(), Target(), Chart(), new FakeRng(ints: [0, 0]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is Transformed { Side: BattleSide.Player });
        var payloads = battle.Overlays.Snapshot()
            .Where(o => o.Owner.Side == BattleSide.Player).Select(o => o.Payload).ToList();
        Assert.Contains(payloads, p => p is CreatureTypesOverlay c && c.Types.SequenceEqual(new[] { Water, Ghost }));
        Assert.Contains(payloads, p => p is StatsOverlay { Stats: { Spe: 130, Atk: 200, Hp: 200 } });
        Assert.Contains(payloads, p => p is AbilityOverlay { Ability: { } a } && a == AbilityB);
        Assert.Contains(payloads, p => p is FormOverlay { FormId: "transform" });
    }

    [Fact]
    public void TransformSnapshotIsIndependentOfLaterTargetChanges()
    {
        var battle = new BattleController(User(), Target(), Chart(), new FakeRng(ints: [0, 0]));
        battle.ResolveTurn(new UseMove(0), new Pass());

        // Change the target's effective types after the transform; the user's copied snapshot is a
        // separate stored value and must not change.
        battle.Overlays.Apply(new BattleOverlayApplication(
            new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)),
            new BattleOverlaySource(), BattleOverlayLayer.PermanentInstance,
            new CreatureTypesOverlay([Normal]), 1, 0));

        CreatureTypesOverlay copied = battle.Overlays.Snapshot()
            .Where(o => o.Owner.Side == BattleSide.Player).Select(o => o.Payload)
            .OfType<CreatureTypesOverlay>().Single();
        Assert.Equal([Water, Ghost], copied.Types);
    }

    [Fact]
    public void SecondTransformFailsWhileAlreadyTransformed()
    {
        var battle = new BattleController(User2(), Target(), Chart(), new FakeRng(ints: [0, 0, 0, 0]));
        battle.ResolveTurn(new UseMove(0), new Pass());
        int overlaysAfterFirst = battle.Overlays.Snapshot().Count(o => o.Owner.Side == BattleSide.Player);
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Empty(second.OfType<Transformed>());
        Assert.Equal(overlaysAfterFirst, battle.Overlays.Snapshot().Count(o => o.Owner.Side == BattleSide.Player));
    }

    [Fact]
    public void TransformOverlaysClearAtBattleEnd()
    {
        var battle = new BattleController(User(), Target(), Chart(), new FakeRng(ints: [0, 0]));
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.NotEmpty(battle.Overlays.Snapshot());

        battle.Overlays.EndBattle(1, 0);

        Assert.Empty(battle.Overlays.Snapshot());
    }

    private static BattleCreature User() => new(EntityId.Parse("species:user"), "user", 50, [Fire],
        new Stats(200, 100, 100, 100, 100, 30), [Transform("t")], ability: AbilityA);

    // Two transform moves, so an already-transformed second cast can be attempted.
    private static BattleCreature User2() => new(EntityId.Parse("species:user"), "user", 50, [Fire],
        new Stats(200, 100, 100, 100, 100, 30), [Transform("t1"), Transform("t2")], ability: AbilityA);

    private static BattleCreature Target() => TargetCreature();

    private static BattleCreature TargetCreature() => new(EntityId.Parse("species:target"), "target", 50,
        [Water, Ghost], new Stats(300, 200, 150, 150, 150, 130),
        [new BattleMove(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0)],
        ability: AbilityB);

    private static BattleMove Transform(string slug) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal, DamageClass = DamageClass.Status,
        Pp = 10, Target = MoveTarget.Selected, Effects = [new Effect { Op = "transform" }],
    });

    private static TypeChart Chart() => new(
        [new TypeDef { Id = Fire }, new TypeDef { Id = Water }, new TypeDef { Id = Ghost },
            new TypeDef { Id = Normal }]);
}
