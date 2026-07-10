using System.Text.Json;
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

    private static BattleMove EffectMove(params MoveEffect[] effects) =>
        new(EntityId.Parse("move:stage_helper"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            secondaryEffects: effects);

    private static BattleCreature Creature(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:x"), "X", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleController Battle(BattleCreature p, BattleCreature e, int seed = 1) =>
        new(p, e, Chart(), new Rng(seed));

    private static Effect Op(string op, int? chance = null, params (string Key, object Value)[] ps)
    {
        var dict = ps.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value));
        return new Effect { Op = op, Chance = chance, Params = dict.Count == 0 ? null : dict };
    }

    private static Move DataMove(params Effect[] effects) => new()
    {
        Id = EntityId.Parse("move:stage_helper"),
        Name = "Stage Helper",
        Type = Normal,
        DamageClass = DamageClass.Status,
        Power = null,
        Accuracy = null,
        Pp = 10,
        Effects = effects,
    };

    [Fact]
    public void SelfBuff_RaisesUsersStage()
    {
        // Swords Dance-style: +2 Atk to self, guaranteed.
        var player = Creature(200, Status(new StageEffect(StatKind.Atk, 2, OnSelf: true, Chance: 100)));
        var enemy = Creature(200, EffectMove());
        var events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, player.Stage(StatKind.Atk));
        Assert.Contains(events, e => e is StatStageChanged { Side: BattleSide.Player, Stat: StatKind.Atk, Delta: 2 });
    }

    [Fact]
    public void MultipleStatEffects_ApplyInOrder()
    {
        var player = Creature(200, new BattleMove(EntityId.Parse("move:setup"), Normal, DamageClass.Status,
            null, null, 25, 0, 0,
            stageEffects:
            [
                new StageEffect(StatKind.Atk, 1, OnSelf: true, Chance: 100),
                new StageEffect(StatKind.Spa, 1, OnSelf: true, Chance: 100),
            ]));
        var enemy = Creature(200, EffectMove());

        IReadOnlyList<BattleEvent> events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(1, player.Stage(StatKind.Atk));
        Assert.Equal(1, player.Stage(StatKind.Spa));
        Assert.True(events.OfType<StatStageChanged>().Take(2).SequenceEqual([
            new StatStageChanged(BattleSide.Player, StatKind.Atk, 1),
            new StatStageChanged(BattleSide.Player, StatKind.Spa, 1),
        ]));
    }

    [Fact]
    public void AllStatEffect_UsesOneChanceRollAndAppliesInOrder()
    {
        var player = Creature(200, new BattleMove(EntityId.Parse("move:allup"), Normal, DamageClass.Status,
            null, null, 25, 0, 0,
            target: MoveTarget.User,
            stageAllEffect: new StageAllEffect(1, OnSelf: true, Chance: 100)));
        var enemy = Creature(200, EffectMove());

        IReadOnlyList<BattleEvent> events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(1, player.Stage(StatKind.Atk));
        Assert.Equal(1, player.Stage(StatKind.Def));
        Assert.Equal(1, player.Stage(StatKind.Spa));
        Assert.Equal(1, player.Stage(StatKind.Spd));
        Assert.Equal(1, player.Stage(StatKind.Spe));
        Assert.Equal(0, player.Stage(StatKind.Accuracy));
        Assert.Equal(0, player.Stage(StatKind.Evasion));
        Assert.True(events.OfType<StatStageChanged>().Take(5).SequenceEqual([
            new StatStageChanged(BattleSide.Player, StatKind.Atk, 1),
            new StatStageChanged(BattleSide.Player, StatKind.Def, 1),
            new StatStageChanged(BattleSide.Player, StatKind.Spa, 1),
            new StatStageChanged(BattleSide.Player, StatKind.Spd, 1),
            new StatStageChanged(BattleSide.Player, StatKind.Spe, 1),
        ]));
    }

    [Fact]
    public void AllStatEffect_ChanceMissSkipsWholeBundle()
    {
        var player = Creature(200, new BattleMove(EntityId.Parse("move:allup"), Normal, DamageClass.Status,
            null, null, 25, 0, 0,
            stageAllEffect: new StageAllEffect(1, OnSelf: true, Chance: 0)));
        var enemy = Creature(200, EffectMove());

        IReadOnlyList<BattleEvent> events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(0, player.Stage(StatKind.Atk));
        Assert.DoesNotContain(events, e => e is StatStageChanged);
    }

    [Fact]
    public void ResetTargetStages_ClearsAllChangedSlots()
    {
        var player = Creature(200, EffectMove(new StatResetEffect(StageEffectScope.Target)));
        var enemy = Creature(200, Plain());
        enemy.SetStage(StatKind.Atk, 3);
        enemy.SetStage(StatKind.Accuracy, -2);

        IReadOnlyList<BattleEvent> events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(0, enemy.Stage(StatKind.Atk));
        Assert.Equal(0, enemy.Stage(StatKind.Accuracy));
        Assert.Contains(events, e => e is StatStageChanged { Side: BattleSide.Enemy, Stat: StatKind.Atk, Delta: -3 });
        Assert.Contains(events, e => e is StatStageChanged { Side: BattleSide.Enemy, Stat: StatKind.Accuracy, Delta: 2 });
    }

    [Fact]
    public void CopyTargetStages_CopiesAccuracyAndEvasionToo()
    {
        var player = Creature(200, EffectMove(new StatCopyEffect(StageEffectScope.Target, StageEffectScope.Self)));
        var enemy = Creature(200, Plain());
        enemy.SetStage(StatKind.Spa, 4);
        enemy.SetStage(StatKind.Evasion, -1);

        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(4, player.Stage(StatKind.Spa));
        Assert.Equal(-1, player.Stage(StatKind.Evasion));
    }

    [Fact]
    public void SwapOffenseStages_LeavesDefenseStagesAlone()
    {
        var player = Creature(200, EffectMove(new StatSwapEffect(StageSwapGroup.Offense)));
        var enemy = Creature(200, Plain());
        player.SetStage(StatKind.Atk, 2);
        player.SetStage(StatKind.Def, 3);
        enemy.SetStage(StatKind.Atk, -1);
        enemy.SetStage(StatKind.Def, -2);

        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(-1, player.Stage(StatKind.Atk));
        Assert.Equal(2, enemy.Stage(StatKind.Atk));
        Assert.Equal(3, player.Stage(StatKind.Def));
        Assert.Equal(-2, enemy.Stage(StatKind.Def));
    }

    [Fact]
    public void InvertTargetStages_FlipsAllChangedSlots()
    {
        var player = Creature(200, EffectMove(new StatInvertEffect(OnSelf: false)));
        var enemy = Creature(200, Plain());
        enemy.SetStage(StatKind.Atk, 2);
        enemy.SetStage(StatKind.Evasion, -3);

        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(-2, enemy.Stage(StatKind.Atk));
        Assert.Equal(3, enemy.Stage(StatKind.Evasion));
    }

    [Fact]
    public void HpCostSetup_PaysCostBeforeLaterStatBoost()
    {
        BattleMove move = MoveCompiler.ToBattleMove(DataMove(
            Op("hpCost", null, ("num", 1), ("den", 2)),
            Op("statStage", null, ("stat", "atk"), ("delta", 6), ("onSelf", true))));
        var player = Creature(200, move);
        var enemy = Creature(200, EffectMove());

        IReadOnlyList<BattleEvent> events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(100, player.CurrentHp);
        Assert.Equal(6, player.Stage(StatKind.Atk));
        Assert.Contains(events, e => e is HpCostPaid { Side: BattleSide.Player, Amount: 100 });
    }

    [Fact]
    public void HpCostSetup_StopsLaterEffectsWhenCostCannotBePaid()
    {
        BattleMove move = MoveCompiler.ToBattleMove(DataMove(
            Op("hpCost", null, ("num", 1), ("den", 2)),
            Op("statStage", null, ("stat", "atk"), ("delta", 6), ("onSelf", true))));
        var player = Creature(200, move);
        player.TakeDamage(100);
        var enemy = Creature(200, EffectMove());

        IReadOnlyList<BattleEvent> events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(100, player.CurrentHp);
        Assert.Equal(0, player.Stage(StatKind.Atk));
        Assert.DoesNotContain(events, e => e is HpCostPaid);
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
