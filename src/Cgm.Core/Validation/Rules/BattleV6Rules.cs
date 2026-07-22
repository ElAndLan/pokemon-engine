using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

public sealed class AbilityHookRule : IValidationRule
{
    public string Id => "ability-hooks";

    private static readonly IReadOnlySet<AbilityHookPoint> SupportedHooks = new HashSet<AbilityHookPoint>
    {
        AbilityHookPoint.OnSwitchIn,
        AbilityHookPoint.OnModifyOutgoingDamage,
        AbilityHookPoint.OnModifyIncomingDamage,
        AbilityHookPoint.OnStatusAttempt,
        AbilityHookPoint.OnEndOfTurn,
        AbilityHookPoint.OnContactReceived,
        AbilityHookPoint.OnWeatherChange,
        AbilityHookPoint.OnTerrainChange,
        AbilityHookPoint.OnGroundedQuery,
        AbilityHookPoint.OnEscapeAttempt,
    };

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Ability ability in project.All<Ability>())
            foreach (AbilityHook hook in ability.Hooks)
            {
                if (!SupportedHooks.Contains(hook.Hook))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                        $"Ability hook '{hook.Hook}' is not a Phase 15 supported hook point.");

                foreach (Effect effect in hook.Effects)
                {
                    if (effect.Op == "terrainSummon"
                        && hook.Hook is not AbilityHookPoint.OnSwitchIn and not AbilityHookPoint.OnTerrainChange)
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            "Effect op 'terrainSummon' requires onSwitchIn or onTerrainChange.");
                    else if (hook.Hook == AbilityHookPoint.OnTerrainChange && effect.Op != "terrainSummon")
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            $"Ability hook 'onTerrainChange' does not support effect op '{effect.Op}'.");
                    else if (effect.Op == "groundedModify" && hook.Hook != AbilityHookPoint.OnGroundedQuery)
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            "Effect op 'groundedModify' requires onGroundedQuery.");
                    else if (hook.Hook == AbilityHookPoint.OnGroundedQuery && effect.Op != "groundedModify")
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            $"Ability hook 'onGroundedQuery' does not support effect op '{effect.Op}'.");
                    else if (effect.Op == "escapeBlock" && hook.Hook != AbilityHookPoint.OnEscapeAttempt)
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            "Effect op 'escapeBlock' requires onEscapeAttempt.");
                    else if (hook.Hook == AbilityHookPoint.OnEscapeAttempt && effect.Op != "escapeBlock")
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            $"Ability hook 'onEscapeAttempt' does not support effect op '{effect.Op}'.");
                    else if (effect.Op == "escapeBlock" && (effect.Chance is not null || effect.Params?.Count > 0))
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            "Effect op 'escapeBlock' accepts no chance or params.");
                    else if (effect.Op is "sideConditionBypass" or "protectionBypass"
                        && hook.Hook != AbilityHookPoint.OnModifyOutgoingDamage)
                        yield return new ValidationIssue(Id, ValidationSeverity.Error, ability.Id,
                            $"Effect op '{effect.Op}' requires onModifyOutgoingDamage.");
                }

                foreach (ValidationIssue issue in Phase15EffectRules.Check(
                    Id, ability.Id, hook.Effects, Phase15EffectRules.AbilityOps, "ability"))
                    yield return issue;
            }
    }
}

public sealed class HeldItemBattleEffectRule : IValidationRule
{
    public string Id => "held-item-battle-effects";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Item item in project.All<Item>())
        {
            if (item.BattleEffects.Count > 0 && !item.Holdable)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, item.Id,
                    "Item has held battle effects but holdable is false.");

            foreach (ValidationIssue issue in Phase15EffectRules.Check(
                Id, item.Id, item.BattleEffects, Phase15EffectRules.HeldItemOps, "held-item"))
                yield return issue;

            if (item.BattleEffects.Count(effect => effect.Op == "terrainSeed") > 1)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, item.Id,
                    "Held item supports only one terrainSeed effect because consumption is per op.");
        }
    }
}

public sealed class FormRule : IValidationRule
{
    public string Id => "forms";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Species species in project.All<Species>())
        {
            foreach (var group in species.Forms.GroupBy(f => f.FormId))
                if (string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, species.Id,
                        $"Form id '{group.Key}' is empty or duplicated.");

            foreach (Form form in species.Forms)
            {
                foreach (ValidationIssue issue in CheckShape(species.Id, form))
                    yield return issue;
                foreach (ValidationIssue issue in CheckActivation(species.Id, form, project))
                    yield return issue;
            }
        }
    }

    private IEnumerable<ValidationIssue> CheckShape(EntityId speciesId, Form form)
    {
        if (form.StatOverrides is { } stats)
            foreach ((string name, int value) in Named(stats))
                if (value is < 1 or > 255)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Form '{form.FormId}' base {name} is {value}; must be 1-255.");

        if (form.TypeOverrides is { Count: < 1 or > 2 })
            yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                $"Form '{form.FormId}' has {form.TypeOverrides.Count} type overrides; must be 1-2.");
        else if (form.TypeOverrides is { Count: 2 } types && types[0] == types[1])
            yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                $"Form '{form.FormId}' has duplicate type overrides.");

        if (form.Sprites.Front is null || form.Sprites.Back is null || form.Sprites.Icon is null)
            yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                $"Form '{form.FormId}' must define front, back, and icon sprites.");

        if (form.HpMultiplierPercent is <= 0)
            yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                $"Form '{form.FormId}' hpMultiplierPercent must be > 0.");
    }

    private IEnumerable<ValidationIssue> CheckActivation(EntityId speciesId, Form form, Project project)
    {
        switch (form.Activation)
        {
            case FormActivation.BattleTemporary:
                if (form.RequiredHeldItem is null || form.RequiredTrainerItem is null)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Battle-temporary form '{form.FormId}' requires held and trainer key items.");
                if (form.RequiredHeldItem is { } heldItem && project.Find<Item>(heldItem) is { Holdable: false })
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Battle-temporary form '{form.FormId}' requiredHeldItem must be holdable.");
                if (form.RequiredTrainerItem is { } trainerItem && project.Find<Item>(trainerItem) is { KeyItem: false })
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Battle-temporary form '{form.FormId}' requiredTrainerItem must be a key item.");
                if (form.Turns is not null || form.Condition is not null)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Battle-temporary form '{form.FormId}' must not set turns or condition.");
                break;

            case FormActivation.BattleTimed:
                if (form.Turns is not > 0)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Battle-timed form '{form.FormId}' requires turns > 0.");
                if (form.Condition is not null)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Battle-timed form '{form.FormId}' must not set condition.");
                break;

            case FormActivation.Condition:
                if (form.Condition is null || (string.IsNullOrWhiteSpace(form.Condition.Weather) && form.Condition.HeldItem is null))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Condition form '{form.FormId}' requires weather or heldItem condition.");
                if (form.Condition?.HeldItem is { } conditionHeldItem && project.Find<Item>(conditionHeldItem) is { Holdable: false })
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Condition form '{form.FormId}' heldItem condition must be holdable.");
                if (form.Turns is not null || form.RequiredTrainerItem is not null)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, speciesId,
                        $"Condition form '{form.FormId}' must not set turns or requiredTrainerItem.");
                break;
        }
    }

    private static IEnumerable<(string, int)> Named(Stats s) =>
    [
        ("HP", s.Hp), ("Attack", s.Atk), ("Defense", s.Def),
        ("Sp.Atk", s.Spa), ("Sp.Def", s.Spd), ("Speed", s.Spe),
    ];
}

internal static class Phase15EffectRules
{
    public static readonly IReadOnlySet<string> AbilityOps = new HashSet<string>
    {
        "statModify", "typeDamageModify", "statusImmunity", "weatherSummon", "terrainSummon",
        "contactChanceEffect", "residualHeal", "residualDamage", "groundedModify",
        "sideConditionBypass", "protectionBypass", "itemMutationGuard", "abilityMutationGuard",
        "escapeBlock",
    };

    public static readonly IReadOnlySet<string> HeldItemOps = new HashSet<string>
    {
        "thresholdHeal", "statusCure", "typeDamageBoost", "choiceLock",
        "residualHeal", "surviveFromFull", "weatherDurationExtend", "terrainDurationExtend", "groundedModify",
        "terrainSeed",
        "sideConditionDurationExtend", "itemMutationGuard",
    };

    public static IEnumerable<ValidationIssue> Check(
        string ruleId,
        EntityId owner,
        IEnumerable<Effect> effects,
        IReadOnlySet<string> allowedOps,
        string label)
    {
        foreach (Effect effect in effects)
        {
            if (!allowedOps.Contains(effect.Op))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' is not a Phase 15 {label} op.");

            if (effect.Chance is < 1 or > 100)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' chance {effect.Chance} must be 1-100.");

            foreach (ValidationIssue issue in CheckNumericParams(ruleId, owner, effect))
                yield return issue;
            foreach (ValidationIssue issue in CheckRequiredParams(ruleId, owner, effect))
                yield return issue;
        }
    }

    private static IEnumerable<ValidationIssue> CheckRequiredParams(string ruleId, EntityId owner, Effect effect)
    {
        if (effect.Op == "weatherSummon" && !HasString(effect, "weather"))
            yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                "Effect op 'weatherSummon' requires string param 'weather'.");

        if (effect.Op == "terrainSummon")
        {
            if (!HasString(effect, "terrain"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'terrainSummon' requires string param 'terrain'.");
            else if (!Enum.GetNames<Terrain>().Any(name =>
                    string.Equals(name, Str(effect, "terrain"), StringComparison.OrdinalIgnoreCase))
                || string.Equals(Str(effect, "terrain"), nameof(Terrain.None), StringComparison.OrdinalIgnoreCase))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'terrainSummon' has unknown terrain '{Str(effect, "terrain")}'.");
            if (effect.Params?.ContainsKey("duration") == true && !HasNumber(effect, "duration"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'terrainSummon' param 'duration' must be an integer.");
            foreach (string key in effect.Params?.Keys.Where(key => key is not "terrain" and not "duration") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'terrainSummon' has unknown param '{key}'.");
        }

        if (effect.Op is "statusImmunity" or "statusCure")
        {
            if (!HasString(effect, "status"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' requires string param 'status'.");
            else if (!Enum.TryParse(Str(effect, "status"), ignoreCase: true, out PersistentStatus _))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' has unknown status '{Str(effect, "status")}'.");
        }

        if (effect.Op is "typeDamageModify" or "typeDamageBoost")
        {
            if (!HasString(effect, "type"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' requires string param 'type'.");
            if (!HasNumber(effect, "multiplierPercent"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' requires numeric param 'multiplierPercent'.");
        }

        if (effect.Op == "statModify")
        {
            if (!HasString(effect, "stat"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'statModify' requires string param 'stat'.");
            else if (!IsDamageStat(Str(effect, "stat")))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'statModify' has unknown stat '{Str(effect, "stat")}'.");
            if (!HasNumber(effect, "multiplierPercent") && !HasNumber(effect, "add"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'statModify' requires numeric param 'multiplierPercent' or 'add'.");
        }

        if (effect.Op == "contactChanceEffect")
        {
            bool hasStatus = HasString(effect, "status");
            bool hasStat = HasString(effect, "stat");
            bool hasDelta = HasNumber(effect, "delta");
            bool hasDamage = HasNumber(effect, "damage");

            if (!hasStatus && !(hasStat && hasDelta) && !hasDamage)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'contactChanceEffect' requires 'status', both 'stat' and 'delta', or 'damage'.");
            if ((hasStatus ? 1 : 0) + (hasStat || hasDelta ? 1 : 0) + (hasDamage ? 1 : 0) > 1)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'contactChanceEffect' requires exactly one effect payload.");
            if (hasStatus && !Enum.TryParse(Str(effect, "status"), ignoreCase: true, out PersistentStatus _))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'contactChanceEffect' has unknown status '{Str(effect, "status")}'.");
            if (hasStat && !IsBattleStat(Str(effect, "stat")))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'contactChanceEffect' has unknown stat '{Str(effect, "stat")}'.");
            if (hasDelta && Int(effect, "delta") == 0)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'contactChanceEffect' param 'delta' must not be 0.");
            if (hasDamage && Int(effect, "damage") <= 0)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'contactChanceEffect' param 'damage' must be positive.");
        }

        if (effect.Op == "choiceLock")
        {
            if (!HasString(effect, "damageClass"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'choiceLock' requires string param 'damageClass'.");
            else if (!Enum.TryParse(Str(effect, "damageClass"), ignoreCase: true, out DamageClass damageClass)
                || damageClass == DamageClass.Status)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'choiceLock' has unknown damageClass '{Str(effect, "damageClass")}'.");
            if (!HasNumber(effect, "multiplierPercent"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'choiceLock' requires numeric param 'multiplierPercent'.");
        }

        if (effect.Op == "thresholdHeal")
        {
            bool hasHealAmount = HasNumber(effect, "healAmount");
            bool hasHealFraction = HasNumber(effect, "healFractionPercent");
            if (!HasNumber(effect, "thresholdPercent"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'thresholdHeal' requires numeric param 'thresholdPercent'.");
            if (!hasHealAmount && !hasHealFraction)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'thresholdHeal' requires numeric param 'healAmount' or 'healFractionPercent'.");
            if (hasHealAmount && hasHealFraction)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'thresholdHeal' requires only one of 'healAmount' or 'healFractionPercent'.");
        }

        if (effect.Op is "residualHeal" or "residualDamage")
        {
            if (!HasNumber(effect, "num"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' requires numeric param 'num'.");
            if (!HasNumber(effect, "den"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' requires numeric param 'den'.");
        }

        if (effect.Op == "surviveFromFull" && effect.Params is { Count: > 0 })
            yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                "Effect op 'surviveFromFull' does not take params.");

        if (effect.Op == "weatherDurationExtend" && !HasNumber(effect, "turns"))
            yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                "Effect op 'weatherDurationExtend' requires numeric param 'turns'.");

        if (effect.Op == "terrainDurationExtend" && !HasNumber(effect, "turns"))
            yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                "Effect op 'terrainDurationExtend' requires numeric param 'turns'.");
        if (effect.Op == "terrainDurationExtend")
            foreach (string key in effect.Params?.Keys.Where(key => key != "turns") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'terrainDurationExtend' has unknown param '{key}'.");

        if (effect.Op == "sideConditionDurationExtend")
        {
            if (!HasString(effect, "tag") || !string.Equals(Str(effect, "tag"), "screen", StringComparison.Ordinal))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'sideConditionDurationExtend' requires tag 'screen'.");
            if (!HasNumber(effect, "turns"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'sideConditionDurationExtend' requires numeric param 'turns'.");
            foreach (string key in effect.Params?.Keys.Where(key => key is not "tag" and not "turns") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'sideConditionDurationExtend' has unknown param '{key}'.");
            if (effect.Chance is not null)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'sideConditionDurationExtend' does not support chance.");
        }

        if (effect.Op == "sideConditionBypass")
        {
            if (!HasString(effect, "tag") || Str(effect, "tag") is not ("screen" or "status_guard" or "stage_guard" or "side_protection"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'sideConditionBypass' requires tag 'screen', 'status_guard', 'stage_guard', or 'side_protection'.");
            foreach (string key in effect.Params?.Keys.Where(key => key != "tag") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'sideConditionBypass' has unknown param '{key}'.");
            if (effect.Chance is not null)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'sideConditionBypass' does not support chance.");
        }

        if (effect.Op == "protectionBypass")
        {
            foreach (string key in effect.Params?.Keys ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'protectionBypass' has unknown param '{key}'.");
            if (effect.Chance is not null)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'protectionBypass' does not support chance.");
        }

        if (effect.Op == "terrainSeed")
        {
            if (!HasString(effect, "terrain"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'terrainSeed' requires string param 'terrain'.");
            else if (!Enum.GetNames<Terrain>().Any(name =>
                    !string.Equals(name, nameof(Terrain.None), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(name, Str(effect, "terrain"), StringComparison.OrdinalIgnoreCase)))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'terrainSeed' has unknown terrain '{Str(effect, "terrain")}'.");
            if (!HasString(effect, "stat"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'terrainSeed' requires string param 'stat'.");
            else if (!new[] { "def", "spd" }.Contains(Str(effect, "stat"), StringComparer.OrdinalIgnoreCase))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'terrainSeed' has unknown stat '{Str(effect, "stat")}'.");
            foreach (string key in effect.Params?.Keys.Where(key => key is not "terrain" and not "stat") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'terrainSeed' has unknown param '{key}'.");
            if (effect.Chance is not null)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'terrainSeed' does not support chance.");
        }

        if (effect.Op == "groundedModify")
        {
            if (!HasString(effect, "state")
                || Str(effect, "state") is not ("grounded" or "airborne"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'groundedModify' requires state 'grounded' or 'airborne'.");
            foreach (string key in effect.Params?.Keys.Where(key => key != "state") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'groundedModify' has unknown param '{key}'.");
            if (effect.Chance is not null)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'groundedModify' does not support chance.");
        }

        if (effect.Op == "itemMutationGuard")
        {
            if (!HasString(effect, "operations"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'itemMutationGuard' requires string param 'operations'.");
            else
            {
                string[] operations = Str(effect, "operations").Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (operations.Length == 0 || operations.Distinct(StringComparer.Ordinal).Count() != operations.Length
                    || operations.Any(value => !Enum.TryParse<BattleItemOperation>(value, true, out _)))
                    yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                        "Effect op 'itemMutationGuard' requires unique held-item mutation operations.");
            }
            foreach (string key in effect.Params?.Keys.Where(key => key != "operations") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'itemMutationGuard' has unknown param '{key}'.");
            if (effect.Chance is not null)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'itemMutationGuard' does not support chance.");
        }

        if (effect.Op == "abilityMutationGuard")
        {
            if (!HasString(effect, "operations"))
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'abilityMutationGuard' requires string param 'operations'.");
            else
            {
                string[] operations = Str(effect, "operations").Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (operations.Length == 0 || operations.Distinct(StringComparer.Ordinal).Count() != operations.Length
                    || operations.Any(value => !Enum.TryParse<BattleAbilityOperation>(value, true, out _)))
                    yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                        "Effect op 'abilityMutationGuard' requires unique ability mutation operations.");
            }
            foreach (string key in effect.Params?.Keys.Where(key => key != "operations") ?? [])
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op 'abilityMutationGuard' has unknown param '{key}'.");
            if (effect.Chance is not null)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    "Effect op 'abilityMutationGuard' does not support chance.");
        }
    }

    private static IEnumerable<ValidationIssue> CheckNumericParams(string ruleId, EntityId owner, Effect effect)
    {
        if (effect.Params is null)
            yield break;

        foreach ((string key, JsonElement value) in effect.Params)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int n))
                continue;

            bool invalid = key switch
            {
                "den" => n == 0,
                "thresholdPercent" or "chance" => n is < 1 or > 100,
                "num" or "amount" or "duration" or "turns" or "multiplierPercent" or "hpMultiplierPercent"
                    or "healAmount" or "healFractionPercent" => n <= 0,
                _ => false,
            };

            if (invalid)
                yield return new ValidationIssue(ruleId, ValidationSeverity.Error, owner,
                    $"Effect op '{effect.Op}' param '{key}' has invalid value {n}.");
        }
    }

    private static bool HasString(Effect effect, string key) =>
        effect.Params is not null
        && effect.Params.TryGetValue(key, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasNumber(Effect effect, string key) =>
        effect.Params is not null
        && effect.Params.TryGetValue(key, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out _);

    private static bool IsDamageStat(string value) =>
        Enum.TryParse(value, ignoreCase: true, out StatKind stat)
        && stat is StatKind.Atk or StatKind.Def or StatKind.Spa or StatKind.Spd;

    private static bool IsBattleStat(string value) =>
        Enum.TryParse(value, ignoreCase: true, out StatKind stat)
        && stat is StatKind.Atk or StatKind.Def or StatKind.Spa or StatKind.Spd or StatKind.Spe;

    private static int? Int(Effect effect, string key) =>
        effect.Params is not null
        && effect.Params.TryGetValue(key, out JsonElement value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out int n)
            ? n
            : null;

    private static string Str(Effect effect, string key) =>
        effect.Params is not null && effect.Params.TryGetValue(key, out JsonElement value)
            ? value.GetString() ?? ""
            : "";
}
