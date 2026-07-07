using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>Move fields must be self-consistent: status moves deal no damage, damaging moves have
/// power, accuracy/pp/priority in range.</summary>
public sealed class MoveRule : IValidationRule
{
    public string Id => "move";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Move m in project.All<Move>())
        {
            bool damaging = m.DamageClass != DamageClass.Status;

            if (damaging && m.Power is not > 0)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, m.Id,
                    "Damaging move must have power > 0.");
            if (!damaging && m.Power is not null)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, m.Id,
                    "Status move must have null power.");

            if (m.Accuracy is { } acc && acc is < 1 or > 100)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, m.Id,
                    $"Accuracy {acc} must be 1–100 (or null for always-hit).");
            if (m.Pp <= 0)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, m.Id,
                    $"PP is {m.Pp}; must be > 0.");
            if (m.Priority is < -7 or > 7)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, m.Id,
                    $"Priority {m.Priority} must be -7..7.");
        }
    }
}
