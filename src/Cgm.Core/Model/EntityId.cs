using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgm.Core.Model;

/// <summary>The closed set of entity categories (DATA_SCHEMA.md §2). Adding one is a schema change.</summary>
public enum EntityCategory
{
    Project, Type, Species, Move, Item, Tileset, Tile, Object,
    Sheet, Sprite, Anim, Map, Encounter, Trainer, Flag, Box,
}

/// <summary>
/// A stable, immutable entity reference of the form <c>category:slug</c> (DATA_SCHEMA.md §2),
/// e.g. <c>species:bulbasaur</c>. Slugs match <c>[a-z0-9_]+</c>. Serializes as its string form.
/// </summary>
[JsonConverter(typeof(EntityIdJsonConverter))]
public readonly record struct EntityId
{
    public EntityId(EntityCategory category, string slug)
    {
        if (!IsValidSlug(slug))
            throw new ArgumentException($"Invalid slug '{slug}'; must match [a-z0-9_]+.", nameof(slug));

        Category = category;
        Slug = slug;
    }

    public EntityCategory Category { get; }
    public string Slug { get; }

    /// <summary>Lowercase category token used in the string form (e.g. "species").</summary>
    public string Prefix => Category.ToString().ToLowerInvariant();

    public override string ToString() => $"{Prefix}:{Slug}";

    public static EntityId Parse(string value) =>
        TryParse(value, out EntityId id) ? id : throw new FormatException($"Invalid EntityId '{value}'.");

    public static bool TryParse(string? value, out EntityId id)
    {
        id = default;
        if (string.IsNullOrEmpty(value))
            return false;

        int colon = value.IndexOf(':');
        if (colon <= 0 || colon == value.Length - 1)
            return false;

        if (!Enum.TryParse(value[..colon], ignoreCase: true, out EntityCategory category))
            return false;

        string slug = value[(colon + 1)..];
        if (!IsValidSlug(slug))
            return false;

        id = new EntityId(category, slug);
        return true;
    }

    public static bool IsValidSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) &&
        slug.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
}

/// <summary>Serializes <see cref="EntityId"/> as its <c>category:slug</c> string.</summary>
public sealed class EntityIdJsonConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        return EntityId.TryParse(s, out EntityId id) ? id : throw new JsonException($"Invalid EntityId '{s}'.");
    }

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());

    // Support EntityId as a dictionary KEY (serializes/reads the same string form).
    public override EntityId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? s = reader.GetString();
        return EntityId.TryParse(s, out EntityId id) ? id : throw new JsonException($"Invalid EntityId key '{s}'.");
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.ToString());
}
