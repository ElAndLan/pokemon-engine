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
    private static readonly IReadOnlyDictionary<EntityCategory, Type> Registry =
        new Dictionary<EntityCategory, Type>
        {
            [EntityCategory.Type] = typeof(TypeDef),
            [EntityCategory.Species] = typeof(Species),
            [EntityCategory.Move] = typeof(Move),
            [EntityCategory.Item] = typeof(Item),
            [EntityCategory.Encounter] = typeof(EncounterTable),
            [EntityCategory.Trainer] = typeof(Trainer),
            [EntityCategory.Flag] = typeof(StoryFlag),
            [EntityCategory.Sheet] = typeof(SpriteSheet),
            [EntityCategory.Anim] = typeof(Animation),
            [EntityCategory.Tileset] = typeof(Tileset),
            [EntityCategory.Object] = typeof(MapObject),
            [EntityCategory.Map] = typeof(Map),
        };
    // Note: sprite/tile ids are defined inside sheet/tileset files (projections), not standalone
    // files, so those categories are not loaded here (DATA_SCHEMA.md §4.6/§4.9).

    public static Project Load(string projectFolder)
    {
        ProjectSettings settings = ProjectFile.Load(projectFolder);
        var entities = new Dictionary<EntityId, IEntity>();
        string dataRoot = Path.Combine(projectFolder, "data");

        foreach ((EntityCategory category, Type type) in Registry)
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

        return new Project(settings, entities);
    }
}
