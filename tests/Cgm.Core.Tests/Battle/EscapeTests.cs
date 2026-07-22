using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class EscapeTests
{
    private static readonly EntityId Neutral = EntityId.Parse("type:neutral");

    private static TypeChart Chart() => new([new TypeDef { Id = Neutral }]);

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Neutral,
        DamageClass.Status, null, null, 10, 0, 0);

    private static BattleCreature Creature(int level, int speed, IReadOnlyList<AbilityHook>? hooks = null) =>
        new(EntityId.Parse("species:creature"), "Creature", level, [Neutral],
            new Stats(100, 50, 50, 50, 50, speed), [Wait()], abilityHooks: hooks);

    private static BattleController Wild(BattleCreature player, BattleCreature enemy, IRng rng) =>
        new(player, enemy, Chart(), rng, isWild: true);

    [Theory]
    [InlineData(100, 100, 1, 256)]
    [InlineData(20, 100, 1, 55)]
    [InlineData(20, 100, 2, 85)]
    [InlineData(1, 1000, 9, 256)]
    public void OddsMatchModernFormula(int playerSpeed, int wildSpeed, int attempt, int expected)
    {
        EscapeResult result = EscapeCalc.Attempt(playerSpeed, wildSpeed, attempt,
            expected == 256 ? new FakeRng() : new FakeRng(ints: [255]));

        Assert.Equal(expected, result.Odds);
        Assert.Equal(expected == 256, result.Escaped);
    }

    [Fact]
    public void FasterPlayerEscapesWithoutDrawingRng()
    {
        BattleController battle = Wild(Creature(10, 100), Creature(20, 99), new FakeRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Run(), new Pass());

        Assert.Contains(events, e => e is Escaped { Attempt: 1, Odds: 256, Roll: null });
        Assert.True(battle.Outcome!.IsDraw);
    }

    [Fact]
    public void HigherLevelFasterWildCreatureCanStopTheFirstAttempt()
    {
        BattleController battle = Wild(Creature(5, 20), Creature(50, 100), new FakeRng(ints: [200]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Run(), new UseMove(0));

        Assert.Contains(events, e => e is EscapeFailed { Attempt: 1, Odds: 55, Roll: 200 });
        Assert.Contains(events, e => e is MoveUsed { Side: BattleSide.Enemy });
        Assert.Null(battle.Outcome);
    }

    [Fact]
    public void RepeatedAttemptsBecomeEasierAndEventuallyEscape()
    {
        BattleController battle = Wild(Creature(5, 20), Creature(50, 100),
            new FakeRng(ints: [255, 60]));

        Assert.Contains(battle.ResolveTurn(new Run(), new Pass()),
            e => e is EscapeFailed { Attempt: 1, Odds: 55 });
        Assert.Contains(battle.ResolveTurn(new Run(), new Pass()),
            e => e is Escaped { Attempt: 2, Odds: 85, Roll: 60 });
        Assert.NotNull(battle.Outcome);
    }

    [Fact]
    public void TrapPreventsEscapeWithoutDrawingOrRemovingRunFromTheMenuSurface()
    {
        BattleCreature player = Creature(10, 100);
        player.SetTrap(2);
        BattleController battle = Wild(player, Creature(10, 10), new FakeRng());

        Assert.True(battle.CanSubmitAction(BattleSide.Player, new Run()));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Run(), new Pass());

        Assert.Contains(events, e => e is EscapePrevented { Reason: EscapePreventionReason.Trapped });
        Assert.Null(battle.Outcome);
    }

    [Fact]
    public void EffectiveOpponentAbilityCanPreventEscapeWithoutNamedAbilityLogic()
    {
        AbilityHook blocker = new()
        {
            Hook = AbilityHookPoint.OnEscapeAttempt,
            Effects = [new Effect { Op = "escapeBlock" }],
        };
        BattleController battle = Wild(Creature(10, 100), Creature(10, 10, [blocker]), new FakeRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Run(), new UseMove(0));

        Assert.Contains(events, e => e is EscapePrevented
            { Reason: EscapePreventionReason.Ability, Blocker.Side: BattleSide.Enemy });
        Assert.Contains(events, e => e is MoveUsed { Side: BattleSide.Enemy });
        Assert.Null(battle.Outcome);
    }

    [Fact]
    public void TrainerBattleAcceptsTheMenuActionButRefusesEscapeWithoutDrawingRng()
    {
        var battle = new BattleController(Creature(10, 100), Creature(10, 10), Chart(), new FakeRng());

        Assert.True(battle.CanSubmitAction(BattleSide.Player, new Run()));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Run(), new UseMove(0));

        Assert.Contains(events, e => e is EscapePrevented
            { Reason: EscapePreventionReason.TrainerBattle });
        Assert.Contains(events, e => e is MoveUsed { Side: BattleSide.Enemy });
        Assert.Null(battle.Outcome);
    }
}
