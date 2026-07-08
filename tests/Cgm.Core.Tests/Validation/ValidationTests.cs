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
    public void TrainerParty_FlagsSightRangeAndDialogue()
    {
        Trainer negRange = Trainer("neg") with { SightRange = -1 };
        Assert.Contains(Run(new TrainerPartyRule(), Project(negRange)),
            i => i.Severity == ValidationSeverity.Error);

        Trainer sightedNoText = Trainer("s") with { SightRange = 3 }; // default dialogue is empty
        Assert.Contains(Run(new TrainerPartyRule(), Project(sightedNoText)),
            i => i.Severity == ValidationSeverity.Warning);

        Trainer sightedOk = Trainer("s2") with
        {
            SightRange = 3,
            Dialogue = new TrainerDialogue { Sight = "Hey, you!" },
        };
        Assert.Empty(Run(new TrainerPartyRule(), Project(sightedOk)));
    }

    [Fact]
    public void TrainerParty_WarnsOnUnlearnableMoveOverride()
    {
        Species mon = Species("mon") with { Learnset = [new LearnsetEntry(3, EntityId.Parse("move:hit"))] };
        EntityId monId = EntityId.Parse("species:mon");

        Trainer unlearnable = Trainer("u") with
        {
            Party = [new PartyMember { Species = monId, Level = 5, Moves = [EntityId.Parse("move:tackle")] }],
        };
        Assert.Contains(Run(new TrainerPartyRule(), Project(unlearnable, mon)),
            i => i.Severity == ValidationSeverity.Warning);

        // Learned at level 3, member is level 2 → too early → warning.
        Trainer tooEarly = Trainer("e") with
        {
            Party = [new PartyMember { Species = monId, Level = 2, Moves = [EntityId.Parse("move:hit")] }],
        };
        Assert.Contains(Run(new TrainerPartyRule(), Project(tooEarly, mon)),
            i => i.Severity == ValidationSeverity.Warning);

        // Learned at level 3, member is level 5 → fine, no issues.
        Trainer legal = Trainer("l") with
        {
            Party = [new PartyMember { Species = monId, Level = 5, Moves = [EntityId.Parse("move:hit")] }],
        };
        Assert.Empty(Run(new TrainerPartyRule(), Project(legal, mon)));
    }

    [Fact]
    public void TrainerParty_FlagsTooManyMoves()
    {
        Species mon = Species("mon");
        var five = Enumerable.Range(0, 5).Select(i => EntityId.Parse($"move:m{i}")).ToList();
        Trainer t = Trainer("x") with
        {
            Party = [new PartyMember { Species = EntityId.Parse("species:mon"), Level = 5, Moves = five }],
        };
        Assert.Contains(Run(new TrainerPartyRule(), Project(t, mon)),
            i => i.Severity == ValidationSeverity.Error);
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

    // --- Asset rules -------------------------------------------------------------

    private static SheetCell Cell(string spriteSlug) =>
        new() { SpriteId = EntityId.Parse($"sprite:{spriteSlug}") };

    [Fact]
    public void Animation_FlagsEmptyAndNonPositiveDurations()
    {
        var empty = new Animation { Id = EntityId.Parse("anim:a"), Frames = [] };
        var badMs = new Animation
        {
            Id = EntityId.Parse("anim:b"),
            Frames = [new AnimFrame(EntityId.Parse("sprite:x"), 0)],
        };
        var ok = new Animation
        {
            Id = EntityId.Parse("anim:c"),
            Frames = [new AnimFrame(EntityId.Parse("sprite:x"), 100)],
        };
        Assert.NotEmpty(Run(new AnimationRule(), Project(empty)));
        Assert.NotEmpty(Run(new AnimationRule(), Project(badMs)));
        Assert.Empty(Run(new AnimationRule(), Project(ok)));
    }

    [Fact]
    public void SpriteUniqueness_FlagsDuplicatesAcrossAndWithinSheets()
    {
        var a = new SpriteSheet { Id = EntityId.Parse("sheet:a"), Cells = [Cell("dup"), Cell("a1")] };
        var b = new SpriteSheet { Id = EntityId.Parse("sheet:b"), Cells = [Cell("dup")] };
        Assert.NotEmpty(Run(new SpriteUniquenessRule(), Project(a, b))); // across sheets

        var withinDup = new SpriteSheet { Id = EntityId.Parse("sheet:c"), Cells = [Cell("x"), Cell("x")] };
        Assert.NotEmpty(Run(new SpriteUniquenessRule(), Project(withinDup))); // within one sheet

        var clean = new SpriteSheet { Id = EntityId.Parse("sheet:d"), Cells = [Cell("p"), Cell("q")] };
        Assert.Empty(Run(new SpriteUniquenessRule(), Project(clean)));
    }

    // --- Map rules ---------------------------------------------------------------

    [Fact]
    public void PlayerStart_RequiresExactlyOne()
    {
        var none = new Map { Id = EntityId.Parse("map:a"), Width = 1, Height = 1 };
        Assert.NotEmpty(Run(new PlayerStartRule(), Project(none)));

        var two = new Map
        {
            Id = EntityId.Parse("map:b"),
            Width = 2,
            Height = 1,
            Entities =
            [
                new PlayerStartEntity { Pos = new GridPos(0, 0) },
                new PlayerStartEntity { Pos = new GridPos(1, 0) },
            ],
        };
        Assert.NotEmpty(Run(new PlayerStartRule(), Project(two)));

        var one = new Map
        {
            Id = EntityId.Parse("map:c"),
            Width = 1,
            Height = 1,
            Entities = [new PlayerStartEntity { Pos = new GridPos(0, 0) }],
        };
        Assert.Empty(Run(new PlayerStartRule(), Project(one)));
    }

    [Fact]
    public void WarpLanding_FlagsSolidLandingTile()
    {
        var tileset = new Tileset
        {
            Id = EntityId.Parse("tileset:t"),
            Tiles = [new Tile(), new Tile { Solid = true }], // 0 open, 1 solid
        };
        var target = new Map
        {
            Id = EntityId.Parse("map:room"),
            Width = 2,
            Height = 1,
            Tilesets = [EntityId.Parse("tileset:t")],
            Layers = new MapLayers { Ground = [0, 1] }, // cell 1 is solid
        };
        WarpEntity ToCell(int cell) =>
            new() { Pos = new GridPos(0, 0), Target = EntityId.Parse("map:room"), TargetPos = new GridPos(cell, 0) };

        var solidWarp = new Map { Id = EntityId.Parse("map:h1"), Width = 1, Height = 1, Entities = [ToCell(1)] };
        var openWarp = new Map { Id = EntityId.Parse("map:h2"), Width = 1, Height = 1, Entities = [ToCell(0)] };

        Assert.NotEmpty(Run(new WarpLandingRule(), Project(solidWarp, target, tileset)));
        Assert.Empty(Run(new WarpLandingRule(), Project(openWarp, target, tileset)));
    }
}
