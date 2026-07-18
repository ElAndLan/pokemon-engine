using Cgm.Core.Battle;
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

            bool formulaPower = m.Effects.Any(effect => effect.Op is "fixedDamage" or "ohko" or "counterDamage" or "hpFraction" or "hpEqualize"
                or "hpBandPower" or "statusCountPower" or "speedRatioPower" or "metricBandPower" or "metricRatioPower"
                or "partyCountPower" or "friendshipPower" or "ppPower" or "positiveStagePower"
                or "itemDataPower" or "randomTablePower"
                || effect.Op == "hpRatioPower" && effect.Params?.ContainsKey("scale") == true);
            if (damaging && m.Power is not > 0 && !formulaPower)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, m.Id,
                    "Damaging move must have power > 0 or a replacement base-power formula.");
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
            if (!Enum.IsDefined(m.Target))
                yield return new ValidationIssue(Id, ValidationSeverity.Error, m.Id,
                    $"Target {m.Target} is not a known move target.");

            if (CheckEffects(m, project) is { } issue)
                yield return issue;
        }
    }

    private ValidationIssue? CheckEffects(Move move, Project project)
    {
        try
        {
            BattleMove compiled = MoveCompiler.ToBattleMove(move);
            EntityId? missingType = compiled.SecondaryEffects
                .Where(effect => effect is WeatherMoveEffect or TerrainMoveEffect)
                .SelectMany(effect => effect switch
                {
                    WeatherMoveEffect weather => weather.TypeOverrides.Values,
                    TerrainMoveEffect terrain => terrain.TypeOverrides.Values,
                    _ => [],
                })
                .FirstOrDefault(type => project.Find<TypeDef>(type) is null);
            if (missingType is { } type && type != default)
                return new ValidationIssue(Id, ValidationSeverity.Error, move.Id,
                    $"A field-sensitive move references '{type}', which does not exist.");
            EntityId? missingAbility = compiled.SecondaryEffects.OfType<AbilityMutationEffect>()
                .Select(effect => effect.Ability).FirstOrDefault(ability => ability is { } id
                    && project.Find<Ability>(id) is null);
            if (missingAbility is { } ability)
                return new ValidationIssue(Id, ValidationSeverity.Error, move.Id,
                    $"An ability mutation references '{ability}', which does not exist.");
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return new ValidationIssue(Id, ValidationSeverity.Error, move.Id, ex.Message);
        }
    }
}
