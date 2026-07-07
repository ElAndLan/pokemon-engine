using System.Collections.ObjectModel;
using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>A category node in the left navigation tree, holding its entities.</summary>
public sealed class NavCategory
{
    public NavCategory(string name) => Name = name;

    public string Name { get; }
    public ObservableCollection<NavEntity> Entities { get; } = [];
}

/// <summary>A selectable entity leaf in the navigation tree.</summary>
public sealed record NavEntity(EntityId Id, string Label);
