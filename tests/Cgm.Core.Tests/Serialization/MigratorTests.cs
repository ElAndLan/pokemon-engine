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

    [Fact]
    public void Migrate_AppliesStepAndBumpsVersion()
    {
        var json = new JsonObject { ["schemaVersion"] = 0, ["foo"] = 42 };
        JsonObject result = Migrator.Migrate(json, [new RenameFooToBar()]);

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
        Assert.Equal(1, json["x"]!.GetValue<int>());
    }
}
