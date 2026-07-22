using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>One row in the picker: display name plus the stable id it binds.</summary>
public sealed record ReferenceChoice(EntityId Id, string Name)
{
    public string Display => $"{Name} ({Id})";
}

/// <summary>
/// The shared searchable reference picker (CREATOR_APP_SPEC §10.8): filters a category's entities
/// by display name or slug as typed, binds an <see cref="EntityId"/>. Headless; views host it in a
/// window or inline. A broken current value stays visible as broken rather than being cleared.
/// </summary>
public sealed partial class ReferencePickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<ReferenceChoice> _all;

    public ReferencePickerViewModel(IEnumerable<(EntityId Id, string Name)> candidates)
    {
        _all = candidates.Select(c => new ReferenceChoice(c.Id, c.Name))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id.Slug, StringComparer.Ordinal)
            .ToList();
        Refilter();
    }

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ReferenceChoice? _selected;

    public ObservableCollection<ReferenceChoice> Choices { get; } = [];

    partial void OnSearchTextChanged(string value) => Refilter();

    private void Refilter()
    {
        Choices.Clear();
        foreach (ReferenceChoice choice in _all)
            if (Matches(choice))
                Choices.Add(choice);
        if (Selected is { } current && !Choices.Contains(current))
            Selected = null;
    }

    private bool Matches(ReferenceChoice choice) =>
        string.IsNullOrWhiteSpace(SearchText)
        || choice.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
        || choice.Id.Slug.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
}
