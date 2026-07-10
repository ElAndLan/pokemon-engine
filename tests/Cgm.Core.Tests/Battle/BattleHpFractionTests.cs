using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleHpFractionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static Effect Op(string op, params (string Key, object Value)[] values) =>
        new()
        {
            Op = op,
            Params = values.Length == 0 ? null : values.ToDictionary(
                value => value.Key,
                value => JsonSerializer.SerializeToElement(value.Value)),
        };

    private static BattleMove Compile(MoveTarget target, params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:hp_fraction"),
        Name = "HP Fraction",
        Type = Normal,
        DamageClass = DamageClass.Status,
        Power = null,
        Accuracy = null,
        Pp = 10,
        Target = target,
        Effects = effects,
    });

    private static BattleCreature Creature(string id, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{id}"), id, 50, [Normal], new Stats(200, 100, 100, 100, 100, speed), moves);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    [Fact]
    public void Heal_CompilesTargetRecipientWithoutAdvertisingItAsSelfRecovery()
    {
        BattleMove move = Compile(MoveTarget.Selected, Op("heal", ("num", 1), ("den", 4), ("recipient", "target")));

        Assert.Contains(move.SecondaryEffects, effect =>
            effect is HealEffect { Recipient: HpFractionRecipient.Target, Fraction: { Num: 1, Den: 4 } });
        Assert.Null(move.Heal);
    }

    [Fact]
    public void HpFraction_CompilesStrictTypedEffect()
    {
        BattleMove move = Compile(MoveTarget.Selected, Op("hpFraction",
            ("recipient", "target"), ("operation", "damage"), ("basis", "currentHp"), ("num", 1), ("den", 2)));

        Assert.Contains(move.SecondaryEffects, effect => effect is HpFractionEffect
        {
            Recipient: HpFractionRecipient.Target,
            Operation: HpFractionOperation.Damage,
            Basis: HpFractionBasis.CurrentHp,
            Fraction: { Num: 1, Den: 2 },
        });
    }

    [Fact]
    public void HpFraction_RejectsChanceAndInvalidParameters()
    {
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected, new Effect
        {
            Op = "hpFraction",
            Chance = 50,
            Params = new Dictionary<string, JsonElement>
            {
                ["recipient"] = JsonSerializer.SerializeToElement("target"),
                ["operation"] = JsonSerializer.SerializeToElement("damage"),
                ["basis"] = JsonSerializer.SerializeToElement("currentHp"),
                ["num"] = JsonSerializer.SerializeToElement(1),
                ["den"] = JsonSerializer.SerializeToElement(2),
            },
        }));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected, Op("hpFraction",
            ("recipient", "target"), ("operation", "damage"), ("basis", "currentHp"), ("num", 0), ("den", 2))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected, Op("heal", ("recipient", "other"))));
    }

    [Fact]
    public void TargetHeal_UsesTheSharedHealPrimitive()
    {
        BattleMove heal = Compile(MoveTarget.Selected, Op("heal", ("num", 1), ("den", 4), ("recipient", "target")));
        BattleCreature player = Creature("player", 100, heal);
        BattleCreature enemy = Creature("enemy", 1, Inert());
        enemy.TakeDamage(100);
        var battle = new BattleController(player, enemy, new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(150, enemy.CurrentHp);
        Assert.Contains(events, eventItem => eventItem is Healed { Side: BattleSide.Enemy, Amount: 50 });
    }

    [Fact]
    public void CurrentHpFractionDamage_UsesTheSharedSapPrimitive()
    {
        BattleMove damage = Compile(MoveTarget.Selected, Op("hpFraction",
            ("recipient", "target"), ("operation", "damage"), ("basis", "currentHp"), ("num", 1), ("den", 2)));
        BattleCreature player = Creature("player", 100, damage);
        BattleCreature enemy = Creature("enemy", 1, Inert());
        var battle = new BattleController(player, enemy, new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(100, enemy.CurrentHp);
        Assert.Contains(events, eventItem => eventItem is HpFractionDamaged { Side: BattleSide.Enemy, Amount: 100 });
    }
}
