namespace Cgm.Core.Model;

public enum EncounterMethod { Grass, Cave, Water, Tile, Interact }

/// <summary>An encounter table (DATA_SCHEMA.md §4.12), reusable across maps.</summary>
public sealed record EncounterTable : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";
    public EncounterMethod Method { get; init; } = EncounterMethod.Grass;
    public double BaseRate { get; init; } = 0.08;
    public IReadOnlyList<EncounterSlot> Slots { get; init; } = [];
}

public sealed record EncounterSlot
{
    public EntityId Species { get; init; }
    public int Weight { get; init; } = 1;
    public int MinLevel { get; init; } = 1;
    public int MaxLevel { get; init; } = 1;
    public TimeOfDay? TimeOfDay { get; init; }
    public string? RequiredFlag { get; init; }
}
