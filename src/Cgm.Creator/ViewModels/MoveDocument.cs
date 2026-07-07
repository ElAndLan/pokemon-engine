using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>Basic move editor (CREATOR_APP_SPEC §6.3). Effect-list editing lands with the shared
/// effect control; here: the core fields + type reference.</summary>
public sealed class MoveDocument : EntityEditorDocument<Move>
{
    public MoveDocument(ProjectSession session, Move model) : base(session, model) { }

    public string Name
    {
        get => Model.Name;
        set { if (value != Model.Name) Edit(Model with { Name = value }); }
    }

    public EntityId? Type
    {
        get => Model.Type;
        set { if (value is { } t && t != Model.Type) Edit(Model with { Type = t }); }
    }

    public DamageClass DamageClass
    {
        get => Model.DamageClass;
        set { if (value != Model.DamageClass) Edit(Model with { DamageClass = value }); }
    }

    public int? Power
    {
        get => Model.Power;
        set { if (value != Model.Power) Edit(Model with { Power = value }); }
    }

    public int? Accuracy
    {
        get => Model.Accuracy;
        set { if (value != Model.Accuracy) Edit(Model with { Accuracy = value }); }
    }

    public int Pp
    {
        get => Model.Pp;
        set { if (value != Model.Pp) Edit(Model with { Pp = value }); }
    }

    public int Priority
    {
        get => Model.Priority;
        set { if (value != Model.Priority) Edit(Model with { Priority = value }); }
    }

    public int CritStage
    {
        get => Model.CritStage;
        set { if (value != Model.CritStage) Edit(Model with { CritStage = value }); }
    }

    /// <summary>Types available to pick for this move (populates the reference combo).</summary>
    public IReadOnlyList<EntityId> AvailableTypes =>
        Session.All<TypeDef>().Select(t => t.Id).OrderBy(id => id.Slug).ToList();

    public IReadOnlyList<DamageClass> DamageClasses { get; } =
        [DamageClass.Physical, DamageClass.Special, DamageClass.Status];
}
