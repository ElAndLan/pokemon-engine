using Cgm.Core.Model;
using Cgm.Core.Validation.Rules;

namespace Cgm.Core.Validation;

/// <summary>The result of running rules over a project.</summary>
public sealed class ValidationReport
{
    public ValidationReport(IReadOnlyList<ValidationIssue> issues) => Issues = issues;

    public IReadOnlyList<ValidationIssue> Issues { get; }
    public int ErrorCount => Issues.Count(i => i.Severity == ValidationSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);
    public bool HasErrors => ErrorCount > 0;
}

/// <summary>Runs validation rules over a project. The default catalog is the export gate (Phase 12).</summary>
public static class Validator
{
    /// <summary>The registered rules, in a stable order.</summary>
    public static readonly IReadOnlyList<IValidationRule> DefaultRules =
    [
        new BrokenReferenceRule(),
        new StartMapExistsRule(),
        new StartPositionInBoundsRule(),
        new StarterPartyRule(),
        new GrowthRateRule(),
        new SpeciesTypesRule(),
        new SpeciesStatsRule(),
        new LearnsetRule(),
        new EvolutionRule(),
        new AbilityHookRule(),
        new HeldItemBattleEffectRule(),
        new FormRule(),
        new MoveRule(),
        new EncounterTableRule(),
        new TrainerPartyRule(),
        new WarpTargetRule(),
        new AnimationRule(),
        new SpriteUniquenessRule(),
        new MapEntityKeyRule(),
        new TriggerActionRule(),
        new PlayerStartRule(),
        new WarpLandingRule(),
    ];

    public static ValidationReport Run(Project project, IEnumerable<IValidationRule>? rules = null)
    {
        var issues = (rules ?? DefaultRules).SelectMany(r => r.Check(project)).ToList();
        return new ValidationReport(issues);
    }
}
