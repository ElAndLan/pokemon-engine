using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15F-5 controller wiring for the two stage ops the existing effects cannot express:
/// steal (Spectral Thief) and single-draw random raise (Acupressure).</summary>
public sealed class BattleStageMutationOpsTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void StealMoveTakesTargetPositiveBoostsOntoTheUser()
    {
        BattleMove thief = Compile("thief", DamageClass.Physical, 60, MoveTarget.Selected, Op("statStageSteal"));
        BattleCreature attacker = Creature("attacker", Fast, thief);
        BattleCreature target = Creature("target", Slow, Inert());
        target.ChangeStage(StatKind.Atk, 2);
        target.ChangeStage(StatKind.Spe, -1);

        var battle = new BattleController(attacker, target, Chart(), new FakeRng(ints: [0, 100], doubles: [0.99]));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, attacker.Stage(StatKind.Atk)); // stolen boost
        Assert.Equal(0, target.Stage(StatKind.Atk));   // boost removed
        Assert.Equal(-1, target.Stage(StatKind.Spe));  // negative untouched
    }

    [Fact]
    public void RandomRaiseMoveRaisesOneStatByTheDelta()
    {
        BattleMove acupressure = Compile("acupressure", DamageClass.Status, null, MoveTarget.User,
            Op("statStageRandomRaise", ("delta", 2)));
        BattleCreature user = Creature("user", Fast, acupressure);
        BattleCreature enemy = Creature("enemy", Slow, Inert());

        // FakeRng draws 0 -> first eligible stat in enum order (Atk).
        var battle = new BattleController(user, enemy, Chart(), new FakeRng(ints: [0, 0], doubles: [0.99]));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2, user.Stage(StatKind.Atk));
        Assert.Equal(0, user.Stage(StatKind.Def)); // exactly one stat raised
    }

    private static readonly Stats Fast = new(400, 120, 100, 120, 100, 100);
    private static readonly Stats Slow = new(400, 100, 100, 100, 100, 1);

    private static BattleMove Compile(string slug, DamageClass dc, int? power, MoveTarget target, Effect effect) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal, DamageClass = dc,
            Power = power, Accuracy = power is null ? null : 100, Pp = 10, Target = target, Effects = [effect],
        });

    private static BattleCreature Creature(string slug, Stats stats, BattleMove move) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal], stats, [move]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.Length == 0 ? null : values.ToDictionary(v => v.Key,
            v => JsonSerializer.SerializeToElement(v.Value)),
    };
}
