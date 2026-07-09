namespace Cgm.Core.Model;

public enum Facing { Down, Up, Left, Right }

public enum ClockMode { Realtime, Ingame }

/// <summary>A tile coordinate on a map.</summary>
public readonly record struct GridPos(int X, int Y);

/// <summary>The project root record (DATA_SCHEMA.md §4.1), stored as <c>project.cgmproj</c>.</summary>
public sealed record ProjectSettings
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public EntityId Id { get; init; } = new(EntityCategory.Project, "main");
    public string Name { get; init; } = "";
    public string EngineVersion { get; init; } = "";
    public int TileSize { get; init; } = 16;

    public EntityId? StartMap { get; init; }
    public GridPos StartPos { get; init; }
    public Facing StartFacing { get; init; } = Facing.Down;

    public IReadOnlyList<EntityId> StarterParty { get; init; } = [];
    public PlayerSprites PlayerSprites { get; init; } = new();
    public IReadOnlyList<string> Pockets { get; init; } = ["items", "medicine", "balls", "key"];
    public BoxConfig Boxes { get; init; } = new();
    public ClockConfig Clock { get; init; } = new();
    public EncounterDefaults EncounterDefaults { get; init; } = new();
}

public sealed record PlayerSprites
{
    public EntityId? Front { get; init; }
    public EntityId? Back { get; init; }
    public IReadOnlyList<EntityId> WalkClips { get; init; } = [];
}

public sealed record BoxConfig
{
    public int Count { get; init; } = 8;
    public int Capacity { get; init; } = 30;
    public IReadOnlyList<string> Names { get; init; } = [];
}

public sealed record ClockConfig
{
    public ClockMode Mode { get; init; } = ClockMode.Ingame;
    public int CycleMinutes { get; init; } = 60;
}

public sealed record EncounterDefaults
{
    public double GrassRatePerStep { get; init; } = 0.08;
}
