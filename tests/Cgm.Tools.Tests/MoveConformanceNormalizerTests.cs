using System.Text.Json;
using Cgm.Core.Model;
using Cgm.Core.Battle;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class MoveConformanceNormalizerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cgm-move-normalize-" + Guid.NewGuid().ToString("N"));

    public MoveConformanceNormalizerTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Build_NormalizesCompilesHashesAndPromotesThroughTheManifest()
    {
        WriteMove("neutral_wave", 51);
        var decisions = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"], [new("move-0051", false)]);

        MoveConformanceCatalog definitions = MoveConformanceNormalizer.Build(_folder, decisions);
        MoveCorpusManifest manifest = MoveCorpusAuditor.Build(_folder, definitions);
        MoveConformanceRecord definition = Assert.Single(definitions.Entries);
        MoveCorpusEntry entry = Assert.Single(manifest.Entries);

        Assert.Equal(MoveTarget.AllOpponents, definition.Mechanics.Target);
        Assert.Equal(DamageClass.Special, definition.Mechanics.DamageClass);
        Assert.Contains(definition.Mechanics.Effects, effect => effect.Op == "damage");
        Assert.Contains(definition.Mechanics.Effects, effect => effect.Op == "statStage");
        Assert.Equal(MoveConformanceStatus.Certified, entry.Status);
        Assert.Equal(definition.NormalizedDefinitionHash, entry.NormalizedDefinitionHash);
        Assert.Equal(definition.TestIds, entry.TestIds);
        Assert.DoesNotContain("neutral_wave", MoveConformanceNormalizer.Serialize(definitions), StringComparison.Ordinal);
        string output = Path.Combine(_folder, "definitions.txt");
        MoveConformanceNormalizer.Write(definitions, output);
        Assert.DoesNotContain('\r', File.ReadAllText(output));
    }

    [Fact]
    public void Build_RejectsDuplicateMissingAndStaleDecisions()
    {
        string path = WriteMove("neutral_wave", 51);
        var duplicate = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"], [new("move-0051", false), new("move-0051", false)]);
        Assert.Throws<InvalidDataException>(() => MoveConformanceNormalizer.Build(_folder, duplicate));

        var missing = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"], [new("move-9999", false)]);
        Assert.Throws<InvalidDataException>(() => MoveConformanceNormalizer.Build(_folder, missing));

        var valid = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"], [new("move-0051", false)]);
        MoveConformanceCatalog definitions = MoveConformanceNormalizer.Build(_folder, valid);
        File.AppendAllText(path, " ");
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => MoveCorpusAuditor.Build(_folder, definitions));
        Assert.Contains("do not match", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsMissingEvidenceAndDuplicateCorpusKeys()
    {
        WriteMove("neutral_wave", 51);
        var noEvidence = new MoveConformanceDecisionCatalog(1, [], [new("move-0051", false)]);
        Assert.Throws<InvalidDataException>(() => MoveConformanceNormalizer.Build(_folder, noEvidence));

        WriteMove("neutral_echo", 51);
        var valid = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"], [new("move-0051", false)]);
        Assert.Throws<InvalidDataException>(() => MoveConformanceNormalizer.Build(_folder, valid));
    }

    [Fact]
    public void Build_RegistersFormulaVectorsAndRejectsUnownedSinglesRows()
    {
        WriteMove("neutral_formula", 900, "selected-pokemon");
        var formula = new Effect
        {
            Op = "targetHpThresholdPower",
            Params = new Dictionary<string, JsonElement>
            {
                ["thresholdNum"] = JsonSerializer.SerializeToElement(1),
                ["thresholdDen"] = JsonSerializer.SerializeToElement(2),
                ["multiplierNum"] = JsonSerializer.SerializeToElement(2),
                ["multiplierDen"] = JsonSerializer.SerializeToElement(1),
            },
        };
        var decisions = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"],
            [new("move-0900", false, [formula])]);

        MoveConformanceRecord record = Assert.Single(MoveConformanceNormalizer.Build(_folder, decisions).Entries);
        Assert.Equal(["HpStatusFormulaConformanceTests.Certified(move-0900)"], record.TestIds);

        WriteMove("neutral_speed_formula", 901, "selected-pokemon", power: null);
        var speedFormula = new Effect
        {
            Op = "speedRatioPower",
            Params = new Dictionary<string, JsonElement>
            {
                ["numerator"] = JsonSerializer.SerializeToElement("user"),
                ["denominator"] = JsonSerializer.SerializeToElement("target"),
                ["bands"] = JsonSerializer.SerializeToElement("0:40,1:60"),
            },
        };
        var speedDecisions = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"],
            [new("move-0901", false, [speedFormula])]);
        record = Assert.Single(MoveConformanceNormalizer.Build(_folder, speedDecisions).Entries);
        Assert.Equal(["PhysicalMetricFormulaConformanceTests.Certified(move-0901)"], record.TestIds);

        var unowned = new MoveConformanceDecisionCatalog(1, ["neutral-test-evidence"], [new("move-0900", false)]);
        Assert.Throws<InvalidDataException>(() => MoveConformanceNormalizer.Build(_folder, unowned));
    }

    private string WriteMove(string name, int id, string target = "all-opponents", int? power = 40)
    {
        string powerJson = power?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        string json = $$"""
        {
          "content_hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "endpoint": "move",
          "payload": {
            "id": {{id}},
            "name": "{{name}}",
            "type": { "name": "neutral", "url": "https://example.invalid/type/1/" },
            "damage_class": { "name": "special" },
            "power": {{powerJson}},
            "accuracy": 100,
            "pp": 20,
            "priority": 0,
            "target": { "name": "{{target}}" },
            "stat_changes": [{ "change": -1, "stat": { "name": "special-defense" } }],
            "meta": {
              "ailment": { "name": "none" },
              "ailment_chance": 0,
              "category": { "name": "damage+lower" },
              "crit_rate": 0,
              "drain": 0,
              "flinch_chance": 0,
              "healing": 0,
              "max_hits": null,
              "min_hits": null,
              "stat_chance": 10
            }
          }
        }
        """;
        string path = Path.Combine(_folder, name + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
