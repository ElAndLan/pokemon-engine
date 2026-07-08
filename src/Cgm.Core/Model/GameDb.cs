namespace Cgm.Core.Model;

/// <summary>
/// The compiled, immutable game database the runtime plays from (ADR-006). One loader builds the
/// same <see cref="GameDb"/> whether the source is a raw project folder (<see cref="FromProject"/>)
/// or a <c>.cgmpack</c> — the pack is just a container. Entities are held in a stable id order so
/// two GameDbs of the same content serialize identically (the pack-vs-folder unity test).
/// </summary>
public sealed class GameDb
{
    private readonly IReadOnlyDictionary<EntityId, IEntity> _byId;

    public GameDb(ProjectSettings settings, IReadOnlyList<IEntity> entities)
    {
        Settings = settings;
        Entities = entities;
        _byId = entities.ToDictionary(e => e.Id);
    }

    public ProjectSettings Settings { get; }

    /// <summary>All entities, ordered by id (ordinal) for determinism.</summary>
    public IReadOnlyList<IEntity> Entities { get; }

    public IEnumerable<T> All<T>() where T : IEntity => Entities.OfType<T>();

    public T? Find<T>(EntityId id) where T : class, IEntity =>
        _byId.TryGetValue(id, out IEntity? e) ? e as T : null;

    public static GameDb FromProject(Project project) =>
        new(project.Settings, [.. project.Entities.OrderBy(e => e.Id.ToString(), StringComparer.Ordinal)]);
}
