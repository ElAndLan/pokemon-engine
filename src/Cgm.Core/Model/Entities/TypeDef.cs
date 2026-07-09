namespace Cgm.Core.Model;

/// <summary>A damage type (DATA_SCHEMA.md §4.2). The effectiveness matrix is derived from these
/// "…To" lists; "…From" lists are their inverse and not stored.</summary>
public sealed record TypeDef : IEntity
{
    public int SchemaVersion { get; init; } = SchemaVersions.Current;
    public EntityId Id { get; init; }
    public string Name { get; init; } = "";
    public IReadOnlyList<EntityId> DoubleDamageTo { get; init; } = [];
    public IReadOnlyList<EntityId> HalfDamageTo { get; init; } = [];
    public IReadOnlyList<EntityId> NoDamageTo { get; init; } = [];
}
