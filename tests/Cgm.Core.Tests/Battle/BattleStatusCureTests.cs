using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStatusCureTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Ghost = EntityId.Parse("type:ghost");

    private static Effect Op(params (string Key, object Value)[] values) => new()
    {
        Op = "statusCure",
        Params = values.Length == 0 ? null : values.ToDictionary(
            value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static BattleMove Compile(MoveTarget target, DamageClass damageClass, Effect effect) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:cure"), Name = "Cure", Type = Normal,
            DamageClass = damageClass, Power = damageClass == DamageClass.Status ? null : 40,
            Accuracy = null, Pp = 10, Target = target, Effects = [effect],
        });

    private static BattleCreature Creature(string slug, EntityId type, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(200, 100, 100, 100, 100, 100), moves);

    private static BattleMove Wait() =>
        new(EntityId.Parse("move:wait"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    [Fact]
    public void CompilerClosesRecipientFilterAndDamageContract()
    {
        BattleMove self = Compile(MoveTarget.User, DamageClass.Status,
            Op(("statuses", "burn,poison")));
        StatusCureEffect compiled = Assert.IsType<StatusCureEffect>(Assert.Single(self.SecondaryEffects));
        Assert.Equal(HpFractionRecipient.Self, compiled.Recipient);
        Assert.Equal([PersistentStatus.Burn, PersistentStatus.Poison], compiled.Statuses);
        Assert.False(compiled.RequireDamage);
        Assert.Empty(Assert.IsType<StatusCureEffect>(Assert.Single(
            Compile(MoveTarget.User, DamageClass.Status, Op()).SecondaryEffects)).Statuses);

        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.User, DamageClass.Status,
            Op(("recipient", "target"))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.User, DamageClass.Status,
            Op(("statuses", ""))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.User, DamageClass.Status,
            Op(("statuses", "burn,burn"))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected, DamageClass.Status,
            Op(("recipient", "target"), ("requireDamage", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected, DamageClass.Physical,
            new Effect { Op = "statusCure", Chance = 50 }));
    }

    [Fact]
    public void SelfCureRemovesOnlyAMatchingPersistentStatusAndTracesBothOutcomes()
    {
        BattleMove cure = Compile(MoveTarget.User, DamageClass.Status, Op(("statuses", "toxic")));
        BattleCreature source = Creature("source", Normal, cure);
        source.SetStatus(PersistentStatus.Toxic, counter: 4);
        var battle = new BattleController(source, Creature("target", Normal, Wait()),
            new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Null(source.Status);
        Assert.Equal(0, source.StatusCounter);
        Assert.Contains(events, item => item is StatusCured
            { Slot: { Side: BattleSide.Player, Position: 0 }, Status: PersistentStatus.Toxic });
        Assert.Contains(battle.Trace, item => item is
            { Kind: EffectTraceKind.StatusCure, Performed: true });

        source.SetStatus(PersistentStatus.Burn);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(PersistentStatus.Burn, source.Status);
        Assert.Contains(battle.Trace, item => item is
            { Kind: EffectTraceKind.StatusCure, Performed: false });
    }

    [Fact]
    public void DamageRequiredCureUsesPerTargetActualHpDamage()
    {
        BattleMove cure = Compile(MoveTarget.Selected, DamageClass.Special,
            Op(("recipient", "target"), ("statuses", "burn"), ("requireDamage", true)));
        BattleCreature connectedTarget = Creature("connected", Normal, Wait());
        connectedTarget.SetStatus(PersistentStatus.Burn);
        var connected = new BattleController(Creature("source", Normal, cure), connectedTarget,
            new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));

        connected.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Null(connectedTarget.Status);

        BattleCreature immuneTarget = Creature("immune", Ghost, Wait());
        immuneTarget.SetStatus(PersistentStatus.Burn);
        var immune = new BattleController(Creature("source2", Normal, cure), immuneTarget,
            new TypeChart([new TypeDef { Id = Normal, NoDamageTo = [Ghost] }, new TypeDef { Id = Ghost }]),
            new Rng(1));

        immune.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(PersistentStatus.Burn, immuneTarget.Status);
        Assert.DoesNotContain(immune.Log, item => item is StatusCured);
    }
}
