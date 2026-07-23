using System.Collections.ObjectModel;
using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>A category node in the left navigation tree, holding its entities. Carries the
/// <see cref="Category"/> so context actions (New here) know what to create, and a friendly icon
/// + count so the tree reads at a glance.</summary>
public sealed class NavCategory
{
    public NavCategory(EntityCategory category)
    {
        Category = category;
        Name = Plural(category);
        Icon = IconFor(category);
    }

    public EntityCategory Category { get; }
    public string Name { get; }
    public string Icon { get; }
    public ObservableCollection<NavEntity> Entities { get; } = [];

    /// <summary>"🗺 Maps (3)" — icon, plural name, and count in one label for the tree row.</summary>
    public string Header => $"{Icon}  {Name}  ({Entities.Count})";

    private static string Plural(EntityCategory c) => c switch
    {
        EntityCategory.Type => "Types",
        EntityCategory.Species => "Species",
        EntityCategory.Move => "Moves",
        EntityCategory.Item => "Items",
        EntityCategory.Ability => "Abilities",
        EntityCategory.Tileset => "Tilesets",
        EntityCategory.Map => "Maps",
        EntityCategory.Sheet => "Sprite sheets",
        EntityCategory.Sound => "Sounds",
        EntityCategory.Anim => "Animations",
        EntityCategory.Encounter => "Encounter tables",
        EntityCategory.Trainer => "Trainers",
        EntityCategory.Object => "Objects",
        EntityCategory.Flag => "Flags",
        _ => c + "s",
    };

    private static string IconFor(EntityCategory c) => c switch
    {
        EntityCategory.Type => "🏷",
        EntityCategory.Species => "🐾",
        EntityCategory.Move => "⚔",
        EntityCategory.Item => "🎒",
        EntityCategory.Ability => "✨",
        EntityCategory.Tileset => "🧱",
        EntityCategory.Map => "🗺",
        EntityCategory.Sheet => "🖼",
        EntityCategory.Sound => "🔊",
        EntityCategory.Anim => "🎞",
        EntityCategory.Encounter => "🌿",
        EntityCategory.Trainer => "🧑",
        EntityCategory.Object => "🏠",
        EntityCategory.Flag => "🚩",
        _ => "📄",
    };
}

/// <summary>A selectable entity leaf in the navigation tree.</summary>
public sealed record NavEntity(EntityId Id, string Label);
