namespace Cgm.Core.Model;

public enum FlagType { Bool, Int }

/// <summary>A declared story flag (DATA_SCHEMA.md §4.14).</summary>
public sealed record StoryFlag : IEntity
{
    public int SchemaVersion { get; init; } = 1;
    public EntityId Id { get; init; }
    public FlagType Type { get; init; } = FlagType.Bool;
    public string Description { get; init; } = "";
}
