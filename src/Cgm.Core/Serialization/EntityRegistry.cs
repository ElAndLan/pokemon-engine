using Cgm.Core.Model;

namespace Cgm.Core.Serialization;

/// <summary>
/// The one mapping from an <see cref="EntityCategory"/> to its record type, shared by the folder
/// loader and the pack loader so they can never drift. Sprite/tile ids live inside sheet/tileset
/// files as projections (DATA_SCHEMA §4.6/§4.9), so those categories have no standalone type here.
/// </summary>
public static class EntityRegistry
{
    public static readonly IReadOnlyDictionary<EntityCategory, Type> ByCategory =
        new Dictionary<EntityCategory, Type>
        {
            [EntityCategory.Type] = typeof(TypeDef),
            [EntityCategory.Species] = typeof(Species),
            [EntityCategory.Move] = typeof(Move),
            [EntityCategory.Item] = typeof(Item),
            [EntityCategory.Ability] = typeof(Ability),
            [EntityCategory.Encounter] = typeof(EncounterTable),
            [EntityCategory.Trainer] = typeof(Trainer),
            [EntityCategory.Flag] = typeof(StoryFlag),
            [EntityCategory.Sheet] = typeof(SpriteSheet),
            [EntityCategory.Anim] = typeof(Animation),
            [EntityCategory.Tileset] = typeof(Tileset),
            [EntityCategory.Object] = typeof(MapObject),
            [EntityCategory.Map] = typeof(Map),
            [EntityCategory.Sound] = typeof(Sound),
        };

    public static Type TypeFor(EntityCategory category) =>
        ByCategory.TryGetValue(category, out Type? t)
            ? t
            : throw new InvalidDataException($"No entity type registered for category '{category}'.");
}
