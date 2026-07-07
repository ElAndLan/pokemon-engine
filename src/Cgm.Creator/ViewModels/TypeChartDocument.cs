using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cgm.Core.Model;
using Cgm.Creator.Editing;

namespace Cgm.Creator.ViewModels;

/// <summary>Type-chart editor (CREATOR_APP_SPEC §6.1): an attacker×defender matrix of
/// effectiveness multipliers. Clicking a cell cycles 1 → 2 → ½ → 0 and rewrites the attacker
/// type's damage lists (DATA_SCHEMA §4.2), undoably.</summary>
public sealed class TypeChartDocument : EditorDocument
{
    public TypeChartDocument(ProjectSession session) : base(session, null) => Build();

    public override string Title => "Type Chart";

    public IReadOnlyList<string> TypeLabels { get; private set; } = [];
    public ObservableCollection<TypeChartRow> Rows { get; } = [];

    public double Multiplier(EntityId attacker, EntityId defender)
    {
        TypeDef t = Session.Find<TypeDef>(attacker)!;
        if (t.NoDamageTo.Contains(defender)) return 0;
        if (t.DoubleDamageTo.Contains(defender)) return 2;
        if (t.HalfDamageTo.Contains(defender)) return 0.5;
        return 1;
    }

    public void Cycle(EntityId attacker, EntityId defender)
    {
        double next = Multiplier(attacker, defender) switch { 1 => 2, 2 => 0.5, 0.5 => 0, _ => 1 };
        TypeDef before = Session.Find<TypeDef>(attacker)!;
        TypeDef after = WithMultiplier(before, defender, next);

        Undo.Push(new SnapshotCommand<TypeDef>(before, after, td =>
        {
            Session.Put(td);
            foreach (TypeChartCell cell in Rows.First(r => r.Attacker == attacker).Cells)
                cell.Refresh();
        }));
    }

    private static TypeDef WithMultiplier(TypeDef t, EntityId defender, double mult)
    {
        List<EntityId> dbl = t.DoubleDamageTo.Where(x => x != defender).ToList();
        List<EntityId> half = t.HalfDamageTo.Where(x => x != defender).ToList();
        List<EntityId> no = t.NoDamageTo.Where(x => x != defender).ToList();

        if (mult == 2) dbl.Add(defender);
        else if (mult == 0.5) half.Add(defender);
        else if (mult == 0) no.Add(defender);

        return t with { DoubleDamageTo = dbl, HalfDamageTo = half, NoDamageTo = no };
    }

    private void Build()
    {
        var types = Session.All<TypeDef>().Select(t => t.Id)
            .OrderBy(id => id.Slug, StringComparer.Ordinal).ToList();
        TypeLabels = types.Select(id => id.Slug).ToList();
        Rows.Clear();
        foreach (EntityId attacker in types)
            Rows.Add(new TypeChartRow(
                attacker,
                types.Select(defender => new TypeChartCell(this, attacker, defender)).ToList()));
    }
}

public sealed record TypeChartRow(EntityId Attacker, IReadOnlyList<TypeChartCell> Cells)
{
    public string AttackerLabel => Attacker.Slug;
}

/// <summary>One editable matrix cell.</summary>
public sealed partial class TypeChartCell : ObservableObject
{
    private readonly TypeChartDocument _chart;

    public TypeChartCell(TypeChartDocument chart, EntityId attacker, EntityId defender)
    {
        _chart = chart;
        Attacker = attacker;
        Defender = defender;
    }

    public EntityId Attacker { get; }
    public EntityId Defender { get; }

    public string Display => _chart.Multiplier(Attacker, Defender) switch
    {
        0 => "0", 0.5 => "½", 2 => "2", _ => "1",
    };

    [RelayCommand]
    private void Cycle() => _chart.Cycle(Attacker, Defender);

    public void Refresh() => OnPropertyChanged(nameof(Display));
}
