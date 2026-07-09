using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>The profile-dispatch layer: <see cref="TrainerAi"/> must route each <see cref="AiProfile"/>
/// to its underlying chooser. Uses a scenario where Basic (damage-only) and Smart (values status on a
/// healthy target) diverge, so a mis-route is observable, and cross-checks each route against the
/// underlying AI with a matching fresh RNG.</summary>
public sealed class TrainerAiTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new(
    [
        new TypeDef { Id = Fire, DoubleDamageTo = [Grass] },
        new TypeDef { Id = Grass }, new TypeDef { Id = Normal },
    ]);

    private static BattleMove Chip() =>
        new(EntityId.Parse("move:chip"), Normal, DamageClass.Physical, 10, 100, 10, 0, 0);

    private static BattleMove Paralyze() =>
        new(EntityId.Parse("move:twave"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            ailment: PersistentStatus.Paralysis, ailmentChance: 100);

    private static BattleCreature Attacker() =>
        new(EntityId.Parse("species:a"), "A", 50, [Fire], new Stats(100, 100, 100, 100, 100, 100),
            [Chip(), Paralyze()]);

    private static BattleCreature Target() =>
        new(EntityId.Parse("species:d"), "D", 50, [Normal], new Stats(200, 100, 100, 100, 100, 100),
            [Chip()]);

    private static SmartAiContext Ctx(int seed) =>
        new([Attacker()], 0, [Target()], 0, Chart(), new Rng(seed),
            Weights: new SmartAiWeights { NoiseFraction = 0 });

    [Fact]
    public void ChooseMove_Basic_PicksDamage_MatchingBasicAi()
    {
        // Basic only values damage → the chip move (index 0), not the status move.
        Assert.Equal(0, TrainerAi.ChooseMove(AiProfile.Basic, Attacker(), Target(), Chart(), new Rng(2)));
        Assert.Equal(
            BasicAi.ChooseMove(Attacker(), Target(), Chart(), new Rng(2)),
            TrainerAi.ChooseMove(AiProfile.Basic, Attacker(), Target(), Chart(), new Rng(2)));
    }

    [Fact]
    public void ChooseMove_Smart_PicksStatus_MatchingSmartAi()
    {
        // Smart values paralysis on a healthy target → the status move (index 1).
        Assert.Equal(1, TrainerAi.ChooseMove(AiProfile.Smart, Attacker(), Target(), Chart(), new Rng(2)));
        Assert.Equal(
            SmartAi.ChooseMove(Attacker(), Target(), Chart(), new Rng(2)),
            TrainerAi.ChooseMove(AiProfile.Smart, Attacker(), Target(), Chart(), new Rng(2)));
    }

    [Fact]
    public void ChooseMove_Random_MatchesRandomAi_IgnoringMatchup()
    {
        Assert.Equal(
            RandomAi.ChooseMove(Attacker(), new Rng(5)),
            TrainerAi.ChooseMove(AiProfile.Random, Attacker(), Target(), Chart(), new Rng(5)));
    }

    [Fact]
    public void ChooseAction_Basic_RoutesToBasicChooser()
    {
        Assert.Equal(
            new UseMove(BasicAi.ChooseMove(Attacker(), Target(), Chart(), new Rng(2))),
            TrainerAi.ChooseAction(AiProfile.Basic, Ctx(2)));
    }

    [Fact]
    public void ChooseAction_Random_RoutesToRandomChooser()
    {
        Assert.Equal(
            new UseMove(RandomAi.ChooseMove(Attacker(), new Rng(5))),
            TrainerAi.ChooseAction(AiProfile.Random, Ctx(5)));
    }

    [Fact]
    public void BasicAndSmart_DivergeOnTheSameBoard()
    {
        // Guards the routing test's premise: the two profiles genuinely disagree here.
        Assert.NotEqual(
            TrainerAi.ChooseMove(AiProfile.Basic, Attacker(), Target(), Chart(), new Rng(2)),
            TrainerAi.ChooseMove(AiProfile.Smart, Attacker(), Target(), Chart(), new Rng(2)));
    }
}
