using System.Text.Json;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class EntityIdTests
{
    [Theory]
    [InlineData("species:bulbasaur", EntityCategory.Species, "bulbasaur")]
    [InlineData("type:fire", EntityCategory.Type, "fire")]
    [InlineData("move:ember", EntityCategory.Move, "ember")]
    [InlineData("map:route_001", EntityCategory.Map, "route_001")]
    [InlineData("encounter:route_001_grass", EntityCategory.Encounter, "route_001_grass")]
    public void Parse_ValidIds(string text, EntityCategory cat, string slug)
    {
        EntityId id = EntityId.Parse(text);
        Assert.Equal(cat, id.Category);
        Assert.Equal(slug, id.Slug);
        Assert.Equal(text, id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("nocolon")]
    [InlineData(":fire")]                 // empty category
    [InlineData("type:")]                 // empty slug
    [InlineData("bogus:fire")]            // unknown category
    [InlineData("type:Fire")]             // uppercase slug not allowed
    [InlineData("type:fire-bug")]         // hyphen not allowed
    [InlineData("type:fire fly")]         // space not allowed
    [InlineData("type::fire")]            // empty first slug segment
    public void TryParse_RejectsInvalid(string text)
    {
        Assert.False(EntityId.TryParse(text, out _));
        Assert.Throws<FormatException>(() => EntityId.Parse(text));
    }

    [Fact]
    public void TryParse_IsCaseInsensitiveOnCategoryOnly()
    {
        Assert.True(EntityId.TryParse("SPECIES:bulbasaur", out EntityId id));
        Assert.Equal(EntityCategory.Species, id.Category);
        Assert.Equal("species:bulbasaur", id.ToString()); // normalized to lowercase prefix
    }

    [Fact]
    public void Constructor_RejectsBadSlug()
    {
        Assert.Throws<ArgumentException>(() => new EntityId(EntityCategory.Type, "Fire"));
        Assert.Throws<ArgumentException>(() => new EntityId(EntityCategory.Type, ""));
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var a = new EntityId(EntityCategory.Move, "ember");
        var b = EntityId.Parse("move:ember");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, new EntityId(EntityCategory.Move, "tackle"));
    }

    [Fact]
    public void WorksAsDictionaryKey()
    {
        var map = new Dictionary<EntityId, int> { [EntityId.Parse("item:potion")] = 5 };
        Assert.Equal(5, map[new EntityId(EntityCategory.Item, "potion")]);
    }

    [Fact]
    public void JsonRoundTrip_AsValue()
    {
        EntityId id = EntityId.Parse("species:leafcub");
        string json = JsonSerializer.Serialize(id);
        Assert.Equal("\"species:leafcub\"", json);
        Assert.Equal(id, JsonSerializer.Deserialize<EntityId>(json));
    }

    [Fact]
    public void JsonRoundTrip_AsDictionaryKey()
    {
        var data = new Dictionary<EntityId, int> { [EntityId.Parse("move:ember")] = 40 };
        string json = JsonSerializer.Serialize(data);
        Assert.Equal("{\"move:ember\":40}", json);
        var back = JsonSerializer.Deserialize<Dictionary<EntityId, int>>(json)!;
        Assert.Equal(40, back[EntityId.Parse("move:ember")]);
    }

    [Fact]
    public void Json_RejectsInvalidString()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EntityId>("\"bogus:x\""));
    }
}
