using Cgm.Core.Model;

namespace Cgm.Core.Validation;

public enum ValidationSeverity { Error, Warning, Info }

/// <summary>One problem found by a validation rule (MASTER_PLAN §4). Errors block export.
/// <paramref name="Field"/>, when present, is the camelCase path of the offending field
/// (e.g. <c>learnset[3].move</c>) so the Creator can navigate to it (CREATOR_APP_SPEC §10.8).</summary>
public sealed record ValidationIssue(
    string RuleId,
    ValidationSeverity Severity,
    EntityId? EntityId,
    string Message,
    string? FixHint = null,
    string? Field = null)
{
    public override string ToString()
    {
        string where = EntityId is { } id ? Field is { } f ? $" [{id}.{f}]" : $" [{id}]" : "";
        string hint = FixHint is { } h ? $" ({h})" : "";
        return $"{Severity}: {Message}{where}{hint} <{RuleId}>";
    }
}
