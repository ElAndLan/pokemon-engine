using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;
using Cgm.Core.Validation.Rules;
using static Cgm.Core.Tests.Validation.TestEntities;

namespace Cgm.Core.Tests.Validation;

public sealed class ValidationTests
{
    // --- Framework / integration -------------------------------------------------

    [Fact]
    public void FixtureMin_ValidatesWithNoErrors()
    {
        Project p = ProjectLoader.Load(TestPaths.Sample("fixture-min"));
        ValidationReport report = Validator.Run(p);
        Assert.False(report.HasErrors, string.Join("\n", report.Issues));
    }

    [Fact]
    public void DefaultRules_HasAtLeastTwelveDistinctRules()
    {
        var ids = Validator.DefaultRules.Select(r => r.Id).ToList();
        Assert.True(ids.Count >= 12);
        Assert.Equal(ids.Count, ids.Distinct().Count()); // no duplicate rule ids
    }

    private static IReadOnlyList<ValidationIssue> Run(IValidationRule rule, Project p) =>
        rule.Check(p).ToList();

    // --- BrokenReference ---------------------------------------------------------

    [Fact]
    public void BrokenReference_FlagsMissingTarget()
    {
        Species mon = Species() with { Types = [EntityId.Parse("type:missing")] };
        var issues = Run(new BrokenReferenceRule(), Project(mon));
        Assert.Contains(issues, i => i.Message.Contains("type:missing"));
    }

    [Fact]
    public void BrokenReference_PassesWhenTargetExists()
    {
        var type = new TypeDef { Id = EntityId.Parse("type:fire"), Name = "Fire" };
        Assert.Empty(Run(new BrokenReferenceRule(), Project(Species(), type)));
    }

    [Fact]
    public void BrokenReference_ChecksProjectSettings()
    {
        var settings = new ProjectSettings { Name = "T", StartMap = EntityId.Parse("map:ghost") };
        var issues = Run(new BrokenReferenceRule(), Project(settings));
        Assert.Contains(issues, i => i.Message.Contains("map:ghost"));
    }

    // --- Project rules -----------------------------------------------------------

    [Fact]
    public void StartMapExists_FlagsUnset()
    {
        Assert.NotEmpty(Run(new StartMapExistsRule(), Project()));
    }

    [Fact]
    public void StarterParty_FlagsEmptyAndOversized()
    {
        var empty = new ProjectSettings { Name = "T", StarterParty = [] };
        Assert.NotEmpty(Run(new StarterPartyRule(), Project(empty)));

        EntityId[] seven = Enumerable.Range(0, 7).Select(i => EntityId.Parse($"species:s{i}")).ToArray();
        var big = new ProjectSettings { Name = "T", StarterParty = seven };
        Assert.NotEmpty(Run(new StarterPartyRule(), Project(big)));
    }

    // --- Species rules -----------------------------------------------------------

    [Fact]
    public void GrowthRate_FlagsUnknownKey()
    {
        Species bad = Species() with { GrowthRate = "turbo" };
        Assert.NotEmpty(Run(new GrowthRateRule(), Project(bad)));
        Assert.Empty(Run(new GrowthRateRule(), Project(Species())));
    }

    [Fact]
    public void SpeciesTypes_FlagsWrongCountAndDuplicates()
    {
        Species none = Species("a") with { Types = [] };
        Species dup = Species("b") with { Types = [EntityId.Parse("type:fire"), EntityId.Parse("type:fire")] };
        Assert.NotEmpty(Run(new SpeciesTypesRule(), Project(none)));
        Assert.NotEmpty(Run(new SpeciesTypesRule(), Project(dup)));
        Assert.Empty(Run(new SpeciesTypesRule(), Project(Species())));
    }

    [Fact]
    public void SpeciesStats_FlagsOutOfRange()
    {
        Species zeroHp = Species("a") with { BaseStats = new Stats(0, 45, 45, 45, 45, 45) };
        Species badCatch = Species("b") with { CatchRate = 300 };
        Assert.NotEmpty(Run(new SpeciesStatsRule(), Project(zeroHp)));
        Assert.NotEmpty(Run(new SpeciesStatsRule(), Project(badCatch)));
        Assert.Empty(Run(new SpeciesStatsRule(), Project(Species())));
    }

    [Fact]
    public void Learnset_FlagsBadLevel()
    {
        Species bad = Species() with { Learnset = [new LearnsetEntry(0, EntityId.Parse("move:hit"))] };
        Assert.NotEmpty(Run(new LearnsetRule(), Project(bad)));
    }

    [Fact]
    public void Evolution_FlagsSelfTargetAndBadLevel()
    {
        Species self = Species("a") with
        {
            Evolutions = [new Evolution { Target = EntityId.Parse("species:a"), Trigger = EvolutionTrigger.LevelUp }],
        };
        Species lowLevel = Species("b") with
        {
            Evolutions = [new Evolution { Target = EntityId.Parse("species:c"), Trigger = EvolutionTrigger.LevelUp, MinLevel = 1 }],
        };
        Assert.NotEmpty(Run(new EvolutionRule(), Project(self)));
        Assert.NotEmpty(Run(new EvolutionRule(), Project(lowLevel)));
    }

    // --- Move rule ---------------------------------------------------------------

    [Fact]
    public void Move_FlagsPowerClassMismatchAndRanges()
    {
        Move statusWithPower = Move("a") with { DamageClass = DamageClass.Status, Power = 40 };
        Move damagingNoPower = Move("b") with { DamageClass = DamageClass.Physical, Power = null };
        Move badAcc = Move("c") with { Accuracy = 200 };
        Move noPp = Move("d") with { Pp = 0 };
        Assert.NotEmpty(Run(new MoveRule(), Project(statusWithPower)));
        Assert.NotEmpty(Run(new MoveRule(), Project(damagingNoPower)));
        Assert.NotEmpty(Run(new MoveRule(), Project(badAcc)));
        Assert.NotEmpty(Run(new MoveRule(), Project(noPp)));
        Assert.Empty(Run(new MoveRule(), Project(Move())));
    }

    // --- World rules -------------------------------------------------------------

    [Fact]
    public void EncounterTable_FlagsEmptyWeightAndLevelRange()
    {
        var emptyTable = new EncounterTable { Id = EntityId.Parse("encounter:a"), Name = "A", Slots = [] };
        var badSlot = new EncounterTable
        {
            Id = EntityId.Parse("encounter:b"),
            Name = "B",
            Slots = [new EncounterSlot { Species = EntityId.Parse("species:mon"), Weight = 0, MinLevel = 9, MaxLevel = 3 }],
        };
        Assert.NotEmpty(Run(new EncounterTableRule(), Project(emptyTable)));
        Assert.True(Run(new EncounterTableRule(), Project(badSlot)).Count >= 2); // weight + range
    }

    [Fact]
    public void TrainerParty_FlagsSizeAndLevel()
    {
        Trainer empty = Trainer("a") with { Party = [] };
        Trainer badLevel = Trainer("b") with
        {
            Party = [new PartyMember { Species = EntityId.Parse("species:mon"), Level = 0 }],
        };
        Assert.NotEmpty(Run(new TrainerPartyRule(), Project(empty)));
        Assert.NotEmpty(Run(new TrainerPartyRule(), Project(badLevel)));
        Assert.Empty(Run(new TrainerPartyRule(), Project(Trainer())));
    }

    [Fact]
    public void WarpTarget_FlagsOutOfBoundsLanding()
    {
        var target = new Map { Id = EntityId.Parse("map:room"), Name = "Room", Width = 4, Height = 3 };
        var source = new Map
        {
            Id = EntityId.Parse("map:hall"),
            Name = "Hall",
            Width = 4,
            Height = 4,
            Entities = [new WarpEntity { Pos = new GridPos(0, 0), Target = EntityId.Parse("map:room"), TargetPos = new GridPos(9, 9) }],
        };
        Assert.NotEmpty(Run(new WarpTargetRule(), Project(source, target)));
    }
}
