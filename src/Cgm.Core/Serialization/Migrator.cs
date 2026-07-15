using System.Text.Json.Nodes;
using Cgm.Core.Model;

namespace Cgm.Core.Serialization;

/// <summary>An in-place upgrade of one entity/settings JSON object from
/// <see cref="FromVersion"/> to <c>FromVersion + 1</c> (DATA_SCHEMA.md §6).</summary>
public interface IJsonMigration
{
    int FromVersion { get; }
    void Apply(JsonObject json);
}

/// <summary>
/// Runs registered migrations to bring a parsed JSON object up to <see cref="CurrentVersion"/>
/// before it is deserialized. Everything is v1 today, so no migrations are registered yet — but
/// the mechanism is wired into the load path so a future version bump upgrades old files
/// automatically, and loading a file newer than we support fails loudly instead of silently.
/// </summary>
public static class Migrator
{
    public const int CurrentVersion = SchemaVersions.Current;

    private static readonly IReadOnlyList<IJsonMigration> Registered =
        [new V1ToV2(), new V2ToV3(), new V3ToV4(), new V4ToV5()];

    public static JsonObject Migrate(JsonObject json) => Migrate(json, Registered);

    public static JsonObject Migrate(JsonObject json, IReadOnlyList<IJsonMigration> migrations)
    {
        int version = ReadVersion(json);
        if (version > CurrentVersion)
            throw new InvalidDataException(
                $"Schema version {version} is newer than this app supports ({CurrentVersion}). Update the app.");

        while (version < CurrentVersion)
        {
            IJsonMigration step = migrations.FirstOrDefault(m => m.FromVersion == version)
                ?? throw new InvalidDataException($"No migration registered from schema version {version}.");
            step.Apply(json);
            version++;
            json["schemaVersion"] = version;
        }

        return json;
    }

    private static int ReadVersion(JsonObject json) =>
        json.TryGetPropertyValue("schemaVersion", out JsonNode? v) && v is not null ? v.GetValue<int>() : 1;

    private sealed class V1ToV2 : IJsonMigration
    {
        public int FromVersion => 1;
        public void Apply(JsonObject json) { }
    }

    private sealed class V2ToV3 : IJsonMigration
    {
        public int FromVersion => 2;
        public void Apply(JsonObject json) { }
    }

    private sealed class V3ToV4 : IJsonMigration
    {
        public int FromVersion => 3;
        public void Apply(JsonObject json) { }
    }

    private sealed class V4ToV5 : IJsonMigration
    {
        public int FromVersion => 4;
        public void Apply(JsonObject json)
        {
            if (json["id"]?.GetValue<string>().StartsWith("species:", StringComparison.Ordinal) != true)
                return;
            json["weightHectograms"] ??= 1;
            json["heightDecimeters"] ??= 1;
        }
    }
}
