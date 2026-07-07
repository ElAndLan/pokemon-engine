namespace Cgm.Core.Model;

public enum LedgeDir { None, Up, Down, Left, Right }

/// <summary>A tileset: its tiles carry gameplay flags (DATA_SCHEMA.md §4.9). Tiles are nested,
/// not standalone files.</summary>
public sealed record Tileset : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";
    public IReadOnlyList<Tile> Tiles { get; init; } = [];
}

public sealed record Tile
{
    public EntityId? Sprite { get; init; }
    public EntityId? Anim { get; init; }
    public bool Solid { get; init; }
    public bool Grass { get; init; }
    public bool Water { get; init; }
    public LedgeDir Ledge { get; init; } = LedgeDir.None;
    public bool Counter { get; init; }
    public string TerrainTag { get; init; } = "";
}
