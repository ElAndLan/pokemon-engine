using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Creator;

/// <summary>
/// The Creator's editable working copy of a project (CREATOR_APP_SPEC §3): the loaded settings and
/// entities, with per-entity dirty tracking. Edits replace immutable records in place;
/// <see cref="Save"/> writes only the dirty ones back through <see cref="Cgm.Core"/>. A
/// <see cref="Snapshot"/> feeds the validator. The Creator never mutates game state at runtime —
/// this is authoring data only.
/// </summary>
public sealed class ProjectSession
{
    private readonly Dictionary<EntityId, IEntity> _entities;
    private readonly HashSet<EntityId> _dirty = [];

    private ProjectSession(string folder, ProjectSettings settings, Dictionary<EntityId, IEntity> entities)
    {
        Folder = folder;
        Settings = settings;
        _entities = entities;
    }

    public string Folder { get; }
    public ProjectSettings Settings { get; private set; }
    public bool SettingsDirty { get; private set; }
    public bool IsDirty => _dirty.Count > 0 || SettingsDirty;

    /// <summary>True when opening found and rolled back a save the previous process never finished
    /// (CREATOR_APP_SPEC §10.2 step 5). The shell reports it; the data is already consistent.</summary>
    public bool RolledBackInterruptedSave { get; private init; }

    public static ProjectSession Open(string folder)
    {
        bool rolledBack = Editing.SaveTransaction.RollbackIfUnfinished(folder);
        Project project = ProjectLoader.Load(folder);
        return new ProjectSession(folder, project.Settings, project.Entities.ToDictionary(e => e.Id))
        {
            RolledBackInterruptedSave = rolledBack,
        };
    }

    /// <summary>A read-only view for validation.</summary>
    public Project Snapshot() => new(Settings, _entities);

    public IEnumerable<T> All<T>() where T : IEntity => _entities.Values.OfType<T>();
    public T? Find<T>(EntityId id) where T : class, IEntity =>
        _entities.TryGetValue(id, out IEntity? e) ? e as T : null;
    public IEntity? Get(EntityId id) => _entities.GetValueOrDefault(id);
    public bool Contains(EntityId id) => _entities.ContainsKey(id);

    /// <summary>Insert or replace an entity (the normal path for an editor commit).</summary>
    public void Put(IEntity entity)
    {
        _entities[entity.Id] = entity;
        _dirty.Add(entity.Id);
    }

    /// <summary>Create a new entity; throws if the id already exists.</summary>
    public void Add(IEntity entity)
    {
        if (!_entities.TryAdd(entity.Id, entity))
            throw new InvalidOperationException($"Entity '{entity.Id}' already exists.");
        _dirty.Add(entity.Id);
    }

    public void Remove(EntityId id)
    {
        if (_entities.Remove(id))
            _dirty.Add(id); // remembered so Save deletes the file
    }

    public void UpdateSettings(ProjectSettings settings)
    {
        Settings = settings;
        SettingsDirty = true;
    }

    /// <summary>Writes every dirty file as one atomic transaction (CREATOR_APP_SPEC §10.2). On
    /// failure the project on disk is unchanged and the session stays dirty; the caller surfaces
    /// the error and the user may retry.</summary>
    public void Save()
    {
        var writes = new List<(string RelPath, string? Content)>();
        if (SettingsDirty)
            writes.Add((ProjectFile.FileName, CgmJson.Serialize(Settings)));
        foreach (EntityId id in _dirty)
            writes.Add((EntityRelPath(id),
                _entities.TryGetValue(id, out IEntity? entity) ? CgmJson.SerializeEntity(entity) : null));

        Editing.SaveTransaction.Run(Folder, writes);

        // Only a committed transaction clears dirt; an exception above leaves everything dirty.
        _dirty.Clear();
        SettingsDirty = false;
    }

    private static string EntityRelPath(EntityId id) =>
        Path.Combine("data", id.Prefix, id.Slug + ".json");
}
