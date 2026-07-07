namespace Cgm.Core.Model;

/// <summary>
/// A loaded project: its <see cref="Settings"/> (from <c>project.cgmproj</c>) plus every entity
/// keyed by id (ADR/ADDENDUM §6). Entity ids are globally unique because the category is part of
/// the id, so one dictionary suffices; <see cref="All{T}"/>/<see cref="Find{T}"/> give typed views.
/// </summary>
public sealed class Project
{
    private readonly IReadOnlyDictionary<EntityId, IEntity> _entities;

    public Project(ProjectSettings settings, IReadOnlyDictionary<EntityId, IEntity> entities)
    {
        Settings = settings;
        _entities = entities;
    }

    public ProjectSettings Settings { get; }

    public IReadOnlyCollection<IEntity> Entities => (IReadOnlyCollection<IEntity>)_entities.Values;

    public bool Contains(EntityId id) => _entities.ContainsKey(id);

    public IEnumerable<T> All<T>() where T : IEntity => _entities.Values.OfType<T>();

    public T? Find<T>(EntityId id) where T : class, IEntity =>
        _entities.TryGetValue(id, out IEntity? e) ? e as T : null;
}
