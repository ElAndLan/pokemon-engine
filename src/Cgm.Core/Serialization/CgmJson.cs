using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Cgm.Core.Serialization;

/// <summary>
/// The one JSON configuration for all project data (DATA_SCHEMA.md §1): camelCase field names,
/// string enums, 2-space indent, nulls omitted, tolerant reads. Reflection-based serialization
/// writes properties in declaration order, which is deterministic — that gives the byte-stable
/// output fixtures and git diffs rely on, without needing a source generator yet.
/// </summary>
public static class CgmJson
{
    public static readonly JsonSerializerOptions Options = Build();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Serializes an entity by its concrete runtime type (not the static/interface type),
    /// so saving through <c>IEntity</c> writes all of the real record's fields.</summary>
    public static string SerializeEntity(object entity) =>
        JsonSerializer.Serialize(entity, entity.GetType(), Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidDataException($"JSON deserialized to null for {typeof(T).Name}.");

    public static object Deserialize(string json, Type type) =>
        JsonSerializer.Deserialize(json, type, Options)
        ?? throw new InvalidDataException($"JSON deserialized to null for {type.Name}.");

    /// <summary>Parses, runs schema migrations (<see cref="Migrator"/>), then deserializes. Use
    /// this for anything loaded from disk so old files are upgraded and too-new files rejected.</summary>
    public static T DeserializeVersioned<T>(string json) => (T)DeserializeVersioned(json, typeof(T));

    public static object DeserializeVersioned(string json, Type type)
    {
        JsonObject obj = JsonNode.Parse(json, documentOptions: DocumentOptions) as JsonObject
            ?? throw new InvalidDataException("Expected a JSON object at the document root.");
        Migrator.Migrate(obj);
        return obj.Deserialize(type, Options)
            ?? throw new InvalidDataException($"JSON deserialized to null for {type.Name}.");
    }

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions
        {
            WriteIndented = true,
            IndentSize = 2,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return o;
    }
}
