using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class EvolutionCheckTests
{
    private static readonly EntityId Target = EntityId.Parse("species:evolved");
    private static readonly EntityId Stone = EntityId.Parse("item:fire_stone");
    private static readonly EntityId LinkCord = EntityId.Parse("item:link_cord");
    private static readonly EntityId AncientMove = EntityId.Parse("move:ancient_power");

    private static Species SpeciesWith(params Evolution[] evolutions) => new()
    {
        Id = EntityId.Parse("species:base"),
        Name = "Base",
        Types = [EntityId.Parse("type:normal")],
        Evolutions = evolutions,
    };

    private static Evolution Evo(EvolutionTrigger trigger) => new() { Target = Target, Trigger = trigger };

    // --- Level-up ---------------------------------------------------------------

    [Fact]
    public void LevelUp_AtOrAboveMinLevel_Evolves()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.LevelUp) with { MinLevel = 16 });
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, level: 16)));
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, level: 30)));
    }

    [Fact]
    public void LevelUp_BelowMinLevel_DoesNotEvolve()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.LevelUp) with { MinLevel = 16 });
        Assert.Null(EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, level: 15)));
    }

    // --- Item -------------------------------------------------------------------

    [Fact]
    public void UseItem_MatchingStone_Evolves()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.UseItem) with { Item = Stone });
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.UseItem, usedItem: Stone)));
    }

    [Fact]
    public void UseItem_WrongStone_DoesNotEvolve()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.UseItem) with { Item = Stone });
        Assert.Null(EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.UseItem, usedItem: EntityId.Parse("item:water_stone"))));
    }

    // --- Happiness (on level-up) ------------------------------------------------

    [Fact]
    public void Happiness_ThresholdMet_Evolves()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.LevelUp) with { MinHappiness = 220 });
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, happiness: 220)));
    }

    [Fact]
    public void Happiness_ThresholdNotMet_DoesNotEvolve()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.LevelUp) with { MinHappiness = 220 });
        Assert.Null(EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, happiness: 219)));
    }

    // --- Time of day ------------------------------------------------------------

    [Theory]
    [InlineData(TimeOfDay.Night, true)]
    [InlineData(TimeOfDay.Day, false)]
    public void TimeOfDay_Conditional(TimeOfDay clock, bool evolves)
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.LevelUp) with { MinLevel = 5, TimeOfDay = TimeOfDay.Night });
        EntityId? result = EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, level: 20, timeOfDay: clock));
        Assert.Equal(evolves ? Target : null, result);
    }

    // --- Trade (+ held item / known move) --------------------------------------

    [Fact]
    public void Trade_Evolves()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.Trade));
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.Trade)));
    }

    [Fact]
    public void Trade_WithHeldItem_RequiresThatItem()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.Trade) with { HeldItem = LinkCord });
        Assert.Null(EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.Trade)));                         // no item
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.Trade, heldItem: LinkCord)));
    }

    [Fact]
    public void KnownMove_Required()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.LevelUp) with { KnownMove = AncientMove });
        Assert.Null(EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp)));                        // doesn't know it
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, knownMoves: [AncientMove])));
    }

    // --- Ordering / wrong trigger ----------------------------------------------

    [Fact]
    public void WrongTrigger_DoesNotEvolve()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.UseItem) with { Item = Stone });
        Assert.Null(EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, level: 50)));
    }

    [Fact]
    public void FirstMatchingEvolutionWins()
    {
        // A branching species: night → one target, day → another. First in list that matches wins.
        EntityId night = EntityId.Parse("species:nightform");
        EntityId day = EntityId.Parse("species:dayform");
        Species s = SpeciesWith(
            new Evolution { Target = night, Trigger = EvolutionTrigger.LevelUp, MinLevel = 10, TimeOfDay = TimeOfDay.Night },
            new Evolution { Target = day, Trigger = EvolutionTrigger.LevelUp, MinLevel = 10, TimeOfDay = TimeOfDay.Day });

        Assert.Equal(day, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, level: 12, timeOfDay: TimeOfDay.Day)));
        Assert.Equal(night, EvolutionCheck.Evaluate(s, Ctx(EvolutionTrigger.LevelUp, level: 12, timeOfDay: TimeOfDay.Night)));
    }

    [Fact]
    public void NoEvolutions_ReturnsNull()
    {
        Assert.Null(EvolutionCheck.Evaluate(SpeciesWith(), Ctx(EvolutionTrigger.LevelUp, level: 99)));
    }

    [Fact]
    public void InstanceOverload_DerivesLevelHappinessHeldItemAndMoves()
    {
        Species s = SpeciesWith(Evo(EvolutionTrigger.LevelUp) with { MinLevel = 20, KnownMove = AncientMove });
        var creature = new CreatureInstance
        {
            Species = EntityId.Parse("species:base"),
            Level = 25,
            Happiness = 100,
            Moves = [new MoveSlot(AncientMove, 5)],
        };
        Assert.Equal(Target, EvolutionCheck.Evaluate(s, creature, EvolutionTrigger.LevelUp, TimeOfDay.Day));
    }

    private static EvolutionContext Ctx(EvolutionTrigger trigger, int level = 5, int happiness = 70,
        TimeOfDay timeOfDay = TimeOfDay.Day, EntityId? usedItem = null, EntityId? heldItem = null,
        IReadOnlyCollection<EntityId>? knownMoves = null) =>
        new(trigger, level, happiness, timeOfDay, usedItem, heldItem, knownMoves);
}
