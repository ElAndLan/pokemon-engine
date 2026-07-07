namespace Cgm.Core.Model;

public enum ObjectLayer { Below, Above }

/// <summary>A multi-tile world object (DATA_SCHEMA.md §4.10). Category <c>object</c>.</summary>
public sealed record MapObject : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public int FootprintW { get; init; } = 1;
    public int FootprintH { get; init; } = 1;
    public IReadOnlyList<bool> Collision { get; init; } = [];
    public GridPos Anchor { get; init; }
    public ObjectLayer Layer { get; init; } = ObjectLayer.Below;
    public EntityId? Sprite { get; init; }
    public EntityId? Anim { get; init; }
    public string? Interaction { get; init; } // sign/… vocabulary grows Phase 7/16
}
