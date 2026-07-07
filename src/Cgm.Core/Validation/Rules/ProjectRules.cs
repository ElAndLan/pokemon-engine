using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>The project's start map must be set and refer to an actual map.</summary>
public sealed class StartMapExistsRule : IValidationRule
{
    public string Id => "start-map-exists";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        EntityId? start = project.Settings.StartMap;
        if (start is null)
        {
            yield return new ValidationIssue(Id, ValidationSeverity.Error, project.Settings.Id,
                "No start map is set.", "Set project startMap.");
            yield break;
        }

        if (project.Find<Map>(start.Value) is null)
            yield return new ValidationIssue(Id, ValidationSeverity.Error, project.Settings.Id,
                $"Start map '{start}' is not a map.", "Point startMap at a map: entity.");
    }
}

/// <summary>The starter party must have 1–6 members, each an existing species.</summary>
public sealed class StarterPartyRule : IValidationRule
{
    public string Id => "starter-party";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        IReadOnlyList<EntityId> party = project.Settings.StarterParty;
        if (party.Count is < 1 or > 6)
            yield return new ValidationIssue(Id, ValidationSeverity.Error, project.Settings.Id,
                $"Starter party has {party.Count} members; must be 1–6.");

        foreach (EntityId id in party)
            if (project.Find<Species>(id) is null)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, project.Settings.Id,
                    $"Starter '{id}' is not a species.");
    }
}
