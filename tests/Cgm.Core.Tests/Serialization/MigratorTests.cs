using System.Text.Json.Nodes;
using Cgm.Core.Serialization;

namespace Cgm.Core.Tests.Serialization;

public sealed class MigratorTests
{
    /// <summary>A synthetic v0→v1 step used to exercise the mechanism.</summary>
    private sealed class RenameFooToBar : IJsonMigration
    {
        public int FromVersion => 0;
        public void Apply(JsonObject json)
        {
            if (json.Remove("foo", out JsonNode? value))
                json["bar"] = value;
        }
    }

    private sealed class NoOpV1ToV2 : IJsonMigration
    {
        public int FromVersion => 1;
        public void Apply(JsonObject json) { }
    }

    private sealed class NoOpV2ToV3 : IJsonMigration
    {
        public int FromVersion => 2;
        public void Apply(JsonObject json) { }
    }

    private sealed class NoOpV3ToV4 : IJsonMigration
    {
        public int FromVersion => 3;
        public void Apply(JsonObject json) { }
    }

    [Fact]
    public void Migrate_AppliesStepAndBumpsVersion()
    {
        var json = new JsonObject { ["schemaVersion"] = 0, ["foo"] = 42 };
        JsonObject result = Migrator.Migrate(json, [new RenameFooToBar(), new NoOpV1ToV2(), new NoOpV2ToV3(), new NoOpV3ToV4()]);

        Assert.Equal(Migrator.CurrentVersion, result["schemaVersion"]!.GetValue<int>());
        Assert.Null(result["foo"]);
        Assert.Equal(42, result["bar"]!.GetValue<int>());
    }

    [Fact]
    public void Migrate_NoOpWhenAlreadyCurrent()
    {
        var json = new JsonObject { ["schemaVersion"] = Migrator.CurrentVersion, ["x"] = 1 };
        Migrator.Migrate(json);
        Assert.Equal(Migrator.CurrentVersion, json["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public void Migrate_ThrowsWhenNewerThanSupported()
    {
        var json = new JsonObject { ["schemaVersion"] = Migrator.CurrentVersion + 5 };
        Assert.Throws<InvalidDataException>(() => Migrator.Migrate(json));
    }

    [Fact]
    public void Migrate_ThrowsWhenNoStepRegisteredForOldVersion()
    {
        var json = new JsonObject { ["schemaVersion"] = 0 };
        Assert.Throws<InvalidDataException>(() => Migrator.Migrate(json)); // no registered v0→v1 yet
    }

    [Fact]
    public void Migrate_MissingVersionTreatedAsCurrent()
    {
        var json = new JsonObject { ["x"] = 1 };
        Migrator.Migrate(json);
        Assert.Equal(Migrator.CurrentVersion, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal(1, json["x"]!.GetValue<int>());
    }

    [Fact]
    public void Migrate_V1ToCurrent_IsRegisteredNoOp()
    {
        var json = new JsonObject { ["schemaVersion"] = 1, ["id"] = "item:berry" };
        Migrator.Migrate(json);
        Assert.Equal(Migrator.CurrentVersion, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal("item:berry", json["id"]!.GetValue<string>());
    }
}
