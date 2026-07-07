using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class TypeChartTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Steel = EntityId.Parse("type:steel");
    private static readonly EntityId Ground = EntityId.Parse("type:ground");
    private static readonly EntityId Flying = EntityId.Parse("type:flying");

    private static TypeChart Chart()
    {
        var fire = new TypeDef
        {
            Id = Fire,
            DoubleDamageTo = [Grass, Steel],
            HalfDamageTo = [Fire, Water],
        };
        var ground = new TypeDef
        {
            Id = Ground,
            DoubleDamageTo = [Fire, Steel],
            NoDamageTo = [Flying], // ground can't hit flying
        };
        var grass = new TypeDef { Id = Grass, DoubleDamageTo = [Water] };
        return new TypeChart([fire, ground, grass, new TypeDef { Id = Water }, new TypeDef { Id = Steel }, new TypeDef { Id = Flying }]);
    }

    [Theory]
    [InlineData("type:fire", "type:grass", 2.0)]
    [InlineData("type:fire", "type:fire", 0.5)]
    [InlineData("type:fire", "type:water", 0.5)]
    [InlineData("type:fire", "type:flying", 1.0)] // no relation
    [InlineData("type:ground", "type:flying", 0.0)] // immunity
    public void Single_MatchesData(string move, string def, double expected)
    {
        Assert.Equal(expected, Chart().Single(EntityId.Parse(move), EntityId.Parse(def)));
    }

    [Fact]
    public void Effectiveness_DualType_IsProduct()
    {
        // Fire vs (Grass, Steel) = 2 × 2 = 4.
        Assert.Equal(4.0, Chart().Effectiveness(Fire, [Grass, Steel]));
        // Fire vs (Water, Fire) = 0.5 × 0.5 = 0.25.
        Assert.Equal(0.25, Chart().Effectiveness(Fire, [Water, Fire]));
    }

    [Fact]
    public void Effectiveness_ImmunityInEitherType_IsZero()
    {
        // Ground vs (Flying, Steel) = 0 × 2 = 0.
        Assert.Equal(0.0, Chart().Effectiveness(Ground, [Flying, Steel]));
    }

    [Fact]
    public void Effectiveness_SingleType_And_Neutral()
    {
        Assert.Equal(2.0, Chart().Effectiveness(Fire, [Grass]));
        Assert.Equal(1.0, Chart().Effectiveness(Fire, [Flying]));
    }

    [Fact]
    public void UnknownMoveType_IsNeutral()
    {
        Assert.Equal(1.0, Chart().Single(EntityId.Parse("type:mystery"), Grass));
    }

    [Fact]
    public void Stab_AppliesOnlyOnTypeMatch()
    {
        Assert.Equal(1.5, TypeChart.Stab(Fire, [Fire, Flying]));
        Assert.Equal(1.0, TypeChart.Stab(Fire, [Water]));
    }
}
