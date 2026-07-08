using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStageEffectTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Status(StageEffect effect) =>
        new(EntityId.Parse("move:s"), Normal, DamageClass.Status, null, null, 25, 0, 0, stageEffect: effect);

    private static BattleMove Plain() =>
        new(EntityId.Parse("move:m"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0);

    private static BattleCreature Creature(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:x"), "X", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleController Battle(BattleCreature p, BattleCreature e, int seed = 1) =>
        new(p, e, Chart(), new Rng(seed));

    [Fact]
    public void SelfBuff_RaisesUsersStage()
    {
        // Swords Dance-style: +2 Atk to self, guaranteed.
        var player = Creature(200, Status(new StageEffect(StatKind.Atk, 2, OnSelf: true, Chance: 100)));
        var enemy = Creature(200, Plain());
        var events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, player.Stage(StatKind.Atk));
        Assert.Contains(events, e => e is StatStageChanged { Side: BattleSide.Player, Stat: StatKind.Atk, Delta: 2 });
    }

    [Fact]
    public void Debuff_LowersTargetsStage()
    {
        // Growl-style: −1 Atk to target, guaranteed.
        var player = Creature(200, Status(new StageEffect(StatKind.Atk, -1, OnSelf: false, Chance: 100)));
        var enemy = Creature(200, Plain());
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(-1, enemy.Stage(StatKind.Atk));
    }

    [Fact]
    public void ZeroChance_NoStageChange()
    {
        var player = Creature(200, Status(new StageEffect(StatKind.Atk, 2, true, Chance: 0)));
        var enemy = Creature(200, Plain());
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(0, player.Stage(StatKind.Atk));
    }

    [Fact]
    public void FaintedTarget_NotDebuffed()
    {
        // A damaging move that also debuffs the target on hit; if the hit faints the target, no debuff.
        BattleMove crunch = new(EntityId.Parse("move:crunch"), Normal, DamageClass.Physical, 200, 100, 25, 0, 0,
            stageEffect: new StageEffect(StatKind.Def, -1, OnSelf: false, Chance: 100));
        var player = Creature(200, crunch);
        var enemy = Creature(5, Plain()); // will faint from the hit
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.True(enemy.IsFainted);
        Assert.Equal(0, enemy.Stage(StatKind.Def)); // no debuff applied to a fainted target
    }

    [Fact]
    public void PlainMoves_ProduceNoStageEvents()
    {
        var events = Battle(Creature(200, Plain()), Creature(200, Plain())).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.DoesNotContain(events, e => e is StatStageChanged);
    }
}
