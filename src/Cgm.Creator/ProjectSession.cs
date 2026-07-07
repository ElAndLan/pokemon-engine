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

    public static ProjectSession Open(string folder)
    {
        Project project = ProjectLoader.Load(folder);
        return new ProjectSession(folder, project.Settings, project.Entities.ToDictionary(e => e.Id));
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

    public void Save()
    {
        if (SettingsDirty)
        {
            ProjectFile.Save(Folder, Settings);
            SettingsDirty = false;
        }

        foreach (EntityId id in _dirty)
        {
            string path = EntityPath(id);
            if (_entities.TryGetValue(id, out IEntity? entity))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, CgmJson.SerializeEntity(entity));
            }
            else if (File.Exists(path))
            {
                File.Delete(path); // entity was removed
            }
        }

        _dirty.Clear();
    }

    private string EntityPath(EntityId id) =>
        Path.Combine(Folder, "data", id.Prefix, id.Slug + ".json");
}
