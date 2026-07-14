using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleQueryIntegrationTests
{
    private static readonly EntityId Neutral = EntityId.Parse("type:neutral");

    [Fact]
    public void Resolver_ExposesExactActionAddressedQueryTrace()
    {
        var move = new BattleMove(EntityId.Parse("move:query_strike"), Neutral, DamageClass.Special,
            65, 100, 10, 0, 0, heal: new Fraction(1, 2),
            targetHpThresholdPower: new TargetHpThresholdPower(new Fraction(1, 2), new Fraction(2, 1)));
        BattleCreature source = Creature("source", 100, move);
        source.TakeDamage(50);
        BattleCreature target = Creature("target", 1, Wait(), hp: 400);
        target.TakeDamage(200);
        var battle = new BattleController(source, target, Chart(), new FakeRng(ints: [0, 15], doubles: [0.99]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        BattleQueryTraceEntry power = Assert.Single(battle.QueryTrace,
            entry => entry.ActionSequence > 0 && entry.Result.Query == BattleQueryId.BasePower);
        Assert.Equal(65, power.Result.AuthoredBase.ToInt32());
        Assert.Equal(130, power.Result.FinalValue.ToInt32());
        Assert.Contains(battle.QueryTrace, entry => entry.ActionSequence == power.ActionSequence
            && entry.Result.Query == BattleQueryId.OffensiveStat);
        Assert.Contains(battle.QueryTrace, entry => entry.ActionSequence == power.ActionSequence
            && entry.Result.Query == BattleQueryId.DefensiveStat);
        Assert.Contains(battle.QueryTrace, entry => entry.ActionSequence == power.ActionSequence
            && entry.Result.Query == BattleQueryId.FinalDamage);
        Assert.Contains(battle.QueryTrace, entry => entry.Result.Query == BattleQueryId.Healing);
        Assert.Contains(battle.QueryTrace, entry => entry.ActionSequence == 0 && entry.Result.Query == BattleQueryId.Speed);
    }

    [Fact]
    public void DoublesDamageHooks_UseExactSlotOwnedQueryModifiers()
    {
        var hook = new AbilityHook
        {
            Hook = AbilityHookPoint.OnModifyOutgoingDamage,
            Effects =
            [
                new Effect
                {
                    Op = "typeDamageModify",
                    Params = Params(("type", "neutral"), ("multiplierPercent", 200)),
                },
            ],
        };
        var strike = new BattleMove(EntityId.Parse("move:slot_strike"), Neutral, DamageClass.Physical,
            60, 100, 10, 0, 0);
        BattleCreature target = Creature("target", 1, Wait(), hp: 400);
        var battle = new BattleController(
            [Creature("source", 100, strike, abilityHooks: [hook]), Creature("ally", 50, Wait())],
            [target, Creature("other", 1, Wait(), hp: 400)],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(),
            new FakeRng(ints: [0, 15], doubles: [0.99]));

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 0), new UseMove(0),
                new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))),
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]));

        BattleQueryTraceEntry damage = Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.FinalDamage);
        BattleQueryStep modifier = Assert.Single(damage.Result.Steps,
            step => step.Operation == BattleQueryOperation.Multiply);
        Assert.Equal(BattleQueryOwnerScope.Source, modifier.OwnerScope);
        Assert.Equal(damage.Result.AuthoredBase.ToInt32() * 2, damage.Result.FinalValue.ToInt32());
    }

    [Fact]
    public void FixedDamage_RemainsExactWhilePassingThroughFinalDamageQuery()
    {
        var fixedMove = new BattleMove(EntityId.Parse("move:fixed_strike"), Neutral, DamageClass.Special,
            null, 100, 10, 0, 0, fixedDamage: 37);
        BattleCreature target = Creature("target", 1, Wait(), hp: 200);
        var battle = new BattleController(Creature("source", 100, fixedMove), target, Chart(), new FakeRng(ints: [0]));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(163, target.CurrentHp);
        BattleQueryTraceEntry damage = Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.FinalDamage);
        Assert.Equal(37, damage.Result.AuthoredBase.ToInt32());
        Assert.Equal(37, damage.Result.FinalValue.ToInt32());
    }

    private static BattleCreature Creature(string slug, int speed, BattleMove move, int hp = 200,
        IReadOnlyList<AbilityHook>? abilityHooks = null) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Neutral], new Stats(hp, 100, 100, 100, 100, speed), [move],
        abilityHooks: abilityHooks);

    private static BattleMove Wait() =>
        new(EntityId.Parse("move:wait"), Neutral, DamageClass.Status, null, null, 10, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Neutral }]);

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value));
}
