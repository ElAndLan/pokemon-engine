using Cgm.Core.Model;

namespace Cgm.Core.Serialization;

/// <summary>
/// Loads a project folder (DATA_SCHEMA.md §3): <c>project.cgmproj</c> plus every
/// <c>data/&lt;category&gt;/&lt;slug&gt;.json</c> file, into an in-memory <see cref="Project"/>.
/// Enforces the folder/filename ↔ id integrity rules. Asset/world entities (sheets, tilesets,
/// maps…) join the registry in the next increment.
/// </summary>
public static class ProjectLoader
{
    public static Project Load(string projectFolder)
    {
        ProjectSettings settings = ProjectFile.Load(projectFolder);
        var entities = new Dictionary<EntityId, IEntity>();
        string dataRoot = Path.Combine(projectFolder, "data");

        foreach ((EntityCategory category, Type type) in EntityRegistry.ByCategory)
        {
            string dir = Path.Combine(dataRoot, category.ToString().ToLowerInvariant());
            if (!Directory.Exists(dir))
                continue;

            foreach (string file in Directory.EnumerateFiles(dir, "*.json"))
            {
                var entity = (IEntity)CgmJson.DeserializeVersioned(File.ReadAllText(file), type);
                string fileSlug = Path.GetFileNameWithoutExtension(file);

                if (entity.Id.Category != category)
                    throw new InvalidDataException(
                        $"{file}: id '{entity.Id}' is not in category '{category.ToString().ToLowerInvariant()}'.");
                if (entity.Id.Slug != fileSlug)
                    throw new InvalidDataException(
                        $"{file}: id slug '{entity.Id.Slug}' does not match file name '{fileSlug}'.");

                entities.Add(entity.Id, entity); // filename==slug makes this collision-free
            }
        }

        return new Project(settings, entities, projectFolder);
    }
}
