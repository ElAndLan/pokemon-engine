using System.Text.Json;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

public sealed class SchemaV2SerializationTests
{
    [Fact]
    public void Move_RoundTripsContactFlag()
    {
        var move = new Move
        {
            Id = EntityId.Parse("move:root_claw"),
            Name = "Root Claw",
            Type = EntityId.Parse("type:normal"),
            DamageClass = DamageClass.Physical,
            Power = 40,
            Accuracy = 100,
            Pp = 35,
            MakesContact = true,
        };

        Move back = CgmJson.Deserialize<Move>(CgmJson.Serialize(move));
        Assert.Equal(SchemaVersions.Current, back.SchemaVersion);
        Assert.True(back.MakesContact);
    }

    [Fact]
    public void Move_RoundTripsExpandedTargetVocabulary()
    {
        var move = new Move
        {
            Id = EntityId.Parse("move:ally_target"),
            Name = "Ally Target",
            Type = EntityId.Parse("type:normal"),
            DamageClass = DamageClass.Status,
            Pp = 10,
            Target = MoveTarget.UserOrAlly,
        };

        Move back = CgmJson.Deserialize<Move>(CgmJson.Serialize(move));

        Assert.Equal(MoveTarget.UserOrAlly, back.Target);
        Assert.Equal(SchemaVersions.Current, back.SchemaVersion);
    }

    [Fact]
    public void Ability_RoundTripsHooks()
    {
        var ability = new Ability
        {
            Id = EntityId.Parse("ability:sturdy_root"),
            Name = "Sturdy Root",
            Hooks =
            [
                new AbilityHook
                {
                    Hook = AbilityHookPoint.OnTerrainChange,
                    Effects = [new Effect { Op = "terrainSummon" }],
                },
            ],
        };

        Ability back = CgmJson.Deserialize<Ability>(CgmJson.Serialize(ability));
        Assert.Equal(SchemaVersions.Current, back.SchemaVersion);
        Assert.Equal(AbilityHookPoint.OnTerrainChange, back.Hooks.Single().Hook);
        Assert.Equal("terrainSummon", back.Hooks.Single().Effects.Single().Op);
    }

    [Fact]
    public void Species_RoundTripsAbilitiesAndForm()
    {
        var species = new Species
        {
            Id = EntityId.Parse("species:leafcub"),
            Name = "Leafcub",
            Types = [EntityId.Parse("type:grass")],
            WeightHectograms = 69,
            HeightDecimeters = 7,
            Abilities = [EntityId.Parse("ability:sturdy_root")],
            HiddenAbility = EntityId.Parse("ability:sap_veil"),
            Forms =
            [
                new Form
                {
                    FormId = "mega",
                    Activation = FormActivation.BattleTemporary,
                    AbilityOverride = EntityId.Parse("ability:sap_veil"),
                    RequiredHeldItem = EntityId.Parse("item:root_stone"),
                    RequiredTrainerItem = EntityId.Parse("item:focus_band"),
                    MoveRemap = new Dictionary<EntityId, EntityId>
                    {
                        [EntityId.Parse("move:tackle")] = EntityId.Parse("move:root_crash"),
                    },
                },
            ],
        };

        Species back = CgmJson.Deserialize<Species>(CgmJson.Serialize(species));
        Assert.Equal(SchemaVersions.Current, back.SchemaVersion);
        Assert.Equal([EntityId.Parse("ability:sturdy_root")], back.Abilities);
        Assert.Equal(FormActivation.BattleTemporary, back.Forms.Single().Activation);
        Assert.Equal(69, back.WeightHectograms);
        Assert.Equal(7, back.HeightDecimeters);
        Assert.Equal(EntityId.Parse("move:root_crash"),
            back.Forms.Single().MoveRemap![EntityId.Parse("move:tackle")]);
    }

    [Fact]
    public void Item_RoundTripsHeldBattleEffects()
    {
        var item = new Item
        {
            Id = EntityId.Parse("item:root_berry"),
            Name = "Root Berry",
            Holdable = true,
            BattleEffects =
            [
                new Effect
                {
                    Op = "thresholdHeal",
                    Params = new Dictionary<string, JsonElement>
                    {
                        ["thresholdPercent"] = JsonDocument.Parse("25").RootElement.Clone(),
                    },
                },
            ],
        };

        Item back = CgmJson.Deserialize<Item>(CgmJson.Serialize(item));
        Assert.Equal(SchemaVersions.Current, back.SchemaVersion);
        Assert.Equal("thresholdHeal", back.BattleEffects.Single().Op);
        Assert.Equal(25, back.BattleEffects.Single().Params!["thresholdPercent"].GetInt32());
    }
}
