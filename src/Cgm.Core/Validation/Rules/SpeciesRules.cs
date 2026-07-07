using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>Species must use a known growth-rate curve key (GrowthRates).</summary>
public sealed class GrowthRateRule : IValidationRule
{
    public string Id => "growth-rate";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Species s in project.All<Species>())
            if (!GrowthRates.IsValid(s.GrowthRate))
                yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                    $"Unknown growth rate '{s.GrowthRate}'.",
                    "Use one of: " + string.Join(", ", GrowthRates.Keys));
    }
}

/// <summary>Species must have 1–2 distinct types.</summary>
public sealed class SpeciesTypesRule : IValidationRule
{
    public string Id => "species-types";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Species s in project.All<Species>())
        {
            if (s.Types.Count is < 1 or > 2)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                    $"Has {s.Types.Count} types; must be 1–2.");
            else if (s.Types.Count == 2 && s.Types[0] == s.Types[1])
                yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                    "Both types are the same.");
        }
    }
}

/// <summary>Species base stats and rates must be in valid ranges.</summary>
public sealed class SpeciesStatsRule : IValidationRule
{
    public string Id => "species-stats";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Species s in project.All<Species>())
        {
            foreach ((string name, int value) in Named(s.BaseStats))
                if (value is < 1 or > 255)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                        $"Base {name} is {value}; must be 1–255.");

            if (s.CatchRate is < 0 or > 255)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                    $"Catch rate is {s.CatchRate}; must be 0–255.");
            if (s.BaseHappiness is < 0 or > 255)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                    $"Base happiness is {s.BaseHappiness}; must be 0–255.");
            if (s.GenderFemaleEighths is < -1 or > 8)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                    $"genderFemaleEighths is {s.GenderFemaleEighths}; must be -1..8.");
        }
    }

    private static IEnumerable<(string, int)> Named(Stats s) =>
    [
        ("HP", s.Hp), ("Attack", s.Atk), ("Defense", s.Def),
        ("Sp.Atk", s.Spa), ("Sp.Def", s.Spd), ("Speed", s.Spe),
    ];
}

/// <summary>Learnset levels must be 1–100.</summary>
public sealed class LearnsetRule : IValidationRule
{
    public string Id => "learnset";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Species s in project.All<Species>())
            foreach (LearnsetEntry entry in s.Learnset)
                if (entry.Level is < 1 or > 100)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                        $"Learnset level {entry.Level} for '{entry.Move}' is out of 1–100.");
    }
}

/// <summary>Evolutions must not target themselves; level-up evolutions need a sane level.</summary>
public sealed class EvolutionRule : IValidationRule
{
    public string Id => "evolution";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Species s in project.All<Species>())
            foreach (Evolution evo in s.Evolutions)
            {
                if (evo.Target == s.Id)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                        "Evolves into itself.");
                if (evo.Trigger == EvolutionTrigger.LevelUp && evo.MinLevel is { } lvl && lvl is < 2 or > 100)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, s.Id,
                        $"Level-up evolution minLevel {lvl} is out of 2–100.");
            }
    }
}
