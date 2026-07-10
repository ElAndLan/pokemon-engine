using System.Text;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class MoveCorpusAuditorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cgm-move-audit-" + Guid.NewGuid().ToString("N"));

    public MoveCorpusAuditorTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void Build_IsDeterministicSortedAndSanitized()
    {
        WriteMove("neutral_beta", 20, power: null, target: "user", meta: "\"healing\":50");
        WriteMove("neutral_alpha", 3, power: 50, target: "selected-pokemon",
            meta: "\"ailment\":{\"name\":\"burn\"},\"flinch_chance\":10");

        MoveCorpusManifest first = MoveCorpusAuditor.Build(_folder);
        MoveCorpusManifest second = MoveCorpusAuditor.Build(_folder);
        string json = MoveCorpusAuditor.Serialize(first);

        Assert.Equal(1, first.FormatVersion);
        Assert.Equal(2, first.FileCount);
        Assert.Equal([3, 20], first.Entries.Select(e => e.SourceId));
        Assert.Equal("move-0003", first.Entries[0].ReferenceKey);
        Assert.Equal(["ailment", "flinch", "standardDamage"], first.Entries[0].ObservedMechanicFamilies);
        Assert.Equal(["heal"], first.Entries[1].ObservedMechanicFamilies);
        Assert.All(first.Entries, e => Assert.Equal(MoveConformanceStatus.InventoryOnly, e.Status));
        Assert.Equal(2, first.StatusCounts.Single(c => c.Status == MoveConformanceStatus.InventoryOnly).Count);
        Assert.Equal(0, first.StatusCounts.Single(c => c.Status == MoveConformanceStatus.Certified).Count);
        Assert.Equal(MoveCorpusAuditor.Serialize(second), json);
        Assert.DoesNotContain("neutral_alpha", json, StringComparison.Ordinal);
        Assert.DoesNotContain("neutral_beta", json, StringComparison.Ordinal);
        Assert.DoesNotContain("selected-pokemon.json", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DigestChangesWhenFileBytesChange()
    {
        string path = WriteMove("neutral_alpha", 3, power: 50);
        string before = MoveCorpusAuditor.Build(_folder).CorpusDigest;

        File.AppendAllText(path, " ", Encoding.UTF8);

        string after = MoveCorpusAuditor.Build(_folder).CorpusDigest;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Build_RejectsDuplicateSourceIds()
    {
        WriteMove("neutral_alpha", 3, power: 50);
        WriteMove("neutral_beta", 3, power: 60);

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => MoveCorpusAuditor.Build(_folder));
        Assert.Contains("duplicate source id 3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsFilenameNameMismatch()
    {
        WriteMove("neutral_alpha", 3, power: 50);
        File.Move(Path.Combine(_folder, "neutral_alpha.json"), Path.Combine(_folder, "wrong.json"));

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => MoveCorpusAuditor.Build(_folder));
        Assert.Contains("filename must match payload.name", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsEmptyCorpus()
    {
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => MoveCorpusAuditor.Build(_folder));
        Assert.Contains("contains no JSON files", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsNonMoveEndpoint()
    {
        string path = WriteMove("neutral_alpha", 3, power: 50);
        File.WriteAllText(path, File.ReadAllText(path).Replace(
            "\"endpoint\": \"move\"", "\"endpoint\": \"ability\"", StringComparison.Ordinal));

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => MoveCorpusAuditor.Build(_folder));
        Assert.Contains("endpoint must be 'move'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsInvalidPayloadHash()
    {
        string path = WriteMove("neutral_alpha", 3, power: 50);
        File.WriteAllText(path, File.ReadAllText(path).Replace(
            new string('a', 64), "not-a-hash", StringComparison.Ordinal));

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => MoveCorpusAuditor.Build(_folder));
        Assert.Contains("content_hash must be a 64-character hexadecimal SHA-256", ex.Message, StringComparison.Ordinal);
    }

    private string WriteMove(string name, int id, int? power, string target = "selected-pokemon", string meta = "")
    {
        string json = $$"""
        {
          "content_hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "endpoint": "move",
          "payload": {
            "id": {{id}},
            "name": "{{name}}",
            "power": {{(power?.ToString() ?? "null")}},
            "target": { "name": "{{target}}" },
            "stat_changes": [],
            "meta": { {{meta}} }
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
