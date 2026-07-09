using System.Text.Json.Serialization;

namespace Cgm.Core.Model;

/// <summary>A map (DATA_SCHEMA.md §4.11): tile layers, collision/encounter overlays, and placed
/// entities.</summary>
public sealed record Map : IEntity
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";

    public int Width { get; init; }
    public int Height { get; init; }
    public IReadOnlyList<EntityId> Tilesets { get; init; } = [];
    public MapLayers Layers { get; init; } = new();
    public IReadOnlyList<CollisionOverride> CollisionOverrides { get; init; } = [];
    public IReadOnlyList<EncounterZoneCell> EncounterZones { get; init; } = [];
    public IReadOnlyList<MapEntity> Entities { get; init; } = [];
    public string? Bgm { get; init; }
    public bool Indoor { get; init; }
}

/// <summary>Row-major tile-index arrays per visual layer; -1 = empty cell.</summary>
public sealed record MapLayers
{
    public IReadOnlyList<int> Ground { get; init; } = [];
    public IReadOnlyList<int> DecoBelow { get; init; } = [];
    public IReadOnlyList<int> DecoAbove { get; init; } = [];
}

public enum CollisionValue { Solid, Open, LedgeUp, LedgeDown, LedgeLeft, LedgeRight }

public readonly record struct CollisionOverride(int Index, CollisionValue Value);

public readonly record struct EncounterZoneCell(int Index, EntityId Table);

public enum NpcMovement { Static, Wander, Patrol }
public enum WarpTransition { Door, Edge, Stairs }

/// <summary>A placed map entity (DATA_SCHEMA.md §4.11a), a tagged union keyed by <c>kind</c>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(PlayerStartEntity), "player-start")]
[JsonDerivedType(typeof(NpcEntity), "npc")]
[JsonDerivedType(typeof(WarpEntity), "warp")]
[JsonDerivedType(typeof(PickupEntity), "pickup")]
[JsonDerivedType(typeof(SignEntity), "sign")]
[JsonDerivedType(typeof(TriggerEntity), "trigger")]
public abstract record MapEntity
{
    public GridPos Pos { get; init; }
}

public sealed record PlayerStartEntity : MapEntity
{
    public Facing Facing { get; init; } = Facing.Down;
}

public sealed record NpcEntity : MapEntity
{
    public Facing Facing { get; init; } = Facing.Down;
    public EntityId? Sprite { get; init; }
    public NpcMovement Move { get; init; } = NpcMovement.Static;
    public int? Radius { get; init; }
    public IReadOnlyList<GridPos>? Path { get; init; }
    public string? Dialogue { get; init; }
    public EntityId? Trainer { get; init; }
}

public sealed record WarpEntity : MapEntity
{
    public EntityId Target { get; init; }
    public GridPos TargetPos { get; init; }
    public WarpTransition Transition { get; init; } = WarpTransition.Door;
}

public sealed record PickupEntity : MapEntity
{
    public EntityId Item { get; init; }
    public int Qty { get; init; } = 1;
    public string Flag { get; init; } = "";
}

public sealed record SignEntity : MapEntity
{
    public string Text { get; init; } = "";
}

public sealed record TriggerEntity : MapEntity
{
    public string? Condition { get; init; }
    public IReadOnlyList<string> Actions { get; init; } = []; // vocabulary grows Phase 7/16
}
