using System.Text.Json;
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
    public void DemoGame_HasPhase15ShowcaseContent()
    {
        Project p = ProjectLoader.Load(TestPaths.Sample("demo-game"));
        ValidationReport report = Validator.Run(p);
        Assert.Empty(report.Issues);

        Assert.Equal(4, p.All<Ability>().Count());
        Assert.Contains(p.All<Move>(), m => m.Id == EntityId.Parse("move:flare_break"));

        IReadOnlyList<Item> heldItems = p.All<Item>()
            .Where(i => i.Holdable && i.BattleEffects.Count > 0)
            .OrderBy(i => i.Id.ToString())
            .ToList();
        Assert.Equal(
            [EntityId.Parse("item:bloom_stone"), EntityId.Parse("item:storm_band"), EntityId.Parse("item:surge_sash")],
            heldItems.Select(i => i.Id).ToList());

        Species species = p.Find<Species>(EntityId.Parse("species:asterling"))!;
        Assert.Contains(species.Forms, f => f.Activation == FormActivation.BattleTemporary);
        Assert.Contains(species.Forms, f => f.Activation == FormActivation.BattleTimed);

        Trainer trainer = p.Find<Trainer>(EntityId.Parse("trainer:expert_rematch_mira"))!;
        Assert.Equal(AiProfile.Smart, trainer.AiProfile);
        Assert.Equal(3, trainer.Party.Count);
        Assert.Equal(heldItems.Select(i => i.Id), trainer.Party.Select(m => m.HeldItem!.Value));
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

    [Fact]
    public void BrokenReference_ChecksDictionaryKeysAndValues()
    {
        Species mon = Species() with
        {
            Forms =
            [
                ValidForm() with
                {
                    MoveRemap = new Dictionary<EntityId, EntityId>
                    {
                        [EntityId.Parse("move:old")] = EntityId.Parse("move:new"),
                    },
                },
            ],
        };

        var issues = Run(new BrokenReferenceRule(), Project(mon));

        Assert.Contains(issues, i => i.Message.Contains("move:old"));
        Assert.Contains(issues, i => i.Message.Contains("move:new"));
    }

    [Fact]
    public void BrokenReference_ChecksPhase15AbilityFormAndItemRefs()
    {
        Species mon = Species() with
        {
            Abilities = [EntityId.Parse("ability:normal")],
            HiddenAbility = EntityId.Parse("ability:hidden"),
            Forms =
            [
                ValidForm("burst") with
                {
                    AbilityOverride = EntityId.Parse("ability:form"),
                    RequiredHeldItem = EntityId.Parse("item:held"),
                    RequiredTrainerItem = EntityId.Parse("item:key"),
                    Condition = new FormCondition { HeldItem = EntityId.Parse("item:condition") },
                },
            ],
        };

        var issues = Run(new BrokenReferenceRule(), Project(mon));

        Assert.Contains(issues, i => i.Message.Contains("ability:normal"));
        Assert.Contains(issues, i => i.Message.Contains("ability:hidden"));
        Assert.Contains(issues, i => i.Message.Contains("ability:form"));
        Assert.Contains(issues, i => i.Message.Contains("item:held"));
        Assert.Contains(issues, i => i.Message.Contains("item:key"));
        Assert.Contains(issues, i => i.Message.Contains("item:condition"));
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
        Species badWeight = Species("c") with { WeightHectograms = 0 };
        Species badHeight = Species("d") with { HeightDecimeters = -1 };
        Assert.NotEmpty(Run(new SpeciesStatsRule(), Project(zeroHp)));
        Assert.NotEmpty(Run(new SpeciesStatsRule(), Project(badCatch)));
        Assert.NotEmpty(Run(new SpeciesStatsRule(), Project(badWeight)));
        Assert.NotEmpty(Run(new SpeciesStatsRule(), Project(badHeight)));
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

    [Fact]
    public void AbilityHook_FlagsUnknownOpChanceAndBadFraction()
    {
        var ability = new Ability
        {
            Id = EntityId.Parse("ability:a"),
            Name = "A",
            Hooks =
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnEndOfTurn,
                    Effects =
                    [
                        new Effect { Op = "madeUp" },
                        new Effect { Op = "residualHeal", Chance = 0 },
                        new Effect { Op = "residualDamage", Params = Params(("den", 0)) },
                        new Effect { Op = "weatherSummon" },
                        new Effect { Op = "terrainSummon" },
                        new Effect { Op = "terrainSummon", Params = Params(("terrain", "none")) },
                        new Effect { Op = "terrainSummon", Params = Params(("terrain", "1")) },
                        new Effect { Op = "terrainSummon", Params = Params(("terrain", "electric"), ("duration", "five"), ("extra", 1)) },
                        new Effect { Op = "statusImmunity" },
                        new Effect { Op = "statusCure" },
                        new Effect { Op = "statusCure", Params = Params(("status", "dizzy")) },
                        new Effect { Op = "typeDamageModify", Params = Params(("multiplierPercent", 150)) },
                        new Effect { Op = "statModify" },
                        new Effect { Op = "statModify", Params = Params(("stat", "hp"), ("multiplierPercent", 150)) },
                        new Effect { Op = "contactChanceEffect" },
                        new Effect { Op = "contactChanceEffect", Params = Params(("status", "dizzy")) },
                        new Effect { Op = "contactChanceEffect", Params = Params(("stat", "hp"), ("delta", -1)) },
                        new Effect { Op = "contactChanceEffect", Params = Params(("stat", "atk"), ("delta", 0)) },
                    ],
                },
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnTerrainChange,
                    Effects = [new Effect { Op = "residualHeal", Params = Params(("num", 1), ("den", 16)) }],
                },
            ],
        };

        var issues = Run(new AbilityHookRule(), Project(ability));

        Assert.Contains(issues, i => i.Message.Contains("madeUp"));
        Assert.Contains(issues, i => i.Message.Contains("chance 0"));
        Assert.Contains(issues, i => i.Message.Contains("den"));
        Assert.Contains(issues, i => i.Message.Contains("num"));
        Assert.Contains(issues, i => i.Message.Contains("weather"));
        Assert.Contains(issues, i => i.Message.Contains("requires string param 'terrain'"));
        Assert.Contains(issues, i => i.Message.Contains("unknown terrain"));
        Assert.Contains(issues, i => i.Message.Contains("duration") && i.Message.Contains("integer"));
        Assert.Contains(issues, i => i.Message.Contains("unknown param 'extra'"));
        Assert.Contains(issues, i => i.Message.Contains("terrainSummon") && i.Message.Contains("requires onSwitchIn"));
        Assert.Contains(issues, i => i.Message.Contains("onTerrainChange") && i.Message.Contains("residualHeal"));
        Assert.Contains(issues, i => i.Message.Contains("status"));
        Assert.Contains(issues, i => i.Message.Contains("unknown status"));
        Assert.Contains(issues, i => i.Message.Contains("type"));
        Assert.Contains(issues, i => i.Message.Contains("stat"));
        Assert.Contains(issues, i => i.Message.Contains("unknown stat"));
        Assert.Contains(issues, i => i.Message.Contains("multiplierPercent") && i.Message.Contains("add"));
        Assert.Contains(issues, i => i.Message.Contains("contactChanceEffect") && i.Message.Contains("requires"));
        Assert.Contains(issues, i => i.Message.Contains("contactChanceEffect") && i.Message.Contains("unknown status"));
        Assert.Contains(issues, i => i.Message.Contains("contactChanceEffect") && i.Message.Contains("unknown stat"));
        Assert.Contains(issues, i => i.Message.Contains("contactChanceEffect") && i.Message.Contains("delta"));
        Assert.Empty(Run(new AbilityHookRule(), Project(ability with
        {
            Hooks =
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnSwitchIn,
                    Effects =
                    [
                        new Effect { Op = "weatherSummon", Chance = 100, Params = Params(("weather", "rain"), ("duration", 5)) },
                        new Effect { Op = "terrainSummon", Params = Params(("terrain", "grassy"), ("duration", 5)) },
                        new Effect { Op = "residualHeal", Params = Params(("num", 1), ("den", 16)) },
                        new Effect { Op = "statusImmunity", Params = Params(("status", "burn")) },
                        new Effect { Op = "statModify", Params = Params(("stat", "atk"), ("multiplierPercent", 150)) },
                        new Effect { Op = "statModify", Params = Params(("stat", "def"), ("add", -10)) },
                        new Effect { Op = "contactChanceEffect", Chance = 30, Params = Params(("status", "poison")) },
                        new Effect { Op = "contactChanceEffect", Params = Params(("stat", "atk"), ("delta", -1)) },
                    ],
                },
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnTerrainChange,
                    Effects = [new Effect { Op = "terrainSummon", Params = Params(("terrain", "misty")) }],
                },
            ],
        })));
    }

    [Fact]
    public void AbilityHook_FlagsDeferredHookPoints()
    {
        var ability = new Ability
        {
            Id = EntityId.Parse("ability:a"),
            Name = "A",
            Hooks =
            [
                new AbilityHook { Hook = AbilityHookPoint.OnModifyStat },
                new AbilityHook { Hook = AbilityHookPoint.OnFaint },
            ],
        };

        var issues = Run(new AbilityHookRule(), Project(ability));

        Assert.Contains(issues, i => i.Message.Contains("OnModifyStat"));
        Assert.Contains(issues, i => i.Message.Contains("OnFaint"));
    }

    [Fact]
    public void SideConditionBypass_RequiresOutgoingDamageHookAndClosedTag()
    {
        var invalid = new Ability
        {
            Id = EntityId.Parse("ability:invalid_screen_bypass"),
            Name = "Invalid Screen Bypass",
            Hooks = [new AbilityHook
            {
                Hook = AbilityHookPoint.OnSwitchIn,
                Effects = [new Effect { Op = "sideConditionBypass", Chance = 100,
                    Params = Params(("tag", "other"), ("extra", 1)) }],
            }],
        };
        ValidationIssue[] issues = Run(new AbilityHookRule(), Project(invalid)).ToArray();
        Assert.Contains(issues, issue => issue.Message.Contains("requires onModifyOutgoingDamage"));
        Assert.Contains(issues, issue => issue.Message.Contains("requires tag"));
        Assert.Contains(issues, issue => issue.Message.Contains("unknown param"));
        Assert.Contains(issues, issue => issue.Message.Contains("does not support chance"));

        var valid = invalid with
        {
            Hooks = [new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyOutgoingDamage,
                Effects =
                [
                    new Effect { Op = "sideConditionBypass", Params = Params(("tag", "screen")) },
                    new Effect { Op = "sideConditionBypass", Params = Params(("tag", "status_guard")) },
                    new Effect { Op = "sideConditionBypass", Params = Params(("tag", "stage_guard")) },
                    new Effect { Op = "sideConditionBypass", Params = Params(("tag", "side_protection")) },
                ],
            }],
        };
        Assert.Empty(Run(new AbilityHookRule(), Project(valid)));
    }

    [Fact]
    public void ProtectionBypass_RequiresOutgoingDamageHookAndNoPayload()
    {
        var invalid = new Ability
        {
            Id = EntityId.Parse("ability:invalid_protection_bypass"),
            Name = "Invalid Protection Bypass",
            Hooks = [new AbilityHook
            {
                Hook = AbilityHookPoint.OnSwitchIn,
                Effects = [new Effect { Op = "protectionBypass", Chance = 100,
                    Params = Params(("extra", 1)) }],
            }],
        };
        ValidationIssue[] issues = Run(new AbilityHookRule(), Project(invalid)).ToArray();
        Assert.Contains(issues, issue => issue.Message.Contains("requires onModifyOutgoingDamage"));
        Assert.Contains(issues, issue => issue.Message.Contains("unknown param"));
        Assert.Contains(issues, issue => issue.Message.Contains("does not support chance"));

        Ability valid = invalid with
        {
            Hooks = [new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyOutgoingDamage,
                Effects = [new Effect { Op = "protectionBypass" }],
            }],
        };
        Assert.Empty(Run(new AbilityHookRule(), Project(valid)));
    }

    [Fact]
    public void GroundedModify_RequiresItsQueryHookAndClosedStateShape()
    {
        var invalid = new Ability
        {
            Id = EntityId.Parse("ability:invalid_grounding"),
            Name = "Invalid Grounding",
            Hooks =
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnSwitchIn,
                    Effects = [new Effect { Op = "groundedModify", Params = Params(("state", "grounded")) }],
                },
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnGroundedQuery,
                    Effects =
                    [
                        new Effect { Op = "groundedModify", Chance = 50, Params = Params(("state", "floating")) },
                        new Effect { Op = "groundedModify", Params = Params(("state", "airborne"), ("extra", 1)) },
                        new Effect { Op = "residualHeal", Params = Params(("num", 1), ("den", 16)) },
                    ],
                },
            ],
        };

        ValidationIssue[] issues = Run(new AbilityHookRule(), Project(invalid)).ToArray();
        Assert.Contains(issues, issue => issue.Message.Contains("requires onGroundedQuery"));
        Assert.Contains(issues, issue => issue.Message.Contains("grounded") && issue.Message.Contains("airborne"));
        Assert.Contains(issues, issue => issue.Message.Contains("unknown param 'extra'"));
        Assert.Contains(issues, issue => issue.Message.Contains("does not support chance"));
        Assert.Contains(issues, issue => issue.Message.Contains("onGroundedQuery") && issue.Message.Contains("residualHeal"));

        var valid = invalid with
        {
            Hooks =
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnGroundedQuery,
                    Effects = [new Effect { Op = "groundedModify", Params = Params(("state", "airborne")) }],
                },
            ],
        };
        Assert.Empty(Run(new AbilityHookRule(), Project(valid)));
    }

    [Fact]
    public void HeldItemBattleEffects_RequireHoldableAndClosedOps()
    {
        var item = new Item
        {
            Id = EntityId.Parse("item:berry"),
            Name = "Berry",
            Holdable = false,
            BattleEffects =
            [
                new Effect { Op = "damage" },
                new Effect { Op = "thresholdHeal", Params = Params(("thresholdPercent", 101)) },
                new Effect { Op = "thresholdHeal", Params = Params(("thresholdPercent", 50), ("healAmount", 20), ("healFractionPercent", 25)) },
                new Effect { Op = "statusCure" },
                new Effect { Op = "typeDamageBoost", Params = Params(("type", "fire")) },
                new Effect { Op = "choiceLock" },
                new Effect { Op = "choiceLock", Params = Params(("damageClass", "status"), ("multiplierPercent", 150)) },
                new Effect { Op = "residualHeal", Params = Params(("den", 0)) },
                new Effect { Op = "surviveFromFull", Params = Params(("amount", 1)) },
                new Effect { Op = "weatherDurationExtend" },
                new Effect { Op = "weatherDurationExtend", Params = Params(("turns", 0)) },
                new Effect { Op = "terrainDurationExtend" },
                new Effect { Op = "terrainDurationExtend", Params = Params(("turns", 0)) },
                new Effect { Op = "terrainDurationExtend", Params = Params(("turns", 2), ("extra", 1)) },
                new Effect { Op = "sideConditionDurationExtend" },
                new Effect { Op = "sideConditionDurationExtend", Chance = 100,
                    Params = Params(("tag", "other"), ("turns", 0), ("extra", 1)) },
                new Effect { Op = "groundedModify", Params = Params(("state", "floating")) },
                new Effect { Op = "terrainSeed", Chance = 100, Params = Params(("terrain", "none"), ("stat", "atk"), ("extra", 1)) },
                new Effect { Op = "terrainSeed", Params = Params(("terrain", 1), ("stat", 1)) },
                new Effect { Op = "terrainSeed", Params = Params(("terrain", "1"), ("stat", "2")) },
            ],
        };

        var issues = Run(new HeldItemBattleEffectRule(), Project(item));

        Assert.Contains(issues, i => i.Message.Contains("holdable is false"));
        Assert.Contains(issues, i => i.Message.Contains("damage"));
        Assert.Contains(issues, i => i.Message.Contains("thresholdPercent"));
        Assert.Contains(issues, i => i.Message.Contains("healAmount"));
        Assert.Contains(issues, i => i.Message.Contains("only one"));
        Assert.Contains(issues, i => i.Message.Contains("status"));
        Assert.Contains(issues, i => i.Message.Contains("multiplierPercent"));
        Assert.Contains(issues, i => i.Message.Contains("choiceLock") && i.Message.Contains("damageClass"));
        Assert.Contains(issues, i => i.Message.Contains("residualHeal") && i.Message.Contains("num"));
        Assert.Contains(issues, i => i.Message.Contains("residualHeal") && i.Message.Contains("den"));
        Assert.Contains(issues, i => i.Message.Contains("does not take params"));
        Assert.Contains(issues, i => i.Message.Contains("weatherDurationExtend") && i.Message.Contains("turns"));
        Assert.Contains(issues, i => i.Message.Contains("terrainDurationExtend") && i.Message.Contains("turns"));
        Assert.Contains(issues, i => i.Message.Contains("terrainDurationExtend") && i.Message.Contains("unknown param"));
        Assert.Contains(issues, i => i.Message.Contains("sideConditionDurationExtend") && i.Message.Contains("screen"));
        Assert.Contains(issues, i => i.Message.Contains("sideConditionDurationExtend") && i.Message.Contains("turns"));
        Assert.Contains(issues, i => i.Message.Contains("sideConditionDurationExtend") && i.Message.Contains("unknown param"));
        Assert.Contains(issues, i => i.Message.Contains("sideConditionDurationExtend") && i.Message.Contains("does not support chance"));
        Assert.Contains(issues, i => i.Message.Contains("grounded") && i.Message.Contains("airborne"));
        Assert.Contains(issues, i => i.Message.Contains("terrainSeed") && i.Message.Contains("unknown terrain"));
        Assert.Contains(issues, i => i.Message.Contains("terrainSeed") && i.Message.Contains("unknown stat"));
        Assert.Contains(issues, i => i.Message.Contains("terrainSeed") && i.Message.Contains("unknown param"));
        Assert.Contains(issues, i => i.Message.Contains("terrainSeed") && i.Message.Contains("does not support chance"));
        Assert.Contains(issues, i => i.Message.Contains("terrainSeed") && i.Message.Contains("requires string param 'terrain'"));
        Assert.Contains(issues, i => i.Message.Contains("terrainSeed") && i.Message.Contains("requires string param 'stat'"));
        Assert.Contains(issues, i => i.Message.Contains("only one terrainSeed"));
        Assert.Empty(Run(new HeldItemBattleEffectRule(), Project(item with
        {
            Holdable = true,
            BattleEffects =
            [
                new Effect { Op = "thresholdHeal", Params = Params(("thresholdPercent", 50), ("healAmount", 20)) },
                new Effect { Op = "statusCure", Params = Params(("status", "poison")) },
                new Effect { Op = "typeDamageBoost", Params = Params(("type", "fire"), ("multiplierPercent", 120)) },
                new Effect { Op = "choiceLock", Params = Params(("damageClass", "physical"), ("multiplierPercent", 150)) },
                new Effect { Op = "residualHeal", Params = Params(("num", 1), ("den", 16)) },
                new Effect { Op = "surviveFromFull" },
                new Effect { Op = "weatherDurationExtend", Params = Params(("turns", 2)) },
                new Effect { Op = "terrainDurationExtend", Params = Params(("turns", 2)) },
                new Effect { Op = "sideConditionDurationExtend", Params = Params(("tag", "screen"), ("turns", 3)) },
                new Effect { Op = "groundedModify", Params = Params(("state", "grounded")) },
                new Effect { Op = "terrainSeed", Params = Params(("terrain", "grassy"), ("stat", "def")) },
            ],
        })));
    }

    [Fact]
    public void AbilityMutationGuardRequiresUniqueKnownOperations()
    {
        var invalid = new Ability
        {
            Id = EntityId.Parse("ability:guarded"),
            Name = "Guarded",
            Hooks = [new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyOutgoingDamage,
                Effects = [new Effect
                {
                    Op = "abilityMutationGuard",
                    Chance = 50,
                    Params = Params(("operations", "copy,copy,unknown"), ("extra", true)),
                }],
            }],
        };

        ValidationIssue[] issues = Run(new AbilityHookRule(), Project(invalid)).ToArray();
        Assert.Contains(issues, issue => issue.Message.Contains("unique ability mutation operations"));
        Assert.Contains(issues, issue => issue.Message.Contains("unknown param 'extra'"));
        Assert.Contains(issues, issue => issue.Message.Contains("does not support chance"));

        Ability valid = invalid with
        {
            Hooks = [new AbilityHook
            {
                Hook = AbilityHookPoint.OnModifyOutgoingDamage,
                Effects = [new Effect
                {
                    Op = "abilityMutationGuard",
                    Params = Params(("operations", "copy,swap,replace,suppress")),
                }],
            }],
        };
        Assert.Empty(Run(new AbilityHookRule(), Project(valid)));
    }

    [Fact]
    public void AbilityMutationReplacementMustReferenceExistingAbility()
    {
        EntityId replacement = EntityId.Parse("ability:replacement");
        var move = new Move
        {
            Id = EntityId.Parse("move:ability_replace"),
            Name = "Ability replace",
            Type = EntityId.Parse("type:normal"),
            DamageClass = DamageClass.Status,
            Pp = 10,
            Target = MoveTarget.Selected,
            Effects = [new Effect
            {
                Op = "abilityMutation",
                Params = Params(("operation", "replace"), ("ability", replacement.ToString())),
            }],
        };

        Assert.Contains(Run(new MoveRule(), Project(move)), issue => issue.Message.Contains(replacement.ToString()));
        Assert.Empty(Run(new MoveRule(), Project(move,
            new Ability { Id = replacement, Name = "Replacement" })));
    }

    [Fact]
    public void Forms_FlagShapeAndActivationInvariants()
    {
        Form badTimed = ValidForm("timed") with
        {
            Activation = FormActivation.BattleTimed,
            Turns = 0,
            Sprites = new SpeciesSprites(),
            TypeOverrides = [EntityId.Parse("type:fire"), EntityId.Parse("type:fire")],
            StatOverrides = new Stats(0, 45, 45, 45, 45, 45),
        };
        Form badCondition = ValidForm("condition") with
        {
            Activation = FormActivation.Condition,
            Condition = new FormCondition(),
            RequiredTrainerItem = EntityId.Parse("item:key"),
        };
        Form badTemporaryItems = ValidForm("temporary_items") with
        {
            Activation = FormActivation.BattleTemporary,
            RequiredHeldItem = EntityId.Parse("item:not_holdable"),
            RequiredTrainerItem = EntityId.Parse("item:not_key"),
        };
        Form badConditionItem = ValidForm("condition_item") with
        {
            Activation = FormActivation.Condition,
            Condition = new FormCondition { HeldItem = EntityId.Parse("item:not_holdable") },
        };
        Species bad = Species() with { Forms = [badTimed, badCondition, badTemporaryItems, badConditionItem, ValidForm("dup"), ValidForm("dup")] };

        var issues = Run(new FormRule(), Project(bad,
            new Item { Id = EntityId.Parse("item:not_holdable"), Name = "Not Holdable" },
            new Item { Id = EntityId.Parse("item:not_key"), Name = "Not Key", Holdable = true }));

        Assert.Contains(issues, i => i.Message.Contains("turns > 0"));
        Assert.Contains(issues, i => i.Message.Contains("front, back, and icon"));
        Assert.Contains(issues, i => i.Message.Contains("duplicate type"));
        Assert.Contains(issues, i => i.Message.Contains("base HP"));
        Assert.Contains(issues, i => i.Message.Contains("requires weather or heldItem"));
        Assert.Contains(issues, i => i.Message.Contains("requiredTrainerItem"));
        Assert.Contains(issues, i => i.Message.Contains("requiredHeldItem") && i.Message.Contains("holdable"));
        Assert.Contains(issues, i => i.Message.Contains("requiredTrainerItem") && i.Message.Contains("key item"));
        Assert.Contains(issues, i => i.Message.Contains("heldItem condition") && i.Message.Contains("holdable"));
        Assert.Contains(issues, i => i.Message.Contains("empty or duplicated"));
        Assert.Empty(Run(new FormRule(), Project(Species() with
        {
            Forms =
            [
                ValidForm("mega") with
                {
                    Activation = FormActivation.BattleTemporary,
                    RequiredHeldItem = EntityId.Parse("item:stone"),
                    RequiredTrainerItem = EntityId.Parse("item:key"),
                },
                ValidForm("timed") with { Activation = FormActivation.BattleTimed, Turns = 3 },
                ValidForm("rain") with
                {
                    Activation = FormActivation.Condition,
                    Condition = new FormCondition { Weather = "rain" },
                },
            ],
        },
            new Item { Id = EntityId.Parse("item:stone"), Name = "Stone", Holdable = true },
            new Item { Id = EntityId.Parse("item:key"), Name = "Key", KeyItem = true })));
    }

    // --- Move rule ---------------------------------------------------------------

    [Fact]
    public void Move_FlagsPowerClassMismatchAndRanges()
    {
        Move statusWithPower = Move("a") with { DamageClass = DamageClass.Status, Power = 40 };
        Move damagingNoPower = Move("b") with { DamageClass = DamageClass.Physical, Power = null };
        Move badAcc = Move("c") with { Accuracy = 200 };
        Move noPp = Move("d") with { Pp = 0 };
        Move badTarget = Move("e") with { Target = (MoveTarget)999 };
        Assert.NotEmpty(Run(new MoveRule(), Project(statusWithPower)));
        Assert.NotEmpty(Run(new MoveRule(), Project(damagingNoPower)));
        Assert.NotEmpty(Run(new MoveRule(), Project(badAcc)));
        Assert.NotEmpty(Run(new MoveRule(), Project(noPp)));
        Assert.Contains(Run(new MoveRule(), Project(badTarget)), i => i.Message.Contains("move target"));
        Assert.Empty(Run(new MoveRule(), Project(Move())));
    }

    [Fact]
    public void Move_FlagsUnknownEffectOpsAndBadParams()
    {
        Move unknown = Move("unknown") with { Effects = [new Effect { Op = "bespokeMoveCode" }] };
        Move badParam = Move("bad") with
        {
            Effects = [new Effect { Op = "statStage", Params = Params(("stat", "hp"), ("delta", 1)) }],
        };
        Move badAll = Move("badall") with
        {
            Effects = [new Effect { Op = "statStageAll", Params = Params(("delta", 0)) }],
        };
        Move badHelper = Move("badhelper") with
        {
            Effects = [new Effect { Op = "statStageCopy", Params = Params(("from", "both"), ("to", "self")) }],
        };
        Move badDamageStat = Move("badstat") with
        {
            Effects = [new Effect { Op = "damageStatOverride", Params = Params(("offensiveStat", "spe")) }],
        };
        Move badWeather = Move("badweather") with
        {
            Effects = [new Effect { Op = "weather", Params = Params(("weather", "fog")) }],
        };
        Move badTargetHpPower = Move("badhppower") with
        {
            Effects =
            [
                new Effect
                {
                    Op = "targetHpThresholdPower",
                    Params = Params(("thresholdNum", 1), ("thresholdDen", 0), ("multiplierNum", 2), ("multiplierDen", 1)),
                },
            ],
        };
        Move badHpRatioPower = Move("badratiopower") with
        {
            Effects = [new Effect { Op = "hpRatioPower", Params = Params(("source", "bench")) }],
        };
        Move multiStage = Move("multi") with
        {
            Effects =
            [
                new Effect { Op = "statStage", Params = Params(("stat", "atk"), ("delta", 1), ("onSelf", true)) },
                new Effect { Op = "statStage", Params = Params(("stat", "spa"), ("delta", 1), ("onSelf", true)) },
            ],
        };
        Move allStage = Move("allstage") with
        {
            Effects = [new Effect { Op = "statStageAll", Chance = 10, Params = Params(("delta", 1), ("onSelf", true)) }],
        };
        Move stageHelper = Move("stagehelper") with
        {
            Effects =
            [
                new Effect { Op = "hpCost", Params = Params(("num", 1), ("den", 2)) },
                new Effect { Op = "statStageSwap", Params = Params(("group", "offense")) },
            ],
        };
        Move damageStat = Move("damagestat") with
        {
            Effects = [new Effect { Op = "damageStatOverride", Params = Params(("offensiveStat", "def")) }],
        };
        Move weather = Move("weather") with
        {
            DamageClass = DamageClass.Status,
            Power = null,
            Target = MoveTarget.EntireField,
            Effects = [new Effect { Op = "weather", Params = Params(("weather", "rain")) }],
        };
        Move targetHpPower = Move("hppower") with
        {
            Effects =
            [
                new Effect
                {
                    Op = "targetHpThresholdPower",
                    Params = Params(("thresholdNum", 1), ("thresholdDen", 2), ("multiplierNum", 2), ("multiplierDen", 1)),
                },
            ],
        };
        Move hpRatioPower = Move("ratiopower") with
        {
            Effects = [new Effect { Op = "hpRatioPower", Params = Params(("source", "user")) }],
        };
        Move noBattle = Move("nobattle") with { DamageClass = DamageClass.Status, Power = null, Effects = [new Effect { Op = "noBattleEffect" }] };

        Assert.Contains(Run(new MoveRule(), Project(unknown)), i => i.Message.Contains("bespokeMoveCode"));
        Assert.Contains(Run(new MoveRule(), Project(badParam)), i => i.Message.Contains("HP"));
        Assert.Contains(Run(new MoveRule(), Project(badAll)), i => i.Message.Contains("statStageAll"));
        Assert.Contains(Run(new MoveRule(), Project(badHelper)), i => i.Message.Contains("statStageCopy"));
        Assert.Contains(Run(new MoveRule(), Project(badDamageStat)), i => i.Message.Contains("damageStatOverride"));
        Assert.Contains(Run(new MoveRule(), Project(badWeather)), i => i.Message.Contains("weather"));
        Assert.Contains(Run(new MoveRule(), Project(badTargetHpPower)), i => i.Message.Contains("targetHpThresholdPower"));
        Assert.Contains(Run(new MoveRule(), Project(badHpRatioPower)), i => i.Message.Contains("Unknown source"));
        Assert.Empty(Run(new MoveRule(), Project(multiStage)));
        Assert.Empty(Run(new MoveRule(), Project(allStage)));
        Assert.Empty(Run(new MoveRule(), Project(stageHelper)));
        Assert.Empty(Run(new MoveRule(), Project(damageStat)));
        Assert.Empty(Run(new MoveRule(), Project(weather)));
        Assert.Empty(Run(new MoveRule(), Project(targetHpPower)));
        Assert.Empty(Run(new MoveRule(), Project(hpRatioPower)));
        Assert.Empty(Run(new MoveRule(), Project(noBattle)));
    }

    [Fact]
    public void Move_FieldSensitiveTypeRowsRequireExistingTypes()
    {
        Move move = Move("weather_move") with
        {
            Effects = [new Effect { Op = "weatherMove", Params = Params(("types", "rain:water")) }],
        };
        var water = new TypeDef { Id = EntityId.Parse("type:water") };
        Move terrainMove = Move("terrain_move") with
        {
            Effects = [new Effect { Op = "terrainMove", Params = Params(
                ("subject", "user"), ("types", "electric:electric")) }],
        };
        var electric = new TypeDef { Id = EntityId.Parse("type:electric") };

        Assert.Contains(Run(new MoveRule(), Project(move)), issue => issue.Message.Contains("type:water"));
        Assert.Empty(Run(new MoveRule(), Project(move, water)));
        Assert.Contains(Run(new MoveRule(), Project(terrainMove)), issue => issue.Message.Contains("type:electric"));
        Assert.Empty(Run(new MoveRule(), Project(terrainMove, electric)));
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

    private static Form ValidForm(string id = "mega") => new()
    {
        FormId = id,
        Sprites = new SpeciesSprites
        {
            Front = EntityId.Parse($"sprite:{id}_front"),
            Back = EntityId.Parse($"sprite:{id}_back"),
            Icon = EntityId.Parse($"sprite:{id}_icon"),
        },
    };

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, int Value)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonDocument.Parse(v.Value.ToString()).RootElement.Clone());

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonSerializer.SerializeToElement(v.Value));

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
