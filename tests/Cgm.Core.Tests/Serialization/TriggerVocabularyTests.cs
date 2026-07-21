using System.Text.Json.Nodes;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;
using Cgm.Core.Validation.Rules;

namespace Cgm.Core.Tests.Serialization;

/// <summary>Schema v9 closed world-action vocabulary (DATA_SCHEMA §4.11b): migration off free
/// strings, validation of every op, and the round trip.</summary>
public sealed class TriggerVocabularyTests
{
    private static readonly EntityId ItemId = EntityId.Parse("item:potion");
    private static readonly EntityId TrainerId = EntityId.Parse("trainer:rival");

    private static Map MigrateMap(string entities, int version = 8) =>
        CgmJson.DeserializeVersioned<Map>($$"""
        {
          "schemaVersion": {{version}},
          "id": "map:test", "name": "Test", "width": 4, "height": 4,
          "entities": [{{entities}}]
        }
        """);

    // --- Migration ----------------------------------------------------------------

    /// <summary>Typed actions arrived in v9; asserted through behaviour, not a version literal.</summary>
    [Fact]
    public void PreV9Documents_GainTypedActions()
    {
        Map map = MigrateMap("""
        { "kind": "trigger", "key": "t", "pos": { "x": 0, "y": 0 }, "actions": ["hi"] }
        """);
        Assert.Equal(TriggerOp.Dialogue, Assert.Single(Assert.IsType<TriggerEntity>(map.Entities[0]).Actions).Op);
        Assert.True(SchemaVersions.Current >= 9);
    }

    [Fact]
    public void LegacyTriggerStrings_BecomeDialogueActionsPreservingText()
    {
        Map map = MigrateMap("""
        { "kind": "trigger", "key": "t", "pos": { "x": 0, "y": 0 },
          "actions": ["Welcome to town.", "Mind the ledge."] }
        """);

        TriggerEntity trigger = Assert.IsType<TriggerEntity>(map.Entities[0]);
        Assert.Equal(2, trigger.Actions.Count);
        Assert.All(trigger.Actions, a => Assert.Equal(TriggerOp.Dialogue, a.Op));
        Assert.Equal("Welcome to town.", trigger.Actions[0].Text);
        Assert.Equal("Mind the ledge.", trigger.Actions[1].Text);
    }

    [Fact]
    public void LegacyObjectInteractionString_BecomesADialogueList()
    {
        MapObject obj = CgmJson.DeserializeVersioned<MapObject>("""
        { "schemaVersion": 8, "id": "object:sign", "name": "Sign", "interaction": "It reads: keep out." }
        """);

        TriggerAction action = Assert.Single(obj.Interaction);
        Assert.Equal(TriggerOp.Dialogue, action.Op);
        Assert.Equal("It reads: keep out.", action.Text);
    }

    [Fact]
    public void NullOrEmptyLegacyValues_BecomeEmptyLists()
    {
        Assert.Empty(CgmJson.DeserializeVersioned<MapObject>("""
        { "schemaVersion": 8, "id": "object:rock", "name": "Rock" }
        """).Interaction);

        Assert.Empty(Assert.IsType<TriggerEntity>(MigrateMap("""
        { "kind": "trigger", "key": "t", "pos": { "x": 0, "y": 0 }, "actions": [] }
        """).Entities[0]).Actions);
    }

    [Fact]
    public void BlankLegacyStrings_AreDropped() =>
        Assert.Empty(Assert.IsType<TriggerEntity>(MigrateMap("""
        { "kind": "trigger", "key": "t", "pos": { "x": 0, "y": 0 }, "actions": ["", "   "] }
        """).Entities[0]).Actions);

    /// <summary>Only triggers carry actions; other entity kinds must pass through untouched.</summary>
    [Fact]
    public void NonTriggerEntities_AreUnaffected()
    {
        Map map = MigrateMap("""{ "kind": "sign", "key": "s", "pos": { "x": 0, "y": 0 }, "text": "Hi" }""");
        Assert.Equal("Hi", Assert.IsType<SignEntity>(map.Entities[0]).Text);
    }

    [Fact]
    public void MigrationIsIdempotentAgainstAlreadyTypedActions()
    {
        Map map = MigrateMap("""
        { "kind": "trigger", "key": "t", "pos": { "x": 0, "y": 0 },
          "actions": [{ "op": "heal" }] }
        """, version: 9);

        Assert.Equal(TriggerOp.Heal, Assert.Single(Assert.IsType<TriggerEntity>(map.Entities[0]).Actions).Op);
    }

    [Fact]
    public void EveryOpDeserializesFromItsCamelCaseName()
    {
        Map map = MigrateMap("""
        { "kind": "trigger", "key": "t", "pos": { "x": 0, "y": 0 }, "actions": [
            { "op": "dialogue", "text": "hello" },
            { "op": "setFlag", "flag": "met_rival", "value": 2 },
            { "op": "clearFlag", "flag": "met_rival" },
            { "op": "giveItem", "entity": "item:potion", "value": 3 },
            { "op": "heal" },
            { "op": "startBattle", "entity": "trainer:rival" }
        ] }
        """, version: 9);

        IReadOnlyList<TriggerAction> actions = Assert.IsType<TriggerEntity>(map.Entities[0]).Actions;
        Assert.Equal(
            [TriggerOp.Dialogue, TriggerOp.SetFlag, TriggerOp.ClearFlag,
             TriggerOp.GiveItem, TriggerOp.Heal, TriggerOp.StartBattle],
            actions.Select(a => a.Op).ToArray());
        Assert.Equal(2, actions[1].Value);
        Assert.Equal(ItemId, actions[3].Entity);
        Assert.Equal(TrainerId, actions[5].Entity);
    }

    [Fact]
    public void ValueDefaultsToOne() =>
        Assert.Equal(1, new TriggerAction { Op = TriggerOp.GiveItem, Entity = ItemId }.Value);

    // --- Validation ---------------------------------------------------------------

    private static Project WithTrigger(params TriggerAction[] actions) => Build(new Map
    {
        Id = EntityId.Parse("map:test"), Name = "Test", Width = 4, Height = 4,
        Entities = [new TriggerEntity { Key = "t", Pos = new GridPos(0, 0), Actions = actions }],
    });

    private static Project Build(Map map) => new(new ProjectSettings { Name = "T" },
        new Dictionary<EntityId, IEntity>
        {
            [map.Id] = map,
            [ItemId] = new Item { Id = ItemId, Name = "Potion" },
            [TrainerId] = new Trainer { Id = TrainerId, Name = "Rival" },
        });

    private static IReadOnlyList<ValidationIssue> Check(Project project) =>
        new TriggerActionRule().Check(project).ToList();

    [Fact]
    public void Validation_AcceptsEveryWellFormedOp() =>
        Assert.Empty(Check(WithTrigger(
            new TriggerAction { Op = TriggerOp.Dialogue, Text = "hi" },
            new TriggerAction { Op = TriggerOp.SetFlag, Flag = "seen" },
            new TriggerAction { Op = TriggerOp.ClearFlag, Flag = "seen" },
            new TriggerAction { Op = TriggerOp.GiveItem, Entity = ItemId, Value = 2 },
            new TriggerAction { Op = TriggerOp.Heal },
            new TriggerAction { Op = TriggerOp.StartBattle, Entity = TrainerId })));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validation_RejectsDialogueWithoutText(string? text) =>
        Assert.Contains("no text",
            Assert.Single(Check(WithTrigger(new TriggerAction { Op = TriggerOp.Dialogue, Text = text }))).Message);

    [Fact]
    public void Validation_RejectsFlagOpsWithoutAFlagName()
    {
        Assert.Contains("no flag name",
            Assert.Single(Check(WithTrigger(new TriggerAction { Op = TriggerOp.SetFlag }))).Message);
        Assert.Contains("no flag name",
            Assert.Single(Check(WithTrigger(new TriggerAction { Op = TriggerOp.ClearFlag, Flag = " " }))).Message);
    }

    [Fact]
    public void Validation_RejectsGiveItemWithTheWrongCategory() =>
        Assert.Contains("must reference an item",
            Assert.Single(Check(WithTrigger(
                new TriggerAction { Op = TriggerOp.GiveItem, Entity = TrainerId }))).Message);

    [Fact]
    public void Validation_RejectsGiveItemWithNoEntity() =>
        Assert.Contains("must reference an item",
            Assert.Single(Check(WithTrigger(new TriggerAction { Op = TriggerOp.GiveItem }))).Message);

    [Fact]
    public void Validation_RejectsAMissingItemReference() =>
        Assert.Contains("missing item",
            Assert.Single(Check(WithTrigger(new TriggerAction
            {
                Op = TriggerOp.GiveItem,
                Entity = EntityId.Parse("item:absent"),
            }))).Message);

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validation_RejectsNonPositiveQuantities(int value) =>
        Assert.Contains("positive quantity",
            Check(WithTrigger(new TriggerAction { Op = TriggerOp.GiveItem, Entity = ItemId, Value = value }))
                .Select(i => i.Message).Single(m => m.Contains("positive")));

    [Fact]
    public void Validation_RejectsStartBattleWithTheWrongCategory() =>
        Assert.Contains("must reference a trainer",
            Assert.Single(Check(WithTrigger(
                new TriggerAction { Op = TriggerOp.StartBattle, Entity = ItemId }))).Message);

    [Fact]
    public void Validation_RejectsAMissingTrainerReference() =>
        Assert.Contains("missing trainer",
            Assert.Single(Check(WithTrigger(new TriggerAction
            {
                Op = TriggerOp.StartBattle,
                Entity = EntityId.Parse("trainer:absent"),
            }))).Message);

    [Fact]
    public void Validation_RejectsAnUndefinedOp() =>
        Assert.Contains("unknown op",
            Assert.Single(Check(WithTrigger(new TriggerAction { Op = (TriggerOp)99 }))).Message);

    /// <summary>Heal needs nothing, so it must never be flagged for missing fields.</summary>
    [Fact]
    public void Validation_AcceptsBareHeal() =>
        Assert.Empty(Check(WithTrigger(new TriggerAction { Op = TriggerOp.Heal })));

    [Fact]
    public void Validation_ReportsEveryOffendingAction() =>
        Assert.Equal(2, Check(WithTrigger(
            new TriggerAction { Op = TriggerOp.Dialogue },
            new TriggerAction { Op = TriggerOp.Heal },
            new TriggerAction { Op = TriggerOp.SetFlag })).Count);

    /// <summary>Object interactions use the same rule, so both call sites stay consistent.</summary>
    [Fact]
    public void Validation_CoversObjectInteractionsToo()
    {
        var project = new Project(new ProjectSettings { Name = "T" }, new Dictionary<EntityId, IEntity>
        {
            [EntityId.Parse("object:sign")] = new MapObject
            {
                Id = EntityId.Parse("object:sign"),
                Name = "Sign",
                Interaction = [new TriggerAction { Op = TriggerOp.Dialogue }],
            },
        });

        Assert.Contains("no text", Assert.Single(Check(project)).Message);
    }

    [Fact]
    public void Validation_AcceptsEmptyActionLists() => Assert.Empty(Check(WithTrigger()));

    // --- Round trip ---------------------------------------------------------------

    [Fact]
    public void ActionsSurviveASerializationRoundTrip()
    {
        var map = new Map
        {
            Id = EntityId.Parse("map:test"), Name = "Test", Width = 2, Height = 2,
            Entities =
            [
                new TriggerEntity
                {
                    Key = "gate", Pos = new GridPos(0, 0),
                    Actions =
                    [
                        new TriggerAction { Op = TriggerOp.SetFlag, Flag = "opened", Value = 1 },
                        new TriggerAction { Op = TriggerOp.StartBattle, Entity = TrainerId },
                    ],
                },
            ],
        };

        Map restored = CgmJson.DeserializeVersioned<Map>(CgmJson.SerializeEntity(map));
        IReadOnlyList<TriggerAction> actions = Assert.IsType<TriggerEntity>(restored.Entities[0]).Actions;
        Assert.Equal(TriggerOp.SetFlag, actions[0].Op);
        Assert.Equal("opened", actions[0].Flag);
        Assert.Equal(TrainerId, actions[1].Entity);
    }
}
