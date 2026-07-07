using Cgm.Core.Model;

namespace Cgm.Core.Tests.Validation;

/// <summary>Builders for in-memory projects and valid baseline entities used by rule tests.</summary>
internal static class TestEntities
{
    public static Project Project(ProjectSettings settings, params IEntity[] entities) =>
        new(settings, entities.ToDictionary(e => e.Id));

    public static Project Project(params IEntity[] entities) =>
        Project(new ProjectSettings { Name = "T" }, entities);

    public static Species Species(string slug = "mon") => new()
    {
        Id = EntityId.Parse($"species:{slug}"),
        Name = slug,
        Types = [EntityId.Parse("type:fire")],
        BaseStats = new Stats(45, 45, 45, 45, 45, 45),
        GrowthRate = "medium-fast",
    };

    public static Move Move(string slug = "hit") => new()
    {
        Id = EntityId.Parse($"move:{slug}"),
        Name = slug,
        Type = EntityId.Parse("type:fire"),
        DamageClass = DamageClass.Physical,
        Power = 40,
        Accuracy = 100,
        Pp = 25,
    };

    public static Trainer Trainer(string slug = "rival") => new()
    {
        Id = EntityId.Parse($"trainer:{slug}"),
        Name = slug,
        Party = [new PartyMember { Species = EntityId.Parse("species:mon"), Level = 5 }],
    };
}
