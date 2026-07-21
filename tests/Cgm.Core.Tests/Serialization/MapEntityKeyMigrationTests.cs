using System.Text.Json.Nodes;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;
using Cgm.Core.Validation.Rules;

namespace Cgm.Core.Tests.Serialization;

/// <summary>Schema v8 map-entity stable keys (DATA_SCHEMA §4.11a): migration, validation, and the
/// invariant that a key survives reordering.</summary>
public sealed class MapEntityKeyMigrationTests
{
    private static JsonObject MapJson(int schemaVersion, params string[] entityBodies) =>
        (JsonObject)JsonNode.Parse($$"""
        {
          "schemaVersion": {{schemaVersion}},
          "id": "map:test",
          "name": "Test",
          "width": 4,
          "height": 4,
          "entities": [{{string.Join(",", entityBodies)}}]
        }
        """)!;

    private static Map Migrate(JsonObject json) =>
        CgmJson.DeserializeVersioned<Map>(json.ToJsonString());

    private static string[] Keys(Map map) => map.Entities.Select(e => e.Key).ToArray();

    /// <summary>Keys arrived in v8, so any document at or before v7 must gain them on load. Asserted
    /// against the migrated result rather than a version literal, so a later bump does not churn.</summary>
    [Fact]
    public void PreV8Documents_GainKeysAtTheCurrentVersion()
    {
        Map map = Migrate(MapJson(7, """{ "kind": "npc", "pos": { "x": 0, "y": 0 } }"""));
        Assert.NotEmpty(map.Entities[0].Key);
        Assert.True(SchemaVersions.Current >= 8);
    }

    [Fact]
    public void V7Entities_ReceiveKindAndIndexKeys()
    {
        Map map = Migrate(MapJson(7,
            """{ "kind": "player-start", "pos": { "x": 0, "y": 0 } }""",
            """{ "kind": "npc", "pos": { "x": 1, "y": 0 } }""",
            """{ "kind": "sign", "pos": { "x": 2, "y": 0 } }"""));

        Assert.Equal(["player_start_0", "npc_1", "sign_2"], Keys(map));
    }

    /// <summary>The hyphen in a kind becomes an underscore, so keys stay a single token.</summary>
    [Fact]
    public void HyphenatedKinds_BecomeUnderscoredKeys() =>
        Assert.Equal(["player_start_0"], Keys(Migrate(MapJson(7,
            """{ "kind": "player-start", "pos": { "x": 0, "y": 0 } }"""))));

    [Fact]
    public void AuthoredKeys_ArePreserved()
    {
        Map map = Migrate(MapJson(7,
            """{ "kind": "npc", "key": "rival", "pos": { "x": 0, "y": 0 } }""",
            """{ "kind": "npc", "pos": { "x": 1, "y": 0 } }"""));

        Assert.Equal(["rival", "npc_1"], Keys(map));
    }

    /// <summary>A derived key must never steal an authored one; it suffixes instead.</summary>
    [Fact]
    public void DerivedKeys_AvoidCollidingWithAnAuthoredKey()
    {
        Map map = Migrate(MapJson(7,
            """{ "kind": "npc", "key": "npc_1", "pos": { "x": 0, "y": 0 } }""",
            """{ "kind": "npc", "pos": { "x": 1, "y": 0 } }"""));

        Assert.Equal("npc_1", map.Entities[0].Key);
        Assert.Equal("npc_1_2", map.Entities[1].Key);
        Assert.Equal(2, Keys(map).Distinct().Count());
    }

    [Fact]
    public void BlankKeys_AreTreatedAsMissing() =>
        Assert.Equal(["npc_0"], Keys(Migrate(MapJson(7,
            """{ "kind": "npc", "key": "   ", "pos": { "x": 0, "y": 0 } }"""))));

    [Fact]
    public void MigrationIsDeterministicAcrossRuns()
    {
        JsonObject json = MapJson(7,
            """{ "kind": "npc", "pos": { "x": 0, "y": 0 } }""",
            """{ "kind": "warp", "pos": { "x": 1, "y": 0 }, "target": "map:other", "targetPos": { "x": 0, "y": 0 } }""");

        Assert.Equal(Keys(Migrate(json)), Keys(Migrate(json)));
    }

    [Fact]
    public void MigrationIsIdempotent()
    {
        Map once = Migrate(MapJson(7, """{ "kind": "npc", "pos": { "x": 0, "y": 0 } }"""));
        Map twice = Migrate(MapJson(8, $$"""{ "kind": "npc", "key": "{{once.Entities[0].Key}}", "pos": { "x": 0, "y": 0 } }"""));
        Assert.Equal(Keys(once), Keys(twice));
    }

    [Fact]
    public void MapsWithoutEntities_MigrateCleanly() =>
        Assert.Empty(Migrate(MapJson(7)).Entities);

    /// <summary>Only map documents carry entities; other categories must pass through untouched.</summary>
    [Fact]
    public void NonMapDocuments_AreUnaffected()
    {
        var species = (JsonObject)JsonNode.Parse("""
        { "schemaVersion": 7, "id": "species:test", "name": "Test", "types": ["type:normal"] }
        """)!;
        Species migrated = CgmJson.DeserializeVersioned<Species>(species.ToJsonString());
        Assert.Equal(EntityId.Parse("species:test"), migrated.Id);
    }

    /// <summary>The point of the key: reordering the file must not move a save's flags.</summary>
    [Fact]
    public void ReorderingEntities_DoesNotChangeAnAuthoredKey()
    {
        string npc = """{ "kind": "npc", "key": "rival", "pos": { "x": 0, "y": 0 } }""";
        string sign = """{ "kind": "sign", "key": "notice", "pos": { "x": 1, "y": 0 } }""";

        Map before = Migrate(MapJson(8, npc, sign));
        Map after = Migrate(MapJson(8, sign, npc));

        Assert.Equal("rival", before.Entities[0].Key);
        Assert.Equal("rival", after.Entities[1].Key);
    }

    // --- Validation ---------------------------------------------------------------

    private static Project WithMap(params MapEntity[] entities) =>
        new(new ProjectSettings { Name = "T" }, new Dictionary<EntityId, IEntity>
        {
            [EntityId.Parse("map:test")] = new Map
            {
                Id = EntityId.Parse("map:test"),
                Name = "Test",
                Width = 4,
                Height = 4,
                Entities = entities,
            },
        });

    [Fact]
    public void Validation_AcceptsUniqueNonEmptyKeys() =>
        Assert.Empty(new MapEntityKeyRule().Check(WithMap(
            new SignEntity { Key = "a", Pos = new GridPos(0, 0) },
            new SignEntity { Key = "b", Pos = new GridPos(1, 0) })));

    [Fact]
    public void Validation_RejectsADuplicateKey()
    {
        ValidationIssue issue = Assert.Single(new MapEntityKeyRule().Check(WithMap(
            new SignEntity { Key = "same", Pos = new GridPos(0, 0) },
            new SignEntity { Key = "same", Pos = new GridPos(1, 0) })));

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("more than once", issue.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validation_RejectsAnEmptyKey(string key)
    {
        ValidationIssue issue = Assert.Single(new MapEntityKeyRule().Check(WithMap(
            new SignEntity { Key = key, Pos = new GridPos(0, 0) })));
        Assert.Contains("no key", issue.Message);
    }

    [Fact]
    public void Validation_ReportsEveryOffender() =>
        Assert.Equal(2, new MapEntityKeyRule().Check(WithMap(
            new SignEntity { Key = "", Pos = new GridPos(0, 0) },
            new SignEntity { Key = "x", Pos = new GridPos(1, 0) },
            new SignEntity { Key = "x", Pos = new GridPos(2, 0) })).Count());

    /// <summary>Keys are scoped per map, so the same key in two maps is legitimate.</summary>
    [Fact]
    public void Validation_AllowsTheSameKeyInDifferentMaps()
    {
        var project = new Project(new ProjectSettings { Name = "T" }, new Dictionary<EntityId, IEntity>
        {
            [EntityId.Parse("map:a")] = new Map
            {
                Id = EntityId.Parse("map:a"), Name = "A", Width = 2, Height = 2,
                Entities = [new SignEntity { Key = "post", Pos = new GridPos(0, 0) }],
            },
            [EntityId.Parse("map:b")] = new Map
            {
                Id = EntityId.Parse("map:b"), Name = "B", Width = 2, Height = 2,
                Entities = [new SignEntity { Key = "post", Pos = new GridPos(0, 0) }],
            },
        });

        Assert.Empty(new MapEntityKeyRule().Check(project));
    }

    [Fact]
    public void Validation_AcceptsAMapWithNoEntities() =>
        Assert.Empty(new MapEntityKeyRule().Check(WithMap()));

    // --- Round trip ---------------------------------------------------------------

    [Fact]
    public void KeySurvivesSerializationRoundTrip()
    {
        var map = new Map
        {
            Id = EntityId.Parse("map:test"),
            Name = "Test",
            Width = 2,
            Height = 2,
            Entities = [new NpcEntity { Key = "rival", Pos = new GridPos(1, 1) }],
        };

        Map restored = CgmJson.DeserializeVersioned<Map>(CgmJson.SerializeEntity(map));
        Assert.Equal("rival", restored.Entities[0].Key);
    }
}
