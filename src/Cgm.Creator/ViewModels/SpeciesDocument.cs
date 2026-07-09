using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Creator.ViewModels;

public sealed class SpeciesDocument : EntityEditorDocument<Species>
{
    public SpeciesDocument(ProjectSession session, Species model) : base(session, model) { }

    private string _editError = "";
    public string EditError
    {
        get => _editError;
        private set { if (value != _editError) { _editError = value; OnPropertyChanged(); } }
    }

    public string Name
    {
        get => Model.Name;
        set { if (value != Model.Name) Edit(Model with { Name = value }); }
    }

    public string AbilitiesText
    {
        get => string.Join(", ", Model.Abilities);
        set
        {
            if (!TryParseIds(value, out IReadOnlyList<EntityId> ids))
                return;
            Edit(Model with { Abilities = ids });
            EditError = "";
        }
    }

    public string HiddenAbilityText
    {
        get => Model.HiddenAbility?.ToString() ?? "";
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Edit(Model with { HiddenAbility = null });
                EditError = "";
                return;
            }
            if (!EntityId.TryParse(value.Trim(), out EntityId id))
            {
                EditError = $"Invalid EntityId '{value}'.";
                return;
            }
            Edit(Model with { HiddenAbility = id });
            EditError = "";
        }
    }

    public string FormsJson
    {
        get => CgmJson.Serialize(Model.Forms);
        set
        {
            try
            {
                var forms = CgmJson.Deserialize<List<Form>>(value);
                Edit(Model with { Forms = forms });
                EditError = "";
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
            {
                EditError = ex.Message;
            }
        }
    }

    private bool TryParseIds(string value, out IReadOnlyList<EntityId> ids)
    {
        var parsed = new List<EntityId>();
        foreach (string part in value.Split([',', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!EntityId.TryParse(part.Trim(), out EntityId id))
            {
                EditError = $"Invalid EntityId '{part}'.";
                ids = [];
                return false;
            }
            parsed.Add(id);
        }
        ids = parsed;
        return true;
    }
}
