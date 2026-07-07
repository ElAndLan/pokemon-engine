namespace Cgm.Core.Model;

/// <summary>
/// Common surface for every stored entity: its stable <see cref="Id"/> and the
/// <see cref="SchemaVersion"/> its file was written at (used by the migrator). DATA_SCHEMA.md §1.
/// </summary>
public interface IEntity
{
    int SchemaVersion { get; }
    EntityId Id { get; }
}
