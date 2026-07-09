using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Seeded AI-vs-AI integration smoke: drive whole battles with <see cref="TrainerAi"/> choosing
/// both sides' actions until the controller sets an <see cref="BattleOutcome"/>. Asserts the guaranteeable
/// properties — every battle terminates with a valid winner, and a given seed replays identically. The
/// tuning-dependent win-rate band (Phase 14 exit gate) is deliberately out of scope here.</summary>
public sealed class AiBattleSimTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new(
    [
        new TypeDef { Id = Fire, DoubleDamageTo = [Grass] },
        new TypeDef { Id = Water, DoubleDamageTo = [Fire] },
        new TypeDef { Id = Grass, DoubleDamageTo = [Water] },
        new TypeDef { Id = Normal },
    ]);

    private static BattleMove Atk(EntityId type, int power) =>
        new(EntityId.Parse("move:m"), type, DamageClass.Physical, power, 100, 15, 0, 0);

    private static BattleCreature Mon(string slug, EntityId type, EntityId atkType) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(120, 100, 100, 100, 100, 100), [Atk(atkType, 80), Atk(Normal, 60)]);

    // Fresh (mutable) parties per run — battles mutate HP/PP/stages.
    private static BattleCreature[] PartyA() => [Mon("a1", Fire, Fire), Mon("a2", Water, Water)];
    private static BattleCreature[] PartyB() => [Mon("b1", Grass, Grass), Mon("b2", Normal, Normal)];

    private const int TurnCap = 1000;

    /// <summary>Runs one battle to completion; returns the winner and the number of turns it took.</summary>
    private static (BattleSide Winner, int Turns) Simulate(int seed, AiProfile playerAi, AiProfile enemyAi)
    {
        BattleCreature[] player = PartyA(), enemy = PartyB();
        TypeChart chart = Chart();
        var battle = new BattleController(player, enemy, chart, new Rng(seed));
        var rngP = new Rng(seed + 1);
        var rngE = new Rng(seed + 2);

        int turns = 0;
        while (battle.Outcome is null)
        {
            Assert.True(++turns <= TurnCap, "battle failed to terminate within the turn cap");

            int pActive = Array.FindIndex(player, c => ReferenceEquals(c, battle.Active(BattleSide.Player)));
            int eActive = Array.FindIndex(enemy, c => ReferenceEquals(c, battle.Active(BattleSide.Enemy)));

            BattleAction pAction = TrainerAi.ChooseAction(playerAi,
                new SmartAiContext(player, pActive, enemy, eActive, chart, rngP, Turn: turns));
            BattleAction eAction = TrainerAi.ChooseAction(enemyAi,
                new SmartAiContext(enemy, eActive, player, pActive, chart, rngE, Turn: turns));

            battle.ResolveTurn(pAction, eAction);
        }

        return (battle.Outcome.Winner, turns);
    }

    [Theory]
    [InlineData(AiProfile.Basic, AiProfile.Basic)]
    [InlineData(AiProfile.Smart, AiProfile.Basic)]
    [InlineData(AiProfile.Smart, AiProfile.Smart)]
    public void EveryBattle_TerminatesWithAValidWinner(AiProfile playerAi, AiProfile enemyAi)
    {
        for (int seed = 0; seed < 60; seed++)
        {
            var (winner, _) = Simulate(seed, playerAi, enemyAi);
            Assert.True(winner is BattleSide.Player or BattleSide.Enemy);
        }
    }

    [Theory]
    [InlineData(AiProfile.Basic, AiProfile.Basic)]
    [InlineData(AiProfile.Smart, AiProfile.Basic)]
    [InlineData(AiProfile.Smart, AiProfile.Smart)]
    public void SameSeed_ReplaysIdentically(AiProfile playerAi, AiProfile enemyAi)
    {
        for (int seed = 0; seed < 30; seed++)
            Assert.Equal(Simulate(seed, playerAi, enemyAi), Simulate(seed, playerAi, enemyAi));
    }
}
