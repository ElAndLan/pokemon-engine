using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>Basic item editor (CREATOR_APP_SPEC §6.2) — a copy of the same single-entity pattern
/// as <see cref="MoveDocument"/>, proving the reusable editor shape.</summary>
public sealed class ItemDocument : EntityEditorDocument<Item>
{
    public ItemDocument(ProjectSession session, Item model) : base(session, model) { }

    public string Name
    {
        get => Model.Name;
        set { if (value != Model.Name) Edit(Model with { Name = value }); }
    }

    public string Pocket
    {
        get => Model.Pocket;
        set { if (value != Model.Pocket) Edit(Model with { Pocket = value }); }
    }

    public int Price
    {
        get => Model.Price;
        set { if (value != Model.Price) Edit(Model with { Price = value }); }
    }

    public bool KeyItem
    {
        get => Model.KeyItem;
        set { if (value != Model.KeyItem) Edit(Model with { KeyItem = value }); }
    }

    public bool Consumable
    {
        get => Model.Consumable;
        set { if (value != Model.Consumable) Edit(Model with { Consumable = value }); }
    }

    public bool UsableInField
    {
        get => Model.UsableInField;
        set { if (value != Model.UsableInField) Edit(Model with { UsableInField = value }); }
    }

    public bool UsableInBattle
    {
        get => Model.UsableInBattle;
        set { if (value != Model.UsableInBattle) Edit(Model with { UsableInBattle = value }); }
    }

    /// <summary>Pockets defined by the project (populates the pocket combo).</summary>
    public IReadOnlyList<string> AvailablePockets => Session.Settings.Pockets;
}
