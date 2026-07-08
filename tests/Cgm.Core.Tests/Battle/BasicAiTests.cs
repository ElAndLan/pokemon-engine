using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BasicAiTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Ground = EntityId.Parse("type:ground");
    private static readonly EntityId Flying = EntityId.Parse("type:flying");

    private static TypeChart Chart() => new(
    [
        new TypeDef { Id = Fire, DoubleDamageTo = [Grass] },
        new TypeDef { Id = Ground, NoDamageTo = [Flying] },
        new TypeDef { Id = Grass }, new TypeDef { Id = Normal }, new TypeDef { Id = Flying },
    ]);

    private static BattleMove Move(EntityId type, int power, int pp = 10) =>
        new(EntityId.Parse("move:m"), type, DamageClass.Physical, power, 100, pp, 0, 0);

    private static BattleCreature Attacker(params BattleMove[] moves) =>
        new(EntityId.Parse("species:a"), "A", 50, [Fire], new Stats(100, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Defender(EntityId type) =>
        new(EntityId.Parse("species:d"), "D", 50, [type], new Stats(100, 100, 100, 100, 100, 100), [Move(Normal, 40)]);

    [Fact]
    public void PicksSuperEffectiveOverNeutral()
    {
        var atk = Attacker(Move(Normal, 60), Move(Fire, 60)); // index 1 = fire, super vs grass
        int choice = BasicAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(1));
        Assert.Equal(1, choice);
    }

    [Fact]
    public void AvoidsImmuneMove()
    {
        // index 0 = ground (0× vs flying), index 1 = normal (neutral). Must pick 1.
        var atk = Attacker(Move(Ground, 120), Move(Normal, 40));
        Assert.Equal(1, BasicAi.ChooseMove(atk, Defender(Flying), Chart(), new Rng(1)));
    }

    [Fact]
    public void SkipsNoPpMove()
    {
        // index 0 is the strongest but out of PP → picks index 1.
        var atk = Attacker(Move(Fire, 120, pp: 0), Move(Fire, 40));
        Assert.Equal(1, BasicAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(1)));
    }

    [Fact]
    public void PrefersHigherPower_WhenSameEffectiveness()
    {
        var atk = Attacker(Move(Normal, 40), Move(Normal, 90));
        Assert.Equal(1, BasicAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(1)));
    }

    [Fact]
    public void Deterministic_ForSameSeed()
    {
        var atk = Attacker(Move(Fire, 60), Move(Fire, 60)); // tie → rng decides
        Assert.Equal(
            BasicAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(7)),
            BasicAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(7)));
    }

    [Fact]
    public void AllNoPp_FallsBackToFirst()
    {
        var atk = Attacker(Move(Fire, 60, pp: 0), Move(Fire, 60, pp: 0));
        Assert.Equal(0, BasicAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(1)));
    }
}
