using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Creator.ViewModels;

public sealed class AbilityDocument : EntityEditorDocument<Ability>
{
    public AbilityDocument(ProjectSession session, Ability model) : base(session, model) { }

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

    public string HooksJson
    {
        get => CgmJson.Serialize(Model.Hooks);
        set
        {
            try
            {
                var hooks = CgmJson.Deserialize<List<AbilityHook>>(value);
                Edit(Model with { Hooks = hooks });
                JsonError = "";
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
            {
                JsonError = ex.Message;
            }
        }
    }
}
