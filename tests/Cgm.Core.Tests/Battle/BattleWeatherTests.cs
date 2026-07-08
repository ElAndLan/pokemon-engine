using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Weather as a field condition (catalog §7.6): on_turn_end residual + on_damage_query type mods.</summary>
public sealed class BattleWeatherTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Rock = EntityId.Parse("type:rock");

    private static TypeChart Chart() => new(
        [new TypeDef { Id = Normal }, new TypeDef { Id = Water }, new TypeDef { Id = Fire }, new TypeDef { Id = Rock }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove WaterHit() =>
        new(EntityId.Parse("move:surf"), Water, DamageClass.Special, 60, 100, 25, 0, 0);

    private static BattleMove RainMove() =>
        new(EntityId.Parse("move:raindance"), Normal, DamageClass.Status, null, null, 25, 0, 0, setsWeather: Weather.Rain);

    private static BattleMove SandMove() =>
        new(EntityId.Parse("move:sandstorm"), Normal, DamageClass.Status, null, null, 25, 0, 0, setsWeather: Weather.Sandstorm);

    private static BattleCreature Fast(IReadOnlyList<EntityId> types, int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, types, new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(IReadOnlyList<EntityId> types, int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, types, new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void WeatherMove_SetsWeather()
    {
        var player = Fast([Normal], 300, RainMove());
        var enemy = Slow([Normal], 300, Inert());
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(events, e => e is WeatherChanged { Weather: Weather.Rain });
    }

    [Fact]
    public void Rain_BoostsWaterDamage()
    {
        // Same water hit with and without rain; rain should deal more.
        int noRain = DamageDealtBy([WaterHit()], Weather.None);
        int rain = DamageDealtBy([RainMove(), WaterHit()], Weather.Rain);
        Assert.True(rain > noRain, $"rain {rain} should exceed clear {noRain}");
    }

    private static int DamageDealtBy(BattleMove[] playerMoves, Weather _)
    {
        var player = Fast([Normal], 300, playerMoves);
        var enemy = Slow([Normal], 9999, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(5));
        if (playerMoves.Length == 2)
        {
            battle.ResolveTurn(new UseMove(0), new UseMove(0)); // set weather
            int before = enemy.CurrentHp;
            battle.ResolveTurn(new UseMove(1), new UseMove(0)); // water hit under weather
            return before - enemy.CurrentHp;
        }
        int b = enemy.CurrentHp;
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        return b - enemy.CurrentHp;
    }

    [Fact]
    public void Sandstorm_ChipsNonImmuneActives()
    {
        var player = Fast([Normal], 320, SandMove());
        var enemy = Slow([Normal], 320, Inert());
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(320 - 320 / 16, enemy.CurrentHp); // 1/16 chip
        Assert.Equal(320 - 320 / 16, player.CurrentHp);
        Assert.Contains(events, e => e is WeatherDamage);
    }

    [Fact]
    public void Sandstorm_DoesNotChipImmuneType()
    {
        var player = Fast([Rock], 320, SandMove()); // Rock is immune to sandstorm
        var enemy = Slow([Normal], 320, Inert());
        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(320, player.CurrentHp);          // rock takes no chip
        Assert.Equal(320 - 320 / 16, enemy.CurrentHp); // normal does
    }

    [Fact]
    public void Weather_ExpiresAfterFiveTurns()
    {
        var player = Fast([Rock], 9999, SandMove(), Inert()); // rock so it never faints from chip
        var enemy = Slow([Rock], 9999, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // turn 1 sets weather (counts as turn 1)
        for (int i = 0; i < 3; i++)
            battle.ResolveTurn(new UseMove(1), new UseMove(0)); // turns 2–4
        var last = battle.ResolveTurn(new UseMove(1), new UseMove(0)); // turn 5 → expires

        Assert.Contains(last, e => e is WeatherEnded { Weather: Weather.Sandstorm });
    }
}
