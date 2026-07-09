namespace Cgm.Core.Model;

public enum SliceMode { Grid, Rects }

public enum SpriteClass { Tile, Object, Character, CreatureFront, CreatureBack, Icon, Ui }

/// <summary>A pixel rectangle within a sheet.</summary>
public readonly record struct Rect(int X, int Y, int W, int H);

/// <summary>A sprite sheet + its slice metadata (DATA_SCHEMA.md §4.6). Sprites are projections of
/// its <see cref="Cells"/> (there are no standalone sprite files).</summary>
public sealed record SpriteSheet : IEntity
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public string Asset { get; init; } = "";
    public string? ContentHash { get; init; }
    public SliceMode Mode { get; init; } = SliceMode.Grid;
    public int CellW { get; init; }
    public int CellH { get; init; }
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
    public int SpacingX { get; init; }
    public int SpacingY { get; init; }
    public IReadOnlyList<SheetCell> Cells { get; init; } = [];
}

public sealed record SheetCell
{
    public int? Index { get; init; }
    public Rect? Rect { get; init; }
    public EntityId SpriteId { get; init; }
    public SpriteClass Class { get; init; } = SpriteClass.Tile;
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>An animation clip referencing sprite ids (DATA_SCHEMA.md §4.8).</summary>
public sealed record Animation : IEntity
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";
    public IReadOnlyList<AnimFrame> Frames { get; init; } = [];
    public bool Loop { get; init; } = true;
}

public readonly record struct AnimFrame(EntityId Sprite, int Ms);
