using Cgm.Core.Model;

namespace Cgm.Core.Validation;

/// <summary>
/// A stateless check over a whole <see cref="Project"/>, yielding zero or more issues
/// (CODING_STANDARDS.md). Rules never mutate; they only report.
/// </summary>
public interface IValidationRule
{
    string Id { get; }
    IEnumerable<ValidationIssue> Check(Project project);
}
