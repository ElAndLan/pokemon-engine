using Cgm.Core.Model;
using Cgm.Core.Tests.Battle; // FakeRng test helper

namespace Cgm.Core.Tests.Model;

public sealed class EncounterRollTests
{
    private static EncounterSlot Slot(string species, int weight, int min = 2, int max = 5,
        TimeOfDay? time = null, string? flag = null) => new()
    {
        Species = EntityId.Parse($"species:{species}"),
        Weight = weight,
        MinLevel = min,
        MaxLevel = max,
        TimeOfDay = time,
        RequiredFlag = flag,
    };

    private static EncounterTable Table(params EncounterSlot[] slots) =>
        new() { Id = EntityId.Parse("encounter:t"), Slots = slots };

    [Fact]
    public void PickSlot_SingleSlot_AlwaysReturnsIt()
    {
        EncounterTable t = Table(Slot("a", 100));
        Assert.Equal(EntityId.Parse("species:a"), EncounterRoll.PickSlot(t, new Rng(1))!.Species);
    }

    [Fact]
    public void PickSlot_PicksByCumulativeWeight()
    {
        EncounterTable t = Table(Slot("a", 30), Slot("b", 70));
        // roll 29 → within a's [0,30); roll 30 → b's [30,100).
        Assert.Equal(EntityId.Parse("species:a"), EncounterRoll.PickSlot(t, new FakeRng(ints: [29]))!.Species);
        Assert.Equal(EntityId.Parse("species:b"), EncounterRoll.PickSlot(t, new FakeRng(ints: [30]))!.Species);
    }

    [Fact]
    public void PickSlot_DistributionMatchesWeights()
    {
        EncounterTable t = Table(Slot("a", 75), Slot("b", 25));
        var rng = new Rng(2026);
        int a = Enumerable.Range(0, 10_000)
            .Count(_ => EncounterRoll.PickSlot(t, rng)!.Species == EntityId.Parse("species:a"));
        Assert.InRange(a, 7200, 7800); // ~7500
    }

    [Fact]
    public void PickSlot_EmptyOrZeroWeight_ReturnsNull()
    {
        Assert.Null(EncounterRoll.PickSlot(Table(), new Rng(1)));
        Assert.Null(EncounterRoll.PickSlot(Table(Slot("a", 0)), new Rng(1)));
    }

    [Fact]
    public void PickSlot_FiltersByTimeOfDay()
    {
        EncounterTable t = Table(Slot("nightmon", 100, time: TimeOfDay.Night));
        Assert.Null(EncounterRoll.PickSlot(t, new Rng(1), time: TimeOfDay.Day)); // excluded by day
        Assert.NotNull(EncounterRoll.PickSlot(t, new Rng(1), time: TimeOfDay.Night));
    }

    [Fact]
    public void PickSlot_FiltersByRequiredFlag()
    {
        EncounterTable t = Table(Slot("rare", 100, flag: "story.dex"));
        Assert.Null(EncounterRoll.PickSlot(t, new Rng(1), flagSet: _ => false)); // flag not set
        Assert.NotNull(EncounterRoll.PickSlot(t, new Rng(1), flagSet: _ => true));
    }

    [Fact]
    public void RollLevel_WithinRangeInclusive_AndSpansEnds()
    {
        EncounterSlot s = Slot("a", 1, min: 3, max: 6);
        var rng = new Rng(9);
        var levels = Enumerable.Range(0, 2000).Select(_ => EncounterRoll.RollLevel(s, rng)).ToList();
        Assert.All(levels, l => Assert.InRange(l, 3, 6));
        Assert.Contains(3, levels);
        Assert.Contains(6, levels);
    }

    [Fact]
    public void RollLevel_MinEqualsMax_IsFixed()
    {
        Assert.Equal(5, EncounterRoll.RollLevel(Slot("a", 1, min: 5, max: 5), new Rng(1)));
    }
}
