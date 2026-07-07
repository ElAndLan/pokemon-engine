using Cgm.Core.Model;

namespace Cgm.Core.Validation;

public enum ValidationSeverity { Error, Warning, Info }

/// <summary>One problem found by a validation rule (MASTER_PLAN §4). Errors block export.</summary>
public sealed record ValidationIssue(
    string RuleId,
    ValidationSeverity Severity,
    EntityId? EntityId,
    string Message,
    string? FixHint = null)
{
    public override string ToString()
    {
        string where = EntityId is { } id ? $" [{id}]" : "";
        string hint = FixHint is { } h ? $" ({h})" : "";
        return $"{Severity}: {Message}{where}{hint} <{RuleId}>";
    }
}
