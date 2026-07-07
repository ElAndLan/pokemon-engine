namespace Cgm.Core.Model;

/// <summary>An item (DATA_SCHEMA.md §4.5). Usability flags map from PokeAPI item attributes.</summary>
public sealed record Item : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public string Pocket { get; init; } = "items";
    public int Price { get; init; }
    public int? FlingPower { get; init; }
    public bool Consumable { get; init; }
    public bool UsableInField { get; init; }
    public bool UsableInBattle { get; init; }
    public bool Holdable { get; init; }
    public bool KeyItem { get; init; }
    public IReadOnlyList<Effect> Effects { get; init; } = [];
    public string? SpriteUrl { get; init; } // import-staging (ADR-010)
    public EntityId? Icon { get; init; }
}
