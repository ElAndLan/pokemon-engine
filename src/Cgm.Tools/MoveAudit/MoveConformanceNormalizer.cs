using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;

namespace Cgm.Tools.MoveAudit;

public sealed record MoveConformanceDecisionCatalog(
    int FormatVersion,
    IReadOnlyList<string> EvidenceSources,
    IReadOnlyList<MoveConformanceDecision> Entries);

public sealed record MoveConformanceDecision(
    string ReferenceKey,
    bool MakesContact,
    IReadOnlyList<Effect>? AdditionalEffects = null);

public sealed record NormalizedMoveMechanics(
    EntityId Type,
    DamageClass DamageClass,
    int? Power,
    int? Accuracy,
    int Pp,
    int Priority,
    int CritStage,
    bool MakesContact,
    MoveTarget Target,
    IReadOnlyList<Effect> Effects)
{
    public Move ToMove(string referenceKey) => new()
    {
        Id = EntityId.Parse($"move:{referenceKey.Replace('-', '_')}"),
        Name = referenceKey,
        Type = Type,
        DamageClass = DamageClass,
        Power = Power,
        Accuracy = Accuracy,
        Pp = Pp,
        Priority = Priority,
        CritStage = CritStage,
        MakesContact = MakesContact,
        Target = Target,
        Effects = Effects,
    };
}

public sealed record MoveConformanceRecord(
    string ReferenceKey,
    string SourceFileHash,
    string PayloadContentHash,
    string RequiredRuleset,
    string RequiredTopology,
    IReadOnlyList<string> MechanicFamilies,
    NormalizedMoveMechanics Mechanics,
    string NormalizedDefinitionHash,
    IReadOnlyList<string> TestIds);

public sealed record MoveConformanceCatalog(
    int FormatVersion,
    IReadOnlyList<MoveConformanceRecord> Entries);

public static class MoveConformanceNormalizer
{
    public const int FormatVersion = 1;

    public static MoveConformanceDecisionCatalog ReadDecisions(string path) =>
        CgmJson.Deserialize<MoveConformanceDecisionCatalog>(File.ReadAllText(path));

    public static MoveConformanceCatalog Build(string corpusFolder, MoveConformanceDecisionCatalog decisions)
    {
        if (decisions.FormatVersion != FormatVersion)
            throw new InvalidDataException($"Unsupported move conformance decision format {decisions.FormatVersion}.");
        if (decisions.EvidenceSources.Count == 0 || decisions.EvidenceSources.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Move conformance decisions require non-empty evidence sources.");

        string? duplicate = decisions.Entries.GroupBy(e => e.ReferenceKey, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicate is not null)
            throw new InvalidDataException($"Move conformance decisions contain duplicate key '{duplicate}'.");

        var sourceFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string sourceFile in Directory.EnumerateFiles(corpusFolder, "*.json", SearchOption.TopDirectoryOnly))
        {
            string referenceKey = ReadReferenceKey(sourceFile);
            if (!sourceFiles.TryAdd(referenceKey, sourceFile))
                throw new InvalidDataException($"Move corpus contains duplicate reference key '{referenceKey}'.");
        }
        List<MoveConformanceRecord> records = [];
        foreach (MoveConformanceDecision decision in decisions.Entries.OrderBy(e => e.ReferenceKey, StringComparer.Ordinal))
        {
            if (!sourceFiles.TryGetValue(decision.ReferenceKey, out string? path))
                throw new InvalidDataException($"Move conformance decision '{decision.ReferenceKey}' is not in the corpus.");
            records.Add(Normalize(path, decision));
        }

        return new MoveConformanceCatalog(FormatVersion, records);
    }

    public static string Serialize(MoveConformanceCatalog catalog) => CgmJson.Serialize(catalog);

    public static void Write(MoveConformanceCatalog catalog, string outputPath)
    {
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, Serialize(catalog).ReplaceLineEndings("\n"));
    }

    private static MoveConformanceRecord Normalize(string path, MoveConformanceDecision decision)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement payload = RequiredObject(root, "payload", path);
        int sourceId = RequiredInt(payload, "id", path);
        string referenceKey = $"move-{sourceId:D4}";
        if (referenceKey != decision.ReferenceKey)
            throw Invalid(path, $"decision key '{decision.ReferenceKey}' does not match '{referenceKey}'");

        DamageClass damageClass = ParseDamageClass(ReferenceName(payload, "damage_class", path), path);
        int? power = OptionalInt(payload, "power");
        if (damageClass == DamageClass.Status && power == 0)
            power = null;
        int? accuracy = OptionalInt(payload, "accuracy");
        int pp = RequiredInt(payload, "pp", path);
        int priority = RequiredInt(payload, "priority", path);
        MoveTarget target = ParseTarget(ReferenceName(payload, "target", path), path);
        EntityId type = EntityId.Parse($"type:reference_{ReferenceId(payload, "type", path):D2}");
        JsonElement? meta = OptionalObject(payload, "meta");

        bool replacementPower = decision.AdditionalEffects?.Any(effect => effect.Op is "hpBandPower" or "statusCountPower" or "hpFraction" or "hpEqualize"
            or "speedRatioPower" or "metricBandPower" or "metricRatioPower" or "partyCountPower"
            or "friendshipPower" or "ppPower" or "positiveStagePower" or "itemDataPower" or "randomTablePower"
            or "counterDamage" or "revengeDamage" or "bide"
            || effect.Op == "hpRatioPower" && effect.Params?.ContainsKey("scale") == true) == true;
        if (damageClass == DamageClass.Status ? power is not null : power is not > 0 && !replacementPower)
            throw Invalid(path, "power is inconsistent with damage_class");
        if (accuracy is not null && accuracy is < 1 or > 100)
            throw Invalid(path, "accuracy must be 1-100 or null");
        if (pp <= 0)
            throw Invalid(path, "pp must be positive");
        List<Effect> effects = [];
        if (damageClass != DamageClass.Status)
            effects.Add(new Effect { Op = "damage" });

        if (meta is { } metadata)
        {
            AddAilment(metadata, effects, path);
            AddStatChanges(payload, metadata, target, effects, path);
            AddFlinch(metadata, effects);
            AddDrainOrRecoil(metadata, effects);
            AddHealing(metadata, effects, decision);
            AddMultiHit(metadata, effects, path);
        }

        if (decision.AdditionalEffects is { Count: > 0 })
            effects.AddRange(decision.AdditionalEffects);

        var mechanics = new NormalizedMoveMechanics(
            type,
            damageClass,
            power,
            accuracy,
            pp,
            priority,
            meta is { } critMeta ? OptionalInt(critMeta, "crit_rate") ?? 0 : 0,
            decision.MakesContact,
            target,
            effects);

        MoveCompiler.ToBattleMove(mechanics.ToMove(referenceKey));
        string definitionHash = Hash(Encoding.UTF8.GetBytes(CgmJson.Serialize(mechanics)));
        bool requiresDoubles = RequiresDoubles(target)
            || mechanics.Effects.Any(effect => effect.Op == "pairedAction");
        string topology = requiresDoubles ? "doubles" : "singles-or-doubles";
        var testIds = new List<string>();
        if (RequiresDoubles(target))
            testIds.Add($"TargetTopologyConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => IsHpStatusFormula(effect.Op)))
            testIds.Add($"HpStatusFormulaConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => IsPhysicalMetricFormula(effect.Op)))
            testIds.Add($"PhysicalMetricFormulaConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => IsActionHistoryFormula(effect.Op)))
            testIds.Add($"ActionHistoryFormulaConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => IsPartyResourceFormula(effect.Op)))
            testIds.Add($"PartyResourceFormulaConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => IsActionGate(effect.Op)))
            testIds.Add($"ActionGateConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => IsCharge(effect.Op)))
            testIds.Add($"ChargeConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => IsDelayedAction(effect.Op)))
            testIds.Add($"DelayedActionConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op is "multiTurnLock" or "multiTurnPowerBoost"))
            testIds.Add($"MultiTurnLockConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "actionFilter"))
            testIds.Add($"ActionFilterConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op is "callMove" or "turnOrderIntent" or "pairedAction"))
            testIds.Add($"MoveReferenceConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op is "itemRequire" or "itemMutation"))
            testIds.Add($"ItemMutationConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "abilityMutation"))
            testIds.Add($"AbilityMutationConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "typeMutation"))
            testIds.Add($"TypeMutationConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op is "statStageSteal" or "statStageRandomRaise"
            or "derivedStatSwap" or "derivedStatSplit"))
            testIds.Add($"StatMutationConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op is "decoy" or "transform" or "replaceMove"))
            testIds.Add($"SnapshotConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op is "batonPass" or "pivotSwitch"))
            testIds.Add($"SwitchIntentConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op is "counterDamage" or "revengeDamage" or "bide"))
            testIds.Add($"DamageMemoryConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "heal"))
            testIds.Add($"HealingConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "drain"))
            testIds.Add($"DrainConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "recoil"))
            testIds.Add($"RecoilConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "ailment"))
            testIds.Add($"SecondaryAilmentConformanceTests.Certified({referenceKey})");
        if (mechanics.Effects.Any(effect => effect.Op == "flinch"))
            testIds.Add($"SecondaryFlinchConformanceTests.Certified({referenceKey})");
        if (testIds.Count == 0)
            throw Invalid(path, "decision has no registered conformance family");
        return new MoveConformanceRecord(
            referenceKey,
            Hash(bytes),
            RequiredString(root, "content_hash", path).ToLowerInvariant(),
            "modern_reference",
            topology,
            Families(mechanics),
            mechanics,
            definitionHash,
            testIds);
    }

    private static void AddAilment(JsonElement meta, List<Effect> effects, string path)
    {
        string ailment = ReferenceName(meta, "ailment", path);
        if (ailment is "none" or "yawn" or "disable" or "infatuation" or "torment"
            or "embargo" or "heal-block" or "silence")
            return;
        string normalized = ailment switch
        {
            "burn" or "poison" or "paralysis" or "sleep" or "freeze" or "confusion" => ailment,
            "bad-poison" => "toxic",
            _ => throw Invalid(path, $"ailment '{ailment}' is not implemented"),
        };
        int chance = OptionalInt(meta, "ailment_chance") is > 0 and var value ? value : 100;
        effects.Add(new Effect { Op = "ailment", Chance = chance, Params = Params(("ailment", normalized)) });
    }

    private static void AddStatChanges(JsonElement payload, JsonElement meta, MoveTarget target, List<Effect> effects, string path)
    {
        if (!payload.TryGetProperty("stat_changes", out JsonElement changes) || changes.ValueKind != JsonValueKind.Array)
            return;
        string category = ReferenceName(meta, "category", path);
        bool onSelf = category == "damage+raise" || target == MoveTarget.User;
        int chance = OptionalInt(meta, "stat_chance") is > 0 and var value ? value : 100;
        foreach (JsonElement change in changes.EnumerateArray())
        {
            string stat = ReferenceName(change, "stat", path) switch
            {
                "attack" => "atk",
                "defense" => "def",
                "special-attack" => "spa",
                "special-defense" => "spd",
                "speed" => "spe",
                "accuracy" => "accuracy",
                "evasion" => "evasion",
                var unknown => throw Invalid(path, $"stat '{unknown}' is not implemented"),
            };
            effects.Add(new Effect
            {
                Op = "statStage",
                Chance = chance,
                Params = Params(("stat", stat), ("delta", RequiredInt(change, "change", path)), ("onSelf", onSelf)),
            });
        }
    }

    private static void AddFlinch(JsonElement meta, List<Effect> effects)
    {
        if (OptionalInt(meta, "flinch_chance") is > 0 and var chance)
            effects.Add(new Effect { Op = "flinch", Chance = chance });
    }

    private static void AddDrainOrRecoil(JsonElement meta, List<Effect> effects)
    {
        if (OptionalInt(meta, "drain") is not { } drain || drain == 0)
            return;
        effects.Add(new Effect
        {
            Op = drain > 0 ? "drain" : "recoil",
            Params = Params(("num", Math.Abs(drain)), ("den", 100)),
        });
    }

    private static void AddHealing(JsonElement meta, List<Effect> effects, MoveConformanceDecision decision)
    {
        // A decision that authors its own heal (e.g. a weather table or a non-self recipient) replaces
        // the flat self-heal this would otherwise derive from meta.healing.
        if (decision.AdditionalEffects?.Any(effect => effect.Op == "heal") == true)
            return;
        if (OptionalInt(meta, "healing") is > 0 and var healing)
            effects.Add(new Effect { Op = "heal", Params = Params(("num", healing), ("den", 100)) });
    }

    private static void AddMultiHit(JsonElement meta, List<Effect> effects, string path)
    {
        int? min = OptionalInt(meta, "min_hits");
        int? max = OptionalInt(meta, "max_hits");
        if (min is null && max is null)
            return;
        if (min is not > 0 || max is not > 0)
            throw Invalid(path, "multi-hit bounds must both be positive");
        effects.Add(new Effect { Op = "multiHit", Params = Params(("min", min.Value), ("max", max.Value)) });
    }

    private static IReadOnlyList<string> Families(NormalizedMoveMechanics mechanics)
    {
        var families = new SortedSet<string>(StringComparer.Ordinal) { "targetTopology" };
        if (mechanics.Power is not null) families.Add("standardDamage");
        foreach (Effect effect in mechanics.Effects)
            if (effect.Op != "damage") families.Add(effect.Op);
        return families.ToList();
    }

    private static bool RequiresDoubles(MoveTarget target) => target is
        MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon or MoveTarget.AllAllies or MoveTarget.AllPokemon
        or MoveTarget.Ally or MoveTarget.RandomOpponent or MoveTarget.SelectedPokemonMeFirst
        or MoveTarget.UserAndAllies or MoveTarget.UserOrAlly;

    private static bool IsHpStatusFormula(string op) => op is "targetHpThresholdPower" or "hpRatioPower"
        or "hpBandPower" or "statusPower" or "statusCountPower" or "hpFraction" or "hpEqualize"
        or "cannotKo" or "statusChance";

    private static bool IsPhysicalMetricFormula(string op) =>
        op is "speedRatioPower" or "metricBandPower" or "metricRatioPower";

    private static bool IsActionHistoryFormula(string op) => op is "consecutivePower" or "historyPower";

    private static bool IsPartyResourceFormula(string op) => op is "partyCountPower" or "friendshipPower"
        or "ppPower" or "positiveStagePower" or "itemDataPower" or "randomTablePower";

    private static bool IsActionGate(string op) => op is "moveGate" or "queueActionGate" or "recharge";

    private static bool IsCharge(string op) => op is "chargeTurn" or "chargeStartStat"
        or "semiInvulnerableHit";

    private static bool IsDelayedAction(string op) => op is "delayedDamage" or "delayedHeal"
        or "delayedStatus" or "replacementRestore";

    private static MoveTarget ParseTarget(string value, string path) => value switch
    {
        "selected-pokemon" => MoveTarget.Selected,
        "user" => MoveTarget.User,
        "all-opponents" => MoveTarget.AllOpponents,
        "all-other-pokemon" => MoveTarget.AllOtherPokemon,
        "users-field" => MoveTarget.UsersField,
        "entire-field" => MoveTarget.EntireField,
        "all-allies" => MoveTarget.AllAllies,
        "all-pokemon" => MoveTarget.AllPokemon,
        "ally" => MoveTarget.Ally,
        "opponents-field" => MoveTarget.OpponentsField,
        "random-opponent" => MoveTarget.RandomOpponent,
        "selected-pokemon-me-first" => MoveTarget.SelectedPokemonMeFirst,
        "specific-move" => MoveTarget.SpecificMove,
        "user-and-allies" => MoveTarget.UserAndAllies,
        "user-or-ally" => MoveTarget.UserOrAlly,
        "fainting-pokemon" => MoveTarget.FaintingPokemon,
        _ => throw Invalid(path, $"target '{value}' is not implemented"),
    };

    private static DamageClass ParseDamageClass(string value, string path) => value switch
    {
        "physical" => DamageClass.Physical,
        "special" => DamageClass.Special,
        "status" => DamageClass.Status,
        _ => throw Invalid(path, $"damage class '{value}' is not implemented"),
    };

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(v => v.Key, v => JsonSerializer.SerializeToElement(v.Value, CgmJson.Options), StringComparer.Ordinal);

    private static string ReadReferenceKey(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return $"move-{RequiredInt(RequiredObject(document.RootElement, "payload", path), "id", path):D4}";
    }

    private static string ReferenceName(JsonElement owner, string property, string path) =>
        RequiredString(RequiredObject(owner, property, path), "name", path);

    private static int ReferenceId(JsonElement owner, string property, string path)
    {
        string url = RequiredString(RequiredObject(owner, property, path), "url", path).TrimEnd('/');
        return int.TryParse(url[(url.LastIndexOf('/') + 1)..], out int id) && id > 0
            ? id
            : throw Invalid(path, $"{property}.url must end in a positive numeric id");
    }

    private static JsonElement RequiredObject(JsonElement owner, string property, string path) =>
        owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Object
            ? value
            : throw Invalid(path, $"{property} must be an object");

    private static JsonElement? OptionalObject(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static string RequiredString(JsonElement owner, string property, string path) =>
        owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? throw Invalid(path, $"{property} must not be null")
            : throw Invalid(path, $"{property} must be a string");

    private static int RequiredInt(JsonElement owner, string property, string path) =>
        owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : throw Invalid(path, $"{property} must be an integer");

    private static int? OptionalInt(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid move conformance source '{Path.GetFileName(path)}': {message}.");

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
