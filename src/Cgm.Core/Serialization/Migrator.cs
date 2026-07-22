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
/// before it is deserialized. Registered ordered steps upgrade old files automatically, and loading
/// a file newer than we support fails loudly instead of silently.
/// </summary>
public static class Migrator
{
    public const int CurrentVersion = SchemaVersions.Current;

    private static readonly IReadOnlyList<IJsonMigration> Registered =
    [
        new V1ToV2(), new V2ToV3(), new V3ToV4(), new V4ToV5(), new V5ToV6(), new V6ToV7(),
        new V7ToV8(), new V8ToV9(), new V9ToV10(), new V10ToV11(),
    ];

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

    private sealed class V5ToV6 : IJsonMigration
    {
        public int FromVersion => 5;
        public void Apply(JsonObject json) { }
    }

    private sealed class V6ToV7 : IJsonMigration
    {
        public int FromVersion => 6;
        public void Apply(JsonObject json) { }
    }

    /// <summary>Gives every placed map entity a stable key (DATA_SCHEMA §4.11a). Pre-v8 files
    /// addressed entities by list position, so keys derive from kind plus original index: stable for
    /// a given file, identical on every machine, and never dependent on enumeration order.</summary>
    private sealed class V7ToV8 : IJsonMigration
    {
        public int FromVersion => 7;

        public void Apply(JsonObject json)
        {
            if (json["entities"] is not JsonArray entities)
                return;

            // Authored keys win; derived keys must not collide with them, so claim those first.
            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonNode? node in entities)
                if (node is JsonObject entity && Existing(entity) is { } key)
                    taken.Add(key);

            for (int index = 0; index < entities.Count; index++)
            {
                if (entities[index] is not JsonObject entity || Existing(entity) is not null)
                    continue;

                string kind = entity["kind"]?.GetValue<string>() ?? "entity";
                string candidate = $"{kind.Replace('-', '_')}_{index}";
                for (int suffix = 2; !taken.Add(candidate); suffix++)
                    candidate = $"{kind.Replace('-', '_')}_{index}_{suffix}";
                entity["key"] = candidate;
            }
        }

        private static string? Existing(JsonObject entity) =>
            entity["key"] is JsonValue value && value.TryGetValue(out string? key)
                && !string.IsNullOrWhiteSpace(key) ? key : null;
    }

    /// <summary>Adds the <c>object</c> map-entity placement kind (DATA_SCHEMA §4.11a). Additive: a
    /// map without placed objects is unchanged, so no document needs rewriting.</summary>
    private sealed class V10ToV11 : IJsonMigration
    {
        public int FromVersion => 10;
        public void Apply(JsonObject json) { }
    }

    /// <summary>Sheets record their source image size (DATA_SCHEMA §4.6). The size cannot be derived
    /// from the document, and Core must not decode PNGs to find it, so this leaves <c>imageW/imageH</c>
    /// at zero and lets <c>sheet-slice</c> validation report the sheet as needing re-import. Inventing
    /// a size here would produce cells that slice the wrong pixels and pass validation.</summary>
    private sealed class V9ToV10 : IJsonMigration
    {
        public int FromVersion => 9;
        public void Apply(JsonObject json) { }
    }

    /// <summary>Closes the world-interaction vocabulary (DATA_SCHEMA §4.11b). Pre-v9 trigger actions
    /// and object interactions were free strings with no defined meaning. They convert to explicit
    /// <c>dialogue</c> actions carrying the original text: lossless, hand-correctable, and never
    /// guessing that a string meant something executable.</summary>
    private sealed class V8ToV9 : IJsonMigration
    {
        public int FromVersion => 8;

        public void Apply(JsonObject json)
        {
            if (json["entities"] is JsonArray entities)
                foreach (JsonNode? node in entities)
                    if (node is JsonObject entity && entity["kind"]?.GetValue<string>() == "trigger")
                        entity["actions"] = Convert(entity["actions"]);

            // Object documents carry a single interaction string rather than a list.
            if (json["interaction"] is JsonValue interaction)
                json["interaction"] = Convert(interaction);
        }

        private static JsonArray Convert(JsonNode? legacy)
        {
            var actions = new JsonArray();
            foreach (string text in Strings(legacy))
                actions.Add(new JsonObject { ["op"] = "dialogue", ["text"] = text });
            return actions;
        }

        private static IEnumerable<string> Strings(JsonNode? node) => node switch
        {
            JsonArray array => array.OfType<JsonValue>()
                .Select(value => value.TryGetValue(out string? text) ? text : null)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!),
            JsonValue value when value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text) => [text],
            _ => [],
        };
    }
}
