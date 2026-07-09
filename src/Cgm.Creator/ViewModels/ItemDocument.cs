using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Creator.ViewModels;

/// <summary>Basic item editor (CREATOR_APP_SPEC §6.2) — a copy of the same single-entity pattern
/// as <see cref="MoveDocument"/>, proving the reusable editor shape.</summary>
public sealed class ItemDocument : EntityEditorDocument<Item>
{
    public ItemDocument(ProjectSession session, Item model) : base(session, model) { }

    private string _jsonError = "";
    public string JsonError
    {
        get => _jsonError;
        private set { if (value != _jsonError) { _jsonError = value; OnPropertyChanged(); } }
    }

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

    public bool Holdable
    {
        get => Model.Holdable;
        set { if (value != Model.Holdable) Edit(Model with { Holdable = value }); }
    }

    public string BattleEffectsJson
    {
        get => CgmJson.Serialize(Model.BattleEffects);
        set
        {
            try
            {
                var effects = CgmJson.Deserialize<List<Effect>>(value);
                Edit(Model with { BattleEffects = effects });
                JsonError = "";
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
            {
                JsonError = ex.Message;
            }
        }
    }

    /// <summary>Pockets defined by the project (populates the pocket combo).</summary>
    public IReadOnlyList<string> AvailablePockets => Session.Settings.Pockets;
}
